using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class DisCityRelics
    {
        internal const uint Sword=723088;
        internal static Func<int,int> Roll=n=>DisCityPacks.Roll(n);
        private static readonly uint[] Dresses={137023,137033,137043};
        private sealed class Offer {internal uint Sword;internal DateTime Until;}
        private static readonly ConditionalWeakTable<GameState,Offer> offers=new ConditionalWeakTable<GameState,Offer>();
        private static bool enabled;
        internal static void Initialize() {
            foreach(uint id in Dresses.Concat(new[]{Sword}))if(!ConquerItemInformation.BaseInformations.ContainsKey(id))throw new InvalidOperationException("Dis City relic catalog missing.");
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_dis_relics(
item_uid bigint PRIMARY KEY,uid bigint NOT NULL,run uuid NOT NULL REFERENCES revora_dis_completions(run) DEFERRABLE INITIALLY DEFERRED,
item_type integer NOT NULL,issued_at timestamptz NOT NULL,used_at timestamptz,UNIQUE(run,item_type));
CREATE TABLE IF NOT EXISTS revora_dis_relic_claims(item_uid bigint PRIMARY KEY,uid bigint NOT NULL,reward_uid bigint UNIQUE NOT NULL,reward_type integer NOT NULL,claimed_at timestamptz NOT NULL);");}enabled=true;
        }
        internal static bool DrawSword() {int draw=Roll(100);if(draw<0 || draw>=100)throw new InvalidOperationException("Dis City relic draw invalid.");return draw<20;}
        internal static void Issue(NpgsqlConnection c,NpgsqlTransaction t,uint uid,Guid run,ConquerItem item) {
            if(item.ID!=Sword && item.ID!=DisCityDarkHorn.Horn || !item.Bound || item.StackSize!=1)throw new InvalidOperationException("Invalid Dis City relic.");
            Q.Execute(c,t,"INSERT INTO revora_dis_relics(item_uid,uid,run,item_type,issued_at) VALUES(@item,@uid,@run,@type,@now)",
                "item",(long)item.UID,"uid",(long)uid,"run",run,"type",(int)item.ID,"now",DisCityQuest.Clock());
        }
        private static bool Owned(NpgsqlConnection c,NpgsqlTransaction t,GameState p,uint uid) {
            ConquerItem item;if(!p.Inventory.TryGetItem(uid,out item) || item.Lock!=0 || item.Days!=0 || item.InWardrobe || !Q.Owns(c,t,p,uid,Sword))return false;
            using(var q=Q.Command(c,t,@"SELECT EXISTS(SELECT 1 FROM revora_dis_relics r JOIN revora_dis_completions v ON v.run=r.run AND v.uid=r.uid
JOIN items i ON i.uid=r.item_uid WHERE r.item_uid=@item AND r.uid=@uid AND r.item_type=@type AND r.used_at IS NULL AND i.locked=0 AND i.days='0')",
                "item",(long)uid,"uid",(long)p.Entity.UID,"type",(int)Sword))return Equals(q.ExecuteScalar(),true);
        }
        private static string Claim(GameState p,byte choice) {
            Offer offer;if(!offers.TryGetValue(p,out offer) || offer.Until<=DisCityQuest.Clock())return "Show me your Star Sword before choosing a dress.";
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))if(q.ExecuteScalar()==null)throw new InvalidOperationException("Relic owner missing.");
                if(!Owned(c,t,p,offer.Sword))return "Bring the Star Sword earned in your own battle.";
                var reward=Q.New(Dresses[choice-40]);
                if(Q.Execute(c,t,@"DELETE FROM items WHERE uid=@item AND entityid=@uid AND id=@type AND position=0 AND warehouse=0 AND bound=1
AND stacksize=1 AND locked=0 AND days='0'","item",(long)offer.Sword,"uid",(long)p.Entity.UID,"type",(int)Sword)!=1)throw new InvalidOperationException("Relic item ownership changed.");
                if(Q.Execute(c,t,"UPDATE revora_dis_relics SET used_at=@now WHERE item_uid=@item AND uid=@uid AND used_at IS NULL",
                    "now",DisCityQuest.Clock(),"item",(long)offer.Sword,"uid",(long)p.Entity.UID)!=1)throw new InvalidOperationException("Relic receipt changed.");
                Q.Insert(c,t,p.Entity.UID,reward);Q.Execute(c,t,"INSERT INTO revora_dis_relic_claims(item_uid,uid,reward_uid,reward_type,claimed_at) VALUES(@item,@uid,@reward,@type,@now)",
                    "item",(long)offer.Sword,"uid",(long)p.Entity.UID,"reward",(long)reward.UID,"type",(int)reward.ID,"now",DisCityQuest.Clock());t.Commit();
                Q.Install(p,new[]{offer.Sword},new[]{reward});offers.Remove(p);return "Wear this "+ConquerItemInformation.BaseInformations[reward.ID].Name+" with pride. It occupies your armor slot.";
            }}
        }
        internal static bool HandleNpc(GameState p,NpcRequest request) {
            if(!enabled || p==null || request==null || p.ActiveNpc!=DisCityWorld.Saint
                || request.OptionID!=4 && (request.OptionID<40 || request.OptionID>42) && request.OptionID!=255)return false;
            if(!Q.Player(p))return true;
            lock(p.ItemSyncRoot)try {
                if(!Q.Near(p,DisCityWorld.Saint,1002))return request.OptionID!=255;
                if(request.OptionID==255){offers.Remove(p);return false;}
                var d=new Npcs(p);
                if(request.OptionID!=4)d.Text(Claim(p,request.OptionID));
                else {
                    offers.Remove(p);uint sword=0;
                    using(var c=DataHolder.MySqlConnection){c.Open();foreach(var item in p.Inventory.Objects.Where(i=>i.ID==Sword).OrderBy(i=>i.UID))if(Owned(c,null,p,item.UID)){sword=item.UID;break;}}
                    if(sword==0)d.Text("Bring a Star Sword from your victory over Pluto. I will offer you one of three exotic dresses.");
                    else {offers.Add(p,new Offer{Sword=sword,Until=DisCityQuest.Clock().AddMinutes(1)});d.Text("Choose one permanent bound dress. These exotic clothes replace your armor; they require level 100.");
                        d.Option("Phoenix Dress.",40);d.Option("Elegant Dress.",41);d.Option("Celestial Dress.",42);}
                }
                d.Option("Farewell.",255);d.Send();
            }catch(Exception ex){Q.Fail(p,"dis-city-relic",ex);}return true;
        }
        internal static bool TryUse(ConquerItem item,GameState p) {
            if(item==null || item.ID!=Sword)return false;if(!enabled || !Q.Player(p))return true;
            lock(p.ItemSyncRoot){ConquerItem owned;if(p.Inventory.TryGetItem(item.UID,out owned) && ReferenceEquals(owned,item))p.Send("Bring your Star Sword to Solar Saint in Twin City (327,250) and choose an exotic dress.");}return true;
        }
    }
}
