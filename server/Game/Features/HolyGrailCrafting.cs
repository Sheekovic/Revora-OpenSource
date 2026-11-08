using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class HolyGrailCrafting
    {
        internal const uint Grail=2100095;
        internal const int Invincible=20342034,Supreme=60116011;
        private sealed class Trophy {internal Guid Prize;internal uint Uid;internal ConquerItem Item;}
        private sealed class Offer {internal Guid Id;internal DateTime Until;internal List<Trophy> Materials;}
        private static readonly ConditionalWeakTable<GameState,Offer> offers=new ConditionalWeakTable<GameState,Offer>();
        internal static void Initialize() {
            ConquerItemBaseInformation info;
            if(!ConquerItemInformation.BaseInformations.TryGetValue(Grail,out info) || info.Name!="HolyGrail")throw new InvalidOperationException("Holy Grail catalog changed.");
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_holy_grail_crafts(
id uuid PRIMARY KEY,uid bigint NOT NULL CHECK(uid>0),item_uid bigint NOT NULL UNIQUE CHECK(item_uid>0),crafted_at timestamptz NOT NULL);
CREATE TABLE IF NOT EXISTS revora_holy_grail_materials(
craft uuid NOT NULL REFERENCES revora_holy_grail_crafts(id),prize uuid NOT NULL UNIQUE REFERENCES revora_guild_prizes(id),
item_uid bigint NOT NULL UNIQUE CHECK(item_uid>0),PRIMARY KEY(craft,prize));");}
        }
        internal static void Clear(GameState p){offers.Remove(p);}
        private static bool Usable(ConquerItem item) {
            return item!=null && item.ID==GuildPrizeRollover.Trophy && item.Position==0 && item.Warehouse==0 && item.StackSize==1 && item.Bound
                && item.Days==0 && item.TimeLeftInMinutes==0 && item.Lock==0 && !item.Suspicious && !item.InWardrobe;
        }
        private static bool Same(GameState p,Trophy trophy) {
            ConquerItem item;return p.Inventory.TryGetItem(trophy.Uid,out item) && ReferenceEquals(item,trophy.Item) && item.UID==trophy.Uid && Usable(item);
        }
        private static void Prepare(GameState p,Npcs d) {
            Clear(p);var now=GuildSeasonWar.Clock();var selected=new List<Trophy>();
            using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,@"SELECT p.id,p.trophy_uid FROM revora_guild_prizes p
WHERE p.uid=@uid AND p.tier=3 AND p.rolled_at<=@now AND p.trophy_uid=p.item_uid
AND NOT EXISTS(SELECT 1 FROM revora_holy_grail_materials m WHERE m.prize=p.id) ORDER BY p.ends_at,p.item_uid LIMIT 1000",
                "uid",(long)p.Entity.UID,"now",now))using(var r=q.ExecuteReader())while(r.Read()) {
                    uint uid=Convert.ToUInt32(r[1]);ConquerItem item;
                    if(!p.Inventory.TryGetItem(uid,out item) || !Usable(item))continue;
                    selected.Add(new Trophy{Prize=r.GetGuid(0),Uid=uid,Item=item});if(selected.Count==12)break;
                }}
            if(selected.Count!=12){d.Text("Bring twelve permanent Gold Trophies earned from Guild War seasons. Keep them in your bag, unlocked.");return;}
            if(p.Inventory.Count-12>=40){d.Text("Make room for the Holy Grail.");return;}
            offers.Add(p,new Offer{Id=Guid.NewGuid(),Until=now.AddMinutes(2),Materials=selected});
            d.Text("I can forge your twelve Gold Trophies into a bound Holy Grail. The Invincible title and Supreme wings will honor your deed.");
            d.Option("Forge the Holy Grail.",87);d.Option("Not yet.",80);
        }
        private static void Craft(GameState p,Npcs d) {
            Offer offer;if(!offers.TryGetValue(p,out offer)){d.Text("Show me your twelve Gold Trophies first.");return;}Clear(p);
            var now=GuildSeasonWar.Clock();
            if(now>=offer.Until || !offer.Materials.All(i=>Same(p,i))){d.Text("Show me your trophies again.");return;}
            if(p.Inventory.Count-12>=40){d.Text("Make room for the Holy Grail.");return;}
            var grail=Q.New(Grail,true);
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                using(var q=Q.Command(c,t,"SELECT mapid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                    if(Convert.ToUInt32(q.ExecuteScalar())!=1002)throw new InvalidOperationException("Holy Grail owner location changed.");
                using(var q=Q.Command(c,t,"SELECT count(*) FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0","uid",(long)p.Entity.UID))
                    if(Convert.ToInt64(q.ExecuteScalar())!=p.Inventory.Count)throw new InvalidOperationException("Holy Grail inventory requires reload.");
                foreach(var trophy in offer.Materials.OrderBy(i=>i.Uid)) {
                    using(var q=Q.Command(c,t,@"SELECT p.id FROM revora_guild_prizes p WHERE p.id=@prize AND p.uid=@uid AND p.tier=3
AND p.item_uid=@item AND p.trophy_uid=@item AND p.rolled_at<=@now
AND NOT EXISTS(SELECT 1 FROM revora_holy_grail_materials m WHERE m.prize=p.id) FOR UPDATE",
                        "prize",trophy.Prize,"uid",(long)p.Entity.UID,"item",(long)trophy.Uid,"now",now))
                        if(!Equals(q.ExecuteScalar(),trophy.Prize))throw new InvalidOperationException("Holy Grail trophy provenance changed.");
                    if(Q.Execute(c,t,@"DELETE FROM items WHERE uid=@item AND entityid=@uid AND id=2100085 AND position=0 AND warehouse=0
AND bound=1 AND stacksize=1 AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')='0'",
                        "item",(long)trophy.Uid,"uid",(long)p.Entity.UID)!=1)throw new InvalidOperationException("Holy Grail material ownership changed.");
                }
                if(Q.Execute(c,t,@"INSERT INTO items(uid,id,entityid,position,warehouse,stacksize,bound,durability,maximdurability)
VALUES(@item,2100095,@uid,0,0,1,1,@dur,@max)","item",(long)grail.UID,"uid",(long)p.Entity.UID,"dur",(int)grail.Durability,"max",(int)grail.MaximDurability)!=1)
                    throw new InvalidOperationException("Holy Grail item not saved.");
                if(Q.Execute(c,t,"INSERT INTO revora_holy_grail_crafts(id,uid,item_uid,crafted_at) VALUES(@craft,@uid,@item,@now)",
                    "craft",offer.Id,"uid",(long)p.Entity.UID,"item",(long)grail.UID,"now",now)!=1)throw new InvalidOperationException("Holy Grail craft not recorded.");
                foreach(var trophy in offer.Materials)
                    if(Q.Execute(c,t,"INSERT INTO revora_holy_grail_materials(craft,prize,item_uid) VALUES(@craft,@prize,@item)",
                        "craft",offer.Id,"prize",trophy.Prize,"item",(long)trophy.Uid)!=1)throw new InvalidOperationException("Holy Grail material receipt not recorded.");
                string key=offer.Id.ToString("N");
                if(!ProgressionEvidence.Grant(c,t,p.Entity.UID,"holy-grail-craft-v1",key+":title",Invincible,now)
                    || !ProgressionEvidence.Grant(c,t,p.Entity.UID,"holy-grail-craft-v1",key+":wings",Supreme,now))throw new InvalidOperationException("Holy Grail wardrobe grants not recorded.");
                if(!GuildSeasonClaims.Near(p) || GuildSeasonWar.Clock()>=offer.Until || !offer.Materials.All(i=>Same(p,i)))throw new InvalidOperationException("Holy Grail session changed.");
                t.Commit();
            }}
            Q.Install(p,offer.Materials.Select(i=>i.Uid),new[]{grail});Q.Refresh(p);
            d.Text("Your Holy Grail is forged. Invincible and Supreme now await you in the wardrobe.");
        }
        internal static void Menu(GameState p,byte option,Npcs d) {
            if(option==86)Prepare(p,d);else if(option==87)Craft(p,d);
        }
    }
}
