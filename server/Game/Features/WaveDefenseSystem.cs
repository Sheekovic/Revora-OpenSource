using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Revora.Client;
using Revora.Database;
using Revora.Interfaces;
using Revora.Network.GamePackets;

namespace Revora.Game.Features
{
    public static class WaveDefenseSystem
    {
        public const uint NpcId = 900000007;
        public const uint MedicNpcId = 900000008;
        public const string NpcName = "WaveWarden";
        public const int MaxTeams = 8;
        public const int MinPlayers = 2;
        public const int MaxPlayers = 4;
        public const int TeamSize = 4;
        public const int TeamLives = 4;
        public const string ClaimKey = "wave-claim";
        public const ushort TwinCityMap = 1002;
        public const ushort TwinCityX = 408;
        public const ushort TwinCityY = 330;
        public const ushort ClaimX = 370;
        public const ushort ClaimY = 295;
        public const ushort Mesh = 50610;
        public const ushort MedicMesh = 30;
        public const int MedicRange = 5;
        public const int PeelRange = 8;

        private static readonly int[] StartHours = new int[] { 0, 2, 4, 6, 8, 10, 12, 14, 16, 18, 20, 22 };
        private static readonly TimeSpan RegisterWindow = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan FightLimit = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan MedicInterval = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan MedicWindow = TimeSpan.FromSeconds(4);
        private static readonly object Gate = new object();
        private static readonly List<Instance> Live = new List<Instance>();
        private static readonly Dictionary<ushort, Instance> ByMap = new Dictionary<ushort, Instance>();
        private static readonly HashSet<uint> RegisteredLeaders = new HashSet<uint>();
        private static readonly HashSet<uint> PendingReturns = new HashSet<uint>();
        private static readonly uint[] WaveMobs = new uint[] { 2, 7, 10, 11, 12, 15, 18 };
        private const uint MiniBossId = 8306;
        private const uint NamedBossId = 31;
        private static uint NextAllyUid = 1310001;
        private static DateTime CycleStart;
        private static bool RegisterOpen;
        private static bool Invited;

        private sealed class Post
        {
            public Entity Body;
            public uint MaxHp;
        }

        private sealed class Instance
        {
            public readonly Guid ResultId = Guid.NewGuid();
            public uint[] Participants;
            public int PendingKind = -1;
            public HashSet<uint> PendingEligible;
            public string PendingReason;
            public bool Abort;
            public uint LeaderUid;
            public bool OwnedArena;
            public bool Closed;
            public Map Arena;
            public List<GameState> Players = new List<GameState>();
            public List<GameState> Allies = new List<GameState>();
            public readonly Dictionary<uint, CompanionNavigation> AllyRoutes = new Dictionary<uint, CompanionNavigation>();
            public readonly Dictionary<uint, DateTime> AllyActions = new Dictionary<uint, DateTime>();
            public readonly Dictionary<uint, CompanionNavigation> MonsterRoutes = new Dictionary<uint, CompanionNavigation>();
            public readonly Dictionary<uint, DateTime> MonsterActions = new Dictionary<uint, DateTime>();
            public Post[] Posts = new Post[3];
            public int Lives = TeamLives;
            public int Wave;
            public DateTime WaveUntil;
            public DateTime NextWaveAt;
            public DateTime FightUntil;
            public DateTime NextMedic;
            public DateTime MedicUntil;
            public DateTime Started;
            public ushort MedicX;
            public ushort MedicY;
            public bool Won;
            public bool Failed;
            public Dictionary<uint, uint> MonsterPost = new Dictionary<uint, uint>();
        }

        public static void Create()
        {
            InitializePrizes();
            WaveAdmissions.Initialize();
            WaveAdmissions.RecoverInterrupted();
            INpc existing = null;
            Map from = null;
            foreach (Map map in Kernel.Maps.Values)
            {
                if (map == null || map.Npcs == null) continue;
                if (map.Npcs.TryGetValue(NpcId, out existing))
                {
                    from = map;
                    break;
                }
            }
            if (existing != null && from != null)
            {
                from.RemoveNpc(existing);
                if (Map.SecoundNpc.ContainsKey(NpcId))
                    Map.SecoundNpc.Remove(NpcId);
            }
            CityGuardSystem.SpawnTalker(NpcId, TwinCityMap, ClaimX, ClaimY, Mesh, NpcName, (byte)Enums.ConquerAngle.South);
            try
            {
                using (var cmd = new MySqlCommand(MySqlCommandType.UPDATE))
                    cmd.Update("npcs").Set("mapid", TwinCityMap).Set("cellx", ClaimX).Set("celly", ClaimY).Set("lookface", Mesh).Where("id", NpcId).Execute();
            }
            catch
            {
            }
            if (Kernel.Maps.ContainsKey(TwinCityMap) && Kernel.Maps[TwinCityMap].Npcs.ContainsKey(NpcId))
                Console.WriteLine(NpcName + " at Twin City " + ClaimX + "," + ClaimY);
            CycleStart = NextStart(DateTime.Now);
        }

        public static DateTime NextStart(DateTime now)
        {
            DateTime today = now.Date;
            for (int i = 0; i < StartHours.Length; i++)
            {
                DateTime slot = today.AddHours(StartHours[i]).AddMinutes(30);
                if (now < slot)
                    return slot;
            }
            return today.AddDays(1).AddHours(StartHours[0]).AddMinutes(30);
        }

        public static bool InEvent(Entity entity)
        {
            if (entity == null) return false;
            lock (Gate)
                return ByMap.ContainsKey(entity.MapID);
        }

        public static bool BlocksHeals(Entity entity)
        {
            return InEvent(entity);
        }

        public static bool BlocksHealSpell(GameState client, ushort spellId)
        {
            if (client == null || client.Entity == null || !InEvent(client.Entity))
                return false;
            return spellId == 1005 || spellId == 1055 || spellId == 1170 || spellId == 1175
                || spellId == 1190 || spellId == 1050 || spellId == 1100;
        }

        public static bool IsBanner(Entity entity)
        {
            return entity != null && entity.Name == "WavePost";
        }

        public static bool BlocksPostAttack(Entity attacker, Entity target)
        {
            if (target == null) return false;
            Instance inst;
            lock (Gate)
                if (!ByMap.TryGetValue(target.MapID, out inst)) return false;
            lock (inst)
            {
                if (!inst.Posts.Any(p => p != null && ReferenceEquals(p.Body, target))) return false;
                Entity current;
                return inst.Closed || inst.Won || inst.Failed || inst.Abort || inst.PendingKind >= 0
                    || attacker == null || attacker.Dead || attacker.EntityFlag != EntityFlag.Monster
                    || attacker.MapID != inst.Arena.ID || !inst.MonsterPost.ContainsKey(attacker.UID)
                    || !inst.Arena.Entities.TryGetValue(attacker.UID, out current) || !ReferenceEquals(current, attacker);
            }
        }

