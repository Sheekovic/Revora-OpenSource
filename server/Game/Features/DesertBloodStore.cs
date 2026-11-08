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
    internal static partial class DesertBloodStore
    {
        internal sealed class Run {internal int Stage,Kills;internal bool SpellRead;internal uint Soul;}
        internal static Func<int,int> Roll=max=>Kernel.Random.Next(max);
        private static readonly ConditionalWeakTable<Entity,object> deaths=new ConditionalWeakTable<Entity,object>();
        internal static bool Eligible(GameState p){return Q.Player(p) && ReferenceEquals(p.Entity.Owner,p) && CharacterProgression.Managed(p)
            && p.Entity.Level>=85 && p.Entity.Reborn<=2 && p.Trade!=null && !p.Trade.InTrade && p.Booth==null && p.Entity.PokerTable==0;}
        private static void Ready(GameState p,Func<bool> context){if(!Eligible(p) || !Monitor.IsEntered(p.ItemSyncRoot) || context==null || !context())throw new InvalidOperationException("DesertBlood context changed.");}
        private static int Draw(int max){int value=Roll(max);if(value<0 || value>=max)throw new InvalidOperationException("DesertBlood roll outside range.");return value;}
        internal static void Initialize(){
            foreach(uint id in new uint[]{721841,721842,721843,721844,721845,721846,728242,723911})if(!ConquerItemInformation.BaseInformations.ContainsKey(id))throw new InvalidOperationException("DesertBlood item catalog missing.");
            foreach(uint id in new uint[]{17,76}){MonsterInformation m;if(!MonsterInformation.MonsterInformations.TryGetValue(id,out m) || m.Name!=(id==17?"BladeGhost":"BladeGhostL83") || m.Level!=(id==17?82:83) || m.Guard || m.Reviver || m.Boss)throw new InvalidOperationException("DesertBlood monster catalog changed.");}
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_desert_blood(
uid bigint NOT NULL,life smallint NOT NULL CHECK(life BETWEEN 0 AND 2),stage smallint NOT NULL CHECK(stage BETWEEN 1 AND 11),
kills smallint NOT NULL DEFAULT 0 CHECK(kills BETWEEN 0 AND 200),spell_read boolean NOT NULL DEFAULT false,soul bigint NOT NULL,rewards bigint[] NOT NULL DEFAULT '{}',PRIMARY KEY(uid,life));
CREATE TABLE IF NOT EXISTS revora_desert_blood_materials(item_uid bigint PRIMARY KEY,uid bigint NOT NULL,life smallint NOT NULL,
type integer NOT NULL CHECK(type IN(721841,721842,721843,721844,721845,721846)),state smallint NOT NULL DEFAULT 0 CHECK(state BETWEEN 0 AND 2),
FOREIGN KEY(uid,life) REFERENCES revora_desert_blood(uid,life));
CREATE TABLE IF NOT EXISTS revora_desert_blood_recoveries(old_uid bigint NOT NULL,new_uid bigint NOT NULL,uid bigint NOT NULL,life smallint NOT NULL,type integer NOT NULL,PRIMARY KEY(uid,life,old_uid),
FOREIGN KEY(uid,life) REFERENCES revora_desert_blood(uid,life));");}
        }
        private static Run Read(NpgsqlConnection c,NpgsqlTransaction t,GameState p){
            using(var q=Q.Command(c,t,"SELECT stage,soul,kills,spell_read FROM revora_desert_blood WHERE uid=@uid AND life=@life FOR UPDATE","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn))
            using(var r=q.ExecuteReader())return r.Read()?new Run{Stage=r.GetInt16(0),Soul=checked((uint)r.GetInt64(1)),Kills=r.GetInt16(2),SpellRead=r.GetBoolean(3)}:new Run();
        }
        internal static Run Load(GameState p){if(!Q.Player(p,false))return null;lock(p.ItemSyncRoot)using(var c=DataHolder.MySqlConnection){c.Open();return Read(c,null,p);}}
        private static void Owner(Npgsql.NpgsqlConnection c,Npgsql.NpgsqlTransaction t,GameState p)
        {
            SkillTable.LockOwner(c,t,p);
            using(var q=Q.Command(c,t,"SELECT reborn,level FROM entities WHERE uid=@uid","uid",(long)p.Entity.UID))using(var r=q.ExecuteReader())
                if(!r.Read() || Convert.ToInt32(r[0])!=p.Entity.Reborn || Convert.ToInt32(r[1])!=p.Entity.Level)throw new InvalidOperationException("DesertBlood saved owner changed.");
            var saved=new List<uint>();using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid FOR UPDATE","uid",(long)p.Entity.UID))
            using(var r=q.ExecuteReader())while(r.Read())saved.Add(checked((uint)r.GetInt64(0)));
            if(!saved.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException("DesertBlood bag changed.");
        }

        private static bool Material(NpgsqlConnection c,NpgsqlTransaction t,GameState p,uint uid,uint type){ConquerItem item;
            if(uid==0 || !p.Inventory.TryGetItem(uid,out item) || item.Lock!=0 || item.Suspicious || item.InWardrobe || item.Days!=0 || item.TimeLeftInMinutes!=0 || !Q.Owns(c,t,p,uid,type))return false;
            using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_desert_blood_materials m JOIN items i ON i.uid=m.item_uid WHERE m.item_uid=@id AND m.uid=@uid AND m.life=@life AND m.type=@type AND m.state=0 AND i.locked=0 AND COALESCE(i.suspicious,0)=0 AND COALESCE(i.inwardrobe,0)=0 AND COALESCE(NULLIF(i.days,''),'0')='0')","id",(long)uid,"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"type",(int)type))return Equals(q.ExecuteScalar(),true);
        }
        private static List<uint> Materials(NpgsqlConnection c,NpgsqlTransaction t,GameState p,uint type){
            var ids=new List<uint>();using(var q=Q.Command(c,t,"SELECT item_uid FROM revora_desert_blood_materials WHERE uid=@uid AND life=@life AND type=@type AND state=0 ORDER BY item_uid FOR UPDATE","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"type",(int)type))using(var r=q.ExecuteReader())while(r.Read())ids.Add(checked((uint)r.GetInt64(0)));return ids;
        }
        private static void Track(NpgsqlConnection c,NpgsqlTransaction t,GameState p,ConquerItem item){Q.Insert(c,t,p.Entity.UID,item);
            if(Q.Execute(c,t,"INSERT INTO revora_desert_blood_materials(item_uid,uid,life,type) VALUES(@item,@uid,@life,@type)","item",(long)item.UID,"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"type",(int)item.ID)!=1)throw new InvalidOperationException("DesertBlood material tracking failed.");
        }
        private static void Consume(NpgsqlConnection c,NpgsqlTransaction t,GameState p,uint uid,uint type){
            if(!Material(c,t,p,uid,type))throw new InvalidOperationException("DesertBlood material ownership changed.");Q.Consume(c,t,p,uid,type);
            if(Q.Execute(c,t,"UPDATE revora_desert_blood_materials SET state=1 WHERE item_uid=@item AND uid=@uid AND life=@life AND state=0","item",(long)uid,"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn)!=1)throw new InvalidOperationException("DesertBlood consumption was not saved.");
        }
        private static void Save(NpgsqlConnection c,NpgsqlTransaction t,GameState p,int before,int after){
            if(Q.Execute(c,t,"UPDATE revora_desert_blood SET stage=@after WHERE uid=@uid AND life=@life AND stage=@before","after",after,"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"before",before)!=1)throw new InvalidOperationException("DesertBlood stage changed.");
        }
        private static uint Owned(NpgsqlConnection c,NpgsqlTransaction t,GameState p,uint type){return Materials(c,t,p,type).FirstOrDefault(id=>Material(c,t,p,id,type));}
        // Context comes from the ordinary NPC coordinator or exact owned Crypt actor.
        internal static bool Advance(GameState p,int action,Func<bool> context){
            Ready(p,context);var removed=new List<uint>();var added=new List<ConquerItem>();ulong silver=p.Entity.Money;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);if(action<0 || action>7 || run.Stage!=action)return false;
                if(action==0){if(Q.Execute(c,t,"INSERT INTO revora_desert_blood(uid,life,stage,soul) VALUES(@uid,@life,1,@soul)","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"soul",(long)QuestSoulReward.Create().ID)!=1)throw new InvalidOperationException("Desert Blood acceptance failed.");}
                else{
                    if(action==2 && (!run.SpellRead || Owned(c,t,p,721843)==0))return false;
                    if(action==3 && run.Kills!=200)return false;
                    uint take=action==3?721845u:action==4?721846u:action==6?721841u:action==7?721842u:0;
                    if(take!=0){int count=action==4?5:1;var ids=Materials(c,t,p,take).Where(id=>Material(c,t,p,id,take)).Take(count).ToArray();if(ids.Length!=count)return false;
                        foreach(uint id in ids){Consume(c,t,p,id,take);removed.Add(id);}
                    }
                    if(action==7){uint spell=Owned(c,t,p,721843);if(spell==0)return false;Consume(c,t,p,spell,721843);removed.Add(spell);}
                    uint give=action==5?721841u:action==6?721842u:action==7?721844u:0;
                    if(give!=0){if(p.Inventory.Count-removed.Count>=40)return false;var item=Q.New(give,true);Track(c,t,p,item);added.Add(item);}
                    if(action<=6){SilverBalance.AssertSaved(c,t,p);silver=checked(silver+100);if(silver>Entity.MaximumSilver)return false;SilverBalance.Persist(c,t,p,silver);}
                    Save(c,t,p,action,action+1);
                }Ready(p,context);t.Commit();
            }}Q.Install(p,removed,added);if(action>0 && action<=6)SilverBalance.ApplyCommitted(p,silver);return true;
        }
        internal static bool ReadItem(GameState p,uint uid,Func<bool> context){
            Ready(p,context);using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);
                if(run.Stage==2 && !run.SpellRead && Material(c,t,p,uid,721843)){
                    if(Q.Execute(c,t,"UPDATE revora_desert_blood SET spell_read=true WHERE uid=@uid AND life=@life AND stage=2 AND NOT spell_read","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn)!=1)throw new InvalidOperationException("Desert Blood spell reading changed.");
                }else if(run.Stage==8 && Material(c,t,p,uid,721844))Save(c,t,p,8,9);else return false;
                Ready(p,context);t.Commit();return true;
            }}
        }
        internal static void Revived(Entity body){if(body!=null && !body.Dead)lock(body)deaths.Remove(body);}
        internal static bool Hunt(GameState p,Entity body,Func<bool> context){
            Ready(p,context);if(body==null)return false;
            lock(body){object used;if(body.MapID!=1000 || body.MonsterInfo==null || body.MonsterInfo.ID!=17 && body.MonsterInfo.ID!=76
                || !DailySoulBall.WildernessDeath(body,p.Entity) || !ReferenceEquals(body.Killer,p.Entity) || Kernel.GetDistance(p.Entity.X,p.Entity.Y,body.X,body.Y)>Constants.pScreenDistance || deaths.TryGetValue(body,out used))return false;
                deaths.Add(body,new object());ConquerItem added=null;bool changed=false;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    Owner(c,t,p);var run=Read(c,t,p);uint type=0;int cap=1;
                    if(run.Stage==3){
                        if(run.Kills<200){if(Q.Execute(c,t,"UPDATE revora_desert_blood SET kills=kills+1 WHERE uid=@uid AND life=@life AND stage=3 AND kills=@kills","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"kills",run.Kills)!=1)throw new InvalidOperationException("Desert Blood kill progress changed.");run.Kills++;changed=true;}
                        if(run.Kills==200)type=721845;
                    }else if((run.Stage==2 || run.Stage==4) && body.MonsterInfo.ID==76){if(Draw(100)>=50)return false;type=run.Stage==2?721843u:721846u;cap=run.Stage==4?5:1;}else return false;
                    if(type!=0 && p.Inventory.Count<40 && Materials(c,t,p,type).Count<cap){added=Q.New(type,true);Track(c,t,p,added);changed=true;}
                    if(!changed)return false;
                    Ready(p,()=>context() && DailySoulBall.WildernessDeath(body,p.Entity) && ReferenceEquals(body.Killer,p.Entity));t.Commit();
                }}if(added!=null)Q.Install(p,new uint[0],new[]{added});return true;
            }
        }
        internal static bool Recover(GameState p,Func<bool> context){
            Ready(p,context);ConquerItem added=null;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);if(run.Stage<2 || run.Stage>9)return false;uint old=0,type=0;
                using(var q=Q.Command(c,t,"SELECT item_uid,type FROM revora_desert_blood_materials WHERE uid=@uid AND life=@life AND state=0 AND NOT EXISTS(SELECT 1 FROM items WHERE uid=item_uid) ORDER BY item_uid LIMIT 1 FOR UPDATE","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn))using(var r=q.ExecuteReader())if(r.Read()){old=checked((uint)r.GetInt64(0));type=checked((uint)r.GetInt32(1));}
                if(old==0){if(run.Stage!=3 || run.Kills!=200 || Materials(c,t,p,721845).Count!=0)return false;type=721845;}
                bool raw=type==721846;
                if(raw && run.Stage!=4)return false;
                if(!raw){if(p.Inventory.Count>=40)return false;added=Q.New(type,true);Track(c,t,p,added);}
                if(old!=0 && Q.Execute(c,t,"UPDATE revora_desert_blood_materials SET state=2 WHERE item_uid=@old AND uid=@uid AND life=@life AND state=0","old",(long)old,"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn)!=1)throw new InvalidOperationException("Desert Blood lost material changed.");
                if(Q.Execute(c,t,"INSERT INTO revora_desert_blood_recoveries(old_uid,new_uid,uid,life,type) VALUES(@old,@new,@uid,@life,@type)","old",(long)old,"new",added==null?0L:(long)added.UID,"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"type",(int)type)!=1)throw new InvalidOperationException("Desert Blood recovery receipt missing.");
                Ready(p,context);t.Commit();
            }}if(added!=null)Q.Install(p,new uint[0],new[]{added});return true;
        }
    }
}
