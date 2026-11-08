using System;
using System.Drawing;
using System.Linq;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;

namespace Revora.Game.Features
{
    public static class WelcomeGuideSystem
    {
        public const uint NpcId = 900000002;
        public const byte GiftLevel = 70;
        public const byte ProficiencyLevel = 12;
        public const uint ArrowId = 1050021;
        public const int ArrowStacks = 5;
        public const uint ScrollId = 1060020;
        public const int ScrollCount = 3;
        public const uint BlessSeconds = 7u * 24u * 60u * 60u;
        public const string ClaimKey = "welcome-kit";

        private static readonly uint[] ExtraIds = new uint[] { 203009, 204009, 300000, ScrollId, ArrowId };

        public static void Create()
        {
            CityGuardSystem.SpawnTalker(NpcId, 1007, 25, 28, 51030, "Anwen");
            if (Kernel.Maps.ContainsKey(1007) && Kernel.Maps[1007].Npcs.ContainsKey(NpcId))
                Console.WriteLine("Anwen at Birth Isle 1007 25,28");
            else
                Console.WriteLine("Anwen spawn skipped: map 1007 not ready");
        }

        public static bool HandleNpc(GameState client, NpcRequest request)
        {
            if (client == null || request == null || client.Entity == null || client.ActiveNpc != NpcId)
                return false;
            Npcs dialog = new Npcs(client);
            switch (request.OptionID)
            {
                case 0:
                    if (AlreadyClaimed(client))
                    {
                        dialog.Text("I already armed you, " + client.Entity.Name + ". Twin City waits. Use a Twin City scroll, or I can send you there.");
                        dialog.Option("Send me to Twin City", 30);
                        dialog.Option("I will stay.", 255);
                        dialog.Send();
                        return true;
                    }
                    if (ClassAtSeventy(client.Entity.Class) == 0)
                    {
                        dialog.Text("I do not know your path. Return after you have a class.");
                        dialog.Option("I see.", 255);
                        dialog.Send();
                        return true;
                    }
                    dialog.Text("I am Anwen. You are a " + ClassName(client.Entity.Class) + ". I will raise you to level 70 on that path and give your bound set: plus 6, minus 1, damage gems. No fan or tower. You also receive a crop, a wing, a steed, class arts, seven days of Heaven Blessing, gold, and Twin City scrolls. Then I send you to Twin City. Once only.");
                    dialog.Option("Arm me", 5);
                    dialog.Option("Goodbye", 255);
                    dialog.Send();
                    return true;
                case 5:
                    ApplyGift(client, dialog);
                    return true;
                case 30:
                    client.Entity.Teleport(1002, 408, 330);
                    return true;
            }
            return true;
        }

        private static string ClassName(byte cls)
        {
            switch (cls / 10)
            {
                case 1: return "Trojan";
                case 2: return "Warrior";
                case 4: return "Archer";
                case 5: return "Ninja";
                case 6: return "Monk";
                case 7: return "Pirate";
                case 8: return "Dragon Warrior";
                case 10: return "Taoist";
                case 13: return "Water Taoist";
                case 14: return "Fire Taoist";
                case 16: return "Windwalker";
                default: return "adventurer";
            }
        }

        private static byte ClassAtSeventy(byte cls)
        {
            byte target;
            switch (cls / 10)
            {
                case 1: target = 13; break;
                case 2: target = 23; break;
                case 4: target = 43; break;
                case 5: target = 53; break;
                case 6: target = 63; break;
                case 7: target = 73; break;
                case 8: target = 83; break;
                case 10: target = 101; break;
                case 13: target = 133; break;
                case 14: target = 143; break;
                case 16: target = 163; break;
                default: return 0;
            }
            if (cls > target)
                return cls;
            return target;
        }

        private static bool AlreadyClaimed(GameState client)
        {
            if (client.Variables == null)
                return false;
            return (uint)client[ClaimKey] != 0;
        }