        public static bool SteerMonster(Entity monster)
        {
            if (monster == null || monster.MonsterInfo == null) return false;
            Instance inst;
            lock (Gate)
                if (!ByMap.TryGetValue(monster.MapID, out inst)) return false;
            lock (inst) SelectMonsterTarget(inst, monster);
            return true;
        }

        public static Entity ResolveTarget(Entity monster)
        {
            if (monster == null || monster.MonsterInfo == null) return null;
            Instance inst;
            lock (Gate)
                if (!ByMap.TryGetValue(monster.MapID, out inst)) return null;
            lock (inst) return SelectMonsterTarget(inst, monster);
        }

        private static Entity SelectMonsterTarget(Instance inst, Entity monster)
        {
            monster.MonsterInfo.InSight = 0;
            Entity current;
            uint postUid;
            if (inst.Closed || inst.Abort || inst.Won || inst.Failed || inst.PendingKind >= 0
                || monster.Dead || monster.Killed || IsBanner(monster)
                || !inst.MonsterPost.TryGetValue(monster.UID, out postUid)
                || !inst.Arena.Entities.TryGetValue(monster.UID, out current) || !ReferenceEquals(current, monster))
                return null;
            Entity target = NearestPlayer(inst, monster, PeelRange);
            if (target == null)
            {
                int distance = int.MaxValue;
                foreach (Post post in inst.Posts)
                {
                    if (post == null || post.Body == null || post.Body.Dead) continue;
                    // Keep the assigned post while it stands; otherwise choose the nearest survivor.
                    if (post.Body.UID == postUid) { target = post.Body; break; }
                    int d = Kernel.GetDistance(monster.X, monster.Y, post.Body.X, post.Body.Y);
                    if (d < distance) { target = post.Body; distance = d; }
                }
                if (target != null) inst.MonsterPost[monster.UID] = target.UID;
            }
            if (target != null) monster.MonsterInfo.InSight = target.UID;
            return target;
        }

        public static void Tick()
        {
            ProcessReturns();
            if (CycleStart == default(DateTime)) return;
            DateTime now = DateTime.Now;
            if (!Invited && now >= CycleStart.Subtract(RegisterWindow) && now < CycleStart)
            {
                Invited = true;
                RegisterOpen = true;
                Kernel.SendWorldMessage(new Message("Wave Defense registration is open for 5 minutes. Teams of 2-4. Talk to WaveWarden in Twin City (" + ClaimX + ", " + ClaimY + ").", Color.Yellow, Message.Center), Server.GamePool);
                InviteAll();
            }
            if (RegisterOpen && now >= CycleStart)
                RegisterOpen = false;
            if (now >= CycleStart.AddMinutes(20))
            {
                Invited = false;
                CycleStart = NextStart(now.AddSeconds(2));
            }
            List<Instance> snapshot;
            lock (Gate)
                snapshot = new List<Instance>(Live);
            for (int i = 0; i < snapshot.Count; i++)
                TickInstance(snapshot[i], now);
        }

        public static bool HandleNpc(GameState client, NpcRequest request)
        {
            if (client == null || request == null || client.Entity == null)
                return false;
            if (client.ActiveNpc == MedicNpcId)
            {
                Npcs medic = new Npcs(client);
                medic.Text("I pulse every 30 seconds. Stand close when the puff appears. That is the only heal here.");
                medic.Option("Alright.", 255);
                medic.Send();
                return true;
            }
            if (client.ActiveNpc != NpcId)
                return false;
            Npcs dialog = new Npcs(client);
            switch (request.OptionID)
            {
                case 0:
                    dialog.Text(StatusText());
                    if (RegisterOpen)
                        dialog.Option("Register my team", 1);
                    if (HasClaim(client))
                        dialog.Option("Claim our prize", 2);
                    dialog.Option("Goodbye", 255);
                    dialog.Send();
                    return true;
                case 1:
                    dialog.Text(TryRegister(client));
                    dialog.Option("Alright.", 255);
                    dialog.Send();
                    return true;
                case 2:
                    dialog.Text(TryClaim(client));
                    dialog.Option("Alright.", 255);
                    dialog.Send();
                    return true;
            }
            return true;
        }

        public static void OnDeath(GameState client)
        {
            if (client == null || client.Entity == null) return;
            Instance inst = FindByPlayer(client.Entity.UID);
            if (inst == null) return;
            lock (inst)
            {
            if (inst.Won || inst.Failed || inst.Abort || inst.PendingKind >= 0) return;
            // Entity death dispatch also runs after DropRandomStuff. The first
            // callback revives the defender; the later callback must not spend another life.
            if (!client.Entity.Dead || client.Entity.MapID != inst.Arena.ID
                || !inst.Players.Any(p => ReferenceEquals(p, client))) return;
            lock (Gate)
            {
                inst.Lives--;
                if (inst.Lives < 0) inst.Lives = 0;
            }
            Tell(inst, client.Entity.Name + " fell. Team lives: " + inst.Lives + ".");
            if (inst.Lives <= 0)
            {
                Fail(inst, "The team is out of lives.");
                return;
            }
            ReviveAtMedic(inst, client);
            }
        }

        public static void OnDisconnect(GameState client)
        {
            if (client == null || client.Entity == null) return;
            Instance inst = FindByPlayer(client.Entity.UID);
            if (inst == null) return;
            lock (inst)
            {
            lock (Gate)
            {
                inst.Players.RemoveAll(p => p != null && p.Entity != null && p.Entity.UID == client.Entity.UID);
                PendingReturns.Add(client.Entity.UID);
            }
            if (inst.Players.Count == 0)
                Fail(inst, "The team left the field.");
            }
        }

        public static void OnLogin(GameState client)
        {
            if (!QuestInventory.Player(client, false)) return;
            OnDisconnect(client);
            lock (Gate)
            {

                {
                    if (WaveAdmissions.HasPendingReturn(client.Entity.UID)) PendingReturns.Add(client.Entity.UID);
                    else PendingReturns.Remove(client.Entity.UID);
                }
            }
            ProcessReturns();
            lock (Gate)
                if (PendingReturns.Contains(client.Entity.UID))
                    QuestInventory.Fail(client, "wave-login-return", new InvalidOperationException("Wave Defense return is pending; reconnect."));
        }

        private static void ProcessReturns()
        {
            lock (Gate)
            {
                var pending = new List<uint>(PendingReturns);
                for (int i = 0; i < Math.Min(MaxTeams * MaxPlayers, pending.Count); i++)
                {
                    uint uid = pending[i];
                    try
                    {
                        WaveAdmissions.Forfeit(uid);
                        GameState p;
                        // Offline owners discover their durable return at login.
                        if (!Kernel.GamePool.TryGetValue(uid, out p) || p.Entity.PersistenceRequiresReload || WaveAdmissions.Return(p))
                            PendingReturns.Remove(uid);
                    }
                    catch (Exception ex) { Console.WriteLine("[wave-return] error=" + ex.GetType().Name); }
                }
            }
        }

