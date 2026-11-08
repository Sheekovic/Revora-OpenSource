using Revora.Network.GamePackets;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Revora.Client;
using System;
using Revora.Database;
using Revora.Network;
using ProtoBuf;

namespace Revora
{
    public static class PrestigueExtensions
    {
        public static TSource FindMax<TSource>(this IEnumerable<TSource> source, System.Func<TSource, object> selector)
        {
            object currentObject = null;
            foreach (var obj in source)
            {
                var value = selector(obj);
                if (currentObject == null || System.Convert.ToInt64(value) >= System.Convert.ToInt64(selector((TSource)System.Convert.ChangeType(currentObject, typeof(TSource)))))
                    currentObject = obj;
            }
            return (TSource)System.Convert.ChangeType(currentObject, typeof(TSource));
        }
        public static IEnumerable<TSource> FindMax<TSource>(this IEnumerable<TSource> source, System.Func<TSource, object> selector, int count, int skipCount = 0)
        {
            if (count >= source.Count())
                return source.OrderByDescending(selector);

            return source.Skip(skipCount).Take(count).OrderByDescending(selector);
        }
        public static int Value(this System.Enum myEnum)
        {
            return (int)System.Convert.ChangeType(myEnum, myEnum.GetTypeCode());
        }
    }
    public enum AbilitiesSort
    {
        Jiang = 1,
        Chi = 2,
        InnerPower = 3,
        Enchant = 4,
        Level = 5,
        PerfectionLevel = 6,
        Attributes = 7,
        Rebirth = 8,
        Quality = 9,
        Sockets = 10,
        GemQuality = 11,
        Plus = 12,
        SoulPhase = 13,
        MaterialPhase = 14,
        Protection = 15,
        SubClass = 16,
        Garments = 17,
        MountArmors = 18,
        NobilityRank = 19,
        ItemLevel = 20,
        RedRune = 21,
        BlueRune = 22,
        YellowRune = 23,
    }
    public class PerfectionManager
    {
        public static Dictionary<AbilitiesSort, List<AbilityScore>> AbilitiesScore = new Dictionary<AbilitiesSort, List<AbilityScore>>();
        public static Dictionary<int, RefineEffect> ItemsRefineEffects = new Dictionary<int, RefineEffect>();
        public static Dictionary<int, RefineAttribute> ItemsRefineAttributes = new Dictionary<int, RefineAttribute>();
        public static AbilityScore GetAbilityScore(AbilitiesSort abilitySort, int score)
        {
            var abilityScores = AbilitiesScore[abilitySort];

            foreach (var abilityScore in abilityScores)
                if (abilityScore.Min <= score && abilityScore.Max >= score)
                    return abilityScore;

            // Collections can outgrow the last configured prestige bracket.
            // Extra designs retain that bracket's score, without inventing points.
            if (abilitySort == AbilitiesSort.Garments || abilitySort == AbilitiesSort.MountArmors)
            {
                var highest = abilityScores.OrderByDescending(row => row.Max).FirstOrDefault();
                if (highest != null && score > highest.Max)
                    return highest;
            }

            throw new ArgumentOutOfRangeException("score", score,
                "No configured prestige bracket for " + abilitySort + ".");
        }
        public class RefineEffect
        {
            public int Id;
            public int PhysicalAttack;
            public int PhysicalDefense;
            public int MagicAttack;
            public int MagicDefense;
            public int ToxinEraserLevel;
            public int StrikeLockLevel;
            public int LuckyStrike;
            public int CalmWind;
            public int DrainingTouch;
            public int BloodSpawn;
            public int LightOfStamina;
            public int ShieldBreak;
            public int KillingFlash;
            public int MirrorOfSin;
            public int DivineGuard;
            public int CoreStrike;
            public int InvisableArrow;
            public int FreeSoul;
            public int StraightLife;
            public int AbsoluteLuck;
            internal RefineEffect WithRuneBonuses(int draining, int blood, int free)
            {
                var result = (RefineEffect)MemberwiseClone();
                result.DrainingTouch += draining;
                result.BloodSpawn += blood;
                result.FreeSoul += free;
                return result;
            }
        }
        public class AbilityScore
        {
            public int Id { get; set; }
            public AbilitiesSort Sort;
            public int Min, Max, Score;
            public static void Read(out Dictionary<AbilitiesSort, List<AbilityScore>> abilitiesInfo)
            {
                abilitiesInfo = new Dictionary<AbilitiesSort, List<AbilityScore>>();
                var readerInfo = new Dictionary<int, AbilityScore>();
                using (var memoryStream = new MemoryStream(File.ReadAllBytes("ability_score.txt")))
                {
                    using (var streamReader = new StreamReader(memoryStream))
                    {
                        var line = string.Empty;
                        while (!string.IsNullOrEmpty(line = streamReader.ReadLine()))
                        {
                            var spliter = line.Split(new string[] { "@@" }, System.StringSplitOptions.RemoveEmptyEntries);
                            readerInfo.Add(
                                int.Parse(spliter[0]),
                                new AbilityScore()
                                {
                                    Id = int.Parse(spliter[0]),
                                    Sort = (AbilitiesSort)short.Parse(spliter[1]),
                                    Min = int.Parse(spliter[2]),
                                    Max = int.Parse(spliter[3]),
                                    Score = int.Parse(spliter[4])
                                });
                        }
                    }
                }
                foreach (var ability in readerInfo.Values)
                {
                    if (!abilitiesInfo.ContainsKey(ability.Sort))
                        abilitiesInfo.Add(ability.Sort, new List<AbilityScore>());

                    abilitiesInfo[ability.Sort].Add(ability);
                }
            }
        }
        public class RefineCost
        {
            public uint Id;
            public uint Value;

        }
        public class RefineUpgrade
        {
            public uint Id;
            public uint Level;
            public uint UpgradeCost;
        }
        public class RefineAttribute
        {
            public int Id;
            public int EffectType;
            public int Level;
            public int dwParam;
            public int dwParam1;
            public int dwParam2;
            public int Value;
        }
        public static Dictionary<uint, RefineCost> ItemsRefineCost;
        public static Dictionary<uint, List<RefineUpgrade>> ItemsRefineUpgrade;
        public static void Load()
        {
            ItemsRefineCost = new Dictionary<uint, RefineCost>();
            AbilityScore.Read(out AbilitiesScore);
            using (var memoryStream = new MemoryStream(File.ReadAllBytes("item_refine_cost.txt")))
            {
                using (var streamReader = new StreamReader(memoryStream))
                {
                    var line = string.Empty;
                    while (!string.IsNullOrEmpty(line = streamReader.ReadLine()))
                    {
                        var spliter = line.Split(new string[] { "@@" }, System.StringSplitOptions.RemoveEmptyEntries);
                        ItemsRefineCost.Add(uint.Parse(spliter[0]), new RefineCost() { Id = uint.Parse(spliter[0]), Value = uint.Parse(spliter[1]) });
                    }
                }
            }
            ItemsRefineUpgrade = new Dictionary<uint, List<RefineUpgrade>>();
            using (var memoryStream = new MemoryStream(File.ReadAllBytes("item_refine_upgrade.txt")))
            {
                using (var streamReader = new StreamReader(memoryStream))
                {
                    var line = string.Empty;
                    while (!string.IsNullOrEmpty(line = streamReader.ReadLine()))
                    {
                        var spliter = line.Split(new string[] { "@@" }, System.StringSplitOptions.RemoveEmptyEntries).ToArray();
                        if (spliter.Length < 3)
                            continue;
                        var Id = uint.Parse(spliter[0]);
                        if (!ItemsRefineUpgrade.ContainsKey(Id))
                            ItemsRefineUpgrade.Add(Id, new List<RefineUpgrade>());
                        var perfectionLevel = uint.Parse(spliter[1]);
                        if (!ItemsRefineUpgrade[Id].Exists(upg => upg.Level == perfectionLevel))
                        {
                            var myObj = new RefineUpgrade();
                            myObj.Id = Id;
                            myObj.Level = perfectionLevel;
                            myObj.UpgradeCost = uint.Parse(spliter[2]);
                            ItemsRefineUpgrade[Id].Add(myObj);
                        }
                    }
                }
            }
            ItemsRefineAttributes = new Dictionary<int, RefineAttribute>();
            using (var memoryStream = new MemoryStream(File.ReadAllBytes("item_refine_attr.txt")))
            {
                using (var streamReader = new StreamReader(memoryStream))
                {
                    var line = string.Empty;
                    while (!string.IsNullOrEmpty(line = streamReader.ReadLine()))
                    {
                        var spliter = line.Split(new string[] { "@@" }, System.StringSplitOptions.RemoveEmptyEntries).ToArray();
                        if (spliter.Length < 7)
                            continue;
                        var refineAttr = new RefineAttribute();
                        refineAttr.Id = int.Parse(spliter[0]);
                        refineAttr.EffectType = int.Parse(spliter[1]);
                        refineAttr.Level = int.Parse(spliter[2]);
                        refineAttr.dwParam = int.Parse(spliter[3]);
                        refineAttr.dwParam1 = int.Parse(spliter[4]);
                        refineAttr.dwParam2 = int.Parse(spliter[5]);
                        refineAttr.Value = int.Parse(spliter[6]);
                        ItemsRefineAttributes.Add(refineAttr.Id, refineAttr);
                    }
                }
            }
            ItemsRefineEffects = new Dictionary<int, RefineEffect>();
            using (var memoryStream = new MemoryStream(File.ReadAllBytes("item_refine_effect.txt")))
            {
                using (var streamReader = new StreamReader(memoryStream))
                {
                    var line = string.Empty;
                    while (!string.IsNullOrEmpty(line = streamReader.ReadLine()))
                    {
                        var spliter = line.Split(new string[] { "@@" }, System.StringSplitOptions.RemoveEmptyEntries).ToArray();
                        if (spliter.Length < 21)
                            continue;
                        var refineEffect = new RefineEffect();

                        refineEffect.Id = int.Parse(spliter[0]);
                        refineEffect.PhysicalAttack = int.Parse(spliter[1]);
                        refineEffect.PhysicalDefense = int.Parse(spliter[2]);
                        refineEffect.MagicAttack = int.Parse(spliter[3]);
                        refineEffect.MagicDefense = int.Parse(spliter[4]);
                        refineEffect.ToxinEraserLevel = int.Parse(spliter[5]) % 10;
                        refineEffect.StrikeLockLevel = int.Parse(spliter[6]) % 10;
                        refineEffect.LuckyStrike = int.Parse(spliter[7]) % 10;
                        refineEffect.CalmWind = int.Parse(spliter[8]) % 10;
                        refineEffect.DrainingTouch = int.Parse(spliter[9]) % 10;
                        refineEffect.BloodSpawn = int.Parse(spliter[10]) % 10;
                        refineEffect.LightOfStamina = int.Parse(spliter[11]) % 10;
                        refineEffect.ShieldBreak = int.Parse(spliter[12]) % 10;
                        refineEffect.KillingFlash = int.Parse(spliter[13]) % 10;
                        refineEffect.MirrorOfSin = int.Parse(spliter[14]) % 10;
                        refineEffect.DivineGuard = int.Parse(spliter[15]) % 10;
                        refineEffect.CoreStrike = int.Parse(spliter[16]) % 10;
                        refineEffect.InvisableArrow = int.Parse(spliter[17]) % 10;
                        refineEffect.FreeSoul = int.Parse(spliter[18]) % 10;
                        refineEffect.StraightLife = int.Parse(spliter[19]) % 10;
                        refineEffect.AbsoluteLuck = int.Parse(spliter[20]) % 10;

                        ItemsRefineEffects.Add(refineEffect.Id, refineEffect);
                    }
                }
            }
        }
        public static uint GetItemsRefineCost(ConquerItem[] items)
        {
            uint value = 0;
            foreach (var item in items)
            {
                if (ItemsRefineCost.ContainsKey(item.ID))
                    value += ItemsRefineCost[item.ID].Value;
                else
                {
                    value += 0;
                }
            }
            return value;
        }
        public static uint ItemGetSort(uint ID)
        {
            uint ret = 1;
            uint Type1 = ID % 1000000;
            uint Type2 = Type1 / 1000;
            switch (Type2)
            {
                case 123:
                    return 1;
                case 203:
                    return 21;
                case 204:
                    return 22;
                case 300:
                    if (ID % 10 != 0)
                        return 14;
                    break;
                case 601:
                case 610:
                    return 4;
                case 201:
                case 202:
                    return 12;
                case 200:
                    return 20;
                case 350:
                    return 16;
                case 360:
                    return 15;
                case 370:
                    return 17;
                case 380:
                    return 18;
            }
            uint Type3 = ID % 10000000 / 100000;
            uint Type4 = Type1 / 10000;
            switch (Type3)
            {
                case 1:
                    switch (Type4)
                    {
                        case 11:
                        case 14:
                        case 17:
                            return 1;
                        case 18:
                        case 19:
                            return 11;
                        case 12:
                            return 2;
                        case 13:
                            return 3;
                        case 15:
                            return 7;
                        case 16:
                            return 8;
                    }
                    break;
                case 4:
                case 6:
                    return 4;
                case 5:
                    return 5;
                case 7:
                    return 9;
                case 9:
                    return 6;
                case 10:
                    return 0;
                default:
                    if (Type3 <= 29 && Type3 > 7)
                        return 9;
                    break;
            }
            return ret;
        }

