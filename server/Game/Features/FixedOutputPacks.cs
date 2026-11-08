using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    // Ordinary use of 10-EXP-Ball pack 720757, MeteorBox 723268 and MeteorTearPack 723711.
    // 720757 and 723711 copy pack Bound. 723268 also preserves source binding.
    // 721330 stays an unresolved leftover and is not handled here.
    internal static class FixedOutputPacks
    {
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        internal static Action<string> Spoken;
        internal static Action<GameState> BeforeInstall;
        private static bool enabled;
        private sealed class Gift { internal uint ID; internal byte Plus, Bless; internal bool Bound; }
        private static bool Handles(uint id){return MountFor(id)!=0 || id==720757 || id==723268 || id==723711 || id==724002 || id==728238 || id==728251 || id==728236 || id==728237 || id==728239 || id==728240 || id==728243 || id==728244 || id==728245 || id==728246 || id==728241 || id==728242 || id==728247 || id==728248 || id==728249 || id==728250;}
        internal static void Initialize(){
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_fixed_output_pack_uses(
id uuid PRIMARY KEY,uid bigint NOT NULL CHECK(uid>0),item_uid bigint NOT NULL,
item_type integer NOT NULL,
before_stack integer NOT NULL CHECK(before_stack>=0),bound boolean NOT NULL,
rewards bigint[] NOT NULL,used_at timestamptz NOT NULL);
ALTER TABLE revora_fixed_output_pack_uses DROP CONSTRAINT IF EXISTS revora_fixed_output_pack_uses_item_type_check;
ALTER TABLE revora_fixed_output_pack_uses ADD CONSTRAINT revora_fixed_output_pack_uses_item_type_check CHECK(item_type IN (720757,723268,723711,724002,728238,728251,728236,728237,728239,728240,728243,728244,728245,728246,728241,728242,728247,728248,728249,728250,3000254,3000256,3000257,3000258,3000259,3000260,3000261,3000262,3000263,3000264,3000265,3000266,3000267,3000268,3000269,3000270,3000271));");}enabled=true;
        }
        internal static uint DesertExpeditionRingFor(byte job){
            bool taoist=job==100 || job==101 || job>=132 && job<=135 || job>=142 && job<=145;
            bool physical=job>=10 && job<=15 || job>=20 && job<=25 || job>=40 && job<=45 || job>=50 && job<=55
                || job>=60 && job<=65 || job>=70 && job<=75 || job>=80 && job<=85 || job>=160 && job<=165;
            if(!taoist && !physical)return 0;uint id=taoist?152229u:150219u;ConquerItemBaseInformation info;
            if(!ConquerItemInformation.BaseInformations.TryGetValue(id,out info) || info.Level!=(taoist?115:110) || info.Class!=(taoist?190:0)
                || info.Name!=(taoist?"DiamondBracelet":"DragonRing") || id%10!=9)throw new InvalidOperationException("Desert Expedition ring catalog changed.");
            return id;
        }
        internal static uint DesertBloodRingFor(byte job){
            bool taoist=job==100 || job==101 || job>=132 && job<=135 || job>=142 && job<=145;
            bool physical=job>=10 && job<=15 || job>=20 && job<=25 || job>=40 && job<=45 || job>=50 && job<=55
                || job>=60 && job<=65 || job>=70 && job<=75 || job>=80 && job<=85 || job>=160 && job<=165;
            if(!taoist && !physical)return 0;uint id=taoist?152168u:150178u;ConquerItemBaseInformation info;
            if(!ConquerItemInformation.BaseInformations.TryGetValue(id,out info) || info.Level!=(taoist?85:90) || info.Class!=(taoist?190:0)
                || info.Name!=(taoist?"JadeBracelet":"CrystalRing") || id%10!=8)throw new InvalidOperationException("Desert Blood ring catalog changed.");
            return id;
        }
        internal static uint FamilyFeudNecklaceFor(byte job){
            bool taoist=job==100 || job==101 || job>=132 && job<=135 || job>=142 && job<=145;
            bool physical=job>=10 && job<=15 || job>=20 && job<=25 || job>=40 && job<=45 || job>=50 && job<=55
                || job>=60 && job<=65 || job>=70 && job<=75 || job>=80 && job<=85 || job>=160 && job<=165;
            if(!taoist && !physical)return 0;uint id=taoist?121189u:120189u;ConquerItemBaseInformation info;
            if(!ConquerItemInformation.BaseInformations.TryGetValue(id,out info) || info.Level!=97 || info.Class!=(taoist?190:0)
                || info.Name!=(taoist?"GoldBag":"DragonNecklace") || id%10!=9)throw new InvalidOperationException("Family Feud necklace catalog changed.");
            return id;
        }
        internal static uint TomboyNecklaceFor(byte job){
            bool taoist=job==100 || job==101 || job>=132 && job<=135 || job>=142 && job<=145;
            bool physical=job>=10 && job<=15 || job>=20 && job<=25 || job>=40 && job<=45 || job>=50 && job<=55
                || job>=60 && job<=65 || job>=70 && job<=75 || job>=80 && job<=85 || job>=160 && job<=165;
            if(!taoist && !physical)return 0;uint id=taoist?121158u:120158u;ConquerItemBaseInformation info;
            if(!ConquerItemInformation.BaseInformations.TryGetValue(id,out info) || info.Level!=82 || info.Class!=(taoist?190:0)
                || info.Name!=(taoist?"JadeBag":"BasaltNecklace") || id%10!=8)throw new InvalidOperationException("Tomboy necklace catalog changed.");
            return id;
        }
        internal static uint GhostCityArmorFor(byte job){
            uint id=0;byte required=0,level=87;
            if(job>=10 && job<=15){id=130078;required=11;}
            else if(job>=20 && job<=25){id=131078;required=21;}
            else if(job>=40 && job<=45){id=133068;required=40;}
            else if(job>=50 && job<=55){id=135078;required=50;}
            else if(job>=60 && job<=65){id=136078;required=60;}
            else if(job>=70 && job<=75){id=139078;required=70;}
            else if(job>=80 && job<=85){id=138078;required=80;}
            else if(job==100 || job==101 || job>=132 && job<=135 || job>=142 && job<=145){id=134078;required=190;}
            else if(job>=160 && job<=165){id=101078;required=160;}
            if(id==0)return 0;ConquerItemBaseInformation info;
            if(!ConquerItemInformation.BaseInformations.TryGetValue(id,out info) || info.Level!=level || info.Class!=required || id%10!=8)
                throw new InvalidOperationException("Ghost City armor catalog changed.");
            return id;
        }
        internal static uint SpunGoldArmorFor(byte job){
            uint id=0;byte required=0,level=32;
            if(job>=10 && job<=15){id=130027;required=11;}
            else if(job>=20 && job<=25){id=131027;required=21;}
            else if(job>=40 && job<=45){id=133017;required=40;level=27;}
            else if(job>=50 && job<=55){id=135027;required=50;}
            else if(job>=60 && job<=65){id=136027;required=60;}
            else if(job>=70 && job<=75){id=139027;required=70;}
            else if(job>=80 && job<=85){id=138027;required=80;}
            else if(job==100 || job==101 || job>=132 && job<=135 || job>=142 && job<=145){id=134027;required=190;}
            else if(job>=160 && job<=165){id=101027;required=160;}
            if(id==0)return 0;ConquerItemBaseInformation info;
            if(!ConquerItemInformation.BaseInformations.TryGetValue(id,out info) || info.Level!=level || info.Class!=required || id%10!=7)
                throw new InvalidOperationException("Spun Gold armor catalog changed.");
            return id;
        }
        internal static uint ArtisanWeaponFor(byte job){
            uint id=0;byte required=0,level=50;
            if(job>=10 && job<=15)id=410097;
            else if(job>=20 && job<=25)id=510097;
            else if(job>=40 && job<=45){id=500087;required=40;}
            else if(job>=50 && job<=55){id=601097;required=50;}
            else if(job>=60 && job<=65){id=610097;required=60;}
            else if(job>=70 && job<=75){id=611097;required=70;}
            else if(job>=80 && job<=85){id=617097;required=80;}
            else if(job==100 || job==101 || job>=132 && job<=135 || job>=142 && job<=145){id=421087;required=190;level=45;}
            else if(job>=160 && job<=165){id=626097;required=160;}
            if(id==0)return 0;ConquerItemBaseInformation info;
            if(!ConquerItemInformation.BaseInformations.TryGetValue(id,out info) || info.Level!=level || info.Class!=required || id%10!=7)
                throw new InvalidOperationException("Artisan weapon catalog changed.");
            return id;
        }
        internal static uint FranticWeaponFor(byte job){
            uint id=0;byte required=0,level=55;
            if(job>=10 && job<=15)id=410108;
            else if(job>=20 && job<=25)id=510108;
            else if(job>=40 && job<=45){id=500098;required=40;}
            else if(job>=50 && job<=55){id=601108;required=50;}
            else if(job>=60 && job<=65){id=610108;required=60;}
            else if(job>=70 && job<=75){id=611108;required=70;}
            else if(job>=80 && job<=85){id=617108;required=80;}
            else if(job==100 || job==101 || job>=132 && job<=135 || job>=142 && job<=145){id=421108;required=190;level=55;}
            else if(job>=160 && job<=165){id=626108;required=160;}
            if(id==0)return 0;ConquerItemBaseInformation info;
            if(!ConquerItemInformation.BaseInformations.TryGetValue(id,out info) || info.Level!=level || info.Class!=required || id%10!=8)
                throw new InvalidOperationException("Frantic weapon catalog changed.");
            return id;
        }
        internal static uint MysticHeadgearFor(byte job){
            uint id=0;byte required=0;
            if(job>=10 && job<=15){id=118057;required=11;}
            else if(job>=20 && job<=25){id=111057;required=21;}
            else if(job>=40 && job<=45){id=113027;required=40;}
            else if(job>=50 && job<=55){id=112057;required=50;}
            else if(job>=60 && job<=65){id=143057;required=60;}
            else if(job>=70 && job<=75){id=144057;required=70;}
            else if(job>=80 && job<=85){id=148057;required=80;}
            else if(job==100 || job==101 || job>=132 && job<=135 || job>=142 && job<=145){id=114057;required=190;}
            else if(job>=160 && job<=165){id=170057;required=160;}
            if(id==0)return 0;ConquerItemBaseInformation info;
            if(!ConquerItemInformation.BaseInformations.TryGetValue(id,out info) || info.Level!=52 || info.Class!=required || id%10!=7)
                throw new InvalidOperationException("Mystic headgear catalog changed.");
            return id;
        }
        internal static uint MadManHeadgearFor(byte job){
            uint id=0;byte required=0,level=67;
            if(job>=10 && job<=15){id=118068;required=11;}
            else if(job>=20 && job<=25){id=111068;required=21;}
            else if(job>=40 && job<=45){id=113038;required=40;level=62;}
            else if(job>=50 && job<=55){id=112068;required=50;}
            else if(job>=60 && job<=65){id=143068;required=60;}
            else if(job>=70 && job<=75){id=144068;required=70;}
            else if(job>=80 && job<=85){id=148068;required=80;}
            else if(job==100 || job==101 || job>=132 && job<=135 || job>=142 && job<=145){id=114068;required=190;}
            else if(job>=160 && job<=165){id=170068;required=160;}
            if(id==0)return 0;ConquerItemBaseInformation info;
            if(!ConquerItemInformation.BaseInformations.TryGetValue(id,out info) || info.Level!=level || info.Class!=required || id%10!=8)
                throw new InvalidOperationException("Mad Man headgear catalog changed.");
            return id;
        }
        internal static uint HeroWingsHeadgearFor(byte job){
            uint id=0;byte required=0,level=97;
            if(job>=10 && job<=15){id=118089;required=11;}
            else if(job>=20 && job<=25){id=111089;required=21;}
            else if(job>=40 && job<=45){id=113069;required=40;level=92;}
            else if(job>=50 && job<=55){id=112089;required=50;}
            else if(job>=60 && job<=65){id=143089;required=60;}
            else if(job>=70 && job<=75){id=144089;required=70;}
            else if(job>=80 && job<=85){id=148089;required=80;}
            else if(job==100 || job==101 || job>=132 && job<=135 || job>=142 && job<=145){id=114089;required=190;}
            else if(job>=160 && job<=165){id=170089;required=160;}
            if(id==0)return 0;ConquerItemBaseInformation info;
            if(!ConquerItemInformation.BaseInformations.TryGetValue(id,out info) || info.Level!=level || info.Class!=required || id%10!=9)
                throw new InvalidOperationException("Hero Wings headgear catalog changed.");
            return id;
        }
        private static bool Ready(GameState p){return enabled && Q.Player(p) && p.Booth==null && p.Trade!=null && !p.Trade.InTrade;}
        private static void Full(GameState p){p.Send(Constants.FullInventory);var s=Spoken;if(s!=null)s("Your inventory is Full.");}
        internal static uint MountFor(uint id){
            switch(id){
                case 3000254:return 200407;
                case 3000256:return 200411;
                case 3000257:return 200412;
                case 3000258:return 200413;
                case 3000259:return 200414;
                case 3000260:return 200415;
                case 3000261:return 200416;
                case 3000262:return 200417;
                case 3000263:return 200418;
                case 3000264:return 200419;
                case 3000265:return 200420;
                case 3000266:return 200421;
                case 3000267:return 200427;
                case 3000268:return 200431;
                case 3000269:return 200438;
                case 3000270:return 200444;
                case 3000271:return 200442;
                default:return 0;
            }
        }
        private static List<Gift> Outputs(uint id,bool bound,byte job){
            var list=new List<Gift>();
            uint mount=MountFor(id);
            if(mount!=0){
                if(!CollectionAchievementCatalog.Mounts.Contains(mount) || !ConquerItemInformation.BaseInformations.ContainsKey(mount))
                    throw new InvalidOperationException("Mount pack catalog changed.");
                return new List<Gift>{new Gift{ID=mount,Plus=0,Bless=1,Bound=false}};
            }
            if(id==728251){
                uint armor=PowerBookStore.ArmorFor(job);if(armor==0)return null;
                return new List<Gift>{new Gift{ID=armor,Plus=0,Bound=bound},new Gift{ID=723911,Plus=0,Bound=true}};
            }
            if(id==728250){
                uint ring=DesertExpeditionRingFor(job);if(ring==0)return null;
                return new List<Gift>{new Gift{ID=ring,Plus=0,Bound=bound}};
            }
            if(id==728249){
                ConquerItemBaseInformation boots;
                if(!ConquerItemInformation.BaseInformations.TryGetValue(160199,out boots) || boots.Level!=100 || boots.Class!=0 || boots.Name!="KylinBoots")
                    throw new InvalidOperationException("Desert Treasure boots catalog changed.");
                return new List<Gift>{new Gift{ID=160199,Plus=0,Bound=bound}};
            }
            if(id==728248){
                uint necklace=FamilyFeudNecklaceFor(job);if(necklace==0)return null;
                return new List<Gift>{new Gift{ID=necklace,Plus=0,Bound=bound}};
            }
            if(id==728247){
                uint head=HeroWingsHeadgearFor(job);if(head==0)return null;
                return new List<Gift>{new Gift{ID=head,Plus=0,Bound=bound}};
            }
            if(id==728242){
                uint ring=DesertBloodRingFor(job);if(ring==0)return null;
                return new List<Gift>{new Gift{ID=ring,Plus=0,Bound=bound}};
            }
            if(id==728241){
                uint necklace=TomboyNecklaceFor(job);if(necklace==0)return null;
                return new List<Gift>{new Gift{ID=necklace,Plus=0,Bound=bound}};
            }
            if(id==728246){
                uint armor=GhostCityArmorFor(job);if(armor==0)return null;
                return new List<Gift>{new Gift{ID=armor,Plus=0,Bound=bound}};
            }
            if(id==728245){
                uint head=MadManHeadgearFor(job);if(head==0)return null;
                return new List<Gift>{new Gift{ID=head,Plus=0,Bound=bound}};
            }
            if(id==728244){
                ConquerItemBaseInformation boots;
                if(!ConquerItemInformation.BaseInformations.TryGetValue(160118,out boots) || boots.Level!=60 || boots.Class!=0 || boots.Name!="CrocodileBoots")
                    throw new InvalidOperationException("Soul of the General boots catalog changed.");
                return new List<Gift>{new Gift{ID=160118,Plus=0,Bound=bound}};
            }
            if(id==728243){
                uint weapon=FranticWeaponFor(job);if(weapon==0)return null;
                return new List<Gift>{new Gift{ID=weapon,Plus=0,Bound=bound}};
            }
            if(id==728240){
                uint headgear=MysticHeadgearFor(job);if(headgear==0)return null;
                return new List<Gift>{new Gift{ID=headgear,Plus=0,Bound=bound}};
            }
            if(id==728239){
                uint weapon=ArtisanWeaponFor(job);if(weapon==0)return null;
                return new List<Gift>{new Gift{ID=weapon,Plus=0,Bound=bound}};
            }
            if(id==728237){
                uint armor=SpunGoldArmorFor(job);if(armor==0)return null;
                return new List<Gift>{new Gift{ID=armor,Plus=0,Bound=bound}};
            }
            if(id==728236) {
                ConquerItemBaseInformation necklace;
                if(!ConquerItemInformation.BaseInformations.TryGetValue(120046,out necklace) || necklace.Level!=27 || necklace.Class!=0 || necklace.Name!="ThreadNecklace")
                    throw new InvalidOperationException("Connector necklace catalog changed.");
                return new List<Gift>{new Gift{ID=120046,Plus=0,Bound=bound}};
            }
            if(id==728238) {
                ConquerItemBaseInformation ring;
                if(!ConquerItemInformation.BaseInformations.TryGetValue(151077,out ring) || ring.Level!=43 || ring.Class!=0 || ring.Name!="IvoryHeavyRing")
                    throw new InvalidOperationException("Azure Pill ring catalog changed.");
                return new List<Gift>{new Gift{ID=151077,Plus=0,Bound=bound}};
            }
            int n;uint gift;bool copy;
            if(id==724002){n=3;gift=711504u;copy=bound;}
            else if(id==720757){n=10;gift=723700u;copy=bound;}
            else if(id==723711){n=5;gift=1088002u;copy=bound;}
            else{n=5;gift=1088001u;copy=bound;}
            for(int i=0;i<n;i++)list.Add(new Gift{ID=gift,Plus=0,Bound=copy});
            return list;
        }
        private static ConquerItem Item(Gift g){
            ushort dur=1;
            if(ConquerItemInformation.BaseInformations.ContainsKey(g.ID))dur=ConquerItemInformation.BaseInformations[g.ID].Durability;
            return new ConquerItem(true){ID=g.ID,UID=ConquerItem.ItemUID.Next,Plus=g.Plus,Bless=g.Bless,Bound=g.Bound,StackSize=1,Durability=dur,MaximDurability=dur};
        }
        internal static bool TryHandle(GameState p,byte[] packet){
            ConquerItem item;
            if(p==null || p.Inventory==null || packet==null || packet.Length<12 || !p.Inventory.TryGetItem(BitConverter.ToUInt32(packet,8),out item) || !Handles(item.ID))return false;
            if(packet.Length<32 || packet.Length>128 || BitConverter.ToUInt16(packet,0)+8!=packet.Length || BitConverter.ToUInt16(packet,2)!=1009
                || BitConverter.ToUInt16(packet,20)!=4 || BitConverter.ToUInt64(packet,packet.Length-8)!=Revora.Network.PacketHandler.ClientSeal)return true;
            TryUse(item,p);return true;
        }
        internal static bool TryUse(ConquerItem item,GameState p){
            if(item==null || !Handles(item.ID))return false;if(!Ready(p))return true;
            lock(p.ItemSyncRoot)try{
                ConquerItem owned;DateTime now=Clock();
                if(!Ready(p) || !p.Inventory.TryGetItem(item.UID,out owned) || !ReferenceEquals(item,owned) || item.Position!=0 || item.Warehouse!=0
                    || item.Lock!=0 || item.Suspicious || item.InWardrobe || (item.Days==0?item.TimeLeftInMinutes!=0:now.ToLocalTime()>=item.DayStamp.AddDays(item.Days)))return true;
                if(item.ID==720757 && p.Inventory.Count>39){Full(p);return true;}
                if(item.ID==723268 && p.Inventory.Count>30){Full(p);return true;}
                byte job=p.Entity.Class;var gifts=Outputs(item.ID,item.Bound,job);if(gifts==null)return true;int spaces=gifts.Count-(item.StackSize<=1?1:0);
                if(p.Inventory.Count+spaces>40){Full(p);return true;}
                if((item.ID==728249 || item.ID==728250 || MountFor(item.ID)!=0) && p.Inventory.Count+spaces>40){Full(p);return true;}
                var rewards=gifts.Select(Item).ToArray();
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                        if(q.ExecuteScalar()==null)throw new InvalidOperationException("Fixed pack owner missing.");
                    var ids=new List<uint>();using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid","uid",(long)p.Entity.UID))
                    using(var r=q.ExecuteReader())while(r.Read()){if(ids.Count>=40)throw new InvalidOperationException("Fixed pack inventory exceeds capacity.");ids.Add(checked((uint)Convert.ToInt64(r[0])));}
                    if(!ids.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException("Fixed pack inventory changed.");
                    string sql=item.StackSize<=1?"DELETE FROM items":"UPDATE items SET stacksize=stacksize-1";
                    if(Q.Execute(c,t,sql+@" WHERE uid=@item AND entityid=@uid AND id=@type AND warehouse=0 AND position=0 AND stacksize=@stack AND bound=@bound
AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')=@days AND (@permanent OR daystamp=@stamp)",
                        "item",(long)item.UID,"uid",(long)p.Entity.UID,"type",(int)item.ID,"stack",(int)item.StackSize,"bound",item.Bound?1:0,
                        "days",item.Days.ToString(CultureInfo.InvariantCulture),"permanent",item.Days==0,"stamp",item.DayStamp.ToBinary())!=1)
                        throw new InvalidOperationException("Fixed pack ownership changed.");
                    foreach(var reward in rewards)
                        if(Q.Execute(c,t,@"INSERT INTO items(uid,id,entityid,position,warehouse,stacksize,bound,plus,bless,durability,maximdurability)
VALUES(@item,@type,@uid,0,0,1,@bound,@plus,@bless,@dur,@max)","item",(long)reward.UID,"type",(int)reward.ID,"uid",(long)p.Entity.UID,
                            "bound",reward.Bound?1:0,"plus",(int)reward.Plus,"bless",(int)reward.Bless,"dur",(int)reward.Durability,"max",(int)reward.MaximDurability)!=1)
                            throw new InvalidOperationException("Fixed pack item was not saved.");
                    if(Q.Execute(c,t,@"INSERT INTO revora_fixed_output_pack_uses(id,uid,item_uid,item_type,before_stack,bound,rewards,used_at)
VALUES(@id,@uid,@item,@type,@stack,@bound,@rewards,@now)","id",Guid.NewGuid(),"uid",(long)p.Entity.UID,"item",(long)item.UID,
                        "type",(int)item.ID,"stack",(int)item.StackSize,"bound",item.Bound,"rewards",rewards.Select(r=>(long)r.UID).ToArray(),"now",now)!=1)
                        throw new InvalidOperationException("Fixed pack receipt was not saved.");
                    if(!Ready(p) || p.Entity.Class!=job)throw new InvalidOperationException("Fixed pack session changed.");t.Commit();
                }}
                var before=BeforeInstall;if(before!=null)before(p);
                Q.Install(p,item.StackSize<=1?new[]{item.UID}:new uint[0],rewards);
                if(item.StackSize>1){item.StackSize--;item.Mode=Enums.ItemMode.Update;item.Send(p);item.Mode=Enums.ItemMode.Default;}
            }catch(Exception ex){Q.Fail(p,"fixed-pack",ex);}return true;
        }
    }
}
