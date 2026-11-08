using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Npgsql;
using Revora.Client;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features
{
    internal static partial class MysticRingStore
    {
        private static readonly ConditionalWeakTable<Entity,object> deaths=new ConditionalWeakTable<Entity,object>();
        internal static void Revived(Entity body){if(body!=null && !body.Dead)lock(body)deaths.Remove(body);}
        internal static bool Hunt(GameState p,Entity body,Func<bool> context){
            Ready(p,context);if(body==null)return false;
            lock(body){object used;if(body.MapID!=1011 || body.MonsterInfo==null || body.MonsterInfo.ID!=8 && body.MonsterInfo.ID!=67
                || !DailySoulBall.WildernessDeath(body,p.Entity) || !ReferenceEquals(body.Killer,p.Entity) || Kernel.GetDistance(p.Entity.X,p.Entity.Y,body.X,body.Y)>Constants.pScreenDistance || deaths.TryGetValue(body,out used))return false;
                deaths.Add(body,new object());using(var c=Database.DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    Owner(c,t,p);var run=Read(c,t,p);if(run.Stage!=4 || run.Kills>=20)return false;
                    if(Q.Execute(c,t,"UPDATE revora_mystic_ring SET kills=kills+1 WHERE uid=@uid AND life=@life AND stage=4 AND kills=@old","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"old",run.Kills)!=1)throw new InvalidOperationException("Mystic hunt changed.");
                    Ready(p,()=>context() && DailySoulBall.WildernessDeath(body,p.Entity) && ReferenceEquals(body.Killer,p.Entity));t.Commit();return true;
                }}
            }
        }
        private static bool Material(NpgsqlConnection c,NpgsqlTransaction t,GameState p,Run run,uint type){ConquerItem item;
            if(run.Item==0 || run.Type!=type || !p.Inventory.TryGetItem(run.Item,out item) || item.Lock!=0 || item.Suspicious || item.InWardrobe || item.Days!=0 || item.TimeLeftInMinutes!=0 || !Q.Owns(c,t,p,run.Item,type))return false;
            using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM items WHERE uid=@id AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')='0')","id",(long)run.Item))return Equals(q.ExecuteScalar(),true);
        }
        private static void Save(NpgsqlConnection c,NpgsqlTransaction t,GameState p,Run run,int before){
            if(Q.Execute(c,t,"UPDATE revora_mystic_ring SET stage=@stage,item_uid=@item,item_type=@type WHERE uid=@uid AND life=@life AND stage=@before",
                "stage",run.Stage,"item",(long)run.Item,"type",(long)run.Type,"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"before",before)!=1)throw new InvalidOperationException("Mystic progress changed.");
        }
        // 4 Shen's hunt hand-in; 5 Junior's scroll exchange; 6 return to Shen;
        // 7 Minstrel's ring exchange. Ring use requires the future cosmetic coordinator.
        internal static bool Progress(GameState p,int action,Func<bool> context){
            Ready(p,context);uint removed=0;ConquerItem added=null;ulong silver=p.Entity.Money;bool paid=false;
            using(var c=Database.DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);if(run.Stage!=action || action<4 || action>7)return false;
                if(action==4){if(run.Kills!=20 || p.Inventory.Count>=40)return false;added=Q.New(721710,true);}
                else if(action==5 || action==7){uint type=action==5?721710u:721707u;if(!Material(c,t,p,run,type))return false;
                    Q.Consume(c,t,p,run.Item,type);removed=run.Item;added=Q.New(action==5?721707u:721708u,true);
                }else if(!Material(c,t,p,run,721707))return false;
                if(action==4 || action==6){SilverBalance.AssertSaved(c,t,p);silver=checked(silver+100);if(silver>Entity.MaximumSilver)return false;SilverBalance.Persist(c,t,p,silver);paid=true;}
                if(added!=null){Q.Insert(c,t,p.Entity.UID,added);run.Item=added.UID;run.Type=added.ID;}
                run.Stage++;Save(c,t,p,run,action);Ready(p,context);t.Commit();
            }}Q.Install(p,removed==0?new uint[0]:new[]{removed},added==null?new ConquerItem[0]:new[]{added});if(paid)SilverBalance.ApplyCommitted(p,silver);return true;
        }
        internal static bool Recover(GameState p,Func<bool> context){
            Ready(p,context);ConquerItem added;
            using(var c=Database.DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);uint type=run.Stage==5?721710u:run.Stage==6 || run.Stage==7?721707u:run.Stage==8?721708u:0;
                if(type==0 || run.Item==0 || run.Type!=type || p.Inventory.Count>=40)return false;uint old=run.Item;
                using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE uid=@uid FOR UPDATE","uid",(long)old))if(q.ExecuteScalar()!=null)return false;
                added=Q.New(type,true);Q.Insert(c,t,p.Entity.UID,added);
                if(Q.Execute(c,t,"INSERT INTO revora_mystic_recoveries(uid,life,old_uid,new_uid,type) VALUES(@uid,@life,@old,@new,@type)","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"old",(long)old,"new",(long)added.UID,"type",(long)type)!=1)throw new InvalidOperationException("Mystic recovery receipt failed.");
                run.Item=added.UID;Save(c,t,p,run,run.Stage);Ready(p,context);t.Commit();
            }}Q.Install(p,new uint[0],new[]{added});return true;
        }
    }
}