        public static void ShowRank(GenericRanking ranking, GameState client)
        {
            PrestigeRanking.SendPage(client, ranking.RankingType, ranking.Page);
        }

        public static uint GetRank(GameState client)
        {
            var all = PrestigeRanking.Snapshot(client);
            var place = Array.FindIndex(all, e => e.Profile.UID == client.Entity.UID) + 1;
            return place <= 100 ? (uint)place : 0;
        }
                [ProtoContract]
        public class MsgUserAbilityScoreProto
        {
            [ProtoMember(1, IsRequired = true)]
            public uint ActionId;
            [ProtoMember(2, IsRequired = true)]
            public uint UID;
            [ProtoMember(3, IsRequired = true)]
            public uint Level;
            [ProtoMember(4, IsRequired = true)]
            public Scores[] Scores;
        }
        [ProtoContract]
        public class Scores
        {
            [ProtoMember(1, IsRequired = true)]
            public uint Type;
            [ProtoMember(2, IsRequired = true)]
            public uint Score;
        }
     //  public MsgUserAbilityScore() { }
        public MsgUserAbilityScoreProto Info;

        public static void DoEffects(Game.Entity attacker, Game.Entity attacked, ref uint damage)
        {
            if (attacker != null && attacker.Owner != null && attacker.Owner.ChampionGroup != null
                || attacked != null && attacked.Owner != null && attacked.Owner.ChampionGroup != null) return;
            if (attacker.Perfection != null && attacked != null)
            {
                var canDO = attacked.Perfection == null || attacked.Owner == null
                    || attacked.Owner.Equipment == null ? true : attacker.Owner.Equipment.GetTPL() > attacked.Owner.Equipment.GetTPL();
                if (canDO)
                {
                    if (attacker.ToxicFogPercent > 0 && Success(attacker.Perfection.ToxinEraserLevel))
                    {
                        attacker.ToxicFogPercent /= 2;
                        attacker.Owner.SendScreen(new CMsgRefineEffect(attacker.UID, RefineEffects.ToxinEraserLevel), true);
                    }
                    else if (Success(attacker.Perfection.LuckyStrike))
                    {
                        damage *= 1/2;
                        attacker.Owner.SendScreen(new CMsgRefineEffect(attacker.UID, RefineEffects.LuckyStrike), true);
                    }
                    else if (Success(attacker.Perfection.LightOfStamina))
                    {
                        attacker.Stamina = 100;
                        attacker.Owner.SendScreen(new CMsgRefineEffect(attacker.UID, RefineEffects.LightOfStamina), true);
                    }
                    else if (Success(attacker.Perfection.AbsoluteLuck))
                    {
                        damage *= 1/2;
                        attacker.Owner.SendScreen(new CMsgRefineEffect(attacker.UID, RefineEffects.AbsoluteLuck), true);
                    }
                    else if (Success(attacker.Perfection.BloodSpawn))
                    {
                        attacker.Hitpoints = attacker.MaxHitpoints;
                        attacker.Mana = attacker.MaxMana;
                        attacker.Owner.SendScreen(new CMsgRefineEffect(attacker.UID, RefineEffects.BloodSpawn), true);
                    }
                    else if (Success(attacker.Perfection.CalmWind))
                    {
                        attacker.Owner.SendScreen(new CMsgRefineEffect(attacker.UID, RefineEffects.CalmWind), true);
                    }
                    else if (Success(attacker.Perfection.CoreStrike))
                    {
                        //damage += ((uint)(attacker.MagicDamageIncrease - attacked.Immunity) * 100);
                        attacker.Owner.SendScreen(new CMsgRefineEffect(attacker.UID, RefineEffects.CoreStrike), true);
                    }
                    else if (attacked.Perfection != null && Success(attacked.Perfection.DivineGuard))
                    {
                        //damage -= ((uint)(attacker.Defence * 5) / 100);
                        //client.Player.AddFlag3(Update.Flags.DivineGuard);
                        attacked.Owner.SendScreen(new CMsgRefineEffect(attacked.UID, RefineEffects.DivineGuard), true);
                    }
                    else if (Success(attacker.Perfection.DrainingTouch))
                    {
                        if (Success(80))
                        {
                            attacker.Hitpoints = attacker.MaxHitpoints;
                            attacker.Mana = attacker.MaxMana;
                            attacker.Owner.SendScreen(new CMsgRefineEffect(attacker.UID, RefineEffects.DrainingTouch), true);
                        }
                    }
                    else if (attacked.Perfection != null && Success(attacked.Perfection.FreeSoul))
                    {
                        if (attacked.ShackleTime > 0)
                        {
                            attacked.ShackleTime = 0;
                            attacked.RemoveFlag((ulong)FlagsUpdate.Flags.SoulShackle);
                            attacked.Owner.SendScreen(new CMsgRefineEffect(attacked.UID, RefineEffects.FreeSoul), true);
                        }
                    }
                    else if (Success(attacker.Perfection.InvisableArrow))
                    {
                        //damage += ((uint)(attacker.MagicAttack * 5) / 100);
                        attacker.Owner.SendScreen(new CMsgRefineEffect(attacker.UID, RefineEffects.InvisbleArrow), true);
                    }
                    else if (Success(attacker.Perfection.KillingFlash))
                    {
                        //attacker.Experience += attacker.Level >= 140 ? 0 : ((uint)(attacker.Experience * 5) / 100);
                        attacker.Owner.SendScreen(new CMsgRefineEffect(attacker.UID, RefineEffects.KillingFlash), true);
                    }
                    else if (attacked.Perfection != null && Success(attacked.Perfection.MirrorOfSin))
                    {
                        //attacked.Experience += attacked.Level >= 140 ? 0 : ((uint)(attacked.Experience * 5) / 100);
                        attacked.Owner.SendScreen(new CMsgRefineEffect(attacked.UID, RefineEffects.MirrorOfSin), true);
                    }
                    else if (Success(attacker.Perfection.ShieldBreak))
                    {
                        //damage += (uint)(attacked.Block * 100);
                        //client.Player.AddFlag3(Update.Flags.ShieldBreak);
                        attacker.Owner.SendScreen(new CMsgRefineEffect(attacker.UID, RefineEffects.ShiledBreak), true);
                    }
                    else if (attacked.Perfection != null && Success(attacked.Perfection.StraightLife))
                    {
                        if (attacked.Dead)
                        {
                            attacked.BringToLife();
                          //  attacked.Player.a7a -= 15;
                            attacked.Owner.SendScreen(new CMsgRefineEffect(attacked.UID, RefineEffects.ShiledBreak), true);
                        }
                    }
                }
            }
        }
        public void SendScore(Client.GameState client, Client.GameState Observer)
        {
            var packet = new MsgUserAbilityScoreProto();
            packet.ActionId = 0;
            packet.UID = client.Entity.UID;
            packet.Level = client.Entity.Level;
            packet.Scores = new Scores[20];
            for (int i = 0; i < packet.Scores.Length; i++)
            {
                byte Type = (byte)(i + 1);
                packet.Scores[i] = new Scores();
                packet.Scores[i].Type = Type;
                packet.Scores[i].Score = GetScoreValue(client, Type);
            }


            var proto = Kernel.FinalizeProtoBuf(packet, 3253);
            Observer.Send(proto);
        }
        public uint GetScoreValue(Client.GameState client, uint Type)
        {
            return new CMsgUserAbilityScore().GetScoreValue(client, Type);
        }
        public void Handle(Client.GameState client)
        {
            switch (Info.ActionId)
            {
                case 1://Observ
                    {
                        if (Kernel.GamePool.ContainsKey(Info.UID))
                        {
                            SendScore(client, client);
                            SendScore(Kernel.GamePool[Info.UID], client);
                        }
                        else
                        {
                            client.Send(Kernel.FinalizeProtoBuf(new MsgUserAbilityScoreProto()
                            {
                                ActionId = 1,
                                UID = 0
                            }, 3253));
                        }
                        break;
                    }
            }
        }
        public static int CalculatePerfectionChiPoints(Client.GameState client)
        {
            if (client.ChiData == null)
                return 0;

            int Point = 0;
            for (int i = 0; i < 4; i++)
            {
                if (client.ChiData.Powers.Count < 1) break;
                Point += ((client.ChiData.Powers[0].Attributes[i].Value / Game.Enums.ChiMaxValues(client.ChiData.Powers[0].Attributes[i].Type)) * 100) * 60;
            }
            for (int i = 0; i < 4; i++)
            {
                if (client.ChiData.Powers.Count < 2) break;
                Point += ((client.ChiData.Powers[1].Attributes[i].Value / Game.Enums.ChiMaxValues(client.ChiData.Powers[1].Attributes[i].Type)) * 100) * 60;
            }
            for (int i = 0; i < 4; i++)
            {
                if (client.ChiData.Powers.Count < 3) break;
                Point += ((client.ChiData.Powers[2].Attributes[i].Value / Game.Enums.ChiMaxValues(client.ChiData.Powers[2].Attributes[i].Type)) * 100) * 60;
            }
            for (int i = 0; i < 4; i++)
            {
                if (client.ChiData.Powers.Count < 4) break;
                Point += ((client.ChiData.Powers[3].Attributes[i].Value / Game.Enums.ChiMaxValues(client.ChiData.Powers[3].Attributes[i].Type)) * 100) * 60;
            }
            return Point;
        }
        public static int CalculatePerfectionJiangPoints(Client.GameState client)
        {
            int Points = 0;
            if (client.Entity.MyJiang != null)
            {
                for (int i = 0; i < 9; i++)
                {
                    if (client.Entity.MyJiang.Stagers[i] == null || !client.Entity.MyJiang.Stagers[i].Activate) break;
                    foreach (var star in client.Entity.MyJiang.Stagers[i].Stars)
                    {
                        if (star.Activate)
                            Points += star.Level * 100;
                    }
                }
            }
            return Points;
        }
        public static uint CalculateSubClassPoints(Client.GameState client)
        {
            uint Points = 0;
            return Points;
        }
        public static uint CalculatePerfectionItemPoints(ConquerItem item)
        {
            uint Points = 50;
            if (item == null) return 50;
            #region Plus
            if (!ItemHandler.IsTwoHand(item.ID))
            {
                if (item.Plus == 1) Points += 200;
                if (item.Plus == 2) Points += 600;
                if (item.Plus == 3) Points += 1200;
                if (item.Plus == 4) Points += 1800;
                if (item.Plus == 5) Points += 2600;
                if (item.Plus == 6) Points += 3500;
                if (item.Plus == 7) Points += 4800;
                if (item.Plus == 8) Points += 5800;
                if (item.Plus == 9) Points += 6800;
                if (item.Plus == 10) Points += 7800;
                if (item.Plus == 11) Points += 8800;
                if (item.Plus == 12) Points += 10000;
            }
            else
            {
                if (item.Plus == 1) Points += 400;
                if (item.Plus == 2) Points += 1200;
                if (item.Plus == 3) Points += 2400;
                if (item.Plus == 4) Points += 3600;
                if (item.Plus == 5) Points += 5200;
                if (item.Plus == 6) Points += 7000;
                if (item.Plus == 7) Points += 9600;
                if (item.Plus == 8) Points += 11600;
                if (item.Plus == 9) Points += 13600;
                if (item.Plus == 10) Points += 15600;
                if (item.Plus == 11) Points += 17600;
                if (item.Plus == 12) Points += 20000;
            }
            #endregion
            #region Quality
            if (!ItemHandler.IsTwoHand(item.ID))
            {
                if (item.ID % 10 == 9) Points += 500;
                if (item.ID % 10 == 8) Points += 300;
                if (item.ID % 10 == 7) Points += 200;
                if (item.ID % 10 == 6) Points += 100;
                if (item.ID % 10 > 0 && item.ID % 10 < 6) Points += 50;
            }
            else
            {
                if (item.ID % 10 == 9) Points += 1000;
                if (item.ID % 10 == 8) Points += 600;
                if (item.ID % 10 == 7) Points += 400;
                if (item.ID % 10 == 6) Points += 200;
                if (item.ID % 10 > 0 && item.ID % 10 < 6) Points += 100;
            }
            #endregion
            #region Soul
            if (!ItemHandler.IsTwoHand(item.ID))
            {
                if (item.Purification.PurificationLevel == 1) Points += 100;
                if (item.Purification.PurificationLevel == 2) Points += 300;
                if (item.Purification.PurificationLevel == 3) Points += 500;
                if (item.Purification.PurificationLevel == 4) Points += 800;
                if (item.Purification.PurificationLevel == 5) Points += 1200;
                if (item.Purification.PurificationLevel == 6) Points += 1600;
                if (item.Purification.PurificationLevel == 7) Points += 2000;
            }
            else
            {
                if (item.Purification.PurificationLevel == 1) Points += 200;
                if (item.Purification.PurificationLevel == 2) Points += 600;
                if (item.Purification.PurificationLevel == 3) Points += 1000;
                if (item.Purification.PurificationLevel == 4) Points += 1600;
                if (item.Purification.PurificationLevel == 5) Points += 2400;
                if (item.Purification.PurificationLevel == 6) Points += 3200;
                if (item.Purification.PurificationLevel == 7) Points += 4000;
            }
            #endregion
            #region Bless
            if (!ItemHandler.IsTwoHand(item.ID))
            {
                Points += (uint)(item.Bless * 100);
            }
            else
            {
                Points += (uint)(item.Bless * 200);
            }
            #region Refine
            if (!ItemHandler.IsTwoHand(item.ID))
            {
                if (item.ExtraEffect.EffectLevel == 1) Points += 100;
                if (item.ExtraEffect.EffectLevel == 2) Points += 400;
                if (item.ExtraEffect.EffectLevel == 3) Points += 800;
                if (item.ExtraEffect.EffectLevel == 4) Points += 1200;
                if (item.ExtraEffect.EffectLevel == 5) Points += 1600;
                if (item.ExtraEffect.EffectLevel == 6) Points += 2000;
            }
            else
            {
                if (item.ExtraEffect.EffectLevel == 1) Points += 200;
                if (item.ExtraEffect.EffectLevel == 2) Points += 800;
                if (item.ExtraEffect.EffectLevel == 3) Points += 1600;
                if (item.ExtraEffect.EffectLevel == 4) Points += 2400;
                if (item.ExtraEffect.EffectLevel == 5) Points += 3200;
                if (item.ExtraEffect.EffectLevel == 6) Points += 4000;
            }
            #endregion
            #endregion
            #region Level
            if (!ItemHandler.IsTwoHand(item.ID))
            {
                var lvl = (uint)Database.ConquerItemInformation.BaseInformations[item.ID].Level;
                if (lvl <= 120)
                    Points += lvl * 3;
                else if (lvl <= 130)
                    Points += lvl * 5;
                else if (lvl <= 140)
                    Points += lvl * 6;
            }
            else
            {
                var lvl = (uint)Database.ConquerItemInformation.BaseInformations[item.ID].Level;
                if (lvl <= 120)
                    Points += lvl * 6;
                else if (lvl <= 130)
                    Points += lvl * 10;
                else if (lvl <= 140)
                    Points += lvl * 12;
            }
            #endregion
            #region Gem
            if (!ItemHandler.IsTwoHand(item.ID))
            {
                if (item.SocketOne != (Game.Enums.Gem)0)
                {
                    if (item.SocketOne2 % 10 == 1) Points += 200;
                    if (item.SocketOne2 % 10 == 2) Points += 500;
                    if (item.SocketOne2 % 10 == 3) Points += 800;
                }
                if (item.SocketTwo != (Game.Enums.Gem)0)
                {
                    if (item.SocketTwo2 % 10 == 1) Points += 200;
                    if (item.SocketTwo2 % 10 == 2) Points += 500;
                    if (item.SocketTwo2 % 10 == 3) Points += 800;
                }
            }
            else
            {
                if (item.SocketOne != (Game.Enums.Gem)0)
                {
                    if (item.SocketOne2 % 10 == 1) Points += 400;
                    if (item.SocketOne2 % 10 == 2) Points += 1000;
                    if (item.SocketOne2 % 10 == 3) Points += 1600;
                }
                if (item.SocketTwo != (Game.Enums.Gem)0)
                {
                    if (item.SocketTwo2 % 10 == 1) Points += 400;
                    if (item.SocketTwo2 % 10 == 2) Points += 1000;
                    if (item.SocketTwo2 % 10 == 3) Points += 1600;
                }
            }
            #endregion
            #region PerfectionLevel
            if (item.PerfectionLevel >= 1) Points += 180;
            if (item.PerfectionLevel >= 2) Points += 180;
            if (item.PerfectionLevel >= 3) Points += 180;
            if (item.PerfectionLevel >= 4) Points += 180;
            if (item.PerfectionLevel >= 5) Points += 180;
            if (item.PerfectionLevel >= 6) Points += 180;
            if (item.PerfectionLevel >= 7) Points += 180;
            if (item.PerfectionLevel >= 8) Points += 180;
            if (item.PerfectionLevel >= 9) Points += 180;
            if (item.PerfectionLevel >= 10) Points += 2380;
            if (item.PerfectionLevel >= 11) Points += 400;
            if (item.PerfectionLevel >= 12) Points += 400;
            if (item.PerfectionLevel >= 13) Points += 400;
            if (item.PerfectionLevel >= 14) Points += 400;
            if (item.PerfectionLevel >= 15) Points += 400;
            if (item.PerfectionLevel >= 16) Points += 400;
            if (item.PerfectionLevel >= 17) Points += 400;
            if (item.PerfectionLevel >= 18) Points += 400;
            if (item.PerfectionLevel >= 19) Points += 5150;
            if (item.PerfectionLevel >= 20) Points += 650;
            if (item.PerfectionLevel >= 21) Points += 650;
            if (item.PerfectionLevel >= 22) Points += 650;
            if (item.PerfectionLevel >= 23) Points += 650;
            if (item.PerfectionLevel >= 24) Points += 650;
            if (item.PerfectionLevel >= 25) Points += 650;
            if (item.PerfectionLevel >= 26) Points += 650;
            if (item.PerfectionLevel >= 27) Points += 650;
            if (item.PerfectionLevel >= 28) Points += 100;
            if (item.PerfectionLevel >= 29) Points += 100;
            if (item.PerfectionLevel >= 30) Points += 100;
            if (item.PerfectionLevel >= 31) Points += 100;
            if (item.PerfectionLevel >= 32) Points += 100;
            if (item.PerfectionLevel >= 33) Points += 100;
            if (item.PerfectionLevel >= 34) Points += 100;
            if (item.PerfectionLevel >= 35) Points += 100;
            if (item.PerfectionLevel >= 36) Points += 100;
            if (item.PerfectionLevel >= 37) Points += 100;
            if (item.PerfectionLevel >= 38) Points += 100;
            if (item.PerfectionLevel >= 39) Points += 100;
            if (item.PerfectionLevel >= 40) Points += 100;
            if (item.PerfectionLevel >= 41) Points += 100;
            if (item.PerfectionLevel >= 42) Points += 100;
            if (item.PerfectionLevel >= 43) Points += 100;
            if (item.PerfectionLevel >= 44) Points += 100;
            if (item.PerfectionLevel >= 45) Points += 100;
            if (item.PerfectionLevel >= 46) Points += 100;
            if (item.PerfectionLevel >= 47) Points += 100;
            if (item.PerfectionLevel >= 48) Points += 100;
            if (item.PerfectionLevel >= 49) Points += 100;
            if (item.PerfectionLevel >= 50) Points += 100;
            if (item.PerfectionLevel >= 51) Points += 100;
            if (item.PerfectionLevel >= 52) Points += 100;
            if (item.PerfectionLevel >= 53) Points += 100;
            if (item.PerfectionLevel >= 54) Points += 200;// jheisson
            if (ItemHandler.IsTwoHand(item.ID))
            {
                if (item.PerfectionLevel >= 1) Points += 180;
                if (item.PerfectionLevel >= 2) Points += 180;
                if (item.PerfectionLevel >= 3) Points += 180;
                if (item.PerfectionLevel >= 4) Points += 180;
                if (item.PerfectionLevel >= 5) Points += 180;
                if (item.PerfectionLevel >= 6) Points += 180;
                if (item.PerfectionLevel >= 7) Points += 180;
                if (item.PerfectionLevel >= 8) Points += 180;
                if (item.PerfectionLevel >= 9) Points += 180;
                if (item.PerfectionLevel >= 10) Points += 2380;
                if (item.PerfectionLevel >= 11) Points += 400;
                if (item.PerfectionLevel >= 12) Points += 400;
                if (item.PerfectionLevel >= 13) Points += 400;
                if (item.PerfectionLevel >= 14) Points += 400;
                if (item.PerfectionLevel >= 15) Points += 400;
                if (item.PerfectionLevel >= 16) Points += 400;
                if (item.PerfectionLevel >= 17) Points += 400;
                if (item.PerfectionLevel >= 18) Points += 400;
                if (item.PerfectionLevel >= 19) Points += 5150;
                if (item.PerfectionLevel >= 20) Points += 650;
                if (item.PerfectionLevel >= 21) Points += 650;
                if (item.PerfectionLevel >= 22) Points += 650;
                if (item.PerfectionLevel >= 23) Points += 650;
                if (item.PerfectionLevel >= 24) Points += 650;
                if (item.PerfectionLevel >= 25) Points += 650;
                if (item.PerfectionLevel >= 26) Points += 650;
                if (item.PerfectionLevel >= 27) Points += 650;
                if (item.PerfectionLevel >= 28) Points += 100;
                if (item.PerfectionLevel >= 29) Points += 100;
                if (item.PerfectionLevel >= 30) Points += 100;
                if (item.PerfectionLevel >= 31) Points += 100;
                if (item.PerfectionLevel >= 32) Points += 100;
                if (item.PerfectionLevel >= 33) Points += 100;
                if (item.PerfectionLevel >= 34) Points += 100;
                if (item.PerfectionLevel >= 35) Points += 100;
                if (item.PerfectionLevel >= 36) Points += 100;
                if (item.PerfectionLevel >= 37) Points += 100;
                if (item.PerfectionLevel >= 38) Points += 100;
                if (item.PerfectionLevel >= 39) Points += 100;
                if (item.PerfectionLevel >= 40) Points += 100;
                if (item.PerfectionLevel >= 41) Points += 100;
                if (item.PerfectionLevel >= 42) Points += 100;
                if (item.PerfectionLevel >= 43) Points += 100;
                if (item.PerfectionLevel >= 44) Points += 100;
                if (item.PerfectionLevel >= 45) Points += 100;
                if (item.PerfectionLevel >= 46) Points += 100;
                if (item.PerfectionLevel >= 47) Points += 100;
                if (item.PerfectionLevel >= 48) Points += 100;
                if (item.PerfectionLevel >= 49) Points += 100;
                if (item.PerfectionLevel >= 50) Points += 100;
                if (item.PerfectionLevel >= 51) Points += 100;
                if (item.PerfectionLevel >= 52) Points += 100;
                if (item.PerfectionLevel >= 53) Points += 100;
                if (item.PerfectionLevel >= 54) Points += 200; //jheisson
            }
            #endregion
            #region Socket
            if (!ItemHandler.IsTwoHand(item.ID))
            {
                if (item.SocketOne != (Game.Enums.Gem)0) Points += 1000;
                if (item.SocketTwo != (Game.Enums.Gem)0) Points += 2500;
            }
            else
            {
                if (item.SocketOne != (Game.Enums.Gem)0) Points += 2000;
                if (item.SocketTwo != (Game.Enums.Gem)0) Points += 5000;
            }
            #endregion
            #region Enchant
            if (!ItemHandler.IsTwoHand(item.ID))
            {
                var enc = (uint)(item.Enchant);
                if (enc != 0)
                {
                    if (enc <= 200) Points += enc * 1;
                    if (enc <= 240) Points += (uint)(enc * 1.3);
                    if (enc <= 254) Points += (uint)(enc * 1.6);
                    if (enc <= 255) Points += enc * 2;
                }
            }
            else
            {
                var enc = (uint)(item.Enchant);
                if (enc != 0)
                {
                    if (enc <= 200) Points += enc * 2;
                    if (enc <= 240) Points += (uint)(enc * 2.6);
                    if (enc <= 254) Points += (uint)(enc * 3.2);
                    if (enc <= 255) Points += enc * 4;
                }
            }
            #endregion
            return Points;
        }
        private static bool Success(int percent)
        {
            if (percent == 0) return false;
            return Kernel.ChanceSuccess(Kernel.Random.Next(0, 20) + percent);
        }
    }
}
