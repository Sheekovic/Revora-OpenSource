using System;
using System.Collections.Generic;
using System.Drawing;
using Revora.Client;
using Revora.Database;
using Revora.Interfaces;
using Revora.Network.GamePackets;

namespace Revora.Game.Features
{
    public static class SteedRaceSystem
    {
        public const uint NpcId = 900000012;
        public const string NpcName = "SteedRace";
        public const int Capacity = 24;
        public const int MinPlayers = 2;
        public const string ClaimKey = "steed-claim";
        public const ushort TwinCityMap = 1002;
        public const ushort TwinCityX = 408;
        public const ushort TwinCityY = 330;
        public const ushort ClaimX = 370;
        public const ushort ClaimY = 311;
        // Twin City Zebra look. Distinct from BeastTrial MountTrainer (6327) and WaveWarden (5150).
        public const ushort Mesh = 50820;
        public const ushort RideSpell = 7001;

        private static readonly int[] StartHours = new int[] { 13, 21 };
        private static readonly TimeSpan RegisterWindow = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan RaceLimit = TimeSpan.FromMinutes(10);
        private static readonly object Gate = new object();

        private static DateTime CycleStart;
        private static DateTime RaceUntil;
        private static Time32 PeaceUntil;
        private static bool Running;
        private static bool RegisterOpen;
        private static bool Invited;
        private static bool GateOpened;
        private static readonly Dictionary<uint,GameState> Registered = new Dictionary<uint,GameState>();

        private static readonly ushort[] MountSkills = { 7001, 7002, 7003, 11190 };
        private static readonly ulong[] MountPrices = { 50000, 100000, 100000, 500000 };
        private static readonly string[] MountNames = { "Riding", "Spook", "War Cry", "Vortex" };

        private static void InitializeMountLessons()
        {
            using (var connection = DataHolder.MySqlConnection)
            {
                connection.Open();
                QuestInventory.Execute(connection, null, @"CREATE TABLE IF NOT EXISTS revora_mount_lessons(
id uuid PRIMARY KEY,uid bigint NOT NULL REFERENCES entities(uid),skill integer NOT NULL CHECK(skill IN(7001,7002,7003,11190)),
silver bigint NOT NULL CHECK(silver>0),learned_at timestamptz NOT NULL DEFAULT now());");
            }
        }
        private static bool MountStudent(GameState player)
        {
            return QuestInventory.Near(player, 9881, 1002) && player.Spells != null && player.Entity.PokerTable == 0;
        }
        private static string BuyMountLesson(GameState player, int index)
        {
            ushort id = MountSkills[index]; ulong price = MountPrices[index];
            if (player.Spells.ContainsKey(id)) return "You already know " + MountNames[index] + ". Keep your silver.";
            if (player.Entity.Money < price) return "Bring " + price.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + " silver for this lesson.";
            if (!SpellTable.SpellInformations.ContainsKey(id) || !SpellTable.SpellInformations[id].ContainsKey(0))
                throw new InvalidOperationException("Mount lesson catalog missing.");
            var skill = new Spell(true) { ID = id, Level = 0 };
            ulong balance = player.Entity.Money - price;
            using (var connection = DataHolder.MySqlConnection)
            {
                connection.Open(); using (var transaction = connection.BeginTransaction())
                {
                    SkillTable.LockOwner(connection, transaction, player);
                    SilverBalance.Persist(connection, transaction, player, balance);
                    SkillTable.Persist(connection, transaction, player, skill);
                    if (QuestInventory.Execute(connection, transaction, @"INSERT INTO revora_mount_lessons(id,uid,skill,silver)
VALUES(@id,@uid,@skill,@silver)", "id", Guid.NewGuid(), "uid", (long)player.Entity.UID,
                        "skill", (int)id, "silver", checked((long)price)) != 1)
                        throw new InvalidOperationException("Mount lesson receipt was not saved.");
                    if (!MountStudent(player) || player.Spells.ContainsKey(id) || player.Entity.Money != balance + price)
                        throw new InvalidOperationException("Mount student changed.");
                    transaction.Commit();
                }
            }
            SilverBalance.ApplyCommitted(player, balance);
            skill.Available = true; player.Spells.Add(id, skill); skill.Send(player);
            return "You have learned " + MountNames[index] + ". Ride well.";
        }
        private static bool HandleMountTrainer(GameState player, NpcRequest request)
        {
            if (player == null || player.Entity == null || player.ActiveNpc != 9881) return false;
            if (request == null || (request.NpcID != 0 && request.NpcID != 9881) || player.ItemSyncRoot == null) return true;
            lock (player.ItemSyncRoot) try
            {
                if (!MountStudent(player)) return true;
                var dialog = new Npcs(player);
                int option = request.OptionID;
                if (option >= 5 && option <= 8) dialog.Text(BuyMountLesson(player, option - 5));
                else if (option >= 1 && option <= 4)
                {
                    int index = option - 1;
                    dialog.Text(MountNames[index] + " costs " + MountPrices[index].ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + " silver. Shall we begin?");
                    dialog.Option("Teach me.", (byte)(index + 5));
                }
                else if (option == 0)
                {
                    dialog.Text("A steady hand makes a fine rider. Which lesson do you seek?");
                    for (int index = 0; index < MountSkills.Length; index++)
                        dialog.Option(MountNames[index] + " (" + MountPrices[index].ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + " silver)", (byte)(index + 1));
                }
                else return true;
                dialog.Option("Farewell.", 255); dialog.Send();
            }
            catch (Exception error) { QuestInventory.Fail(player, "mount-lesson", error); }
            return true;
        }