        private static string StatusText()
        {
            if (RegisterOpen)
                return "Registration is open until " + CycleStart.ToString("HH:mm") + ". Teams of 2-4. Empty slots become [Adv] allies. Hold three posts through 8 waves. The Warden Medic is the only heal.";
            return "Next Wave Defense " + CycleStart.ToString("HH:mm") + ". Even hours at :30. Register 5 minutes before. 2-4 players. Private map. 4 team lives. Medic every 30 seconds.";
        }

        private static void InviteAll()
        {
            foreach (GameState client in Kernel.GamePool.Values)
            {
                if (client == null || client.Entity == null || client.Entity.Dead || client.Fake) continue;
                GameState captured = client;
                captured.MessageOK = p => { TryRegister(p); };
                captured.Send(new NpcReply(NpcReply.MessageBox, "Wave Defense is open. Teams of 2-4. Hold three posts through 8 waves. Register at WaveWarden (" + ClaimX + ", " + ClaimY + ")?"));
                captured.Send(new Data(true) { UID = captured.Entity.UID, dwParam = 60, ID = Data.CountDown });
            }
        }

        private static string TryRegister(GameState client)
        {
            if (!QuestInventory.Player(client)) return "Unable to register.";
            if (!RegisterOpen) return "Registration is closed.";
            if (client.Team == null || client.Team.Lider == null)
                return "Form a team of 2 to 4 first. The leader registers everyone.";
            if (client.Entity.UID != client.Team.Lider.Entity.UID)
                return "Only the team leader can register.";
            List<GameState> members = new List<GameState>();
            foreach (GameState mate in client.Team.Players)
            {
                if (mate == null || mate.Entity == null) continue;
                members.Add(mate);
            }
            if (members.Count < MinPlayers || members.Count > MaxPlayers)
                return "Bring 2 to 4 living teammates.";
            for (int i = 0; i < members.Count; i++)
            {
                if (!QuestInventory.Player(members[i]) || members[i].Booth != null || members[i].Trade == null || members[i].Trade.InTrade)
                    return "Your teammates must be ready before entering.";
                if (members[i].Entity.Dead) return members[i].Entity.Name + " is dead.";
                if (members[i].Entity.MapID != TwinCityMap) return "Everyone must stand in Twin City.";
                if (Kernel.GetDistance(members[i].Entity.X, members[i].Entity.Y, client.Entity.X, client.Entity.Y) > 18)
                    return members[i].Entity.Name + " is too far from you.";
                if (InEvent(members[i].Entity)) return members[i].Entity.Name + " is already in an instance.";
            }
            lock (Gate)
            {
                if (Live.Count >= MaxTeams) return "All fields are full (8 teams).";
                if (RegisteredLeaders.Contains(client.Entity.UID)) return "Your team is already registered.";
                members = members.OrderBy(p => p.Entity.UID == client.Entity.UID ? 0 : 1).ToList();
                if (!BeginInstance(members)) return "Your field is not ready. Please try again.";
                RegisteredLeaders.Add(client.Entity.UID);
            }
            return "Your team is in. Hold the three posts. The Medic pulses every 30 seconds. Four lives for the whole team.";
        }

        private static bool BeginInstance(List<GameState> members)
        {
            if (members.Count < MinPlayers || members.Count > MaxPlayers || members.Any(p => !QuestInventory.Player(p))) return false;
            var locked = new List<GameState>();
            try
            {
                foreach (var p in members.OrderBy(p => p.Entity.UID))
                {
                    if (!System.Threading.Monitor.TryEnter(p.ItemSyncRoot)) return false;
                    locked.Add(p);
                }
                return BeginInstanceCore(members);
            }
            catch (Exception ex) { Console.WriteLine("[wave-map] error=" + ex.GetType().Name); return false; }
            finally { foreach (var p in locked) System.Threading.Monitor.Exit(p.ItemSyncRoot); }
        }
        private static bool BeginInstanceCore(List<GameState> members)
        {
            Map source = null;
            if (Kernel.Maps.ContainsKey(700))
                source = Kernel.Maps[700];
            else if (Kernel.Maps.ContainsKey(1005))
                source = Kernel.Maps[1005];
            else if (Kernel.Maps.ContainsKey(1002))
                source = Kernel.Maps[1002];
            if (source == null) return false;
            // This event owns its posts, medic and waves; ambient catalog spawns
            // and portals must not be loaded into its private arena.
            Map arena = Map.CreateDynamic(source.ID, source.Path, false);
            if (arena == null) return false;
            Instance inst = new Instance();
            inst.Arena = arena;
            inst.OwnedArena = true;
            inst.Players.AddRange(members);
            inst.Participants = members.Select(p => p.Entity.UID).Distinct().OrderBy(uid => uid).ToArray();
            inst.LeaderUid = members[0].Entity.UID;
            try { WaveAdmissions.Begin(inst.ResultId.ToString("N"), arena.ID, members.ToArray()); }
            catch (Exception ex) { arena.Dispose(); Console.WriteLine("[wave-admission] error=" + ex.GetType().Name); return false; }
            if (!Constants.PKFreeMaps.Contains(arena.ID)) Constants.PKFreeMaps.Add(arena.ID);
            Live.Add(inst); ByMap[arena.ID] = inst;
            try
            {
            inst.Started = DateTime.Now;
            inst.FightUntil = inst.Started.Add(FightLimit);
            inst.NextMedic = inst.Started.AddSeconds(8);
            inst.Wave = 0;
            Tuple<ushort, ushort> spawn = arena.RandomCoordinates();
            inst.MedicX = spawn.Item1;
            inst.MedicY = spawn.Item2;
            PlaceMedic(inst);
            PlacePosts(inst);
            int need = TeamSize - members.Count;
            for (int i = 0; i < need; i++)
            {
                GameState ally = SpawnAlly(arena, inst.MedicX, inst.MedicY);
                if (ally != null)
                    inst.Allies.Add(ally);
            }
            for (int i = 0; i < members.Count; i++)
            {
                ushort x = inst.MedicX;
                ushort y = inst.MedicY;
                arena.SelectCoordonates(ref x, ref y);
                members[i].Entity.Teleport(arena.BaseID, arena.ID, x, y);
                if (members[i].Entity.MapID != arena.ID) throw new InvalidOperationException("Wave transfer failed.");
                members[i].Send(new Message("Hold the three posts through 8 waves. Player heals are sealed. Stand by the Medic when the puff appears. Team lives: 4.", Color.Yellow, Message.TopLeft));
            }
            Kernel.SendWorldMessage(new Message(members[0].Entity.Name + "'s team entered Wave Defense.", Color.Orange, Message.Center), Server.GamePool);
            StartWave(inst, 1);
            return !inst.Abort;
            }
            catch (Exception ex) { inst.Abort = true; Console.WriteLine("[wave-entry] error=" + ex.GetType().Name); return false; }
        }

        private static void PlaceMedic(Instance inst)
        {
            INpc old;
            if (inst.Arena.Npcs.TryGetValue(MedicNpcId, out old) && old != null)
            {
                inst.Arena.RemoveNpc(old);
                if (Map.SecoundNpc.ContainsKey(MedicNpcId))
                    Map.SecoundNpc.Remove(MedicNpcId);
            }
            CityGuardSystem.SpawnTalker(MedicNpcId, inst.Arena.ID, inst.MedicX, inst.MedicY, MedicMesh, "WardenMedic", (byte)Enums.ConquerAngle.South);
        }

