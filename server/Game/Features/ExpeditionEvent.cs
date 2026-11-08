using System;
using System.Collections.Generic;
using System.Drawing;
using Revora.Client;
using Revora.Database;
using Revora.Interfaces;
using Revora.Network.GamePackets;

namespace Revora.Game.Features
{
    public static partial class ExpeditionEvent
    {
        public const uint ClerkNpcId = 91002;
        public const ushort ClerkMap = 1002;
        public const ushort ClerkX = 358;
        public const ushort ClerkY = 295;
        // Twin City Pedlar look. Distinct from LastManStanding (Arena Manager) and NobleWar (Captain).
        public const ushort Mesh = 2706;
        public const int Capacity = 16;
        public static readonly TimeSpan Interval = TimeSpan.FromHours(3);
        public static readonly TimeSpan RegisterWindow = TimeSpan.FromMinutes(5);
        public static readonly TimeSpan ExtractOpenAfter = TimeSpan.FromMinutes(5);
        public static readonly TimeSpan Duration = TimeSpan.FromMinutes(20);
        public static readonly TimeSpan ExtractHold = TimeSpan.FromSeconds(10);

        private static Map EventMap;
        private static DateTime CycleStart;
        private static readonly Dictionary<uint, Member> Members = new Dictionary<uint, Member>();
        private static readonly object Gate = new object();
        private static bool Running;
        private static bool AnnouncedOpen;

        private sealed class Member
        {
            public uint Uid;
            public string State;
            public List<uint> Loot = new List<uint>();
            public DateTime ExtractUntil;
            public ushort HoldX;
            public ushort HoldY;
            public uint BonusPoints;
            public uint SoulType;
            public Enums.PKMode OriginalPkMode;
        }

        public static void PlaceClerk()
        {
            INpc existing = null;
            Map from = null;
            foreach (Map map in Kernel.Maps.Values)
            {
                if (map == null || map.Npcs == null) continue;
                if (map.Npcs.TryGetValue(ClerkNpcId, out existing))
                {
                    from = map;
                    break;
                }
            }
            if (existing != null && from != null)
            {
                from.RemoveNpc(existing);
                if (Map.SecoundNpc.ContainsKey(ClerkNpcId))
                    Map.SecoundNpc.Remove(ClerkNpcId);
            }
            CityGuardSystem.SpawnTalker(ClerkNpcId, ClerkMap, ClerkX, ClerkY, Mesh, "ExpeditionClerk", (byte)Enums.ConquerAngle.South);
            if (existing != null && Kernel.Maps.ContainsKey(ClerkMap) && Kernel.Maps[ClerkMap].Npcs.ContainsKey(ClerkNpcId))
            {
                INpc placed = Kernel.Maps[ClerkMap].Npcs[ClerkNpcId];
                placed.Name = existing.Name;
            }
            try
            {
                using (var cmd = new MySqlCommand(MySqlCommandType.UPDATE))
                    cmd.Update("npcs").Set("mapid", ClerkMap).Set("cellx", ClerkX).Set("celly", ClerkY).Set("lookface", Mesh).Where("id", ClerkNpcId).Execute();
            }
            catch
            {
            }
            if (Kernel.Maps.ContainsKey(ClerkMap) && Kernel.Maps[ClerkMap].Npcs.ContainsKey(ClerkNpcId))
                Console.WriteLine("NPC " + ClerkNpcId + " at Twin City " + ClerkX + "," + ClerkY);
        }

        public static void Create()
        {
            PlaceClerk();
            Map source = Kernel.Maps.ContainsKey(1015) ? Kernel.Maps[1015] : new Map(1015, "");
            EventMap = source.MakeDynamicMap();
            if (!Constants.PKFreeMaps.Contains(EventMap.ID))
                Constants.PKFreeMaps.Add(EventMap.ID);
            InitializeField();
            CycleStart = NextStart(Clock());
            Console.WriteLine("Expedition arena map " + EventMap.ID + " (base " + EventMap.BaseID + ")");
        }

        public static DateTime NextStart(DateTime now)
        {
            DateTime utc = now.ToUniversalTime();
            int hour = (utc.Hour / 3) * 3;
            DateTime start = new DateTime(utc.Year, utc.Month, utc.Day, hour, 0, 0, DateTimeKind.Utc);
            if (utc > start) start = start.Add(Interval);
            return start.ToLocalTime();
        }

        public static bool RegistrationOpen()
        {
            DateTime now = Clock();
            return now >= CycleStart.Subtract(RegisterWindow) && now < CycleStart;
        }