        public static void Create()
        {
            SteedRaceClaims.Initialize();
            InitializeMountLessons();
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
                DateTime slot = today.AddHours(StartHours[i]).AddMinutes(15);
                if (now < slot)
                    return slot;
            }
            return today.AddDays(1).AddHours(StartHours[0]).AddMinutes(15);
        }

        public static bool InEvent(Entity entity)
        {
            if (entity == null) return false;
            return SteedRace.isOn && entity.MapID == SteedRace.MAPID;
        }

        public static void Tick()
        {
            if (CycleStart == default(DateTime)) return;
            DateTime now = DateTime.Now;
            if (!Running && !Invited && now >= CycleStart.Subtract(RegisterWindow) && now < CycleStart)
            {
                Invited = true;
                RegisterOpen = true;
                Kernel.SendWorldMessage(new Message("Steed Race registration is open for 5 minutes. Equip a steed and talk to SteedRace in Twin City (" + ClaimX + ", " + ClaimY + "). Max 24. Top three claim.", Color.Yellow, Message.Center), Server.GamePool);
                InviteAll();
            }
            if (!Running && now >= CycleStart)
                StartRace();
            if (!Running) return;
            if (!GateOpened && Time32.Now > PeaceUntil)
            {
                if (Server.Thread != null && Server.Thread.SteedRace != null)
                {
                    Server.Thread.SteedRace.OpenGate();
                    GateOpened = SteedRace.GateOpen;
                    if (GateOpened) Server.Thread.SteedRace.SignalStart();
                }
            }
            if (now >= RaceUntil)
                FinishEvent();
        }