        private static void PlacePosts(Instance inst)
        {
            Tuple<ushort, ushort> a = inst.Arena.RandomCoordinates();
            ushort x2 = (ushort)Math.Min(a.Item1 + 18, inst.Arena.Floor.Bounds.Width - 2);
            ushort y2 = a.Item2;
            ushort x3 = a.Item1;
            ushort y3 = (ushort)Math.Min(a.Item2 + 18, inst.Arena.Floor.Bounds.Height - 2);
            inst.Arena.SelectCoordonates(ref x2, ref y2);
            inst.Arena.SelectCoordonates(ref x3, ref y3);
            inst.Posts[0] = SpawnPost(inst, a.Item1, a.Item2);
            inst.Posts[1] = SpawnPost(inst, x2, y2);
            inst.Posts[2] = SpawnPost(inst, x3, y3);
        }

        private static Post SpawnPost(Instance inst, ushort x, ushort y)
        {
            MonsterInformation mt = FirstMob();
            Entity body = new Entity(EntityFlag.Monster, false);
            body.MapObjType = MapObjectType.Monster;
            if (mt != null)
            {
                body.MonsterInfo = mt.Copy();
                body.MonsterInfo.Owner = body;
                body.MonsterInfo.Guard = false;
                body.MonsterInfo.ViewRange = 0;
                body.MonsterInfo.BoundX = x;
                body.MonsterInfo.BoundY = y;
            }
            body.Name = "WavePost";
            body.Body = 9307;
            body.Level = 1;
            body.UID = inst.Arena.EntityUIDCounter.Next;
            body.MapID = inst.Arena.ID;
            body.X = x;
            body.Y = y;
            body.MaxHitpoints = 12000;
            body.Hitpoints = 12000;
            body.MinAttack = 0;
            body.MaxAttack = 0;
            body.SendUpdates = true;
            inst.Arena.AddEntity(body);
            BroadcastEntity(inst, body);
            Post post = new Post();
            post.Body = body;
            post.MaxHp = body.MaxHitpoints;
            return post;
        }

        private static MonsterInformation FirstMob()
        {
            if (MonsterInformation.MonsterInformations.ContainsKey(2))
                return MonsterInformation.MonsterInformations[2];
            foreach (MonsterInformation mt in MonsterInformation.MonsterInformations.Values)
                return mt;
            return null;
        }

        private static void StartWave(Instance inst, int wave)
        {
            try
            {
            if (wave < 1 || wave > 8) throw new InvalidOperationException("Wave number is invalid.");
            uint mob = WaveMobs[(wave - 1) % WaveMobs.Length];
            if (!MonsterInformation.MonsterInformations.ContainsKey(mob)
                || wave == 4 && !MonsterInformation.MonsterInformations.ContainsKey(MiniBossId)
                || wave == 8 && !MonsterInformation.MonsterInformations.ContainsKey(NamedBossId))
                throw new InvalidOperationException("Wave monster catalog is incomplete.");
            var standing=inst.Posts.Where(p=>p!=null && p.Body!=null && !p.Body.Dead).ToArray();
            if(standing.Length==0)throw new InvalidOperationException("Wave has no surviving posts.");
            inst.Wave = wave;
            inst.NextWaveAt=default(DateTime);
            int seconds = (wave == 4 || wave == 8) ? 90 : 70;
            if (wave == 8) seconds = 120;
            inst.WaveUntil = DateTime.Now.AddSeconds(seconds);
            int packs = 6 + wave;
            if (wave >= 5) packs = 10 + wave;
            for (int i = 0; i < packs; i++)
                SpawnWaveMob(inst, mob, 0, standing[i % standing.Length]);
            if (wave == 4)
                SpawnWaveMob(inst, MiniBossId, 25000, standing[Kernel.Random.Next(standing.Length)]);
            if (wave == 8)
                SpawnWaveMob(inst, NamedBossId, 60000, standing[Kernel.Random.Next(standing.Length)]);
            Tell(inst, "Wave " + wave + " of 8. Hold the posts.");
            }
            catch (Exception ex) { inst.Abort = true; Console.WriteLine("[wave-spawn] wave=" + wave + " error=" + ex.GetType().Name); }
        }

        private static void SpawnWaveMob(Instance inst, uint mobId, uint hpOverride, Post post)
        {
            if (post == null || post.Body == null) throw new InvalidOperationException("Wave target post is missing.");
            if (!MonsterInformation.MonsterInformations.ContainsKey(mobId))
                throw new InvalidOperationException("Wave monster is missing.");
            MonsterInformation mt = MonsterInformation.MonsterInformations[mobId];
            Tuple<ushort, ushort> xy = inst.Arena.RandomCoordinates();
            Entity entity = new Entity(EntityFlag.Monster, false);
            entity.MapObjType = MapObjectType.Monster;
            entity.MonsterInfo = mt.Copy();
            entity.MonsterInfo.Owner = entity;
            entity.MonsterInfo.BoundX = post.Body.X;
            entity.MonsterInfo.BoundY = post.Body.Y;
            entity.Name = mt.Name;
            entity.MinAttack = mt.MinAttack;
            entity.MaxAttack = entity.MagicAttack = mt.MaxAttack;
            uint hp = hpOverride > 0 ? hpOverride : mt.Hitpoints;
            if (hp < 200) hp = 400;
            entity.Hitpoints = entity.MaxHitpoints = hp;
            entity.Body = mt.Mesh;
            entity.Level = mt.Level;
            entity.ApplyMonsterBossHud();
            entity.UID = inst.Arena.EntityUIDCounter.Next;
            entity.MapID = inst.Arena.ID;
            entity.X = xy.Item1;
            entity.Y = xy.Item2;
            entity.SendUpdates = true;
            inst.Arena.AddEntity(entity);
            lock (Gate)
                inst.MonsterPost[entity.UID] = post.Body.UID;
            entity.MonsterInfo.InSight = post.Body.UID;
            BroadcastEntity(inst, entity);
        }

        private static GameState SpawnAlly(Map arena, ushort x, ushort y)
        {
            uint uid = NextAllyUid++;
            GameState gs = new GameState(null);
            gs.Fake = true;
            gs.Account = new AccountTable(null);
            gs.Account.EntityID = uid;
            gs.ReadyToPlay();
            gs.Spells = new SafeDictionary<ushort, ISkill>();
            gs.Proficiencies = new SafeDictionary<ushort, IProf>();
            gs.Entity = new Entity(EntityFlag.Player, false);
            gs.Entity.Owner = gs;
            gs.Entity.UID = uid;
            gs.Entity.Name = "[Adv] Ward" + (uid % 100);
            gs.Entity.Face = 98;
            gs.Entity.Body = 1003;
            gs.Entity.HairStyle = 315;
            gs.Entity.Level = 120;
            gs.Entity.Reborn = 1;
            gs.Entity.Class = 15;
            gs.Entity.PKMode = Enums.PKMode.Capture;
            gs.Entity.MaxHitpoints = 8000;
            gs.Entity.Hitpoints = 8000;
            gs.Entity.MinAttack = 400;
            gs.Entity.MaxAttack = 700;
            gs.Entity.Defence = 500;
            gs.Entity.MapObjType = MapObjectType.Player;
            gs.Attackable = true;
            gs.Entity.X = x;
            gs.Entity.Y = y;
            gs.BindWorldMap(arena.ID);
            gs.Entity.SendUpdates = true;
            if (!SessionInstall.TryInstallFake(uid, gs)) return null;
            if (gs.Map != null)
                gs.Map.AddEntity(gs.Entity);
            return gs;
        }