        public static bool InEvent(Entity entity)
        {
            if (entity == null || EventMap == null) return false;
            return entity.MapID == EventMap.ID;
        }

        public static bool Isolates(GameState client)
        {
            return client != null && client.Entity != null && InEvent(client.Entity);
        }

        public static bool SuppressCrime(Entity killer, Entity victim)
        {
            return InEvent(killer) && InEvent(victim);
        }

        public static string StatusText()
        {
            DateTime now = Clock();
            if (Running)
            {
                TimeSpan left = CycleStart.Add(Duration) - now;
                if (left.TotalSeconds < 0) left = TimeSpan.Zero;
                string extract = Clock() >= CycleStart.Add(ExtractOpenAfter) ? "Extraction is open." : "Extraction opens after minute 5.";
                return "Expedition running. " + extract + " Time left: " + (int)left.TotalMinutes + "m. Seats " + Members.Count + "/" + Capacity + ".";
            }
            if (RegistrationOpen())
                return "Registration is open until " + CycleStart.ToString("HH:mm") + ". Seats " + Members.Count + "/" + Capacity + ". Solo only. Original equipment is protected.";
            return "Next expedition " + CycleStart.ToString("HH:mm") + ". Registration opens 5 minutes before start.";
        }

        public static bool HandleNpc(GameState client, NpcRequest request)
        {
            if (client == null || request == null) return false;
            if (HandleFieldNpc(client,request)) return true;
            if (client.ActiveNpc != ClerkNpcId) return false;
            if (!Ready(client) || client.Entity.MapID != ClerkMap || Kernel.GetDistance(client.Entity.X,client.Entity.Y,ClerkX,ClerkY)>5) return true;
            Npcs dialog = new Npcs(client);
            switch (request.OptionID)
            {
                case 0:
                    dialog.Text("Relics lie beyond the coast. Survive the hunt and reach a ferryman; a bound P7 soul awaits your safe return.");
                    dialog.Text(StatusText());
                    dialog.Option("Register for this cycle", 1);

                    dialog.Option("Leave.", 255);
                    dialog.Send();
                    return true;
                case 1:
                    dialog.Text(Register(client));
                    dialog.Option("Alright.", 255);
                    dialog.Send();
                    return true;
                case 2:
                    if (!BeginExtract(client))
                    {
                        dialog.Text("You cannot extract now. Enter the expedition and wait until extraction opens.");
                        dialog.Option("Alright.", 255);
                        dialog.Send();
                    }
                    return true;
            }
            return true;
        }

        public static string Register(GameState client)
        {
            if (!Ready(client) || !QuestInventory.Player(client,false) || client.Team != null || (client.Trade != null && client.Trade.InTrade) || client.Booth != null || client.InQualifier() || client.Entity.PokerTable>0) return "Come alone, with your affairs settled.";
            lock (Gate)
            {
                if (!RegistrationOpen()) return "Registration is closed.";
                if (Members.ContainsKey(client.Entity.UID)) return "You already hold a seat for this cycle.";
                if (Members.Count >= Capacity) return "This expedition is full.";
                Member member = new Member();
                member.Uid = client.Entity.UID;
                member.State = "registered";
                try { member.SoulType = QuestSoulReward.Create().ID; }
                catch (Exception ex) {
                    Console.WriteLine("[expedition] reward unavailable type=" + ex.GetType().Name);
                    return "The expedition's supplies are not ready. Return shortly.";
                }
                Members.Add(client.Entity.UID, member);
                return "Seat confirmed. You enter at " + CycleStart.ToString("HH:mm") + ".";
            }
        }

        public static bool TryCollectKill(GameState client, MonsterInformation monster)
        {
            if (client == null || client.Entity == null || !InEvent(client.Entity)) return false;
            lock(Gate) {
                Encounter encounter; Member member;
                if(!Running || Clock()>=CycleStart.Add(Duration) || !Ready(client) || monster==null || monster.Owner==null
                    || !Encounters.TryGetValue(monster.Owner.UID,out encounter) || !ReferenceEquals(encounter.Body,monster.Owner)
                    || !encounter.Dead || encounter.LootRolled || !Members.TryGetValue(client.Entity.UID,out member)
                    || (member.State!="entered" && member.State!="extracting")) return true;
                encounter.LootRolled=true;
                NotifyAction(client,"you fought");
                if(encounter.Camp==3) {AddTemporaryLoot(client,member,3);member.BonusPoints+=500;FieldMessage(client.Entity.Name+" has slain the Specter. The boats are still waiting.");}
                else if(encounter.Camp==-1 && Kernel.Rate(Revora.Config.ExpeditionLootChance)) AddTemporaryLoot(client,member,1);
                return true;
            }
        }

