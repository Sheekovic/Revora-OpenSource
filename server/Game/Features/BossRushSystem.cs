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
    public static class BossRushSystem
    {
        public const uint NpcId = 900000010;
        public const string NpcName = "BossRush";
        public const int MaxSolos = 16;
        public const int Lives = 2;
        public const int BossCount = 5;
        public const string ClaimKey = "rush-claim";
        public const string ClaimRunKey = "rush-claim-run";
        public const string PendingKindKey = "rush-pending-kind";
        public const string PendingRunKey = "rush-pending-run";
        public const ushort TwinCityMap = 1002;
        public const ushort TwinCityX = 408;
        public const ushort TwinCityY = 330;
        public const ushort ClaimX = 370;
        public const ushort ClaimY = 303;
        // Twin City SolarSaint look. Distinct from BeastTrial (6327) and WaveWarden (5150).
        public const ushort Mesh = 50070;

        private static readonly TimeSpan RegisterWindow = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan FightLimit = TimeSpan.FromMinutes(12);
        internal static Action<GameState> AfterClaimCommit;
        internal static Action<GameState> AfterIssueCommit;
        internal static Action<GameState> AfterRunRecorded;
        // Test seam: the monster is inserted, but its confirmation does not commit.
        internal static bool HoldWaveConfirm;
        internal static bool HoldReturn;
        static BossRushSystem() { AfterClaimCommit = null; AfterIssueCommit = null; AfterRunRecorded = null; HoldWaveConfirm = false; HoldReturn = false; }
        private static readonly object Gate = new object();
        private static readonly Queue<uint> DeferredRecovery = new Queue<uint>();
        private static readonly HashSet<uint> DeferredRecoveryIds = new HashSet<uint>();
        private static readonly List<Instance> Live = new List<Instance>();
        private static readonly HashSet<Instance> Closing = new HashSet<Instance>();
        private static readonly Dictionary<ushort, Instance> ByMap = new Dictionary<ushort, Instance>();
        private static readonly HashSet<uint> Registered = new HashSet<uint>();
        // Looks from catalog bosses. Combat stats are ours: old 8k-32k HP with
        // city-trash attack and no Defence was a one-shot. Defence is ushort.
        private static readonly RushBoss[] Bosses = new RushBoss[]
        {
            new RushBoss(7484, "Misery Monk", 1000000, 4000, 30, 5000, 8000, 140),
            new RushBoss(7486, "Awakened Flame", 3000000, 5000, 35, 8000, 12000, 140),
            new RushBoss(4212, "Thrilling Spook", 6000000, 6000, 40, 11000, 16000, 140),
            new RushBoss(7487, "Furious Flame", 9000000, 7000, 45, 15000, 22000, 140),
            new RushBoss(4220, "Tyrant Nemesis", 12000000, 8000, 50, 20000, 28000, 140)
        };
        // Percent of the player's max HP per boss hit. Super-set defence
        // otherwise reduces catalog attack to 1.
        private static readonly int[] HitPercent = new int[] { 8, 11, 14, 18, 22 };

        private struct RushBoss
        {
            public readonly uint Id;
            public readonly string Name;
            public readonly uint Hp;
            public readonly ushort Defence;
            public readonly ushort MagicDefence;
            public readonly uint MinAttack;
            public readonly uint MaxAttack;
            public readonly byte Level;

            public RushBoss(uint id, string name, uint hp, ushort defence, ushort magicDefence, uint minAttack, uint maxAttack, byte level)
            {
                Id = id;
                Name = name;
                Hp = hp;
                Defence = defence;
                MagicDefence = magicDefence;
                MinAttack = minAttack;
                MaxAttack = maxAttack;
                Level = level;
            }
        }
        private static DateTime CycleStart;
        private static bool RegisterOpen;
        private static bool Invited;

        private sealed class Instance
        {
            public Map Arena;
            public bool OwnedArena;
            public GameState Player;
            public int LivesLeft = Lives;
            public int Boss;
            public DateTime FightUntil;
            public DateTime LastDeath;
            public ushort SpawnX;
            public ushort SpawnY;
            public bool Won;
            public bool Failed;
            public bool? PendingVictory;
            public string PendingReason;
            public uint PendingClaim;
            public ulong RunId;
            public bool LifeDropPending;
            public int PendingLives;
            public bool Entered = true;
            // Boss is the last confirmed wave. PendingWave is the saved intent
            // until the monster insertion is checkpointed.
            public int PendingWave;
            public bool WaveMaterialized;
        }

        private const int PhaseActive = 1;
        // Written before the instance is registered. Recovery leaves this
        // phase alone only while this process still has the admission open.
        private const int PhaseAdmitting = 4;
        internal const int CheckpointConfirmed = 1;
        internal const int CheckpointConflicting = 2;
        internal const int CheckpointUncertain = 3;
        private const int PhaseFailed = 2;
        private const int PhaseWon = 3;

        private static ulong NextRunId()
        {
            try
            {
                using (var conn = DataHolder.MySqlConnection)
                {
                    conn.Open();
                    using (var seq = new Npgsql.NpgsqlCommand("CREATE SEQUENCE IF NOT EXISTS revora_boss_rush_run_seq", conn))
                        seq.ExecuteNonQuery();
                    using (var next = new Npgsql.NpgsqlCommand("SELECT nextval('revora_boss_rush_run_seq')", conn))
                    {
                        long id = Convert.ToInt64(next.ExecuteScalar());
                        if (id > 0) return (ulong)id;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[boss-rush] run id sequence failed type=" + ex.GetType().Name);
            }
            // A process-local fallback can collide with the durable sequence or
            // with an earlier process. Do not admit a run without a durable id.
            return 0;
        }

        public static void Create()
        {
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
            for (int h = 0; h < 24; h++)
            {
                DateTime slot = today.AddHours(h).AddMinutes(45);
                if (now < slot)
                    return slot;
            }
            return today.AddDays(1).AddMinutes(45);
        }

        public static bool InEvent(Entity entity)
        {
            if (entity == null) return false;
            lock (Gate)
                return ByMap.ContainsKey(entity.MapID);
        }

        public static void Tick()
        {
            DrainClosures();
            DrainLeaves();
            DrainRemovals();
            if (CycleStart == default(DateTime)) return;
            RetryDeferredRecovery();
            DateTime now = DateTime.Now;
            if (!Invited && now >= CycleStart.Subtract(RegisterWindow) && now < CycleStart)
            {
                Invited = true;
                RegisterOpen = true;
                Kernel.SendWorldMessage(new Message("Boss Rush registration is open for 5 minutes. Solo PVE. Fight five bosses. Talk to BossRush in Twin City (" + ClaimX + ", " + ClaimY + ").", Color.Yellow, Message.Center), Server.GamePool);
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
                        dialog.Option("Enter the rush", 1);
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
            if (inst == null) return;
            uint uid = client.Entity.UID;
            SessionInstall.Enter(uid);
            try { OnDeathOwned(inst, client); }
            finally { SessionInstall.Exit(uid); }
        }

        private static void OnDeathOwned(Instance inst, GameState client)
        {
            // Keep the death decision, checkpoint, and revival in the same
            // session transition. A waiting replacement must run afterwards.
            if (RetiredSession(inst) || inst.LifeDropPending) return;
            if (inst == null || !inst.Entered || inst.Won || inst.Failed || !object.ReferenceEquals(inst.Player, client)) return;
            // Life loss is independent of wave confirmation. Record it even
            // while that confirmation is unavailable, so this callback is not
            // forgotten and the player can be revived exactly once.
            if (inst.PendingVictory.HasValue)
            {
                Finish(inst, inst.PendingVictory.Value, inst.PendingReason);
                return;
            }
            DateTime now = DateTime.Now;
            if (inst.LastDeath != default(DateTime) && (now - inst.LastDeath).TotalSeconds < 2)
                return;
            inst.LastDeath = now;
            if (inst.LivesLeft <= 1)
            {
                inst.LivesLeft = 0;
                Fail(inst, "You are out of lives.");
                return;
            }
            int nextLives = inst.LivesLeft - 1;
            int outcome = SaveCheckpoint(client, inst.RunId, DurableBoss(inst), nextLives, PhaseActive, false);
            if (outcome == CheckpointUncertain)
            {
                inst.LifeDropPending = true;
                inst.PendingLives = nextLives;
                return;
            }
            if (outcome != CheckpointConfirmed) return;
            inst.LifeDropPending = false;
            inst.LivesLeft = nextLives;
            ReviveAtSpawn(inst, client);
        }

        public static void OnDisconnect(GameState client)
        {
            if (client == null || client.Entity == null) return;
            // Login replacement can invoke this while another thread holds
            // Gate. Metadata lookup must not wait, or publication blocks.
            if (!System.Threading.Monitor.TryEnter(Gate))
            {
                lock (PendingLeave) PendingLeave.Add(client);
                return;
            }
            Instance inst;
            try { inst = FindHeld(client.Entity.UID); }
            finally { System.Threading.Monitor.Exit(Gate); }
            if (inst == null || !object.ReferenceEquals(inst.Player, client)) return;
            Fail(inst, "You left the field.");
        }

        public static bool TryRushHit(Entity attacker, Entity attacked, out uint damage)
        {
            damage = 1;
            if (attacker == null || attacked == null)
                return false;
            if (attacker.EntityFlag != EntityFlag.Monster || attacked.EntityFlag != EntityFlag.Player)
                return false;
            Instance inst = FindByMap(attacked.MapID);
            if (inst == null || !inst.Entered || inst.Won || inst.Failed)
                return false;
            int idx = inst.Boss - 1;
            if (idx < 0) idx = 0;
            if (idx >= HitPercent.Length) idx = HitPercent.Length - 1;
            uint max = attacked.MaxHitpoints;
            if (max < 100) max = 100;
            int pct = HitPercent[idx];
            pct = pct * (90 + Kernel.Random.Next(0, 21)) / 100;
            if (pct < 1) pct = 1;
            damage = max * (uint)pct / 100;
            if (damage < 1) damage = 1;
            return true;
        }

        public static string GmEnter(GameState client)
        {
            if (client == null || client.Entity == null) return "Unable to start.";
            if (client.Entity.Dead) return "You are dead.";
            if (HasTeammates(client)) return "This rush is solo. Leave your team first.";
            if (InEvent(client.Entity)) return "You are already in an instance.";
            if (BeastTrialSystem.InEvent(client.Entity)) return "Finish Beast Trial first.";
            string admitted = Admit(client, false, false);
            if (admitted != null) return admitted;
            return "Boss Rush test started. Five bosses. Two lives. Twelve minutes.";
        }

        private static string StatusText()
        {
            if (RegisterOpen)
                return "Registration is open until " + CycleStart.ToString("HH:mm") + ". Solo only. Fight five bosses one after another on a private map. Heals are yours. Two lives. Twelve minutes. Victory also earns a bound P7 soul.";
            return "Next Boss Rush " + CycleStart.ToString("HH:mm") + ". Every hour at :45. Register 5 minutes before. Solo PVE. Five bosses. 2 lives. 12 minutes. Victory also earns a bound P7 soul.";
        }

        private static void InviteAll()
        {
            foreach (GameState client in Kernel.GamePool.Values)
            {
                if (client == null || client.Entity == null || client.Entity.Dead || client.Fake) continue;
                GameState captured = client;
                captured.MessageOK = p => { TryRegister(p); };
                captured.Send(new NpcReply(NpcReply.MessageBox, "Boss Rush is open. Solo PVE. Fight five bosses. Register at BossRush (" + ClaimX + ", " + ClaimY + ")?"));
                captured.Send(new Data(true) { UID = captured.Entity.UID, dwParam = 60, ID = Data.CountDown });
            }
        }

        private static string TryRegister(GameState client)
        {
            if (client == null || client.Entity == null) return "Unable to register.";
            if (!RegisterOpen) return "Registration is closed.";
            if (client.Entity.Dead) return "You are dead.";
            if (client.Entity.MapID != TwinCityMap) return "Stand in Twin City first.";
            if (HasTeammates(client)) return "This rush is solo. Leave your team first.";
            if (InEvent(client.Entity)) return "You are already in an instance.";
            if (BeastTrialSystem.InEvent(client.Entity)) return "Finish Beast Trial first.";
            string admitted = Admit(client, true, true);
            if (admitted != null) return admitted;
            return "You are in. Five bosses. Two lives. Heals are yours. Twelve minutes.";
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

        private static Instance PrepareInstance(GameState client, ulong runId)
        {
            if (client == null || client.Entity == null || runId == 0) return null;
            Map source = null;
            if (Kernel.Maps.ContainsKey(700))
                source = Kernel.Maps[700];
            else if (Kernel.Maps.ContainsKey(1005))
                source = Kernel.Maps[1005];
            else if (Kernel.Maps.ContainsKey(1002))
                source = Kernel.Maps[1002];
            if (source == null) return null;
            Map arena = source.MakeDynamicMap();
            if (arena == null) return null;
            if (!Constants.PKFreeMaps.Contains(arena.ID))
                Constants.PKFreeMaps.Add(arena.ID);
            Instance inst = new Instance();
            inst.RunId = runId;
            inst.Arena = arena;
            inst.OwnedArena = true;
            inst.Player = client;
            inst.FightUntil = DateTime.Now.Add(FightLimit);
            Tuple<ushort, ushort> spawn = arena.RandomCoordinates();
            inst.SpawnX = spawn.Item1;
            inst.SpawnY = spawn.Item2;
            ushort x = inst.SpawnX;
            ushort y = inst.SpawnY;
            arena.SelectCoordonates(ref x, ref y);
            inst.SpawnX = x;
            inst.SpawnY = y;
            inst.Entered = false;
            return inst;
        }

        private static bool EnterArena(Instance inst, bool announce)
        {
            GameState client = inst.Player;
            if (client == null || client.Entity == null || inst.Arena == null) return false;
            uint uid = client.Entity.UID;
            // Lease then teleport-guard locks. Installation waits until this
            // call returns, so it cannot publish while a guard is held and
            // the old session cannot enter after being replaced.
            SessionInstall.Enter(uid);
            try
            {
                if (!SessionOwns(client) || !AdmissionOpen(inst.RunId)) return false;
                client.Entity.Teleport(inst.Arena.BaseID, inst.Arena.ID, inst.SpawnX, inst.SpawnY);
                if (!SessionOwns(client) || client.Entity.MapID != inst.Arena.ID) return false;
                inst.Entered = true;
                client.Send(new Message("Boss Rush: five bosses, one at a time. Two lives. Heals are yours. 12 minutes.", Color.Yellow, Message.TopLeft));
                if (announce && Server.GamePool != null)
                    Kernel.SendWorldMessage(new Message(client.Entity.Name + " entered Boss Rush.", Color.Orange, Message.Center), Server.GamePool);
                StartBoss(inst, 1);
                return SessionOwns(client) && inst.Entered;
            }
            finally { SessionInstall.Exit(uid); }
        }

        private static void StartBoss(Instance inst, int number)
        {
            // Registry operations cannot perform database or gameplay work.
            // An old caller holding Gate must retry outside it, never bypass
            // the ownership checkpoint.
            if (System.Threading.Monitor.IsEntered(Gate) || inst == null
                || inst.Player == null || inst.Player.Entity == null) return;
            uint uid = inst.Player.Entity.UID;
            SessionInstall.Enter(uid);
            try
            {
                // Serialize the live application as well as its checkpoint.
                // A second tick may have selected this number before waiting.
                if (RetiredSession(inst) || inst.Won || inst.Failed || !inst.Entered
                    || inst.PendingVictory.HasValue || inst.LifeDropPending
                    || number != inst.Boss + 1 || number > BossCount) return;
                int idx = number - 1;
                if (idx < 0 || idx >= Bosses.Length) return;
                // Missing catalog data is a retryable spawn failure, not proof
                // that the next boss existed. Capture the template before its
                // progress can become eligible for cold-recovery rewards.
                MonsterInformation template;
                if (inst.Arena == null
                    || (!MonsterInformation.MonsterInformations.TryGetValue(Bosses[idx].Id, out template)
                        && !MonsterInformation.MonsterInformations.TryGetValue(2, out template))
                    || template == null) return;
                // Checkpoint while this lease is held and before the monster
                // exists, so a second tick waits here instead of spawning too.
                // The boss number is recorded before the monster exists. It
                // stays an intent until the insertion itself is checkpointed.
                if (inst.PendingWave != number)
                {
                    if (!SaveProgress(inst.Player, inst.RunId, number, inst.LivesLeft, PhaseActive)) return;
                    if (RetiredSession(inst) || !inst.Entered || number != inst.Boss + 1) return;
                    inst.PendingWave = number;
                    inst.WaveMaterialized = false;
                }
                else if (RetiredSession(inst) || !inst.Entered) return;
                if (inst.WaveMaterialized || RemainingMobs(inst) > 0)
                {
                    inst.WaveMaterialized = true;
                    AcceptInsertedWave(inst);
                    return;
                }
                if (!SpawnBoss(inst, Bosses[idx], template)) return;
                inst.WaveMaterialized = true;
                AcceptInsertedWave(inst);
            }
            finally { SessionInstall.Exit(uid); }
        }

        private static bool SpawnBoss(Instance inst, RushBoss boss, MonsterInformation mt)
        {
            if (inst == null || inst.Arena == null) return false;
            ushort x = inst.SpawnX;
            ushort y = inst.SpawnY;
            inst.Arena.SelectCoordonates(ref x, ref y);
            Entity entity = new Entity(EntityFlag.Monster, false);
            entity.MapObjType = MapObjectType.Monster;
            entity.MonsterInfo = mt.Copy();
            entity.MonsterInfo.Owner = entity;
            entity.MonsterInfo.BoundX = inst.SpawnX;
            entity.MonsterInfo.BoundY = inst.SpawnY;
            entity.MonsterInfo.Boss = true;
            entity.MonsterInfo.Hitpoints = boss.Hp;
            entity.MonsterInfo.Defence = boss.Defence;
            entity.MonsterInfo.MinAttack = boss.MinAttack;
            entity.MonsterInfo.MaxAttack = boss.MaxAttack;
            entity.MonsterInfo.ViewRange = 24;
            entity.MonsterInfo.AttackRange = 3;
            entity.MonsterInfo.AttackSpeed = 800;
            entity.MonsterInfo.SpellID = 0;
            entity.Name = boss.Name;
            entity.MinAttack = boss.MinAttack;
            entity.MaxAttack = entity.MagicAttack = boss.MaxAttack;
            entity.Defence = boss.Defence;
            entity.MagicDefence = boss.MagicDefence;
            entity.UID = inst.Arena.EntityUIDCounter.Next;
            entity.MapID = inst.Arena.ID;
            entity.X = x;
            entity.Y = y;
            entity.Body = mt.Mesh;
            entity.Level = boss.Level;
            entity.MonsterInfo.Boss = true;
            entity.MaxHitpoints = boss.Hp;
            entity.Hitpoints = boss.Hp;
            entity.ApplyMonsterBossHud();
            entity.SendUpdates = true;
            // AddEntity can return without inserting (for example, a UID
            // collision). Confirm this exact monster, and retain insertion
            // even if a later floor/spawn notification throws.
            try { inst.Arena.AddEntity(entity); }
            finally
            {
                Entity inserted;
                inst.WaveMaterialized = inst.Arena.Entities.TryGetValue(entity.UID, out inserted)
                    && object.ReferenceEquals(inserted, entity);
            }
            if (!inst.WaveMaterialized) return false;
            if (inst.Player != null)
            {
                entity.SendSpawn(inst.Player);
                FlagsUpdate upd = new FlagsUpdate(true);
                upd.UID = entity.UID;
                upd.Append((byte)FlagsUpdate.DataType.MaxHitpoints, ShownLife(entity.MaxHitpoints, entity.MaxHitpoints));
                upd.Append((byte)FlagsUpdate.DataType.Hitpoints, ShownLife(entity.Hitpoints, entity.MaxHitpoints));
                inst.Player.Send(upd);
            }
            return true;
        }

        private static uint ShownLife(uint current, uint max)
        {
            if (max < 10000) return current;
            uint key = max / 10000;
            if (key == 0) return current;
            uint shown = current / key;
            if (shown < 1 && current > 0) return 1;
            return shown;
        }

        private static bool RetiredSession(Instance inst)
        {
            if (inst == null || inst.Player == null || inst.Player.Entity == null || Kernel.GamePool == null)
                return true;
            GameState current;
            return !Kernel.GamePool.TryGetValue(inst.Player.Entity.UID, out current)
                || !object.ReferenceEquals(current, inst.Player);
        }

        private static void TickInstance(Instance inst, DateTime now)
        {
            // Gate is metadata-only. Never acquire a session lease under it.
            if (System.Threading.Monitor.IsEntered(Gate) || inst == null
                || inst.Player == null || inst.Player.Entity == null) return;
            uint uid = inst.Player.Entity.UID;
            SessionInstall.Enter(uid);
            try { TickInstanceOwned(inst, now); }
            finally { SessionInstall.Exit(uid); }
        }

        private static void TickInstanceOwned(Instance inst, DateTime now)
        {
            if (inst.Won || inst.Failed) return;
            if (RetiredSession(inst))
            {
                // The current session's recovery writes the terminal phase.
                // Removing the registry entry here would look like a boss
                // checkpoint that has not been registered yet.
                GameState current;
                if (Kernel.GamePool != null
                    && inst.Player != null && inst.Player.Entity != null
                    && Kernel.GamePool.TryGetValue(inst.Player.Entity.UID, out current))
                    ScheduleRecovery(current);
                return;
            }
            // Published, but the player has not passed the teleport guards yet.
            if (!inst.Entered) return;
            bool reviveAfterCheckpoint = false;
            if (inst.LifeDropPending)
            {
                if (inst.Player == null) return;
                int outcome = SaveCheckpoint(inst.Player, inst.RunId, DurableBoss(inst), inst.PendingLives, PhaseActive, false);
                if (outcome == CheckpointUncertain) return;
                if (outcome != CheckpointConfirmed)
                {
                    inst.LifeDropPending = false;
                    return;
                }
                inst.LivesLeft = inst.PendingLives;
                inst.LifeDropPending = false;
                reviveAfterCheckpoint = true;
                if (inst.LivesLeft <= 0)
                {
                    Fail(inst, "You are out of lives.");
                    return;
                }
            }
            if (inst.PendingWave != 0 && (inst.WaveMaterialized || RemainingMobs(inst) > 0))
            {
                inst.WaveMaterialized = true;
                AcceptInsertedWave(inst);
                if (inst.PendingWave != 0)
                {
                    if (reviveAfterCheckpoint && inst.Player != null)
                        ReviveAtSpawn(inst, inst.Player);
                    return;
                }
            }
            if (inst.PendingVictory.HasValue)
            {
                Finish(inst, inst.PendingVictory.Value, inst.PendingReason);
                return;
            }
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
            if (inst.Arena != null && inst.Player.Entity.MapID != inst.Arena.ID)
            {
                Fail(inst, "You left the field.");
                return;
            }
            if (reviveAfterCheckpoint)
                ReviveAtSpawn(inst, inst.Player);
            // An unresolved miss is the same wave again. An empty arena is not
            // a clear and does not open the following boss.
            if (inst.PendingWave != 0)
            {
                StartBoss(inst, inst.PendingWave);
                if (inst.PendingWave != 0 || inst.Won || inst.Failed) return;
            }
            if (inst.Boss < BossCount && RemainingMobs(inst) == 0)
                StartBoss(inst, inst.Boss + 1);
            else if (inst.Boss == BossCount && RemainingMobs(inst) == 0)
                Win(inst);
            if (inst.Player != null)
                inst.Player.Send(new Message("Boss " + inst.Boss + "/" + BossCount + "  Lives " + inst.LivesLeft, Color.Yellow, Message.FirstRightCorner));
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

        private static void Win(Instance inst)
        {
            Finish(inst, true, null);
        }

        private static void Fail(Instance inst, string why)
        {
            Finish(inst, false, why);
        }

        // Keep the chosen outcome on a write failure. The next tick retries
        // that outcome before advancing combat or evaluating a later deadline.
        private static void Finish(Instance inst, bool victory, string why)
        {
            if (inst == null) return;
            GameState client = inst.Player;
            // Lease before the owner lock. Login replacement holds the lease
            // and may take the owner lock from disconnect. The opposite order
            // deadlocks. Claim consumption uses this same owner lock.
            uint finishUid = client != null && client.Entity != null ? client.Entity.UID : 0;
            if (finishUid != 0) SessionInstall.Enter(finishUid);
            try
            {
            lock (client != null && client.ItemSyncRoot != null ? client.ItemSyncRoot : inst)
            {
                if (inst.Won || inst.Failed) return;
                // Retain the first terminal choice even if confirmation must
                // wait. Compute its prize only after the wave is reconciled.
                if (!inst.PendingVictory.HasValue)
                {
                    inst.PendingVictory = victory;
                    inst.PendingReason = why;
                }
                // An inserted wave has to be confirmed before this call chooses
                // a prize or a terminal phase. A miss keeps the prior boss.
                if (inst.PendingWave != 0 && (inst.WaveMaterialized || RemainingMobs(inst) > 0))
                {
                    inst.WaveMaterialized = true;
                    AcceptInsertedWave(inst);
                    if (inst.PendingWave != 0) return;
                }
                inst.PendingClaim = inst.PendingVictory.Value ? 1u : (inst.Boss >= 3 ? 2u : 0u);
                if (inst.RunId == 0) inst.RunId = NextRunId();
                if (inst.RunId == 0) return;
                victory = inst.PendingVictory.Value;
                why = inst.PendingReason;
                // Progress commits before the outcome row. A rejected outcome insert
                // can still be rebuilt from this boss, life count, and phase.
                // The saved number may be the open intent; the claim uses the
                // last confirmed boss, which is what inst.Boss now is.
                if (!SaveProgress(client, inst.RunId, DurableBoss(inst), inst.LivesLeft, victory ? PhaseWon : PhaseFailed))
                    return;
                if (inst.PendingClaim != 0 && client != null && client.Entity != null && !TryIssue(client, inst.PendingClaim, inst.RunId))
                    return;
                if (victory) inst.Won = true;
                else inst.Failed = true;
            }
            // Metadata only. A disconnect callback must not wait here while
            // login or a test still holds Gate.
            if (!System.Threading.Monitor.TryEnter(Gate))
            {
                lock (PendingRemoval) PendingRemoval.Add(inst);
            }
            else
            {
                try { RemoveHeld(inst); }
                finally { System.Threading.Monitor.Exit(Gate); }
            }
            QueueClosure(inst);
            if (client == null || client.Entity == null) return;
            if (inst.OwnedArena)
            {
                if (!ReturnClosedRuns(client)) ScheduleRecovery(client);
                else client.Send(new Message(victory ? "You slew all five bosses. Claim at BossRush." : "Boss Rush failed. " + why, Color.Yellow, Message.TopLeft));
                return;
            }
            try
            {
                if (victory)
                {
                    client.Entity.Teleport(TwinCityMap, ClaimX, ClaimY);
                    client.Send(new Message("You slew all five bosses. Claim at BossRush.", Color.Yellow, Message.TopLeft));
                    if (Server.GamePool != null)
                        Kernel.SendWorldMessage(new Message(client.Entity.Name + " cleared Boss Rush.", Color.Red, Message.Center), Server.GamePool);
                }
                else
                {
                    if (inst.Arena != null && client.Entity.MapID == inst.Arena.ID)
                        client.Entity.Teleport(TwinCityMap, TwinCityX, TwinCityY);
                    client.Send(new Message("Boss Rush failed. " + why, Color.Red, Message.TopLeft));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[boss-rush] prize issued but exit failed uid=" + client.Entity.UID + " type=" + ex.GetType().Name);
            }
            }
            finally
            {
                if (finishUid != 0) SessionInstall.Exit(finishUid);
            }
        }

        private static void EnsureProgress(Npgsql.NpgsqlConnection conn)
        {
            using (var cmd = new Npgsql.NpgsqlCommand(@"CREATE TABLE IF NOT EXISTS revora_boss_rush_progress (
entityid bigint NOT NULL,
run_id bigint NOT NULL,
boss integer NOT NULL,
lives integer NOT NULL,
phase integer NOT NULL,
PRIMARY KEY (entityid, run_id))", conn))
                cmd.ExecuteNonQuery();
            using (var alter = new Npgsql.NpgsqlCommand(
                "ALTER TABLE revora_boss_rush_progress ADD COLUMN IF NOT EXISTS spawned boolean NOT NULL DEFAULT true", conn))
                alter.ExecuteNonQuery();
            using (var alter = new Npgsql.NpgsqlCommand(
                "ALTER TABLE revora_boss_rush_progress ADD COLUMN IF NOT EXISTS soul_type bigint NOT NULL DEFAULT 0", conn))
                alter.ExecuteNonQuery();
            using (var alter = new Npgsql.NpgsqlCommand(
                "ALTER TABLE revora_boss_rush_progress ADD COLUMN IF NOT EXISTS arena integer NOT NULL DEFAULT 0; ALTER TABLE revora_boss_rush_progress ADD COLUMN IF NOT EXISTS returned boolean NOT NULL DEFAULT true", conn))
                alter.ExecuteNonQuery();
        }

        private sealed class AdmissionMark
        {
            public GameState Session;
            public long Epoch;
        }

        private static readonly Dictionary<ulong, AdmissionMark> OpenAdmissions = new Dictionary<ulong, AdmissionMark>();
        private static readonly List<GameState> PendingLeave = new List<GameState>();
        private static readonly List<Instance> PendingRemoval = new List<Instance>();

        private static void MarkAdmission(ulong runId, GameState client)
        {
            lock (OpenAdmissions)
                OpenAdmissions[runId] = new AdmissionMark { Session = client, Epoch = SessionInstall.Epoch(client.Entity.UID) };
        }

        private static void ClearAdmission(ulong runId)
        {
            lock (OpenAdmissions) OpenAdmissions.Remove(runId);
        }

        private static bool SessionOwns(GameState client)
        {
            if (client == null || client.Fake || client.Entity == null || Kernel.GamePool == null) return false;
            GameState current;
            return Kernel.GamePool.TryGetValue(client.Entity.UID, out current)
                && object.ReferenceEquals(current, client);
        }

        private static bool AdmissionOpen(ulong runId)
        {
            AdmissionMark mark;
            lock (OpenAdmissions)
            {
                if (!OpenAdmissions.TryGetValue(runId, out mark)) return false;
            }
            if (mark.Session == null || mark.Session.Entity == null || !SessionOwns(mark.Session)) return false;
            return SessionInstall.Epoch(mark.Session.Entity.UID) == mark.Epoch;
        }

        // Test and failed registration drop the in-memory admission mark.
        // The durable row stays until recovery terminalizes it.
        private static void AbandonAdmission(ulong runId)
        {
            ClearAdmission(runId);
        }

        // Returns null when the player is in the arena.
        private static string Admit(GameState client, bool announce, bool checkRegistered)
        {
            RecoverPending(client);
            if (!QuestInventory.Player(client, false)) return "Please reconnect before entering Boss Rush.";
            try
            {
                using (var c = DataHolder.MySqlConnection)
                {
                    c.Open();
                    using (var q = QuestInventory.Command(c, null,
                        "SELECT EXISTS(SELECT 1 FROM revora_boss_rush_progress WHERE entityid=@uid AND arena<>0 AND NOT returned)", "uid", (long)client.Entity.UID))
                        if (Equals(q.ExecuteScalar(), true)) return "Your previous return is still pending. Please try again shortly.";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[boss-rush] admission return check failed type=" + ex.GetType().Name);
                return "Boss Rush registration is temporarily unavailable. Please try again.";
            }
            ulong runId = NextRunId();
            if (runId == 0 || !SaveProgress(client, runId, 1, Lives, PhaseAdmitting))
                return "Boss Rush registration is temporarily unavailable. Please try again.";
            string blocked = null;
            // Capacity check only. Map preparation and teleport stay outside
            // Gate. A replacement that won before this lock is not registered.
            lock (Gate)
            {
                if (!AdmissionOpen(runId))
                    blocked = "Boss Rush registration is temporarily unavailable. Please try again.";
                else if (Live.Count >= MaxSolos) blocked = "All pits are full (16).";
                else if (checkRegistered && Registered.Contains(client.Entity.UID)) blocked = "You already entered this round.";
            }
            if (blocked != null)
            {
                ClearAdmission(runId);
                return blocked;
            }
            Instance inst = PrepareInstance(client, runId);
            if (inst == null)
            {
                ClearAdmission(runId);
                return "No map could be opened.";
            }
            try
            {
                using (var c = DataHolder.MySqlConnection)
                {
                    c.Open();
                    if (QuestInventory.Execute(c, null,
                        "UPDATE revora_boss_rush_progress SET arena=@arena,returned=false WHERE entityid=@uid AND run_id=@run AND phase=@phase AND arena=0",
                        "arena", (int)inst.Arena.ID, "uid", (long)client.Entity.UID, "run", RunDb(runId), "phase", PhaseAdmitting) != 1)
                        throw new InvalidOperationException("Boss Rush arena ownership was not saved.");
                }
            }
            catch (Exception ex)
            {
                QueueClosure(inst); ClearAdmission(runId); ScheduleRecovery(client);
                Console.WriteLine("[boss-rush] admission arena failed type=" + ex.GetType().Name);
                return "Boss Rush registration is temporarily unavailable. Please try again.";
            }
            if (!AdmissionOpen(runId))
            {
                QueueClosure(inst); ClearAdmission(runId); ScheduleRecovery(client);
                return "Boss Rush registration is temporarily unavailable. Please try again.";
            }
            bool published = false;
            lock (Gate)
            {
                if (AdmissionOpen(runId) && Live.Count < MaxSolos
                    && (!checkRegistered || !Registered.Contains(client.Entity.UID)))
                {
                    Live.Add(inst);
                    ByMap[inst.Arena.ID] = inst;
                    if (checkRegistered) Registered.Add(client.Entity.UID);
                    published = true;
                }
            }
            if (!published || !AdmissionOpen(runId))
            {
                if (published) RemoveMetadata(inst);
                else QueueClosure(inst);
                ClearAdmission(runId);
                return "Boss Rush registration is temporarily unavailable. Please try again.";
            }
            if (!EnterArena(inst, announce) || !SessionOwns(client))
            {
                RemoveMetadata(inst);
                ClearAdmission(runId);
                return "Boss Rush registration is temporarily unavailable. Please try again.";
            }
            if (!SaveProgress(client, runId, 1, Lives, PhaseActive))
            {
                if (!SessionOwns(client))
                {
                    RemoveMetadata(inst);
                    ClearAdmission(runId);
                }
                else ScheduleRecovery(client);
            }
            return null;
        }

        private static int DurableBoss(Instance inst)
        {
            if (inst != null && inst.PendingWave > inst.Boss) return inst.PendingWave;
            return inst == null ? 0 : inst.Boss;
        }

        // Last boss whose monster insertion was checkpointed. An unconfirmed
        // advance keeps the previous boss. Legacy rows default to confirmed.
        private static int EligibleBoss(int boss, bool spawned)
        {
            if (spawned) return boss;
            return boss > 0 ? boss - 1 : 0;
        }

        private static void AcceptInsertedWave(Instance inst)
        {
            if (inst == null || inst.PendingWave == 0 || inst.Player == null || inst.Player.Entity == null) return;
            int number = inst.PendingWave;
            GameState writer = inst.Player;
            GameState current;
            if (Kernel.GamePool != null
                && Kernel.GamePool.TryGetValue(inst.Player.Entity.UID, out current)
                && current != null && current.Entity != null)
                writer = current;
            if (!ConfirmWave(writer, inst.RunId, number, inst.LivesLeft)) return;
            inst.Boss = number;
            inst.PendingWave = 0;
            inst.WaveMaterialized = false;
            int idx = number - 1;
            if (writer != null && idx >= 0 && idx < Bosses.Length)
                writer.Send(new Message("Boss " + number + " of " + BossCount + ": " + Bosses[idx].Name + ".", Color.Yellow, Message.TopLeft));
        }

        // Recovery holds the session lease and has not taken the vault yet.
        // Confirm a monster that is already on the map before settlement, so
        // an inserted wave is not abandoned as if it never landed.
        private static void ConfirmLiveWaves(GameState client)
        {
            if (client == null || client.Entity == null) return;
            Instance[] pending;
            if (!System.Threading.Monitor.TryEnter(Gate))
            {
                ScheduleRecovery(client);
                return;
            }
            try
            {
                var found = new List<Instance>();
                for (int i = 0; i < Live.Count; i++)
                {
                    Instance inst = Live[i];
                    if (inst == null || inst.Player == null || inst.Player.Entity == null) continue;
                    if (inst.Player.Entity.UID != client.Entity.UID || inst.PendingWave == 0) continue;
                    if (!inst.WaveMaterialized && RemainingMobs(inst) == 0) continue;
                    found.Add(inst);
                }
                pending = found.ToArray();
            }
            finally { System.Threading.Monitor.Exit(Gate); }
            for (int i = 0; i < pending.Length; i++)
            {
                pending[i].WaveMaterialized = true;
                AcceptInsertedWave(pending[i]);
            }
        }

        private static bool UnconfirmedMaterialized(ulong runId)
        {
            if (!System.Threading.Monitor.TryEnter(Gate)) return true;
            try
            {
                for (int i = 0; i < Live.Count; i++)
                {
                    Instance inst = Live[i];
                    if (inst == null || inst.RunId != runId || inst.Won || inst.Failed || inst.PendingWave == 0) continue;
                    if (inst.WaveMaterialized || RemainingMobs(inst) > 0) return true;
                }
            }
            finally { System.Threading.Monitor.Exit(Gate); }
            return false;
        }

        private static bool SaveProgress(GameState client, ulong runId, int boss, int lives, int phase)
        {
            return SaveCheckpoint(client, runId, boss, lives, phase, false) == CheckpointConfirmed;
        }

        private static bool ConfirmWave(GameState client, ulong runId, int boss, int lives)
        {
            if (HoldWaveConfirm) return false;
            return SaveCheckpoint(client, runId, boss, lives, PhaseActive, true) == CheckpointConfirmed;
        }

        // Confirmed: the requested boss, lives and phase were committed for
        // the session that still owned the character after deferred constraints.
        // Conflicting: stored state or a replacement session rejected the write.
        // Uncertain: the database did not say whether the write landed.
        private static int SaveCheckpoint(GameState client, ulong runId, int boss, int lives, int phase, bool confirmWave)
        {
            if (client == null || client.Entity == null || runId == 0 || phase == 0) return CheckpointConflicting;
            uint uid = client.Entity.UID;
            bool admitting = phase == PhaseAdmitting;
            // Lease covers the transaction through commit. Installation takes
            // the same lease before it publishes a session, so it cannot
            // finish in the middle of this commit. Gate is not involved.
            SessionInstall.Enter(uid);
            try
            {
                GameState current;
                if (client.Fake || Kernel.GamePool == null
                    || !Kernel.GamePool.TryGetValue(uid, out current)
                    || !object.ReferenceEquals(current, client))
                {
                    if (admitting) ClearAdmission(runId);
                    return CheckpointConflicting;
                }
                if (admitting) MarkAdmission(runId, client);
                using (var conn = DataHolder.MySqlConnection)
                {
                    conn.Open();
                    EnsureProgress(conn);
                    using (var tx = conn.BeginTransaction())
                    using (var cmd = new Npgsql.NpgsqlCommand(@"INSERT INTO revora_boss_rush_progress(entityid,run_id,boss,lives,phase,soul_type)
VALUES(@uid,@run,@boss,@lives,@phase,@soul)
ON CONFLICT (entityid, run_id) DO UPDATE
SET boss=GREATEST(revora_boss_rush_progress.boss, EXCLUDED.boss),
lives=LEAST(revora_boss_rush_progress.lives, EXCLUDED.lives), phase=EXCLUDED.phase,
spawned=CASE WHEN @confirm THEN true
WHEN EXCLUDED.boss > revora_boss_rush_progress.boss THEN false
ELSE revora_boss_rush_progress.spawned END
WHERE revora_boss_rush_progress.phase=@active OR revora_boss_rush_progress.phase=@admitting", conn, tx))
                    {
                        cmd.Parameters.AddWithValue("uid", (long)uid);
                        cmd.Parameters.AddWithValue("run", RunDb(runId));
                        cmd.Parameters.AddWithValue("boss", boss);
                        cmd.Parameters.AddWithValue("lives", lives);
                        cmd.Parameters.AddWithValue("phase", phase);
                        // Only a newly admitted run receives the new reward contract.
                        // ON CONFLICT deliberately preserves its first saved selection.
                        cmd.Parameters.AddWithValue("soul", admitting ? (long)QuestSoulReward.Create().ID : 0L);
                        cmd.Parameters.AddWithValue("confirm", confirmWave);
                        cmd.Parameters.AddWithValue("active", PhaseActive);
                        cmd.Parameters.AddWithValue("admitting", PhaseAdmitting);
                        cmd.ExecuteNonQuery();
                        bool confirmed;
                        using (var saved = new Npgsql.NpgsqlCommand(@"SELECT boss, lives, phase, spawned FROM revora_boss_rush_progress
WHERE entityid=@uid AND run_id=@run FOR UPDATE", conn, tx))
                        {
                            saved.Parameters.AddWithValue("uid", (long)uid);
                            saved.Parameters.AddWithValue("run", RunDb(runId));
                            using (var reader = saved.ExecuteReader())
                                confirmed = reader.Read() && reader.GetInt32(0) == boss
                                    && reader.GetInt32(1) == lives && reader.GetInt32(2) == phase
                                    && (!confirmWave || reader.GetBoolean(3));
                        }
                        // Deferred triggers run here, still before durability.
                        // A replacement that lands during that wait is visible
                        // to the decision below. Commit itself then has no
                        // deferred work left, and installation is still waiting
                        // on this lease.
                        using (var flush = new Npgsql.NpgsqlCommand("SET CONSTRAINTS ALL IMMEDIATE", conn, tx))
                            flush.ExecuteNonQuery();
                        // A monotonic merge can preserve one field while
                        // changing another. A conflicting readback must roll
                        // back the whole request, including phase changes.
                        if (!confirmed || Kernel.GamePool == null
                            || !Kernel.GamePool.TryGetValue(uid, out current)
                            || !object.ReferenceEquals(current, client))
                        {
                            tx.Rollback();
                            if (admitting) ClearAdmission(runId);
                            return CheckpointConflicting;
                        }
                        tx.Commit();
                        if (phase == PhaseActive) ClearAdmission(runId);
                        return CheckpointConfirmed;
                    }
                }
            }
            catch (Exception ex)
            {
                if (admitting) ClearAdmission(runId);
                Console.WriteLine("[boss-rush] progress checkpoint failed uid=" + uid + " type=" + ex.GetType().Name);
                return CheckpointUncertain;
            }
            finally
            {
                SessionInstall.Exit(uid);
            }
        }

        private static void ScheduleRecovery(GameState client)
        {
            if (client == null || client.Entity == null) return;
            lock (DeferredRecovery)
            {
                if (DeferredRecoveryIds.Add(client.Entity.UID))
                    DeferredRecovery.Enqueue(client.Entity.UID);
            }
        }

        private static void RetryDeferredRecovery()
        {
            uint[] pending;
            lock (DeferredRecovery)
            {
                if (DeferredRecovery.Count == 0) return;
                // Bound work per world tick, preserve FIFO order, and do not
                // retain disconnected sessions while waiting to retry.
                pending = new uint[Math.Min(MaxSolos, DeferredRecovery.Count)];
                for (int i = 0; i < pending.Length; i++)
                {
                    pending[i] = DeferredRecovery.Dequeue();
                    DeferredRecoveryIds.Remove(pending[i]);
                }
            }
            for (int i = 0; i < pending.Length; i++)
            {
                GameState current;
                if (Kernel.GamePool.TryGetValue(pending[i], out current))
                    RecoverPending(current);
            }
        }

        private static bool RunStillOwned(GameState recovering, ulong runId, out bool deferred, List<Instance> retiring)
        {
            // Recovery already owns the player, vault and database row locks.
            // Registry inspection must not block while holding those locks:
            // unknown ownership defers settlement to a later tick.
            deferred = false;
            if (recovering == null || recovering.Entity == null) return true;
            if (!System.Threading.Monitor.TryEnter(Gate))
            {
                deferred = true;
                return true;
            }
            try
            {
                GameState current;
                if (Kernel.GamePool == null
                    || !Kernel.GamePool.TryGetValue(recovering.Entity.UID, out current)
                    || !object.ReferenceEquals(current, recovering))
                {
                    deferred = true;
                    return true;
                }
                for (int i = Live.Count - 1; i >= 0; i--)
                {
                    Instance inst = Live[i];
                    if (inst == null || inst.Won || inst.Failed || inst.RunId != runId) continue;
                    if (inst.Player == null || inst.Player.Entity == null || inst.Player.Entity.UID != recovering.Entity.UID)
                        continue;
                    if (object.ReferenceEquals(inst.Player, recovering))
                        return true;
                    // Inspection must not destroy the retry evidence before
                    // the transaction has durably chosen this run's outcome.
                    retiring.Add(inst);
                }
            }
            finally { System.Threading.Monitor.Exit(Gate); }
            return false;
        }

        private static void EnsureRunLedger(Npgsql.NpgsqlConnection conn)
        {
            using (var cmd = new Npgsql.NpgsqlCommand(@"CREATE TABLE IF NOT EXISTS revora_boss_rush_runs (
entityid bigint NOT NULL,
run_id bigint NOT NULL,
kind integer NOT NULL,
consumed boolean NOT NULL DEFAULT false,
PRIMARY KEY (entityid, run_id))", conn))
                cmd.ExecuteNonQuery();
        }

        private static long RunDb(ulong runId)
        {
            if (runId > (ulong)long.MaxValue)
                throw new InvalidOperationException("Boss Rush run id does not fit the ledger.");
            return (long)runId;
        }

        // 1 = recorded or already issued with this kind, 0 = already consumed, -1 = conflicting kind.
        private static int RememberIssued(Npgsql.NpgsqlConnection conn, uint uid, ulong runId, uint kind)
        {
            EnsureRunLedger(conn);
            using (var tx = conn.BeginTransaction())
            {
                bool found = false;
                int storedKind = 0;
                bool consumed = false;
                using (var q = new Npgsql.NpgsqlCommand("SELECT kind, consumed FROM revora_boss_rush_runs WHERE entityid=@uid AND run_id=@run FOR UPDATE", conn, tx))
                {
                    q.Parameters.AddWithValue("uid", (long)uid);
                    q.Parameters.AddWithValue("run", RunDb(runId));
                    using (var reader = q.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            found = true;
                            storedKind = reader.GetInt32(0);
                            consumed = reader.GetBoolean(1);
                        }
                    }
                }
                if (!found)
                {
                    using (var ins = new Npgsql.NpgsqlCommand("INSERT INTO revora_boss_rush_runs(entityid,run_id,kind,consumed) VALUES(@uid,@run,@kind,false)", conn, tx))
                    {
                        ins.Parameters.AddWithValue("uid", (long)uid);
                        ins.Parameters.AddWithValue("run", RunDb(runId));
                        ins.Parameters.AddWithValue("kind", (int)kind);
                        if (ins.ExecuteNonQuery() != 1)
                            throw new InvalidOperationException("Boss Rush run was not recorded.");
                    }
                }
                tx.Commit();
                if (!found) return 1;
                if (consumed) return 0;
                if ((uint)storedKind != kind) return -1;
                return 1;
            }
        }

        private static void ConsumeRun(Npgsql.NpgsqlConnection conn, Npgsql.NpgsqlTransaction tx, uint uid, ulong runId, uint kind)
        {
            if (runId == 0) return;
            bool found = false;
            int storedKind = 0;
            bool consumed = false;
            using (var q = new Npgsql.NpgsqlCommand("SELECT kind, consumed FROM revora_boss_rush_runs WHERE entityid=@uid AND run_id=@run FOR UPDATE", conn, tx))
            {
                q.Parameters.AddWithValue("uid", (long)uid);
                q.Parameters.AddWithValue("run", RunDb(runId));
                using (var reader = q.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        found = true;
                        storedKind = reader.GetInt32(0);
                        consumed = reader.GetBoolean(1);
                    }
                }
            }
            if (found)
            {
                if (consumed || (uint)storedKind != kind)
                    throw new InvalidOperationException("Boss Rush run was already settled.");
                if (QuestInventory.Execute(conn, tx, "UPDATE revora_boss_rush_runs SET consumed=true WHERE entityid=@uid AND run_id=@run AND consumed=false AND kind=@kind",
                    "uid", (long)uid, "run", RunDb(runId), "kind", (int)kind) != 1)
                    throw new InvalidOperationException("Boss Rush run was already settled.");
                return;
            }
            if (QuestInventory.Execute(conn, tx, "INSERT INTO revora_boss_rush_runs(entityid,run_id,kind,consumed) VALUES(@uid,@run,@kind,true)",
                "uid", (long)uid, "run", RunDb(runId), "kind", (int)kind) != 1)
                throw new InvalidOperationException("Boss Rush run was not recorded.");
        }

        private static bool TryNextIssued(Npgsql.NpgsqlConnection conn, Npgsql.NpgsqlTransaction tx, uint uid, ulong except, out ulong runId, out uint kind)
        {
            runId = 0;
            kind = 0;
            using (var q = new Npgsql.NpgsqlCommand(@"SELECT run_id, kind FROM revora_boss_rush_runs
WHERE entityid=@uid AND consumed=false AND run_id<>@except ORDER BY run_id LIMIT 1 FOR UPDATE", conn, tx))
            {
                q.Parameters.AddWithValue("uid", (long)uid);
                q.Parameters.AddWithValue("except", RunDb(except));
                using (var reader = q.ExecuteReader())
                {
                    if (!reader.Read()) return false;
                    runId = (ulong)reader.GetInt64(0);
                    kind = (uint)reader.GetInt32(1);
                    return kind != 0 && runId != 0;
                }
            }
        }

        private static bool TryReadVault(Npgsql.NpgsqlConnection conn, uint uid, out uint claim, out ulong claimRun, out uint pendingKind, out ulong pendingRun)
        {
            claim = 0;
            claimRun = 0;
            pendingKind = 0;
            pendingRun = 0;
            Npgsql.NpgsqlTransaction tx = null;
            try
            {
                tx = conn.BeginTransaction();
                byte[] before;
                using (var q = QuestInventory.Command(conn, tx, "SELECT data FROM variablevault WHERE entityid=@uid FOR UPDATE", "uid", (long)uid))
                    before = q.ExecuteScalar() as byte[];
                tx.Commit();
                if (before != null && before.Length >= 4)
                {
                    var saved = new VariableVault();
                    saved.Deserialize(before);
                    claim = (uint)saved.ReadULong(ClaimKey);
                    claimRun = saved.ReadULong(ClaimRunKey);
                    pendingKind = (uint)saved.ReadULong(PendingKindKey);
                    pendingRun = saved.ReadULong(PendingRunKey);
                }
                return true;
            }
            catch
            {
                if (tx != null)
                {
                    try { tx.Rollback(); }
                    catch { }
                }
                return false;
            }
            finally
            {
                if (tx != null)
                {
                    try { tx.Dispose(); }
                    catch { }
                }
            }
        }

        private static bool TryIssue(GameState client, uint kind, ulong runId)
        {
            if (client == null || client.Entity == null || client.ItemSyncRoot == null || kind == 0 || runId == 0)
                return false;
            if (client.Variables == null)
                EntityVariableTable.Load(client);
            if (client.Variables == null)
                return false;
            lock (client.ItemSyncRoot)
            lock (client.Variables.SyncRoot)
            {
                try
                {
                    // The saved row is the stored image. This object's baseline,
                    // not that row, decides which live keys are still pending.
                    using (var conn = DataHolder.MySqlConnection)
                    {
                        conn.Open();
                        uint existing;
                        ulong existingRun;
                        uint pendingKind;
                        ulong pendingRun;
                        bool sawVault = TryReadVault(conn, client.Entity.UID, out existing, out existingRun, out pendingKind, out pendingRun);
                        if (sawVault)
                        {
                            if (pendingKind != 0 && pendingRun == runId && pendingKind != kind)
                                return false;
                            if (existing != 0 && existingRun == runId && existing != kind)
                                return false;
                        }
                        // Commit the outcome before the vault write. A later vault
                        // failure can be recovered, and a consumed row makes replay a no-op.
                        int recorded = RememberIssued(conn, client.Entity.UID, runId, kind);
                        if (recorded < 0) return false;
                        if (recorded == 0) return true;
                        if (!sawVault) return false;
                        Action<GameState> recordedHook = AfterRunRecorded;
                        if (recordedHook != null) recordedHook(client);
                        using (var tx = conn.BeginTransaction())
                        {
                            byte[] before;
                            using (var q = QuestInventory.Command(conn, tx, "SELECT data FROM variablevault WHERE entityid=@uid FOR UPDATE", "uid", (long)client.Entity.UID))
                                before = q.ExecuteScalar() as byte[];
                            // A replacement session may have claimed after RememberIssued
                            // committed. Lock vault then ledger, just as TryClaim does,
                            // and recheck the receipt before restoring any projection.
                            using (var q = QuestInventory.Command(conn, tx,
                                "SELECT kind, consumed FROM revora_boss_rush_runs WHERE entityid=@uid AND run_id=@run FOR UPDATE",
                                "uid", (long)client.Entity.UID, "run", RunDb(runId)))
                            using (var reader = q.ExecuteReader())
                            {
                                if (!reader.Read()) return false;
                                if (reader.GetBoolean(1)) return true;
                                if ((uint)reader.GetInt32(0) != kind) return false;
                            }
                            var saved = new VariableVault();
                            if (before != null && before.Length >= 4)
                                saved.Deserialize(before);
                            existing = (uint)saved.ReadULong(ClaimKey);
                            existingRun = saved.ReadULong(ClaimRunKey);
                            pendingKind = (uint)saved.ReadULong(PendingKindKey);
                            pendingRun = saved.ReadULong(PendingRunKey);
                            if (pendingKind != 0 && pendingRun == runId && pendingKind != kind)
                                return false;
                            if (existing != 0 && existingRun == runId)
                            {
                                if (existing != kind) return false;
                                tx.Commit();
                                ProjectClaim(client, existing, existingRun);
                                return true;
                            }
                            string[] names;
                            DynamicVariable[] forced;
                            if (existing != 0)
                            {
                                // The visible claim and one parked prize stay put.
                                // This run is already in the ledger, so later claims can surface it.
                                if (pendingKind != 0 && pendingRun != runId)
                                    return true;
                                names = new string[] { ClaimKey, ClaimRunKey, PendingKindKey, PendingRunKey };
                                forced = new DynamicVariable[] { existing, existingRun, kind, runId };
                            }
                            else if (pendingKind != 0 && pendingRun != runId)
                            {
                                names = new string[] { ClaimKey, ClaimRunKey };
                                forced = new DynamicVariable[] { kind, runId };
                            }
                            else
                            {
                                names = new string[] { ClaimKey, ClaimRunKey, PendingKindKey, PendingRunKey };
                                forced = new DynamicVariable[] { kind, runId, (DynamicVariable)0ul, (DynamicVariable)0ul };
                            }
                            byte[] merged;
                            if (!client.Variables.TryMerge(before, names, forced, out merged))
                                return false;
                            int rows = before == null
                                ? QuestInventory.Execute(conn, tx, "INSERT INTO variablevault(entityid,data) VALUES(@uid,@data)", "uid", (long)client.Entity.UID, "data", merged)
                                : QuestInventory.Execute(conn, tx, "UPDATE variablevault SET data=@data WHERE entityid=@uid AND data=@before", "data", merged, "uid", (long)client.Entity.UID, "before", before);
                            if (rows != 1)
                                throw new InvalidOperationException("Boss Rush claim changed.");
                            tx.Commit();
                            Action<GameState> hook = AfterIssueCommit;
                            if (hook != null) hook(client);
                            client.Variables.ReplaceContents(merged);
                            return true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[boss-rush] claim issue failed uid=" + client.Entity.UID + " type=" + ex.GetType().Name);
                    return false;
                }
            }
        }

        private static void ProjectClaim(GameState client, uint kind, ulong runId)
        {
            if (client.Variables.ReadULong(ClaimKey) != kind)
                client.Variables[ClaimKey] = kind;
            if (client.Variables.ReadULong(ClaimRunKey) != runId)
                client.Variables[ClaimRunKey] = runId;
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
            if (!HasClaim(client))
                return "You have no Boss Rush prize to claim.";
            uint kind = (uint)client[ClaimKey];
            ulong runId = client.Variables.ReadULong(ClaimRunKey);
            uint cp = Revora.Config.BossRushWinCp;
            ulong gold = Revora.Config.BossRushWinGold;
            uint rp = Revora.Config.BossRushWinRevoraPoints;
            string label = "victory";
            uint stoneId = 0;
            byte stones = 0;
            if (kind == 2)
            {
                cp = Revora.Config.BossRushConsolationCp;
                gold = Revora.Config.BossRushConsolationGold;
                rp = Revora.Config.BossRushConsolationRevoraPoints;
                label = "boss-3 consolation";
            }
            else
            {
                stoneId = Revora.Config.BossRushWinPerfectionStoneId;
                stones = Revora.Config.BossRushWinPerfectionStones;
                if (stones > 0 && !ConquerItemInformation.BaseInformations.ContainsKey(stoneId))
                    return "The configured Perfection Stone is unavailable. Your whole claim is waiting.";
            }
            if (client.ItemSyncRoot == null || client.Variables == null)
                return "You have no Boss Rush prize to claim.";
            lock (client.ItemSyncRoot) lock (client.Variables.SyncRoot)
            {
                try
                {
                    if (client.Entity.PersistenceRequiresReload)
                        return "Please reconnect before claiming your Boss Rush prize.";
                    if (!HasClaim(client) || (uint)client[ClaimKey] != kind
                        || client.Variables.ReadULong(ClaimRunKey) != runId)
                        return "You have no Boss Rush prize to claim.";
                    if (stones > 0 && (client.Inventory == null || client.Inventory.Count + stones > 40))
                        return "Clear " + stones + " inventory spaces first. Your claim is waiting.";
                    if ((ulong)client.Entity.ConquerPoints + cp > uint.MaxValue
                        || (ulong)client.Entity.RevoraPoint + rp > uint.MaxValue
                        || gold > Entity.MaximumSilver - client.Entity.Money)
                        return "Spend some of your purse before claiming. Your claim is waiting.";
                    uint nextCp = client.Entity.ConquerPoints + cp;
                    uint nextRvp = client.Entity.RevoraPoint + rp;
                    ulong nextGold = client.Entity.Money + gold;
                    var rewards = new List<ConquerItem>();
                    for (byte n = 0; n < stones; n++)
                        rewards.Add(QuestInventory.New(stoneId, false));
                    byte[] merged = null;
                    using (var conn = DataHolder.MySqlConnection)
                    {
                        conn.Open();
                        EnsureRunLedger(conn);
                        EnsureProgress(conn);
                        using (var tx = conn.BeginTransaction())
                        {
                            long? savedCp, savedRvp;
                            PurseBalance.CopySaved(client, out savedCp, out savedRvp);
                            using (var q = QuestInventory.Command(conn, tx, "SELECT conquerpoints, revorapoint FROM entities WHERE uid=@uid FOR UPDATE", "uid", (long)client.Entity.UID))
                            using (var reader = q.ExecuteReader())
                            {
                                if (!reader.Read()) throw new InvalidOperationException("Boss Rush owner missing.");
                                long? dbCp = reader.IsDBNull(0) ? (long?)null : Convert.ToInt64(reader.GetValue(0));
                                long? dbRvp = reader.IsDBNull(1) ? (long?)null : Convert.ToInt64(reader.GetValue(1));
                                if (dbCp != savedCp || dbRvp != savedRvp)
                                    throw new InvalidOperationException("Boss Rush purse changed.");
                            }
                            SilverBalance.AssertSaved(conn, tx, client);
                            byte[] before;
                            using (var q = QuestInventory.Command(conn, tx, "SELECT data FROM variablevault WHERE entityid=@uid FOR UPDATE", "uid", (long)client.Entity.UID))
                                before = q.ExecuteScalar() as byte[];
                            if (before == null || before.Length < 4) throw new InvalidOperationException("Boss Rush claim is not saved.");
                            var savedVault = new VariableVault();
                            savedVault.Deserialize(before);
                            if ((uint)savedVault.ReadULong(ClaimKey) != kind
                                || savedVault.ReadULong(ClaimRunKey) != runId)
                                throw new InvalidOperationException("Boss Rush claim changed.");
                            // The immutable admission contract survives recovery and result replay.
                            if (kind == 1 && runId != 0)
                            {
                                using (var q = QuestInventory.Command(conn, tx,
                                    "SELECT soul_type FROM revora_boss_rush_progress WHERE entityid=@uid AND run_id=@run AND phase=@won FOR UPDATE",
                                    "uid", (long)client.Entity.UID, "run", RunDb(runId), "won", PhaseWon))
                                {
                                    var value = q.ExecuteScalar();
                                    uint soul = value == null ? 0u : checked((uint)Convert.ToInt64(value));
                                    if (soul != 0)
                                    {
                                        ConquerItemBaseInformation info;
                                        if (soul < 800000 || soul >= 900000 || !ConquerItemInformation.BaseInformations.TryGetValue(soul, out info)
                                            || info.PurificationLevel != 7)
                                            throw new InvalidOperationException("Boss Rush saved soul catalog unavailable.");
                                        rewards.Add(QuestInventory.New(soul, true));
                                    }
                                }
                            }
                            if (rewards.Count > 0)
                            {
                                if (client.Inventory == null || client.Inventory.Count + rewards.Count > 40)
                                    return "Clear " + rewards.Count + " inventory spaces first. Your claim is waiting.";
                                using (var q = QuestInventory.Command(conn, tx,
                                    "SELECT count(*) FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0", "uid", (long)client.Entity.UID))
                                    if (Convert.ToInt64(q.ExecuteScalar()) + rewards.Count > 40)
                                        return "Clear " + rewards.Count + " inventory spaces first. Your claim is waiting.";
                            }
                            ConsumeRun(conn, tx, client.Entity.UID, runId, kind);
                            uint pendingKind = (uint)savedVault.ReadULong(PendingKindKey);
                            ulong pendingRun = savedVault.ReadULong(PendingRunKey);
                            string[] names;
                            DynamicVariable[] forced;
                            if (pendingKind != 0)
                            {
                                names = new string[] { ClaimKey, ClaimRunKey, PendingKindKey, PendingRunKey };
                                forced = new DynamicVariable[] { pendingKind, pendingRun, (DynamicVariable)0ul, (DynamicVariable)0ul };
                            }
                            else
                            {
                                ulong nextRun;
                                uint nextKind;
                                if (TryNextIssued(conn, tx, client.Entity.UID, runId, out nextRun, out nextKind))
                                {
                                    names = new string[] { ClaimKey, ClaimRunKey, PendingKindKey, PendingRunKey };
                                    forced = new DynamicVariable[] { nextKind, nextRun, (DynamicVariable)0ul, (DynamicVariable)0ul };
                                }
                                else
                                {
                                    names = new string[] { ClaimKey, ClaimRunKey };
                                    forced = new DynamicVariable[] { (DynamicVariable)0ul, (DynamicVariable)0ul };
                                }
                            }
                            if (!client.Variables.TryMerge(before, names, forced, out merged))
                                throw new InvalidOperationException("Boss Rush claim changed.");
                            if (QuestInventory.Execute(conn, tx, @"UPDATE entities SET conquerpoints=@cp, revorapoint=@rvp WHERE uid=@uid AND (CASE WHEN @cpnull THEN conquerpoints IS NULL ELSE conquerpoints=@oldcp END) AND (CASE WHEN @rvpnull THEN revorapoint IS NULL ELSE revorapoint=@oldrvp END)",
                                "cp", (long)nextCp, "rvp", (long)nextRvp, "uid", (long)client.Entity.UID,
                                "cpnull", !savedCp.HasValue, "oldcp", savedCp.GetValueOrDefault(),
                                "rvpnull", !savedRvp.HasValue, "oldrvp", savedRvp.GetValueOrDefault()) != 1)
                                throw new InvalidOperationException("Boss Rush purse changed.");
                            SilverBalance.Persist(conn, tx, client, nextGold);
                            foreach (ConquerItem reward in rewards)
                                QuestInventory.Insert(conn, tx, client.Entity.UID, reward);
                            if (QuestInventory.Execute(conn, tx, "UPDATE variablevault SET data=@data WHERE entityid=@uid AND data=@before",
                                "data", merged, "uid", (long)client.Entity.UID, "before", before) != 1)
                                throw new InvalidOperationException("Boss Rush claim changed.");
                            if (client.Entity.PersistenceRequiresReload || (uint)client[ClaimKey] != kind
                                || client.Variables.ReadULong(ClaimRunKey) != runId)
                                throw new InvalidOperationException("Boss Rush session changed.");
                            tx.Commit();
                        }
                    }
                    Action<GameState> hook = AfterClaimCommit;
                    if (hook != null) hook(client);
                    client.Variables.ReplaceContents(merged);
                    client.Entity.ApplyCommittedConquerPoints(nextCp);
                    client.Entity.ApplyCommittedRevoraPoints(nextRvp);
                    SilverBalance.ApplyCommitted(client, nextGold);
                    if (rewards.Count > 0)
                        QuestInventory.Install(client, new uint[0], rewards);
                    string extra = stones > 0 ? ", and " + stones + " Perfection Stone" + (stones > 1 ? "s" : "") : "";
                    if (rewards.Count > stones) extra += ", and one bound P7 soul";
                    return "Claimed " + label + ": " + cp + " CPs, " + gold + " gold, " + rp + " Revora Points" + extra + ".";
                }
                catch (Exception ex)
                {
                    QuestInventory.Fail(client, "boss-rush-claim", ex);
                    return "Your Boss Rush claim could not be confirmed. Please reconnect to check your prize and balances.";
                }
            }
        }

        public static void RecoverPending(GameState client)
        {
            if (client == null || client.Entity == null || client.ItemSyncRoot == null || client.Variables == null)
                return;
            // Same direction as checkpoints/claim issuance: session, player,
            // vault, database. Replacement cannot publish between the durable
            // decision and application to the old session's live vault.
            uint uid = client.Entity.UID;
            if (uid == 0 || client.Fake) return;
            if (System.Threading.Monitor.IsEntered(Gate))
            {
                ScheduleRecovery(client);
                return;
            }
            SessionInstall.Enter(uid);
            try
            {
                ConfirmLiveWaves(client);
                var retired = new List<Instance>();
                bool committed = false;
                try {
                    RecoverPendingCore(client, retired, out committed);
                    if (committed && !ReturnClosedRuns(client)) ScheduleRecovery(client);
                }
                finally
                {
                    // Player/vault and database locks have all been released.
                    // Gate protects metadata only, without inverted waits.
                    if (committed && retired.Count != 0)
                        foreach (Instance inst in retired)
                        {
                            inst.Failed = true;
                            RemoveMetadata(inst);
                        }
                }
            }
            finally { SessionInstall.Exit(uid); }
        }

        private static void RecoverPendingCore(GameState client, List<Instance> retired, out bool committed)
        {
            committed = false;
            lock (client.ItemSyncRoot)
            lock (client.Variables.SyncRoot)
            {
                GameState current;
                if (client.Fake || Kernel.GamePool == null
                    || !Kernel.GamePool.TryGetValue(client.Entity.UID, out current)
                    || !object.ReferenceEquals(current, client))
                    return;
                try
                {
                    using (var conn = DataHolder.MySqlConnection)
                    {
                        conn.Open();
                        EnsureRunLedger(conn);
                        EnsureProgress(conn);
                        using (var tx = conn.BeginTransaction())
                        {
                            // All projection transactions lock the vault before ledger
                            // rows. Recovery must not invert TryClaim's lock order.
                            byte[] before;
                            using (var q = QuestInventory.Command(conn, tx, "SELECT data FROM variablevault WHERE entityid=@uid FOR UPDATE", "uid", (long)client.Entity.UID))
                                before = q.ExecuteScalar() as byte[];
                            uint claim = 0;
                            uint pendingKind = 0;
                            ulong pendingRun = 0;
                            if (before != null && before.Length >= 4)
                            {
                                var saved = new VariableVault();
                                saved.Deserialize(before);
                                claim = (uint)saved.ReadULong(ClaimKey);
                                pendingKind = (uint)saved.ReadULong(PendingKindKey);
                                pendingRun = saved.ReadULong(PendingRunKey);
                            }
                            // Queue every reward-eligible abandoned predecessor
                            // independently of the single projected prize.
                            QueueAbandoned(conn, tx, client, retired);
                            if (claim != 0)
                            {
                                tx.Commit();
                                committed = true;
                                return;
                            }
                            ulong ledgerRun;
                            uint ledgerKind;
                            bool haveLedger = TryNextIssued(conn, tx, client.Entity.UID, 0, out ledgerRun, out ledgerKind);
                            uint kind;
                            ulong run;
                            if (pendingKind != 0)
                            {
                                kind = pendingKind;
                                run = pendingRun;
                            }
                            else if (haveLedger)
                            {
                                kind = ledgerKind;
                                run = ledgerRun;
                            }
                            else
                            {
                                // Commit any queue work even without a projection.
                                tx.Commit();
                                committed = true;
                                return;
                            }
                            byte[] merged;
                            if (!client.Variables.TryMerge(before,
                                new string[] { ClaimKey, ClaimRunKey, PendingKindKey, PendingRunKey },
                                new DynamicVariable[] { kind, run, (DynamicVariable)0ul, (DynamicVariable)0ul },
                                out merged))
                            {
                                ScheduleRecovery(client);
                                return;
                            }
                            int rows = before == null
                                ? QuestInventory.Execute(conn, tx, "INSERT INTO variablevault(entityid,data) VALUES(@uid,@data)", "uid", (long)client.Entity.UID, "data", merged)
                                : QuestInventory.Execute(conn, tx, "UPDATE variablevault SET data=@data WHERE entityid=@uid AND data=@before", "data", merged, "uid", (long)client.Entity.UID, "before", before);
                            if (rows != 1)
                                throw new InvalidOperationException("Boss Rush pending prize changed.");
                            tx.Commit();
                            committed = true;
                            client.Variables.ReplaceContents(merged);
                        }
                    }
                }
                catch (Exception ex)
                {
                    ScheduleRecovery(client);
                    Console.WriteLine("[boss-rush] pending recovery failed uid=" + client.Entity.UID + " type=" + ex.GetType().Name);
                }
            }
        }

        private static void QueueAbandoned(Npgsql.NpgsqlConnection conn, Npgsql.NpgsqlTransaction tx, GameState client, List<Instance> retired)
        {
            // A previous COMMIT may have succeeded without acknowledgement.
            // Revisit tracked terminal rows even when their ledger exists, so
            // that retry can finish metadata retirement without a second prize.
            var tracked = new List<long>();
            if (!System.Threading.Monitor.TryEnter(Gate))
            {
                ScheduleRecovery(client);
                return;
            }
            try
            {
                foreach (Instance inst in Live)
                    if (inst.Player != null && inst.Player.Entity != null
                        && inst.Player.Entity.UID == client.Entity.UID)
                        tracked.Add(RunDb(inst.RunId));
            }
            finally { System.Threading.Monitor.Exit(Gate); }
            var runs = new List<ulong>();
            var bosses = new List<int>();
            var phases = new List<int>();
            var spawned = new List<bool>();
            using (var q = new Npgsql.NpgsqlCommand(@"SELECT p.run_id, p.boss, p.phase, p.spawned FROM revora_boss_rush_progress p
WHERE p.entityid=@uid AND (p.phase=@won OR p.phase=@failed OR p.phase=@active OR p.phase=@admitting)
AND (p.run_id=ANY(@tracked) OR NOT EXISTS (SELECT 1 FROM revora_boss_rush_runs r WHERE r.entityid=p.entityid AND r.run_id=p.run_id))
ORDER BY p.run_id FOR UPDATE OF p", conn, tx))
            {
                q.Parameters.AddWithValue("uid", (long)client.Entity.UID);
                q.Parameters.AddWithValue("tracked", tracked.ToArray());
                q.Parameters.AddWithValue("failed", PhaseFailed);
                q.Parameters.AddWithValue("won", PhaseWon);
                q.Parameters.AddWithValue("active", PhaseActive);
                q.Parameters.AddWithValue("admitting", PhaseAdmitting);
                using (var reader = q.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        runs.Add((ulong)reader.GetInt64(0));
                        bosses.Add(reader.GetInt32(1));
                        phases.Add(reader.GetInt32(2));
                        spawned.Add(reader.GetBoolean(3));
                    }
                }
            }
            for (int i = 0; i < runs.Count; i++)
            {
                bool deferred;
                var retiring = new List<Instance>();
                bool owned = RunStillOwned(client, runs[i], out deferred, retiring);
                if (deferred)
                {
                    ScheduleRecovery(client);
                    continue;
                }
                // In-flight admission is process-local. A cold admitting row
                // has no mark and is terminalized below with no prize.
                if (phases[i] == PhaseAdmitting && AdmissionOpen(runs[i]))
                    continue;
                if (phases[i] == PhaseActive && owned)
                    continue;
                // The map still has this wave and its confirmation has not
                // committed. Settling now would drop a prize or invent one.
                if ((phases[i] == PhaseActive || phases[i] == PhaseAdmitting)
                    && UnconfirmedMaterialized(runs[i]))
                {
                    ScheduleRecovery(client);
                    continue;
                }
                // spawned false means the stored boss was not inserted. The
                // previous boss is the last confirmed progress. Won phase is
                // already a completed outcome.
                int eligible = EligibleBoss(bosses[i], spawned[i]);
                bool reward = phases[i] == PhaseWon || eligible >= 3;
                if ((phases[i] == PhaseActive || phases[i] == PhaseAdmitting)
                    && QuestInventory.Execute(conn, tx, @"UPDATE revora_boss_rush_progress SET phase=@failed
WHERE entityid=@uid AND run_id=@run AND (phase=@active OR phase=@admitting)",
                        "failed", PhaseFailed, "uid", (long)client.Entity.UID,
                        "run", RunDb(runs[i]), "active", PhaseActive, "admitting", PhaseAdmitting) != 1)
                {
                    ScheduleRecovery(client);
                    continue;
                }
                retired.AddRange(retiring);
                if (!reward) continue;
                uint rebuilt = phases[i] == PhaseWon ? 1u : 2u;
                QuestInventory.Execute(conn, tx, @"INSERT INTO revora_boss_rush_runs(entityid,run_id,kind,consumed)
VALUES(@uid,@run,@kind,false) ON CONFLICT (entityid, run_id) DO NOTHING",
                    "uid", (long)client.Entity.UID, "run", RunDb(runs[i]), "kind", (int)rebuilt);
            }
        }

        private static bool HasClaim(GameState client)
        {
            if (client.Variables == null) return false;
            return (uint)client[ClaimKey] != 0;
        }

        private static Instance FindByMap(ushort mapId)
        {
            lock (Gate)
            {
                Instance inst;
                if (ByMap.TryGetValue(mapId, out inst))
                    return inst;
            }
            return null;
        }

        private static Instance FindByPlayer(uint uid)
        {
            lock (Gate) return FindHeld(uid);
        }

        private static Instance FindHeld(uint uid)
        {
            for (int i = 0; i < Live.Count; i++)
            {
                Instance inst = Live[i];
                if (inst.Player != null && inst.Player.Entity != null && inst.Player.Entity.UID == uid)
                    return inst;
            }
            return null;
        }

        private static void RemoveHeld(Instance inst)
        {
            if (inst == null) return;
            QueueClosure(inst);
            Live.Remove(inst);
            Instance mapped;
            if (inst.Arena != null && ByMap.TryGetValue(inst.Arena.ID, out mapped)
                && object.ReferenceEquals(mapped, inst))
                ByMap.Remove(inst.Arena.ID);
        }

        private static void RemoveMetadata(Instance inst)
        {
            if (inst == null) return;
            if (!System.Threading.Monitor.TryEnter(Gate))
            {
                lock (PendingRemoval) PendingRemoval.Add(inst);
                return;
            }
            try { RemoveHeld(inst); }
            finally { System.Threading.Monitor.Exit(Gate); }
        }

        private static void QueueClosure(Instance inst)
        {
            if (inst == null || !inst.OwnedArena || inst.Arena == null) return;
            lock (Closing) Closing.Add(inst);
        }

        private static bool ReturnClosedRuns(GameState client)
        {
            if (!QuestInventory.Player(client, false)) return false;
            if (!System.Threading.Monitor.TryEnter(client.ItemSyncRoot)) return false;
            bool virtue = false, pk = false;
            try
            {
                virtue = System.Threading.Monitor.TryEnter(client.VirtueSyncRoot);
                if (!virtue) return false;
                pk = System.Threading.Monitor.TryEnter(client.PkSyncRoot);
                if (!pk) return false;
                long run;
                using (var c = DataHolder.MySqlConnection)
                {
                    c.Open();
                    using (var q = QuestInventory.Command(c, null,
                        "SELECT run_id FROM revora_boss_rush_progress WHERE entityid=@uid AND NOT returned AND phase IN(@won,@failed) ORDER BY run_id LIMIT 1",
                        "uid", (long)client.Entity.UID, "won", PhaseWon, "failed", PhaseFailed))
                    {
                        var value = q.ExecuteScalar(); if (value == null) return true;
                        run = Convert.ToInt64(value);
                    }
                    if (HoldReturn) return false;
                    client.Entity.Teleport(TwinCityMap, ClaimX, ClaimY);
                    if (!SessionOwns(client) || client.Entity.MapID != TwinCityMap || client.Entity.X != ClaimX || client.Entity.Y != ClaimY) return false;
                    if (!EntityTable.SaveEntity(client)) return false;
                    if (QuestInventory.Execute(c, null,
                        "UPDATE revora_boss_rush_progress SET returned=true WHERE entityid=@uid AND run_id=@run AND NOT returned AND phase IN(@won,@failed)",
                        "uid", (long)client.Entity.UID, "run", run, "won", PhaseWon, "failed", PhaseFailed) != 1)
                        throw new InvalidOperationException("Boss Rush return acknowledgement failed.");
                    return true;
                }
            }
            catch (Exception ex) { QuestInventory.Fail(client, "boss-rush-return", ex); return false; }
            finally {
                if (pk) System.Threading.Monitor.Exit(client.PkSyncRoot);
                if (virtue) System.Threading.Monitor.Exit(client.VirtueSyncRoot);
                System.Threading.Monitor.Exit(client.ItemSyncRoot);
            }
        }

        private static void DrainClosures()
        {
            Instance[] pending;
            lock (Closing) pending = Closing.Take(MaxSolos).ToArray();
            foreach (var inst in pending)
            {
                uint uid = inst.Player.Entity.UID;
                if (!SessionInstall.TryEnter(uid)) continue;
                try
                {
                    GameState current;
                    if (Kernel.GamePool.TryGetValue(uid, out current))
                    {
                        RecoverPending(current);
                        if (current.Entity.MapID == inst.Arena.ID) continue;
                    }
                    if (Kernel.GamePool.Values.Any(p => p != null && p.Entity != null && p.Entity.MapID == inst.Arena.ID)) continue;
                    Map map;
                    if (Kernel.Maps.TryGetValue(inst.Arena.ID, out map) && ReferenceEquals(map, inst.Arena))
                    {
                        inst.Arena.Entities.Clear(); inst.Arena.FloorItems.Clear();
                        Constants.PKFreeMaps.Remove(inst.Arena.ID); inst.Arena.Dispose();
                    }
                    lock (Closing) Closing.Remove(inst);
                }
                finally { SessionInstall.Exit(uid); }
            }
        }

        private static void DrainLeaves()
        {
            GameState[] batch;
            lock (PendingLeave)
            {
                if (PendingLeave.Count == 0) return;
                batch = PendingLeave.ToArray();
                PendingLeave.Clear();
            }
            for (int i = 0; i < batch.Length; i++)
            {
                GameState client = batch[i];
                if (client == null || client.Entity == null) continue;
                if (!System.Threading.Monitor.TryEnter(Gate))
                {
                    lock (PendingLeave)
                        for (int remaining = i; remaining < batch.Length; remaining++)
                            PendingLeave.Add(batch[remaining]);
                    return;
                }
                Instance inst;
                try { inst = FindHeld(client.Entity.UID); }
                finally { System.Threading.Monitor.Exit(Gate); }
                if (inst != null && object.ReferenceEquals(inst.Player, client) && !inst.Won && !inst.Failed)
                    Fail(inst, "You left the field.");
            }
        }

        private static void DrainRemovals()
        {
            if (!System.Threading.Monitor.TryEnter(Gate)) return;
            try
            {
                Instance[] batch;
                lock (PendingRemoval)
                {
                    if (PendingRemoval.Count == 0) return;
                    batch = PendingRemoval.ToArray();
                    PendingRemoval.Clear();
                }
                for (int i = 0; i < batch.Length; i++)
                    RemoveHeld(batch[i]);
            }
            finally { System.Threading.Monitor.Exit(Gate); }
        }
    }

    // Per-character session publication. A checkpoint holds the lease across
    // its database transaction, including commit. Login holds the same lease,
    // runs the disconnect callback, then publishes. That callback does not
    // wait on the Boss Rush registry gate. Gate only reserves capacity and
    // publishes or removes fight metadata. A checkpoint takes lease, then
    // database. Entry and revival keep the lease through teleport guards.
    // Death and Tick keep it through decisions and live application. Finish
    // takes lease before the player owner lock. Recovery takes lease, confirms
    // a monster already on the map, then player, vault, and database, and only
    // TryEnter on the registry gate. An unconfirmed wave is not a prize.
    // Login holds the lease across each early and late phase and checks the
    // generation captured on that session. Fake actors use TryInstallFake and
    // cannot remove a real session.
    internal static class SessionInstall
    {
        private static readonly Dictionary<uint, object> Leases = new Dictionary<uint, object>();
        private static readonly Dictionary<uint, long> Epochs = new Dictionary<uint, long>();
        private static readonly Dictionary<uint, long> LoginEpoch = new Dictionary<uint, long>();
        private static readonly object Catalog = new object();

        internal static void Enter(uint uid)
        {
            System.Threading.Monitor.Enter(Lease(uid));
        }

        internal static void Exit(uint uid)
        {
            System.Threading.Monitor.Exit(Lease(uid));
        }

        internal static bool TryEnter(uint uid)
        {
            if (uid == 0) return false;
            return System.Threading.Monitor.TryEnter(Lease(uid));
        }

        internal static bool Holds(uint uid)
        {
            return uid != 0 && System.Threading.Monitor.IsEntered(Lease(uid));
        }

        // Callers that already hold the character lock cannot wait for this lease.
        internal static void Defer(Action work)
        {
            if (work == null) return;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                try { work(); }
                catch (Exception ex)
                {
                    Console.WriteLine("[session] deferred shutdown type=" + ex.GetType().Name);
                }
            });
        }

        internal static long Epoch(uint uid)
        {
            lock (Catalog)
            {
                long epoch;
                Epochs.TryGetValue(uid, out epoch);
                return epoch;
            }
        }

        internal static void Install(GameState client)
        {
            if (client == null || client.Fake || client.Entity == null) return;
            Replace(client.Entity.UID, client, null);
        }

        internal static void Replace(uint uid, GameState client, Action disconnectPrevious)
        {
            Publish(uid, client, disconnectPrevious, true);
        }

        // True only when this object is still the session published for login.
        internal static Action<GameState> AfterEarlyLogin = null;
        internal static Action<GameState> BeforeLoginRelease = null;
        internal static Action<GameState> BeforeLateLogin = null;
        internal static Func<GameState, bool> LoadEntityHook = null;

        // True only when this object is still the session published for its own
        // captured generation. The per-UID login epoch is not read here: a
        // successor overwrites that value.
        internal static bool CompleteLogin(uint uid, GameState client)
        {
            if (!EnterCurrent(uid, client)) return false;
            Exit(uid);
            return true;
        }

        // Enters the session lease and leaves it held when this object is current.
        internal static bool EnterCurrent(uint uid, GameState client)
        {
            if (client == null || client.Fake || uid == 0 || client.SessionGeneration == 0) return false;
            Enter(uid);
            if (!IsCurrent(uid, client))
            {
                Exit(uid);
                return false;
            }
            return true;
        }

        internal static bool IsCurrent(uint uid, GameState client)
        {
            if (client == null || client.Fake || uid == 0 || Kernel.GamePool == null || client.SessionGeneration == 0
                || client.DisconnectStarted || client.ShutdownStarted)
                return false;
            GameState current;
            if (!Kernel.GamePool.TryGetValue(uid, out current) || !object.ReferenceEquals(current, client))
                return false;
            return Epoch(uid) == client.SessionGeneration;
        }

        // Called with the session lease held. An obsolete actor may dispose its
        // own transport/timers, but must not write shared character state.
        internal static bool IsSuperseded(GameState client)
        {
            uint uid = SessionUid(client);
            if (uid == 0 || Kernel.GamePool == null) return false;
            GameState current;
            if (Kernel.GamePool.TryGetValue(uid, out current) && !object.ReferenceEquals(current, client))
                return true;
            return client.SessionGeneration != 0 && Epoch(uid) != client.SessionGeneration;
        }

        // A real login replaces a fake without running that fake's player shutdown.
        // Timer and thread cleanup touch only the fake object, never the successor.
        internal static void TakeoverPrevious(GameState previous, GameState successor)
        {
            if (previous == null || object.ReferenceEquals(previous, successor)) return;
            if (previous.Fake)
            {
                previous.ReleaseSyntheticPresence();
                return;
            }
            previous.Disconnect();
        }

        // Publish the creator before its character row and account row are saved.
        // A failed or replaced attempt removes only this object.
        internal static bool TryOwnCreation(uint uid, GameState client, Func<bool> persist)
        {
            if (client == null || client.Fake || uid == 0 || persist == null || Kernel.GamePool == null) return false;
            Enter(uid);
            try
            {
                if (client.DisconnectStarted || client.ShutdownStarted
                    || (client.SessionGeneration != 0 && !IsCurrent(uid, client)))
                    return false;
                GameState current;
                bool present = Kernel.GamePool.TryGetValue(uid, out current);
                if (present && current != null && !object.ReferenceEquals(current, client))
                    return false;
                if (!present || !object.ReferenceEquals(current, client)
                    || client.SessionGeneration == 0 || Epoch(uid) != client.SessionGeneration)
                {
                    Kernel.GamePool[uid] = client;
                    Remember(uid, client);
                }
                bool saved;
                try { saved = persist(); }
                catch
                {
                    DropHolding(uid, client);
                    throw;
                }
                if (!saved || !IsCurrent(uid, client))
                {
                    DropHolding(uid, client);
                    return false;
                }
                return true;
            }
            finally { Exit(uid); }
        }

        // Publish a new character without disconnecting a different owner.
        internal static bool TryClaim(uint uid, GameState client)
        {
            if (client == null || client.Fake || uid == 0 || Kernel.GamePool == null) return false;
            Enter(uid);
            try
            {
                GameState current;
                bool present = Kernel.GamePool.TryGetValue(uid, out current);
                if (present && current != null && !object.ReferenceEquals(current, client))
                    return false;
                if (present && object.ReferenceEquals(current, client))
                    return client.SessionGeneration != 0 && Epoch(uid) == client.SessionGeneration;
                Kernel.GamePool[uid] = client;
                Remember(uid, client);
                return true;
            }
            finally { Exit(uid); }
        }

        internal static bool TryInstallFake(uint uid, GameState fake)
        {
            if (fake == null || !fake.Fake || uid == 0 || Kernel.GamePool == null) return false;
            Enter(uid);
            try
            {
                GameState current;
                if (Kernel.GamePool.TryGetValue(uid, out current) && current != null)
                    return object.ReferenceEquals(current, fake);
                Kernel.GamePool[uid] = fake;
                Bump(uid);
                return true;
            }
            finally { Exit(uid); }
        }

        internal static bool TryRemoveFake(uint uid, GameState fake)
        {
            if (fake == null || !fake.Fake || uid == 0 || Kernel.GamePool == null) return false;
            Enter(uid);
            try
            {
                GameState current;
                if (!Kernel.GamePool.TryGetValue(uid, out current) || !object.ReferenceEquals(current, fake) || !current.Fake)
                    return false;
                GameState removed;
                Kernel.GamePool.TryRemove(uid, out removed);
                Bump(uid);
                return true;
            }
            finally { Exit(uid); }
        }

        internal static void DropVacant(uint uid, GameState seen)
        {
            if (Kernel.GamePool == null) return;
            Enter(uid);
            try
            {
                GameState current;
                if (!Kernel.GamePool.TryGetValue(uid, out current) || !object.ReferenceEquals(current, seen))
                    return;
                if (current != null && current.Entity != null) return;
                GameState removed;
                Kernel.GamePool.TryRemove(uid, out removed);
                Bump(uid);
            }
            finally { Exit(uid); }
        }

        private static void Publish(uint uid, GameState client, Action disconnectPrevious, bool takeover)
        {
            if (client == null || client.Fake) return;
            Enter(uid);
            try
            {
                if (client.DisconnectStarted || client.ShutdownStarted) return;
                if (disconnectPrevious != null)
                {
                    try { disconnectPrevious(); }
                    catch (Exception ex)
                    {
                        Console.WriteLine("[session] disconnect callback failed uid=" + uid + " type=" + ex.GetType().Name);
                    }
                }
                if (Kernel.GamePool == null) return;
                GameState current;
                bool present = Kernel.GamePool.TryGetValue(uid, out current);
                if (present && current != null && !object.ReferenceEquals(current, client)
                    && current.ShutdownDeferred)
                    return;
                if (present && object.ReferenceEquals(current, client))
                {
                    if (client.SessionGeneration == 0 || Epoch(uid) != client.SessionGeneration)
                        Remember(uid, client);
                    return;
                }
                if (!takeover) return;
                Kernel.GamePool[uid] = client;
                Remember(uid, client);
            }
            finally { Exit(uid); }
        }

        internal static void Retire(GameState client)
        {
            if (client == null || client.Fake || Kernel.GamePool == null) return;
            uint uid = SessionUid(client);
            if (uid == 0) return;
            Enter(uid);
            try { DropHolding(uid, client); }
            finally { Exit(uid); }
        }

        internal static uint SessionUid(GameState client)
        {
            if (client == null) return 0;
            // Initial login publication uses the account id while Entity is
            // still a non-null placeholder with UID zero.
            if (client.Entity != null && client.Entity.UID != 0) return client.Entity.UID;
            return client.Account != null ? client.Account.EntityID : 0;
        }

        internal static void DropRegistration(GameState client, bool releaseDisconnect)
        {
            if (client == null || Kernel.GamePool == null) return;
            uint uid = SessionUid(client);
            if (uid == 0) return;
            DropHolding(uid, client);
            if (!releaseDisconnect || Kernel.DisconnectPool == null) return;
            var entry = new System.Collections.Generic.KeyValuePair<uint, GameState>(uid, client);
            ((System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<uint, GameState>>)Kernel.DisconnectPool).Remove(entry);
        }

        static void DropHolding(uint uid, GameState client)
        {
            if (Kernel.GamePool == null || client == null) return;
            GameState current;
            if (!Kernel.GamePool.TryGetValue(uid, out current) || !object.ReferenceEquals(current, client))
                return;
            GameState removed;
            Kernel.GamePool.TryRemove(uid, out removed);
            if (removed != null && !object.ReferenceEquals(removed, client))
                Kernel.GamePool[uid] = removed;
            else
                Bump(uid);
        }

        private static object Lease(uint uid)
        {
            lock (Catalog)
            {
                object lease;
                if (!Leases.TryGetValue(uid, out lease))
                {
                    lease = new object();
                    Leases[uid] = lease;
                }
                return lease;
            }
        }

        private static void Bump(uint uid)
        {
            lock (Catalog)
            {
                long epoch;
                Epochs.TryGetValue(uid, out epoch);
                Epochs[uid] = epoch + 1;
            }
        }

        private static void Remember(uint uid, GameState client)
        {
            Bump(uid);
            long epoch = Epoch(uid);
            client.SessionGeneration = epoch;
            lock (Catalog) LoginEpoch[uid] = epoch;
        }
    }
}