        private static void TickInstance(Instance inst, DateTime now)
        {
            lock(inst)TickInstanceCore(inst,now);
        }
        private static void TickInstanceCore(Instance inst, DateTime now)
        {
            if (inst.Won || inst.Failed || inst.Abort) { Close(inst); return; }
            if (inst.PendingKind >= 0) { Complete(inst, false, inst.PendingReason); return; }
            if (now >= inst.FightUntil)
            {
                Fail(inst, "Time is up.");
                return;
            }
            int standing = 0;
            for (int p = 0; p < inst.Posts.Length; p++)
            {
                if (inst.Posts[p] != null && inst.Posts[p].Body != null && inst.Posts[p].Body.Hitpoints > 0)
                    standing++;
            }
            if (standing == 0)
            {
                Fail(inst, "All three posts have fallen.");
                return;
            }
            if (now >= inst.NextMedic)
            {
                inst.MedicUntil = now.Add(MedicWindow);
                inst.NextMedic = now.Add(MedicInterval);
                PulseMedic(inst);
            }
            DriveAllies(inst, now);
            DriveMonsters(inst, now);
            if (inst.Closed || inst.Won || inst.Failed || inst.Abort || inst.PendingKind >= 0) return;
            standing = inst.Posts.Count(p => p != null && p.Body != null && !p.Body.Dead);
            if (standing == 0) { Fail(inst, "All three posts have fallen."); return; }
            SweepDeadMobs(inst);
            if (inst.Wave < 8 && RemainingMobs(inst) == 0)
            {
                if(inst.NextWaveAt==default(DateTime)) {
                    inst.NextWaveAt=now.AddSeconds(8);
                    foreach(var post in inst.Posts)if(post!=null && post.Body!=null)RecoverBetweenWaves(post.Body,10);
                    foreach(var player in inst.Players)if(player!=null && player.Entity!=null && player.Entity.MapID==inst.Arena.ID)RecoverBetweenWaves(player.Entity,15);
                    Tell(inst,"The wave is broken! Regroup for eight seconds. The surviving posts are being repaired.");
                } else if(now>=inst.NextWaveAt)StartWave(inst,inst.Wave+1);
            }
            else if (inst.Wave == 8 && RemainingMobs(inst) == 0)
                Win(inst);
            string hud = "Wave " + inst.Wave + "/8  Posts " + standing + "/3  Lives " + inst.Lives;
            if(inst.NextWaveAt!=default(DateTime))hud+="  Next wave: "+Math.Max(0,(int)Math.Ceiling((inst.NextWaveAt-now).TotalSeconds))+"s";
            hud+="  Post HP: "+string.Join("/",inst.Posts.Select(p=>p==null || p.Body==null || p.MaxHp==0?"0":((ulong)p.Body.Hitpoints*100/p.MaxHp).ToString()))+"%";
            if (now < inst.MedicUntil)
                hud += "  MEDIC PULSE";
            TellCorner(inst, hud);
        }

        internal static void RecoverBetweenWaves(Entity entity,uint percent)
        {
            if(entity==null || entity.Dead || percent>100)return;
            entity.Hitpoints=(uint)Math.Min((ulong)entity.MaxHitpoints,(ulong)entity.Hitpoints+(ulong)entity.MaxHitpoints*percent/100);
        }
        private static void PulseMedic(Instance inst)
        {
            Tell(inst, "The Warden Medic pulses. Stand close!");
            for (int i = 0; i < inst.Players.Count; i++)
            {
                GameState client = inst.Players[i];
                if (client == null || client.Entity == null || client.Entity.Dead) continue;
                if (client.Entity.MapID != inst.Arena.ID) continue;
                if (Kernel.GetDistance(client.Entity.X, client.Entity.Y, inst.MedicX, inst.MedicY) > MedicRange)
                    continue;
                uint heal = client.Entity.MaxHitpoints * 30 / 100;
                client.Entity.Hitpoints = Math.Min(client.Entity.MaxHitpoints, client.Entity.Hitpoints + heal);
                client.Entity.Mana = (ushort)Math.Min(client.Entity.MaxMana, client.Entity.Mana + 200);
                client.Send(new Message("The Medic restores you.", Color.Lime, Message.TopLeft));
                client.Entity.Update((byte)FlagsUpdate.DataType.Hitpoints, client.Entity.Hitpoints, false);
            }
            for (int i = 0; i < inst.Allies.Count; i++)
            {
                GameState ally = inst.Allies[i];
                if (ally == null || ally.Entity == null) continue;
                if (ally.Entity.Dead)
                {
                    ally.Entity.Hitpoints = ally.Entity.MaxHitpoints;
                    ally.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.Dead | (ulong)FlagsUpdate.Flags.Ghost);
                    ally.Entity.X = inst.MedicX;
                    ally.Entity.Y = inst.MedicY;
                }
                else
                    ally.Entity.Hitpoints = Math.Min(ally.Entity.MaxHitpoints, ally.Entity.Hitpoints + ally.Entity.MaxHitpoints * 30 / 100);
            }
        }

