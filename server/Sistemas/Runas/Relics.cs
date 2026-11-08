using Revora.Network.GamePackets;
using System;
using System.Collections.Generic;

namespace Revora.Network
{
    public class Relics
    {
        //public static Dictionary<RelicsID, List<Stru>> RelicsItemsInfo = new Dictionary<RelicsID, List<Stru>>()
        //{
        //    [RelicsID.WesternMirror] = new List<Stru>()
        //    {
        //     new Stru(){ minValue = 600 , maxvalue = 900, relictype = RelicType.Featured , type = RelicsAttribute.MAttack},
        //     new Stru(){ minValue = 100, maxvalue=200,  relictype= RelicType.Gold , type = RelicsAttribute.Immunity },
        //     new Stru(){ minValue = 100, maxvalue = 200, relictype = RelicType.Gold, type= RelicsAttribute.AntiBreak },
        //     new Stru(){ minValue = 300, maxvalue = 500, relictype = RelicType.Gold, type= RelicsAttribute.MAttackIncrease},
        //     new Stru(){ minValue = 100, maxvalue = 300, relictype = RelicType.Gold, type= RelicsAttribute.MDmgIncrease },
        //     new Stru(){ minValue = 30, maxvalue = 100, relictype = RelicType.Common, type= RelicsAttribute.Breakthrough },
        //     new Stru(){ minValue = 30, maxvalue = 49, relictype = RelicType.Common, type= RelicsAttribute.MDefence },
        //     new Stru(){ minValue = 100, maxvalue = 300, relictype = RelicType.Common, type= RelicsAttribute.PAttackIncrease },
        //     new Stru(){ minValue = 100, maxvalue = 300, relictype = RelicType.Common, type= RelicsAttribute.PDmgIncrease },
        //    },
        //    [RelicsID.ImperialTripodVessel] = new List<Stru>()
        //    {
        //     new Stru(){ minValue = 500 , maxvalue = 800, relictype = RelicType.Featured , type = RelicsAttribute.HP},
        //     new Stru(){ minValue = 100, maxvalue= 200,  relictype= RelicType.Gold , type = RelicsAttribute.AntiBreak },
        //     new Stru(){ minValue = 50, maxvalue = 60, relictype = RelicType.Gold, type= RelicsAttribute.MDefence },
        //     new Stru(){ minValue = 300, maxvalue = 500, relictype = RelicType.Gold, type= RelicsAttribute.PDmgIncrease},
        //     new Stru(){ minValue = 300, maxvalue = 500, relictype = RelicType.Gold, type= RelicsAttribute.MDmgIncrease },
        //     new Stru(){ minValue = 30, maxvalue = 100, relictype = RelicType.Common, type= RelicsAttribute.Immunity },
        //     new Stru(){ minValue = 30, maxvalue = 100, relictype = RelicType.Common, type= RelicsAttribute.Breakthrough },
        //     new Stru(){ minValue = 100, maxvalue = 300, relictype = RelicType.Common, type= RelicsAttribute.PAttack },
        //     new Stru(){ minValue = 100, maxvalue = 300, relictype = RelicType.Common, type= RelicsAttribute.MAttack },
        //    },
        //    [RelicsID.BeadsofCalmSea] = new List<Stru>()
        //    {
        //     new Stru(){ minValue = 100 , maxvalue = 200, relictype = RelicType.Featured , type = RelicsAttribute.SkillCriticalStrike},
        //     new Stru(){ minValue = 100, maxvalue= 200,  relictype= RelicType.Gold , type = RelicsAttribute.Immunity },
        //     new Stru(){ minValue = 300, maxvalue = 500, relictype = RelicType.Gold, type= RelicsAttribute.MAttackIncrease },
        //     new Stru(){ minValue = 100, maxvalue = 200, relictype = RelicType.Gold, type= RelicsAttribute.Breakthrough},
        //     new Stru(){ minValue = 300, maxvalue = 500, relictype = RelicType.Gold, type= RelicsAttribute.MDmgIncrease },
        //     new Stru(){ minValue = 100, maxvalue = 200, relictype = RelicType.Common, type= RelicsAttribute.AntiBreak },
        //     new Stru(){ minValue = 30, maxvalue = 50, relictype = RelicType.Common, type= RelicsAttribute.MDefence },
        //     new Stru(){ minValue = 100, maxvalue = 300, relictype = RelicType.Common, type= RelicsAttribute.PAttackIncrease },
        //     new Stru(){ minValue = 100, maxvalue = 300, relictype = RelicType.Common, type= RelicsAttribute.MDmgIncrease },
        //    },
        //    [RelicsID.HeartlessSeal] = new List<Stru>()
        //    {
        //     new Stru(){ minValue = 200 , maxvalue = 300, relictype = RelicType.Featured , type = RelicsAttribute.PAttack},
        //     new Stru(){ minValue = 100, maxvalue= 200,  relictype= RelicType.Gold , type = RelicsAttribute.Breakthrough},
        //     new Stru(){ minValue = 100, maxvalue = 200, relictype = RelicType.Gold, type= RelicsAttribute.AntiBreak },
        //     new Stru(){ minValue = 300, maxvalue = 500, relictype = RelicType.Gold, type= RelicsAttribute.PAttackIncrease},
        //     new Stru(){ minValue = 300, maxvalue = 500, relictype = RelicType.Gold, type= RelicsAttribute.PDmgIncrease },
        //     new Stru(){ minValue = 30, maxvalue = 100, relictype = RelicType.Common, type= RelicsAttribute.AntiBreak },
        //     new Stru(){ minValue = 30, maxvalue = 50, relictype = RelicType.Common, type= RelicsAttribute.MDefence },
        //     new Stru(){ minValue = 100, maxvalue = 300, relictype = RelicType.Common, type= RelicsAttribute.MAttackIncrease },
        //     new Stru(){ minValue = 100, maxvalue = 300, relictype = RelicType.Common, type= RelicsAttribute.MDmgIncrease},
        //    },
        //    [RelicsID.BellofDoom] = new List<Stru>()
        //    {
        //     new Stru(){ minValue = 100 , maxvalue = 200, relictype = RelicType.Featured , type = RelicsAttribute.CriticalStrike},
        //     new Stru(){ minValue = 100, maxvalue= 200,  relictype= RelicType.Gold , type = RelicsAttribute.Immunity},
        //     new Stru(){ minValue = 100, maxvalue = 200, relictype = RelicType.Gold, type= RelicsAttribute.AntiBreak },
        //     new Stru(){ minValue = 100, maxvalue = 200, relictype = RelicType.Gold, type= RelicsAttribute.Breakthrough},
        //     new Stru(){ minValue = 300, maxvalue = 500, relictype = RelicType.Gold, type= RelicsAttribute.PAttackIncrease },
        //     new Stru(){ minValue = 100, maxvalue = 300, relictype = RelicType.Common, type= RelicsAttribute.MDmgIncrease },
        //     new Stru(){ minValue = 30, maxvalue = 50, relictype = RelicType.Common, type= RelicsAttribute.MDefence },
        //     new Stru(){ minValue = 100, maxvalue = 300, relictype = RelicType.Common, type= RelicsAttribute.MAttackIncrease },
        //     new Stru(){ minValue = 100, maxvalue = 300, relictype = RelicType.Common, type= RelicsAttribute.PDmgIncrease},
        //    },
        //};
        // Revora starter relics: one featured stat and four distinct supporting stats.
        public static Dictionary<RelicsID, List<Stru>> RelicsItemsInfo = new Dictionary<RelicsID, List<Stru>>() {
            {RelicsID.WesternMirror, Profile(RelicsAttribute.MAttack)},
            {RelicsID.ImperialTripodVessel, Profile(RelicsAttribute.HP)},
            {RelicsID.BeadsofCalmSea, Profile(RelicsAttribute.SkillCriticalStrike)},
            {RelicsID.HeartlessSeal, Profile(RelicsAttribute.PAttack)},
            {RelicsID.BellofDoom, Profile(RelicsAttribute.CriticalStrike)}
        };
        private static List<Stru> Profile(RelicsAttribute featured) {
            var result=new List<Stru>();
            foreach(var type in new[]{featured,RelicsAttribute.HP,RelicsAttribute.PAttack,RelicsAttribute.MAttack,
                RelicsAttribute.Immunity,RelicsAttribute.AntiBreak}) {
                if(result.Exists(r=>r.type==type))continue;
                bool primary=type==featured;
                int max=type==RelicsAttribute.HP?600:type==RelicsAttribute.PAttack || type==RelicsAttribute.MAttack?300:100;
                result.Add(new Stru{type=type,minValue=max/2,maxvalue=max,relictype=primary?RelicType.Featured:RelicType.Gold});
                if(result.Count==5)break;
            }
            return result;
        }
        internal static ConquerItem CreateHuntRelic(uint id,Func<int,int> roll) {
            List<Stru> pool;
            if(!RelicsItemsInfo.TryGetValue((RelicsID)id,out pool))throw new ArgumentOutOfRangeException("id");
            var choices=new List<Stru>(pool);var values=new uint[3];
            for(int i=0;i<3;i++) {
                int index=i==0?0:roll(choices.Count);
                if(index<0 || index>=choices.Count)throw new InvalidOperationException("Invalid relic roll.");
                var a=choices[index];choices.RemoveAt(index);
                int draw=roll(a.maxvalue-a.minValue+1);
                if(draw<0 || draw>a.maxvalue-a.minValue)throw new InvalidOperationException("Invalid relic value roll.");
                values[i]=CreatRelicAttr(a.minValue+draw,(int)a.relictype,a.type);
            }
            return new ConquerItem(true){ID=id,StackSize=1,Durability=10000,MaximDurability=10000,
                RelicAttrib1=values[0],RelicAttrib2=values[1],RelicAttrib3=values[2]};
        }
        public class Stru
        {
            public RelicsAttribute type;
            public int minValue;
            public int maxvalue;
            public RelicType relictype;
        }
        public enum RelicType
        {
            Featured = 2,
            Gold = 1,
            Common = 0,
        }
        public enum RelicsID
        {
            WesternMirror = 4100001,
            ImperialTripodVessel = 4100002,
            BeadsofCalmSea = 4100003,
            HeartlessSeal = 4100004,
            BellofDoom = 4100005
        }
        public enum RelicsAttribute
        {
            None = 0,
            CriticalStrike = 1,
            SkillCriticalStrike = 2,
            Immunity = 3,
            Breakthrough = 4,
            AntiBreak = 5,
            HP = 6,
            PAttack = 7,
            MAttack = 8,
            MDefence = 9,
            PAttackIncrease = 10,
            MAttackIncrease = 11,
            PDmgIncrease = 12,
            MDmgIncrease = 13
        }
        public static int GetSoulCount(uint data)
        {
            if (data == 0)
                return 0;
            var stringdata = data.ToString("D6");
            var Type = (RelicType)int.Parse(stringdata.Substring(3, 1));
            switch (Type)
            {
                case RelicType.Common:
                    return 3;
                case RelicType.Gold:
                    return 6;
                case RelicType.Featured:
                    return 6;
            }
            return 0;
        }
        public static ConquerItem RandomRelic(uint ID = 0)
        {
            ConquerItem item = new ConquerItem(true);
            var relic = (RelicsID)Kernel.Random.Next(4100001, 4100006);
            if (ID == 0)
                item.ID = (uint)relic;
            else
                item.ID = ID;
            item.Color = Game.Enums.Color.White;
            item.Durability = item.MaximDurability = 10000;
            int val = Kernel.Random.Next(1, 6);
            var info = RelicsItemsInfo[relic];
            if (ID != 0)
            {
                info = RelicsItemsInfo[(RelicsID)ID];
            }
            List<RelicsAttribute> haved = new List<RelicsAttribute>();
            for (int i = 1; i <= val; i++)
            {
            again:
                var attr = info[new Random(Kernel.Random.Next()).Next(info.Count)];
                if (haved.Contains(attr.type))
                    goto again;
                haved.Add(attr.type);
                switch (i)
                {
                    case 1:
                        item.RelicAttrib1 = CreatRelicAttr(new Random(Kernel.Random.Next()).Next(attr.minValue, attr.maxvalue), (int)attr.relictype, attr.type);
                        break;
                    case 2:
                        item.RelicAttrib2 = CreatRelicAttr(new Random(Kernel.Random.Next()).Next(attr.minValue, attr.maxvalue), (int)attr.relictype, attr.type);
                        break;
                    case 3:
                        item.RelicAttrib3 = CreatRelicAttr(new Random(Kernel.Random.Next()).Next(attr.minValue, attr.maxvalue), (int)attr.relictype, attr.type);
                        break;
                    case 4:
                        item.RelicAttrib4 = CreatRelicAttr(new Random(Kernel.Random.Next()).Next(attr.minValue, attr.maxvalue), (int)attr.relictype, attr.type);
                        break;
                    case 5:
                        item.RelicAttrib5 = CreatRelicAttr(new Random(Kernel.Random.Next()).Next(attr.minValue, attr.maxvalue), (int)attr.relictype, attr.type);
                        break;
                }
            }
            return item;
        }
        public static ConquerItem RandomSuperRelic(uint ID = 0)
        {
            ConquerItem item = new ConquerItem(true);
            var relic = (RelicsID)Kernel.Random.Next(4100001, 4100006);
            if (ID == 0)
                item.ID = (uint)relic;
            else
                item.ID = ID;
            item.Color = Game.Enums.Color.White;
            item.Durability = item.MaximDurability = 10000;
            var info = RelicsItemsInfo[relic];
            if (ID != 0)
            {
                info = RelicsItemsInfo[(RelicsID)ID];
            }
            List<RelicsAttribute> haved = new List<RelicsAttribute>();
            for (int i = 1; i <= 5; i++)
            {
            again:
                var attr = info[new Random(Kernel.Random.Next()).Next(info.Count)];
                if (attr.relictype == RelicType.Common)
                    goto again;
                if (haved.Contains(attr.type))
                    goto again;
                haved.Add(attr.type);
                switch (i)
                {
                    case 1:
                        item.RelicAttrib1 = CreatRelicAttr(attr.maxvalue, (int)attr.relictype, attr.type);
                        break;
                    case 2:
                        item.RelicAttrib2 = CreatRelicAttr(attr.maxvalue, (int)attr.relictype, attr.type);
                        break;
                    case 3:
                        item.RelicAttrib3 = CreatRelicAttr(attr.maxvalue, (int)attr.relictype, attr.type);
                        break;
                    case 4:
                        item.RelicAttrib4 = CreatRelicAttr(attr.maxvalue, (int)attr.relictype, attr.type);
                        break;
                    case 5:
                        item.RelicAttrib5 = CreatRelicAttr(attr.maxvalue, (int)attr.relictype, attr.type);
                        break;
                }
            }
            return item;
        }
        public static uint CreatRelicAttr(int val, int isheight, RelicsAttribute type)
        {
            string result = val.ToString("D3") + isheight + ((int)(type)).ToString("D2");
            return uint.Parse(result);
        }
        public static void HandleRelicDurability(Client.GameState client, ConquerItem relic)
        {
            if (client.Entity.MapID == 1038 || client.Entity.MapID == 1005)
                return;
            if (relic.Durability > 0)
            {
                if (relic.Durability >= 100)
                    relic.Durability -= 100;
                else
                    relic.Durability = 0;
                Database.ConquerItemTable.UpdateDurabilityItem2(relic, relic.ID);
                Database.ConquerItemTable.UpdateDurabilityItem(relic);
                relic.Mode = Game.Enums.ItemMode.Update;
                relic.Send(client);
                relic.Mode = Game.Enums.ItemMode.Default;
            }
            else if (relic.Durability == 0)
            {
                _String str = new _String(true)
                {
                    UID = client.Entity.UID,
                    Type = 10,
                    TextsCount = 1
                };
                str.Texts.Add("EquipBroken");
                client.Send(str.ToArray());
                client.Send(new Message(Database.ConquerItemInformation.BaseInformations[relic.ID].Name + " is totaly damaged!", Message.TopLeft));
            }
        }
        public static void HandleRepair(Client.GameState client, ConquerItem item, uint CrystalNeeded)
        {
            if (client.Inventory.Contains(3306885, CrystalNeeded))
            {
                if (item.Durability < item.MaximDurability)
                {
                    client.Inventory.Remove(3306885, CrystalNeeded);
                    item.Durability = item.MaximDurability;
                    item.Mode = Game.Enums.ItemMode.Update;
                    item.Send(client);
                    Database.ConquerItemTable.UpdateDurabilityItem(item);
                }
            }
            else
                client.Send("You don't have " + CrystalNeeded + " RelicCrystal!");
        }
    }
}