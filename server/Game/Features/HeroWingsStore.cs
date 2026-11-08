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
    internal static partial class HeroWingsStore
    {
        internal sealed class Run {internal int Stage,Site;internal DateTime? FirstKill;internal uint Soul;}
        internal static Func<int,int> Roll=max=>Kernel.Random.Next(max);
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        internal static readonly ushort[,] Sites={{450,720},{296,290},{255,228},{243,193},{229,142}};
        private static readonly ConditionalWeakTable<Entity,object> deaths=new ConditionalWeakTable<Entity,object>();
        internal static bool Eligible(GameState p){return Q.Player(p) && ReferenceEquals(p.Entity.Owner,p) && CharacterProgression.Managed(p)
            && p.Entity.Level>=90 && p.Entity.Reborn<=2 && p.Trade!=null && !p.Trade.InTrade && p.Booth==null && p.Entity.PokerTable==0;}
        private static void Ready(GameState p,Func<bool> context){if(!Eligible(p) || !Monitor.IsEntered(p.ItemSyncRoot) || context==null || !context())throw new InvalidOperationException("HeroWings context changed.");}
        private static int Draw(int max){int value=Roll(max);if(value<0 || value>=max)throw new InvalidOperationException("HeroWings roll outside range.");return value;}
        internal static void Initialize(){
            foreach(uint id in new uint[]{1088001,721906,721908,721909,721910,721911,721912,728247,723911})if(!ConquerItemInformation.BaseInformations.ContainsKey(id))throw new InvalidOperationException("HeroWings item catalog missing.");
            uint[] families={18,19,77,78};string[] names={"Birdman","HawKing","BirdmanL88","HawkL93"};byte[] levels={87,92,88,93};
            for(int i=0;i<families.Length;i++){MonsterInformation m;if(!MonsterInformation.MonsterInformations.TryGetValue(families[i],out m) || m.Name!=names[i] || m.Level!=levels[i] || m.Guard || m.Reviver || m.Boss!=(families[i]==19))throw new InvalidOperationException("Hero Wings family changed.");}
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_hero_wings(
uid bigint NOT NULL,life smallint NOT NULL CHECK(life BETWEEN 0 AND 2),stage smallint NOT NULL CHECK(stage BETWEEN 1 AND 12),
site smallint NOT NULL CHECK(site BETWEEN 0 AND 4),first_kill timestamptz,soul bigint NOT NULL,rewards bigint[] NOT NULL DEFAULT '{}',PRIMARY KEY(uid,life));
CREATE TABLE IF NOT EXISTS revora_hero_wings_materials(item_uid bigint PRIMARY KEY,uid bigint NOT NULL,life smallint NOT NULL,
type integer NOT NULL CHECK(type IN(721906,721908,721909,721910,721911,721912)),state smallint NOT NULL DEFAULT 0 CHECK(state BETWEEN 0 AND 2),
FOREIGN KEY(uid,life) REFERENCES revora_hero_wings(uid,life));
CREATE TABLE IF NOT EXISTS revora_hero_wings_recoveries(old_uid bigint NOT NULL,new_uid bigint NOT NULL,uid bigint NOT NULL,life smallint NOT NULL,type integer NOT NULL,PRIMARY KEY(uid,life,old_uid),
FOREIGN KEY(uid,life) REFERENCES revora_hero_wings(uid,life));");}
        }
        private static Run Read(NpgsqlConnection c,NpgsqlTransaction t,GameState p){
            using(var q=Q.Command(c,t,"SELECT stage,soul,site,first_kill FROM revora_hero_wings WHERE uid=@uid AND life=@life FOR UPDATE","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn))
            using(var r=q.ExecuteReader())return r.Read()?new Run{Stage=r.GetInt16(0),Soul=checked((uint)r.GetInt64(1)),Site=r.GetInt16(2),FirstKill=r.IsDBNull(3)?(DateTime?)null:r.GetDateTime(3).ToUniversalTime()}:new Run();
        }
        internal static Run Load(GameState p){if(!Q.Player(p,false))return null;lock(p.ItemSyncRoot)using(var c=DataHolder.MySqlConnection){c.Open();return Read(c,null,p);}}
        private static void Owner(Npgsql.NpgsqlConnection c,Npgsql.NpgsqlTransaction t,GameState p)
        {
            SkillTable.LockOwner(c,t,p);
            using(var q=Q.Command(c,t,"SELECT reborn,level FROM entities WHERE uid=@uid","uid",(long)p.Entity.UID))using(var r=q.ExecuteReader())
                if(!r.Read() || Convert.ToInt32(r[0])!=p.Entity.Reborn || Convert.ToInt32(r[1])!=p.Entity.Level)throw new InvalidOperationException("HeroWings saved owner changed.");
            var saved=new List<uint>();using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid FOR UPDATE","uid",(long)p.Entity.UID))
            using(var r=q.ExecuteReader())while(r.Read())saved.Add(checked((uint)r.GetInt64(0)));
            if(!saved.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException("HeroWings bag changed.");
        }

        private static bool Material(NpgsqlConnection c,NpgsqlTransaction t,GameState p,uint uid,uint type){ConquerItem item;
            if(uid==0 || !p.Inventory.TryGetItem(uid,out item) || item.Lock!=0 || item.Suspicious || item.InWardrobe || item.Days!=0 || item.TimeLeftInMinutes!=0 || !Q.Owns(c,t,p,uid,type))return false;
            using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_hero_wings_materials m JOIN items i ON i.uid=m.item_uid WHERE m.item_uid=@id AND m.uid=@uid AND m.life=@life AND m.type=@type AND m.state=0 AND i.locked=0 AND COALESCE(i.suspicious,0)=0 AND COALESCE(i.inwardrobe,0)=0 AND COALESCE(NULLIF(i.days,''),'0')='0')","id",(long)uid,"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"type",(int)type))return Equals(q.ExecuteScalar(),true);
        }
        private static List<uint> Materials(NpgsqlConnection c,NpgsqlTransaction t,GameState p,uint type){
            var ids=new List<uint>();using(var q=Q.Command(c,t,"SELECT item_uid FROM revora_hero_wings_materials WHERE uid=@uid AND life=@life AND type=@type AND state=0 ORDER BY item_uid FOR UPDATE","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"type",(int)type))using(var r=q.ExecuteReader())while(r.Read())ids.Add(checked((uint)r.GetInt64(0)));return ids;
        }
        private static void Track(NpgsqlConnection c,NpgsqlTransaction t,GameState p,ConquerItem item){Q.Insert(c,t,p.Entity.UID,item);
            if(Q.Execute(c,t,"INSERT INTO revora_hero_wings_materials(item_uid,uid,life,type) VALUES(@item,@uid,@life,@type)","item",(long)item.UID,"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"type",(int)item.ID)!=1)throw new InvalidOperationException("HeroWings material tracking failed.");
        }
        private static void Consume(NpgsqlConnection c,NpgsqlTransaction t,GameState p,uint uid,uint type){
            if(!Material(c,t,p,uid,type))throw new InvalidOperationException("HeroWings material ownership changed.");Q.Consume(c,t,p,uid,type);
            if(Q.Execute(c,t,"UPDATE revora_hero_wings_materials SET state=1 WHERE item_uid=@item AND uid=@uid AND life=@life AND state=0","item",(long)uid,"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn)!=1)throw new InvalidOperationException("HeroWings consumption was not saved.");
        }
        private static void Save(NpgsqlConnection c,NpgsqlTransaction t,GameState p,int before,int after){
            if(Q.Execute(c,t,"UPDATE revora_hero_wings SET stage=@after WHERE uid=@uid AND life=@life AND stage=@before","after",after,"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"before",before)!=1)throw new InvalidOperationException("HeroWings stage changed.");
        }
        private static uint Owned(NpgsqlConnection c,NpgsqlTransaction t,GameState p,uint type){return Materials(c,t,p,type).FirstOrDefault(id=>Material(c,t,p,id,type));}
        internal static bool Advance(GameState p,int action,Func<bool> context){
            Ready(p,context);if(action<0 || action>9 || action==4)return false;var removed=new List<uint>();var added=new List<ConquerItem>();ulong silver=p.Entity.Money;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);if(run.Stage!=action)return false;
                if(action==0){if(Q.Execute(c,t,"INSERT INTO revora_hero_wings(uid,life,stage,soul,site) VALUES(@uid,@life,1,@soul,@site)","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"soul",(long)QuestSoulReward.Create().ID,"site",Draw(5))!=1)throw new InvalidOperationException("Hero Wings acceptance failed.");}
                else{
                    uint take=action==5?721906u:action==6?721908u:action==7?721910u:action==8?721911u:0;
                    if(take!=0){int count=action==5?10:action==7?5:1;var ids=Materials(c,t,p,take).Where(id=>Material(c,t,p,id,take)).Take(count).ToArray();if(ids.Length!=count)return false;foreach(uint id in ids){Consume(c,t,p,id,take);removed.Add(id);}}
                    if(action==6){uint map=Owned(c,t,p,721912);if(map==0)return false;Consume(c,t,p,map,721912);removed.Add(map);}
                    uint give=action==5?721912u:action==7?721911u:0;if(give!=0){if(p.Inventory.Count-removed.Count>=40)return false;var item=Q.New(give,true);Track(c,t,p,item);added.Add(item);}
                    if(action>=2){SilverBalance.AssertSaved(c,t,p);silver=checked(silver+100);if(silver>Entity.MaximumSilver)return false;SilverBalance.Persist(c,t,p,silver);}
                    Save(c,t,p,action,action+1);
                }Ready(p,context);t.Commit();
            }}Q.Install(p,removed,added);if(action>=2)SilverBalance.ApplyCommitted(p,silver);return true;
        }
        internal static bool PayMeteor(GameState p,uint uid,Func<bool> context){
            Ready(p,context);ConquerItem meteor;ushort before;ulong silver;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);if(Read(c,t,p).Stage!=4)return false;
                if(!p.Inventory.TryGetItem(uid,out meteor) || meteor.ID!=1088001 || meteor.Position!=0 || meteor.Warehouse!=0 || meteor.Lock!=0 || meteor.Suspicious || meteor.InWardrobe || meteor.Days!=0 || meteor.TimeLeftInMinutes!=0)return false;
                before=meteor.StackSize;string debit=before<=1?"DELETE FROM items":"UPDATE items SET stacksize=stacksize-1";
                if(Q.Execute(c,t,debit+" WHERE uid=@item AND entityid=@uid AND id=1088001 AND position=0 AND warehouse=0 AND stacksize=@before AND bound=@bound AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')='0'","item",(long)uid,"uid",(long)p.Entity.UID,"before",(int)before,"bound",meteor.Bound?1:0)!=1)throw new InvalidOperationException("Hero Wings Meteor changed.");
                SilverBalance.AssertSaved(c,t,p);silver=checked(p.Entity.Money+100);if(silver>Entity.MaximumSilver)return false;SilverBalance.Persist(c,t,p,silver);Save(c,t,p,4,5);Ready(p,context);t.Commit();
            }}Q.Install(p,before<=1?new[]{uid}:new uint[0],new ConquerItem[0]);if(before>1){meteor.StackSize=(ushort)(before-1);meteor.Mode=Enums.ItemMode.Update;meteor.Send(p);meteor.Mode=Enums.ItemMode.Default;}SilverBalance.ApplyCommitted(p,silver);return true;
        }
        internal static bool Gather(GameState p,uint uid,Func<bool> context){
            Ready(p,context);if(p.Entity.MapID!=1015)return false;ConquerItem item;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);if(run.Stage!=6 || !Material(c,t,p,uid,721909) || Owned(c,t,p,721912)==0 || Materials(c,t,p,721908).Count!=0 || Kernel.GetDistance(p.Entity.X,p.Entity.Y,Sites[run.Site,0],Sites[run.Site,1])>2)return false;
                Consume(c,t,p,uid,721909);item=Q.New(721908,true);Track(c,t,p,item);Ready(p,context);t.Commit();
            }}Q.Install(p,new[]{uid},new[]{item});return true;
        }
        private static bool ValidDeath(Entity body,Entity killer){
            if(body==null || body.MonsterInfo==null || body.MonsterInfo.ID!=19)return DailySoulBall.WildernessDeath(body,killer);
            var p=killer==null?null:killer.Owner;MonsterInformation template;Entity current;Map world;MapsTable.MapInformation info;
            return Q.Player(p) && ReferenceEquals(p.Entity,killer) && body.Dead && body.EntityFlag==EntityFlag.Monster && !body.Companion
                && ReferenceEquals(body.MonsterInfo.Owner,body) && !body.MonsterInfo.Guard && !body.MonsterInfo.Reviver && body.MonsterInfo.Boss
                && !CityGuardSystem.IsCityGuard(body) && !LivingWorldSystem.IsAdventurer(body) && body.MapID==1015 && killer.MapID==1015
                && p.Map!=null && p.Map.BaseID==1015 && Kernel.Maps.TryGetValue(1015,out world) && ReferenceEquals(world,p.Map)
                && MapsTable.MapInformations.TryGetValue(1015,out info) && info.ID==1015 && info.BaseID==1015 && info.Owner==0
                && world.Entities.TryGetValue(body.UID,out current) && ReferenceEquals(current,body)
                && MonsterInformation.MonsterInformations.TryGetValue(19,out template) && template.Name=="HawKing" && template.Level==92
                && template.Boss && !template.Guard && !template.Reviver && body.MonsterInfo.Name==template.Name && body.MonsterInfo.Level==template.Level;
        }
        internal static void Revived(Entity body){if(body!=null && !body.Dead)lock(body)deaths.Remove(body);}
        internal static bool Hunt(GameState p,Entity body,Func<bool> context){
            Ready(p,context);if(body==null)return false;
            lock(body){object used;if(body.MapID!=1015 || body.MonsterInfo==null || !new uint[]{18,19,77,78}.Contains(body.MonsterInfo.ID) || !ValidDeath(body,p.Entity) || !ReferenceEquals(body.Killer,p.Entity) || Kernel.GetDistance(p.Entity.X,p.Entity.Y,body.X,body.Y)>Constants.pScreenDistance || deaths.TryGetValue(body,out used))return false;
                deaths.Add(body,new object());ConquerItem added=null;DateTime now=Clock();if(now.Kind!=DateTimeKind.Utc)throw new InvalidOperationException("Hero Wings clock requires UTC.");now=new DateTime(now.Ticks-now.Ticks%10,DateTimeKind.Utc); // Match PostgreSQL microsecond timestamp precision.
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    Owner(c,t,p);var run=Read(c,t,p);uint family=body.MonsterInfo.ID,type;int cap;
                    if(run.Stage==5 && (family==18 || family==77)){type=721906;cap=10;}
                    else if(run.Stage==6 && family==19){type=721909;cap=1;if(Materials(c,t,p,721908).Count!=0)return false;}
                    else if(run.Stage==7 && family==78){type=721910;cap=5;}
                    else return false;
                    if(p.Inventory.Count>=40 || Materials(c,t,p,type).Count>=cap)return false;
                    if(type!=721910){if(Draw(100)>=50)return false;}
                    else{
                        bool pair=run.FirstKill.HasValue && now>=run.FirstKill.Value && now-run.FirstKill.Value<=TimeSpan.FromMinutes(1);
                        string sql=pair?"UPDATE revora_hero_wings SET first_kill=NULL WHERE uid=@uid AND life=@life AND stage=7":"UPDATE revora_hero_wings SET first_kill=@now WHERE uid=@uid AND life=@life AND stage=7";
                        if(Q.Execute(c,t,sql,"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"now",now)!=1)throw new InvalidOperationException("Hero Wings timer changed.");
                        if(!pair){Ready(p,context);t.Commit();return true;}
                    }
                    added=Q.New(type,true);Track(c,t,p,added);Ready(p,()=>context() && ValidDeath(body,p.Entity) && ReferenceEquals(body.Killer,p.Entity));t.Commit();
                }}Q.Install(p,new uint[0],new[]{added});return true;
            }
        }
        internal static bool Recover(GameState p,Func<bool> context){
            Ready(p,context);ConquerItem added=null;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);if(run.Stage<5 || run.Stage>8)return false;uint old=0,type=0;
                using(var q=Q.Command(c,t,"SELECT item_uid,type FROM revora_hero_wings_materials WHERE uid=@uid AND life=@life AND state=0 AND NOT EXISTS(SELECT 1 FROM items WHERE uid=item_uid) ORDER BY item_uid LIMIT 1 FOR UPDATE","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn))using(var r=q.ExecuteReader())if(r.Read()){old=checked((uint)r.GetInt64(0));type=checked((uint)r.GetInt32(1));}if(old==0)return false;
                bool raw=type==721906 || type==721909 || type==721910;if(!raw){if(p.Inventory.Count>=40)return false;added=Q.New(type,true);Track(c,t,p,added);}
                if(Q.Execute(c,t,"UPDATE revora_hero_wings_materials SET state=2 WHERE item_uid=@old AND uid=@uid AND life=@life AND state=0","old",(long)old,"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn)!=1)throw new InvalidOperationException("Hero Wings recovery changed.");
                if(Q.Execute(c,t,"INSERT INTO revora_hero_wings_recoveries(old_uid,new_uid,uid,life,type) VALUES(@old,@new,@uid,@life,@type)","old",(long)old,"new",added==null?0L:(long)added.UID,"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"type",(int)type)!=1)throw new InvalidOperationException("Hero Wings recovery receipt missing.");Ready(p,context);t.Commit();
            }}if(added!=null)Q.Install(p,new uint[0],new[]{added});return true;
        }
    }
}