        private static void DriveAllies(Instance inst, DateTime now)
        {
            for (int i = 0; i < inst.Allies.Count; i++)
            {
                GameState ally = inst.Allies[i];
                if (ally == null || !ally.Fake || ally.Entity == null || ally.Entity.Dead
                    || ally.Entity.MapID != inst.Arena.ID) continue;
                Entity body = ally.Entity;
                DateTime nextAction;
                if (inst.AllyActions.TryGetValue(body.UID, out nextAction) && now < nextAction) continue;
                Entity pest = null;
                int distance = int.MaxValue;
                foreach (uint uid in inst.MonsterPost.Keys)
                {
                    Entity candidate;
                    if (!inst.Arena.Entities.TryGetValue(uid, out candidate) || candidate == null
                        || candidate.Dead || candidate.Killed || candidate.EntityFlag != EntityFlag.Monster
                        || candidate.MapID != inst.Arena.ID || IsBanner(candidate)) continue;
                    int d = Kernel.GetDistance(body.X, body.Y, candidate.X, candidate.Y);
                    if (d < distance) { pest = candidate; distance = d; }
                }
                if (pest == null) continue;
                Func<int, int, bool> open = (x, y) => inst.Arena.Floor[x, y, MapObjectType.Player, null];
                if (distance > 2 || !CompanionNavigation.HasSight(new Point(body.X, body.Y), new Point(pest.X, pest.Y), open))
                {
                    CompanionNavigation route;
                    if (!inst.AllyRoutes.TryGetValue(body.UID, out route))
                        inst.AllyRoutes[body.UID] = route = new CompanionNavigation();
                    Point step;
                    if (route.Next(inst.Arena, new Point(body.X, body.Y), new Point(pest.X, pest.Y), now, open, out step))
                        WorldActorMotion.Walk(body, (ushort)step.X, (ushort)step.Y, false);
                    inst.AllyActions[body.UID] = now.AddMilliseconds(250);
                    continue;
                }
                if (!Attacking.Handle.CanAttack(body, pest, null, true)) continue;
                var attack = new Attack(true) { Attacker = body.UID, Attacked = pest.UID,
                    AttackType = Attack.Melee, X = pest.X, Y = pest.Y };
                uint damage = Math.Min(pest.Hitpoints, Attacking.Calculate.Melee(body, pest, ref attack));
                inst.AllyActions[body.UID] = now.AddSeconds(1);
                // Follow the existing world-actor death path, without fake-player quest/XP awards.
                if (damage > 0)
                {
                    if (pest.Hitpoints <= damage) pest.Die(body);
                    else pest.Hitpoints -= damage;
                }
                attack.Damage = damage;
                foreach (GameState player in inst.Players)
                    if (player != null && player.Entity != null && player.Entity.MapID == inst.Arena.ID)
                        player.Send(attack);
            }
        }

        private static void DriveMonsters(Instance inst, DateTime now)
        {
            if (inst.Arena.FreezeMonsters) return;
            foreach (uint uid in inst.MonsterPost.Keys.ToArray())
            {
                Entity monster;
                if (!inst.Arena.Entities.TryGetValue(uid, out monster) || monster == null
                    || monster.MonsterInfo == null || monster.Dead || monster.Killed) continue;
                DateTime next;
                if (inst.MonsterActions.TryGetValue(uid, out next) && now < next) continue;
                Entity target = SelectMonsterTarget(inst, monster);
                if (target == null) continue;
                Point start = new Point(monster.X, monster.Y), goal = new Point(target.X, target.Y);
                Func<int, int, bool> terrain = (x, y) => inst.Arena.Floor[x, y, MapObjectType.Player, null];
                if (Kernel.GetDistance(monster.X, monster.Y, target.X, target.Y) <= Math.Max(1, (int)monster.MonsterInfo.AttackRange)
                    && CompanionNavigation.HasSight(start, goal, terrain))
                {
                    inst.MonsterActions[uid] = now.AddMilliseconds(Math.Max(250, (int)monster.MonsterInfo.AttackSpeed));
                    new Attacking.Handle(null, monster, target);
                    // A player's lethal hit can finish and dispose the entire instance.
                    if (inst.Closed || inst.Won || inst.Failed || inst.Abort || inst.PendingKind >= 0) return;
                    continue;
                }
                int delay = monster.MonsterInfo.RunSpeed > 0 ? monster.MonsterInfo.RunSpeed : monster.MonsterInfo.MoveSpeed;
                inst.MonsterActions[uid] = now.AddMilliseconds(Math.Max(250, delay));
                CompanionNavigation route;
                if (!inst.MonsterRoutes.TryGetValue(uid, out route))
                    inst.MonsterRoutes[uid] = route = new CompanionNavigation();
                Point step;
                if (!route.Next(inst.Arena, start, goal, now,
                    (x, y) => terrain(x, y) && (x == goal.X && y == goal.Y || inst.Arena.Floor[x, y, MapObjectType.Monster, null]), out step)) continue;
                var facing = Kernel.GetAngle(monster.X, monster.Y, (ushort)step.X, (ushort)step.Y);
                if (!monster.Move(facing)) { route.Reset(); continue; }
                monster.MonsterInfo.SendScreen(new GroundMovement(true) { UID = uid, Direction = facing,
                    GroundMovementType = GroundMovement.Run, MapID = inst.Arena.ID, TimeStamp = (uint)Time32.Now.GetHashCode() });
            }
        }

        private static void SweepDeadMobs(Instance inst)
        {
            if (inst.Arena == null || inst.Arena.Entities == null) return;
            foreach (uint uid in inst.MonsterPost.Keys.ToArray())
            {
                Entity monster;
                if (inst.Arena.Entities.TryGetValue(uid, out monster) && monster != null && !monster.Dead && !monster.Killed) continue;
                inst.MonsterPost.Remove(uid); inst.MonsterRoutes.Remove(uid); inst.MonsterActions.Remove(uid);
            }
        }

        private static int RemainingMobs(Instance inst)
        {
            int n = 0;
            if (inst.Arena == null || inst.Arena.Entities == null) return 0;
            foreach (Entity e in inst.Arena.Entities.Values)
            {
                if (e == null || e.EntityFlag != EntityFlag.Monster || e.Dead) continue;
                if (IsBanner(e)) continue;
                n++;
            }
            return n;
        }