        private static void ApplyGift(GameState client, Npcs dialog)
        {
            if (client.Entity.Dead)
            {
                dialog.Text("Rise first.");
                dialog.Option("I see.", 255);
                dialog.Send();
                return;
            }
            if (AlreadyClaimed(client))
            {
                dialog.Text("I already armed you. I will not do it twice.");
                dialog.Option("Send me to Twin City", 30);
                dialog.Option("I see.", 255);
                dialog.Send();
                return;
            }
            byte cls = ClassAtSeventy(client.Entity.Class);
            if (cls == 0)
            {
                dialog.Text("I do not know your path. Return after you have a class.");
                dialog.Option("I see.", 255);
                dialog.Send();
                return;
            }
            uint[] kit = KitIds(cls);
            if (kit == null)
            {
                dialog.Text("I have no set for that path.");
                dialog.Option("I see.", 255);
                dialog.Send();
                return;
            }
            bool archer = cls / 10 == 4;
            int need = kit.Length + 3 + ScrollCount;
            if (archer)
                need += ArrowStacks;
            if (client.Inventory == null || client.Inventory.Count + need > 40)
            {
                dialog.Text("Clear " + need + " inventory spaces first.");
                dialog.Option("I see.", 255);
                dialog.Send();
                return;
            }
            for (int i = 0; i < kit.Length; i++)
            {
                if (!ConquerItemInformation.BaseInformations.ContainsKey(kit[i]))
                {
                    dialog.Text("That set is not in this world's catalog.");
                    dialog.Option("I see.", 255);
                    dialog.Send();
                    return;
                }
            }
            for (int i = 0; i < ExtraIds.Length; i++)
            {
                if (ExtraIds[i] == ArrowId && !archer)
                    continue;
                if (!ConquerItemInformation.BaseInformations.ContainsKey(ExtraIds[i]))
                {
                    dialog.Text("A crop, wing, steed, scroll, or arrow is missing from this world's catalog.");
                    dialog.Option("I see.", 255);
                    dialog.Send();
                    return;
                }
            }
            try
            {
                client[ClaimKey] = 1;
                RaiseLevel(client, cls);
                TeachClassArts(client);
                bool taoist = cls / 10 == 10 || cls / 10 == 13 || cls / 10 == 14;
                Enums.Gem gem = taoist ? Enums.Gem.SuperPhoenixGem : Enums.Gem.SuperDragonGem;
                for (int i = 0; i < kit.Length; i++)
                    GiveGear(client, kit[i], gem);
                GiveGear(client, 203009, Enums.Gem.NoSocket);
                GiveGear(client, 204009, Enums.Gem.NoSocket);
                GiveSteed(client);
                if (archer)
                {
                    for (int i = 0; i < ArrowStacks; i++)
                        GivePlainStack(client, ArrowId);
                }
                for (int i = 0; i < ScrollCount; i++)
                    GivePlain(client, ScrollId);
                client.Entity.AdjustMoney((decimal)(Revora.Config.WelcomeGold));
                client.Entity.AddRevoraPoints(Revora.Config.WelcomeRevoraPoints);
                client.AddBless(BlessSeconds);
                client.Entity.Update((byte)FlagsUpdate.DataType.OnlineTraining, client.OnlineTrainingPoints, false);
                EntityVariableTable.Save(client);
                EntityTable.SaveEntity(client);
                SkillTable.SaveSpells(client);
                SkillTable.SaveProficiencies(client);
            }
            catch (Exception e)
            {
                client[ClaimKey] = 0;
                Server.SaveException(e);
                dialog.Text("The work failed. Return when your bag is clear.");
                dialog.Option("I see.", 255);
                dialog.Send();
                return;
            }
            client.Send(new Message("Anwen armed you with a bound set, " + Revora.Config.WelcomeGold + " gold, and " + Revora.Config.WelcomeRevoraPoints + " Revora Points. The wing waits until 100. Talk to Rowan at 319,299 for your Twin City hunt.", Color.Yellow, Message.TopLeft));
            client.Entity.Teleport(1002, 408, 330);
        }

        private static void RaiseLevel(GameState client, byte cls)
        {
            if (client.Entity.Class != cls)
                client.Entity.Class = cls;
            if (client.Entity.Level >= GiftLevel)
                return;
            byte statsClass = cls;
            if (cls / 10 == 8)
                statsClass = 20;
            DataHolder.GetStats(statsClass, GiftLevel, client);
            client.Entity.Level = GiftLevel;
            client.Entity.Experience = 0;
            client.CalculateStatBonus();
            client.CalculateHPBonus();
            client.GemAlgorithm();
            client.Entity.Hitpoints = client.Entity.MaxHitpoints;
            client.Entity.Mana = (ushort)Math.Min(client.Entity.MaxMana, Math.Max(client.Entity.Mana, client.Entity.Spirit * 5));
        }

