using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
using W=Revora.Game.Features.DisCityWorld;

namespace Revora.Game.Features
{
    internal static class DisCityTreasury
    {
        internal const uint Key=3006982;
        internal static Func<int,int> Roll=n=>DisCityPacks.Roll(n);
        private sealed class Offer {internal uint Key;internal Guid Epoch;internal DateTime Until;}
        private static readonly ConditionalWeakTable<GameState,Offer> offers=new ConditionalWeakTable<GameState,Offer>();
        private static bool enabled;

        internal static void Initialize() {
            if(!ConquerItemInformation.BaseInformations.ContainsKey(Key))throw new InvalidOperationException("Dis City key catalog missing.");
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_dis_keys(
item_uid bigint PRIMARY KEY,uid bigint NOT NULL,run uuid UNIQUE NOT NULL REFERENCES revora_dis_completions(run) DEFERRABLE INITIALLY DEFERRED,issued_at timestamptz NOT NULL,used_at timestamptz);
CREATE TABLE IF NOT EXISTS revora_dis_treasury(uid bigint PRIMARY KEY,openings integer NOT NULL DEFAULT 0 CHECK(openings>=0),last_map integer NOT NULL DEFAULT 0);
CREATE TABLE IF NOT EXISTS revora_dis_treasury_claims(uid bigint NOT NULL,opening integer NOT NULL,key_uid bigint UNIQUE NOT NULL,rewards bigint[] NOT NULL,
claimed_at timestamptz NOT NULL,PRIMARY KEY(uid,opening));");}enabled=true;
        }
        internal static void Issue(NpgsqlConnection c,NpgsqlTransaction t,uint uid,Guid run,ConquerItem key) {
            if(key.ID!=Key || !key.Bound || key.StackSize!=1)throw new InvalidOperationException("Invalid issued treasury key.");
            Q.Execute(c,t,"INSERT INTO revora_dis_keys(item_uid,uid,run,issued_at) VALUES(@item,@uid,@run,@now)",
                "item",(long)key.UID,"uid",(long)uid,"run",run,"now",DisCityQuest.Clock());
        }
        private static bool Usable(GameState p,uint uid) {
            ConquerItem item;return p.Inventory.TryGetItem(uid,out item) && item.ID==Key && item.Bound && item.StackSize==1
                && item.Position==0 && item.Warehouse==0 && item.Lock==0 && item.Days==0 && !item.InWardrobe;
        }
        private static Guid? KeyRun(NpgsqlConnection c,NpgsqlTransaction t,GameState p,uint uid) {
            if(!Usable(p,uid) || !Q.Owns(c,t,p,uid,Key))return null;
            using(var q=Q.Command(c,t,@"SELECT k.run FROM revora_dis_keys k JOIN items i ON i.uid=k.item_uid JOIN revora_dis_completions v ON v.run=k.run AND v.uid=k.uid
WHERE k.item_uid=@item AND k.uid=@uid AND k.used_at IS NULL AND i.locked=0 AND i.days='0'",
                "item",(long)uid,"uid",(long)p.Entity.UID)){var value=q.ExecuteScalar();return value==null?(Guid?)null:(Guid)value;}
        }
        private static uint FindKey(NpgsqlConnection c,NpgsqlTransaction t,GameState p) {
            foreach(var item in p.Inventory.Objects.Where(i=>i.ID==Key).OrderBy(i=>i.UID))if(KeyRun(c,t,p,item.UID)!=null)return item.UID;return 0;
        }
        internal static bool WasInside(NpgsqlConnection c,NpgsqlTransaction t,GameState p) {
            using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_dis_treasury WHERE uid=@uid AND last_map=@map AND last_map<>0)",
                "uid",(long)p.Entity.UID,"map",(int)p.Entity.MapID))return Equals(q.ExecuteScalar(),true);
        }
        internal static string Enter(GameState p) {
            if(!enabled || !Q.Near(p,W.Saint,1002))return "Speak to Solar Saint in Twin City.";
            if(p.Entity.OnDeath!=null || p.InQualifier() || p.InTeamQualifier())return "Finish your current contest first.";
            Guid run;using(var c=DataHolder.MySqlConnection){c.Open();uint key=FindKey(c,null,p);if(key==0)return "Bring a Pluto's Key earned from your weekly victory.";run=KeyRun(c,null,p,key).Value;}
            if(!W.Players.ContainsKey(p.Entity.UID) && W.Players.Count>=W.Capacity)return "The treasury paths are occupied. Return shortly.";
            W.Enter(p,run,5,DisCityQuest.Clock(),i=>{
                using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"INSERT INTO revora_dis_treasury(uid,last_map) VALUES(@uid,@map)
ON CONFLICT(uid) DO UPDATE SET last_map=excluded.last_map","uid",(long)p.Entity.UID,"map",(int)i.Map.ID);}
            });offers.Remove(p);return "Pluto's hoard awaits. Bring your key to the Hell Treasury chest.";
        }
        private static W.Instance AtChest(GameState p) {
            W.Instance i;Revora.Interfaces.INpc chest;
            return W.Maps.TryGetValue(p.Entity.MapID,out i) && i.Player==p && i.Stage==5 && !i.Released && DisCityQuest.Clock()<i.Until
                && i.Map.Npcs.TryGetValue(W.Treasury,out chest) && Kernel.GetDistance(p.Entity.X,p.Entity.Y,chest.X,chest.Y)<=6?i:null;
        }
        private static uint[] Rewards(int opening) {
            if(opening%5==0)return new[]{3008058u,3008058u,3008059u};
            int draw=Roll(100);if(draw<0 || draw>=100)throw new InvalidOperationException("Treasury reward draw invalid.");
            return new[]{draw<50?3008057u:draw<85?3008058u:3008059u};
        }
        private static string Open(GameState p,uint key) {
            if(AtChest(p)==null || !Usable(p,key))return "Bring your earned key to your own treasury chest.";
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                    if(q.ExecuteScalar()==null)throw new InvalidOperationException("Treasury owner missing.");
                if(KeyRun(c,t,p,key)==null)return "That key does not belong to an unclaimed victory.";
                Q.Execute(c,t,"INSERT INTO revora_dis_treasury(uid) VALUES(@uid) ON CONFLICT(uid) DO NOTHING","uid",(long)p.Entity.UID);
                int opening;using(var q=Q.Command(c,t,"SELECT openings FROM revora_dis_treasury WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))opening=checked(Convert.ToInt32(q.ExecuteScalar())+1);
                int count=opening%5==0?3:1;if(p.Inventory.Count-1+count>40)return "Make two empty places for the fifth-opening prize. Your key is safe.";
                var rewards=Rewards(opening).Select(id=>Q.New(id)).ToArray();
                if(Q.Execute(c,t,@"DELETE FROM items WHERE uid=@item AND entityid=@uid AND id=@type AND position=0 AND warehouse=0
AND stacksize=1 AND bound=1 AND locked=0 AND days='0'","item",(long)key,"uid",(long)p.Entity.UID,"type",(int)Key)!=1)throw new InvalidOperationException("Treasury key ownership changed.");
                if(Q.Execute(c,t,"UPDATE revora_dis_keys SET used_at=@now WHERE item_uid=@item AND uid=@uid AND used_at IS NULL",
                    "now",DisCityQuest.Clock(),"item",(long)key,"uid",(long)p.Entity.UID)!=1)throw new InvalidOperationException("Treasury key receipt changed.");
                foreach(var item in rewards)Q.Insert(c,t,p.Entity.UID,item);
                Q.Execute(c,t,"INSERT INTO revora_dis_treasury_claims(uid,opening,key_uid,rewards,claimed_at) VALUES(@uid,@opening,@key,@rewards,@now)",
                    "uid",(long)p.Entity.UID,"opening",opening,"key",(long)key,"rewards",rewards.Select(i=>(long)i.UID).ToArray(),"now",DisCityQuest.Clock());
                Q.Execute(c,t,"UPDATE revora_dis_treasury SET openings=@opening WHERE uid=@uid","opening",opening,"uid",(long)p.Entity.UID);t.Commit();
                Q.Install(p,new[]{key},rewards);offers.Remove(p);
                return opening%5==0?"The fifth seal yields two Sacred Refinery Packs and a Yin Jade.":"The treasury grants your prize. "+(5-opening%5)+" more openings unlock the greater hoard.";
            }}
        }
        internal static bool HandleNpc(GameState p,NpcRequest request) {
            if(!enabled || p==null || request==null || p.ActiveNpc!=W.Treasury)return false;
            if(!Q.Player(p))return true;
            lock(p.ItemSyncRoot)try {
                var i=AtChest(p);if(!Q.Near(p,W.Treasury,p.Entity.MapID) || i==null)return true;
                var d=new Npcs(p);if(request.OptionID==255){offers.Remove(p);p.ActiveNpc=0;return true;}
                Offer offer;
                if(request.OptionID==1) {
                    d.Text(offers.TryGetValue(p,out offer) && offer.Epoch==i.Epoch && offer.Until>DisCityQuest.Clock()?Open(p,offer.Key):"Inspect the chest again before choosing a key.");
                }else {
                    offers.Remove(p);uint key;int openings;
                    using(var c=DataHolder.MySqlConnection){c.Open();key=FindKey(c,null,p);
                        using(var q=Q.Command(c,null,"SELECT openings FROM revora_dis_treasury WHERE uid=@uid","uid",(long)p.Entity.UID))openings=Convert.ToInt32(q.ExecuteScalar());}
                    d.Text("Each key opens this hoard once. Every fifth opening grants two Sacred Refinery Packs and a Yin Jade. Your tally is "+openings+".");
                    if(key!=0){offers.Add(p,new Offer{Key=key,Epoch=i.Epoch,Until=DisCityQuest.Clock().AddMinutes(1)});d.Option("Use one Pluto's Key.",1);}
                }
                d.Option("Leave the chest.",255);d.Send();
            }catch(Exception ex){Q.Fail(p,"dis-city-treasury",ex);}return true;
        }
        internal static bool TryUse(ConquerItem item,GameState p) {
            if(item==null || item.ID!=Key)return false;if(!enabled || !Q.Player(p))return true;
            lock(p.ItemSyncRoot)try {
                ConquerItem owned;if(!Q.Player(p) || p.Booth!=null || p.Trade==null || p.Trade.InTrade || !p.Inventory.TryGetItem(item.UID,out owned) || !ReferenceEquals(owned,item))return true;
                p.Send(AtChest(p)==null?"Ask Solar Saint in Twin City to lead you to the treasury, then use this key beside its chest.":Open(p,item.UID));
            }catch(Exception ex){Q.Fail(p,"dis-city-key",ex);}return true;
        }
    }
}
