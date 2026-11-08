// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using Revora.Network.GamePackets;

namespace Revora.Game.Features.Reincarnation
{
    public unsafe class Handle
    {
        public static void Hash(Client.GameState client)
        {
            ReincarnationRecovery.RestoreReady(client);
        }
    }
    public unsafe class Reincarnation
    {
        private Client.GameState _client;
        private SafeDictionary<ushort, Interfaces.ISkill> RemoveSkill = null;
        private SafeDictionary<ushort, Interfaces.ISkill> Addskill = null;
        public Reincarnation(Client.GameState client, byte new_class)
        {
            lock (client.ItemSyncRoot ?? client) Apply(client, new_class);
        }
        private void Apply(Client.GameState client, byte new_class)
        {
            if (client.Entity.PersistenceRequiresReload) throw new InvalidOperationException("Character requires reload.");
            if (client.Entity.Level < 130)
                return;
            _client = client;
            RemoveSkill = new SafeDictionary<ushort, Interfaces.ISkill>(500);
            Addskill = new SafeDictionary<ushort, Interfaces.ISkill>(500);

            #region Low level items

            for (byte i = 1; i < 9; i++)
            {
                if (i != 7)
                {
                    ConquerItem item = client.Equipment.TryGetItem(i);
                    if (item != null && item.ID != 0)
                    {
                        try
                        {
                            //client.UnloadItemStats(item, false);
                            Database.ConquerItemInformation cii =
                                new Database.ConquerItemInformation(item.ID, item.Plus);
                            item.ID =
                                cii.LowestID(
                                   ItemHandler.ItemMinLevel(ItemHandler.ItemPosition(item.ID)));
                            item.Mode = Game.Enums.ItemMode.Update;
                            item.Send(client);
                            client.LoadItemStats();
                            Database.ConquerItemTable.UpdateItemID(item, client);
                        }
                        catch
                        {
                            Console.WriteLine("Reborn item problem: " + item.ID);
                        }
                    }
                }
            }
            ConquerItem hand = client.Equipment.TryGetItem(5);
            if (hand != null)
            {
                client.Equipment.Remove(5);
                client.CalculateStatBonus();
                client.CalculateHPBonus();
                client.SendStatMessage();
            }
            else
                //client.Screen.send(client.Player.SpawnPacket, false);
            #endregion

            #region Remove Extra Skill

                if (client.Entity.FirstRebornClass == 15 && client.Entity.SecondRebornClass == 15 &&
                    client.Entity.Class == 15)
                {
                    WontAdd(Game.Enums.SkillIDs.DragonWhirl);
                }
            if (client.Entity.FirstRebornClass == 85 && client.Entity.SecondRebornClass == 85 &&
                client.Entity.Class == 85)
            {
                WontAdd(Game.Enums.SkillIDs.DragonFury);
            }
            if (client.Entity.FirstRebornClass == 25 && client.Entity.SecondRebornClass == 25 &&
                client.Entity.Class == 25)
            {
                WontAdd(Game.Enums.SkillIDs.Perseverance);
            }
            if (client.Entity.FirstRebornClass == 45 && client.Entity.SecondRebornClass == 45 &&
                client.Entity.Class == 45)
            {
                WontAdd(Game.Enums.SkillIDs.StarArrow);
            }
            if (client.Entity.FirstRebornClass == 55 && client.Entity.SecondRebornClass == 55 &&
                client.Entity.Class == 55)
            {
                WontAdd(Game.Enums.SkillIDs.PoisonStar);
            }
            if (client.Entity.FirstRebornClass == 65 && client.Entity.SecondRebornClass == 65 &&
                client.Entity.Class == 65)
            {
                WontAdd(Game.Enums.SkillIDs.soulshackle);
            }
            if (client.Entity.FirstRebornClass == 135 && client.Entity.SecondRebornClass == 135 &&
                client.Entity.Class == 135)
            {
                WontAdd(Game.Enums.SkillIDs.AzureShield);
            }
            if (client.Entity.FirstRebornClass == 145 && client.Entity.SecondRebornClass == 145 &&
                client.Entity.Class == 145)
            {
                WontAdd(Game.Enums.SkillIDs.HeavenBlade);
            }

                #endregion

            Database.ReincarnationTable.NewReincarnated(client.Entity);
            Game.Features.Reincarnation.ReincarnateInfo info = new Game.Features.Reincarnation.ReincarnateInfo();
            info.UID = client.Entity.UID;
            info.Level = client.Entity.Level;
            info.Experience = client.Entity.Experience;
            Kernel.ReincarnatedCharacters.Add(info.UID, info);
            client.Entity.FirstRebornClass = client.Entity.SecondRebornClass;
            client.Entity.SecondRebornClass = client.Entity.Class;
            client.Entity.Class = new_class;
            client.Entity.SecondRebornLevel = client.Entity.Level;
            client.Entity.Level = 15;
            client.Entity.Experience = 0;
            client.Entity.Atributes =
                (ushort)(client.ExtraAtributePoints(client.Entity.FirstRebornLevel, client.Entity.FirstRebornClass) +
                          client.ExtraAtributePoints(client.Entity.SecondRebornLevel, client.Entity.SecondRebornClass) +
                          62);


            client.Spells.Clear();
            client.Spells = new SafeDictionary<ushort, Interfaces.ISkill>(100);
            switch (client.Entity.FirstRebornClass)
            {
                case 15:
                    {
                        Add(Game.Enums.SkillIDs.Cyclone);
                        Add(Game.Enums.SkillIDs.Hercules);
                        Add(Game.Enums.SkillIDs.SpiritHealing);
                        Add(Game.Enums.SkillIDs.Robot);
                        Add(Game.Enums.SkillIDs.SuperCyclone);
                        Add(Game.Enums.SkillIDs.FatalCross);
                        Add(Game.Enums.SkillIDs.MortalStrike);
                        Add(Game.Enums.SkillIDs.BreathFocus);
                        break;
                    }
                case 25:
                    {
                        Add(Game.Enums.SkillIDs.SuperMan);
                        Add(Game.Enums.SkillIDs.Dash);
                        Add(Game.Enums.SkillIDs.Shield);
                        break;
                    }
                case 45:
                    {
                        Add(Game.Enums.SkillIDs.Intensify);
                        Add(Game.Enums.SkillIDs.Scatter);
                        Add(Game.Enums.SkillIDs.RapidFire);
                        Add(Game.Enums.SkillIDs.XPFly);
                        Add(Game.Enums.SkillIDs.AdvancedFly);
                        break;
                    }
                case 55:
                    {
                        Add(Game.Enums.SkillIDs.FatalStrike);
                        Add(Game.Enums.SkillIDs.ShurikenVortex);
                        Add(Game.Enums.SkillIDs.ToxicFog);
                        Add(Game.Enums.SkillIDs.TwofoldBlades);
                        Add(Game.Enums.SkillIDs.PoisonStar);
                        Add(Game.Enums.SkillIDs.TwilightDance);
                        Add(Game.Enums.SkillIDs.SuperTwofoldBlade);
                        Add(Game.Enums.SkillIDs.FatalSpin);

                        break;
                    }
                case 65:
                    {
                        Add(Game.Enums.SkillIDs.RadiantPalm);
                        Add(Game.Enums.SkillIDs.WhirlWindKick);
                        Add(Game.Enums.SkillIDs.TripleAttack);
                        Add(Game.Enums.SkillIDs.Oblivion);
                        Add(Game.Enums.SkillIDs.Serenity);
                        Add(Game.Enums.SkillIDs.Compassion);
                        Add(Game.Enums.SkillIDs.TyrantAura);
                        Add(Game.Enums.SkillIDs.TyrantAura);
                        Add(Game.Enums.SkillIDs.DeflectionAura);
                        break;
                    }
                case 75:
                    {
                        Add(Game.Enums.SkillIDs.RadiantPalm);
                        Add(Game.Enums.SkillIDs.WhirlWindKick);
                        Add(Game.Enums.SkillIDs.TripleAttack);
                        Add(Game.Enums.SkillIDs.Oblivion);
                        Add(Game.Enums.SkillIDs.Serenity);
                        Add(Game.Enums.SkillIDs.Compassion);
                        Add(Game.Enums.SkillIDs.TyrantAura);
                        Add(Game.Enums.SkillIDs.TyrantAura);
                        Add(Game.Enums.SkillIDs.DeflectionAura);
                        break;
                    }
                case 85:
                    {
                        Add(Game.Enums.SkillIDs.SpeedKick);
                        Add(Game.Enums.SkillIDs.ViolentKick);
                        Add(Game.Enums.SkillIDs.StormKick);
                        Add(Game.Enums.SkillIDs.CrackingSwip);
                        Add(Game.Enums.SkillIDs.SplittingSwipe);
                        Add(Game.Enums.SkillIDs.DragonSwing);
                        Add(Game.Enums.SkillIDs.AirStrike);
                        Add(Game.Enums.SkillIDs.EarthSweep);
                        Add(Game.Enums.SkillIDs.DragonPunch);
                        Add(Game.Enums.SkillIDs.DragonSlash);
                        Add(Game.Enums.SkillIDs.DragonFlow);
                        Add(Game.Enums.SkillIDs.DragonRoar);
                        Add(Game.Enums.SkillIDs.DragonCyclone);
                        Add(Game.Enums.SkillIDs.AirKick);
                        Add(Game.Enums.SkillIDs.AirSweep);
                        Add(Game.Enums.SkillIDs.AirRaid);
                        break;
                    }
                case 135:
                    {
                        Add(Game.Enums.SkillIDs.Thunder);
                        Add(Game.Enums.SkillIDs.WaterElf);
                        Add(Game.Enums.SkillIDs.Cure);
                        Add(Game.Enums.SkillIDs.Lightning);
                        Add(Game.Enums.SkillIDs.Volcano);
                        Add(Game.Enums.SkillIDs.Pray);
                        Add(Game.Enums.SkillIDs.AdvancedCure);
                        Add(Game.Enums.SkillIDs.Meditation);
                        Add(Game.Enums.SkillIDs.Stigma);
                        break;
                    }
                case 165:
                    {
                        Add(Game.Enums.SkillIDs.FrostGazeI);
                        Add(Game.Enums.SkillIDs.Thundercloud);
                        Add(Game.Enums.SkillIDs.TripleBlasts);
                        Add(Game.Enums.SkillIDs.JusticeChant);
                        Add(Game.Enums.SkillIDs.BurntFrost);
                        Add(Game.Enums.SkillIDs.HealingSnow);
                        Add(Game.Enums.SkillIDs.ChillingSnow);
                        Add(Game.Enums.SkillIDs.Thunderbolt);
                        Add(Game.Enums.SkillIDs.AngerofStomper);
                        Add(Game.Enums.SkillIDs.HorrorofStomper);
                        Add(Game.Enums.SkillIDs.RageofWar);
                        Add(Game.Enums.SkillIDs.PeaceofStomper);
                        Add(Game.Enums.SkillIDs.FreezingPelter);
                        Add(Game.Enums.SkillIDs.RevengeTail);
                        Add(Game.Enums.SkillIDs.Circle);
                        Add(Game.Enums.SkillIDs.Rectangle);
                        Add(Game.Enums.SkillIDs.FrostGazeII);
                        Add(Game.Enums.SkillIDs.FrostGazeIII);
                        Add(Game.Enums.SkillIDs.ShadowofChaser);
                        Add(Game.Enums.SkillIDs.ThundercloudAttack);
                        Add(Game.Enums.SkillIDs.DestinyFire);
                        Add(Game.Enums.SkillIDs.FireShower);
                        Add(Game.Enums.SkillIDs.WheelOfDevastation);
                        break;
                    }
                case 140:
                    {
                        Add(Game.Enums.SkillIDs.Thunder);
                        Add(Game.Enums.SkillIDs.Cure);
                        Add(Game.Enums.SkillIDs.Lightning);
                        Add(Game.Enums.SkillIDs.Tornado);
                        Add(Game.Enums.SkillIDs.FireCircle);
                        Add(Game.Enums.SkillIDs.FireMeteor);
                        Add(Game.Enums.SkillIDs.FireRing);
                        break;
                    }
            }

            byte PreviousClass = client.Entity.FirstRebornClass;
            byte toClass = (byte)(client.Entity.SecondRebornClass - 4);

            Interfaces.ISkill[] ADD_spells = this.Addskill.Values.ToArray();
            foreach (Interfaces.ISkill skill in ADD_spells)
            {
                skill.Available = true;
                if (!client.Spells.ContainsKey(skill.ID))
                    client.Spells.Add(skill.ID, skill);
            }

            #region Spells

            Interfaces.ISkill[] spells = client.Spells.Values.ToArray();
            foreach (Interfaces.ISkill spell in spells)
            {
                spell.PreviousLevel = spell.Level;
                spell.Level = 0;
                spell.Experience = 0;

                #region Pirate

                if (PreviousClass == 75)
                {
                    if (client.Entity.Class != 71)
                    {
                        switch (spell.ID)
                        {
                            case 10490:
                            case 10415:
                            case 10381:
                                client.RemoveSpell(spell);
                                break;
                        }
                    }
                }

                #endregion

                #region Monk

                if (PreviousClass == 65)
                {
                    if (client.Entity.Class != 61)
                    {
                        switch (spell.ID)
                        {
                            case 10490:
                            case 10415:
                            case 10381:
                                client.RemoveSpell(spell);
                                break;
                        }
                    }
                }

                #endregion

                #region Warrior

                if (PreviousClass == 25)
                {
                    if (client.Entity.Class != 21)
                    {
                        switch (spell.ID)
                        {
                            case 1025:
                                if (client.Entity.Class != 21 && client.Entity.Class != 132)
                                    client.RemoveSpell(spell);
                                break;
                        }
                    }
                }

                #endregion

                #region Ninja

                if (toClass != 51)
                {
                    switch (spell.ID)
                    {
                        case 6010:
                        case 6000:
                        case 6011:
                            client.RemoveSpell(spell);
                            break;
                    }
                }

                #endregion

                #region Trojan

                if (toClass != 11)
                {
                    switch (spell.ID)
                    {
                        case 1115:
                            client.RemoveSpell(spell);
                            break;
                    }
                }

                #endregion

                #region Archer

                if (toClass != 41)
                {
                    switch (spell.ID)
                    {
                        case 8001:
                        case 8000:
                        case 8003:
                        case 9000:
                        case 8002:
                        case 8030:
                            client.RemoveSpell(spell);
                            break;
                    }
                }

                #endregion

                #region WaterTaoist

                if (PreviousClass == 135)
                {
                    if (toClass != 132)
                    {
                        switch (spell.ID)
                        {
                            case 1000:
                            case 1001:
                            case 1010:
                            case 1125:
                            case 1100:
                            case 8030:
                                client.RemoveSpell(spell);
                                break;
                            case 1050:
                            case 1175:
                            case 1170:
                                if (toClass != 142)
                                    client.RemoveSpell(spell);
                                break;
                        }
                    }
                }

                #endregion

                #region FireTaoist

                if (PreviousClass == 145)
                {
                    if (toClass != 142)
                    {
                        switch (spell.ID)
                        {
                            case 1000:
                            case 1001:
                            case 1150:
                            case 1180:
                            case 1120:
                            case 1002:
                            case 1160:
                            case 1165:
                                client.RemoveSpell(spell);
                                break;
                        }
                    }
                }

                #endregion

                #region Windwalker

                if (PreviousClass == 165)
                {
                    if (client.Entity.Class != 161)
                    {
                        switch (spell.ID)
                        {
                            case 12830:
                            case 12840:
                            case 12850:
                            case 12860:
                            case 12870:
                            case 12880:
                            case 12890:
                            case 12930:
                            case 12940:
                            case 12950:
                            case 12960:
                            case 12970:
                            case 12980:
                            case 12990:
                            case 13000:
                            case 13020:
                            case 13030:
                            case 13190:
                            case 13050:
                            case 13060:
                            case 13070:
                            case 13080:
                            case 13090:
                            case 13360:
                                client.RemoveSpell(spell);
                                break;
                        }
                    }
                }

                #endregion
                if (client.Spells.ContainsKey(spell.ID))
                    if (spell.ID != (ushort)Game.Enums.SkillIDs.Reflect)
                        spell.Send(client);
            }

            #endregion

            Add(Game.Enums.SkillIDs.Bless);

            Addskill.Clear();
            Addskill = new SafeDictionary<ushort, Interfaces.ISkill>(100);

            PreviousClass = client.Entity.SecondRebornClass;
            toClass = client.Entity.Class;
            switch (client.Entity.SecondRebornClass)
            {
                case 15:
                    {
                        Add(Game.Enums.SkillIDs.Robot);
                        Add(Game.Enums.SkillIDs.Cyclone);
                        Add(Game.Enums.SkillIDs.Hercules);
                        Add(Game.Enums.SkillIDs.SpiritHealing);
                        Add(Game.Enums.SkillIDs.SuperCyclone);
                        Add(Game.Enums.SkillIDs.FatalCross);
                        Add(Game.Enums.SkillIDs.MortalStrike);
                        Add(Game.Enums.SkillIDs.BreathFocus);
                        break;
                    }
                case 25:
                    {
                        Add(Game.Enums.SkillIDs.SuperMan);
                        Add(Game.Enums.SkillIDs.Dash);
                        Add(Game.Enums.SkillIDs.Shield);
                        break;
                    }
                case 45:
                    {
                        Add(Game.Enums.SkillIDs.Intensify);
                        Add(Game.Enums.SkillIDs.Scatter);
                        Add(Game.Enums.SkillIDs.RapidFire);
                        Add(Game.Enums.SkillIDs.XPFly);
                        Add(Game.Enums.SkillIDs.AdvancedFly);
                        break;
                    }
                case 55:
                    {
                        Add(Game.Enums.SkillIDs.FatalStrike);
                        Add(Game.Enums.SkillIDs.ShurikenVortex);
                        Add(Game.Enums.SkillIDs.ToxicFog);
                        Add(Game.Enums.SkillIDs.TwofoldBlades);
                        Add(Game.Enums.SkillIDs.TwilightDance);
                        Add(Game.Enums.SkillIDs.SuperTwofoldBlade);
                        Add(Game.Enums.SkillIDs.FatalSpin);
                        break;
                    }
                case 65:
                    {
                        Add(Game.Enums.SkillIDs.RadiantPalm);
                        Add(Game.Enums.SkillIDs.WhirlWindKick);
                        Add(Game.Enums.SkillIDs.TripleAttack);
                        Add(Game.Enums.SkillIDs.Oblivion);
                        Add(Game.Enums.SkillIDs.Serenity);
                        Add(Game.Enums.SkillIDs.Compassion);
                        Add(Game.Enums.SkillIDs.TyrantAura);
                        Add(Game.Enums.SkillIDs.TyrantAura);
                        Add(Game.Enums.SkillIDs.DeflectionAura);
                        break;
                    }
                case 75:
                    {
                        Add(Game.Enums.SkillIDs.RadiantPalm);
                        Add(Game.Enums.SkillIDs.WhirlWindKick);
                        Add(Game.Enums.SkillIDs.TripleAttack);
                        Add(Game.Enums.SkillIDs.Oblivion);
                        Add(Game.Enums.SkillIDs.Serenity);
                        Add(Game.Enums.SkillIDs.Compassion);
                        Add(Game.Enums.SkillIDs.TyrantAura);
                        Add(Game.Enums.SkillIDs.TyrantAura);
                        Add(Game.Enums.SkillIDs.DeflectionAura);
                        break;
                    }
                case 85:
                    {
                        Add(Game.Enums.SkillIDs.SpeedKick);
                        Add(Game.Enums.SkillIDs.ViolentKick);
                        Add(Game.Enums.SkillIDs.StormKick);
                        Add(Game.Enums.SkillIDs.CrackingSwip);
                        Add(Game.Enums.SkillIDs.SplittingSwipe);
                        Add(Game.Enums.SkillIDs.DragonSwing);
                        Add(Game.Enums.SkillIDs.AirStrike);
                        Add(Game.Enums.SkillIDs.EarthSweep);
                        Add(Game.Enums.SkillIDs.DragonPunch);
                        Add(Game.Enums.SkillIDs.DragonSlash);
                        Add(Game.Enums.SkillIDs.DragonFlow);
                        Add(Game.Enums.SkillIDs.DragonCyclone);
                        Add(Game.Enums.SkillIDs.AirKick);
                        Add(Game.Enums.SkillIDs.AirSweep);
                        Add(Game.Enums.SkillIDs.AirRaid);
                        break;
                    }
                case 135:
                    {
                        Add(Game.Enums.SkillIDs.Thunder);
                        Add(Game.Enums.SkillIDs.WaterElf);
                        Add(Game.Enums.SkillIDs.Cure);
                        Add(Game.Enums.SkillIDs.Lightning);
                        Add(Game.Enums.SkillIDs.Volcano);
                        Add(Game.Enums.SkillIDs.Pray);
                        Add(Game.Enums.SkillIDs.Stigma);
                        Add(Game.Enums.SkillIDs.AdvancedCure);
                        Add(Game.Enums.SkillIDs.Meditation);
                        break;
                    }
                case 140:
                    {
                        Add(Game.Enums.SkillIDs.Thunder);
                        Add(Game.Enums.SkillIDs.Cure);
                        Add(Game.Enums.SkillIDs.Lightning);
                        Add(Game.Enums.SkillIDs.Tornado);
                        Add(Game.Enums.SkillIDs.FireCircle);
                        Add(Game.Enums.SkillIDs.FireMeteor);
                        Add(Game.Enums.SkillIDs.FireRing);
                        break;
                    }
                case 165:
                    {
                        Add(Game.Enums.SkillIDs.FrostGazeI);
                        Add(Game.Enums.SkillIDs.Thundercloud);
                        Add(Game.Enums.SkillIDs.TripleBlasts);
                        Add(Game.Enums.SkillIDs.JusticeChant);
                        Add(Game.Enums.SkillIDs.BurntFrost);
                        Add(Game.Enums.SkillIDs.HealingSnow);
                        Add(Game.Enums.SkillIDs.ChillingSnow);
                        Add(Game.Enums.SkillIDs.Thunderbolt);
                        Add(Game.Enums.SkillIDs.AngerofStomper);
                        Add(Game.Enums.SkillIDs.HorrorofStomper);
                        Add(Game.Enums.SkillIDs.RageofWar);
                        Add(Game.Enums.SkillIDs.PeaceofStomper);
                        Add(Game.Enums.SkillIDs.FreezingPelter);
                        Add(Game.Enums.SkillIDs.RevengeTail);
                        Add(Game.Enums.SkillIDs.Circle);
                        Add(Game.Enums.SkillIDs.Rectangle);
                        Add(Game.Enums.SkillIDs.FrostGazeII);
                        Add(Game.Enums.SkillIDs.FrostGazeIII);
                        Add(Game.Enums.SkillIDs.ShadowofChaser);
                        Add(Game.Enums.SkillIDs.ThundercloudAttack);
                        Add(Game.Enums.SkillIDs.DestinyFire);
                        Add(Game.Enums.SkillIDs.FireShower);
                        Add(Game.Enums.SkillIDs.WheelOfDevastation);
                        break;
                    }
            }

            //PreviousClass = client.Player.FirstRebornClass;
            //toClass = client.Player.SecondRebornClass;
            Add(Game.Enums.SkillIDs.Bless);

            Interfaces.ISkill[] aADD_spells = this.Addskill.Values.ToArray();
            foreach (Interfaces.ISkill skill in aADD_spells)
            {
                skill.Available = true;
                if (!client.Spells.ContainsKey(skill.ID))
                    client.Spells.Add(skill.ID, skill);
            }

            #region Spells

            Interfaces.ISkill[] aspells = client.Spells.Values.ToArray();
            foreach (Interfaces.ISkill aspell in spells)
            {
                aspell.PreviousLevel = aspell.Level;
                aspell.Level = 0;
                aspell.Experience = 0;

                #region Pirate

                if (PreviousClass == 75)
                {
                    if (client.Entity.Class != 71)
                    {
                        switch (aspell.ID)
                        {
                            case 10490:
                            case 10415:
                            case 10381:
                                client.RemoveSpell(aspell);
                                break;
                        }
                    }
                }

                #endregion

                #region Monk

                if (PreviousClass == 65)
                {
                    if (client.Entity.Class != 61)
                    {
                        switch (aspell.ID)
                        {
                            case 10490:
                            case 10415:
                            case 10381:
                                client.RemoveSpell(aspell);
                                break;
                        }
                    }
                }

                #endregion

                #region Warrior

                if (PreviousClass == 25)
                {
                    if (client.Entity.Class != 21)
                    {
                        switch (aspell.ID)
                        {
                            case 1025:
                                if (client.Entity.Class != 21 && client.Entity.Class != 132)
                                    client.RemoveSpell(aspell);
                                break;
                        }
                    }
                }

                #endregion

                #region Ninja

                if (toClass != 51)
                {
                    switch (aspell.ID)
                    {
                        case 6010:
                        case 6000:
                        case 6011:
                            client.RemoveSpell(aspell);
                            break;
                    }
                }

                #endregion

                #region Trojan

                if (toClass != 11)
                {
                    switch (aspell.ID)
                    {
                        case 1115:
                            client.RemoveSpell(aspell);
                            break;
                    }
                }

                #endregion

                #region Archer

                if (toClass != 41)
                {
                    switch (aspell.ID)
                    {
                        case 8001:
                        case 8000:
                        case 8003:
                        case 9000:
                        case 8002:
                        case 8030:
                            client.RemoveSpell(aspell);
                            break;
                    }
                }

                #endregion

                #region WaterTaoist

                if (PreviousClass == 135)
                {
                    if (toClass != 132)
                    {
                        switch (aspell.ID)
                        {
                            case 1000:
                            case 1001:
                            case 1010:
                            case 1125:
                            case 1100:
                            case 8030:
                                client.RemoveSpell(aspell);
                                break;
                            case 1050:
                            case 1175:
                            case 1170:
                                if (toClass != 142)
                                    client.RemoveSpell(aspell);
                                break;
                        }
                    }
                }

                #endregion

                #region FireTaoist

                if (PreviousClass == 145)
                {
                    if (toClass != 142)
                    {
                        switch (aspell.ID)
                        {
                            case 1000:
                            case 1001:
                            case 1150:
                            case 1180:
                            case 1120:
                            case 1002:
                            case 1160:
                            case 1165:
                                client.RemoveSpell(aspell);
                                break;
                        }
                    }
                }

                #endregion

                #region Windwalker
                if (PreviousClass == 165)
                {
                    if (client.Entity.Class != 161)
                    {
                        switch (aspell.ID)
                        {
                            case 12830:
                            case 12840:
                            case 12850:
                            case 12860:
                            case 12870:
                            case 12880:
                            case 12890:
                            case 12930:
                            case 12940:
                            case 12950:
                            case 12960:
                            case 12970:
                            case 12980:
                            case 12990:
                            case 13000:
                            case 13020:
                            case 13030:
                            case 13190:
                            case 13050:
                            case 13060:
                            case 13070:
                            case 13080:
                            case 13090:
                            case 13360:
                                client.RemoveSpell(aspell);
                                break;
                        }
                    }
                }
                #endregion
                if (client.Spells.ContainsKey(aspell.ID))
                    if (aspell.ID != (ushort)Game.Enums.SkillIDs.Reflect)
                        aspell.Send(client);
            }

            #endregion

            Addskill.Clear();
            Addskill = new SafeDictionary<ushort, Interfaces.ISkill>(20);

            #region Add Extra Skill

            if (client.Entity.FirstRebornClass == 15 && client.Entity.SecondRebornClass == 15 &&
                client.Entity.Class == 11)
            {
                Add(Game.Enums.SkillIDs.DragonWhirl);
            }
            if (client.Entity.FirstRebornClass == 85 && client.Entity.SecondRebornClass == 85 &&
                client.Entity.Class == 81)
            {
                Add(Game.Enums.SkillIDs.DragonFury);
            }
            if (client.Entity.FirstRebornClass == 25 && client.Entity.SecondRebornClass == 25 &&
                client.Entity.Class == 21)
            {
                Add(Game.Enums.SkillIDs.Perseverance);
            }
            if (client.Entity.FirstRebornClass == 45 && client.Entity.SecondRebornClass == 45 &&
                client.Entity.Class == 41)
            {
                Add(Game.Enums.SkillIDs.StarArrow);
            }
            if (client.Entity.FirstRebornClass == 55 && client.Entity.SecondRebornClass == 55 &&
                client.Entity.Class == 55)
            {
                Add(Game.Enums.SkillIDs.PoisonStar);
                Add(Game.Enums.SkillIDs.CounterKill);
            }
            if (client.Entity.FirstRebornClass == 65 && client.Entity.SecondRebornClass == 65 &&
                client.Entity.Class == 61)
            {
                Add(Game.Enums.SkillIDs.soulshackle);
            }
            if (client.Entity.FirstRebornClass == 135 && client.Entity.SecondRebornClass == 135 &&
                client.Entity.Class == 132)
            {
                Add(Game.Enums.SkillIDs.AzureShield);
            }
            if (client.Entity.FirstRebornClass == 145 && client.Entity.SecondRebornClass == 145 &&
                client.Entity.Class == 142)
            {
                Add(Game.Enums.SkillIDs.HeavenBlade);
            }

            #endregion

            Interfaces.ISkill[] aaADD_spells = this.Addskill.Values.ToArray();
            foreach (Interfaces.ISkill skill in aaADD_spells)
            {
                skill.Available = true;
                if (!client.Spells.ContainsKey(skill.ID))
                    client.Spells.Add(skill.ID, skill);
            }

            #region Proficiencies

            foreach (Interfaces.ISkill proficiency in client.Proficiencies.Values)
            {
                proficiency.PreviousLevel = proficiency.Level;
                proficiency.Level = 0;
                proficiency.Experience = 0;
                proficiency.Send(client);
            }

            #endregion

            Database.DataHolder.GetStats(client.Entity.Class, client.Entity.Level, client);
            client.CalculateStatBonus();
            client.CalculateHPBonus();
            client.GemAlgorithm();
            client.SendStatMessage();
           Chat.WorldMessage(client.Entity.Name + " has reincarnated! Congratulations!");
        }
        private void Add(Game.Enums.SkillIDs S)
        {
            Interfaces.ISkill New = new Network.GamePackets.Spell(true);
            New.ID = (ushort)S;
            New.Level = 0;
            New.Experience = 0;
            New.PreviousLevel = 0;
            New.Send(_client);
            Addskill.Add(New.ID, New);
        }
        private void WontAdd(Game.Enums.SkillIDs S)
        {
            Network.GamePackets.Data data = new Data(true);
            data.UID = _client.Entity.UID;
            data.dwParam = (byte)S;
            data.ID = 109;
            data.Send(_client);

            Interfaces.ISkill New = new Network.GamePackets.Spell(true);
            New.ID = (ushort)S;
            New.Level = 0;
            New.Experience = 0;
            New.PreviousLevel = 0;
            RemoveSkill.Add(New.ID, New);
        }
    }
    public unsafe class Reincarnate
    {
        public Game.ConquerStructures.Inventory Inventory;
        public Game.ConquerStructures.Equipment Equipment;
        public Entity Entity;
        public byte Class;
        public byte First;
        public byte Second;
        private Interfaces.ISkill[] Skills;
        private Interfaces.IProf[] Profs;
        private Dictionary<ushort, Interfaces.ISkill> Learn = null;
        private Dictionary<ushort, Interfaces.ISkill> WontLearn = null;
        public Reincarnate(Entity _Entity, byte _class)
        {
            lock (_Entity.Owner.ItemSyncRoot ?? _Entity.Owner) Initialize(_Entity, _class);
        }
        private void Initialize(Entity _Entity, byte _class)
        {
            if (_Entity.PersistenceRequiresReload) throw new InvalidOperationException("Character requires reload.");
            Entity = _Entity;
            Class = _class;
            First = Entity.SecondRebornClass;
            Second = Entity.Class;
            Learn = new Dictionary<ushort, Interfaces.ISkill>();
            WontLearn = new Dictionary<ushort, Interfaces.ISkill>();
            Skills = new Interfaces.ISkill[Entity.Owner.Spells.Values.Count];
            Entity.Owner.Spells.Values.CopyTo(Skills, 0);
            Profs = new Interfaces.IProf[Entity.Owner.Proficiencies.Values.Count];
            Entity.Owner.Proficiencies.Values.CopyTo(Profs, 0);
            doIt();
        }
        private void doIt()
        {
            #region Reincarnate

            Database.ReincarnationTable.NewReincarnated(Entity);
            Game.Features.Reincarnation.ReincarnateInfo info = new Game.Features.Reincarnation.ReincarnateInfo();
            info.UID = Entity.UID;
            info.Level = Entity.Level;
            info.Experience = Entity.Experience;
            Kernel.ReincarnatedCharacters.Add(info.UID, info);
            Entity.FirstRebornClass = First;
            Entity.SecondRebornClass = Second;
            Entity.Class = Class;
            Entity.Atributes = 182;
            Entity.Level = 15;
            Entity.Experience = 0;

            #endregion

            #region Low level items

            for (byte i = 1; i < 9; i++)
            {
                if (i != 7)
                {
                    ConquerItem item = Entity.Owner.Equipment.TryGetItem(i);
                    if (item != null && item.ID != 0)
                    {
                        try
                        {
                            Database.ConquerItemInformation cii =  new Database.ConquerItemInformation(item.ID, item.Plus);
                            item.ID = cii.LowestID(ItemHandler.ItemMinLevel(ItemHandler.ItemPosition(item.ID)));
                            item.Mode = Game.Enums.ItemMode.Update;
                            item.Send(Entity.Owner);
                            Database.ConquerItemTable.UpdateItemID(item, Entity.Owner);
                        }
                        catch
                        {
                            Console.WriteLine("Reborn item problem: " + item.ID);
                        }
                    }
                }
            }
            Entity.Owner.LoadItemStats();
            ConquerItem hand = Entity.Owner.Equipment.TryGetItem(5);
            if (hand != null)
            {
                Entity.Owner.Equipment.Remove(5);
                CalculateStatBonus();
                CalculateHPBonus();
                Entity.Owner.Screen.Reload(null);
            }
            else
                Entity.Owner.SendScreen(Entity.Owner.Entity.SpawnPacket, false);

            #endregion

            foreach (Interfaces.ISkill s in Skills)
                Entity.Owner.Spells.Remove(s.ID);

            switch (First)
            {
                #region KungFuKing
                case 85:
                    {
                        switch (Second)
                        {
                            case 81: // ana zh2t we da5el anam lma tegy a3melha anta
                                //Add(Enums.SkillIDs.SpeedKick);
                                //Add(Enums.SkillIDs.ViolentKick);
                                //Add(Enums.SkillIDs.StormKick);
                                //Add(Enums.SkillIDs.CrackingSwip);
                                //Add(Enums.SkillIDs.SplittingSwipe);
                                //Add(Enums.SkillIDs.DragonSwing);
                                //Add(Enums.SkillIDs.DragonPunch);
                                //Add(Enums.SkillIDs.DragonSlash);
                                //Add(Enums.SkillIDs.DragonFlow);
                                Add(Enums.SkillIDs.DragonRoar);
                                //Add(Enums.SkillIDs.DragonCyclone);
                                //Add(Enums.SkillIDs.AirKick);
                                //Add(Enums.SkillIDs.AirSweep);
                                //Add(Enums.SkillIDs.AirRaid);
                                break;
                            default:
                                //WontAdd(Enums.SkillIDs.SpeedKick);
                                //WontAdd(Enums.SkillIDs.ViolentKick);
                                //WontAdd(Enums.SkillIDs.StormKick);
                                //WontAdd(Enums.SkillIDs.CrackingSwip);
                                //WontAdd(Enums.SkillIDs.SplittingSwipe);
                                //WontAdd(Enums.SkillIDs.DragonSwing);
                                //WontAdd(Enums.SkillIDs.DragonPunch);
                                //WontAdd(Enums.SkillIDs.DragonSlash);
                                //WontAdd(Enums.SkillIDs.DragonFlow);
                                ////WontAdd(Enums.SkillIDs.DragonRoar);
                                //WontAdd(Enums.SkillIDs.DragonCyclone);
                                //WontAdd(Enums.SkillIDs.AirKick);
                                //WontAdd(Enums.SkillIDs.AirSweep);
                                //WontAdd(Enums.SkillIDs.AirRaid);
                                break;
                        }
                        break;
                    }
                #endregion
                #region Trojan

                case 15:
                    {
                        switch (Second)
                        {
                            case 11:
                                Add(Enums.SkillIDs.CruelShade);
                                break;
                            default:
                                WontAdd(Enums.SkillIDs.Accuracy);
                                break;
                        }
                        break;
                    }

                #endregion
                #region Warrior

                case 25:
                    {
                        switch (Second)
                        {
                            case 11:
                                Add(Enums.SkillIDs.IronShirt);
                                WontAdd(Enums.SkillIDs.Shield);
                                WontAdd(Enums.SkillIDs.SuperMan);
                                break;
                            case 21:
                                Add(Enums.SkillIDs.Reflect);
                                break;
                            case 132:
                                WontAdd(Enums.SkillIDs.FlyingMoon);
                                WontAdd(Enums.SkillIDs.Shield);
                                break;
                            case 142:
                                WontAdd(Enums.SkillIDs.Accuracy);
                                WontAdd(Enums.SkillIDs.FlyingMoon);
                                WontAdd(Enums.SkillIDs.SuperMan);
                                break;
                            default:
                                WontAdd(Enums.SkillIDs.Accuracy);
                                WontAdd(Enums.SkillIDs.FlyingMoon);
                                WontAdd(Enums.SkillIDs.SuperMan);
                                WontAdd(Enums.SkillIDs.Shield);
                                break;
                        }
                        break;
                    }

                #endregion
                #region Archer

                case 45:
                    {
                        switch (Second)
                        {
                            case 41:
                                break;
                            default:
                                WontAdd(Enums.SkillIDs.Scatter);
                                WontAdd(Enums.SkillIDs.XPFly);
                                WontAdd(Enums.SkillIDs.AdvancedFly);
                                WontAdd(Enums.SkillIDs.ArrowRain);
                                WontAdd(Enums.SkillIDs.Intensify);
                                WontAdd(Enums.SkillIDs.RapidFire);
                                break;
                        }
                        break;
                    }

                #endregion
                #region Ninja

                case 55:
                    {
                        switch (Second)
                        {
                            case 51:
                                break;
                            default:
                                WontAdd(Enums.SkillIDs.PoisonStar);
                                WontAdd(Enums.SkillIDs.ShurikenVortex);
                                WontAdd(Enums.SkillIDs.FatalStrike);
                                WontAdd(Enums.SkillIDs.TwofoldBlades);
                                WontAdd(Enums.SkillIDs.ArcherBane);
                                WontAdd(Enums.SkillIDs.TwilightDance);
                                WontAdd(Enums.SkillIDs.SuperTwofoldBlade);
                                WontAdd(Enums.SkillIDs.FatalSpin);
                                break;
                        }
                        break;
                    }

                #endregion
                #region Monk

                case 65:
                    {
                        switch (Second)
                        {
                            case 61:
                                break;
                            default:
                                WontAdd(Enums.SkillIDs.Oblivion);
                                WontAdd(Enums.SkillIDs.RadiantPalm);
                                WontAdd(Enums.SkillIDs.TyrantAura);
                                WontAdd(Enums.SkillIDs.DeathBlow);
                                WontAdd(Enums.SkillIDs.DeflectionAura);
                                WontAdd(Enums.SkillIDs.TripleAttack);
                                break;
                        }
                        break;
                    }

                #endregion
                #region Water

                case 135:
                    {
                        switch (Second)
                        {
                            case 132:
                                Add(Enums.SkillIDs.Pervade);
                                break;
                            case 142:
                                WontAdd(Enums.SkillIDs.Nectar);
                                WontAdd(Enums.SkillIDs.HealingRain);
                                break;
                            default:
                                WontAdd(Enums.SkillIDs.Nectar);
                                WontAdd(Enums.SkillIDs.Lightning);
                                WontAdd(Enums.SkillIDs.Volcano);
                                WontAdd(Enums.SkillIDs.AdvancedCure);
                                WontAdd(Enums.SkillIDs.SpeedLightning);
                                WontAdd(Enums.SkillIDs.HealingRain);
                                break;
                        }
                        break;
                    }

                #endregion
                #region Fire

                case 145:
                    {
                        switch (Second)
                        {
                            case 142:
                                Add(Enums.SkillIDs.Dodge);
                                break;
                            default:
                                if (Second != 132)
                                    WontAdd(Enums.SkillIDs.FireCircle);

                                WontAdd(Enums.SkillIDs.Tornado);
                                WontAdd(Enums.SkillIDs.FireMeteor);
                                WontAdd(Enums.SkillIDs.FireOfHell);
                                WontAdd(Enums.SkillIDs.FireRing);
                                WontAdd(Enums.SkillIDs.Volcano);
                                WontAdd(Enums.SkillIDs.Lightning);
                                WontAdd(Enums.SkillIDs.SpeedLightning);
                                break;
                        }
                        break;
                    }

                #endregion
                #region Windwalker
                case 165:
                    {
                        switch (Second)
                        {
                            case 61: break;
                            default:
                                WontAdd(Enums.SkillIDs.FrostGazeI);
                                WontAdd(Enums.SkillIDs.Thundercloud);
                                WontAdd(Enums.SkillIDs.TripleBlasts);
                                WontAdd(Enums.SkillIDs.JusticeChant);
                                WontAdd(Enums.SkillIDs.SwirlingStorm);
                                WontAdd(Enums.SkillIDs.RageofWar);
                                WontAdd(Enums.SkillIDs.BurntFrost);
                                WontAdd(Enums.SkillIDs.HealingSnow);
                                WontAdd(Enums.SkillIDs.ChillingSnow);
                                WontAdd(Enums.SkillIDs.Thunderbolt);
                                WontAdd(Enums.SkillIDs.AngerofStomper);
                                WontAdd(Enums.SkillIDs.HorrorofStomper);
                                WontAdd(Enums.SkillIDs.PeaceofStomper);
                                WontAdd(Enums.SkillIDs.FreezingPelter);
                                WontAdd(Enums.SkillIDs.RevengeTail);
                                WontAdd(Enums.SkillIDs.Circle);
                                WontAdd(Enums.SkillIDs.Rectangle);
                                WontAdd(Enums.SkillIDs.FrostGazeII);
                                WontAdd(Enums.SkillIDs.FrostGazeIII);
                                WontAdd(Enums.SkillIDs.ShadowofChaser);
                                WontAdd(Enums.SkillIDs.ThundercloudAttack);
                                WontAdd(Enums.SkillIDs.DestinyFire);
                                WontAdd(Enums.SkillIDs.FireShower);
                                WontAdd(Enums.SkillIDs.WheelOfDevastation);
                                break;
                        }
                        break;
                    }
                #endregion
            }
            Add(Enums.SkillIDs.Bless);

            #region Re-Learn Profs

            foreach (Interfaces.IProf Prof in Profs)
            {
                if (Prof == null)
                    continue;

                Prof.Available = false;
                Prof.PreviousLevel = Prof.Level;
                Prof.Level = 0;
                Prof.Experience = 0;
                Entity.Owner.Proficiencies.Add(Prof.ID, Prof);
                Prof.Send(Entity.Owner);
            }

            #endregion

            #region Re-Learn Skills

            foreach (Interfaces.ISkill Skill in Skills)
            {
                if (Skill == null)
                    continue;

                Skill.Available = false;
                Skill.PreviousLevel = Skill.Level;
                Skill.Level = 0;
                Skill.Experience = 0;

                if (!WontLearn.ContainsKey(Skill.ID))
                {
                    Entity.Owner.Spells.Add(Skill.ID, Skill);
                    Skill.Send(Entity.Owner);
                }
            }

            #endregion

            #region Learn Skills

            foreach (Interfaces.ISkill L in Learn.Values)
            {
                if (L == null)
                    continue;

                L.Available = false;
                Entity.Owner.Spells.Add(L.ID, L);
                L.Send(Entity.Owner);
            }

            #endregion

            #region Remove Skills

            foreach (Interfaces.ISkill L in WontLearn.Values)
            {
                if (L == null)
                    continue;

                L.Available = false;
                Entity.Owner.Spells.Remove(L.ID);
                Database.SkillTable.DeleteSpell(Entity.Owner, L.ID);
                L.Send(Entity.Owner);
            }

            #endregion

            Database.DataHolder.GetStats(Entity.Class, Entity.Level, Entity.Owner);
            Entity.Owner.CalculateStatBonus();
            Entity.Owner.CalculateHPBonus();
            Kernel.SendWorldMessage(
                new Network.GamePackets.Message(
                    "Congratulations, " + Entity.Name + " has reincarnated!", System.Drawing.Color.White,
                    Network.GamePackets.Message.TopLeft), Server.GamePool);
        }
        private void Add(Enums.SkillIDs S)
        {
            Interfaces.ISkill New = new Network.GamePackets.Spell(true);
            New.ID = (ushort)S;
            New.Level = 0;
            New.Experience = 0;
            New.PreviousLevel = 0;
            Learn.Add(New.ID, New);
        }
        private void WontAdd(Enums.SkillIDs S)
        {
            Interfaces.ISkill New = new Network.GamePackets.Spell(true);
            New.ID = (ushort)S;
            New.Level = 0;
            New.Experience = 0;
            New.PreviousLevel = 0;
            WontLearn.Add(New.ID, New);
        }
        private void WontAdd2(Enums.SkillIDs S)
        {
            Interfaces.ISkill New = new Network.GamePackets.Spell(true);
            New.ID = (ushort)S;
            New.Level = 0;
            New.Experience = 0;
            New.PreviousLevel = 0;
            WontLearn.Add(New.ID, New);
        }
        private int StatHP;
        public void CalculateHPBonus()
        {
            switch (Entity.Class)
            {
                case 11:
                    Entity.MaxHitpoints = (uint)(StatHP * 1.05F);
                    break;
                case 12:
                    Entity.MaxHitpoints = (uint)(StatHP * 1.08F);
                    break;
                case 13:
                    Entity.MaxHitpoints = (uint)(StatHP * 1.10F);
                    break;
                case 14:
                    Entity.MaxHitpoints = (uint)(StatHP * 1.12F);
                    break;
                case 15:
                    Entity.MaxHitpoints = (uint)(StatHP * 1.15F);
                    break;
                default:
                    Entity.MaxHitpoints = (uint)StatHP;
                    break;
            }
            Entity.MaxHitpoints += Entity.ItemHP;
            Entity.Hitpoints = Math.Min(Entity.Hitpoints, Entity.MaxHitpoints);
        }
        public void CalculateStatBonus()
        {
            byte ManaBoost = 5;
            const byte HitpointBoost = 24;
            sbyte Class = (sbyte)(Entity.Class / 10);
            if (Class == 13 || Class == 14)
                ManaBoost += (byte)(5 * (Entity.Class - (Class * 10)));
            StatHP = (ushort)((Entity.Strength * 3) +
                               (Entity.Agility * 3) +
                               (Entity.Spirit * 3) +
                               (Entity.Vitality * HitpointBoost));
            Entity.MaxMana = (ushort)((Entity.Spirit * ManaBoost) + Entity.ItemMP);
            Entity.Mana = Math.Min(Entity.Mana, Entity.MaxMana);
        }
        public void GemAlgorithm()
        {
            Entity.MaxAttack = Entity.Strength + Entity.BaseMaxAttack;
            Entity.MinAttack = Entity.Strength + Entity.BaseMinAttack;
            Entity.MagicAttack = Entity.BaseMagicAttack;
            if (Entity.Gems[0] != 0)
            {
                Entity.MagicAttack += (uint)Math.Floor(Entity.MagicAttack * (double)(Entity.Gems[0] * 0.01));
            }
            if (Entity.Gems[1] != 0)
            {
                Entity.MaxAttack += (uint)Math.Floor(Entity.MaxAttack * (double)(Entity.Gems[1] * 0.01));
                Entity.MinAttack += (uint)Math.Floor(Entity.MinAttack * (double)(Entity.Gems[1] * 0.01));
            }
        }
    }
}