        public static bool BeginExtract(GameState client)
        {
            if (!Ready(client) || (client.Trade!=null && client.Trade.InTrade) || client.Booth!=null) return false;
            lock (Gate)
            {
                Member member;
                if (!Members.TryGetValue(client.Entity.UID, out member)) return false;
                if (!Running || Clock() < CycleStart.Add(ExtractOpenAfter))
                {
                    client.Send(new Message("Extraction opens 5 minutes after the expedition starts.", Color.Red, Message.TopLeft));
                    return false;
                }
                if (member.State != "entered") return false;
                if (!InEvent(client.Entity) || !NearOpenExit(client) || Clock()>=CycleStart.Add(Duration)) return false;
                member.State = "extracting";
                member.ExtractUntil = Clock().Add(ExtractHold);
                member.HoldX = client.Entity.X;
                member.HoldY = client.Entity.Y;
                client.Send(new Message("Hold position for 10 seconds to extract. Moving or taking damage cancels it.", Color.Yellow, Message.TopLeft));
                return true;
            }
        }

        public static void NotifyAction(GameState client, string reason)
        {
            if (client == null || client.Entity == null) return;
            bool cancel = false;
            lock (Gate)
            {
                Member member;
                if (!Members.TryGetValue(client.Entity.UID, out member)) return;
                if (member.State != "extracting") return;
                member.State = "entered";
                cancel = true;
            }
            if (cancel)
                client.Send(new Message("Extraction cancelled: " + reason, Color.Orange, Message.TopLeft));
        }

        public static void OnDeath(GameState client)
        {
            if (client == null || client.Entity == null) return;
            lock (Gate)
            {
                Member member;
                if (!Members.TryGetValue(client.Entity.UID, out member)) return;
                if (!InEvent(client.Entity)) return;
                member.State = "dead";
                member.Loot.Clear();
                member.BonusPoints=0;
            }
            client.Entity.Teleport(1002, 408, 330);
            client.Send(new Message("You died in the expedition. Temporary loot is lost. Original equipment is safe.", Color.Red, Message.TopLeft));
        }

        public static void OnTeleport(GameState client, ushort mapId)
        {
            if (client == null || client.Entity == null || EventMap == null) return;
            if (client.Entity.MapID != EventMap.ID) return;
            if (mapId == EventMap.ID) return;
            lock (Gate)
            {
                Member member;
                if (!Members.TryGetValue(client.Entity.UID, out member)) return;
                if(member.State!="registered") {
                    client.Entity.PKMode=member.OriginalPkMode;
                    client.Send(new Data(true){UID=client.Entity.UID,ID=Data.ChangePKMode,dwParam=(uint)member.OriginalPkMode});
                    client.Send(new Message("",Color.White,Message.FirstRightCorner));
                }
                if (member.State == "extracted" || member.State == "dead") return;
                member.State = "dead";
                member.Loot.Clear();
                member.BonusPoints=0;
            }
            client.Send(new Message("Leaving the expedition without extracting forfeits temporary loot.", Color.Red, Message.TopLeft));
        }

        public static void OnDisconnect(GameState client)
        {
            OnTeleport(client,ClerkMap);
        }

        public static void Tick()
        {
            if (CycleStart == default(DateTime)) return;
            DateTime now = Clock();
            if (!Running && !AnnouncedOpen && now >= CycleStart.Subtract(RegisterWindow) && now < CycleStart)
            {
                AnnouncedOpen = true;
                Kernel.SendWorldMessage(new Message("Expedition registration is open for 5 minutes. Talk to ExpeditionClerk in Twin City (" + ClerkX + ", " + ClerkY + ").", Color.Yellow, Message.Center), Server.GamePool);
                foreach (GameState client in Kernel.GamePool.Values)
                {
                    if (client == null || client.Entity == null || client.Entity.Dead) continue;
                    ushort map = client.Entity.MapID;
                    if (map == 6000 || map == 6001 || map == 6002 || map == 6003 || map == 6004 || map == 3846) continue;
                    if (client.InQualifier() || client.Entity.PokerTable > 0 || client.PlayRouletteUID > 0) continue;
                    GameState captured = client;
                    captured.MessageOK = p => { Register(p); };
                    captured.Send(new NpcReply(NpcReply.MessageBox, "Expedition registration is open. Solo only. Original gear is protected. Register now? ExpeditionClerk is in Twin City (" + ClerkX + ", " + ClerkY + ")."));
                    captured.Send(new Data(true) { UID = captured.Entity.UID, dwParam = 60, ID = Data.CountDown });
                }
            }
            if (!Running && now >= CycleStart)
                StartCycle();
            if (!Running) return;
            if (now >= CycleStart.Add(Duration))
            {
                EndCycle();
                return;
            }
            lock(Gate) TickField(now);
            List<GameState> finish = new List<GameState>();
            List<GameState> moved = new List<GameState>();
            lock (Gate)
            {
                foreach (Member member in Members.Values)
                {
                    GameState client;
                    if (!Kernel.GamePool.TryGetValue(member.Uid, out client) || client.Entity == null) continue;
                    if (member.State == "extracting")
                    {
                        if (client.Entity.X != member.HoldX || client.Entity.Y != member.HoldY)
                            moved.Add(client);
                        else if (now >= member.ExtractUntil)
                            finish.Add(client);
                    }
                }
            }
            for (int i = 0; i < moved.Count; i++)
                NotifyAction(moved[i], "you moved");
            for (int i = 0; i < finish.Count; i++)
                CompleteExtract(finish[i]);
        }

