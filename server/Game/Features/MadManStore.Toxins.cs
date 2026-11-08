using System;
using System.Linq;
using System.Collections.Generic;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
using Scenes=Revora.Game.Features.MadManScenes;
namespace Revora.Game.Features
{
    internal static partial class MadManStore
    {
        internal enum Mixture {None=0,Selected=1,Poison=2,Heal=3,Strong=4,Float=5,Zombie=6,KunLun=7}
        internal sealed class Toxin {internal uint Type,Item;internal int State;}
        internal sealed class MixResult {internal Mixture Outcome;internal Guid Receipt;}
        internal sealed class Trial {internal Guid ID;internal ushort Map;internal DateTime Started,Until;internal int State,Mask;}
        private static DateTime Now(){var now=Scenes.Clock();if(now.Kind!=DateTimeKind.Utc)throw new InvalidOperationException("Mad Man requires UTC.");return now;}
        internal static Mixture Recipe(uint first,uint second){
            if(first==729981 && second==729980)return Mixture.KunLun;
            if(first==729981 && second==729979)return Mixture.Poison;
            if(first==729979 && second==729981)return Mixture.Heal;
            if(first==729979 && second==729980)return Mixture.Strong;
            if(first==729980 && second==729981)return Mixture.Float;
            if(first==729980 && second==729979)return Mixture.Zombie;
            return Mixture.None;
        }
        private static List<Toxin> Toxins(NpgsqlConnection c,NpgsqlTransaction t,GameState p){
            var list=new List<Toxin>();using(var q=Q.Command(c,t,"SELECT type,item_uid,state FROM revora_mad_toxins WHERE uid=@uid AND life=@life ORDER BY type FOR UPDATE","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn))using(var r=q.ExecuteReader())while(r.Read())list.Add(new Toxin{Type=checked((uint)r.GetInt32(0)),Item=checked((uint)r.GetInt64(1)),State=r.GetInt16(2)});return list;
        }
        private static bool OwnedToxin(NpgsqlConnection c,NpgsqlTransaction t,GameState p,Toxin toxin){return toxin!=null && Material(c,t,p,new Run{Item=toxin.Item,Type=toxin.Type},toxin.Type);}
        // Selection does not consume. A valid distinct second use consumes both exact items,
        // persists their ordered result, and reserves travel atomically only for Blue->Green.
        // Caller reserves the scene and supplies non-database location/session context.
        internal static MixResult UseToxin(GameState p,uint item,Scenes.Scene scene,Func<bool> context){
            Ready(p,context);uint[] removed=null;var result=new MixResult();var now=Now();
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);if(run.Stage!=5)return result;var toxins=Toxins(c,t,p);
                var chosen=toxins.SingleOrDefault(x=>x.Item==item && x.State<2);if(!OwnedToxin(c,t,p,chosen))return result;
                var first=toxins.SingleOrDefault(x=>x.State==1);
                if(first==null){
                    if(scene!=null || chosen.State!=0)return result;
                    if(Q.Execute(c,t,"UPDATE revora_mad_toxins SET state=1 WHERE uid=@uid AND life=@life AND item_uid=@item AND state=0","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"item",(long)item)!=1)throw new InvalidOperationException("Mad Man first toxin changed.");
                    result.Outcome=Mixture.Selected;
                }else{
                    if(first.Item==chosen.Item || !OwnedToxin(c,t,p,first))return result;
                    var outcome=Recipe(first.Type,chosen.Type);if(outcome==Mixture.None)return result;
                    if(outcome==Mixture.KunLun){
                        if(!Scenes.Registered(scene,p) || scene.Until<=now || scene.Until>now.AddMinutes(5))return result;
                        if(Q.Execute(c,t,"INSERT INTO revora_mad_trials(id,uid,life,map,started_at,until_at) VALUES(@id,@uid,@life,@map,@now,@until)","id",scene.Visit,"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"map",(int)scene.Map.ID,"now",now,"until",scene.Until)!=1)throw new InvalidOperationException("Mad Man trial was not reserved.");
                        run.Stage=6;Save(c,t,p,run,5);
                    }else if(scene!=null)return result;
                    Q.Consume(c,t,p,first.Item,first.Type);Q.Consume(c,t,p,chosen.Item,chosen.Type);removed=new[]{first.Item,chosen.Item};
                    if(Q.Execute(c,t,"UPDATE revora_mad_toxins SET state=2 WHERE uid=@uid AND life=@life AND item_uid=ANY(@items) AND state IN(0,1)","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"items",removed.Select(x=>(long)x).ToArray())!=2)throw new InvalidOperationException("Mad Man toxin consumption changed.");
                    result.Outcome=outcome;result.Receipt=Guid.NewGuid();
                    if(Q.Execute(c,t,"INSERT INTO revora_mad_mixes(id,uid,life,first_type,second_type,first_uid,second_uid,outcome,mixed_at) VALUES(@id,@uid,@life,@first,@second,@a,@b,@outcome,@now)","id",result.Receipt,"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"first",(int)first.Type,"second",(int)chosen.Type,"a",(long)first.Item,"b",(long)chosen.Item,"outcome",(int)outcome,"now",now)!=1)throw new InvalidOperationException("Mad Man mix receipt failed.");
                }
                Ready(p,()=>context() && (result.Outcome!=Mixture.KunLun || Scenes.Registered(scene,p)));t.Commit();
            }}if(removed!=null)Q.Install(p,removed,new ConquerItem[0]);return result;
        }
        internal static bool CancelSelection(GameState p,Func<bool> context){
            Ready(p,context);using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);if(Read(c,t,p).Stage!=5)return false;
                if(Q.Execute(c,t,"UPDATE revora_mad_toxins SET state=0 WHERE uid=@uid AND life=@life AND state=1","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn)!=1)return false;
                Ready(p,context);t.Commit();return true;
            }}
        }
        internal static uint SelectedToxin(GameState p){
            Ready(p,()=>true);using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,"SELECT type FROM revora_mad_toxins WHERE uid=@uid AND life=@life AND state=1","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn)){var found=q.ExecuteScalar();return found==null?0:checked((uint)Convert.ToInt32(found));}}
        }
        internal static Guid[] PendingEffects(GameState p){
            Ready(p,()=>true);var ids=new List<Guid>();using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,"SELECT id FROM revora_mad_mixes WHERE uid=@uid AND life=@life AND outcome BETWEEN 2 AND 6 AND NOT applied ORDER BY mixed_at,id","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn))using(var r=q.ExecuteReader())while(r.Read())ids.Add(r.GetGuid(0));}return ids.ToArray();
        }
        internal static Trial ActiveTrial(GameState p){
            if(!Q.Player(p,false))return null;lock(p.ItemSyncRoot)using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,"SELECT id,map,started_at,until_at,state,mask FROM revora_mad_trials WHERE uid=@uid AND life=@life AND state=0","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn))using(var r=q.ExecuteReader())return r.Read()?new Trial{ID=r.GetGuid(0),Map=checked((ushort)r.GetInt32(1)),Started=r.GetDateTime(2).ToUniversalTime(),Until=r.GetDateTime(3).ToUniversalTime(),State=r.GetInt16(4),Mask=r.GetInt16(5)}:null;}
        }
    }
}
