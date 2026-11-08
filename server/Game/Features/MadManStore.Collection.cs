using System;
using System.Collections.Generic;
using System.Linq;
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
        private static Trial ReadTrial(NpgsqlConnection c,NpgsqlTransaction t,GameState p,Guid id){
            using(var q=Q.Command(c,t,"SELECT map,started_at,until_at,state,mask FROM revora_mad_trials WHERE id=@id AND uid=@uid AND life=@life FOR UPDATE","id",id,"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn))using(var r=q.ExecuteReader())return r.Read()?new Trial{ID=id,Map=checked((ushort)r.GetInt32(0)),Started=r.GetDateTime(1).ToUniversalTime(),Until=r.GetDateTime(2).ToUniversalTime(),State=r.GetInt16(3),Mask=r.GetInt16(4)}:null;
        }
        private static bool InTrial(GameState p,Scenes.Scene scene){return Scenes.Registered(scene,p) && scene.Entered && p.Entity.MapID==scene.Map.ID && ReferenceEquals(p.Map,scene.Map);}
        private static bool Open(Trial trial,Scenes.Scene scene){var now=Now();return trial!=null && trial.Map==scene.Map.ID && trial.ID==scene.Visit && trial.State==0 && now>=trial.Started && now<trial.Until;}
        private static bool AtStone(GameState p,Scenes.Scene scene,int colour){
            if(!InTrial(p,scene))return false;Revora.Interfaces.INpc stone;uint uid=Scenes.StoneStart+(uint)colour;
            return p.ActiveNpc==uid && scene.Map.Npcs.TryGetValue(uid,out stone) && ReferenceEquals(stone,scene.Stones[colour]) && Kernel.GetDistance(p.Entity.X,p.Entity.Y,stone.X,stone.Y)<=3;
        }
        internal static bool Collect(GameState p,Scenes.Scene scene,uint stone,Func<bool> context){
            Ready(p,context);if(stone<Scenes.StoneStart || stone>=Scenes.StoneStart+7)return false;int colour=(int)(stone-Scenes.StoneStart);
            if(!AtStone(p,scene,colour))return false;ConquerItem powder;uint type=729983+(uint)colour;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);var trial=ReadTrial(c,t,p,scene.Visit);if(run.Stage!=6 || !Open(trial,scene) || p.Inventory.Count>=40)return false;
                uint old=0;using(var q=Q.Command(c,t,"SELECT item_uid FROM revora_mad_powders WHERE attempt=@attempt AND colour=@colour FOR UPDATE","attempt",scene.Visit,"colour",colour)){var found=q.ExecuteScalar();if(found!=null)old=checked((uint)(long)found);}
                if(old!=0){
                    using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE uid=@uid FOR UPDATE","uid",(long)old))if(q.ExecuteScalar()!=null)return false;
                    if((trial.Mask&(1<<colour))==0)throw new InvalidOperationException("Mad powder receipt disagrees with collected colours.");
                }else if((trial.Mask&(1<<colour))!=0)throw new InvalidOperationException("Mad collected colour lacks receipt.");
                powder=Q.New(type,true);Q.Insert(c,t,p.Entity.UID,powder);
                if(old==0){
                    if(Q.Execute(c,t,"INSERT INTO revora_mad_powders(attempt,colour,item_uid) VALUES(@attempt,@colour,@item)","attempt",scene.Visit,"colour",colour,"item",(long)powder.UID)!=1)throw new InvalidOperationException("Mad powder receipt was not inserted.");
                    if(Q.Execute(c,t,"UPDATE revora_mad_trials SET mask=@after WHERE id=@id AND mask=@before AND state=0","after",trial.Mask|(1<<colour),"before",trial.Mask,"id",scene.Visit)!=1)throw new InvalidOperationException("Mad colour counter changed.");
                }else{
                    if(Q.Execute(c,t,"UPDATE revora_mad_powders SET item_uid=@new WHERE attempt=@attempt AND colour=@colour AND item_uid=@old","new",(long)powder.UID,"attempt",scene.Visit,"colour",colour,"old",(long)old)!=1)throw new InvalidOperationException("Mad powder replacement changed.");
                    if(Q.Execute(c,t,"INSERT INTO revora_mad_recoveries(uid,life,old_uid,new_uid,type) VALUES(@uid,@life,@old,@new,@type)","uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"old",(long)old,"new",(long)powder.UID,"type",(long)type)!=1)throw new InvalidOperationException("Mad powder replacement receipt was not inserted.");
                }
                Ready(p,()=>context() && AtStone(p,scene,colour) && Open(trial,scene));t.Commit();
            }}Q.Install(p,new uint[0],new[]{powder});return true;
        }
        internal static bool Combine(GameState p,Scenes.Scene scene,uint selected,Func<bool> context){
            Ready(p,context);if(!InTrial(p,scene))return false;var parts=new List<Run>();ConquerItem rainbow;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);var trial=ReadTrial(c,t,p,scene.Visit);if(run.Stage!=6 || !Open(trial,scene) || trial.Mask!=127)return false;
                using(var q=Q.Command(c,t,"SELECT colour,item_uid FROM revora_mad_powders WHERE attempt=@attempt ORDER BY colour FOR UPDATE","attempt",scene.Visit))using(var r=q.ExecuteReader())while(r.Read())parts.Add(new Run{Type=729983u+(uint)r.GetInt16(0),Item=checked((uint)r.GetInt64(1))});
                if(parts.Count!=7 || !parts.Any(x=>x.Item==selected) || parts.Any(x=>!Material(c,t,p,x,x.Type)))return false;
                foreach(var part in parts)Q.Consume(c,t,p,part.Item,part.Type);rainbow=Q.New(729982,true);Q.Insert(c,t,p.Entity.UID,rainbow);
                run.Stage=7;run.Item=rainbow.UID;run.Type=rainbow.ID;Save(c,t,p,run,6);
                if(Q.Execute(c,t,"UPDATE revora_mad_trials SET state=1 WHERE id=@id AND state=0 AND mask=127","id",scene.Visit)!=1)throw new InvalidOperationException("Mad powder completion changed.");
                Ready(p,()=>context() && InTrial(p,scene) && Open(trial,scene));t.Commit();
            }}scene.Completed=true;Q.Install(p,parts.Select(x=>x.Item),new[]{rainbow});return true;
        }
    }
}
