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
    public static class BeastTrialSystem
    {
        public const uint NpcId = 900000009;
        public const string NpcName = "BeastTrial";
        public const int MaxSolos = 16;
        public const int Lives = 3;
        public const string ClaimKey = "beast-claim";
        public const ushort TwinCityMap = 1002;
        public const ushort TwinCityX = 408;
        public const ushort TwinCityY = 330;
        public const ushort ClaimX = 370;
        public const ushort ClaimY = 299;
        // Twin City MountTrainer look. Distinct from WaveWarden (5150) and the PvP clerk row.
        public const ushort Mesh = 50010;

        private static readonly int[] StartHours = new int[] { 1, 3, 5, 7, 9, 11, 13, 15, 17, 19, 21, 23 };
        private static readonly TimeSpan RegisterWindow = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan FightLimit = TimeSpan.FromMinutes(12);
        private static readonly object Gate = new object();
        private static readonly List<Instance> Live = new List<Instance>();
        private static readonly Dictionary<ushort, Instance> ByMap = new Dictionary<ushort, Instance>();
        private static readonly HashSet<uint> Registered = new HashSet<uint>();
        private static readonly HashSet<uint> PendingReturns = new HashSet<uint>();
        private static readonly uint[] WaveMobs = new uint[] { 2, 7, 10, 11, 12, 15, 18 };
        private const uint MiniBossId = 8306;
        private const uint NamedBossId = 31;
        private static DateTime CycleStart;
        private static bool RegisterOpen;
        private static bool Invited;

        private sealed class Instance
        {
            public readonly Guid ResultId = Guid.NewGuid();
            public uint PlayerUid;
            public int PendingKind = -1;
            public string PendingReason;
            public bool Abort;
            public bool OwnedArena;
            public bool Closed;
            public Map Arena;
            public GameState Player;
            public int LivesLeft = Lives;
            public int Wave;
            public DateTime FightUntil;
            public ushort SpawnX;
            public ushort SpawnY;
            public bool Won;
            public bool Failed;
        }

        public static void Create()
        {
            InitializePrizes();
            BeastAdmissions.Initialize();
            BeastAdmissions.RecoverInterrupted();
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

        public static void Tick()
        {
            ProcessReturns();
            if (CycleStart == default(DateTime)) return;
            DateTime now = DateTime.Now;
            if (!Invited && now >= CycleStart.Subtract(RegisterWindow) && now < CycleStart)
            {
                Invited = true;
                RegisterOpen = true;
                Kernel.SendWorldMessage(new Message("Beast Trial registration is open for 5 minutes. Solo PVE. Talk to BeastTrial in Twin City (" + ClaimX + ", " + ClaimY + ").", Color.Yellow, Message.Center), Server.GamePool);
                InviteAll();
            }
            if (RegisterOpen && now >= CycleStart)
                RegisterOpen = false;
            if (now >= CycleStart.AddMinutes(20))
            {
                Invited = false;
                lock (Gate)
                    Registered.Clear();
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
            if (client.ActiveNpc != NpcId)
                return false;
            Npcs dialog = new Npcs(client);
            switch (request.OptionID)
            {
                case 0:
                    dialog.Text(StatusText());
                    if (RegisterOpen)
                        dialog.Option("Enter the trial", 1);
                    if (HasClaim(client))
                        dialog.Option("Claim my prize", 2);
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
            if (inst == null || inst.Won || inst.Failed) return;
            lock (inst)
            {
            if (inst.Won || inst.Failed || inst.Abort || inst.PendingKind >= 0 || !client.Entity.Dead) return;
            if (!ReferenceEquals(inst.Player, client) || client.Entity.MapID != inst.Arena.ID) return;
            inst.LivesLeft--;
            if (inst.LivesLeft < 0) inst.LivesLeft = 0;
            if (inst.LivesLeft <= 0)
            {
                Fail(inst, "You are out of lives.");
                return;
            }
            ReviveAtSpawn(inst, client);
            }
        }

        public static void OnDisconnect(GameState client)
        {
            if (client == null || client.Entity == null) return;
            Instance inst = FindByPlayer(client.Entity.UID);
            if (inst == null) return;
            Fail(inst, "You left the field.");
        }

        public static void OnLogin(GameState client)
        {
            if (!QuestInventory.Player(client, false)) return;
            OnDisconnect(client);
            lock (Gate)
            {

                {
                    if (BeastAdmissions.HasPendingReturn(client.Entity.UID)) PendingReturns.Add(client.Entity.UID);
                    else PendingReturns.Remove(client.Entity.UID);
                }
            }
            ProcessReturns();
            lock (Gate)
                if (PendingReturns.Contains(client.Entity.UID))
                    QuestInventory.Fail(client, "beast-login-return", new InvalidOperationException("Beast Trial return is pending; reconnect."));
        }

        private static void ProcessReturns()
        {
            lock (Gate)
            {
                var pending = new List<uint>(PendingReturns);
                for (int i = 0; i < Math.Min(MaxSolos, pending.Count); i++)
                {
                    uint uid = pending[i];
                    try
                    {
                        BeastAdmissions.Forfeit(uid);
                        GameState p;
                        // Offline owners discover their durable return at login.
                        if (!Kernel.GamePool.TryGetValue(uid, out p) || p.Entity.PersistenceRequiresReload || BeastAdmissions.Return(p))
                            PendingReturns.Remove(uid);
                    }
                    catch (Exception ex) { Console.WriteLine("[beast-return] error=" + ex.GetType().Name); }
                }
            }
        }

        private static string StatusText()
        {
            if (RegisterOpen)
                return "Registration is open until " + CycleStart.ToString("HH:mm") + ". Solo only. Kill 8 waves on a private map. Heals are yours. Three lives. Mini-boss on wave 4, named boss on wave 8.";
            return "Next Beast Trial " + CycleStart.ToString("HH:mm") + ". Odd hours at :30. Register 5 minutes before. Solo PVE. Private map. 3 lives. 12 minutes.";
        }

        private static void InviteAll()
        {
            foreach (GameState client in Kernel.GamePool.Values)
            {
                if (client == null || client.Entity == null || client.Entity.Dead || client.Fake) continue;
                GameState captured = client;
                captured.MessageOK = p => { TryRegister(p); };
                captured.Send(new NpcReply(NpcReply.MessageBox, "Beast Trial is open. Solo PVE. Kill 8 waves. Register at BeastTrial (" + ClaimX + ", " + ClaimY + ")?"));
                captured.Send(new Data(true) { UID = captured.Entity.UID, dwParam = 60, ID = Data.CountDown });
            }
        }

        private static string TryRegister(GameState client)
        {
            if (!QuestInventory.Player(client)) return "Unable to register.";
            if (!RegisterOpen) return "Registration is closed.";
            if (client.Entity.Dead) return "You are dead.";
            if (client.Entity.MapID != TwinCityMap) return "Stand in Twin City first.";
            if (HasTeammates(client)) return "This trial is solo. Leave your team first.";
            if (InEvent(client.Entity)) return "You are already in an instance.";
            if (BossRushSystem.InEvent(client.Entity)) return "Finish Boss Rush first.";
            lock (Gate)
            {
                if (Live.Count >= MaxSolos) return "All pits are full (16).";
                if (Registered.Contains(client.Entity.UID)) return "You already entered this round.";
                if (!BeginInstance(client)) return "No map could be opened.";
                Registered.Add(client.Entity.UID);
            }
            return "You are in. Kill every wave. Three lives. Heals are yours. Mini-boss on 4, named boss on 8.";
        }

        private static bool HasTeammates(GameState client)
        {
            if (client.Team == null) return false;
            foreach (GameState mate in client.Team.Players)
            {
                if (mate == null || mate.Entity == null) continue;
                if (mate.Entity.UID == client.Entity.UID) continue;
                return true;
            }
            return false;
        }

        private static bool BeginInstance(GameState client)
        {
            if (!System.Threading.Monitor.TryEnter(client.ItemSyncRoot)) return false;
            try { return BeginInstanceCore(client); }
            finally { System.Threading.Monitor.Exit(client.ItemSyncRoot); }
        }
        private static bool BeginInstanceCore(GameState client)
        {
            if (!QuestInventory.Player(client) || client.Trade.InTrade || client.Booth != null) return false;
            Map source = null;
            if (Kernel.Maps.ContainsKey(700))
                source = Kernel.Maps[700];
            else if (Kernel.Maps.ContainsKey(1005))
                source = Kernel.Maps[1005];
            else if (Kernel.Maps.ContainsKey(1002))
                source = Kernel.Maps[1002];
            if (source == null) return false;
            Map arena = Map.CreateDynamic(source.ID, source.Path, false);
            if (arena == null) return false;
            if (!Constants.PKFreeMaps.Contains(arena.ID))
                Constants.PKFreeMaps.Add(arena.ID);
            Instance inst = new Instance();
            inst.Arena = arena;
            inst.OwnedArena = true;
            inst.Player = client;
            inst.PlayerUid = client.Entity.UID;
            inst.FightUntil = DateTime.Now.Add(FightLimit);
            try { BeastAdmissions.Begin(inst.ResultId.ToString("N"), arena.ID, new[] { client }); }
            catch (Exception ex) { Constants.PKFreeMaps.Remove(arena.ID); arena.Dispose(); Console.WriteLine("[beast-admission] error=" + ex.GetType().Name); return false; }
            Live.Add(inst);
            ByMap[arena.ID] = inst;
            try
            {
            Tuple<ushort, ushort> spawn = arena.RandomCoordinates();
            inst.SpawnX = spawn.Item1;
            inst.SpawnY = spawn.Item2;
            ushort x = inst.SpawnX;
            ushort y = inst.SpawnY;
            arena.SelectCoordonates(ref x, ref y);
            inst.SpawnX = x;
            inst.SpawnY = y;
            client.Entity.Teleport(arena.BaseID, arena.ID, x, y);
            if (client.Entity.MapID != arena.ID) throw new InvalidOperationException("Beast transfer failed.");
            client.Send(new Message("Beast Trial: kill 8 waves. Three lives. Heals are yours. 12 minutes.", Color.Yellow, Message.TopLeft));
            Kernel.SendWorldMessage(new Message(client.Entity.Name + " entered Beast Trial.", Color.Orange, Message.Center), Server.GamePool);
            StartWave(inst, 1);
            if (inst.Abort) { Cancel(inst); return false; }
            return true;
            }
            catch (Exception ex) { inst.Abort = true; Console.WriteLine("[beast-entry] error=" + ex.GetType().Name); return false; }
        }

        private static void StartWave(Instance inst, int wave)
        {
            try
            {
            if (wave < 1 || wave > 8) throw new InvalidOperationException("Beast wave number is invalid.");
            uint mob = WaveMobs[(wave - 1) % WaveMobs.Length];
            if (!MonsterInformation.MonsterInformations.ContainsKey(mob)
                || wave == 4 && !MonsterInformation.MonsterInformations.ContainsKey(MiniBossId)
                || wave == 8 && !MonsterInformation.MonsterInformations.ContainsKey(NamedBossId))
                throw new InvalidOperationException("Beast wave catalog is incomplete.");
            inst.Wave = wave;
            int packs = 3 + wave;
            if (wave >= 5) packs = 5 + wave;
            if (wave == 8) packs = 8;
            for (int i = 0; i < packs; i++)
                SpawnWaveMob(inst, mob, 0);
            if (wave == 4)
                SpawnWaveMob(inst, MiniBossId, 15000);
            if (wave == 8)
                SpawnWaveMob(inst, NamedBossId, 35000);
            if (inst.Player != null)
                inst.Player.Send(new Message("Wave " + wave + " of 8.", Color.Yellow, Message.TopLeft));
            }
            catch (Exception ex) { inst.Abort = true; Console.WriteLine("[beast-spawn] wave=" + wave + " error=" + ex.GetType().Name); }
        }

        private static void SpawnWaveMob(Instance inst, uint mobId, uint hpOverride)
        {
            if (!MonsterInformation.MonsterInformations.ContainsKey(mobId))
                throw new InvalidOperationException("Beast monster is missing.");
            MonsterInformation mt = MonsterInformation.MonsterInformations[mobId];
            Tuple<ushort, ushort> xy = inst.Arena.RandomCoordinates();
            Entity entity = new Entity(EntityFlag.Monster, false);
            entity.MapObjType = MapObjectType.Monster;
            entity.MonsterInfo = mt.Copy();
            entity.MonsterInfo.Owner = entity;
            entity.MonsterInfo.BoundX = inst.SpawnX;
            entity.MonsterInfo.BoundY = inst.SpawnY;
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
            if (inst.Player != null)
                entity.SendSpawn(inst.Player);
        }

        private static void TickInstance(Instance inst, DateTime now)
        {
            lock (inst) TickInstanceCore(inst, now);
        }
        private static void TickInstanceCore(Instance inst, DateTime now)
        {
            if (inst.Abort) { Cancel(inst); return; }
            if (inst.Won || inst.Failed) { Close(inst); return; }
            if (inst.PendingKind >= 0) { Complete(inst, false, inst.PendingReason); return; }
            if (now >= inst.FightUntil)
            {
                Fail(inst, "Time is up.");
                return;
            }
            if (inst.Player == null || inst.Player.Entity == null)
            {
                Fail(inst, "You left the field.");
                return;
            }
            if (inst.Wave < 8 && RemainingMobs(inst) == 0)
                StartWave(inst, inst.Wave + 1);
            else if (inst.Wave == 8 && RemainingMobs(inst) == 0)
                Win(inst);
            if (inst.Player != null)
                inst.Player.Send(new Message("Wave " + inst.Wave + "/8  Lives " + inst.LivesLeft + "  Beasts " + RemainingMobs(inst), Color.Yellow, Message.FirstRightCorner));
        }

        private static int RemainingMobs(Instance inst)
        {
            int n = 0;
            if (inst.Arena == null || inst.Arena.Entities == null) return 0;
            foreach (Entity e in inst.Arena.Entities.Values)
            {
                if (e == null || e.EntityFlag != EntityFlag.Monster || e.Dead) continue;
                n++;
            }
            return n;
        }

        internal static void InitializePrizes()
        {
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); QuestInventory.Execute(c, null, @"CREATE TABLE IF NOT EXISTS revora_beast_results(
run uuid NOT NULL,uid bigint NOT NULL,kind integer NOT NULL CHECK(kind BETWEEN 0 AND 2),
recorded_at timestamptz NOT NULL DEFAULT now(),imported boolean NOT NULL DEFAULT false,
PRIMARY KEY(run,uid));
CREATE INDEX IF NOT EXISTS revora_beast_pending ON revora_beast_results(uid,recorded_at,run) WHERE NOT imported AND kind IN (1,2);");
            }
        }
        private static void Win(Instance inst) { Complete(inst, true, ""); }
        private static void Fail(Instance inst, string why) { Complete(inst, false, why); }
        private static void Complete(Instance inst, bool won, string why)
        {
            lock (inst)
            {
                if (inst.Won || inst.Failed) return;
                if (inst.Abort) { Cancel(inst); return; }
                if (inst.PendingKind < 0)
                {
                    inst.PendingKind = won && inst.Wave >= 8 ? 1 : inst.Wave >= 4 ? 2 : 0;
                    inst.PendingReason = why;
                }
                try
                {
                    if (inst.PlayerUid == 0) throw new InvalidOperationException("Beast result owner is missing.");
                    using (var c = DataHolder.MySqlConnection)
                    {
                        c.Open(); using (var t = c.BeginTransaction())
                        {
                            QuestInventory.Execute(c,t,@"INSERT INTO revora_beast_results(run,uid,kind) VALUES(@run,@uid,@kind)
ON CONFLICT(run,uid) DO NOTHING", "run",inst.ResultId,"uid",(long)inst.PlayerUid,"kind",inst.PendingKind);
                            using (var q = QuestInventory.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_beast_results WHERE run=@run AND uid=@uid AND kind=@kind)",
                                "run",inst.ResultId,"uid",(long)inst.PlayerUid,"kind",inst.PendingKind))
                                if (!Equals(q.ExecuteScalar(),true)) throw new InvalidOperationException("Beast result was not saved.");
                            t.Commit();
                        }
                    }
                }
                catch (Exception ex) { Console.WriteLine("[beast-result] error=" + ex.GetType().Name); return; }
                inst.Won = inst.PendingKind == 1; inst.Failed = !inst.Won;
                try
                {
                    GameState client;
                    if (Kernel.GamePool.TryGetValue(inst.PlayerUid, out client) && QuestInventory.Player(client, false))
                    {
                        client.Send(new Message(inst.Won ? "Eight waves cleared. Your purse and bound P7 soul await at BeastTrial."
                            : inst.PendingKind == 2 ? "Your fourth-wave consolation awaits at BeastTrial."
                            : "Beast Trial failed. " + inst.PendingReason, Color.Yellow, Message.TopLeft));
                    }
                }
                finally { Close(inst); }
            }
        }

        private static void Cancel(Instance inst)
        {
            GameState client;
            if (Kernel.GamePool.TryGetValue(inst.PlayerUid, out client) && QuestInventory.Player(client, false))
            {
                client.Send(new Message("The trial cannot continue. Return to BeastTrial later.", Color.Yellow, Message.TopLeft));
            }
            Close(inst);
        }

        private static void Close(Instance inst)
        {
            if (inst.Closed) return;
            try { BeastAdmissions.End(inst.ResultId.ToString("N")); }
            catch (Exception ex) { Console.WriteLine("[beast-end] error=" + ex.GetType().Name); return; }
            lock (Gate) PendingReturns.Add(inst.PlayerUid);
            ProcessReturns();
            try { if (!BeastAdmissions.Returned(inst.ResultId.ToString("N"))) return; }
            catch (Exception ex) { Console.WriteLine("[beast-release] error=" + ex.GetType().Name); return; }
            if (inst.OwnedArena && Kernel.GamePool.Values.Any(p => p != null && !p.Fake && p.Entity != null && p.Entity.MapID == inst.Arena.ID)) return;
            lock (Gate)
            {
                Live.Remove(inst);
                if (inst.Arena != null)
                    ByMap.Remove(inst.Arena.ID);
                if (inst.OwnedArena)
                {
                    Map current;
                    if (Kernel.Maps.TryGetValue(inst.Arena.ID, out current) && ReferenceEquals(current, inst.Arena))
                    {
                        inst.Arena.Entities.Clear(); inst.Arena.FloorItems.Clear();
                        Constants.PKFreeMaps.Remove(inst.Arena.ID); inst.Arena.Dispose();
                    }
                }
                inst.Closed = true;
            }
        }

        private static void ReviveAtSpawn(Instance inst, GameState client)
        {
            if (client.Entity == null) return;
            client.Entity.Hitpoints = client.Entity.MaxHitpoints / 2;
            client.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.Dead | (ulong)FlagsUpdate.Flags.Ghost);
            ushort x = inst.SpawnX;
            ushort y = inst.SpawnY;
            inst.Arena.SelectCoordonates(ref x, ref y);
            client.Entity.Teleport(inst.Arena.BaseID, inst.Arena.ID, x, y);
            client.CantAttack = Time32.Now.AddSeconds(5);
            client.Attackable = false;
            client.ReviveStamp = Time32.Now;
            client.Send(new Message("You rise. Lives left: " + inst.LivesLeft + ".", Color.Orange, Message.TopLeft));
        }

        private static string TryClaim(GameState client)
        {
            if (!QuestInventory.Player(client) || client.Variables == null)
                return "Return when you are ready to claim your prize.";
            lock (client.ItemSyncRoot) lock (client.Variables.SyncRoot)
            try
            {
                if (!QuestInventory.Player(client) || !HasClaim(client))
                    return "You have no Beast Trial prize to claim.";
                uint kind = (uint)client[ClaimKey];
                if (kind == 1 && client.Inventory.Count >= 40)
                    return "Leave one space for your soul. Your prize will wait.";
                uint cp = kind == 1 ? Revora.Config.BeastTrialWinCp : Revora.Config.BeastTrialConsolationCp;
                ulong gold = kind == 1 ? Revora.Config.BeastTrialWinGold : Revora.Config.BeastTrialConsolationGold;
                uint rp = kind == 1 ? Revora.Config.BeastTrialWinRevoraPoints : Revora.Config.BeastTrialConsolationRevoraPoints;
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
                        if (before == null) throw new InvalidOperationException("Beast Trial entitlement is not saved.");
                        var saved = new VariableVault(); saved.Deserialize(before);
                        if ((uint)saved[ClaimKey] != kind)
                            throw new InvalidOperationException("Beast Trial placement changed.");
                        if (QuestInventory.Execute(c, t, @"UPDATE entities SET conquerpoints=@cp,revorapoint=@rp,money=@gold
    WHERE uid=@uid AND conquerpoints=@oldcp AND revorapoint=@oldrp",
                            "cp", nextCp, "rp", nextRp, "gold", nextGold, "uid", (long)client.Entity.UID,
                            "oldcp", (long)client.Entity.ConquerPoints, "oldrp", (long)client.Entity.RevoraPoint) != 1)
                            throw new InvalidOperationException("Beast Trial purse changed.");
                        if (QuestInventory.Execute(c, t, @"UPDATE variablevault SET data=@data
    WHERE entityid=@uid AND data=@before", "data", next.Serialize(), "uid", (long)client.Entity.UID, "before", before) != 1)
                            throw new InvalidOperationException("Beast Trial entitlement changed.");
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
                QuestInventory.Fail(client, "beast-prize", ex);
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
                            throw new InvalidOperationException("Beast prize variables changed.");
                        var saved = new VariableVault(); saved.Deserialize(before);
                        if ((uint)saved[ClaimKey] != 0)
                            throw new InvalidOperationException("Beast prize placement changed.");
                        Guid run;
                        using (var q = QuestInventory.Command(c,t,@"SELECT run,kind FROM revora_beast_results
WHERE uid=@uid AND kind IN (1,2) AND NOT imported ORDER BY recorded_at,run LIMIT 1 FOR UPDATE","uid",(long)client.Entity.UID))
                        using (var r = q.ExecuteReader())
                        {
                            if (!r.Read()) return false;
                            run = r.GetGuid(0); place = (uint)r.GetInt32(1);
                        }
                        var next = new VariableVault(); next.Deserialize(client.Variables.Serialize()); next[ClaimKey] = place;
                        if (QuestInventory.Execute(c,t,"UPDATE variablevault SET data=@data WHERE entityid=@uid AND data=@before",
                            "data",next.Serialize(),"uid",(long)client.Entity.UID,"before",before) != 1
                            || QuestInventory.Execute(c,t,"UPDATE revora_beast_results SET imported=true WHERE run=@run AND uid=@uid AND NOT imported",
                            "run",run,"uid",(long)client.Entity.UID) != 1)
                            throw new InvalidOperationException("Beast prize import was not saved.");
                        t.Commit(); next.Changed = false; client.Variables = next;
                        return true;
                    }
                }
            }
            catch (Exception ex) { QuestInventory.Fail(client,"beast-import",ex); return false; }
        }

        private static Instance FindByPlayer(uint uid)
        {
            lock (Gate)
            {
                for (int i = 0; i < Live.Count; i++)
                {
                    Instance inst = Live[i];
                    if (inst.Player != null && inst.Player.Entity != null && inst.Player.Entity.UID == uid)
                        return inst;
                }
            }
            return null;
        }
    }
}