        public static bool HandleNpc(GameState client, NpcRequest request)
        {
            if (HandleMountTrainer(client, request)) return true;
            if (client == null || request == null || client.Entity == null || client.ActiveNpc != NpcId)
                return false;
            Npcs dialog = new Npcs(client);
            switch (request.OptionID)
            {
                case 0:
                    dialog.Text(StatusText());
                    if (RegisterOpen && !Running)
                        dialog.Option("Register", 1);
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

        public static void OnFinish(GameState client, int rank)
        {
            if (client == null || client.Entity == null) return;
            if (rank < 1 || rank > 3 || client.Variables == null || client.Variables.ReadULong(ClaimKey) != (ulong)rank) return;
            client.Entity.Teleport(TwinCityMap, ClaimX, ClaimY);
            string[] place = new string[] { "first", "second", "third" };
            string label = rank >= 1 && rank <= 3 ? place[rank - 1] : "placed";
            client.Send(new Message("You finished " + label + " in Steed Race. Claim at SteedRace.", Color.Yellow, Message.TopLeft));
            Kernel.SendWorldMessage(new Message(client.Entity.Name + " finished " + label + " in Steed Race.", Color.Red, Message.Center), Server.GamePool);
        }

        public static void OnDeath(GameState client)
        {
            if (client == null || client.Entity == null) return;
            if (!InEvent(client.Entity)) return;
            if (Server.Thread != null && Server.Thread.SteedRace != null)
                Server.Thread.SteedRace.Exit(client);
        }

        public static void OnDisconnect(GameState client)
        {
            if(client!=null && client.Entity!=null)lock(Gate) {
                GameState owner;
                if(Registered.TryGetValue(client.Entity.UID,out owner) && ReferenceEquals(owner,client))Registered.Remove(client.Entity.UID);
            }
            OnDeath(client);
        }

        private static string StatusText()
        {
            if (Running)
                return "Steed Race is live. Ride to the finish. Top three claim here.";
            if (RegisterOpen)
                return "Registration is open until " + CycleStart.ToString("HH:mm") + ". Seats " + Registered.Count + "/" + Capacity + ". Equip a steed and know Riding. Top three claim CPs, gold, Revora Points, and a bound P7 soul.";
            return "Next Steed Race " + CycleStart.ToString("HH:mm") + ". Twice a day, 13:15 and 21:15. Register 5 minutes before. Equip a steed. Top three claim.";
        }

        private static void InviteAll()
        {
            foreach (GameState client in Kernel.GamePool.Values)
            {
                if (!CanInvite(client)) continue;
                GameState captured = client;
                captured.MessageOK = p => { TryRegister(p); };
                captured.Send(new NpcReply(NpcReply.MessageBox, "Steed Race is open. Equip a steed. Top three claim at SteedRace (" + ClaimX + ", " + ClaimY + "). Register now?"));
                captured.Send(new Data(true) { UID = captured.Entity.UID, dwParam = 60, ID = Data.CountDown });
            }
        }

        private static bool CanInvite(GameState client)
        {
            if (!QuestInventory.Player(client) || client.Variables==null || client.Spells==null || client.Equipment==null
                || client.Booth!=null || client.Trade==null || client.Trade.InTrade) return false;
            ushort map = client.Entity.MapID;
            if (map == 6000 || map == 6001 || map == 6002 || map == 6003 || map == 6004 || map == 3846)
                return false;
            if (client.InQualifier() || client.Entity.PokerTable > 0 || client.PlayRouletteUID > 0)
                return false;
            return true;
        }

        private static string TryRegister(GameState client)
        {
            if (!CanInvite(client)) return "You cannot register right now.";
            if (client.Entity.Dead) return "You cannot register while dead.";
            if (HasClaim(client)) return "Claim your previous prize before entering another race.";
            if (!client.Spells.ContainsKey(RideSpell))
                return "Learn Riding first.";
            if (client.Equipment.Free(ConquerItem.Steed))
                return "Equip a steed first.";
            lock (Gate)
            {
                if (!RegisterOpen || Running) return "Registration is closed.";
                if(!CanInvite(client))return "You cannot register right now.";
                GameState prior;
                if(Registered.TryGetValue(client.Entity.UID,out prior)) {
                    if(ReferenceEquals(prior,client))return "You are already registered.";
                    Registered.Remove(client.Entity.UID);
                }
                if (Registered.Count >= Capacity) return "Steed Race is full (24).";
                Registered.Add(client.Entity.UID,client);
                return "You are registered. The race starts at " + CycleStart.ToString("HH:mm") + ". Stay mounted.";
            }
        }

        internal static bool CanEnter(GameState client) {
            if(client==null || client.Entity==null)return false;
            lock(Gate) { GameState owner;
                return Running && !GateOpened && CanInvite(client) && Registered.TryGetValue(client.Entity.UID,out owner) && ReferenceEquals(owner,client);
            }
        }

        private static void StartRace()
        {
            List<GameState> enter = new List<GameState>();
            lock (Gate)
            {
                RegisterOpen = false;
                if (Running) return;
                foreach (var entry in Registered)
                {
                    GameState client;
                    if (!Kernel.GamePool.TryGetValue(entry.Key, out client) || !ReferenceEquals(entry.Value,client)) continue;
                    if (!CanInvite(client) || HasClaim(client)) continue;
                    if (!client.Spells.ContainsKey(RideSpell)) continue;
                    if (client.Equipment.Free(ConquerItem.Steed)) continue;
                    enter.Add(client);
                    if (enter.Count >= Capacity) break;
                }
                if (enter.Count < MinPlayers)
                {
                    Kernel.SendWorldMessage(new Message("Steed Race did not start. Need at least two mounted racers.", Color.Orange, Message.Center), Server.GamePool);
                    ResetCycle();
                    return;
                }
                if (Server.Thread == null || Server.Thread.SteedRace == null)
                {
                    ResetCycle();
                    return;
                }
                Server.Thread.SteedRace.Create();
                if (SteedRace.Settings == null)
                {
                    Kernel.SendWorldMessage(new Message("Steed Race could not open a track.", Color.Orange, Message.Center), Server.GamePool);
                    ResetCycle();
                    return;
                }
                Server.Thread.SteedRace.PrepareEvent();
                Running = true;
                GateOpened = false;
                RaceUntil = DateTime.Now.Add(RaceLimit);
                PeaceUntil = Time32.Now.AddSeconds(8);
            }
            int joined=0;
            for (int i = 0; i < enter.Count; i++)
            {
                if(!Server.Thread.SteedRace.Join(enter[i]))continue;
                joined++;
                enter[i].CantAttack = PeaceUntil;
                enter[i].Send(new Message("The gate opens in 8 seconds. Ride to the finish. Top three claim at SteedRace.", Color.Yellow, Message.TopLeft));
            }
            if(joined<MinPlayers) {
                FinishEvent();
                Kernel.SendWorldMessage(new Message("Steed Race did not start. Not enough riders could enter.",Color.Orange,Message.Center),Server.GamePool);
                return;
            }
            Kernel.SendWorldMessage(new Message("Steed Race has begun. " + joined + " racers. Top three claim at SteedRace.", Color.Red, Message.Center), Server.GamePool);
        }

        private static void FinishEvent()
        {
            if (Server.Thread != null && Server.Thread.SteedRace != null)
                Server.Thread.SteedRace.End();
            ResetCycle();
        }

        private static void ResetCycle()
        {
            lock (Gate)
            {
                Running = false;
                RegisterOpen = false;
                Invited = false;
                GateOpened = false;
                Registered.Clear();
                CycleStart = NextStart(DateTime.Now.AddSeconds(2));
            }
        }

        private static bool HasClaim(GameState client)
        {
            if (client.Variables == null) return false;
            return (uint)client[ClaimKey] != 0;
        }

        private static string TryClaim(GameState client)
        {
            return SteedRaceClaims.Claim(client);
        }
    }
}