        private static uint[] KitIds(byte cls)
        {
            switch (cls / 10)
            {
                case 1:
                    return new uint[] { 118069, 120129, 130069, 410139, 420139, 150139, 160139 };
                case 2:
                    return new uint[] { 111069, 120129, 131069, 480139, 900049, 150139, 160139 };
                case 4:
                    return new uint[] { 113039, 120129, 133049, 500129, 150139, 160139 };
                case 5:
                    return new uint[] { 123069, 120129, 135069, 601139, 601139, 150139, 160139 };
                case 6:
                    return new uint[] { 143069, 120129, 136069, 610139, 610139, 150139, 160139 };
                case 7:
                    return new uint[] { 144069, 120129, 139069, 611139, 612139, 150139, 160139 };
                case 8:
                    return new uint[] { 148069, 120129, 138069, 617139, 617139, 150139, 160139 };
                case 10:
                    return new uint[] { 114069, 121129, 134069, 421139, 561139, 619139, 152129, 160139 };
                case 13:
                    return new uint[] { 114069, 121129, 134069, 421139, 619139, 152129, 160139 };
                case 14:
                    return new uint[] { 114069, 121129, 134069, 561139, 619139, 152129, 160139 };
                case 16:
                    return new uint[] { 170069, 120129, 101069, 626139, 626139, 150139, 160139 };
                default:
                    return null;
            }
        }

        private static void GiveGear(GameState client, uint id, Enums.Gem gem)
        {
            ConquerItem item = new ConquerItem(true);
            item.ID = id;
            item.Position = 0;
            item.Bound = true;
            item.Color = Enums.Color.Orange;
            item.Enchant = 0;
            item.Plus = 6;
            item.Bless = 1;
            if (id / 1000 == 204)
            {
                item.SocketOne = Enums.Gem.SuperThunderGem;
                item.SocketTwo = Enums.Gem.SuperGloryGem;
            }
            else if (id / 1000 == 203)
            {
                item.SocketOne = Enums.Gem.NoSocket;
                item.SocketTwo = Enums.Gem.NoSocket;
            }
            else if (gem != Enums.Gem.NoSocket)
            {
                item.SocketOne = gem;
                item.SocketTwo = gem;
            }
            FillDurability(item, id);
            if (!client.Inventory.Add(item, Enums.ItemUse.CreateAndAdd))
                throw new InvalidOperationException("inventory-full");
        }

        private static void GivePlain(GameState client, uint id)
        {
            ConquerItem item = new ConquerItem(true);
            item.ID = id;
            item.Position = 0;
            item.Bound = false;
            item.Plus = 0;
            item.Bless = 0;
            item.Enchant = 0;
            FillDurability(item, id);
            if (!client.Inventory.Add(item, Enums.ItemUse.CreateAndAdd))
                throw new InvalidOperationException("inventory-full");
        }

        private static void GivePlainStack(GameState client, uint id)
        {
            ConquerItem item = new ConquerItem(true);
            item.ID = id;
            item.Position = 0;
            item.Bound = false;
            item.Plus = 0;
            item.Bless = 0;
            item.Enchant = 0;
            ConquerItemBaseInformation info;
            if (ConquerItemInformation.BaseInformations.TryGetValue(id, out info) && info != null)
            {
                if (info.Durability > 0)
                {
                    item.Durability = info.Durability;
                    item.MaximDurability = info.Durability;
                }
                else
                {
                    item.Durability = 10000;
                    item.MaximDurability = 10000;
                }
                item.StackSize = info.StackSize > 0 ? info.StackSize : (ushort)25;
                item.MaxStackSize = item.StackSize;
            }
            else
            {
                item.Durability = 10000;
                item.MaximDurability = 10000;
                item.StackSize = 25;
                item.MaxStackSize = 25;
            }
            if (!client.Inventory.Add(item, Enums.ItemUse.CreateAndAdd))
                throw new InvalidOperationException("inventory-full");
        }

        private static void FillDurability(ConquerItem item, uint id)
        {
            ConquerItemBaseInformation info;
            if (ConquerItemInformation.BaseInformations.TryGetValue(id, out info) && info != null && info.Durability > 0)
            {
                item.Durability = info.Durability;
                item.MaximDurability = info.Durability;
            }
            else
            {
                item.Durability = 10000;
                item.MaximDurability = 10000;
            }
        }

