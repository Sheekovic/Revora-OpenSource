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
    internal static partial class TomboyStore
    {
        internal sealed class Run {internal int Stage;internal uint Soul;}
        internal static Func<int,int> Roll=max=>Kernel.Random.Next(max);
        private static readonly ConditionalWeakTable<Entity,object> deaths=new ConditionalWeakTable<Entity,object>();
        internal static bool Eligible(GameState p){return Q.Player(p) && ReferenceEquals(p.Entity.Owner,p) && CharacterProgression.Managed(p)
            && p.Entity.Level>=78 && p.Entity.Reborn<=2 && p.Trade!=null && !p.Trade.InTrade && p.Booth==null && p.Entity.PokerTable==0;}
        private static void Ready(GameState p,Func<bool> context){if(!Eligible(p) || !Monitor.IsEntered(p.ItemSyncRoot) || context==null || !context())throw new InvalidOperationException("Tomboy context changed.");}
        private static int Draw(int max){int value=Roll(max);if(value<0 || value>=max)throw new InvalidOperationException("Tomboy roll outside range.");return value;}
        internal static void Initialize(){
            foreach(uint id in new uint[]{710581,710582,710583,710584,721951,721952,721953,728241,723911})if(!ConquerItemInformation.BaseInformations.ContainsKey(id))throw new InvalidOperationException("Tomboy item catalog missing.");
            foreach(uint id in new uint[]{16,75}){MonsterInformation m;if(!MonsterInformation.MonsterInformations.TryGetValue(id,out m) || m.Name!=(id==16?"RockMonster":"RockMonsterL78") || m.Level!=(id==16?77:78) || m.Guard || m.Reviver || m.Boss)throw new InvalidOperationException("Tomboy monster catalog changed.");}
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_tomboy(
uid bigint NOT NULL,life smallint NOT NULL CHECK(life BETWEEN 0 AND 2),stage smallint NOT NULL CHECK(stage BETWEEN 1 AND 6),
soul bigint NOT NULL,rewards bigint[] NOT NULL DEFAULT '{}',PRIMARY KEY(uid,life));
CREATE TABLE IF NOT EXISTS revora_tomboy_materials(item_uid bigint PRIMARY KEY,uid bigint NOT NULL,life smallint NOT NULL,
type integer NOT NULL CHECK(type IN(710581,710582,710583,710584,721951,721952,721953)),state smallint NOT NULL DEFAULT 0 CHECK(state BETWEEN 0 AND 2),
FOREIGN KEY(uid,life) REFERENCES revora_tomboy(uid,life));
CREATE TABLE IF NOT EXISTS revora_tomboy_fusions(id uuid PRIMARY KEY,uid bigint NOT NULL,life smallint NOT NULL,
clicked bigint NOT NULL,partner bigint NOT NULL,result_type integer NOT NULL,result_uid bigint NOT NULL,success boolean NOT NULL,
FOREIGN KEY(uid,life) REFERENCES revora_tomboy(uid,life));
CREATE TABLE IF NOT EXISTS revora_tomboy_recoveries(old_uid bigint PRIMARY KEY,new_uid bigint NOT NULL,uid bigint NOT NULL,life smallint NOT NULL,type integer NOT NULL,
FOREIGN KEY(uid,life) REFERENCES revora_tomboy(uid,life));");}
        }
        private static Run Read(NpgsqlConnection c,NpgsqlTransaction t,GameState p){
            using(var q=Q.Command(c,t,"SELECT stage,soul FROM revora_tomboy WHERE uid=@uid AND life=@life FOR UPDATE","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn))
            using(var r=q.ExecuteReader())return r.Read()?new Run{Stage=r.GetInt16(0),Soul=checked((uint)r.GetInt64(1))}:new Run();
        }
        internal static Run Load(GameState p){if(!Q.Player(p,false))return null;lock(p.ItemSyncRoot)using(var c=DataHolder.MySqlConnection){c.Open();return Read(c,null,p);}}
        private static void Owner(Npgsql.NpgsqlConnection c,Npgsql.NpgsqlTransaction t,GameState p)
        {
            SkillTable.LockOwner(c,t,p);
            using(var q=Q.Command(c,t,"SELECT reborn,level FROM entities WHERE uid=@uid","uid",(long)p.Entity.UID))using(var r=q.ExecuteReader())
                if(!r.Read() || Convert.ToInt32(r[0])!=p.Entity.Reborn || Convert.ToInt32(r[1])!=p.Entity.Level)throw new InvalidOperationException("Tomboy saved owner changed.");
            var saved=new List<uint>();using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid FOR UPDATE","uid",(long)p.Entity.UID))
            using(var r=q.ExecuteReader())while(r.Read())saved.Add(checked((uint)r.GetInt64(0)));
            if(!saved.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException("Tomboy bag changed.");
        }

        private static bool Material(NpgsqlConnection c,NpgsqlTransaction t,GameState p,uint uid,uint type){ConquerItem item;
            if(uid==0 || !p.Inventory.TryGetItem(uid,out item) || item.Lock!=0 || item.Suspicious || item.InWardrobe || item.Days!=0 || item.TimeLeftInMinutes!=0 || !Q.Owns(c,t,p,uid,type))return false;
            using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_tomboy_materials m JOIN items i ON i.uid=m.item_uid WHERE m.item_uid=@id AND m.uid=@uid AND m.life=@life AND m.type=@type AND m.state=0 AND i.locked=0 AND COALESCE(i.suspicious,0)=0 AND COALESCE(i.inwardrobe,0)=0 AND COALESCE(NULLIF(i.days,''),'0')='0')","id",(long)uid,"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"type",(int)type))return Equals(q.ExecuteScalar(),true);
        }
        private static List<uint> Materials(NpgsqlConnection c,NpgsqlTransaction t,GameState p,uint type){
            var ids=new List<uint>();using(var q=Q.Command(c,t,"SELECT item_uid FROM revora_tomboy_materials WHERE uid=@uid AND life=@life AND type=@type AND state=0 ORDER BY item_uid FOR UPDATE","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"type",(int)type))using(var r=q.ExecuteReader())while(r.Read())ids.Add(checked((uint)r.GetInt64(0)));return ids;
        }
        private static void Track(NpgsqlConnection c,NpgsqlTransaction t,GameState p,ConquerItem item){Q.Insert(c,t,p.Entity.UID,item);
            if(Q.Execute(c,t,"INSERT INTO revora_tomboy_materials(item_uid,uid,life,type) VALUES(@item,@uid,@life,@type)","item",(long)item.UID,"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"type",(int)item.ID)!=1)throw new InvalidOperationException("Tomboy material tracking failed.");
        }
        private static void Consume(NpgsqlConnection c,NpgsqlTransaction t,GameState p,uint uid,uint type){
            if(!Material(c,t,p,uid,type))throw new InvalidOperationException("Tomboy material ownership changed.");Q.Consume(c,t,p,uid,type);
            if(Q.Execute(c,t,"UPDATE revora_tomboy_materials SET state=1 WHERE item_uid=@item AND uid=@uid AND life=@life AND state=0","item",(long)uid,"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn)!=1)throw new InvalidOperationException("Tomboy consumption was not saved.");
        }
        private static void Save(NpgsqlConnection c,NpgsqlTransaction t,GameState p,int before,int after){
            if(Q.Execute(c,t,"UPDATE revora_tomboy SET stage=@after WHERE uid=@uid AND life=@life AND stage=@before","after",after,"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"before",before)!=1)throw new InvalidOperationException("Tomboy stage changed.");
        }
        // Chang acceptance, Lynn referral, ten Marbles, then the three distinct jades.
        internal static bool Advance(GameState p,int action,Func<bool> context){
            Ready(p,context);var removed=new List<uint>();ulong silver=p.Entity.Money;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);if(action<0 || action>3 || run.Stage!=action)return false;
                if(action==0){if(Q.Execute(c,t,"INSERT INTO revora_tomboy(uid,life,stage,soul) VALUES(@uid,@life,1,@soul)","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"soul",(long)QuestSoulReward.Create().ID)!=1)throw new InvalidOperationException("Tomboy acceptance failed.");}
                else{
                    if(action>=2){foreach(uint type in action==2?new uint[]{710581}:new uint[]{710582,710583,710584}){
                        int count=action==2?10:1;var ids=Materials(c,t,p,type).Where(id=>Material(c,t,p,id,type)).Take(count).ToArray();if(ids.Length!=count)return false;
                        foreach(uint id in ids){Consume(c,t,p,id,type);removed.Add(id);}
                    }}
                    SilverBalance.AssertSaved(c,t,p);silver=checked(silver+100);if(silver>Entity.MaximumSilver)return false;SilverBalance.Persist(c,t,p,silver);Save(c,t,p,action,action+1);
                }Ready(p,context);t.Commit();
            }}Q.Install(p,removed,new ConquerItem[0]);if(action>0)SilverBalance.ApplyCommitted(p,silver);return true;
        }
        internal static void Revived(Entity body){if(body!=null && !body.Dead)lock(body)deaths.Remove(body);}
        internal static bool Hunt(GameState p,Entity body,Func<bool> context){
            Ready(p,context);if(body==null)return false;
            lock(body){object used;if(body.MapID!=1000 || body.MonsterInfo==null || body.MonsterInfo.ID!=16 && body.MonsterInfo.ID!=75
                || !DailySoulBall.WildernessDeath(body,p.Entity) || !ReferenceEquals(body.Killer,p.Entity) || Kernel.GetDistance(p.Entity.X,p.Entity.Y,body.X,body.Y)>Constants.pScreenDistance || deaths.TryGetValue(body,out used))return false;
                deaths.Add(body,new object());ConquerItem added;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    Owner(c,t,p);var run=Read(c,t,p);if(run.Stage!=2 && run.Stage!=3 || p.Inventory.Count>=40)return false;
                    if(run.Stage==3 && body.MonsterInfo.ID!=75)return false;
                    if(Draw(100)>=50)return false;uint type=run.Stage==2?710581u:721951u+(uint)Draw(3);
                    if(Materials(c,t,p,type).Count>=(run.Stage==2?10:2))return false;
                    if(run.Stage==3 && new uint[]{710582,710583,710584}.All(id=>Materials(c,t,p,id).Count>0))return false;
                    added=Q.New(type,true);Track(c,t,p,added);
                    Ready(p,()=>context() && DailySoulBall.WildernessDeath(body,p.Entity) && ReferenceEquals(body.Killer,p.Entity));t.Commit();
                }}Q.Install(p,new uint[0],new[]{added});return true;
            }
        }
        internal static uint Recipe(uint clicked,out uint partner){partner=0;switch(clicked){case 721951:partner=721952;return 710582;case 721952:partner=721953;return 710583;case 721953:partner=721951;return 710584;default:return 0;}}
        // A committed failed fusion consumes the pair. Transaction failures consume nothing.
        internal static bool Fuse(GameState p,uint clicked,Func<bool> context){
            Ready(p,context);ConquerItem added=null;uint second;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);ConquerItem source;if(run.Stage!=3 || !p.Inventory.TryGetItem(clicked,out source))return false;
                uint partner;uint result=Recipe(source.ID,out partner);if(result==0 || !Material(c,t,p,clicked,source.ID) || Materials(c,t,p,result).Count>0)return false;
                second=Materials(c,t,p,partner).FirstOrDefault(id=>Material(c,t,p,id,partner));if(second==0)return false;
                bool success=Draw(100)<50;Consume(c,t,p,clicked,source.ID);Consume(c,t,p,second,partner);
                if(success){added=Q.New(result,true);Track(c,t,p,added);}
                if(Q.Execute(c,t,"INSERT INTO revora_tomboy_fusions(id,uid,life,clicked,partner,result_type,result_uid,success) VALUES(@id,@uid,@life,@clicked,@partner,@result,@item,@success)","id",Guid.NewGuid(),"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"clicked",(long)clicked,"partner",(long)second,"result",(int)result,"item",added==null?0L:(long)added.UID,"success",success)!=1)throw new InvalidOperationException("Tomboy fusion receipt missing.");
                Ready(p,context);t.Commit();
            }}Q.Install(p,new[]{clicked,second},added==null?new ConquerItem[0]:new[]{added});return true;
        }
        internal static bool Recover(GameState p,Func<bool> context){
            Ready(p,context);ConquerItem added=null;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);if(run.Stage!=2 && run.Stage!=3)return false;uint old=0,type=0;
                using(var q=Q.Command(c,t,"SELECT item_uid,type FROM revora_tomboy_materials WHERE uid=@uid AND life=@life AND state=0 AND NOT EXISTS(SELECT 1 FROM items WHERE uid=item_uid) ORDER BY item_uid LIMIT 1 FOR UPDATE","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn))using(var r=q.ExecuteReader())if(r.Read()){old=checked((uint)r.GetInt64(0));type=checked((uint)r.GetInt32(1));}
                if(old==0)return false;
                if(type>=710582 && type<=710584){
                    if(run.Stage!=3 || p.Inventory.Count>=40)return false;
                    using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_tomboy_fusions WHERE uid=@uid AND life=@life AND result_type=@type AND success)","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"type",(int)type))if(!Equals(q.ExecuteScalar(),true))return false;
                    added=Q.New(type,true);Track(c,t,p,added);
                }
                if(Q.Execute(c,t,"UPDATE revora_tomboy_materials SET state=2 WHERE item_uid=@old AND uid=@uid AND life=@life AND state=0","old",(long)old,"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn)!=1)throw new InvalidOperationException("Tomboy lost material changed.");
                if(Q.Execute(c,t,"INSERT INTO revora_tomboy_recoveries(old_uid,new_uid,uid,life,type) VALUES(@old,@new,@uid,@life,@type)","old",(long)old,"new",added==null?0L:(long)added.UID,"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"type",(int)type)!=1)throw new InvalidOperationException("Tomboy recovery receipt missing.");
                Ready(p,context);t.Commit();
            }}if(added!=null)Q.Install(p,new uint[0],new[]{added});return true;
        }
    }
}
