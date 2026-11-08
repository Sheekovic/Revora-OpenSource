using System;
using System.IO;
using System.Linq;
using System.Text;
using Revora.Game;
using System.Drawing;
using Revora.Client;
using Revora.Interfaces;
using Revora.Game.Features;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace Revora.Network.GamePackets
{
    public static class GeneralData
    {
        public static string ReadString(byte[] data, ushort position, ushort count)
        {
            StringBuilder builder = new StringBuilder();
            for (byte i = 0; i < count; i = (byte)(i + 1))
            {
                if (i > data.Length)
                {
                    break;
                }
                byte num2 = data[(ushort)(i + position)];
                builder.Append((char)num2);
            }
            return builder.ToString().Replace("\0", "").Replace("\r", "");
        }
        public static void CreateTeam(Team team, Client.GameState client)
        {
            lock (client.ItemSyncRoot)
            {
            if (client.Team != null) return;
            if (client.Entity.MaxHitpoints == 0)//if(hp == 0) the client will close
                return;
            if (client.Entity.MapID == 8892 || client.InQualifier())
            {
                NpcReply reply = new NpcReply(6, "Sorry, All team Systems is forbidden here");
                reply.OptionID = 255;
                client.Send(reply.ToArray());
                return;
            }
            client.Team = new Game.ConquerStructures.Team(client);

            team.Type = 15;
            client.Send(team.ToArray());

            team.Type = 0;
            client.Send(team.ToArray());
            return;
            }
        }
        public static void ReincarnationHash(GameState client)
        {
            Game.Features.ReincarnationRecovery.RestoreReady(client);
        }
        public static void PrintPacket(byte[] packet)
        {
            foreach (byte D in packet)
            {
                Console.WriteLine((Convert.ToString(D, 16)).PadLeft(2, '0') + " ");
            }
            Console.WriteLine("\n\n");
        }
        #region Nuevo Sistema Chi
        private static void SendChiRankings(GenericRanking ranking, GameState client)
        {
            SendRankingQuery(ranking, client, GenericRanking.DragonChi, (uint)client.ChiData.DragonRank, (uint)client.ChiData.DragonPoints);
            SendRankingQuery(ranking, client, GenericRanking.PhoenixChi, (uint)client.ChiData.PhoenixRank, (uint)client.ChiData.PhoenixPoints);
            SendRankingQuery(ranking, client, GenericRanking.TigerChi, (uint)client.ChiData.TigerRank, (uint)client.ChiData.TigerPoints);
            SendRankingQuery(ranking, client, GenericRanking.TurtleChi, (uint)client.ChiData.TurtleRank, (uint)client.ChiData.TurtlePoints);
        }

        public static void SendRankingQuery(GenericRanking ranking, GameState client, uint type, uint rank, uint points)
        {
            if (rank != 0)
            {
                ranking.RankingType = type;
                ranking.Append(rank, points, client.Entity.UID, client.Entity.Name);
                client.Send(ranking);
                ranking.Reset();
            }
        }
        public static void QueryChi(Chi chi, GameState client)
        {
            Client.GameState pClient;
            if (Kernel.GamePool.TryGetValue(chi.UID, out pClient))
                client.Send(new ChiPowers(true).Query(pClient, false));
        }
        public static void ShowGenericRanking(GenericRanking ranking, GameState client)
        {
            ushort page = ranking.Page;
            Database.ChiTable.ChiData[] list = null;
            Func<Database.ChiTable.ChiData, int> select = null;
            if (ranking.RankingType == GenericRanking.Prestige)
            {
                PerfectionManager.ShowRank(ranking, client);
                return;
            }
            if (ranking.RankingType == GenericRanking.DragonChi)
            {
                list = Database.ChiTable.Dragon;
                select = (chiData) => { return chiData.DragonPoints; };
            }
            else if (ranking.RankingType == GenericRanking.PhoenixChi)
            {
                list = Database.ChiTable.Phoenix;
                select = (chiData) => { return chiData.PhoenixPoints; };
            }
            else if (ranking.RankingType == GenericRanking.TigerChi)
            {
                list = Database.ChiTable.Tiger;
                select = (chiData) => { return chiData.TigerPoints; };
            }
            else if (ranking.RankingType == GenericRanking.TurtleChi)
            {
                list = Database.ChiTable.Turtle;
                select = (chiData) => { return chiData.TurtlePoints; };
            }
            if (list.Length < page * 10) return;
            uint count = (uint)Math.Min(list.Length - page * 10, 10);
            var nRanking = new GenericRanking(true, count);
            nRanking.Mode = GenericRanking.Ranking;
            nRanking.RankingType = ranking.RankingType;
            nRanking.Page = page;
            nRanking.RegisteredCount = (ushort)list.Length;
            int rank = (int)page * 10;
            for (int i = rank; i < rank + count; i++)
            {
                var current = list[i];
                nRanking.Append((uint)(i + 1), (uint)select(current), current.UID, current.Name);
            }
            client.Send(nRanking);
        }
        public static void ShowGenericRanking2(GenericRanking ranking, GameState client)
        {
            if (ranking.RankingType == GenericRanking.Prestige)
            {
                PerfectionManager.ShowRank(ranking, client);
                return;
            }
            var response = FlowerRankingPage(ranking);
            if (response != null) client.Send(response);
        }

        internal static GenericRanking FlowerRankingPage(GenericRanking ranking)
        {
            ushort page = ranking.Page;
            Game.Features.Flowers[] list = null;
            Func<Game.Features.Flowers, uint> select = null;
            if (ranking.RankingType == GenericRanking.RoseFairy)
            {
                list = Game.Features.Flowers.RedRousesTop100;
                select = (flower) => { return flower.RedRoses; };
            }
            else if (ranking.RankingType == GenericRanking.LilyFairy)
            {
                list = Game.Features.Flowers.LiliesTop100;
                select = (chiData) => { return chiData.Lilies; };
            }
            else if (ranking.RankingType == GenericRanking.OrchidFairy)
            {
                list = Game.Features.Flowers.OrchidsTop100;
                select = (chiData) => { return chiData.Orchads; };
            }
            else if (ranking.RankingType == GenericRanking.TulipFairy)
            {
                list = Game.Features.Flowers.TulipsTop100;
                select = (chiData) => { return chiData.Tulips; };
            }

            else if (ranking.RankingType == GenericRanking.Kiss)
            {
                list = Game.Features.Flowers.KissTop100;
                select = flower => flower.RedRoses;
            }
            else if (ranking.RankingType == GenericRanking.Love)
            {
                list = Game.Features.Flowers.LoveTop100;
                select = flower => flower.Lilies;
            }
            else if (ranking.RankingType == GenericRanking.Tins)
            {
                list = Game.Features.Flowers.TineTop100;
                select = flower => flower.Orchads;
            }
            else if (ranking.RankingType == GenericRanking.Jade)
            {
                list = Game.Features.Flowers.JadeTop100;
                select = flower => flower.Tulips;
            }

            if (list == null || list.Length < page * 10) return null;
            uint count = (uint)Math.Min(list.Length - page * 10, 10);
            var nRanking = GenericRanking.FlowerPage(count);
            nRanking.Mode = GenericRanking.Ranking;
            nRanking.RankingType = ranking.RankingType;
            nRanking.Page = page;
            nRanking.RegisteredCount = (ushort)list.Length;
            int rank = (int)page * 10;
            for (int i = rank; i < rank + count; i++)
            {
                var current = list[i];
                nRanking.Append((uint)(i + 1), (uint)select(current), current.UID, current.Name);
            }
            return nRanking;
        }

        public static void UnlockChi(Chi chi, GameState client)
        {
            if (chi == null || (byte)chi.Mode < 1 || (byte)chi.Mode > 4) return;
            bool changed = Revora.Game.Features.ChiWallet.ChangePower(client, powers => {
                int index = (int)chi.Mode - 1;
                if (client.Entity.Level < 110 || powers.Count != index) return 0;
                if (index > 0 && (client.Entity.Reborn < Math.Min(index, 2) || powers[index - 1].Points < 300)) return 0;
                if (index == 3 && client.Entity.Reborn != 2) return 0;
                powers.Add(new Game.ConquerStructures.ChiPowerStructure(chi.Mode));
                return 300;
            });
            if (changed) PublishChi(client, chi.Mode);
        }

        private static Game.Enums.ChiAttribute RandomChi()
        {
            return (Game.Enums.ChiAttribute)Kernel.Random.Next(1, 14);
        }


        internal static Func<int, int, int> ChiValueNext = (low, high) => Kernel.Random.Next(low, high);
        internal static Func<int, int, bool> ChiValueRate = Kernel.Rate;
        private static ushort ChiValue(Game.Enums.ChiAttribute attribute)
        {
            return RollChiValue(attribute, ChiValueNext, ChiValueRate);
        }
        internal static ushort RollChiValue(Game.Enums.ChiAttribute attribute,
            Func<int, int, int> next, Func<int, int, bool> rate)
        {
            int max = Game.Enums.ChiMaxValues(attribute),
                min = Game.Enums.ChiMinValues(attribute);
            float baseDiff = (max - min) / 10f;
            // Next excludes its upper bound. Full attributes must be attainable
            // for 400-point powers and the 1,600-point Grandmaster requirement.
            if (rate(4, 100)) return (ushort)next((int)(max - baseDiff), max + 1);
            else if (rate(10, 100)) return (ushort)next((int)(max - 2 * baseDiff), (int)(max - baseDiff));
            else if (rate(40, 100)) return (ushort)next((int)(max - 3 * baseDiff), (int)(max - 2 * baseDiff));
            else return (ushort)next(min, max + 1);
        }
        private static ushort ChiValue2(Game.Enums.ChiAttribute attribute, GameState client)
        {
            return ChiValue(attribute);
        }
        private static bool CanKeepChiPower(Game.ConquerStructures.ChiPowerStructure strct, int attribute, GameState client)
        {
            //#if NOTMULTIPLECHIPOWERS
            if (client.Entity.VIPLevel < 6)
            {
                var attr = strct.Attributes;
                var att = attr[attribute];
                for (int i = 0; i < attr.Length; i++)
                    if (i != attribute)
                        if (attr[i].Type == att.Type)
                            return false;
            }
            //#endif
            return true;
        }
        internal static bool ChiStudyCost(Chi chi, GameState client, out uint cost)
        {
            cost = 0;
            if (chi == null || (byte)chi.Mode < 1 || (byte)chi.Mode > 4
                || chi.dwParam >= 15 || !Revora.Game.Features.QuestInventory.Player(client)) return false;
            lock (client.ItemSyncRoot) {
                if (!Revora.Game.Features.QuestInventory.Player(client) || client.ChiPowers.Count < (int)chi.Mode) return false;
                var power = client.ChiPowers[(int)chi.Mode - 1];
                if (power == null || power.Power != chi.Mode || power.Attributes == null || power.Attributes.Length != 4) return false;
                cost = 50;
                for (int i = 0; i < 4; i++) if ((chi.dwParam & (1u << i)) != 0) cost += 50;
                return true;
            }
        }
        internal static void HandleChiStudy(Chi chi, GameState client)
        {
            uint cost;
            if (!ChiStudyCost(chi, client, out cost)) return;
            if (client.ChiPoints < cost) BuyStrengthChi(chi, client);
            else StudyChi(chi, client);
        }
        public static void StudyChi(Chi chi, GameState client)
        {
            uint studyCost;
            if (!ChiStudyCost(chi, client, out studyCost)) return;
            bool changed = Revora.Game.Features.ChiWallet.ChangePower(client, proposed => {
                uint cost;
                if (!ChiStudyCost(chi, client, out cost) || client.ChiPoints < cost) return 0;
                var power = proposed[(int)chi.Mode - 1];
                for (int slot = 0; slot < 4; slot++) {
                    if ((chi.dwParam & (1u << slot)) != 0) continue;
                    var available = Enumerable.Range(1, 13).Select(n => (Game.Enums.ChiAttribute)n)
                        .Where(type => !power.Attributes.Where((value, index) => index != slot).Any(value => value.Type == type)).ToArray();
                    var attribute = available[Kernel.Random.Next(available.Length)];
                    power.Attributes[slot] = new Game.ConquerStructures.ChiAttribute(attribute, ChiValue(attribute));
                }
                power.CalculatePoints();
                return cost;
            });
            if (changed) PublishChi(client, chi.Mode);
        }
        private static void PublishChi(GameState client, Game.Enums.ChiPowerType mode)
        {
            Database.ChiTable.Sort(mode);
            // No actor lock is held while recalculating another player's stats.
            var ranking = mode == Game.Enums.ChiPowerType.Dragon ? Database.ChiTable.Dragon
                : mode == Game.Enums.ChiPowerType.Phoenix ? Database.ChiTable.Phoenix
                : mode == Game.Enums.ChiPowerType.Tiger ? Database.ChiTable.Tiger : Database.ChiTable.Turtle;
            foreach (var data in ranking) {
                GameState online;
                if (!Kernel.GamePool.TryGetValue(data.UID, out online) || !Revora.Game.Features.QuestInventory.Player(online) || online.ChiData == null) continue;
                SendRankingQuery(new GenericRanking(true) { Mode = GenericRanking.QueryCount }, online,
                    GenericRanking.Chi + (uint)mode, online.ChiData.SelectRank(mode), online.ChiData.SelectPoints(mode));
                online.LoadItemStats();
            }
            if (!Revora.Game.Features.QuestInventory.Player(client) || !Revora.Game.Features.SessionInstall.TryEnter(client.Entity.UID)) return;
            uint uid = client.Entity.UID;
            try {
                lock (client.ItemSyncRoot) {
                    if (!Revora.Game.Features.QuestInventory.Player(client)) return;
                    Revora.Game.Features.QuestInventory.Refresh(client);
                    client.Send(new ChiPowers(true).Query(client));
                }
            } catch (Exception ex) { Revora.Game.Features.QuestInventory.Fail(client, "chi-publish", ex); }
            finally { Revora.Game.Features.SessionInstall.Exit(uid); }
        }

        public static void BuyStrengthChi(Chi chi, GameState client)
        {
            Revora.Game.Features.ChiWallet.Refill(client);
        }
        #endregion
        public static bool PassRequeriments(Game.SubClass Sc, Game.Entity Entity)
        {
            Boolean Pass = false;
            byte Level = 0;
            byte Reborns = 0;
            #region [Switch Phases]
            switch (Sc.Phase)
            {
                case 0:
                case 1: return true;
                case 2: Level = 90; Reborns = 0; break;
                case 3: Level = 110; Reborns = 0; break;
                case 4: Level = 120; Reborns = 0; break;
                case 5: Level = 90; Reborns = 0; break;
                case 6: Level = 110; Reborns = 0; break;
                case 7: Level = 120; Reborns = 0; break;
                case 8: Level = 120; Reborns = 0; break;
                case 9: Level = 120; Reborns = 0; break;
            }
            #endregion
            if (Sc.Level > Sc.Phase && Entity.Level >= Level && Entity.Reborn >= Reborns)
                Pass = true;
            if (Sc.Level == 9 && Sc.Phase == 9)
                Pass = false;
            return Pass;
        }
        public static void ChangeAppearance(Data Data, GameState client)
        {
            if (client.Entity.Tournament_Signed && ((Enums.AppearanceType)Data.dwParam) != Enums.AppearanceType.Garment) return;
            Data.UID = client.Entity.UID;
            client.Entity.Appearance = (Enums.AppearanceType)Data.dwParam;
            client.SendScreen(Data, true);
        }
        public static bool SwitchEquipment(GameState client, bool toAlternative)
        {
            if(!Revora.Game.Features.QuestInventory.Player(client))return false;
            lock(client.ItemSyncRoot) {
                if(!Revora.Game.Features.QuestInventory.Player(client))return false;
                bool changed=client.AlternateEquipment!=toAlternative;
                if(!SwitchEquipmentCore(client,toAlternative))return false;
                if(changed && toAlternative)Revora.Game.Features.EquipmentAchievements.RecordSwitch(client);
                return !client.Entity.PersistenceRequiresReload;
            }
        }
        private static bool SwitchEquipmentCore(GameState client, bool toAlternative)
        {
            if (client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Fly))
            {
                client.Send(new Message("You cannot switch equipment while flying.", System.Drawing.Color.Red, Message.Talk));
                return false;
            }
            if (toAlternative && client.Equipment.Free(ConquerItem.AlternateRightWeapon) && !client.Equipment.Free(ConquerItem.AlternateLeftWeapon))
            {
                client.Send(new Message("Invalid weapons! Looking for the main weapon? Unequip the left-hand weapon first.", System.Drawing.Color.Red, Message.Talk));
                return false;
            }
            client.Entity.AttackPacket = null;
            client.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.Fly);
            client.AlternateEquipment = toAlternative;
            client.LoadItemStats();
            if (client.Equipment != null && client.Equipment.Objects != null)
            {
                foreach (ConquerItem worn in client.Equipment.Objects)
                {
                    if (worn != null)
                        worn.IsWorn = true;
                }
            }
            client.Equipment.UpdateEntityPacket();
            WindowStats Stats = new WindowStats(client);
            client.Send(Stats.ToArray());
            return true;
        }
        public static void LevelUpSpell(Data generalData, GameState client)
        {
            ushort spellID = (ushort)generalData.dwParam;
            ISkill spell = null;
            if (client.Spells.TryGetValue(spellID, out spell))
            {
                var spellInfo = Database.SpellTable.GetSpell(spellID, client);
                if (spellInfo != null)
                {
                    if (client.Trade.InTrade) return;
                    uint CpsCost = 0;
                    #region Costs
                    switch (spell.Level)
                    {
                        case 0: CpsCost = 27; break;
                        case 1: CpsCost = 81; break;
                        case 2: CpsCost = 122; break;
                        case 3: CpsCost = 181; break;
                        case 4: CpsCost = 300; break;
                        case 5: CpsCost = 400; break;
                        case 6: CpsCost = 500; break;
                        case 7: CpsCost = 600; break;
                        case 8: CpsCost = 800; break;
                        case 9: CpsCost = 1000; break;
                    }
                    #endregion
                    int max = Math.Max((int)spell.Experience, 1);
                    int percentage = 100 - (int)(max / Math.Max((spellInfo.NeedExperience / 100), 1));
                    CpsCost = (uint)(CpsCost * percentage / 100);
                    if (client.Entity.ConquerPoints >= CpsCost)
                    {
                        client.Entity.TrySpendConquerPoints((uint)(CpsCost));
                        spell.Level++;
                        if (spell.Level == spell.PreviousLevel / 2)
                            spell.Level = spell.PreviousLevel;
                        spell.Experience = 0;
                        spell.Send(client);
                    }
                }
            }
        }
        public static void LevelUpProficiency(Data generalData, GameState client)
        {
            ushort proficiencyID = (ushort)generalData.dwParam;
            IProf proficiency = null;
            if (client.Proficiencies.TryGetValue(proficiencyID, out proficiency))
            {
                if (proficiency.Level != 20)
                {
                    if (client.Trade.InTrade) return;
                    uint cpCost = 0;
                    #region Costs
                    switch (proficiency.Level)
                    {
                        case 1: cpCost = 28; break;
                        case 2: cpCost = 28; break;
                        case 3: cpCost = 28; break;
                        case 4: cpCost = 28; break;
                        case 5: cpCost = 28; break;
                        case 6: cpCost = 55; break;
                        case 7: cpCost = 81; break;
                        case 8: cpCost = 135; break;
                        case 9: cpCost = 162; break;
                        case 10: cpCost = 270; break;
                        case 11: cpCost = 324; break;
                        case 12: cpCost = 324; break;
                        case 13: cpCost = 324; break;
                        case 14: cpCost = 324; break;
                        case 15: cpCost = 375; break;
                        case 16: cpCost = 548; break;
                        case 17: cpCost = 799; break;
                        case 18: cpCost = 1154; break;
                        case 19: cpCost = 1420; break;
                    }
                    #endregion
                    uint needExperience = Database.DataHolder.ProficiencyLevelExperience(proficiency.Level);
                    int max = Math.Max((int)proficiency.Experience, 1);
                    int percentage = 100 - (int)(max / (needExperience / 100));
                    cpCost = (uint)(cpCost * percentage / 100);
                    if (client.Entity.ConquerPoints >= cpCost)
                    {
                        client.Entity.TrySpendConquerPoints((uint)(cpCost));
                        proficiency.Level++;
                        if (proficiency.Level == proficiency.PreviousLevel / 2)
                        {
                            proficiency.Level = proficiency.PreviousLevel;
                            Database.DataHolder.ProficiencyLevelExperience((byte)(proficiency.Level + 1));
                        }
                        proficiency.Experience = 0;
                        proficiency.Send(client);
                    }
                }
            }
        }
        public static void Revive(Data generalData, GameState client)
        {
            if (client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.SoulShackle)) return;
            if (Time32.Now >= client.Entity.DeathStamp.AddSeconds(18) && client.Entity.Dead)
            {
                client.Entity.Action = Game.Enums.ConquerAction.None;
                client.ReviveStamp = Time32.Now;
                client.Attackable = false;
                client.Entity.TransformationID = 0;
                client.Entity.AutoRev = 0;
                client.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.Dead);
                client.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.Ghost);
                client.Entity.Hitpoints = client.Entity.MaxHitpoints;
                if (client.Entity.MapID == 1518)
                {
                    client.Entity.Teleport(1002, 408, 330);
                    return;
                }
                bool ReviveHere = generalData.dwParam == 1;
                if (client.Entity.MapID == 1038 && DateTime.Now.DayOfWeek == DayOfWeek.Sunday)
                {
                    client.Entity.Teleport(1002, 227, 236);
                }
                else if (client.Entity.MapID == 3333 || client.Entity.MapID == 3334 ||
                         client.Entity.MapID == 3335 || client.Entity.MapID == 50 ||
                         client.Entity.MapID == 1365 || client.Entity.MapID == 1366 ||
                         client.Entity.MapID == 1367 || client.Entity.MapID == 1368 ||
                         client.Entity.MapID == 2014 || client.Entity.MapID == 4022 ||
                    client.Entity.MapID == 60)
                {
                    client.Entity.Teleport(1002, 408, 330);
                }
                else
                {
                    if (ReviveHere && client.Entity.HeavenBlessing > 0)
                    {
                        client.Send(new MapStatus()
                        {
                            BaseID = client.Map.BaseID,
                            ID = client.Map.ID,
                            Status = Database.MapsTable.MapInformations[client.Map.ID].Status
                        });
                    }
                    else
                    {
                        ushort[] Point = Database.DataHolder.FindReviveSpot(client.Map.ID);
                        client.Entity.Teleport(Point[0], Point[1], Point[2]);
                    }
                }
            }
        }
        public static void UsePortal(Data generalData, GameState client)
        {
            if (Revora.Game.Features.TempestWingsQuest.Portal(client,generalData)) return;
            client.Entity.Action = Game.Enums.ConquerAction.None;
            client.ReviveStamp = Time32.Now;
            client.Attackable = false;
            ushort portal_X = (ushort)(generalData.dwParam & 65535);
            ushort portal_Y = (ushort)(generalData.dwParam >> 16);
            string portal_ID = portal_X.ToString() + ":" + portal_Y.ToString() + ":" + client.Map.ID.ToString();
            if (client.Account.State == Database.AccountTable.AccountState.Administrador)
                client.Send(new Message("Portal ID: " + portal_ID, Message.TopLeft));
            foreach (Game.Portal portal in client.Map.Portals)
            {
                if (Kernel.GetDistance(portal.CurrentX, portal.CurrentY, client.Entity.X, client.Entity.Y) <= 4)
                {
                    ushort landingX = portal.DestinationX, landingY = portal.DestinationY;
                    if (!Revora.Game.Features.AdventureTravel.PreparePortal(client, portal, ref landingX, ref landingY)) return;
                    client.Entity.PrevX = client.Entity.X;
                    client.Entity.PrevY = client.Entity.Y;
                    client.Entity.Teleport(portal.DestinationMapID, landingX, landingY);
                    return;
                }
            }
            client.Entity.Teleport(1002, 408, 330);
        }
        public static void ObserveEquipment(Data generalData, GameState client)
        {
            if (ItemHandler.NulledClient(client)) return;
            GameState Observer, Observee;
            if (Kernel.GamePool.TryGetValue(generalData.UID, out Observer) && Kernel.GamePool.TryGetValue(generalData.dwParam, out Observee))
            {
                if (generalData.ID != 117)
                    Observer.Send(Observee.Entity.WindowSpawn());
                WindowStats Stats = new WindowStats(Observee);
                Observer.Send(Stats.ToArray());
                for (Byte pos = (Byte)ConquerItem.Head; pos <= ConquerItem.AlternateGarment; pos++)
                {
                    ConquerItem i = Observee.Equipment.TryGetItem((Byte)pos);
                    if (i != null)
                    {
                        if (i.IsWorn)
                        {
                            BoothItem2 view = new BoothItem2();
                            view.CostType = BoothItem2.CostTypes.ViewEquip;
                            view.Identifier = Observee.Entity.UID;
                            view.Position = (ItemHandler.Positions)(pos % 20);
                            view.ParseItem(i);
                            Observer.Send(view);
                            i.SendExtras(client);
                        }
                    }
                }
                foreach (var item in Observee.RunesItem.Values)
                {
                    if (item.Position != 211)
                    {
                        BoothItem2 view = new BoothItem2();
                        view.CostType = (BoothItem2.CostTypes)7;
                        view.Identifier = Observee.Entity.UID;
                        view.Position = (ItemHandler.Positions)item.Position;
                        view.ParseItem(item);
                        Observer.Send(view);
                        item.SendExtras(client);
                    }
                }
                _String packet = new _String(true);
                packet.Type = 16;
                packet.UID = client.Entity.UID;
                packet.TextsCount = 1;
                packet.Texts = new List<string>()
                {
                    Observee.Entity.Spouse 
                };
                Observer.Send(packet);
                if (generalData.ID == 117)
                {
                    packet.Type = 10;
                    Observer.Send(packet);
                }
                Observer.Send(generalData);
                Observee.Send(new Message(Observer.Entity.Name + " is inspecting your equipment.", Color.Red, Message.System));
            }
        }
        public static void ChangeFace(Data generalData, GameState client)
        {
            if (client.Entity.Money >= 500)
            {
                uint newface = generalData.dwParam;
                if (client.Entity.Body > 2000)
                {
                    newface = newface < 200 ? newface + 200 : newface;
                    client.Entity.Face = (ushort)newface;
                }
                else
                {
                    newface = newface > 200 ? newface - 200 : newface;
                    client.Entity.Face = (ushort)newface;
                }
            }
        }
        public static void CheckForRaceItems(GameState client)
        {
            StaticEntity item;
            if (client.Screen.GetRaceObject(p => { return Kernel.GetDistance(client.Entity.X, client.Entity.Y, p.X, p.Y) <= 1; }, out item))
            {
                if (item == null) return;
                if (!item.Viable) return;
                var type = item.Type;
                bool successful = false;
                if (type == Enums.RaceItemType.FrozenTrap && !item.QuestionMark)
                {
                    if (item.SetBy != client.Entity.UID)
                    {
                        client.ApplyRacePotion(type, uint.MaxValue);
                        client.Map.RemoveStaticItem(item);
                        successful = true;
                    }
                }
                else
                {
                    if (client.Potions == null) client.Potions = new UsableRacePotion[5];
                    for (ushort i = 0; i < client.Potions.Length; i++)
                    {
                        var pot = client.Potions[i];
                        if (pot == null)
                        {
                            pot = (client.Potions[i] = new UsableRacePotion());
                            pot.Type = type;
                            pot.Count = item.Level;
                            client.Send(new RacePotion(true)
                            {
                                PotionType = type,
                                Amount = (ushort)pot.Count,
                                Location = (ushort)(i + 1)
                            });
                            successful = true;
                            break;
                        }
                        else
                        {
                            if (pot.Type == type)
                            {
                                pot.Count += item.Level;
                                client.Send(new RacePotion(true)
                                {
                                    PotionType = type,
                                    Amount = (ushort)pot.Count,
                                    Location = (ushort)(i + 1)
                                });
                                successful = true;
                                break;
                            }
                        }
                    }
                }
                if (successful)
                {
                    client.SendScreen(new _String(true)
                    {
                        Texts = new List<string>() { "eidolon" },
                        UID = client.Entity.UID,
                        Type = _String.Effect
                    });
                    client.RemoveScreenSpawn(item, true);
                    item.Viable = false;
                    item.NotViableStamp = Time32.Now;
                }
            }
        }
        public static void PlayerJump(Data generalData, GameState client)
        {
            if (!Revora.Game.Features.SteedRaceCourse.CanMove(client)) return;
            if (WarriorStance.BlockMovement(client)) return;
            if (ExorcismCombat.IsStunned(client) || SnowBansheeCombat.IsStunned(client) || PurpleBansheeCombat.IsStunned(client)) return;
            if (client.Entity.Dead || client.Entity.ContainsFlag(((ulong)FlagsUpdate.Flags.Dead)) || client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Ghost))
            {
                return;
            }
            client.Entity.KillCount2 = 0;
            client.Entity.SpiritFocus = false;
            ushort oldX = client.Entity.X;
            ushort oldY = client.Entity.Y;
            ushort new_X = (ushort)generalData.Buffer.Coords_X;
            ushort new_Y = (ushort)generalData.Buffer.Coords_Y;

            if (client.Entity.OnCheckGuard)
            {
                client.Entity.OnCheckGuard = false;
                client.MessageBox("Unable to inspect!", null, null, 0);
                DialyQuestsEffect.CheckCancelGuard(client);
                client.Entity.CheckGuardSec = 0;
            }

            if (client.Entity.MapID == 3846 && Kernel.SpawnNemesis2)
            {
                foreach (INpc Npc in client.Map.Npcs.Values)
                {
                    if (Npc.MapID == 3846 && (Npc.UID == 3080) && Kernel.GetDistance(client.Entity.X, client.Entity.Y, Npc.X, Npc.Y) < 17)
                    {
                        Npc.SendSpawn(client);
                    }
                }
            }
            if (client.Entity.MapID == 1927 && Kernel.SpawnBanshee2)
            {
                foreach (INpc Npc in client.Map.Npcs.Values)
                {
                    if (Npc.MapID == 1927 && (Npc.UID == 2999) && Kernel.GetDistance(client.Entity.X, client.Entity.Y, Npc.X, Npc.Y) < 17)
                    {
                        Npc.SendSpawn(client);
                    }
                }
            }
            client.Entity.Action = Enums.ConquerAction.None;
            client.Mining = false;
            if (client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.CastPray))
            {
                client.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.CastPray);
                foreach (var Client in client.Prayers)
                {
                    if (Client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Praying))
                    {
                        Client.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.Praying);
                    }
                }
                client.Prayers.Clear();
            }
            if (client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Praying))
            {
                client.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.Praying);
                client.PrayLead = null;
            }
            Time32 Now = Time32.Now;
            client.Attackable = true;
            if (client.Entity.AttackPacket != null)
            {
                client.Entity.AttackPacket = null;
            }
            if (client.Entity.Dead)
            {
                if (Now > client.Entity.DeathStamp.AddSeconds(4))
                {
                    client.Disconnect();
                    return;
                }
            }
            if (client.Entity.MapID == 1038)
            {
                if (!(client.Entity.X < 224 && client.Entity.X > 3 && client.Entity.Y < 211 && client.Entity.Y > 3))
                {
                    if (new_X < 224 && new_Y < 211)
                    {
                        if (Kernel.GetDistance(client.Entity.X, client.Entity.Y, Game.GuildWar.LeftGate.X, Game.GuildWar.LeftGate.Y) < Kernel.GetDistance(client.Entity.X, client.Entity.Y, Game.GuildWar.RightGate.X, Game.GuildWar.RightGate.Y))
                        {
                            if (Game.GuildWar.LeftGate.Mesh == (ushort)(240 + Game.GuildWar.LeftGate.Mesh % 10))
                            {
                                client.Entity.Teleport(client.Entity.X, client.Entity.Y);
                                client.Send(new Message("Invalid jump!", Color.Red, Message.Talk));
                                return;
                            }
                        }
                        else
                        {
                            if (Game.GuildWar.RightGate.Mesh == (ushort)(270 + Game.GuildWar.RightGate.Mesh % 10))
                            {
                                client.Entity.Teleport(client.Entity.X, client.Entity.Y);
                                client.Send(new Message("Invalid jump!", Color.Red, Message.Talk));
                                return;
                            }
                        }
                    }
                }
            }
            if (client.lastJumpDistance == 0) goto Jump;
            if (client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Ride))
            {
                int distance = Kernel.GetDistance(new_X, new_Y, client.Entity.X, client.Entity.Y);
                ushort take = (ushort)(1.5F * (distance / 2));
                if (client.Vigor >= take)
                {
                    client.Vigor -= take;
                    Vigor vigor = new Vigor(true);
                    vigor.Amount = client.Vigor;
                    vigor.Send(client);
                }
                else
                {
                    //client.Player.Shift(client.Player.X, client.Player.Y);
                    //return;
                }
            }
            
            
            if (client.Entity.Dead)
            {
                if (Now > client.Entity.DeathStamp.AddSeconds(4))
                {
                    client.Disconnect();
                    return;
                }
            }

            if (client.lastJumpDistance == 0) goto Jump;
            if (client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Ride))
            {
                int distance = Kernel.GetDistance(new_X, new_Y, client.Entity.X, client.Entity.Y);
                ushort take = (ushort)(1.5F * (distance / 2));
                if (client.Vigor >= take)
                {
                    client.Vigor -= take;
                    Network.GamePackets.Vigor vigor = new Network.GamePackets.Vigor(true);
                    vigor.Amount = client.Vigor;
                    vigor.Send(client);
                }
                else
                {
                }
            }
            client.LastJumpTime = (int)Kernel.maxJumpTime(client.lastJumpDistance);
            int a1 = Now.GetHashCode() - client.lastJumpTime.GetHashCode();
            int a2 = generalData.TimeStamp.GetHashCode() - client.lastClientJumpTime.GetHashCode();
            bool DOO = false;
            if (a2 - a1 > 1000) DOO = true;
            #region AntiSpeed
            var serverstamp = Now.GetHashCode() - client.lastJumpTime.GetHashCode();
            var clientstamp = generalData.TimeStamp.GetHashCode() - client.lastClientJumpTime.GetHashCode();
            var speed = clientstamp - serverstamp;

            client.LastJumpTime = (int)Kernel.maxJumpTime(client.lastJumpDistance);
            if (speed > 120)
            {
                client.speedHackSuspiction++;
                if (!client.Entity.OnCyclone() &&
                    !client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Ride) &&
                    !client.Entity.OnOblivion() &&
                    !client.Entity.OnSuperman() &&
                    !client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.DragonCyclone) &&
                    !client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Omnipotence) &&
                    !client.Entity.OnSuperCyclone() &&
                    !client.Entity.Transformed &&
                    client.speedHackSuspiction >= 3)
                {
                    client.Disconnect();
                }
            }
            else
            {
                client.speedHackSuspiction = Math.Max(0, client.speedHackSuspiction - 1);
            }
            #endregion
            if (Now < client.lastJumpTime.AddMilliseconds(client.LastJumpTime))
            {
                bool doDisconnect = false;
                if (client.Entity.Transformed)
                    if (client.Entity.TransformationID != 207 && client.Entity.TransformationID != 267)
                        doDisconnect = true;
                if (client.Entity.Transformed && doDisconnect)
                {
                }
                if (client.Entity.Transformed && !doDisconnect)
                {
                    goto Jump;
                }
                if (!client.Entity.OnCyclone() && !client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Ride) && DOO)
                {
                }
                else if (client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Ride))
                {
                    int time = (int)Kernel.maxJumpTime(client.lastJumpDistance);
                    int speedprc = Database.DataHolder.SteedSpeed(client.Equipment.TryGetItem(ConquerItem.Steed).Plus);
                    if (speedprc != 0)
                    {
                        if (Now < client.lastJumpTime.AddMilliseconds(time - (time * speedprc / 100)))
                        {
                        }
                    }
                    else
                    {
                    }
                }
            }
        Jump:
            client.lastJumpDistance = Kernel.GetDistance(new_X, new_Y, client.Entity.X, client.Entity.Y);
            client.lastClientJumpTime = generalData.TimeStamp;
            client.lastJumpTime = Now;
            Game.Map Map = client.Map;
            if (Map != null)
            {
                if (Map.Floor[new_X, new_Y, Game.MapObjectType.Player, null])
                {
                    if (Kernel.GetDistance(new_X, new_Y, client.Entity.X, client.Entity.Y) <= 20)
                    {
                        client.Entity.Action = Game.Enums.ConquerAction.Jump;
                        client.Entity.Facing = Kernel.GetAngle(generalData.wParam1, generalData.wParam2, new_X, new_Y);
                        client.Entity.PX = client.Entity.X;
                        client.Entity.PY = client.Entity.Y;
                        client.Entity.X = new_X;
                        client.Entity.Y = new_Y;
                        Revora.Game.Features.TravelProgress.Arrived(client, Map, new_X, new_Y, Revora.Game.Features.TravelProgress.Arrival.Jump);
                        if (client.Entity.MapID == CaptureTheFlag.MapID)
                            CheckForFlag(client);
                        client.SendScreen(generalData, true);
                        client.Screen.Reload(generalData);
                        if (client.Entity.InteractionInProgress && client.Entity.InteractionSet)
                        {
                            if (client.Entity.Body == 1006 || client.Entity.Body == 2005)
                            {
                                if (Kernel.GamePool.ContainsKey(client.Entity.InteractionWith))
                                {
                                    Revora.Game.Attacking.Handle.MoveInteractionFollower(client,oldX,oldY,new_X,new_Y,true,GroundMovement.Walk,ch=> {
                                        Revora.Network.GamePackets.Data general = new Revora.Network.GamePackets.Data(true);
                                        general.UID = ch.Entity.UID;
                                        general.wParam1 = new_X;
                                        general.wParam2 = new_Y;
                                        general.ID = 0x9c;
                                        ch.Send(general.ToArray());
                                        ch.SendScreen(generalData, true);
                                        ch.Screen.Reload(general);
                                        client.SendScreen(generalData, true);
                                        client.Screen.Reload(general);
                                    });
                                }
                            }
                        }
                    }
                    else
                    {
                        client.Disconnect();
                    }
                }
                else
                {
                    if (client.Entity.Mode == Game.Enums.Mode.None)
                    {
                        client.Entity.Teleport(client.Map.ID, client.Entity.X, client.Entity.Y);
                    }
                }
            }
            else
            {
                if (Kernel.GetDistance(new_X, new_Y, client.Entity.X, client.Entity.Y) <= 20)
                {
                    client.Entity.Action = Game.Enums.ConquerAction.Jump;
                    client.Entity.Facing = Kernel.GetAngle(generalData.wParam1, generalData.wParam2, new_X, new_Y);
                    client.Entity.X = new_X;
                    client.Entity.Y = new_Y;
                    client.SendScreen(generalData, true);
                    client.Screen.Reload(generalData);
                }
                else
                {
                    client.Disconnect();
                }
            }
            if (client.Map.BaseID == 1038 && Game.GuildWar.IsWar || client.Entity.MapID == 1234 && Game.SuperGuildWar.IsWar)
            {
                Game.Calculations.IsBreaking(client, oldX, oldY);
            }

            Revora.Game.Features.MagicDefenderProtection.Refresh(client,Time32.Now);
            #region Shadow Clones
            foreach (var item in client.Entity.MyClones.Values)
            {
                item.X = client.Entity.X;
                item.Y = client.Entity.Y;
            }
            #endregion
        }
        private static void CheckForFlag(GameState client)
        {
            Server.Thread.CTF.Move(client);
        }
        public static void PlayerGroundMovment(GroundMovement groundMovement, GameState client)
        {
            if (groundMovement.GroundMovementType != GroundMovement.Walk && WarriorStance.BlockMovement(client)) return;
            if (ExorcismCombat.IsStunned(client) || SnowBansheeCombat.IsStunned(client) || PurpleBansheeCombat.IsStunned(client)) return;
            client.Entity.SpellStamp = Time32.Now.AddSeconds(-1);
            client.Entity.KillCount2 = 0;
            client.Entity.Action = Game.Enums.ConquerAction.None;
            client.Attackable = true;
            client.Mining = false;
            var oldX = client.Entity.X;
            var oldY = client.Entity.Y;
            if (client.Entity.OnCheckGuard)
            {
                client.Entity.OnCheckGuard = false;
                client.MessageBox("Unable to inspect!", null, null, 0);
                DialyQuestsEffect.CheckCancelGuard(client);
                client.Entity.CheckGuardSec = 0;
            }
            if (client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.CastPray))
            {
                client.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.CastPray);
                foreach (var Client in client.Prayers)
                {
                    if (Client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Praying))
                    {
                        Client.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.Praying);
                    }
                }
                client.Prayers.Clear();
            }
            if (client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Praying))
            {
                client.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.Praying);
                if (client.PrayLead != null)
                    client.PrayLead.Prayers.Remove(client);
                client.PrayLead = null;
            }
            if (client.Entity.AttackPacket != null)
            {
                client.Entity.AttackPacket = null;
            }
            if (client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Ride))
                client.Vigor -= 1;
            client.Entity.PX = client.Entity.X;
            client.Entity.PY = client.Entity.Y;
            if (!client.Entity.Move(groundMovement.Direction, groundMovement.GroundMovementType == GroundMovement.Slide)) return;
            if (client.Entity.MapID == Game.SteedRace.MAPID)
            {
                if (!Server.Thread.SteedRace.IsOn)
                {
                    if (client.Entity.X <= Server.Thread.SteedRace.GateX + 1)
                    {
                        client.Entity.Teleport(client.Entity.PX, client.Entity.PY);
                        return;
                    }
                }
            }
            if (client.Entity.MapID == Game.SteedRace.MAPID)
                CheckForRaceItems(client);
            if (client.Entity.MapID == CaptureTheFlag.MapID)
                CheckForFlag(client);
            client.SendScreen(groundMovement, true);
            client.Screen.Reload(groundMovement);
            if (client.Entity.InteractionInProgress)
            {
                if (!client.Entity.InteractionSet)
                {
                    if (Kernel.GamePool.ContainsKey(client.Entity.InteractionWith))
                    {
                        GameState ch = Kernel.GamePool[client.Entity.InteractionWith];
                        if (Revora.Game.Attacking.Handle.HasAcceptedInteraction(client,ch) && ch.Entity.InteractionInProgress && ch.Entity.InteractionWith == client.Entity.UID)
                        {
                            if (client.Entity.InteractionX == client.Entity.X && client.Entity.Y == client.Entity.InteractionY)
                            {
                                if (client.Entity.X == ch.Entity.X && client.Entity.Y == ch.Entity.Y)
                                {
                                    Network.GamePackets.Attack atac = new Network.GamePackets.Attack(true);
                                    atac.Attacker = ch.Entity.UID;
                                    atac.Attacked = client.Entity.UID;
                                    atac.X = ch.Entity.X;
                                    atac.Y = ch.Entity.Y;
                                    atac.Damage = client.Entity.InteractionType;
                                    atac.ResponseDamage = client.InteractionEffect;
                                    atac.AttackType = 47;
                                    ch.Send(atac);
                                    atac.AttackType = 49;
                                    atac.Attacker = client.Entity.UID;
                                    atac.Attacked = ch.Entity.UID;
                                    client.SendScreen(atac, true);
                                    atac.Attacker = ch.Entity.UID;
                                    atac.Attacked = client.Entity.UID;
                                    client.SendScreen(atac, true);
                                }
                            }
                        }
                    }
                }
                else
                {
                    if (client.Entity.Body == 1006 || client.Entity.Body == 2005 || Revora.Game.Attacking.Handle.CanLeadHandHolding(client))
                    {
                        if (Kernel.GamePool.ContainsKey(client.Entity.InteractionWith))
                        {
                            Revora.Game.Attacking.Handle.MoveInteractionFollower(client,oldX,oldY,client.Entity.X,client.Entity.Y,false,groundMovement.GroundMovementType,ch=> {
                                Network.GamePackets.Data general = new Network.GamePackets.Data(true);
                                general.UID = ch.Entity.UID;
                                general.wParam1 = ch.Entity.X;
                                general.wParam2 = ch.Entity.Y;
                                general.ID = 156;
                                ch.Send(general.ToArray());
                                ch.Screen.Reload(null);
                            });
                        }
                    }
                }
            }
            if (client.Map.BaseID == 1038 && Game.GuildWar.IsWar || client.Map.BaseID == 1234 && Game.SuperGuildWar.IsWar)
            {
                Game.Calculations.IsBreaking(client, oldX, oldY);
            }


            Revora.Game.Features.MagicDefenderProtection.Refresh(client,Time32.Now);
        }
        public static void GetSurroundings(GameState client)
        {
            client.Screen.FullWipe();
            client.Screen.Reload(null);
            if (client.Entity.PreviousMapID == CaptureTheFlag.MapID)
                Server.Thread.CTF.Leave(client);
        }
        public static void ChangeAction(Data generalData, GameState client)
        {
            if (Game.Features.ClassicDances.TryHandle(client, generalData)) return;
            client.Entity.Action = (ushort)generalData.dwParam;
            if (client.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.CastPray))
            {
                foreach (var Client in client.Prayers)
                {
                    generalData.UID = Client.Entity.UID;
                    generalData.dwParam = client.Entity.Action;
                    generalData.wParam1 = Client.Entity.X;
                    generalData.wParam2 = Client.Entity.Y;
                    Client.Entity.Action = client.Entity.Action;
                    if (Time32.Now >= Client.CoolStamp.AddMilliseconds(1500))
                    {
                        if (Client.Equipment.IsAllSuper())
                            generalData.dwParam = (uint)(generalData.dwParam | (uint)(Client.Entity.Class * 0x10000 + 0x1000000));
                        else if (Client.Equipment.IsArmorSuper())
                            generalData.dwParam = (uint)(generalData.dwParam | (uint)(Client.Entity.Class * 0x10000));
                        Client.SendScreen(generalData, true);
                        Client.CoolStamp = Time32.Now;
                    }
                    else
                        Client.SendScreen(generalData, false);
                }
            }
            generalData.UID = client.Entity.UID;
            generalData.dwParam = (uint)client.Entity.Action;
            if (client.Entity.Action == Game.Enums.ConquerAction.Cool)
            {
                if (Time32.Now >= client.CoolStamp.AddMilliseconds(1500))
                {
                    if (client.Equipment.IsAllSuper())
                        generalData.dwParam = (uint)(generalData.dwParam | (uint)(client.Entity.Class * 0x10000 + 0x1000000));
                    else if (client.Equipment.IsArmorSuper())
                        generalData.dwParam = (uint)(generalData.dwParam | (uint)(client.Entity.Class * 0x10000));
                    client.SendScreen(generalData, true);
                    client.CoolStamp = Time32.Now;
                }
                else
                    client.SendScreen(generalData, false);
            }
            else
                client.SendScreen(generalData, false);
        }
        public static void ChangeDirection(Data generalData, GameState client)
        {
            client.Entity.Facing = (Game.Enums.ConquerAngle)generalData.Facing;
            client.SendScreen(generalData, false);
        }
        public static void ChangePKMode(Data generalData, GameState client)
        {
            if (client.Entity.PKMode == Enums.PKMode.Jiang)
            {
                if ((Enums.PKMode)(byte)generalData.dwParam != Enums.PKMode.Jiang)
                {
                    client.Send(new Message("Your JiangHu will deactivate in 10 minutes.", Message.System));
                }
            }
            if (client.InTeamQualifier()) return;
            client.Entity.AttackPacket = null;
            client.Entity.PKMode = (Enums.PKMode)(byte)generalData.dwParam;
            generalData.dwParamPot = generalData.dwParam;
            generalData.dwParam = 0;
            client.Send(generalData);
            if ((client.Entity.PKMode == Game.Enums.PKMode.Jiang) && (client.Entity.MyJiang != null))
            {
                client.Entity.MyJiang.OnJiangHu = true;
                client.Entity.MyJiang.SendStatusMode(client);
            }
            if (client.Entity.PKMode == Enums.PKMode.PK)
            {
                client.Send(new Message("PK Capture mode: you can attack monsters and all players.", Message.System));
            }
            else if (client.Entity.PKMode == Enums.PKMode.Peace)
            {
                client.Send(new Message("Peace Mode: You can only attack monsters.", Message.System));
            }
            else if (client.Entity.PKMode == Enums.PKMode.Team)
            {
                client.Send(new Message("Team Mode: kill monsters, and all other players (including players between servers) not in your current team or clan.", Message.System));
            }
            else if (client.Entity.PKMode == Enums.PKMode.Union)
            {
                client.Send("You can only attack the other Unions.");
            }
            else if (client.Entity.PKMode == Enums.PKMode.Capture)
            {
                client.Send(new Message("Capture Mode: Kill monsters, black / blue-name criminals, and players between servers.", Message.System));
            }
            else if (client.Entity.PKMode == Enums.PKMode.Revenge)
            {
                client.Send(new Message("Revenge Mode: kill your listed enemies, monsters, and players between servers.", Message.System));
            }
            else if (client.Entity.PKMode == Enums.PKMode.Guild)
            {
                client.Send(new Message("Clan Mode: Kill monsters and players in their enemy guilds, and players between servers.", Message.System));
            }
            else if (client.Entity.PKMode == Enums.PKMode.Jiang)
            {
                client.Send(new Message("Jiang Hu mode: Kill fighters Jiang Hu, black / blue-name criminals, and player between servers.", Message.System));
            }
            else if (client.Entity.PKMode == Enums.PKMode.CS)
            {
                client.Send(new Message("CS Mode: Attack players between servers. There is no penalty Pk.", Message.System));
            }
            else if (client.Entity.PKMode == Enums.PKMode.Invade)
            {
                client.Send(new Message("Invade mode: Only attack players of the server target (current) No Pk punishment.", Message.System));
            }
        }

        public static void SetLocation(Data generalData, GameState client)
        {
            try
            {
            if (client.Entity.MyJiang != null)
            {
                client.Entity.MyJiang.OnloginClient(client);
            }
            else if (client.Entity.Reborn == 2)
            {
                Network.GamePackets.JiangHu hu = new Network.GamePackets.JiangHu
                {
                    Texts = { "0" }
                };
                hu.CreateArray();
                hu.Send(client);
            }
            if (client.Entity.MyFlowers != null)
            {
            SendFlower sendFlower = new SendFlower();
            sendFlower.Typing = (Flowers.IsBoy((uint)client.Entity.Body) ? 3u : 2u);
            sendFlower.Apprend(client.Entity.MyFlowers);
            client.Send(sendFlower.ToArray());
            if (client.Entity.MyFlowers.aFlower > 0u)
            {
                client.Send(new SendFlower
                {
                    Typing = Flowers.IsBoy((uint)client.Entity.Body) ? 2u : 3u
                }.ToArray());
            }
            }
            if (client.Guild != null)
            {
                client.Guild.SendGuild(client);
                GuildMinDonations guild = new GuildMinDonations(31);
                guild.AprendGuild(client.Guild);
                client.Send(guild.ToArray());
            }
            Clan clan = client.Entity.GetClan;
            if (clan != null)
            {
                clan.Build(client, Clan.Types.Info);
                client.Send(clan);
                client.Entity.ClanName = clan.Name;
                client.Send(new ClanRelations(clan, ClanRelations.RelationTypes.Allies));
                client.Send(new ClanRelations(clan, ClanRelations.RelationTypes.Enemies));
            }

            foreach (Game.ConquerStructures.Society.Guild guild in Kernel.Guilds.Values)
            {
                guild.SendName(client);
                guild.SendName(client);
            }

            if (client.Entity.EnlightmentTime > 0)
            {
                Enlight enlight = new Enlight(true);
                enlight.Enlighted = client.Entity.UID;
                enlight.Enlighter = 0;

                // Restore the display without changing the durable remaining minutes.
                for (int count = 0; count < client.Entity.EnlightmentTime; count += 20)
                {
                    client.Send(enlight);
                }
            }

            if (client.Entity.Hitpoints != 0)
            {
                if (client.Map.ID == 1036 || client.Map.ID == 1039)
                {
                    if (client.Entity.PreviousMapID == 0)
                        client.Entity.SetLocation(1002, 408, 330);
                    else
                    {
                        switch (client.Entity.PreviousMapID)
                        {
                            default:
                                {
                                    client.Entity.SetLocation(1002, 408, 330);
                                    break;
                                }
                            case 1000:
                                {
                                    client.Entity.SetLocation(1000, 500, 650);
                                    break;
                                }
                            case 1020:
                                {
                                    client.Entity.SetLocation(1020, 565, 562);
                                    break;
                                }
                            case 1011:
                                {
                                    client.Entity.SetLocation(1011, 188, 264);
                                    break;
                                }
                            case 1015:
                                {
                                    client.Entity.SetLocation(1015, 717, 571);
                                    break;
                                }
                        }
                    }
                }
            }
            else
            {
                if (client.Entity.MapID == 1038 && Game.GuildWar.IsWar || client.Entity.MapID == 1234 && Game.SuperGuildWar.IsWar)
                {
                    client.Entity.SetLocation(1002, 227, 236);
                }
                else
                {
                    ushort[] Point = Database.DataHolder.FindReviveSpot(client.Map.ID);
                    client.Entity.SetLocation(Point[0], Point[1], Point[2]);
                }
            }
            }
            catch (Exception e)
            {
                Server.SaveException(e);
            }
            if (client.Map == null)
            {
                client.Entity.MapID = 1002;
                client.Entity.X = 300;
                client.Entity.Y = 278;
            }
            generalData.ID = Data.SetLocation;
            generalData.UID = client.Entity.UID;
            generalData.dwParam = client.Map != null ? client.Map.BaseID : (ushort)1002;
            generalData.wParam1 = client.Entity.X;
            generalData.wParam2 = client.Entity.Y;
            client.Send(generalData);
        }
        public static object LoginSyncRoot = new object();
        public static void AppendConnect(Connect appendConnect, GameState client)
        {
            if (client.LoggedIn || client.Account != null)
                return;
            bool doLogin = false;
            lock (LoginSyncRoot)
            {
                Database.AccountTable Account = null;
                if (Kernel.AwaitingPool.TryGetValue(appendConnect.Identifier, out Account))
                {
                    if (!Account.MatchKey(appendConnect.Identifier))
                    {
                        client.Disconnect(false);
                        return;
                    }

                    // Consume under LoginSyncRoot before every successful branch,
                    // including the account that has not created a character yet.
                    Kernel.AwaitingPool.Remove(appendConnect.Identifier);
                    client.Account = Account;

                    if (Account.EntityID == 0)
                    {
                        client.Socket.OverrideTiming = true;
                        byte[] newRole = GamePackets.Message.EncodeNewRole();
                        Console.WriteLine("[game] NEW_ROLE " + Account.Username
                            + " bytes=" + newRole.Length
                            + " declared=" + BitConverter.ToUInt16(newRole, 0));
                        client.Send(newRole);
                        return;
                    }
                    if (Kernel.DisconnectPool.ContainsKey(Account.EntityID))
                    {
                        client.Send(new Message("Please try again after a minute!", "ALLUSERS", System.Drawing.Color.Orange, GamePackets.Message.Dialog));
                        return;
                    }
                    VariableVault variables;
                    Database.EntityVariableTable.Load(client.Account.EntityID, out variables);
                    client.Variables = variables;

                    if (client["banhours"] == 0)
                    {
                        client["banhours"] = -1;
                        client["banreason"] = "Infinite time.";
                        client["banstamp"] = DateTime.Now.AddYears(100);
                    }
                    if (client.Account.State == Database.AccountTable.AccountState.Banned)
                        client.Send(new Message("Sorry, you can't login this account has been banned.", System.Drawing.Color.Orange, GamePackets.Message.Dialog));
                    string Message = "";
                    if (Account.State == Database.AccountTable.AccountState.Banned)
                    {
                        DateTime banStamp = client["banstamp"];
                        banStamp = banStamp.AddHours(client["banhours"]);
                        Message = "You are banned for " + client["banhours"] + " hours [until " + banStamp.ToString("HH:mm MM/dd/yyyy") + "]. Reason: " + client["banreason"];
                    }
                    else if (Account.State == Database.AccountTable.AccountState.NotActivated)
                        Message = "You cannot login until your account is activated.";
                    if (Message == string.Empty) // ANSWER_OK
                    {
                        SessionInstall.Replace(Account.EntityID, client, delegate
                        {
                            GameState previous;
                            if (Kernel.GamePool.TryGetValue(Account.EntityID, out previous))
                                SessionInstall.TakeoverPrevious(previous, client);
                            client.Entity = new Entity(EntityFlag.Monster, false);
                        });
                        doLogin = true;
                    }
                    else
                    {
                        client.Send(new Message(Message, "ALLUSERS", Color.Orange, GamePackets.Message.Dialog));
                        Thread.Execute<GameState>((pClient, time) => { pClient.Disconnect(); }, client, 100);
                    }
                }
                else
                {
                    client.Disconnect(false);
                    return;
                }
            }
            if (doLogin)
            {
                DoLogin(client);
            }
        }
        public static ConcurrentDictionary<string, byte[]> TreasurePointsAllowance = new ConcurrentDictionary<string, byte[]>();
        public static object TPASyncRoot = new object();
        public static void AddTPA(GameState client)
        {
            if (!TreasurePointsAllowance.ContainsKey(client.Socket.IP))
                TreasurePointsAllowance.Add(client.Socket.IP, new byte[3]);
            lock (TPASyncRoot)
            {
                byte[] data = TreasurePointsAllowance[client.Socket.IP];
                for (int i = 0; i < data.Length; i++)
                {
                    if (data[i] == 0)
                    {
                        client.AllowedTreasurePoints = true;
                        client.AllowedTreasurePointsIndex = i;
                        data[i] = 1;
                    }
                }
            }
        }
        public static void RemoveTPA(GameState client)
        {
            if (client != null && client.AllowedTreasurePoints && client.Socket != null && client.Socket.IP != null)
            {
                lock (TPASyncRoot)
                {
                    byte[] data = TreasurePointsAllowance[client.Socket.IP];
                    data[client.AllowedTreasurePointsIndex] = 0;
                }
            }
        }
        public static void DoLogin(GameState client)
        {
            uint loginUid = client != null && client.Account != null ? client.Account.EntityID : 0;
            bool loginHeld = false;
            try
            {
            if (!SessionInstall.EnterCurrent(loginUid, client))
            {
                if (client != null) client.Disconnect(false);
                return;
            }
            loginHeld = true;
            client.ReadyToPlay();
            bool loginLoaded = SessionInstall.LoadEntityHook != null
                ? SessionInstall.LoadEntityHook(client)
                : Database.EntityTable.LoadEntity(client);
            if (loginLoaded)
            {
                if (client.Entity != null && client.Entity.FullyLoaded)
                {
                    foreach (ConquerItem item in client.Inventory.Objects)
                    {
                        if ((item.ID == 1100003) || (item.ID == 1100006) || (item.ID == 1100009))
                        {
                            if (!client.Warehouses.ContainsKey((Game.ConquerStructures.Warehouse.WarehouseID)item.UID))
                            {
                                client.Warehouses.Add((Game.ConquerStructures.Warehouse.WarehouseID)
                                    item.UID, new Game.ConquerStructures.Warehouse(client,
                                        (Game.ConquerStructures.Warehouse.WarehouseID)item.UID));
                            }
                        }
                    }
                    AddTPA(client);
                    client.LoadData();
                    if (client.Entity.GuildID != 0)
                    {
                        client.Entity.GuildSharedBp = client.Guild.Arsenal.BattlePower;
                    }
                    client.ReviewMentor();
                    #region Game Updates
                    if (client.Entity.UpdateList == 0)
                    {
                        client.Send(new GameUpdates(GameUpdates.Header, "The gates of Revora stand open. " + DateTime.Now.ToString()));
                        client.Send(new GameUpdates(GameUpdates.Body, "Stand a minute in Twin City and the treasury pays Conquer Points."));
                        client.Send(new GameUpdates(GameUpdates.Body, "Old Dragon Country waits beyond the 3D expansion."));
                        client.Send(new GameUpdates(GameUpdates.Body, "VIP hunters take 500 Conquer Points from each beast they fell."));
                        client.Send(new GameUpdates(GameUpdates.Footer, "If the world breaks, tell a Game Master in Twin City."));
                        client.Entity.UpdateList += 1;
                    }
                    if (client.Entity.UpdateList == 1)
                    {
                        client.Send(new GameUpdates(GameUpdates.Header, "The gates of Revora stand open. " + DateTime.Now.ToString()));
                        client.Send(new GameUpdates(GameUpdates.Body, "The hunting grounds have been restocked. Common beasts pay more gold than Conquer Points."));
                        client.Send(new GameUpdates(GameUpdates.Body, "The Super Bosses still hoard Revora Points and armor fragments."));
                        client.Send(new GameUpdates(GameUpdates.Body, "Stand in Twin City a minute and the treasury pays Conquer Points."));
                        client.Send(new GameUpdates(GameUpdates.Body, "Old Dragon Country waits beyond the 3D expansion."));
                        client.Send(new GameUpdates(GameUpdates.Body, "VIP hunters take 500 Conquer Points from each beast they fell."));
                        client.Send(new GameUpdates(GameUpdates.Footer, "If the world breaks, tell a Game Master in Twin City."));
                        client.Entity.UpdateList += 1;
                    }
                    #endregion
                    if ((client.JustCreated || Database.CharacterCreation.StarterOwed(client))
                        && Database.CharacterCreation.RetireLegacyStarterPacks(client))
                    {
                        Database.CharacterCreation.TryIssueStarterGear(client);
                        client.JustCreated = false;
                        if (client.Entity != null)
                            client.Entity.JustCreated = false;
                        SetLocation(new Data(true) { UID = client.Entity.UID }, client);
                    }
                }
                else
                {
                    client.Disconnect(false);
                    return;
                }
            }
            else
            {
                client.Send(new Message("Can't find your player. ", " ALL USERS", System.Drawing.Color.Orange, GamePackets.Message.Dialog));
                client.Disconnect(false);
                return;
            }
            if (!SessionInstall.IsCurrent(loginUid, client))
            {
                client.Disconnect(false);
                return;
            }
            Action<GameState> beforeRelease = SessionInstall.BeforeLoginRelease;
            if (beforeRelease != null) beforeRelease(client);
            SessionInstall.Exit(loginUid);
            loginHeld = false;
            Action<GameState> afterEarly = SessionInstall.AfterEarlyLogin;
            if (afterEarly != null) afterEarly(client);
            if (!SessionInstall.EnterCurrent(loginUid, client))
            {
                client.Disconnect(false);
                return;
            }
            loginHeld = true;
            Action<GameState> beforeLate = SessionInstall.BeforeLateLogin;
            if (beforeLate != null) beforeLate(client);
            if (!SessionInstall.IsCurrent(loginUid, client))
            {
                client.Disconnect(false);
                return;
            }
            if (Kernel.GamePool.Count >= Server.PlayerCap)
            {
                client.Send(new Message("Player Limit Exceeded. (Connected players: " + Kernel.GamePool + "/" + Server.PlayerCap + ")", "ALLUSERS", System.Drawing.Color.Orange, GamePackets.Message.Dialog));
                client.Disconnect(false);
                return;
            }
            try { Database.AccountActivity.Open(client); }
            catch (Exception ex)
            {
                Console.WriteLine("[account-presence] open error=" + ex.GetType().Name);
                client.Disconnect(false);
                return;
            }
            client.Send(new Message("ANSWER_OK", "ALLUSERS", System.Drawing.Color.Orange, GamePackets.Message.Dialog));
            if (!SessionInstall.IsCurrent(loginUid, client))
            {
                client.Disconnect(false);
                return;
            }
            if (Server.Thread != null) Server.Thread.Register(client);
            Database.KnownPersons.LoaderFriends(client);
            Database.KnownPersons.LoadPartner(client);
            Database.KnownPersons.LoadMentor(client);

            Database.EntityTable.UpdateOnlineStatus(client, true);
            CharacterInfo Info = new CharacterInfo(client);
            client.Send(Info.ToArray());
            string IP = client.IP;
            client.Account.SetCurrentIP(IP);
            if (!client.LoggedIn)
                Console.WriteLine("(" + client.Entity.Name + ") Has Login Under:[" + client.Account.IP + "]", ConsoleColor.Green);

            #region Entrada [GM] , [PREMIUM] & [Usuarios]

            #endregion

            client.LoggedIn = true;
            client.Action = 2;
            if (!Revora.Game.Features.Tournaments.LocalTeamPkAdmissions.Return(client, false)) { client.Disconnect(false); return; }
            Revora.Game.Features.RevoraStarterPack.OnLogin(client);
            if (client.Entity.PersistenceRequiresReload) return;
            Revora.Game.Features.LastManStandingSystem.OnLogin(client);
            if (client.Entity.PersistenceRequiresReload) return;
            Revora.Game.Features.NobleWarSystem.OnLogin(client);
            if (client.Entity.PersistenceRequiresReload) return;
            if (!Revora.Game.Features.CtfResults.Return(client, true)) { client.Disconnect(false); return; }
            Revora.Game.Features.DuelPitSystem.OnLogin(client);
            if (client.Entity.PersistenceRequiresReload) return;
            Revora.Game.Features.RelicRushSystem.OnLogin(client);
            if (client.Entity.PersistenceRequiresReload) return;
            Revora.Game.Features.WaveDefenseSystem.OnLogin(client);
            if (client.Entity.PersistenceRequiresReload) return;
            Revora.Game.Features.BeastTrialSystem.OnLogin(client);
            if (client.Entity.PersistenceRequiresReload) return;
            Revora.Game.Features.TravelProgress.Arrived(client, client.Map, client.Entity.X, client.Entity.Y, Revora.Game.Features.TravelProgress.Arrival.Login);
            Revora.Game.Features.AdventureTravel.RecoverAchievement(client);
            Revora.Game.Features.MentorDepartures.ClaimFees(client);
            Revora.Game.Features.DemonExterminators.OnLogin(client);
            Revora.Game.Features.MoonBoxQuest.OnLogin(client);
            Revora.Game.Features.PowerArenaEvent.OnLogin(client);
            Revora.Game.Features.ReleaseSoulsQuest.OnLogin(client);
            Revora.Game.Features.DailyPriceQuest.Refresh(client);
            Revora.Game.Features.DailyRareQuest.Refresh(client);
            Revora.Game.Features.DailySoulBall.Refresh(client);
            AnniversaryQuest.Refresh(client);
            Revora.Game.Features.ExorcismQuest.Refresh(client);
            Revora.Game.Features.SummerHunt.OnLogin(client);
            Revora.Game.Features.PlanetEncounter.OnLogin(client);
            Revora.Game.Features.HeartEncounter.OnLogin(client);
            if (client.Entity.PersistenceRequiresReload) return;
            Revora.Game.Features.HeartCampaign.RecoverPrizes(client);
            if (client.Entity.PersistenceRequiresReload) return;
            Revora.Game.Features.PlanetWeekend.OnLogin(client);
            Revora.Game.Features.SkyPassQuest.OnLogin(client);
            Revora.Game.Features.SnakeIslandsQuest.OnLogin(client);
            Revora.Game.Features.LabyrinthQuest.OnLogin(client);
            Revora.Game.Features.DisCityQuest.OnLogin(client);
            Revora.Game.Features.NinjaEpicQuest.OnLogin(client);
            Revora.Game.Features.AbyssTrialQuest.OnLogin(client);
            Revora.Game.Features.PowerBookQuest.OnLogin(client);
            Revora.Game.Features.FranticMonkeysQuest.OnLogin(client);
            Revora.Game.Features.SoulGeneralQuest.OnLogin(client);
            Revora.Game.Features.MadManQuest.OnLogin(client);
            Revora.Game.Features.GhostCityQuest.OnLogin(client);
            Revora.Game.Features.TomboyQuest.OnLogin(client);
            Revora.Game.Features.DesertTreasureQuest.OnLogin(client);
            Revora.Game.Features.DesertExpeditionQuest.OnLogin(client);
            Revora.Game.Features.DefenseTalismanQuest.OnLogin(client);
            Revora.Game.Features.AttackTalismanQuest.OnLogin(client);
            Revora.Game.Features.TempestWingsQuest.OnLogin(client);
            Revora.Game.Features.FamilyFeudQuest.OnLogin(client);
            Revora.Game.Features.HeroWingsQuest.OnLogin(client);
            Revora.Game.Features.DesertBloodQuest.OnLogin(client);
            Revora.Game.Features.SpunGoldQuest.OnLogin(client);
            Revora.Game.Features.LocalPkWar.OnLogin(client);
            Revora.Game.Features.CouplePkEvent.OnLogin(client);
            client.LoginAt = DateTime.Now;
            client.Entity.MyAchievement.Send();
            if (!SessionInstall.IsCurrent(loginUid, client))
            {
                client.Disconnect(false);
                return;
            }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[session] login failed uid=" + loginUid + " type=" + ex.GetType().Name);
                if (client != null)
                {
                    try { client.Disconnect(false); }
                    catch (Exception cleanup)
                    {
                        Console.WriteLine("[session] login cleanup failed uid=" + loginUid + " type=" + cleanup.GetType().Name);
                    }
                    finally { SessionInstall.Retire(client); }
                }
                throw;
            }
            finally
            {
                if (loginHeld) SessionInstall.Exit(loginUid);
            }
        }
        public static void RemoveBadSkills(GameState client)
        {
            if (client.Spells.ContainsKey(10405))
                if (!(client.Entity.FirstRebornClass / 10 == client.Entity.SecondRebornClass / 10 && client.Entity.SecondRebornClass / 10 == client.Entity.Class / 10 && client.Entity.Class / 10 == 6))
                    client.RemoveSpell(new Spell(true) { ID = 10405 });
            if (!client.Spells.ContainsKey(10405))
                if (client.Entity.FirstRebornClass / 10 == client.Entity.SecondRebornClass / 10 && client.Entity.SecondRebornClass / 10 == client.Entity.Class / 10 && client.Entity.Class / 10 == 6)
                    client.AddSpell(new Spell(true) { ID = 10405 });

            if (client.Spells.ContainsKey(6002))
                if (!(client.Entity.FirstRebornClass / 10 == client.Entity.SecondRebornClass / 10 && client.Entity.SecondRebornClass / 10 == client.Entity.Class / 10 && client.Entity.Class / 10 == 5))
                    client.RemoveSpell(new Spell(true) { ID = 6002 });
            if (!client.Spells.ContainsKey(6002))
                if (client.Entity.FirstRebornClass / 10 == client.Entity.SecondRebornClass / 10 && client.Entity.SecondRebornClass / 10 == client.Entity.Class / 10 && client.Entity.Class / 10 == 5)
                    client.AddSpell(new Spell(true) { ID = 6002 });
            foreach (var spell in client.Spells.Values.ToArray())
                if (RunesSystem.RunesSpells.Contains(spell.ID))
                    client.RemoveSpell(spell.ID);
        }
        public static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1);
        //   private static Time32 Now;
        public static uint UnixTimestamp
        {
            get { return (uint)(DateTime.UtcNow - UnixEpoch).TotalSeconds; }
        }
        public static void SubclassAutoSwitch(GameState client, byte To)
        {
            foreach (Game.SubClass clases in client.Entity.SubClasses.Classes.Values)
            {
                if (client.Entity.SubClasses.Classes.ContainsKey(To) && To > 0)
                {
                    if (client.Entity.SubClass != 0)
                    {
                        Game.SubClass subclass = client.Entity.SubClasses.Classes[client.Entity.SubClass];
                        byte[] Packet = null;
                        Packet = new byte[0];
                        Packet = new SubClassShowFull(0) { ID = 1, Class = subclass.ID, Level = client.Entity.SubClasses.Classes[subclass.ID].Phase }.ToArray();
                        client.Send(Packet);
                        client.Entity.SubClass = subclass.ID;
                        client.Entity.SubClassLevel = client.Entity.SubClasses.Classes[subclass.ID].Level;
                        client.Entity.SubClasses.Active = subclass.ID;
                        client.Entity.SubClassesActive = subclass.ID;
                    }
                }
            }
        }
        public static void SubclassSendInfo(GameState client)
        {
            Revora.Game.Features.SubclassProgression.RestorePerformer(client);
            byte[] Packet = null;
            Game.Entity Owner = client.Entity;
            if (Owner.SubClasses.Classes.Count > 0)
            {
                Game.SubClass[] Classes = new Game.SubClass[Owner.SubClasses.Classes.Count];
                Owner.SubClasses.Classes.Values.CopyTo(Classes, 0);
                foreach (Game.SubClass Class in Classes)
                {
                    if (Class.ID == 9)
                    {
                        for (byte i = 0; i < Class.Phase; i++)
                        {
                            Packet = new byte[0];
                            Packet = new SubClassShowFull(0) { ID = 4, Class = Class.ID, Level = Class.Level }.ToArray();
                            client.Send(Packet);
                        }
                        continue;
                    }
                    Packet = new byte[0];
                    Packet = new SubClassShowFull(0) { ID = 5, Class = Class.ID, Level = Class.Level }.ToArray();
                    client.Send(Packet);
                }
            }
            Packet = new Network.GamePackets.SendSubClass(client.Entity).ToArray();
            client.Send(Packet);
        }
        public static void LoginMessages(Client.GameState client)
        {
            //#region CrossServer
            //if (client.Cross == null)
            //{
            //    client.Cross = new CrossClient(client);
            //    if (client.Cross != null)
            //    {
            //        client.Cross.ConnectTo(Server.CrossIP, Server.CrossPort);
            //    }
            //}
            //client.Send(MyGroupServers.SendPacket());
            //#endregion
            #region Maestro del aire
            if (client.Entity.Windwalker == 0 && client.Entity.Class >= 160)
            {
                client.Entity.Windwalker += 7;
            }
            #endregion
            
            #region Throne C#
            MsgPCServerConfig mpccc = new MsgPCServerConfig(true) { Type = 15 };
            client.Send(mpccc);
            #endregion
            #region Sistema Union
            client.UnionExploits = client.UnionExploits;
            if (Kernel.Unions.Count > 0)
            {
                Revora.Network.GamePackets.Union.Union.MsgLeagueOpt Res = new Revora.Network.GamePackets.Union.Union.MsgLeagueOpt(true);
                Res.Action = Revora.Network.GamePackets.Union.Union.Actions.KingdomIcon;
                if (client.Union != null)
                {
                    Res.dwParam2 = client.Union.ID;
                }
                Res.dwParam3 = Kernel.ServerKingdom;
                client.Send(Res.ToArray());
            }
            if (client.Union != null)
            {
                client.Send(client.Union.SendOverheadLeagueInfo(client));
            }
            if (client.Union != null)
            {
                if (client.Union.IsKingdom())
                {
                    if (client.Union.IsLeader(client.Entity.UID))
                    {
                        Revora.Network.GamePackets.Union.Union.MsgLeagueOpt Res = new Revora.Network.GamePackets.Union.Union.MsgLeagueOpt(true);
                        Res.Action = Revora.Network.GamePackets.Union.Union.Actions.KingdomTitleGui;
                        Res.dwParam3 = Kernel.ServerKingdom;
                        client.Send(Res.ToArray());
                    }
                }
            }
            #endregion
            #region Sistema Perfection
            client.Send(new Revora.Network.GamePackets.Perfection.CMsgUserTotalRefineLevel(client));
            CMsgItemRefineOpt.SendMyPrefictionLevel(client);
            PrestigeRanking.SendSelf(client);
            #endregion
            #region Sistema WardRobe
            new CoatStorage().Login(client);
            #endregion
            #region Sistema Inner
            if (!InnerPower.InnerPowerPolle.TryGetValue(client.Entity.UID, out client.InnerPower))
                client.InnerPower = new InnerPower(client.Entity.Name, client.Entity.UID);
            Game.Features.PotencyPacks.RecoverPlayer(client.InnerPower);
            Game.Features.InnerPowerPurchases.RecoverPlayer(client.InnerPower);
            Game.Features.BossRushSystem.RecoverPending(client);
            client.InnerPower.UpdateStatus();
            new TitleStorage().Login(client);
            #endregion
            #region AtQuests
            if (client.Entity.MapID == 11225 || client.Entity.MapID == 11224)
                client.Entity.Teleport(1002, 408, 330);
            if (client.Entity.MapID == 1234)
                client.Entity.Teleport(1002, 408, 330);
            if (client.Entity.MapID == 4444)
                client.Entity.Teleport(1002, 408, 330);
            if (client.Entity.MapID == 2121)
                client.Entity.Teleport(1002, 408, 330);
            if (client.Entity.MapID == 2222)
                client.Entity.Teleport(1002, 408, 330);
            if (client.Entity.MapID == 2323)
                client.Entity.Teleport(1002, 408, 330);
            if (client.Entity.MapID == 1235)
                client.Entity.Teleport(1002, 408, 330);
            if (client.Entity.MapID == 1236)
                client.Entity.Teleport(1002, 408, 330);
            if (client.Entity.MapID == 1237)
                client.Entity.Teleport(1002, 408, 330);
            if (client.Entity.MapID == 1238)
                client.Entity.Teleport(1002, 408, 330);
            if (client.Entity.MapID == 2014)
                client.Entity.Teleport(1002, 408, 330);
            if (client.Entity.MapID == 3333)
                client.Entity.Teleport(1002, 408, 330);
            if (client.Entity.MapID == 1076)
                client.Entity.Teleport(1002, 408, 330);
            if (client.Entity.MapID == 2071)
                client.Entity.Teleport(1002, 408, 330);
            if (client.Entity.MapID == 7008)
                client.Entity.Teleport(1002, 408, 330);
            if (client.Entity.MapID == 1040)
                client.Entity.Teleport(1002, 408, 330);
            if (client.Entity.MapID == 2222)
                client.Entity.Teleport(1002, 408, 330);
            if (client.Entity.MapID == 11042 || client.Entity.MapID == 11030 || client.Entity.MapID == 11034)
                client.Entity.Teleport(1002, 408, 330);
            if (client.Entity.MapID == 2211)
                client.Entity.Teleport(1002, 408, 330);
            if (client.Entity.MapID == 5451)
                client.Entity.Teleport(1002, 408, 330);
            if (client.Entity.MapID == 1076)
                client.Entity.Teleport(1002, 408, 330);
            if (client.Map != null && client.Map.BaseID == 1005 && client.Entity.MapID != 1005)
                client.Entity.Teleport(1002, 408, 330);
            if (client.Map != null && client.Map.BaseID == 1005 && client.Entity.MapID != 1005)
                client.Entity.Teleport(1002, 408, 330);

            if ((client.Map != null && client.Map.BaseID == 10360) || client.Entity.MapID == 1950
                        || client.Entity.MapID == 7777
                        || client.Entity.MapID == 1090 || client.Entity.MapID == 4021
                        || client.Entity.MapID == 4022 || client.Entity.MapID == 4023
                        || client.Entity.MapID == 4024 || client.Entity.MapID == 4025
                        || client.Entity.MapID == 1508 || client.Entity.MapID == 1518
                        || client.Entity.MapID == 7001 || client.Entity.MapID == 1801
                        || client.Entity.MapID == 2065 || client.Entity.MapID == 8883
                        || client.Entity.MapID == 2057 || client.Entity.MapID == 1458
                        || client.Entity.MapID == 1459 || client.Entity.MapID == 1460
                        || client.Entity.MapID == 16414)
                client.Entity.Teleport(1002, 408, 330);
            if (client.Entity.MapID == 3073) client.Entity.Teleport(1002, 408, 330);
            if (client.Entity.MapID == 3691) client.Entity.Teleport(1002, 408, 330);
            if (client.Entity.MapID == 3692) client.Entity.Teleport(1002, 408, 330);
            if (client.Entity.MapID == 3693) client.Entity.Teleport(1002, 408, 330);
            if (client.Entity.MapID == 3694) client.Entity.Teleport(1002, 408, 330);
            if (client.Entity.MapID == 3071) client.Entity.Teleport(1002, 408, 330);
            if (client.Entity.MapID == 2530) client.Entity.Teleport(1002, 408, 330);
            if (client.Entity.MapID == 2529) client.Entity.Teleport(1002, 408, 330);
            if (client.Entity.MapID == 2528) client.Entity.Teleport(1002, 408, 330);
            if (client.Entity.MapID == 2527) client.Entity.Teleport(1002, 408, 330);
            if (client.Entity.MapID == 2526) client.Entity.Teleport(1002, 408, 330);
            if (client.Entity.MapID == 2525) client.Entity.Teleport(1002, 408, 330);
            if (client.Entity.MapID == 3070) client.Entity.Teleport(1002, 408, 330);
            if (client.Entity.MapID == 33333) client.Entity.Teleport(1002, 408, 330);
            if (client.Entity.MapID == 3320) client.Entity.Teleport(1002, 408, 330);
            if (client.Entity.MapID == 3321) client.Entity.Teleport(1002, 408, 330);
            if (client.Entity.MapID == 3322) client.Entity.Teleport(1002, 408, 330);
            if (client.Entity.MapID == 5528) client.Entity.Teleport(1002, 408, 330);
            if (client.Entity.MapID == 1702) client.Entity.Teleport(1002, 408, 330);
            if (client.Entity.MapID == 1701) client.Entity.Teleport(1002, 408, 330);
            #endregion
            #region Salir de mapas[jheisson]
            if (
                client.Entity.MapID == 4003 ||
                client.Entity.MapID == 200 || client.Entity.MapID == 204 || client.Entity.MapID == 208 ||
                client.Entity.MapID == 201 || client.Entity.MapID == 205 || client.Entity.MapID == 209 ||
                client.Entity.MapID == 202 || client.Entity.MapID == 206 || client.Entity.MapID == 210 ||
                client.Entity.MapID == 203 || client.Entity.MapID == 207 || client.Entity.MapID == 211 ||
                client.Entity.MapID == 212 || client.Entity.MapID == 213 || client.Entity.MapID == 214 ||
                client.Entity.MapID == 215 || client.Entity.MapID == 216 || client.Entity.MapID == 217 ||
                client.Entity.MapID == 218 || client.Entity.MapID == 219 || client.Entity.MapID == 220 ||                

                client.Entity.MapID == 128 || client.Entity.MapID == 129 || client.Entity.MapID == 123 ||
                client.Entity.MapID == 127 || client.Entity.MapID == 126 || client.Entity.MapID == 8877 ||
                client.Entity.MapID == 146 || client.Entity.MapID == 147 || client.Entity.MapID == 148 ||
                client.Entity.MapID == 149 || client.Entity.MapID == 150 || client.Entity.MapID == 151 ||
                client.Entity.MapID == 130 || client.Entity.MapID == 10360 || client.Entity.MapID == 8881 ||
                client.Entity.MapID == 132 || client.Entity.MapID == 10231 || client.Entity.MapID == 10232 ||
                client.Entity.MapID == 10233 || client.Entity.MapID == 10234 || client.Entity.MapID == 10235 ||
                client.Entity.MapID == 10236 || client.Entity.MapID == 10237 || client.Entity.MapID == 10238 ||
                client.Entity.MapID == 10239 || client.Entity.MapID == 5571 || client.Entity.MapID == 5576 ||
                client.Entity.MapID == 1991 || client.Entity.MapID == 3071 || client.Entity.MapID == 4445 ||
                client.Entity.MapID == 1735 || client.Entity.MapID == 1734 || client.Entity.MapID == 1733 ||
                client.Entity.MapID == 1732 || client.Entity.MapID == 1731 || client.Entity.MapID == 1730 ||
                client.Entity.MapID == 1950 || client.Entity.MapID == 2578 || client.Entity.MapID == 2057 ||
                client.Entity.MapID == 2061 || client.Entity.MapID == 2062 || client.Entity.MapID == 2063 ||
                client.Entity.MapID == 1280 || client.Entity.MapID == 1281 ||
                client.Entity.MapID == 1282 || client.Entity.MapID == 1283 ||
                client.Entity.MapID == 1284 || client.Entity.MapID == 1096 ||
                client.Entity.MapID == 1097 || client.Entity.MapID == 1098 ||
                client.Entity.MapID == 2064 || client.Entity.MapID == 5928 || 
                client.Entity.MapID == 6421 || client.Entity.MapID == 4020 ||
                
                client.Entity.MapID == 160 ||client.Entity.MapID == 161 ||
                client.Entity.MapID == 162 ||client.Entity.MapID == 163 ||
                client.Entity.MapID == 164 ||client.Entity.MapID == 165 ||
                client.Entity.MapID == 166 ||client.Entity.MapID == 167 ||
                client.Entity.MapID == 168 ||client.Entity.MapID == 169 ||
                client.Entity.MapID == 170 ||client.Entity.MapID == 171 ||
                client.Entity.MapID == 172 ||client.Entity.MapID == 173 ||
                client.Entity.MapID == 174 ||client.Entity.MapID == 175 ||
                client.Entity.MapID == 176 ||client.Entity.MapID == 177 ||
                client.Entity.MapID == 178 ||client.Entity.MapID == 179 ||
                client.Entity.MapID == 180 ||client.Entity.MapID == 181 ||
                client.Entity.MapID == 182 ||client.Entity.MapID == 183 ||
                client.Entity.MapID == 184 ||client.Entity.MapID == 185 ||
                client.Entity.MapID == 186 ||client.Entity.MapID == 187 ||
                client.Entity.MapID == 188 ||client.Entity.MapID == 189 ||
                client.Entity.MapID == 190 ||client.Entity.MapID == 181 ||
                client.Entity.MapID == 192 ||client.Entity.MapID == 193 ||
                client.Entity.MapID == 194 ||client.Entity.MapID == 195 ||
                client.Entity.MapID == 196 ||client.Entity.MapID == 197 ||
                client.Entity.MapID == 198 ||client.Entity.MapID == 199 ||
                client.Entity.MapID == 2072 || client.Entity.MapID == 10566 ||
                client.Entity.MapID == 2071 ||client.Entity.MapID == 60 ||
                client.Entity.MapID == 10230 || client.Entity.MapID == 1037 ||
                client.Entity.MapID == 1645 || client.Entity.MapID == 1368)//
            {
                client.Entity.Teleport(1002, 408, 330);
            }
            #endregion
            #region Titulo De efecto para Cada VIP

            #region Efecto De VIP  //jheisson     
            //if (client.Entity.VIPLevel == 8)
            //{
            //    client.Entity.AddFlag((ulong)FlagsUpdate.Flags.VIP8Slayer, StatusFlagsBigVector32.PermanentFlag, false);
            //}
            // if (client.Entity.VIPLevel == 7)
            //  {
            //      client.Entity.AddFlag((ulong)FlagsUpdate.Flags.VIP7Slayer,StatusFlagsBigVector32.PermanentFlag,false);                   
            // }
            //if (client.Entity.VIPLevel == 6)
            //{
            //    //client.Entity.AddFlag((ulong)FlagsUpdate.Flags.GoldBrickElite, StatusFlagsBigVector32.PermanentFlag, false);
            //}
            //if (client.Entity.VIPLevel == 5)
            //{
            //    client.Entity.AddFlag((ulong)FlagsUpdate.Flags.GoldBrickUnique, StatusFlagsBigVector32.PermanentFlag, false);
            //}
            //if (client.Entity.VIPLevel <= 4)
            //{
            //    client.Entity.AddFlag((ulong)FlagsUpdate.Flags.GoldBrickRefined, StatusFlagsBigVector32.PermanentFlag, false);
            //}
           
            #endregion
            if (client.Entity.VIPLevel == 7)
            {
                if (!client.Entity.Titles.ContainsKey(Network.GamePackets.TitlePacket.Titles.Vip7))
                    client.Entity.AddTopStatus((ulong)Network.GamePackets.TitlePacket.Titles.Vip7, DateTime.Now.AddMonths(1));
                                           
                    client.Entity.MyTitle = TitlePacket.Titles.Vip7;
            }
            if (client.Entity.VIPLevel == 6)
            {
                if (!client.Entity.Titles.ContainsKey(Network.GamePackets.TitlePacket.Titles.Vip6x30))
                    client.Entity.AddTopStatus((ulong)Network.GamePackets.TitlePacket.Titles.Vip6x30, DateTime.Now.AddMonths(1));
                client.Entity.MyTitle = TitlePacket.Titles.Vip6x30;
            }
            if (client.Entity.VIPLevel <= 5 && client.Entity.VIPLevelDays >= 1)
            {
                client.Entity.MyTitle = 0;
            }
            if (client.Account.State == Database.AccountTable.AccountState.Administrador)
            {
                if (!client.Entity.Titles.ContainsKey(Network.GamePackets.TitlePacket.Titles.ProjectManager))
                    client.Entity.AddTopStatus((ulong)Network.GamePackets.TitlePacket.Titles.ProjectManager, DateTime.Now.AddMonths(10));
                client.Entity.MyTitle = TitlePacket.Titles.ProjectManager;
            }
            //if (client.Account.State == Database.AccountTable.AccountState.GameMaster)
            //{
            //    if (!client.Player.Titles.ContainsKey(Network.GamePackets.TitlePacket.Titles.GameMaster))
            //        client.Player.AddTopStatus((ulong)Network.GamePackets.TitlePacket.Titles.GameMaster, 2, DateTime.Now.AddMonths(1), false);
            //    client.Player.MyTitle = TitlePacket.Titles.GameMaster;
            //}
            //if (client.Player.Titles.Count > 0)
            //{
            //    TitlePacket tpacket = new TitlePacket(true);
            //    tpacket.Identifier = client.Player.UID;
            //    tpacket.Title = client.Player.MyTitle;
            //    tpacket.Type = TitlePacket.Types.Switch;
            //    tpacket.Count = 1;
            //    tpacket.Add((byte)client.Player.MyTitle);
            //    client.Player.MyTitle = tpacket.Title;
            //    client.Send(tpacket);
            //    client.Player.Teleport(client.Player.MapID, client.Player.X, client.Player.Y);
            //}
            #endregion
            #region Class New Effect
            if ((client.Entity.Class >= 10) && (client.Entity.Class <= 15))
            {
                _String str = new _String(true);
                str.UID = client.Entity.UID;
                str.TextsCount = 1;
                str.Type = _String.Effect;
                str.Texts.Add("pk_ble_war");
                client.Entity.SendScreen(str);
            }
            if ((client.Entity.Class >= 20) && (client.Entity.Class <= 25))
            {
                _String str = new _String(true);
                str.UID = client.Entity.UID;
                str.TextsCount = 1;
                str.Type = _String.Effect;
                str.Texts.Add("pk_ble_sol");
                client.Entity.SendScreen(str);
            }
            if ((client.Entity.Class >= 40) && (client.Entity.Class <= 45))
            {
                _String str = new _String(true);
                str.UID = client.Entity.UID;
                str.TextsCount = 1;
                str.Type = _String.Effect;
                str.Texts.Add("pk_ble_bow");
                client.Entity.SendScreen(str);
            }
            if ((client.Entity.Class >= 50) && (client.Entity.Class <= 55))
            {
                _String str = new _String(true);
                str.UID = client.Entity.UID;
                str.TextsCount = 1;
                str.Type = _String.Effect;
                str.Texts.Add("pk_ble_bea");
                client.Entity.SendScreen(str);
            }
            if ((client.Entity.Class >= 60) && (client.Entity.Class <= 65))
            {
                _String str = new _String(true);
                str.UID = client.Entity.UID;
                str.TextsCount = 1;
                str.Type = _String.Effect;
                str.Texts.Add("pk_ble_fie");
                client.Entity.SendScreen(str);
            }
            if ((client.Entity.Class >= 70) && (client.Entity.Class <= 75))
            {
                _String str = new _String(true);
                str.UID = client.Entity.UID;
                str.TextsCount = 1;
                str.Type = _String.Effect;
                str.Texts.Add("pk_ble_pir");
                client.Entity.SendScreen(str);
            }
            if ((client.Entity.Class >= 80) && (client.Entity.Class <= 80))
            {
                _String str = new _String(true);
                str.UID = client.Entity.UID;
                str.TextsCount = 1;
                str.Type = _String.Effect;
                str.Texts.Add("pk_dra_ble");
                client.Entity.SendScreen(str);
            }
            if ((client.Entity.Class >= 130) && (client.Entity.Class <= 135))
            {
                _String str = new _String(true);
                str.UID = client.Entity.UID;
                str.TextsCount = 1;
                str.Type = _String.Effect;
                str.Texts.Add("pk_ble_wat");
                client.Entity.SendScreen(str);
            }
            if ((client.Entity.Class >= 140) && (client.Entity.Class <= 145))
            {
                _String str = new _String(true);
                str.UID = client.Entity.UID;
                str.TextsCount = 1;
                str.Type = _String.Effect;
                str.Texts.Add("pk_ble_fla");
                client.Entity.SendScreen(str);
            }
            #endregion Class New Effect
            #region login game
            if ((int)client.Account.State >= 0)
            {
                Kernel.SendWorldMessage(new Revora.Network.GamePackets.Message("Online Players : [" + Kernel.GamePool.Count + "]", System.Drawing.Color.White, Network.GamePackets.Message.Center), Server.GamePool);
            }
            #endregion
            #region Mesajes de server. Reparar

            //int count = 1;
            //for (int i = 0; i < count; i++)
            //{
            //    Mailbox.PrizeInfo prize = new Mailbox.PrizeInfo()
            //    {
            //        ID = (uint)i,
            //        Time = 1800,
            //        goldprize = 5000,
            //        Sender = "Slayer 2.0 ".ToString(),
            //        Subject = "Cumpliendo 9 Meses Online".ToString(),
            //        Message = "[Puedes reclamar cada 30 Minutos tu premio por estar enlinea] Nos Sentimos Bien Sabiendo Que Usted, es Ahora Juega Slayer 2.0 Por eso Acepte Estos premios" + i.ToString(),

            //    };

            //    client.Prizes.Add(prize.ID, prize);

            //}
            //int count1 = 1;
            //for (int i = 0; i < count1; i++)
            //{
            //    Mailbox.PrizeInfo prize = new Mailbox.PrizeInfo()
            //    {
            //        ID = (uint)i,
            //        Time = 60,
            //        goldprize = 50000000,
            //        Sender = "Slayer 3.0 ".ToString(),
            //        Subject = "Premio Limitado ".ToString(),
            //        Message = "[Puedes reclamar cada  Minutos tu premio por estar enlinea] Nos Sentimos Bien Sabiendo Que Usted, es Ahora Juega Slayer 3.0 Por eso Acepte Estos premios" + i.ToString(),

            //    };

            //    client.Prizes.Add(prize.ID, prize);

            //}
            #endregion
            #region Reward
            RewardEvent.SendReward(client);
            #endregion
            #region Inbox
            if (client.Prizes.Count > 0)
            {
                Mailbox.SendMailbox(client, true);
            }
            #endregion
            #region SendReload
            Game.Entity.SendReload(client);
            #endregion
            #region Out map
            if (client.Entity.MapID == 1992 || client.Entity.MapID == 6412 || client.Entity.MapID == 6420 ||
                client.Entity.MapID == 1806 || client.Entity.MapID == 1559 || client.Entity.MapID == 700 ||
                client.Entity.MapID == 1508 || client.Entity.MapID == 1509 || client.Entity.MapID == 1507 ||
                client.Entity.MapID == 1506 || client.Entity.MapID == 1505 || client.Entity.MapID == 3825 ||
                client.Entity.MapID == 1707 || client.Entity.MapID == 3081 || client.Entity.MapID == 14785 ||
                client.Entity.MapID == 5575 || client.Entity.MapID == 5574 || client.Entity.MapID == 5573 ||
                client.Entity.MapID == 5572 || client.Entity.MapID == 5571 || client.Entity.MapID == 5576 ||
                client.Entity.MapID == 1991 || client.Entity.MapID == 3071 || client.Entity.MapID == 4445 ||
                client.Entity.MapID == 1735 || client.Entity.MapID == 1734 || client.Entity.MapID == 1733 ||
                client.Entity.MapID == 1732 || client.Entity.MapID == 1731 || client.Entity.MapID == 1730 ||
                client.Entity.MapID == 1950 || client.Entity.MapID == 2578 || client.Entity.MapID == 2057 ||
                client.Entity.MapID == 2061 || client.Entity.MapID == 2062 || client.Entity.MapID == 2063 ||
                client.Entity.MapID == 2064 || client.Entity.MapID == 5928 || client.Entity.MapID == 6421 ||
                client.Entity.MapID == 2072)
            {
                client.Entity.Teleport(1002, 408, 330);
            }
            #endregion
            #region OfflineTG
            if (client.Entity.MapID == 601)
            {
                OfflineTGStats sts = new OfflineTGStats(true);
                ushort minutes = Revora.Game.Features.ExperienceMath.TrainingMinutes(client.OfflineTGEnterTime,DateTime.Now);
                sts.TotalTrainingMinutesLeft = (ushort)(900 - minutes);
                sts.TrainedMinutes = minutes;
                ulong exp = client.Entity.Experience;
                byte level = client.Entity.Level;
                int remainingMinutes = minutes;
                while (remainingMinutes >= 300)
                {
                    remainingMinutes -= 300;
                    exp += client.ExpBall;
                }
                if (remainingMinutes != 0)
                    exp += Revora.Game.Features.ExperienceMath.Scale(client.ExpBall,(ulong)remainingMinutes,300);

                while (level < Revora.Config.LevelCap && exp >= Database.DataHolder.LevelExperience(level))
                {
                    exp -= Database.DataHolder.LevelExperience(level);
                    level++;
                }
                double percent = (double)exp * (double)100 / (double)Database.DataHolder.LevelExperience(level);

                sts.Character_NewExp = (ulong)(percent * 100000);
                sts.Character_AcquiredLevel = level;
                sts.Send(client);
            }
            #endregion
            #region SecondaryPassword
            if (client.WarehousePW != 0)
            {
                SecondaryPassword SP = new SecondaryPassword(true);
                SP.Action = SecondaryPassword.Mode.PasswordCorrect;
                SP.OldPassword = 0x1;
                client.Send(SP.ToArray());
            }
            #endregion

            #region Adding earned skills
            if (client.Entity.Reborn == 2)
                client.AddSpell(new Spell(true) { ID = 9876 });
            if (client.Entity.Class >= 51 && client.Entity.Class <= 55 && client.Entity.FirstRebornClass == 55 && client.Entity.Reborn == 1)
                client.AddSpell(new Spell(true) { ID = 6002 });
            if (client.Entity.FirstRebornClass == 15 && client.Entity.SecondRebornClass == 15 && client.Entity.Class >= 11 && client.Entity.Class <= 15 && client.Entity.Reborn == 2)
                client.AddSpell(new Spell(true) { ID = 10315 });
            if (client.Entity.FirstRebornClass == 75 && client.Entity.SecondRebornClass == 75 && client.Entity.Class >= 71 && client.Entity.Class <= 75 && client.Entity.Reborn == 2)
                client.AddSpell(new Spell(true) { ID = 11040 });
            if (client.Entity.FirstRebornClass == 25 && client.Entity.SecondRebornClass == 25 && client.Entity.Class >= 21 && client.Entity.Class <= 25 && client.Entity.Reborn == 2)
                client.AddSpell(new Spell(true) { ID = 10311 });
            if (client.Entity.FirstRebornClass == 45 && client.Entity.SecondRebornClass == 45 && client.Entity.Class >= 41 && client.Entity.Class <= 45 && client.Entity.Reborn == 2)
                client.AddSpell(new Spell(true) { ID = 10313 });
            if (client.Entity.FirstRebornClass == 55 && client.Entity.SecondRebornClass == 55 && client.Entity.Class >= 51 && client.Entity.Class <= 55 && client.Entity.Reborn == 2)
                client.AddSpell(new Spell(true) { ID = 6003 });
            if (client.Entity.FirstRebornClass == 65 && client.Entity.SecondRebornClass == 65 && client.Entity.Class >= 61 && client.Entity.Class <= 65 && client.Entity.Reborn == 2)
                client.AddSpell(new Spell(true) { ID = 10405 });
            if (client.Entity.FirstRebornClass == 135 && client.Entity.SecondRebornClass == 135 && client.Entity.Class >= 131 && client.Entity.Class <= 135 && client.Entity.Reborn == 2)
                client.AddSpell(new Spell(true) { ID = 30000 });
            if (client.Entity.FirstRebornClass == 145 && client.Entity.SecondRebornClass == 145 && client.Entity.Class >= 140 && client.Entity.Class <= 145 && client.Entity.Reborn == 2)
                client.AddSpell(new Spell(true) { ID = 10310 });
            if (client.Entity.Reborn == 1)
            {
                if (client.Entity.FirstRebornClass == 15 && client.Entity.Class >= 11 && client.Entity.Class <= 15)
                {
                    client.AddSpell(new Spell(true) { ID = 3050 });
                }
                else if (client.Entity.FirstRebornClass == 25 && client.Entity.Class >= 21 && client.Entity.Class <= 25)
                {
                    client.AddSpell(new Spell(true) { ID = 3060 });
                }
                else if (client.Entity.FirstRebornClass == 145 && client.Entity.Class >= 142 && client.Entity.Class <= 145)
                {
                    client.AddSpell(new Spell(true) { ID = 3080 });
                }
                else if (client.Entity.FirstRebornClass == 135 && client.Entity.Class >= 132 && client.Entity.Class <= 135)
                {
                    client.AddSpell(new Spell(true) { ID = 3090 });
                }
            }
            if (client.Entity.Reborn == 2)
            {
                if (client.Entity.SecondRebornClass == 15 && client.Entity.Class >= 11 && client.Entity.Class <= 15)
                {
                    client.AddSpell(new Spell(true) { ID = 3050 });
                }
                else if (client.Entity.SecondRebornClass == 25)
                {
                    client.AddSpell(new Spell(true) { ID = 3060 });
                }
                else if (client.Entity.SecondRebornClass == 145 && client.Entity.Class >= 142 && client.Entity.Class <= 145)
                {
                    client.AddSpell(new Spell(true) { ID = 3080 });
                }
                else if (client.Entity.SecondRebornClass == 135 && client.Entity.Class >= 132 && client.Entity.Class <= 135)
                {
                    client.AddSpell(new Spell(true) { ID = 3090 });
                }
            }
            #endregion
            #region Merchant
            if (client.Entity.Merchant == 255)
            {
                client.Entity.Update((byte)FlagsUpdate.DataType.Merchant, 255, 0);
            }
            else if (client.Entity.Merchant == 1)
            {
                Attack send = new Attack(true);
                send.AttackType = Attack.MerchantProgress;
                client.Send(send.ToArray());
            }
            #endregion
            #region Skill
            #region Check Skills
            Interfaces.ISkill[] spells = client.Spells.Values.ToArray();
            foreach (Interfaces.ISkill spell in spells)
            {
                #region No class Trojan
                if (client.Entity.Class / 10 != 1 && client.Entity.FirstRebornClass != 15 && client.Entity.SecondRebornClass != 15)
                {
                    switch (spell.ID)
                    {
                        case 1115://Hercules
                        case 10315://DragonWhirl
                        case 1190:
                        case 1110:
                        case 1270:
                        case 11990:
                        case 11980:
                        case 11970:
                        case 11960:
                            client.RemoveSpell(spell);
                            break;
                    }
                }
                #endregion
                #region No class Ninja
                if (client.Entity.Class / 10 != 5 && client.Entity.FirstRebornClass != 55 && client.Entity.SecondRebornClass != 55)
                {
                    switch (spell.ID)
                    {

                        case 6011:
                        case 6010:
                        case 6001:
                        case 6000:
                        case 6004:
                        case 6003:
                        case 11170:
                        case 11180:
                        case 11230:
                        case 12070:
                        case 12090:
                        case 12080:
                        case 12110:
                            client.RemoveSpell(spell);
                            break;
                    }
                }
                #endregion
                if (client.Entity.Class / 10 != 13 && client.Entity.Class / 10 != 14)
                {
                    switch (spell.ID)
                    {
                        case 10309:
                            client.RemoveSpell(spell);
                            break;
                    }
                }
                if (client.Entity.Class / 10 != 2 && client.Entity.Class / 10 != 1)
                {
                    switch (spell.ID)
                    {
                        case 1015:
                            client.RemoveSpell(spell);
                            break;
                    }
                }
                if (client.Entity.FirstRebornClass != 15 && client.Entity.SecondRebornClass != 15)
                {
                    switch (spell.ID)
                    {
                        case 3050:
                            client.RemoveSpell(spell);
                            break;
                    }
                }
                #region Remover Compasion
                if (client.Entity.Class != 61 && client.Entity.Class != 65)
                {
                    switch (spell.ID)
                    {
                        case 10430:
                        case 10425:
                            client.RemoveSpell(spell);
                            break;
                    }
                }
                #endregion Remover Compasion
                #region No class Warrior
                if (client.Entity.Class / 10 != 2 && client.Entity.FirstRebornClass != 25 && client.Entity.SecondRebornClass != 25)
                {
                    switch (spell.ID)
                    {
                        case 1025:
                        case 1051:
                        case 1020:
                        case 11200:
                        case 10470:
                        case 11160:
                        case 10002:
                        case 1320:
                            client.RemoveSpell(spell);
                            break;
                    }
                }
                #endregion
                #region No class Archer
                if (client.Entity.Class != 40 && client.Entity.Class != 41 && client.Entity.Class != 42 && client.Entity.Class != 43 && client.Entity.Class != 42 && client.Entity.Class != 43 && client.Entity.Class != 44 && client.Entity.Class != 45 && client.Entity.FirstRebornClass != 45 && client.Entity.SecondRebornClass != 45)
                {
                    switch (spell.ID)
                    {
                        case 9000:
                        case 8001:
                        case 8000:
                        case 8003:
                        case 8002:
                        case 8030:
                        case 11600:
                        case 11590:
                        case 11650:
                        case 11610:
                        case 11620:
                        case 11660:
                        case 11670:

                            client.RemoveSpell(spell);
                            break;
                    }
                }
                #endregion
                #region No class Monk
                if (client.Entity.Class / 10 != 6 && client.Entity.FirstRebornClass != 65 && client.Entity.SecondRebornClass != 65)
                {
                    switch (spell.ID)
                    {
                        case 10381:
                        case 10490:
                        case 10390:
                        case 10415:
                        case 10430:
                        case 10395:
                        case 10410:
                        case 10420:
                        case 10421:
                        case 10422:
                        case 10423:
                        case 10424:
                        case 10425:
                            client.RemoveSpell(spell);
                            break;
                    }
                }
                if (client.Entity.FirstRebornClass == 65 && client.Entity.Class / 10 == 6 && client.Entity.Class / 10 != 2 || client.Entity.SecondRebornClass == 65 && client.Entity.Class / 10 == 6 && client.Entity.Class / 10 != 2)
                {
                    switch (spell.ID)
                    {
                        case 10400:
                            client.RemoveSpell(spell);
                            break;
                    }
                }
                if (client.Entity.Class / 10 != 6 && client.Entity.FirstRebornClass != 65 && client.Entity.SecondRebornClass != 65)
                {
                    switch (spell.ID)
                    {
                        case 10400:
                            client.RemoveSpell(spell);
                            break;
                    }
                }
                #endregion
                #region No class Fire
                if (client.Entity.Class / 10 != 14 && client.Entity.FirstRebornClass != 145 && client.Entity.SecondRebornClass != 145)
                {
                    switch (spell.ID)
                    {

                        case 1010:
                        case 1120:
                        case 1002:
                        case 1180:
                        case 1150:
                        case 1195:
                        case 10201:
                            client.RemoveSpell(spell);
                            break;
                    }
                }
                #endregion
                #region No class Water
                if (client.Entity.Class / 10 != 13 && client.Entity.FirstRebornClass != 135 && client.Entity.SecondRebornClass != 135)
                {
                    switch (spell.ID)
                    {
                        case 1280:
                        case 1085:
                        case 1005:
                        case 1010:
                        case 1195:
                        case 1125:
                        case 1100:
                        case 5001:
                        case 1055:
                        case 1050:
                        case 1090:
                        case 1175:
                        case 1170:
                        case 1095:
                        case 1075:
                        case 1350:
                            client.RemoveSpell(spell);

                            break;
                    }
                }
                #endregion
                #region No class Pirate
                if (client.Entity.Class / 10 != 7 && client.Entity.FirstRebornClass != 75 && client.Entity.SecondRebornClass != 75)
                {
                    switch (spell.ID)
                    {
                        case 11140:
                        case 11130:
                        case 11120:
                        case 11110:
                        case 11100:
                        case 11070:
                        case 11060:
                        case 11050:
                        case 11040:
                        case 11030:
                            client.RemoveSpell(spell);
                            break;
                    }
                }
                #endregion
                #region No class Dragon-Warrior
                if (client.Entity.Class != 80 && client.Entity.Class != 81 && client.Entity.Class != 82 && client.Entity.Class != 83 && client.Entity.Class != 82 && client.Entity.Class != 83 && client.Entity.Class != 84 && client.Entity.Class != 85)
                {
                    switch (spell.ID)
                    {
                        case 12270:
                        case 12280:
                        case 12290:
                        case 12350:
                        case 12320:
                        case 12330:
                        case 12340:
                        case 12120:
                        case 12130:
                        case 12140:
                        case 12170:
                        case 12160:
                        case 12210:
                        case 12220:
                        case 12230:
                        case 12240:
                        case 12300:
                        case 12200:
                            client.RemoveSpell(spell);
                            break;
                    }
                }
                #endregion
                if (client.Entity.Reborn == 2 && client.Entity.FirstRebornClass != 75 && client.Entity.SecondRebornClass != 75)
                {
                    switch (spell.ID)
                    {
                        case 11100:
                            client.RemoveSpell(spell);
                            break;
                    }
                }
                #region Reborn 1 and 2 and renacantion After class Ninja
                if (client.Entity.Class / 10 != 5 && client.Entity.FirstRebornClass == 55 || client.Entity.Class / 10 != 5 && client.Entity.SecondRebornClass == 55)
                {
                    switch (spell.ID)
                    {
                        case 6011:
                        case 6010:
                        case 6000:
                        case 11170:
                        case 11230:
                        case 11180:
                        case 6004:
                        case 6003:
                        case 12070:
                        case 12090:
                        case 12080:
                        case 12110:
                            client.RemoveSpell(spell);
                            break;
                    }
                }
                #endregion
                #region Reborn 1 and 2 and renacantion After class Archer
                if (client.Entity.Class / 10 != 4 && client.Entity.FirstRebornClass == 45 || client.Entity.Class / 10 != 4 && client.Entity.SecondRebornClass == 45)
                {
                    switch (spell.ID)
                    {
                        case 9000:
                        case 8001:
                        case 8000:
                        case 8003:
                        case 8002:
                        case 8030:
                        case 11600:
                        case 11590:
                        case 11650:
                        case 11610:
                        case 11620:
                        case 11660:
                        case 11670:
                            client.RemoveSpell(spell);
                            break;
                    }
                }
                #endregion
                #region Reborn 1 and 2 and renacantion After class Fire
                if (client.Entity.Class / 10 != 14 && client.Entity.FirstRebornClass == 145 || client.Entity.Class / 10 != 14 && client.Entity.SecondRebornClass == 145)
                {
                    switch (spell.ID)
                    {
                        case 1010:
                        case 1120:
                        case 1002:
                        case 1180:
                        case 1150:
                            client.RemoveSpell(spell);
                            break;
                    }
                }
                #endregion
                #region Reborn 1 and 2 and renacantion After class Pirate
                if (client.Entity.FirstRebornClass == 75 && client.Entity.Class / 10 == 14 || client.Entity.SecondRebornClass == 75 && client.Entity.Class / 10 == 14)
                {
                    switch (spell.ID)
                    {
                        case 11140:
                        case 11130:
                        case 11120:
                        case 11110:
                        case 11100:
                        case 11060:
                        case 11050:
                        case 11040:
                        case 11030:
                            client.RemoveSpell(spell);
                            break;
                    }
                }
                if (client.Entity.Class / 10 != 7 && client.Entity.SecondRebornClass == 75 && client.Entity.Class / 10 != 14 || client.Entity.Class / 10 != 7 && client.Entity.FirstRebornClass == 75 && client.Entity.Class / 10 != 14)
                {
                    switch (spell.ID)
                    {
                        case 11140:
                        case 11130:
                        case 11120:
                        case 11110:
                        case 11100:
                        case 11060:
                        case 11050:
                        case 11040:
                        case 11030:
                            client.RemoveSpell(spell);
                            break;
                    }
                }
                #endregion
                #region Reborn 1 and 2 and renacantion After class Trojan
                if (client.Entity.Class / 10 != 1 && client.Entity.FirstRebornClass == 15 && client.Entity.Class / 10 != 14 && client.Entity.Class / 10 != 13 && client.Entity.Class / 10 != 4 || client.Entity.Class / 10 != 1 && client.Entity.SecondRebornClass == 15 && client.Entity.Class / 10 != 14 && client.Entity.Class / 10 != 13 && client.Entity.Class / 10 != 4)
                {
                    switch (spell.ID)
                    {
                        case 1115://Hercules
                        case 10315://DragonWhirl
                        case 1270:
                        case 11990:
                        case 11980:
                        case 11970:
                        case 11960:
                            client.RemoveSpell(spell);
                            break;
                    }
                }
                if (client.Entity.FirstRebornClass == 15 && client.Entity.Class / 10 == 14 || client.Entity.FirstRebornClass == 15 && client.Entity.Class / 10 == 13 || client.Entity.FirstRebornClass == 15 && client.Entity.Class / 10 == 4 || client.Entity.SecondRebornClass == 15 && client.Entity.Class / 10 == 14 || client.Entity.SecondRebornClass == 15 && client.Entity.Class / 10 == 13 || client.Entity.SecondRebornClass == 15 && client.Entity.Class / 10 == 4)
                {
                    switch (spell.ID)
                    {
                        case 1115://Hercules
                        case 10315://DragonWhirl
                        case 11990:
                        case 11980:
                        case 11970:
                        case 11960:
                            client.RemoveSpell(spell);
                            break;
                    }
                }
                #endregion
                #region Reborn 1 and 2 and renacantion After class Warrior
                if (client.Entity.FirstRebornClass == 25 && client.Entity.Class / 10 == 14 || client.Entity.FirstRebornClass == 25 && client.Entity.Class / 10 == 4 || client.Entity.SecondRebornClass == 25 && client.Entity.Class / 10 == 14 || client.Entity.SecondRebornClass == 25 && client.Entity.Class / 10 == 4)
                {
                    switch (spell.ID)
                    {
                        case 1025:
                        case 11200:
                        case 10470:
                        case 11160:
                        case 1051:
                        case 1320:
                        case 1015:
                            client.RemoveSpell(spell);
                            break;
                    }
                }
                if (client.Entity.Class / 10 != 2 && client.Entity.FirstRebornClass == 25 && client.Entity.Class / 10 == 1 || client.Entity.Class / 10 != 2 && client.Entity.FirstRebornClass == 25 && client.Entity.Class / 10 == 5 || client.Entity.Class / 10 != 2 && client.Entity.FirstRebornClass == 25 && client.Entity.Class / 10 == 7 || client.Entity.Class / 10 != 2 && client.Entity.SecondRebornClass == 25 && client.Entity.Class / 10 == 1 || client.Entity.Class / 10 != 2 && client.Entity.SecondRebornClass == 25 && client.Entity.Class / 10 == 5 || client.Entity.Class / 10 != 2 && client.Entity.SecondRebornClass == 25 && client.Entity.Class / 10 == 7)
                {
                    switch (spell.ID)
                    {
                        case 1025:
                        case 1051:
                        case 11200:
                        case 10470:
                        case 11160:
                        case 1020:
                            client.RemoveSpell(spell);
                            break;
                    }
                }
                if (client.Entity.Class / 10 != 2 && client.Entity.FirstRebornClass == 25 && client.Entity.Class / 10 == 6 || client.Entity.Class / 10 != 2 && client.Entity.SecondRebornClass == 25 && client.Entity.Class / 10 == 6)
                {
                    switch (spell.ID)
                    {
                        case 1025:
                        case 1051:
                        case 11200:
                        case 10470:
                        case 11160:
                        case 1020:
                            client.RemoveSpell(spell);
                            break;
                    }
                }

                #endregion
                #region Reborn 1 and 2 and renacantion After class Water
                if (client.Entity.FirstRebornClass == 135 && client.Entity.Class / 10 == 4 || client.Entity.SecondRebornClass == 135 && client.Entity.Class / 10 == 4)
                {
                    switch (spell.ID)
                    {
                        case 1280:
                        case 1010:
                        case 1125:
                        case 1100:
                        case 1175:
                        case 1170:
                        case 1050:
                        case 1085:
                        case 5001:
                        case 1055:
                        case 1350:

                            client.RemoveSpell(spell);
                            break;
                    }
                }
                if (client.Entity.FirstRebornClass == 135 && client.Entity.Class / 10 == 14 && client.Entity.Class / 10 != 2 || client.Entity.SecondRebornClass == 135 && client.Entity.Class / 10 == 14 && client.Entity.Class / 10 != 2)
                {
                    switch (spell.ID)
                    {
                        case 1280:
                        case 1010:
                        case 1125:
                        case 5001:
                        case 1100:
                        case 1170:
                        case 1020:
                        case 1085:
                        case 1095:
                        case 1195:
                        case 1350:

                            client.RemoveSpell(spell);
                            break;
                    }
                }
                if (client.Entity.FirstRebornClass == 135 && client.Entity.Class / 10 == 1 || client.Entity.FirstRebornClass == 135 && client.Entity.Class / 10 == 2 || client.Entity.FirstRebornClass == 135 && client.Entity.Class / 10 == 5 || client.Entity.FirstRebornClass == 135 && client.Entity.Class / 10 == 7 || client.Entity.FirstRebornClass == 135 && client.Entity.Class / 10 == 6 || client.Entity.SecondRebornClass == 135 && client.Entity.Class / 10 == 1 || client.Entity.SecondRebornClass == 135 && client.Entity.Class / 10 == 2 || client.Entity.SecondRebornClass == 135 && client.Entity.Class / 10 == 5 || client.Entity.SecondRebornClass == 135 && client.Entity.Class / 10 == 7 || client.Entity.SecondRebornClass == 135 && client.Entity.Class / 10 == 6)
                {
                    switch (spell.ID)
                    {
                        case 1280:
                        case 1010:
                        case 1125:
                        case 1100:
                        case 5001:
                        case 1175:
                        case 1170:
                        case 1050:
                        case 1055:
                        case 1075:

                            client.RemoveSpell(spell);
                            break;
                    }
                }
                #endregion
                #region Reborn 1 and 2 and rencantion After class Monk
                if (client.Entity.FirstRebornClass == 65 && client.Entity.Class / 10 == 14 || client.Entity.SecondRebornClass == 65 && client.Entity.Class / 10 == 14)
                {
                    switch (spell.ID)
                    {
                        case 10381:
                        case 10490:
                        case 10390:
                        case 10430:
                        case 10395:                        
                        case 10410:
                        case 10421:
                        case 10422:
                        case 10423:
                        case 10424:
                            client.RemoveSpell(spell);
                            break;
                    }
                }
                if (client.Entity.Class / 10 != 6 && client.Entity.FirstRebornClass == 65 && client.Entity.Class / 10 != 14 || client.Entity.Class / 10 != 6 && client.Entity.SecondRebornClass == 65 && client.Entity.Class / 10 != 14)
                {
                    switch (spell.ID)
                    {
                        case 10381:
                        case 10490:
                        case 10390:
                        case 10430:
                        case 10395:                        
                        case 10410:
                        case 10421:
                        case 10422:
                        case 10423:
                        case 10424:
                            client.RemoveSpell(spell);
                            break;
                    }
                }
                #endregion
                #region _Skill Pure Torjan_
                if (client.Entity.Class / 10 != 1 && client.Entity.FirstRebornClass != 15 && client.Entity.SecondRebornClass != 15)
                {
                    switch (spell.ID)
                    {
                        case 10315:
                            client.RemoveSpell(spell);
                            break;
                    }
                }
                #endregion
                #region _Skill Pure Warrior_
                if (client.Entity.SecondRebornClass != 25)
                {
                    switch (spell.ID)
                    {
                        case 3060:
                            client.RemoveSpell(spell);
                            break;
                    }
                }
                #endregion
                #region _Skill Pure Water_

                if (client.Entity.Class != 131 && client.Entity.FirstRebornClass != 135 && client.Entity.SecondRebornClass != 135 || client.Entity.Class != 132 && client.Entity.FirstRebornClass != 135 && client.Entity.SecondRebornClass != 135 || client.Entity.Class != 133 && client.Entity.FirstRebornClass != 135 && client.Entity.SecondRebornClass != 135 || client.Entity.Class != 134 && client.Entity.FirstRebornClass != 135 && client.Entity.SecondRebornClass != 135 || client.Entity.Class != 135 && client.Entity.FirstRebornClass != 135 && client.Entity.SecondRebornClass != 135)
                {
                    switch (spell.ID)
                    {
                        case 30000:
                            client.RemoveSpell(spell);
                            break;
                    }
                }
                if (client.Entity.Class == 50 || client.Entity.Class == 51 || client.Entity.Class == 52 || client.Entity.Class == 53 || client.Entity.Class == 54 || client.Entity.Class == 55)
                {
                    switch (spell.ID)
                    {
                        case 30000:
                            client.RemoveSpell(spell);
                            break;
                    }
                }
                if (client.Entity.Class == 10 || client.Entity.Class == 11 || client.Entity.Class == 12 || client.Entity.Class == 13 || client.Entity.Class == 14 || client.Entity.Class == 15)
                {
                    switch (spell.ID)
                    {
                        case 30000:
                            client.RemoveSpell(spell);
                            break;
                    }
                }
                if (client.Entity.Class == 60 || client.Entity.Class == 61 || client.Entity.Class == 62 || client.Entity.Class == 63 || client.Entity.Class == 64 || client.Entity.Class == 65)
                {
                    switch (spell.ID)
                    {
                        case 30000:
                            client.RemoveSpell(spell);
                            break;
                    }
                }
                if (client.Entity.Class == 40 || client.Entity.Class == 41 || client.Entity.Class == 42 || client.Entity.Class == 43 || client.Entity.Class == 44 || client.Entity.Class == 45)
                {
                    switch (spell.ID)
                    {
                        case 30000:
                            client.RemoveSpell(spell);
                            break;
                    }
                }
                if (client.Entity.Class == 20 || client.Entity.Class == 21 || client.Entity.Class == 22 || client.Entity.Class == 23 || client.Entity.Class == 24 || client.Entity.Class == 25)
                {
                    switch (spell.ID)
                    {
                        case 30000:
                            client.RemoveSpell(spell);
                            break;
                    }
                }
                if (client.Entity.Class == 141 || client.Entity.Class == 142 || client.Entity.Class == 143 || client.Entity.Class == 143 || client.Entity.Class == 144 || client.Entity.Class == 145)
                {
                    switch (spell.ID)
                    {
                        case 30000:
                            client.RemoveSpell(spell);
                            break;
                    }
                }
                if (client.Entity.Class == 70 || client.Entity.Class == 71 || client.Entity.Class == 72 || client.Entity.Class == 73 || client.Entity.Class == 74 || client.Entity.Class == 75)
                {
                    switch (spell.ID)
                    {
                        case 30000:
                            client.RemoveSpell(spell);
                            break;
                    }
                }
                #region _Skill Pure Warrior_

                if (client.Entity.Class / 10 != 2 && client.Entity.FirstRebornClass != 25 || client.Entity.Class / 10 != 2 && client.Entity.SecondRebornClass != 25 || client.Entity.Class / 10 != 2 || client.Entity.FirstRebornClass != 25 || client.Entity.SecondRebornClass != 25)
                {
                    switch (spell.ID)
                    {
                        case 10311:
                            client.RemoveSpell(spell);
                            break;
                    }
                }

                #endregion
                #region _Skill Pure Fire

                if (client.Entity.Class / 10 != 14 && client.Entity.FirstRebornClass != 145 || client.Entity.Class / 10 != 14 && client.Entity.SecondRebornClass != 145 || client.Entity.Class / 10 != 14 || client.Entity.FirstRebornClass != 145 || client.Entity.SecondRebornClass != 145)
                {
                    switch (spell.ID)
                    {
                        case 10310:
                            client.RemoveSpell(spell);
                            break;
                    }
                }

                #endregion
                #region _Skill Pure Ninja_

                if (client.Entity.Class / 10 != 5 && client.Entity.FirstRebornClass != 55 || client.Entity.Class / 10 != 5 && client.Entity.SecondRebornClass != 55 || client.Entity.Class / 10 != 5 || client.Entity.FirstRebornClass != 55 || client.Entity.SecondRebornClass != 55)
                {
                    switch (spell.ID)
                    {
                        case 5003:
                            client.RemoveSpell(spell);
                            break;
                    }
                }

                #endregion
                #region _Skill Pure Monk_
                if (client.Entity.Class / 10 != 6 && client.Entity.FirstRebornClass != 65 || client.Entity.Class / 10 != 6 && client.Entity.SecondRebornClass != 65 || client.Entity.Class / 10 != 6 || client.Entity.FirstRebornClass != 65 || client.Entity.SecondRebornClass != 65)
                {
                    switch (spell.ID)
                    {
                        case 10405:
                            client.RemoveSpell(spell);
                            break;
                    }
                }

                #endregion
                #region _Skill Pure Archer_

                if (client.Entity.Class / 10 != 4 && client.Entity.FirstRebornClass != 45 || client.Entity.Class / 10 != 4 && client.Entity.SecondRebornClass != 45 || client.Entity.Class / 10 != 4 || client.Entity.FirstRebornClass != 45 || client.Entity.SecondRebornClass != 45)
                {
                    switch (spell.ID)
                    {
                        case 10313:
                            client.RemoveSpell(spell);
                            break;
                    }
                }

                #endregion
                #endregion
                if (client.Spells.ContainsKey(spell.ID))
                    if (spell.ID != (ushort)Game.Enums.SkillIDs.Reflect)
                        spell.Send(client);
            }
            #endregion
            Interfaces.ISkill spelss = new Network.GamePackets.Spell(true);
            #endregion
            #region Time Server
            ServerTime time = new ServerTime();
            time.Year = (uint)DateTime.Now.Year;
            time.Month = (uint)DateTime.Now.Month;
            time.DayOfYear = (uint)DateTime.Now.DayOfYear;
            time.DayOfMonth = (uint)DateTime.Now.Day;
            time.Hour = (uint)DateTime.Now.Hour;
            time.Minute = (uint)DateTime.Now.Minute;
            time.Second = (uint)DateTime.Now.Second;
            client.Send(time);
            #endregion
            #region MentorInformation
            if (client.Mentor != null)
            {
                if (client.Mentor.IsOnline)
                {
                    MentorInformation Information = new MentorInformation(true);
                    Information.Mentor_Type = 1;
                    Information.Mentor_ID = client.Mentor.Client.Entity.UID;
                    Information.Apprentice_ID = client.Entity.UID;
                    Information.Enrole_Date = client.Mentor.EnroleDate;
                    Information.Mentor_Level = client.Mentor.Client.Entity.Level;
                    Information.Mentor_Class = client.Mentor.Client.Entity.Class;
                    Information.Mentor_PkPoints = client.Mentor.Client.Entity.PKPoints;
                    Information.Mentor_Mesh = client.Mentor.Client.Entity.Mesh;
                    Information.Mentor_Online = true;
                    Information.Shared_Battle_Power = client.Entity.BattlePowerFrom(client.Mentor.Client.Entity);
                    Information.String_Count = 3;
                    Information.Mentor_Name = client.Mentor.Client.Entity.Name;
                    Information.Apprentice_Name = client.Entity.Name;
                    Information.Mentor_Spouse_Name = client.Mentor.Client.Entity.Spouse;
                    client.ReviewMentor();
                    client.Send(Information);

                    ApprenticeInformation AppInfo = new ApprenticeInformation();
                    AppInfo.Apprentice_ID = client.Entity.UID;
                    AppInfo.Apprentice_Level = client.Entity.Level;
                    AppInfo.Apprentice_Class = client.Entity.Class;
                    AppInfo.Apprentice_PkPoints = client.Entity.PKPoints;
                    AppInfo.Apprentice_Experience = client.AsApprentice.Actual_Experience;
                    AppInfo.Apprentice_Composing = client.AsApprentice.Actual_Plus;
                    AppInfo.Apprentice_Blessing = client.AsApprentice.Actual_HeavenBlessing;
                    AppInfo.Apprentice_Name = client.Entity.Name;
                    AppInfo.Apprentice_Online = true;
                    AppInfo.Apprentice_Spouse_Name = client.Entity.Spouse;
                    AppInfo.Enrole_date = client.Mentor.EnroleDate;
                    AppInfo.Mentor_ID = client.Mentor.ID;
                    AppInfo.Mentor_Mesh = client.Mentor.Client.Entity.Mesh;
                    AppInfo.Mentor_Name = client.Mentor.Name;
                    AppInfo.Type = 2;
                    client.Mentor.Client.Send(AppInfo);
                }
                else
                {
                    MentorInformation Information = new MentorInformation(true);
                    Information.Mentor_Type = 1;
                    Information.Mentor_ID = client.Mentor.ID;
                    Information.Apprentice_ID = client.Entity.UID;
                    Information.Enrole_Date = client.Mentor.EnroleDate;
                    Information.Mentor_Online = false;
                    Information.String_Count = 2;
                    Information.Mentor_Name = client.Mentor.Name;
                    Information.Apprentice_Name = client.Entity.Name;
                    client.Send(Information);
                }
            }
            #endregion
            #region NobilityInfo
            NobilityInfo update = new NobilityInfo(true);
            update.Type = NobilityInfo.Icon;
            update.dwParam = client.NobilityInformation.EntityUID;
            update.UpdateString(client.NobilityInformation);
            client.Send(update);
            #endregion
            #region ChiPowers
            client.Send(new ChiPowers(true).Query(client));
            SendChiRankings(new GenericRanking(true) { Mode = GenericRanking.QueryCount }, client);
            #endregion
            #region Appearance
            if (client.Entity.Appearance != 0)
            {
                client.Send(new Data(true) { UID = client.Entity.UID, ID = Data.AppearanceType, dwParam = (uint)client.Entity.Appearance });
            }
            #endregion
            #region TQPoint
            if (client.TQPoint > 0)
            {
                Attack send = new Attack(true);
                send.AttackType = Attack.ClaimCP;
                client.Send(send.ToArray());
            }
            #endregion
            #region LoginInfo
            /*ServerInfo Info = new ServerInfo();
            Info.ToArray();
            Database.EntityTable.LoginNow(client);*/
            #endregion
            #region AutoHunt
            AutoHunt AutoHunt = new AutoHunt();
            AutoHunt.Icon = 341;
            client.Send(AutoHunt.ToArray());
            #endregion
            #region WentToComplete
            client.Filtering = true;
            if (client.WentToComplete) return;
            RemoveBadSkills(client);
            client.WentToComplete = true;
            client.Entity.SendUpdates = true;
            #endregion
            #region QuizShow
            client.Entity.Update((byte)FlagsUpdate.DataType.QuizPoints, client.Entity.QuizPoints, true);
            #endregion
            #region Equipment
            foreach (ConquerItem item in client.Inventory.Objects)
                item.Send(client);
            foreach (ConquerItem item in client.Equipment.Objects)
            {
                if (item != null)
                {
                    if (Database.ConquerItemInformation.BaseInformations.ContainsKey(item.ID))
                    {
                        if (Game.ConquerStructures.Equipment.IsLoginDeferredSlot(item.Position))
                            continue;
                        item.Send(client);
                    }
                    else
                    {
                        client.Equipment.DestroyArrow(item.Position);
                    }
                }
            }
            if (client.Account != null
                && (client.Account.State == Database.AccountTable.AccountState.GameMaster
                    || client.Account.State == Database.AccountTable.AccountState.Administrador))
            {
                client.Entity.Strength = 500;
                client.Entity.Agility = 500;
                client.Entity.Vitality = 500;
                client.Entity.Spirit = 500;
            }
            client.LoadItemStats();
            if (client.Equipment != null && client.Equipment.Objects != null)
            {
                foreach (ConquerItem worn in client.Equipment.Objects)
                {
                    if (worn != null)
                        worn.IsWorn = true;
                }
            }
            if (client.Equipment != null)
                client.Equipment.HideDeferredSlotsForLoginSpawn();
            client.Send(new ClientEquip(client));
            if (client.Equipment != null)
                client.Equipment.UpdateEntityPacket();
            if (!client.Equipment.Free(5))
            {
                if (ItemHandler.IsArrow(client.Equipment.TryGetItem(5).ID))
                {
                    if (client.Equipment.Free(4))
                        client.Equipment.DestroyArrow(5);
                    else
                    {
                        if (client.Equipment.TryGetItem(4).ID / 1000 != 500)
                            client.Equipment.DestroyArrow(5);
                    }
                }
            }
            client.GemAlgorithm();
            client.CalculateStatBonus();
            client.CalculateHPBonus();
            if (client.Account != null
                && (client.Account.State == Database.AccountTable.AccountState.GameMaster
                    || client.Account.State == Database.AccountTable.AccountState.Administrador))
                client.Entity.Hitpoints = client.Entity.MaxHitpoints;
            client.Entity.Stamina = 100;
            #endregion
            #region WelcomeMessages
            string[] wm = File.ReadAllLines(Constants.WelcomeMessages);
            foreach (string line in wm)
            {
                if (line.Length == 0) continue;
                if (line[0] == ';') continue;
                client.Send(line);
            }
            #endregion
            #region LastLogin
            DateTime lastLogin = client["lastlogin"];
            string prevLoc = client["lastlocation"];

            client.Send(new LastLogin()
            {
                TotalSeconds = (uint)(lastLogin - new DateTime(1970, 1, 1).ToLocalTime()).TotalSeconds,
                DifferentCity = prevLoc == client.Account.IP,
                DifferentPlace = prevLoc != client.Account.IP
            });
            client["lastlogin"] = DateTime.Now;
            client["lastlocation"] = prevLoc;
            #endregion LastLogin
            #region Ultimo ingreso
            /* if (client.Action == 2)
            {
                byte[] Packet = null;
                Packet = new byte[0];
                Packet = new Network.GamePackets.LastLoginAlert(client)
                {
                    LastCheck = (uint)client.Account.LastCheck.Ticks,
                    IP = client.Player.IPLast
                }.
                ToArray();
                client.Send(Packet);
            }*/
            #endregion
            #region VIPLevel
            client.Entity.VIPLevel = (byte)(client.Entity.VIPLevel + 0);
            if (client.Entity.VIPLevel > 0)
            {
                VipStatus vip = new VipStatus();
                client.Send(vip.ToArray());
            }
            #endregion
            #region Sistema de Subscripcion VIP
            if (client.Entity.VIPLevel >= 6 && client.Entity.VIPLevelDays == 0 && client.Entity.VIPDays == 0)
            {
                client.Send(Constants.VIPLifetime);
            }
            if (client.Entity.VIPDays >= 1)
            {
                if (client.Entity.VIPDays >= 1 && client.Entity.VIPLevelDays >= 1)//Establecer Nivel VIP 
                {
                    client.Entity.VIPLevel = client.Entity.VIPLevelDays;
                    client.Send(new Message("You have [" + client.Entity.VIPDays + " days] of VIP " + client.Entity.VIPLevelDays + " remaining.", System.Drawing.Color.Red, Message.World));
                }
            }
            if (client.Entity.VIPDays == 0 || client.Entity.VIPLevelDays == 0)//Borrar si faltan datos 
            {
                client.Entity.VIPDays = 0;
                if (client.Entity.VIPLevel == 5 && client.Entity.VIPLevelDays >= 1)
                {
                    client.Send(Constants.VIPExpired);
                }
            }
            #endregion

            #region MapStatus
            client.Send(new MapStatus() { BaseID = client.Map.BaseID, ID = client.Map.ID, Status = Database.MapsTable.MapInformations[client.Map.ID].Status, Weather = Database.MapsTable.MapInformations[client.Map.ID].Weather });
            if (client.Entity.Hitpoints == 0)
            {
                ushort[] Point = Database.DataHolder.FindReviveSpot(client.Map.ID);
                client.Entity.Teleport(Point[0], Point[1], Point[2]);
                client.Entity.Hitpoints = 1;
            }
            #endregion
            #region Guild
            foreach (var Guild in Kernel.Guilds.Values)
            {
                Guild.SendName(client);
            }
            if (client.Guild != null)
            {
                client.Guild.SendAllyAndEnemy(client);
                client.Guild.getbp = true;
                client.Entity.GuildSharedBp = client.Guild.Arsenal.BattlePower;
            }
            #endregion
            #region Subclass
            SubclassAutoSwitch(client, client.Entity.SubClass);
            SubclassSendInfo(client);
            client.LoadItemStats();
            if (client.Equipment != null)
                client.Equipment.HideDeferredSlotsForLoginSpawn();
            #endregion
            #region MentorBattlePower
            if (client.Entity.MentorBattlePower != 0)
                client.Entity.Update((byte)FlagsUpdate.DataType.ExtraBattlePower, client.Entity.MentorBattlePower, false);
            #endregion
            #region Broadcast
            if (Game.ConquerStructures.Broadcast.CurrentBroadcast.EntityID > 2)
                client.Send(new Message(Game.ConquerStructures.Broadcast.CurrentBroadcast.Message, "ALLUSERS", Game.ConquerStructures.Broadcast.CurrentBroadcast.EntityName, System.Drawing.Color.Red, Message.BroadcastMessage));
            #endregion
            #region timedropSlayer x tiempo
            if (client.Entity.TimeDropSlayer > 0)
            {
                client.Entity.RefreshTimeDropSlayer();
                client.Send("TiempoSlayer Expire: " + DateTime.Now.AddSeconds(client.Entity.TimeDropSlayer).ToString("yyyy:MM-dd:HH"));
            }
            #endregion
            #region BlessTime
            client.Entity.RefreshExpProtection();
            client.Entity.RefreshDoubleExperience();
            client.Entity.HandleTiming = true;
            client.Entity.Update((byte)FlagsUpdate.DataType.RevoraPoints, (ulong)client.Entity.RevoraPoint, false);
            client.Entity.Update((byte)FlagsUpdate.DataType.LuckyTimeTimer, client.BlessTime, false);
            if (client.Entity.HeavenBlessing != 0)
            {
                client.Send("Heaven Blessing Expire: " + DateTime.Now.AddSeconds(client.Entity.HeavenBlessing).ToString("yyyy:MM-dd:HH"));
                client.Entity.Update((byte)FlagsUpdate.DataType.OnlineTraining, client.OnlineTrainingPoints, false);
            }
            #endregion
            #region JiangHu
            Game.JiangHu.SendJiangHu(client);
            #endregion
            #region ClaimableItem
            if (client.ClaimableItem.Count > 0)
            {
                foreach (var item in client.ClaimableItem.Values)
                {
                    Database.ItemAddingTable.GetAddingsForItem(item.Item);
                    item.Send(client);
                    item.Item.SendExtras(client);
                }
            }
            #endregion

            #region DeatinedItem
            if (client.DeatinedItem.Count > 0)
            {
                foreach (var item in client.DeatinedItem.Values)
                {
                    Database.ItemAddingTable.GetAddingsForItem(item.Item);
                    item.Send(client);
                    item.Item.SendExtras(client);
                }
            }
            client.Equipment.UpdateEntityPacket();
            #endregion
            #region BloodshedSea
            if (client.Entity.MapID == 3846)
            {
                Data data = new Data(true);
                data.UID = client.Entity.UID;
                data.dwParam = 3846;
                data.ID = 104;
                data.TimeStamp = Time32.Now;
                data.dwParam = 16755370;
                client.Send(data);
                client.SendScreen(data, true);
                client.Screen.Reload(null);
            }
            #endregion
            #region ExtraInv
            client.Entity.Update((byte)FlagsUpdate.DataType.AvailableSlots, 200, false);
            client.Entity.Update((byte)FlagsUpdate.DataType.ExtraInventory, client.Entity.ExtraInventory, false);
            #endregion

            #region Proteccion Anticheat
            #region Equipamiento
            #region Daño
            #region Chequeo daño de alas / Torro / Abanico / latigo
            #endregion
            #region Chequeo daño de caballo
            //if (!client.Equipment.Free(12) && ItemHandler.IsEquipment(client.Equipment.TryGetItem(12).ID))
            //{
            //    if (client.Equipment.TryGetItem(12).Bless > 0)
            //    {
            //        client.Equipment.Remove(12);
            //        ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
            //        client.Inventory.Objects.CopyTo(inventory, 0);
            //        client.Disconnect();
            //    }
            //}
            #endregion
            #region Chequeo daño de Copa
            if (!client.Equipment.Free(7) && ItemHandler.IsEquipment(client.Equipment.TryGetItem(7).ID))
            {
                if (client.Equipment.TryGetItem(7).Bless > 3)
                {
                    client.Equipment.Remove(7);
                    ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                    client.Inventory.Objects.CopyTo(inventory, 0);
                    client.Disconnect();
                }
            }
            #endregion
            #region Chequeo daño de Frack
            if (!client.Equipment.Free(9) && ItemHandler.IsEquipment(client.Equipment.TryGetItem(9).ID))
            {
                if (client.Equipment.TryGetItem(9).Bless > 3)
                {
                    client.Equipment.Remove(9);
                    ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                    client.Inventory.Objects.CopyTo(inventory, 0);
                    client.Disconnect();
                }
            }
            #endregion
            #region Chequeo daño de FrackCaballo
            if (!client.Equipment.Free(17) && ItemHandler.IsEquipment(client.Equipment.TryGetItem(17).ID))
            {
                if (client.Equipment.TryGetItem(17).Bless > 3)
                {
                    client.Equipment.Remove(17);
                    ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                    client.Inventory.Objects.CopyTo(inventory, 0);
                    client.Disconnect();
                }
            }
            #endregion
            #region Chequeo daño de FrackDeArmaDerecha
            if (!client.Equipment.Free(16) && ItemHandler.IsEquipment(client.Equipment.TryGetItem(16).ID))
            {
                if (client.Equipment.TryGetItem(16).Bless > 3)
                {
                    client.Equipment.Remove(16);
                    ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                    client.Inventory.Objects.CopyTo(inventory, 0);
                    client.Disconnect();
                }
            }
            #endregion
            #region Chequeo daño de FrackDeArmaIzquierda
            if (!client.Equipment.Free(15) && ItemHandler.IsEquipment(client.Equipment.TryGetItem(15).ID))
            {
                if (client.Equipment.TryGetItem(15).Bless > 3)
                {
                    client.Equipment.Remove(15);
                    ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                    client.Inventory.Objects.CopyTo(inventory, 0);
                    client.Disconnect();
                }
            }
            #endregion
            #endregion
            #region HP o encante
            #region Chequeo daño de alas
            if (!client.Equipment.Free(19) && ItemHandler.IsEquipment(client.Equipment.TryGetItem(19).ID))
            {
                if (client.Equipment.TryGetItem(19).Enchant > 0)
                {
                    client.Equipment.Remove(19);
                    ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                    client.Inventory.Objects.CopyTo(inventory, 0);
                    client.Disconnect();
                }
            }
            #endregion
            #region Chequeo daño de Torro
            if (!client.Equipment.Free(11) && ItemHandler.IsEquipment(client.Equipment.TryGetItem(11).ID))
            {
                if (client.Equipment.TryGetItem(11).Enchant > 0)
                {
                    client.Equipment.Remove(11);
                    ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                    client.Inventory.Objects.CopyTo(inventory, 0);
                    client.Disconnect();
                }
            }
            #endregion
            #region Chequeo daño de Abanico
            if (!client.Equipment.Free(10) && ItemHandler.IsEquipment(client.Equipment.TryGetItem(10).ID))
            {
                if (client.Equipment.TryGetItem(10).Enchant > 0)
                {
                    client.Equipment.Remove(10);
                    ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                    client.Inventory.Objects.CopyTo(inventory, 0);
                    client.Disconnect();
                }
            }
            #endregion
            #region Chequeo daño de latigo
            if (!client.Equipment.Free(18) && ItemHandler.IsEquipment(client.Equipment.TryGetItem(18).ID))
            {
                if (client.Equipment.TryGetItem(18).Enchant > 0)
                {
                    client.Equipment.Remove(18);
                    ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                    client.Inventory.Objects.CopyTo(inventory, 0);
                    client.Disconnect();
                }
            }
            #endregion
            #region Chequeo daño de caballo
            //if (!client.Equipment.Free(12) && ItemHandler.IsEquipment(client.Equipment.TryGetItem(12).ID))
            //{
            //    if (client.Equipment.TryGetItem(12).Enchant > 0)
            //    {
            //        client.Equipment.Remove(12);
            //        ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
            //        client.Inventory.Objects.CopyTo(inventory, 0);
            //        client.Disconnect();
            //    }
            //}
            #endregion
            #region Chequeo daño de Copa
            if (!client.Equipment.Free(7) && ItemHandler.IsEquipment(client.Equipment.TryGetItem(7).ID))
            {
                if (client.Equipment.TryGetItem(7).Enchant > 0)
                {
                    client.Equipment.Remove(7);
                    ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                    client.Inventory.Objects.CopyTo(inventory, 0);
                    client.Disconnect();
                }
            }
            #endregion
            #region Chequeo daño de Frack
            if (!client.Equipment.Free(9) && ItemHandler.IsEquipment(client.Equipment.TryGetItem(9).ID))
            {
                if (client.Equipment.TryGetItem(9).Enchant > 0)
                {
                    client.Equipment.Remove(9);
                    ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                    client.Inventory.Objects.CopyTo(inventory, 0);
                    client.Disconnect();
                }
            }
            #endregion
            #region Chequeo daño de FrackCaballo
            if (!client.Equipment.Free(17) && ItemHandler.IsEquipment(client.Equipment.TryGetItem(17).ID))
            {
                if (client.Equipment.TryGetItem(17).Enchant > 0)
                {
                    client.Equipment.Remove(17);
                    ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                    client.Inventory.Objects.CopyTo(inventory, 0);
                    client.Disconnect();
                }
            }
            #endregion
            #region Chequeo daño de FrackDeArmaDerecha
            if (!client.Equipment.Free(16) && ItemHandler.IsEquipment(client.Equipment.TryGetItem(16).ID))
            {
                if (client.Equipment.TryGetItem(16).Enchant > 0)
                {
                    client.Equipment.Remove(16);
                    ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                    client.Inventory.Objects.CopyTo(inventory, 0);
                    client.Disconnect();
                }
            }
            #endregion
            #region Chequeo daño de FrackDeArmaIzquierda
            if (!client.Equipment.Free(15) && ItemHandler.IsEquipment(client.Equipment.TryGetItem(15).ID))
            {
                if (client.Equipment.TryGetItem(15).Enchant > 0)
                {
                    client.Equipment.Remove(15);
                    ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                    client.Inventory.Objects.CopyTo(inventory, 0);
                    client.Disconnect();
                }
            }
            #endregion
            #endregion
            #region plus + 0            
            #region Chequeo daño de Copa
            if (!client.Equipment.Free(7) && ItemHandler.IsEquipment(client.Equipment.TryGetItem(7).ID))
            {
                if (client.Equipment.TryGetItem(7).Plus > 0)
                {
                    client.Equipment.Remove(7);
                    ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                    client.Inventory.Objects.CopyTo(inventory, 0);
                    client.Disconnect();
                }
            }
            #endregion
            #region Chequeo daño de Frack
            if (!client.Equipment.Free(9) && ItemHandler.IsEquipment(client.Equipment.TryGetItem(9).ID))
            {
                if (client.Equipment.TryGetItem(9).Plus > 0)
                {
                    client.Equipment.Remove(9);
                    ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                    client.Inventory.Objects.CopyTo(inventory, 0);
                    client.Disconnect();
                }
            }
            #endregion
            #region Chequeo daño de FrackCaballo
            if (!client.Equipment.Free(17) && ItemHandler.IsEquipment(client.Equipment.TryGetItem(17).ID))
            {
                if (client.Equipment.TryGetItem(17).Plus > 0)
                {
                    client.Equipment.Remove(17);
                    ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                    client.Inventory.Objects.CopyTo(inventory, 0);
                    client.Disconnect();
                }
            }
            #endregion
            #region Chequeo daño de FrackDeArmaDerecha
            if (!client.Equipment.Free(16) && ItemHandler.IsEquipment(client.Equipment.TryGetItem(16).ID))
            {
                if (client.Equipment.TryGetItem(16).Plus > 0)
                {
                    client.Equipment.Remove(16);
                    ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                    client.Inventory.Objects.CopyTo(inventory, 0);
                    client.Disconnect();
                }
            }
            #endregion
            #region Chequeo daño de FrackDeArmaIzquierda
            if (!client.Equipment.Free(15) && ItemHandler.IsEquipment(client.Equipment.TryGetItem(15).ID))
            {
                if (client.Equipment.TryGetItem(15).Plus > 0)
                {
                    client.Equipment.Remove(15);
                    ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                    client.Inventory.Objects.CopyTo(inventory, 0);
                    client.Disconnect();
                }
            }
            #endregion
            #endregion
            #region Sockalos
            #region Chequeo daño de latigo
            if (!client.Equipment.Free(18) && ItemHandler.IsEquipment(client.Equipment.TryGetItem(18).ID))
            {
                if (client.Equipment.TryGetItem(18).SocketOne == Game.Enums.Gem.EmptySocket || client.Equipment.TryGetItem(18).SocketTwo == Game.Enums.Gem.EmptySocket)
                {
                    client.Equipment.Remove(18);
                    ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                    client.Inventory.Objects.CopyTo(inventory, 0);
                    client.Disconnect();
                }
                if (client.Equipment.TryGetItem(18).SocketOne > 0 || client.Equipment.TryGetItem(18).SocketTwo > 0)
                {
                    client.Equipment.Remove(18);
                    ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                    client.Inventory.Objects.CopyTo(inventory, 0);
                    client.Disconnect();
                }
            }
            #endregion
            #region Chequeo daño de caballo
            if (!client.Equipment.Free(12) && ItemHandler.IsEquipment(client.Equipment.TryGetItem(12).ID))
            {
                if (client.Equipment.TryGetItem(12).SocketOne == Game.Enums.Gem.EmptySocket || client.Equipment.TryGetItem(12).SocketTwo == Game.Enums.Gem.EmptySocket)
                {
                    client.Equipment.Remove(12);
                    ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                    client.Inventory.Objects.CopyTo(inventory, 0);
                    client.Disconnect();
                }
                if (client.Equipment.TryGetItem(12).SocketOne > 0 || client.Equipment.TryGetItem(12).SocketTwo > 0)
                {
                    client.Equipment.Remove(12);
                    ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                    client.Inventory.Objects.CopyTo(inventory, 0);
                    client.Disconnect();
                }
            }
            #endregion
            #region Chequeo daño de Copa
            if (!client.Equipment.Free(7) && ItemHandler.IsEquipment(client.Equipment.TryGetItem(7).ID))
            {
                if (client.Equipment.TryGetItem(7).SocketOne == Game.Enums.Gem.EmptySocket || client.Equipment.TryGetItem(7).SocketTwo == Game.Enums.Gem.EmptySocket)
                {
                    client.Equipment.Remove(7);
                    ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                    client.Inventory.Objects.CopyTo(inventory, 0);
                    client.Disconnect();
                }
                if (client.Equipment.TryGetItem(7).SocketOne > 0 || client.Equipment.TryGetItem(7).SocketTwo > 0)
                {
                    client.Equipment.Remove(7);
                    ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                    client.Inventory.Objects.CopyTo(inventory, 0);
                    client.Disconnect();
                }
            }
            #endregion
            #region Chequeo daño de Frack
            if (!client.Equipment.Free(9) && ItemHandler.IsEquipment(client.Equipment.TryGetItem(9).ID))
            {
                if (client.Equipment.TryGetItem(9).SocketOne == Game.Enums.Gem.EmptySocket || client.Equipment.TryGetItem(9).SocketTwo == Game.Enums.Gem.EmptySocket)
                {
                    client.Equipment.Remove(9);
                    ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                    client.Inventory.Objects.CopyTo(inventory, 0);
                    client.Disconnect();
                }
                if (client.Equipment.TryGetItem(9).SocketOne > 0 || client.Equipment.TryGetItem(9).SocketTwo > 0)
                {
                    client.Equipment.Remove(9);
                    ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                    client.Inventory.Objects.CopyTo(inventory, 0);
                    client.Disconnect();
                }
            }
            #endregion
            #region Chequeo daño de FrackCaballo
            if (!client.Equipment.Free(17) && ItemHandler.IsEquipment(client.Equipment.TryGetItem(17).ID))
            {
                if (client.Equipment.TryGetItem(17).SocketOne == Game.Enums.Gem.EmptySocket || client.Equipment.TryGetItem(17).SocketTwo == Game.Enums.Gem.EmptySocket)
                {
                    client.Equipment.Remove(17);
                    ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                    client.Inventory.Objects.CopyTo(inventory, 0);
                    client.Disconnect();
                }
                if (client.Equipment.TryGetItem(17).SocketOne > 0 || client.Equipment.TryGetItem(17).SocketTwo > 0)
                {
                    client.Equipment.Remove(17);
                    ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                    client.Inventory.Objects.CopyTo(inventory, 0);
                    client.Disconnect();
                }
            }
            #endregion
            #region Chequeo daño de FrackDeArmaDerecha
            if (!client.Equipment.Free(16) && ItemHandler.IsEquipment(client.Equipment.TryGetItem(16).ID))
            {
                if (client.Equipment.TryGetItem(16).SocketOne == Game.Enums.Gem.EmptySocket || client.Equipment.TryGetItem(16).SocketTwo == Game.Enums.Gem.EmptySocket)
                {
                    client.Equipment.Remove(16);
                    ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                    client.Inventory.Objects.CopyTo(inventory, 0);
                    client.Disconnect();
                }
                if (client.Equipment.TryGetItem(16).SocketOne > 0|| client.Equipment.TryGetItem(16).SocketTwo > 0)
                {
                    client.Equipment.Remove(16);
                    ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                    client.Inventory.Objects.CopyTo(inventory, 0);
                    client.Disconnect();
                }
            }
            #endregion
            #region Chequeo daño de FrackDeArmaIzquierda
            if (!client.Equipment.Free(15) && ItemHandler.IsEquipment(client.Equipment.TryGetItem(15).ID))
            {
                if (client.Equipment.TryGetItem(15).SocketOne == Game.Enums.Gem.EmptySocket || client.Equipment.TryGetItem(15).SocketTwo == Game.Enums.Gem.EmptySocket)
                {
                    client.Equipment.Remove(15);
                    ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                    client.Inventory.Objects.CopyTo(inventory, 0);
                    client.Disconnect();
                }
                if (client.Equipment.TryGetItem(15).SocketOne > 0 || client.Equipment.TryGetItem(15).SocketTwo > 0)
                {
                    client.Equipment.Remove(15);
                    ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                    client.Inventory.Objects.CopyTo(inventory, 0);
                    client.Disconnect();
                }
            }
            #endregion
            #endregion
            #region Gemas
            #region Chequeo Gemas en alas
            if (!client.Equipment.Free(19) && ItemHandler.IsEquipment(client.Equipment.TryGetItem(19).ID))
            {
                if (client.Equipment.TryGetItem(19).SocketOne == Game.Enums.Gem.SuperGloryGem || client.Equipment.TryGetItem(19).SocketOne == Game.Enums.Gem.RefinedGloryGem || client.Equipment.TryGetItem(19).SocketOne == Game.Enums.Gem.NormalGloryGem ||   
                        client.Equipment.TryGetItem(19).SocketOne == Game.Enums.Gem.NormalDragonGem || client.Equipment.TryGetItem(19).SocketOne == Game.Enums.Gem.NormalFuryGem || client.Equipment.TryGetItem(19).SocketOne == Game.Enums.Gem.NormalKylinGem ||
                        client.Equipment.TryGetItem(19).SocketOne == Game.Enums.Gem.NormalMoonGem || client.Equipment.TryGetItem(19).SocketOne == Game.Enums.Gem.NormalPhoenixGem || client.Equipment.TryGetItem(19).SocketOne == Game.Enums.Gem.NormalRainbowGem ||
                        client.Equipment.TryGetItem(19).SocketOne == Game.Enums.Gem.NormalTortoiseGem || client.Equipment.TryGetItem(19).SocketOne == Game.Enums.Gem.NormalVioletGem ||
                        client.Equipment.TryGetItem(19).SocketOne == Game.Enums.Gem.RefinedFuryGem || client.Equipment.TryGetItem(19).SocketOne == Game.Enums.Gem.RefinedKylinGem || client.Equipment.TryGetItem(19).SocketOne == Game.Enums.Gem.RefinedMoonGem ||
                        client.Equipment.TryGetItem(19).SocketOne == Game.Enums.Gem.RefinedPhoenixGem || client.Equipment.TryGetItem(19).SocketOne == Game.Enums.Gem.RefinedRainbowGem ||
                        client.Equipment.TryGetItem(19).SocketOne == Game.Enums.Gem.RefinedTortoiseGem || client.Equipment.TryGetItem(19).SocketOne == Game.Enums.Gem.RefinedVioletGem || client.Equipment.TryGetItem(19).SocketOne == Game.Enums.Gem.SuperDragonGem ||
                        client.Equipment.TryGetItem(19).SocketOne == Game.Enums.Gem.SuperFuryGem || client.Equipment.TryGetItem(19).SocketOne == Game.Enums.Gem.SuperKylinGem || client.Equipment.TryGetItem(19).SocketOne == Game.Enums.Gem.SuperMoonGem ||
                        client.Equipment.TryGetItem(19).SocketOne == Game.Enums.Gem.SuperPhoenixGem || client.Equipment.TryGetItem(19).SocketOne == Game.Enums.Gem.SuperRainbowGem ||
                        client.Equipment.TryGetItem(19).SocketOne == Game.Enums.Gem.SuperTortoiseGem || client.Equipment.TryGetItem(19).SocketOne == Game.Enums.Gem.SuperVioletGem || client.Equipment.TryGetItem(19).SocketTwo == Game.Enums.Gem.NormalDragonGem ||
                        client.Equipment.TryGetItem(19).SocketTwo == Game.Enums.Gem.NormalFuryGem || client.Equipment.TryGetItem(19).SocketTwo == Game.Enums.Gem.NormalKylinGem || client.Equipment.TryGetItem(19).SocketTwo == Game.Enums.Gem.NormalMoonGem ||
                        client.Equipment.TryGetItem(19).SocketTwo == Game.Enums.Gem.NormalPhoenixGem || client.Equipment.TryGetItem(19).SocketTwo == Game.Enums.Gem.NormalRainbowGem ||
                        client.Equipment.TryGetItem(19).SocketTwo == Game.Enums.Gem.NormalTortoiseGem || client.Equipment.TryGetItem(19).SocketTwo == Game.Enums.Gem.NormalVioletGem || client.Equipment.TryGetItem(19).SocketTwo == Game.Enums.Gem.RefinedFuryGem ||
                        client.Equipment.TryGetItem(19).SocketTwo == Game.Enums.Gem.RefinedKylinGem || client.Equipment.TryGetItem(19).SocketTwo == Game.Enums.Gem.RefinedMoonGem || client.Equipment.TryGetItem(19).SocketTwo == Game.Enums.Gem.RefinedPhoenixGem ||
                        client.Equipment.TryGetItem(19).SocketTwo == Game.Enums.Gem.RefinedRainbowGem || client.Equipment.TryGetItem(19).SocketTwo == Game.Enums.Gem.RefinedTortoiseGem ||
                        client.Equipment.TryGetItem(19).SocketTwo == Game.Enums.Gem.RefinedVioletGem || client.Equipment.TryGetItem(19).SocketTwo == Game.Enums.Gem.SuperDragonGem || client.Equipment.TryGetItem(19).SocketTwo == Game.Enums.Gem.SuperFuryGem ||
                        client.Equipment.TryGetItem(19).SocketTwo == Game.Enums.Gem.SuperKylinGem || client.Equipment.TryGetItem(19).SocketTwo == Game.Enums.Gem.SuperMoonGem || client.Equipment.TryGetItem(19).SocketTwo == Game.Enums.Gem.SuperPhoenixGem ||
                        client.Equipment.TryGetItem(19).SocketTwo == Game.Enums.Gem.SuperRainbowGem || client.Equipment.TryGetItem(19).SocketTwo == Game.Enums.Gem.SuperTortoiseGem ||
                        client.Equipment.TryGetItem(19).SocketTwo == Game.Enums.Gem.SuperThunderGem || client.Equipment.TryGetItem(19).SocketTwo == Game.Enums.Gem.RefinedThunderGem || client.Equipment.TryGetItem(19).SocketTwo == Game.Enums.Gem.NormalThunderGem ||                        
                        client.Equipment.TryGetItem(19).SocketTwo == Game.Enums.Gem.SuperVioletGem)
                {
                    client.Equipment.Remove(19);
                    ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                    client.Inventory.Objects.CopyTo(inventory, 0);                    
                    client.Disconnect();
                }
            }
            #endregion
            #region Chequeo Gemas en Torro
            if (!client.Equipment.Free(11) && ItemHandler.IsEquipment(client.Equipment.TryGetItem(11).ID))
            {
                if (client.Equipment.TryGetItem(11).SocketOne == Game.Enums.Gem.NormalDragonGem || client.Equipment.TryGetItem(11).SocketOne == Game.Enums.Gem.NormalFuryGem || client.Equipment.TryGetItem(11).SocketOne == Game.Enums.Gem.NormalKylinGem ||
                        client.Equipment.TryGetItem(11).SocketOne == Game.Enums.Gem.NormalMoonGem || client.Equipment.TryGetItem(11).SocketOne == Game.Enums.Gem.NormalPhoenixGem || client.Equipment.TryGetItem(11).SocketOne == Game.Enums.Gem.NormalRainbowGem ||
                        client.Equipment.TryGetItem(11).SocketOne == Game.Enums.Gem.NormalThunderGem || client.Equipment.TryGetItem(11).SocketOne == Game.Enums.Gem.NormalTortoiseGem || client.Equipment.TryGetItem(11).SocketOne == Game.Enums.Gem.NormalVioletGem ||
                        client.Equipment.TryGetItem(11).SocketOne == Game.Enums.Gem.RefinedFuryGem || client.Equipment.TryGetItem(11).SocketOne == Game.Enums.Gem.RefinedKylinGem || client.Equipment.TryGetItem(11).SocketOne == Game.Enums.Gem.RefinedMoonGem ||
                        client.Equipment.TryGetItem(11).SocketOne == Game.Enums.Gem.RefinedPhoenixGem || client.Equipment.TryGetItem(11).SocketOne == Game.Enums.Gem.RefinedRainbowGem || client.Equipment.TryGetItem(11).SocketOne == Game.Enums.Gem.RefinedThunderGem ||
                        client.Equipment.TryGetItem(11).SocketOne == Game.Enums.Gem.RefinedTortoiseGem || client.Equipment.TryGetItem(11).SocketOne == Game.Enums.Gem.RefinedVioletGem || client.Equipment.TryGetItem(11).SocketOne == Game.Enums.Gem.SuperDragonGem ||
                        client.Equipment.TryGetItem(11).SocketOne == Game.Enums.Gem.SuperFuryGem || client.Equipment.TryGetItem(11).SocketOne == Game.Enums.Gem.SuperKylinGem || client.Equipment.TryGetItem(11).SocketOne == Game.Enums.Gem.SuperMoonGem ||
                        client.Equipment.TryGetItem(11).SocketOne == Game.Enums.Gem.SuperPhoenixGem || client.Equipment.TryGetItem(11).SocketOne == Game.Enums.Gem.SuperRainbowGem || client.Equipment.TryGetItem(11).SocketOne == Game.Enums.Gem.SuperThunderGem ||
                        client.Equipment.TryGetItem(11).SocketOne == Game.Enums.Gem.SuperTortoiseGem || client.Equipment.TryGetItem(11).SocketOne == Game.Enums.Gem.SuperVioletGem || client.Equipment.TryGetItem(11).SocketTwo == Game.Enums.Gem.NormalDragonGem ||

                        client.Equipment.TryGetItem(11).SocketTwo == Game.Enums.Gem.NormalFuryGem || client.Equipment.TryGetItem(11).SocketTwo == Game.Enums.Gem.NormalKylinGem || client.Equipment.TryGetItem(11).SocketTwo == Game.Enums.Gem.NormalMoonGem ||
                        client.Equipment.TryGetItem(11).SocketTwo == Game.Enums.Gem.NormalPhoenixGem || client.Equipment.TryGetItem(11).SocketTwo == Game.Enums.Gem.NormalRainbowGem || client.Equipment.TryGetItem(11).SocketTwo == Game.Enums.Gem.NormalThunderGem ||
                        client.Equipment.TryGetItem(11).SocketTwo == Game.Enums.Gem.NormalTortoiseGem || client.Equipment.TryGetItem(11).SocketTwo == Game.Enums.Gem.NormalVioletGem || client.Equipment.TryGetItem(11).SocketTwo == Game.Enums.Gem.RefinedFuryGem ||
                        client.Equipment.TryGetItem(11).SocketTwo == Game.Enums.Gem.RefinedKylinGem || client.Equipment.TryGetItem(11).SocketTwo == Game.Enums.Gem.RefinedMoonGem || client.Equipment.TryGetItem(11).SocketTwo == Game.Enums.Gem.RefinedPhoenixGem ||
                        client.Equipment.TryGetItem(11).SocketTwo == Game.Enums.Gem.RefinedRainbowGem || client.Equipment.TryGetItem(11).SocketTwo == Game.Enums.Gem.RefinedThunderGem || client.Equipment.TryGetItem(11).SocketTwo == Game.Enums.Gem.RefinedTortoiseGem ||
                        client.Equipment.TryGetItem(11).SocketTwo == Game.Enums.Gem.RefinedVioletGem || client.Equipment.TryGetItem(11).SocketTwo == Game.Enums.Gem.SuperDragonGem || client.Equipment.TryGetItem(11).SocketTwo == Game.Enums.Gem.SuperFuryGem ||
                        client.Equipment.TryGetItem(11).SocketTwo == Game.Enums.Gem.SuperKylinGem || client.Equipment.TryGetItem(11).SocketTwo == Game.Enums.Gem.SuperMoonGem || client.Equipment.TryGetItem(11).SocketTwo == Game.Enums.Gem.SuperPhoenixGem ||
                        client.Equipment.TryGetItem(11).SocketTwo == Game.Enums.Gem.SuperRainbowGem || client.Equipment.TryGetItem(11).SocketTwo == Game.Enums.Gem.SuperThunderGem || client.Equipment.TryGetItem(11).SocketTwo == Game.Enums.Gem.SuperTortoiseGem ||
                        client.Equipment.TryGetItem(11).SocketTwo == Game.Enums.Gem.SuperVioletGem)
                {
                    client.Equipment.Remove(11);
                    ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                    client.Inventory.Objects.CopyTo(inventory, 0);
                    client.Disconnect();
                }
            }
            #endregion
            #region Chequeo Gemas en Abanico
            if (!client.Equipment.Free(10) && ItemHandler.IsEquipment(client.Equipment.TryGetItem(10).ID))
            {
                if (client.Equipment.TryGetItem(10).SocketOne == Game.Enums.Gem.NormalDragonGem || client.Equipment.TryGetItem(10).SocketOne == Game.Enums.Gem.NormalFuryGem || client.Equipment.TryGetItem(10).SocketOne == Game.Enums.Gem.NormalKylinGem ||
                        client.Equipment.TryGetItem(10).SocketOne == Game.Enums.Gem.NormalMoonGem || client.Equipment.TryGetItem(10).SocketOne == Game.Enums.Gem.NormalPhoenixGem || client.Equipment.TryGetItem(10).SocketOne == Game.Enums.Gem.NormalRainbowGem ||
                        client.Equipment.TryGetItem(10).SocketOne == Game.Enums.Gem.NormalGloryGem || client.Equipment.TryGetItem(10).SocketOne == Game.Enums.Gem.NormalTortoiseGem || client.Equipment.TryGetItem(10).SocketOne == Game.Enums.Gem.NormalVioletGem ||
                        client.Equipment.TryGetItem(10).SocketOne == Game.Enums.Gem.RefinedFuryGem || client.Equipment.TryGetItem(10).SocketOne == Game.Enums.Gem.RefinedKylinGem || client.Equipment.TryGetItem(10).SocketOne == Game.Enums.Gem.RefinedMoonGem ||
                        client.Equipment.TryGetItem(10).SocketOne == Game.Enums.Gem.RefinedPhoenixGem || client.Equipment.TryGetItem(10).SocketOne == Game.Enums.Gem.RefinedRainbowGem || client.Equipment.TryGetItem(10).SocketOne == Game.Enums.Gem.RefinedGloryGem ||
                        client.Equipment.TryGetItem(10).SocketOne == Game.Enums.Gem.RefinedTortoiseGem || client.Equipment.TryGetItem(10).SocketOne == Game.Enums.Gem.RefinedVioletGem || client.Equipment.TryGetItem(10).SocketOne == Game.Enums.Gem.SuperDragonGem ||
                        client.Equipment.TryGetItem(10).SocketOne == Game.Enums.Gem.SuperFuryGem || client.Equipment.TryGetItem(10).SocketOne == Game.Enums.Gem.SuperKylinGem || client.Equipment.TryGetItem(10).SocketOne == Game.Enums.Gem.SuperMoonGem ||
                        client.Equipment.TryGetItem(10).SocketOne == Game.Enums.Gem.SuperPhoenixGem || client.Equipment.TryGetItem(10).SocketOne == Game.Enums.Gem.SuperRainbowGem || client.Equipment.TryGetItem(10).SocketOne == Game.Enums.Gem.SuperGloryGem ||
                        client.Equipment.TryGetItem(10).SocketOne == Game.Enums.Gem.SuperTortoiseGem || client.Equipment.TryGetItem(10).SocketOne == Game.Enums.Gem.SuperVioletGem || client.Equipment.TryGetItem(10).SocketTwo == Game.Enums.Gem.NormalDragonGem ||
                        client.Equipment.TryGetItem(10).SocketTwo == Game.Enums.Gem.NormalFuryGem || client.Equipment.TryGetItem(10).SocketTwo == Game.Enums.Gem.NormalKylinGem || client.Equipment.TryGetItem(10).SocketTwo == Game.Enums.Gem.NormalMoonGem ||
                        client.Equipment.TryGetItem(10).SocketTwo == Game.Enums.Gem.NormalPhoenixGem || client.Equipment.TryGetItem(10).SocketTwo == Game.Enums.Gem.NormalRainbowGem || client.Equipment.TryGetItem(10).SocketTwo == Game.Enums.Gem.NormalGloryGem ||
                        client.Equipment.TryGetItem(10).SocketTwo == Game.Enums.Gem.NormalTortoiseGem || client.Equipment.TryGetItem(10).SocketTwo == Game.Enums.Gem.NormalVioletGem || client.Equipment.TryGetItem(10).SocketTwo == Game.Enums.Gem.RefinedFuryGem ||
                        client.Equipment.TryGetItem(10).SocketTwo == Game.Enums.Gem.RefinedKylinGem || client.Equipment.TryGetItem(10).SocketTwo == Game.Enums.Gem.RefinedMoonGem || client.Equipment.TryGetItem(10).SocketTwo == Game.Enums.Gem.RefinedPhoenixGem ||
                        client.Equipment.TryGetItem(10).SocketTwo == Game.Enums.Gem.RefinedRainbowGem || client.Equipment.TryGetItem(10).SocketTwo == Game.Enums.Gem.RefinedGloryGem || client.Equipment.TryGetItem(10).SocketTwo == Game.Enums.Gem.RefinedTortoiseGem ||
                        client.Equipment.TryGetItem(10).SocketTwo == Game.Enums.Gem.RefinedVioletGem || client.Equipment.TryGetItem(10).SocketTwo == Game.Enums.Gem.SuperDragonGem || client.Equipment.TryGetItem(10).SocketTwo == Game.Enums.Gem.SuperFuryGem ||
                        client.Equipment.TryGetItem(10).SocketTwo == Game.Enums.Gem.SuperKylinGem || client.Equipment.TryGetItem(10).SocketTwo == Game.Enums.Gem.SuperMoonGem || client.Equipment.TryGetItem(10).SocketTwo == Game.Enums.Gem.SuperPhoenixGem ||
                        client.Equipment.TryGetItem(10).SocketTwo == Game.Enums.Gem.SuperRainbowGem || client.Equipment.TryGetItem(10).SocketTwo == Game.Enums.Gem.SuperGloryGem || client.Equipment.TryGetItem(10).SocketTwo == Game.Enums.Gem.SuperTortoiseGem ||
                        client.Equipment.TryGetItem(10).SocketTwo == Game.Enums.Gem.SuperVioletGem)
                {
                    client.Equipment.Remove(10);
                    ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                    client.Inventory.Objects.CopyTo(inventory, 0);
                    client.Disconnect();
                }
            }
            #endregion
            #endregion

            #region Reparacion de Perfeccion Jheisson
            #region Perfeccion
            #region Chequeo AlternateHead

            if (!client.Equipment.Free(21) && ItemHandler.IsEquipment(client.Equipment.TryGetItem(21).ID))
            {
                if (client.Equipment.TryGetItem(21).PerfectionLevel > 0)
                {
                    client.Equipment.Remove(21);
                    ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                    client.Inventory.Objects.CopyTo(inventory, 0);
                    client.Disconnect();
                }
            }
            #endregion
            #region Chequeo AlternateNecklace
            if (!client.Equipment.Free(22) && ItemHandler.IsEquipment(client.Equipment.TryGetItem(22).ID))
            {
                if (client.Equipment.TryGetItem(22).PerfectionLevel > 0)
                {
                    client.Equipment.Remove(22);
                    ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                    client.Inventory.Objects.CopyTo(inventory, 0);
                    client.Disconnect();
                }
            }
            #endregion
            #region Chequeo AlternateArmor
            if (!client.Equipment.Free(23) && ItemHandler.IsEquipment(client.Equipment.TryGetItem(23).ID))
            {
                if (client.Equipment.TryGetItem(23).PerfectionLevel > 0)
                {
                    client.Equipment.Remove(23);
                    ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                    client.Inventory.Objects.CopyTo(inventory, 0);
                    client.Disconnect();
                }
            }
            #endregion
            #region Chequeo AlternateRightWeapon
            if (!client.Equipment.Free(24) && ItemHandler.IsEquipment(client.Equipment.TryGetItem(24).ID))
            {
                if (client.Equipment.TryGetItem(24).PerfectionLevel > 0)
                {
                    client.Equipment.Remove(24);
                    ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                    client.Inventory.Objects.CopyTo(inventory, 0);
                    client.Disconnect();
                }
            }
            #endregion
            #region Chequeo AlternateLeftWeapon
            if (!client.Equipment.Free(25) && ItemHandler.IsEquipment(client.Equipment.TryGetItem(25).ID))
            {
                if (client.Equipment.TryGetItem(25).PerfectionLevel > 0)
                {
                    client.Equipment.Remove(25);
                    ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                    client.Inventory.Objects.CopyTo(inventory, 0);
                    client.Disconnect();
                }
            }
            #endregion
            #region Chequeo AlternateRing
            if (!client.Equipment.Free(26) && ItemHandler.IsEquipment(client.Equipment.TryGetItem(26).ID))
            {
                if (client.Equipment.TryGetItem(26).PerfectionLevel > 0)
                {
                    client.Equipment.Remove(26);
                    ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                    client.Inventory.Objects.CopyTo(inventory, 0);
                    client.Disconnect();
                }
            }
            #endregion
            #region Chequeo AlternateBottle
            if (!client.Equipment.Free(27) && ItemHandler.IsEquipment(client.Equipment.TryGetItem(27).ID))
            {
                if (client.Equipment.TryGetItem(27).PerfectionLevel > 0)
                {
                    client.Equipment.Remove(27);
                    ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                    client.Inventory.Objects.CopyTo(inventory, 0);
                    client.Disconnect();
                }
            }
            #endregion
            #region Chequeo AlternateBoots
            if (!client.Equipment.Free(28) && ItemHandler.IsEquipment(client.Equipment.TryGetItem(28).ID))
            {
                if (client.Equipment.TryGetItem(28).PerfectionLevel > 0)
                {
                    client.Equipment.Remove(28);
                    ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                    client.Inventory.Objects.CopyTo(inventory, 0);
                    client.Disconnect();
                }
            }
            #endregion
            #region Chequeo AlternateGarment
            if (!client.Equipment.Free(29) && ItemHandler.IsEquipment(client.Equipment.TryGetItem(29).ID))
            {
                if (client.Equipment.TryGetItem(29).PerfectionLevel > 0)
                {
                    client.Equipment.Remove(29);
                    ConquerItem[] inventory = new ConquerItem[client.Inventory.Objects.Length];
                    client.Inventory.Objects.CopyTo(inventory, 0);
                    client.Disconnect();
                }
            }
            #endregion

            #endregion
            #endregion
            
            
            #endregion

            #endregion
            /// فحص السبيس
            #region AutoChequeo de Cps y oro
            
           
            // Chequeo Cps
            if (client.Entity.ConquerPoints > 2000000001)
            {
                client.Entity.ConquerPoints = 2000000000;
                Console.WriteLine("" + client.Entity.Name + " He have 2kk cps Hack******");
            }
            #endregion

        }
    }
}