        private static void GiveSteed(GameState client)
        {
            ConquerItem item = new ConquerItem(true);
            item.ID = 300000;
            item.Position = 0;
            item.Bound = true;
            item.Plus = 6;
            item.Bless = 1;
            item.Enchant = 0;
            item.Effect = Enums.ItemEffect.Horse;
            item.SocketProgress = 255u | (200u << 8) | (40u << 16);
            item.Durability = 10000;
            item.MaximDurability = 10000;
            if (!client.Inventory.Add(item, Enums.ItemUse.CreateAndAdd))
                throw new InvalidOperationException("inventory-full");
        }

        private static void TeachClassArts(GameState client)
        {
            GiveSkill(client, 9876);
            switch (client.Entity.Class / 10)
            {
                case 1:
                    GiveSkills(client, 1110, 1015, 1115, 1270, 1190, 3050, 10315);
                    GiveProficiency(client, 410);
                    GiveProficiency(client, 420);
                    break;
                case 2:
                    GiveSkills(client, 1020, 1025, 1040, 1050, 1051, 1260, 10470, 3060, 10311);
                    GiveProficiency(client, 480);
                    GiveProficiency(client, 900);
                    break;
                case 4:
                    GiveSkills(client, 8000, 8001, 8002, 8003, 8030, 9000, 10313);
                    GiveProficiency(client, 500);
                    break;
                case 5:
                    GiveSkills(client, 6000, 6001, 6002, 6003, 6004, 6010, 6011);
                    GiveProficiency(client, 601);
                    break;
                case 6:
                    GiveSkills(client, 10490, 10390, 10415, 10381, 10400, 10425, 10430, 10395, 10410, 10420, 10421, 10422, 10423, 10424, 10405);
                    GiveProficiency(client, 610);
                    break;
                case 7:
                    GiveSkills(client, 11030, 11040, 11050, 11060, 11070, 11100, 11110, 11120, 11130, 11140, 11170, 11180, 11230, 3050);
                    GiveProficiency(client, 611);
                    GiveProficiency(client, 612);
                    break;
                case 8:
                    GiveSkills(client, 1020, 1025, 1040, 1050, 1051, 1260, 10470, 3060, 10311);
                    GiveProficiency(client, 617);
                    break;
                case 10:
                    GiveSkills(client, 1000, 1005, 1010, 1125, 1150, 1175, 5001);
                    GiveProficiency(client, 421);
                    GiveProficiency(client, 561);
                    break;
                case 13:
                    GiveSkills(client, 1000, 1010, 1050, 1055, 1075, 1085, 1090, 1095, 1100, 1170, 1175, 1195, 4000, 4010, 4020, 4050, 4060, 4070, 10309, 30000, 3090, 5001);
                    GiveProficiency(client, 421);
                    break;
                case 14:
                    GiveSkills(client, 1000, 1002, 1005, 1010, 1120, 1125, 1150, 1160, 1165, 1180, 5001, 10309, 10310, 3080);
                    GiveProficiency(client, 561);
                    break;
                case 16:
                    GiveProficiency(client, 626);
                    break;
            }
        }

        private static void GiveSkills(GameState client, params ushort[] ids)
        {
            for (int i = 0; i < ids.Length; i++)
                GiveSkill(client, ids[i]);
        }

        private static void GiveSkill(GameState client, ushort id)
        {
            if (id == 0 || client.Spells == null)
                return;
            if (SpellTable.SpellInformations == null || !SpellTable.SpellInformations.ContainsKey(id))
                return;
            byte max = SpellTable.SpellInformations[id].Keys.Max();
            if (client.Spells.ContainsKey(id) && client.Spells[id].Level >= max)
                return;
            Spell spell = new Spell(true);
            spell.ID = id;
            spell.Level = max;
            spell.Experience = 0;
            client.AddSpell(spell);
        }

        private static void GiveProficiency(GameState client, ushort id)
        {
            if (id == 0 || client.Proficiencies == null)
                return;
            if (client.Proficiencies.ContainsKey(id) && client.Proficiencies[id].Level >= ProficiencyLevel)
                return;
            Proficiency prof = new Proficiency(true);
            prof.ID = id;
            prof.Level = ProficiencyLevel;
            prof.Experience = 0;
            client.AddProficiency(prof);
        }
    }
}
