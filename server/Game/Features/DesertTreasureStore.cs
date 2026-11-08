using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Runtime.CompilerServices;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features
{
    internal static partial class DesertTreasureStore
    {
        internal sealed class Run {internal int Stage;internal uint Soul;internal DateTime? PortraitUntil;}
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        internal static bool Eligible(GameState p){return Q.Player(p) && ReferenceEquals(p.Entity.Owner,p) && CharacterProgression.Managed(p)
            && p.Entity.Level>=98 && p.Entity.Reborn<=2 && p.Trade!=null && !p.Trade.InTrade && p.Booth==null && p.Entity.PokerTable==0;}
        private static void Ready(GameState p,Func<bool> context){if(!Eligible(p) || !Monitor.IsEntered(p.ItemSyncRoot) || context==null || !context())throw new InvalidOperationException("DesertTreasure context changed.");}
        internal static void Initialize(){
            foreach(uint id in new uint[]{721870,721871,721872,721873,721874,721875,721876,721877,721878,728249,723911})
                if(!ConquerItemInformation.BaseInformations.ContainsKey(id))throw new InvalidOperationException("Desert Treasure catalog missing.");
            uint[] families={20,80,56,81,86};string[] names={"TombBat","TombBatL103","BloodyBat","BloodyBatL108","VampireBat"};byte[] levels={102,103,107,108,108};
            for(int i=0;i<families.Length;i++){MonsterInformation m;if(!MonsterInformation.MonsterInformations.TryGetValue(families[i],out m) || m.Name!=names[i] || m.Level!=levels[i] || m.Guard || m.Reviver || m.Boss)throw new InvalidOperationException("Desert Treasure family changed.");}
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_desert_treasure(
uid bigint NOT NULL,life smallint NOT NULL CHECK(life BETWEEN 0 AND 2),stage smallint NOT NULL CHECK(stage BETWEEN 1 AND 8),
soul bigint NOT NULL,portrait_until timestamptz,inspected boolean NOT NULL DEFAULT false,rewards bigint[] NOT NULL DEFAULT '{}',PRIMARY KEY(uid,life));
CREATE TABLE IF NOT EXISTS revora_desert_treasure_materials(item_uid bigint PRIMARY KEY,uid bigint NOT NULL,life smallint NOT NULL,
type integer NOT NULL CHECK(type BETWEEN 721870 AND 721878),state smallint NOT NULL DEFAULT 0 CHECK(state BETWEEN 0 AND 2),
FOREIGN KEY(uid,life) REFERENCES revora_desert_treasure(uid,life));
CREATE TABLE IF NOT EXISTS revora_desert_treasure_quiz(uid bigint NOT NULL,life smallint NOT NULL,step smallint NOT NULL CHECK(step BETWEEN 0 AND 3),
PRIMARY KEY(uid,life),FOREIGN KEY(uid,life) REFERENCES revora_desert_treasure(uid,life));
CREATE TABLE IF NOT EXISTS revora_desert_treasure_captures(attempt uuid PRIMARY KEY,uid bigint NOT NULL,life smallint NOT NULL,body bigint NOT NULL,
FOREIGN KEY(uid,life) REFERENCES revora_desert_treasure(uid,life));
CREATE TABLE IF NOT EXISTS revora_desert_treasure_vault(uid bigint NOT NULL,life smallint NOT NULL,until_at timestamptz NOT NULL,stepped boolean NOT NULL DEFAULT false,
PRIMARY KEY(uid,life),FOREIGN KEY(uid,life) REFERENCES revora_desert_treasure(uid,life));
CREATE TABLE IF NOT EXISTS revora_desert_treasure_bids(id uuid PRIMARY KEY,uid bigint NOT NULL,life smallint NOT NULL,
price integer NOT NULL CHECK(price IN(30000,60000,99999)),won boolean NOT NULL,item_uid bigint,attempted_at timestamptz NOT NULL,
CHECK(won=(item_uid IS NOT NULL)),FOREIGN KEY(uid,life) REFERENCES revora_desert_treasure(uid,life));
CREATE TABLE IF NOT EXISTS revora_desert_treasure_recoveries(old_uid bigint PRIMARY KEY,new_uid bigint NOT NULL,uid bigint NOT NULL,life smallint NOT NULL,type integer NOT NULL,
FOREIGN KEY(uid,life) REFERENCES revora_desert_treasure(uid,life));");}
        }
        private static Run Read(NpgsqlConnection c,NpgsqlTransaction t,GameState p){
            using(var q=Q.Command(c,t,"SELECT stage,soul,portrait_until FROM revora_desert_treasure WHERE uid=@uid AND life=@life FOR UPDATE","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn))
            using(var r=q.ExecuteReader())return r.Read()?new Run{Stage=r.GetInt16(0),Soul=checked((uint)r.GetInt64(1)),PortraitUntil=r.IsDBNull(2)?(DateTime?)null:r.GetDateTime(2).ToUniversalTime()}:new Run();
        }
        internal static Run Load(GameState p){if(!Q.Player(p,false))return null;lock(p.ItemSyncRoot)using(var c=DataHolder.MySqlConnection){c.Open();return Read(c,null,p);}}
        private static void Owner(Npgsql.NpgsqlConnection c,Npgsql.NpgsqlTransaction t,GameState p)
        {
            SkillTable.LockOwner(c,t,p);
            using(var q=Q.Command(c,t,"SELECT reborn,level FROM entities WHERE uid=@uid","uid",(long)p.Entity.UID))using(var r=q.ExecuteReader())
                if(!r.Read() || Convert.ToInt32(r[0])!=p.Entity.Reborn || Convert.ToInt32(r[1])!=p.Entity.Level)throw new InvalidOperationException("DesertTreasure saved owner changed.");
            var saved=new List<uint>();using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid FOR UPDATE","uid",(long)p.Entity.UID))
            using(var r=q.ExecuteReader())while(r.Read())saved.Add(checked((uint)r.GetInt64(0)));
            if(!saved.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException("DesertTreasure bag changed.");
        }

        private static bool Material(NpgsqlConnection c,NpgsqlTransaction t,GameState p,uint uid,uint type){ConquerItem item;
            if(uid==0 || !p.Inventory.TryGetItem(uid,out item) || item.Lock!=0 || item.Suspicious || item.InWardrobe || item.Days!=0 || item.TimeLeftInMinutes!=0 || !Q.Owns(c,t,p,uid,type))return false;
            using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_desert_treasure_materials m JOIN items i ON i.uid=m.item_uid WHERE m.item_uid=@id AND m.uid=@uid AND m.life=@life AND m.type=@type AND m.state=0 AND i.locked=0 AND COALESCE(i.suspicious,0)=0 AND COALESCE(i.inwardrobe,0)=0 AND COALESCE(NULLIF(i.days,''),'0')='0')","id",(long)uid,"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"type",(int)type))return Equals(q.ExecuteScalar(),true);
        }
        private static List<uint> Materials(NpgsqlConnection c,NpgsqlTransaction t,GameState p,uint type){
            var ids=new List<uint>();using(var q=Q.Command(c,t,"SELECT item_uid FROM revora_desert_treasure_materials WHERE uid=@uid AND life=@life AND type=@type AND state=0 ORDER BY item_uid FOR UPDATE","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"type",(int)type))using(var r=q.ExecuteReader())while(r.Read())ids.Add(checked((uint)r.GetInt64(0)));return ids;
        }
        private static void Track(NpgsqlConnection c,NpgsqlTransaction t,GameState p,ConquerItem item){Q.Insert(c,t,p.Entity.UID,item);
            if(Q.Execute(c,t,"INSERT INTO revora_desert_treasure_materials(item_uid,uid,life,type) VALUES(@item,@uid,@life,@type)","item",(long)item.UID,"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"type",(int)item.ID)!=1)throw new InvalidOperationException("DesertTreasure material tracking failed.");
        }
        private static void Consume(NpgsqlConnection c,NpgsqlTransaction t,GameState p,uint uid,uint type){
            if(!Material(c,t,p,uid,type))throw new InvalidOperationException("DesertTreasure material ownership changed.");Q.Consume(c,t,p,uid,type);
            if(Q.Execute(c,t,"UPDATE revora_desert_treasure_materials SET state=1 WHERE item_uid=@item AND uid=@uid AND life=@life AND state=0","item",(long)uid,"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn)!=1)throw new InvalidOperationException("DesertTreasure consumption was not saved.");
        }
        private static void Save(NpgsqlConnection c,NpgsqlTransaction t,GameState p,int before,int after){
            if(Q.Execute(c,t,"UPDATE revora_desert_treasure SET stage=@after WHERE uid=@uid AND life=@life AND stage=@before","after",after,"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"before",before)!=1)throw new InvalidOperationException("DesertTreasure stage changed.");
        }
        // Referral and earned material hand-ins. Purchase, corpse capture and final
        // reward transactions have separate operations; action values cannot skip them.
        internal static bool Advance(GameState p,int action,Func<bool> context){
            Ready(p,context);var removed=new List<uint>();ConquerItem added=null;ulong silver=p.Entity.Money;
            DateTime now=Clock();if(now.Kind!=DateTimeKind.Utc)throw new InvalidOperationException("Desert Treasure clock must be UTC.");
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);if(run.Stage!=action || !(action==0 || action==1 || action==2 || action==4 || action==5))return false;
                if(action==0){
                    if(Q.Execute(c,t,"INSERT INTO revora_desert_treasure(uid,life,stage,soul) VALUES(@uid,@life,1,@soul)","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"soul",(long)QuestSoulReward.Create().ID)!=1)throw new InvalidOperationException("Desert Treasure acceptance failed.");
                }else{
                    if(action==4){using(var q=Q.Command(c,t,"SELECT inspected FROM revora_desert_treasure WHERE uid=@uid AND life=@life","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn))if(!Equals(q.ExecuteScalar(),true))return false;}
                    uint type=action==2?721870u:action==5?721871u:0;
                    if(action==5 && (!run.PortraitUntil.HasValue || now>run.PortraitUntil.Value || now<run.PortraitUntil.Value.AddSeconds(-60)))return false;
                    if(type!=0){uint item=Materials(c,t,p,type).FirstOrDefault(id=>Material(c,t,p,id,type));if(item==0)return false;Consume(c,t,p,item,type);removed.Add(item);}
                    uint grant=action==1?721873u:action==5?721874u:0;
                    if(grant!=0){if(p.Inventory.Count-removed.Count>=40)return false;added=Q.New(grant,true);Track(c,t,p,added);}
                    SilverBalance.AssertSaved(c,t,p);silver=checked(silver+100);if(silver>Entity.MaximumSilver)return false;
                    SilverBalance.Persist(c,t,p,silver);Save(c,t,p,action,action+1);
                }
                Ready(p,context);t.Commit();
            }}Q.Install(p,removed,added==null?new ConquerItem[0]:new[]{added});if(action>0)SilverBalance.ApplyCommitted(p,silver);return true;
        }
        internal static bool InspectPortrait(GameState p,uint item,Func<bool> context){
            Ready(p,context);
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);if(Read(c,t,p).Stage!=4 || !Material(c,t,p,item,721878))return false;
                if(Q.Execute(c,t,"UPDATE revora_desert_treasure SET inspected=true WHERE uid=@uid AND life=@life AND stage=4 AND NOT inspected","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn)!=1)return false;
                Ready(p,context);t.Commit();return true;
            }}
        }
        internal static bool RevealPortrait(GameState p,uint item,Func<bool> context){
            Ready(p,context);DateTime now=Clock();if(now.Kind!=DateTimeKind.Utc)throw new InvalidOperationException("Desert Treasure clock must be UTC.");
            var removed=new List<uint>();ConquerItem added=null;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);if(run.Stage!=5 || run.PortraitUntil.HasValue && now<=run.PortraitUntil.Value)return false;
                bool blank=Material(c,t,p,item,721878);if(!blank && !Material(c,t,p,item,721871))return false;
                var essence=Materials(c,t,p,721877).Where(id=>Material(c,t,p,id,721877)).Take(5).ToArray();if(essence.Length!=5)return false;
                foreach(uint uid in essence){Consume(c,t,p,uid,721877);removed.Add(uid);}
                if(blank){Consume(c,t,p,item,721878);removed.Add(item);added=Q.New(721871,true);Track(c,t,p,added);}
                if(Q.Execute(c,t,"UPDATE revora_desert_treasure SET portrait_until=@until WHERE uid=@uid AND life=@life AND stage=5","until",now.AddSeconds(60),"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn)!=1)throw new InvalidOperationException("Desert Treasure reveal changed.");
                Ready(p,context);t.Commit();
            }}Q.Install(p,removed,added==null?new ConquerItem[0]:new[]{added});return true;
        }
    }
}