        internal static void InitializePrizes()
        {
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); QuestInventory.Execute(c, null, @"CREATE TABLE IF NOT EXISTS revora_wave_results(
run uuid NOT NULL,uid bigint NOT NULL,kind integer NOT NULL CHECK(kind BETWEEN 0 AND 2),
recorded_at timestamptz NOT NULL DEFAULT now(),imported boolean NOT NULL DEFAULT false,
PRIMARY KEY(run,uid));
CREATE INDEX IF NOT EXISTS revora_wave_pending ON revora_wave_results(uid,recorded_at,run) WHERE NOT imported AND kind IN (1,2);");
            }
        }
        private static void Win(Instance inst) { Complete(inst, true, ""); }
        private static void Fail(Instance inst, string why) { Complete(inst, false, why); }
        private static void Complete(Instance inst, bool won, string why)
        {
            lock (inst)
            {
                if (inst.Won || inst.Failed) return;
                if (inst.Abort) { Close(inst); return; }
                if (inst.PendingKind < 0)
                {
                    inst.PendingKind = won && inst.Wave >= 8 ? 1 : inst.Wave >= 4 ? 2 : 0;
                    inst.PendingReason = why;
                    lock (Gate) inst.PendingEligible = new HashSet<uint>(inst.Players.Where(p => p != null && p.Entity != null && !p.Fake).Select(p => p.Entity.UID));
                }
                try
                {
                    if (inst.Participants == null || inst.Participants.Length < MinPlayers || inst.Participants.Length > MaxPlayers
                        || inst.Participants.Any(uid => uid == 0) || inst.Participants.Distinct().Count() != inst.Participants.Length)
                        throw new InvalidOperationException("Wave result roster is invalid.");
                    using (var c = DataHolder.MySqlConnection)
                    {
                        c.Open(); using (var t = c.BeginTransaction())
                        {
                            foreach (uint uid in inst.Participants.OrderBy(uid => uid))
                            {
                                int kind = inst.PendingEligible.Contains(uid) ? inst.PendingKind : 0;
                                QuestInventory.Execute(c, t, @"INSERT INTO revora_wave_results(run,uid,kind) VALUES(@run,@uid,@kind)
ON CONFLICT(run,uid) DO NOTHING", "run", inst.ResultId, "uid", (long)uid, "kind", kind);
                                using (var q = QuestInventory.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_wave_results WHERE run=@run AND uid=@uid AND kind=@kind)",
                                    "run",inst.ResultId,"uid",(long)uid,"kind",kind))
                                    if (!Equals(q.ExecuteScalar(),true)) throw new InvalidOperationException("Wave result was not saved.");
                            }
                            using (var q = QuestInventory.Command(c,t,"SELECT count(*) FROM revora_wave_results WHERE run=@run","run",inst.ResultId))
                                if ((long)q.ExecuteScalar() != inst.Participants.Length) throw new InvalidOperationException("Wave result roster changed.");
                            t.Commit();
                        }
                    }
                }
                catch (Exception ex) { Console.WriteLine("[wave-result] error=" + ex.GetType().Name); return; }
                inst.Won = inst.PendingKind == 1; inst.Failed = !inst.Won;
                try
                {
                    foreach (uint uid in inst.Participants)
                    {
                        if (!inst.PendingEligible.Contains(uid)) continue;
                        GameState p;
                        if (!Kernel.GamePool.TryGetValue(uid,out p) || !QuestInventory.Player(p,false)) continue;
                        p.Send(new Message(inst.Won ? "Eight waves held. Your purse and bound P7 soul await at WaveWarden."
                            : inst.PendingKind == 2 ? "Your fourth-wave consolation awaits at WaveWarden."
                            : "Wave Defense failed. " + inst.PendingReason, Color.Yellow, Message.TopLeft));
                    }
                }
                finally { Close(inst); }
            }
        }
        private static void Close(Instance inst)
        {
            if (inst.Closed) return;
            try { WaveAdmissions.End(inst.ResultId.ToString("N")); }
            catch (Exception ex) { Console.WriteLine("[wave-end] error=" + ex.GetType().Name); return; }
            lock (Gate) if (inst.Participants != null) foreach (uint uid in inst.Participants) PendingReturns.Add(uid);
            ProcessReturns();
            try { if (!WaveAdmissions.Returned(inst.ResultId.ToString("N"))) return; }
            catch (Exception ex) { Console.WriteLine("[wave-release] error=" + ex.GetType().Name); return; }
            if (inst.OwnedArena && Kernel.GamePool.Values.Any(p => p != null && !p.Fake && p.Entity != null && p.Entity.MapID == inst.Arena.ID)) return;
            for (int i = 0; i < inst.Allies.Count; i++)
            {
                GameState ally = inst.Allies[i];
                if (ally == null || ally.Entity == null) continue;
                ally.Socket.Alive = false;
                if (ally.Screen != null) ally.Screen.DisposeTimers();
                if (ally.Pet != null) ally.Pet.DisposeTimers();
                if (ally.Map != null)
                    ally.Map.RemoveEntity(ally.Entity);
                SessionInstall.TryRemoveFake(ally.Entity.UID, ally);
            }
            lock (Gate)
            {
                Live.Remove(inst);
                ByMap.Remove(inst.Arena.ID);
                RegisteredLeaders.Remove(inst.LeaderUid);
                if (inst.OwnedArena)
                {
                    Map current;
                    if (Kernel.Maps.TryGetValue(inst.Arena.ID, out current) && ReferenceEquals(current, inst.Arena))
                    {
                        foreach (var npc in inst.Arena.Npcs.Values.ToArray())
                        {
                            INpc indexed;
                            if (Map.SecoundNpc.TryGetValue(npc.UID, out indexed) && ReferenceEquals(indexed, npc)) Map.SecoundNpc.Remove(npc.UID);
                        }
                        inst.Arena.Npcs.Clear(); inst.Arena.Entities.Clear(); inst.Arena.Companions.Clear(); inst.Arena.FloorItems.Clear();
                        Constants.PKFreeMaps.Remove(inst.Arena.ID);
                        inst.Arena.Dispose();
                    }
                }
                inst.Allies.Clear(); inst.AllyRoutes.Clear(); inst.AllyActions.Clear();
                inst.MonsterRoutes.Clear(); inst.MonsterActions.Clear();
                inst.Players.Clear(); inst.MonsterPost.Clear(); inst.Closed = true;
            }
        }

        private static void ReviveAtMedic(Instance inst, GameState client)
        {
            if (client.Entity == null) return;
            client.Entity.Hitpoints = client.Entity.MaxHitpoints / 2;
            client.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.Dead | (ulong)FlagsUpdate.Flags.Ghost);
            ushort x = inst.MedicX;
            ushort y = inst.MedicY;
            inst.Arena.SelectCoordonates(ref x, ref y);
            client.Entity.Teleport(inst.Arena.BaseID, inst.Arena.ID, x, y);
            client.CantAttack = Time32.Now.AddSeconds(5);
            client.Attackable = false;
            client.ReviveStamp = Time32.Now;
            client.Send(new Message("You rise at the Medic. Team lives left: " + inst.Lives + ".", Color.Orange, Message.TopLeft));
        }

        private static string TryClaim(GameState client)
        {
            if (!QuestInventory.Player(client) || client.Variables == null)
                return "Return when you are ready to claim your prize.";
            lock (client.ItemSyncRoot) lock (client.Variables.SyncRoot)
            try
            {
                if (!QuestInventory.Player(client) || !HasClaim(client))
                    return "You have no Wave Defense prize to claim.";
                uint kind = (uint)client[ClaimKey];
                if (kind == 1 && client.Inventory.Count >= 40)
                    return "Leave one space for your soul. Your prize will wait.";
                uint cp = kind == 1 ? Revora.Config.WaveDefenseWinCp : Revora.Config.WaveDefenseConsolationCp;
                ulong gold = kind == 1 ? Revora.Config.WaveDefenseWinGold : Revora.Config.WaveDefenseConsolationGold;
                uint rp = kind == 1 ? Revora.Config.WaveDefenseWinRevoraPoints : Revora.Config.WaveDefenseConsolationRevoraPoints;
                long nextCp = (long)client.Entity.ConquerPoints + cp;
                long nextRp = (long)client.Entity.RevoraPoint + rp;
                decimal nextGold = (decimal)client.Entity.Money + gold;
                if (nextCp > uint.MaxValue || nextRp > uint.MaxValue || nextGold > Entity.MaximumSilver)
                    return "Make room in your purse. Your prize will wait.";
                var next = new VariableVault(); next.Deserialize(client.Variables.Serialize());
                next[ClaimKey] = 0u;
                var soul = kind == 1 ? QuestSoulReward.Create() : null;
                using (var c = DataHolder.MySqlConnection)
                {
                    c.Open(); using (var t = c.BeginTransaction())
                    {
                        SilverBalance.AssertSaved(c, t, client);
                        byte[] before;
                        using (var q = QuestInventory.Command(c, t,
                            "SELECT data FROM variablevault WHERE entityid=@uid FOR UPDATE", "uid", (long)client.Entity.UID))
                            before = q.ExecuteScalar() as byte[];
                        if (before == null) throw new InvalidOperationException("Wave Defense entitlement is not saved.");
                        var saved = new VariableVault(); saved.Deserialize(before);
                        if ((uint)saved[ClaimKey] != kind)
                            throw new InvalidOperationException("Wave Defense placement changed.");
                        if (QuestInventory.Execute(c, t, @"UPDATE entities SET conquerpoints=@cp,revorapoint=@rp,money=@gold
    WHERE uid=@uid AND conquerpoints=@oldcp AND revorapoint=@oldrp",
                            "cp", nextCp, "rp", nextRp, "gold", nextGold, "uid", (long)client.Entity.UID,
                            "oldcp", (long)client.Entity.ConquerPoints, "oldrp", (long)client.Entity.RevoraPoint) != 1)
                            throw new InvalidOperationException("Wave Defense purse changed.");
                        if (QuestInventory.Execute(c, t, @"UPDATE variablevault SET data=@data
    WHERE entityid=@uid AND data=@before", "data", next.Serialize(), "uid", (long)client.Entity.UID, "before", before) != 1)
                            throw new InvalidOperationException("Wave Defense entitlement changed.");
                        if (soul != null) QuestInventory.Insert(c, t, client.Entity.UID, soul);
                        t.Commit();
                    }
                }
                next.Changed = false; client.Variables = next;
                client.Entity.ApplyCommittedConquerPoints((uint)nextCp);
                client.Entity.ApplyCommittedRevoraPoints((uint)nextRp);
                SilverBalance.ApplyCommitted(client, (ulong)nextGold);
                if (soul != null) QuestInventory.Install(client, new uint[0], new[] { soul });
                return kind == 1 ? "Eight waves held. Your victory purse and bound P7 soul are yours." : "Your fourth-wave consolation purse is yours. Stand firm next time.";
            }
            catch (Exception ex)
            {
                QuestInventory.Fail(client, "wave-prize", ex);
                return "Your prize could not be collected. Reconnect and try again.";
            }
        }
        private static bool HasClaim(GameState client)
        {
            if (!QuestInventory.Player(client, false) || client.Variables == null) return false;
            lock (client.ItemSyncRoot) lock (client.Variables.SyncRoot)
            try
            {
                uint place = (uint)client[ClaimKey];
                if (place != 0) return place <= 2;
                using (var c = DataHolder.MySqlConnection)
                {
                    c.Open(); using (var t = c.BeginTransaction())
                    {
                        byte[] before;
                        using (var q = QuestInventory.Command(c,t,"SELECT data FROM variablevault WHERE entityid=@uid FOR UPDATE","uid",(long)client.Entity.UID))
                            before = q.ExecuteScalar() as byte[];
                        if (before == null)
                            throw new InvalidOperationException("Wave prize variables changed.");
                        var saved = new VariableVault(); saved.Deserialize(before);
                        if ((uint)saved[ClaimKey] != 0)
                            throw new InvalidOperationException("Wave prize placement changed.");
                        Guid run;
                        using (var q = QuestInventory.Command(c,t,@"SELECT run,kind FROM revora_wave_results
WHERE uid=@uid AND kind IN (1,2) AND NOT imported ORDER BY recorded_at,run LIMIT 1 FOR UPDATE","uid",(long)client.Entity.UID))
                        using (var r = q.ExecuteReader())
                        {
                            if (!r.Read()) return false;
                            run = r.GetGuid(0); place = (uint)r.GetInt32(1);
                        }
                        var next = new VariableVault(); next.Deserialize(client.Variables.Serialize()); next[ClaimKey] = place;
                        if (QuestInventory.Execute(c,t,"UPDATE variablevault SET data=@data WHERE entityid=@uid AND data=@before",
                            "data",next.Serialize(),"uid",(long)client.Entity.UID,"before",before) != 1
                            || QuestInventory.Execute(c,t,"UPDATE revora_wave_results SET imported=true WHERE run=@run AND uid=@uid AND NOT imported",
                            "run",run,"uid",(long)client.Entity.UID) != 1)
                            throw new InvalidOperationException("Wave prize import was not saved.");
                        t.Commit(); next.Changed = false; client.Variables = next;
                        return true;
                    }
                }
            }
            catch (Exception ex) { QuestInventory.Fail(client,"wave-import",ex); return false; }
        }

        private static Instance FindByPlayer(uint uid)
        {
            lock (Gate)
            {
                for (int i = 0; i < Live.Count; i++)
                {
                    Instance inst = Live[i];
                    for (int p = 0; p < inst.Players.Count; p++)
                    {
                        if (inst.Players[p] != null && inst.Players[p].Entity != null && inst.Players[p].Entity.UID == uid)
                            return inst;
                    }
                }
            }
            return null;
        }

        private static Entity NearestPlayer(Instance inst, Entity monster, int range)
        {
            ushort x = monster.X, y = monster.Y;
            Entity best = null;
            int bestD = range + 1;
            for (int i = 0; i < inst.Players.Count; i++)
            {
                GameState client = inst.Players[i];
                if (client == null || client.Entity == null || client.Entity.Dead) continue;
                if (client.Entity.MapID != inst.Arena.ID) continue;
                if (client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Invisibility)
                    || monster.MonsterInfo.SpellID == 0 && client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Fly)) continue;
                GameState current;
                if (!Kernel.GamePool.TryGetValue(client.Entity.UID, out current) || !ReferenceEquals(current, client)) continue;
                int d = Kernel.GetDistance(x, y, client.Entity.X, client.Entity.Y);
                if (d < bestD)
                {
                    bestD = d;
                    best = client.Entity;
                }
            }
            for (int i = 0; i < inst.Allies.Count; i++)
            {
                GameState ally = inst.Allies[i];
                if (ally == null || ally.Entity == null || ally.Entity.Dead) continue;
                if (ally.Entity.MapID != inst.Arena.ID) continue;
                if (ally.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Invisibility)
                    || monster.MonsterInfo.SpellID == 0 && ally.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Fly)) continue;
                int d = Kernel.GetDistance(x, y, ally.Entity.X, ally.Entity.Y);
                if (d < bestD)
                {
                    bestD = d;
                    best = ally.Entity;
                }
            }
            return best;
        }

        private static void BroadcastEntity(Instance inst, Entity entity)
        {
            for (int i = 0; i < inst.Players.Count; i++)
            {
                if (inst.Players[i] != null)
                    entity.SendSpawn(inst.Players[i]);
            }
        }

        private static void Tell(Instance inst, string text)
        {
            for (int i = 0; i < inst.Players.Count; i++)
            {
                if (inst.Players[i] != null)
                    inst.Players[i].Send(new Message(text, Color.Yellow, Message.TopLeft));
            }
        }

        private static void TellCorner(Instance inst, string text)
        {
            for (int i = 0; i < inst.Players.Count; i++)
            {
                if (inst.Players[i] != null)
                    inst.Players[i].Send(new Message(text, Color.Yellow, Message.FirstRightCorner));
            }
        }
    }
}