        private static void StartCycle()
        {
            lock (Gate)
            {
                if(Running || Clock()<CycleStart)return;
                if (Members.Count == 0)
                {
                    CycleStart = NextStart(Clock().AddSeconds(1));
                    AnnouncedOpen = false;
                    Kernel.SendWorldMessage(new Message("Expedition cancelled: no registered players.", Color.White, Message.Center), Server.GamePool);
                    return;
                }
                PrepareField();
                Running = true;
                int entry = 0;
                foreach (Member member in Members.Values)
                {
                    GameState client;
                    if (!Kernel.GamePool.TryGetValue(member.Uid, out client) || !Ready(client) || client.Team!=null || client.InQualifier() || client.Booth!=null || (client.Trade!=null && client.Trade.InTrade))
                    {
                        member.State = "dead";
                        continue;
                    }
                    member.State = "entered";
                    member.OriginalPkMode=client.Entity.PKMode;
                    client.Entity.PKMode=Enums.PKMode.PK;
                    client.Send(new Data(true){UID=client.Entity.UID,ID=Data.ChangePKMode,dwParam=(uint)Enums.PKMode.PK});
                    client.CantAttack=Time32.Now.AddSeconds(10);
                    var point=Entries[entry++];
                    client.Entity.Teleport(EventMap.ID, (ushort)point.X, (ushort)point.Y);
                    client.Send(new Message("The coast is open. Hunt, raid the guarded caches, and find an open ferryman. Your satchel holds twelve relics.", Color.Yellow, Message.Center));
                }
            }
            Kernel.SendWorldMessage(new Message("The Expedition is underway. Extraction opens in 5 minutes.", Color.Red, Message.Center), Server.GamePool);
        }

        private static void CompleteExtract(GameState client)
        {
            if(!Ready(client) || !System.Threading.Monitor.TryEnter(client.ItemSyncRoot)) return;
            bool extracted=false;
            try {
                lock(Gate) {
                    Member member;
                    if(!Ready(client) || !Running || Clock()>=CycleStart.Add(Duration) || !InEvent(client.Entity)
                        || !Members.TryGetValue(client.Entity.UID,out member) || member.State!="extracting"
                        || Clock()<member.ExtractUntil || client.Entity.X!=member.HoldX || client.Entity.Y!=member.HoldY
                        || !NearOpenExit(client) || (client.Trade!=null && client.Trade.InTrade) || client.Booth!=null) return;
                    extracted=CommitExtraction(client,member);
                }
            } finally {System.Threading.Monitor.Exit(client.ItemSyncRoot);}
            if(extracted)client.Entity.Teleport(1002,408,330);
        }

        private static void EndCycle()
        {
            lock (Gate)
            {
                if(!Running)return;
                foreach (Member member in Members.Values)
                {
                    GameState client;
                    if (!Kernel.GamePool.TryGetValue(member.Uid, out client) || client.Entity == null) continue;
                    if (member.State == "entered" || member.State == "extracting")
                    {
                        member.Loot.Clear();
                        member.BonusPoints=0;
                        if (InEvent(client.Entity))
                            client.Entity.Teleport(1002, 408, 330);
                        client.Send(new Message("The expedition ended. Unextracted loot is lost.", Color.Red, Message.TopLeft));
                    }
                }
                ClearEncounters();
                Members.Clear();
                Running = false;
                AnnouncedOpen = false;
                CycleStart = NextStart(Clock().AddSeconds(1));
                WriteFieldStatus();
            }
        }
    }
}
