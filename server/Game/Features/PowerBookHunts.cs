using System;
using Npgsql;
using System.Runtime.CompilerServices;
using Revora.Client;
using Revora.Database;
using Q=Revora.Game.Features.QuestInventory;
using W=Revora.Game.Features.PowerBookScenes;

namespace Revora.Game.Features
{
    internal static partial class PowerBookStore
    {
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        private sealed class Actor {internal Guid Id;internal GameState Owner;internal W.Scene Scene;internal int Stage,Kind;}
        private static readonly ConditionalWeakTable<Entity,Actor> actors=new ConditionalWeakTable<Entity,Actor>();
        internal static int FloorFor(int stage){return stage>=2 && stage<=4?0:stage>=5 && stage<=7?1:stage>=8 && stage<=10?2:stage==11?3:-1;}
        internal static bool AllowsFloor(int stage,int floor){return FloorFor(stage)==floor || stage==11 && floor==2;}
        private static bool InScene(GameState p,W.Scene scene){return W.Registered(scene,p) && ReferenceEquals(p.Map,scene.Map) && p.Entity.MapID==scene.Map.ID;}
        private static bool Body(W.Scene scene,Entity body)
        {
            Entity current;return body!=null && body.EntityFlag==EntityFlag.Monster && !body.Companion && body.MapID==scene.Map.ID
                && body.MonsterInfo!=null && ReferenceEquals(body.MonsterInfo.Owner,body) && !body.MonsterInfo.Boss && !body.MonsterInfo.Guard && !body.MonsterInfo.Reviver
                && scene.Map.Entities.TryGetValue(body.UID,out current) && ReferenceEquals(current,body);
        }
        internal static Guid RegisterLife(GameState p,W.Scene scene,Entity body)
        {
            Ready(p,()=>InScene(p,scene));
            if(!Body(scene,body) || body.Dead || body.MonsterInfo.ID<W.FamilyStart || body.MonsterInfo.ID>W.FamilyStart+6)
                throw new InvalidOperationException("Power Book actor is not an owned living trial monster.");
            Actor prior;if(actors.TryGetValue(body,out prior))throw new InvalidOperationException("Power Book monster life already registered.");
            var actor=new Actor{Id=Guid.NewGuid(),Owner=p,Scene=scene,Kind=(int)(body.MonsterInfo.ID-W.FamilyStart)};
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);if(!AllowsFloor(run.Stage,scene.Floor))throw new InvalidOperationException("Power Book floor does not match saved progress.");
                actor.Stage=run.Stage;
                if(Q.Execute(c,t,"INSERT INTO revora_power_book_lives(id,uid,life,stage,map,body,kind) VALUES(@id,@uid,@life,@stage,@map,@body,@kind)",
                    "id",actor.Id,"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"stage",actor.Stage,"map",(int)scene.Map.ID,"body",(long)body.UID,"kind",actor.Kind)!=1)
                    throw new InvalidOperationException("Power Book monster life was not saved.");
                Ready(p,()=>InScene(p,scene) && Body(scene,body) && !body.Dead);t.Commit();
            }}
            actors.Add(body,actor);return actor.Id;
        }
        private static bool HuntStep(Run run,int kind,ushort x,ushort y,DateTime now)
        {
            int first=-1,second=-1,needA=0,needB=0,seconds=0;
            switch(run.Stage){
                case 2:first=0;needA=20;break;
                case 3:first=1;needA=10;bool near=false;for(int i=0;i<3;i++)if(Kernel.GetDistance(x,y,W.HerdPoints[i,0],W.HerdPoints[i,1])<=3)near=true;if(!near)return false;break;
                case 4:first=0;second=1;needA=needB=15;break;
                case 5:first=2;needA=2;seconds=10;break;
                case 7:first=2;second=3;needA=needB=10;break;
                case 8:first=4;needA=2;seconds=20;break;
                case 10:first=4;second=5;needA=needB=5;break;
                default:return false;
            }
            if(kind!=first && kind!=second)return false;
            if(run.LastKill.HasValue && now<run.LastKill.Value)throw new InvalidOperationException("Power Book kill clock moved backwards.");
            if(seconds!=0 && (!run.Window.HasValue || now>=run.Window.Value)){run.CountA=0;run.Window=now.AddSeconds(seconds);}
            if(kind==first && run.CountA<needA)run.CountA++;
            if(kind==second && run.CountB<needB)run.CountB++;
            run.LastKill=now;
            if(run.CountA>=needA && run.CountB>=needB){run.Stage++;run.CountA=run.CountB=0;run.Window=null;run.LastKill=null;}
            return true;
        }
        internal static bool AcceptFinalLife(NpgsqlConnection c,NpgsqlTransaction t,GameState p,Entity body) {
            Actor actor;
            if(body==null || !actors.TryGetValue(body,out actor) || !ReferenceEquals(actor.Owner,p) || actor.Kind!=6 || actor.Stage!=11)
                throw new InvalidOperationException("Power Book final life is not registered.");
            Func<bool> context=()=>InScene(p,actor.Scene) && actor.Scene.Floor==3 && Body(actor.Scene,body)
                && !body.Dead && body.MonsterInfo.ID==W.FamilyStart+6;
            Ready(p,context);Owner(c,t,p);Read(c,t,p);
            DateTime now=Clock();if(now.Kind!=DateTimeKind.Utc)throw new InvalidOperationException("Power Book requires UTC.");
            // Final Minotaurs create only a burnable corpse. They do not change
            // stage, silver or inventory; Burn owns those later operations.
            if(Q.Execute(c,t,@"UPDATE revora_power_book_lives SET consumed=true,dead_at=@now,dead_x=@x,dead_y=@y
WHERE id=@id AND uid=@uid AND life=@life AND stage=11 AND map=@map AND body=@body AND kind=6 AND NOT consumed",
                "id",actor.Id,"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"map",(int)actor.Scene.Map.ID,
                "body",(long)body.UID,"now",now,"x",(int)body.X,"y",(int)body.Y)!=1)
                throw new InvalidOperationException("Power Book final corpse was not saved.");
            Ready(p,context);return true;
        }
        internal static bool AcceptDeath(GameState p,Entity body)
        {
            Actor actor;if(body==null || !actors.TryGetValue(body,out actor) || !ReferenceEquals(actor.Owner,p))return false;
            Ready(p,()=>InScene(p,actor.Scene));
            if(!Body(actor.Scene,body) || !body.Dead || !ReferenceEquals(body.Killer,p.Entity)
                || body.MonsterInfo.ID!=W.FamilyStart+actor.Kind)return false;
            DateTime now=Clock();if(now.Kind!=DateTimeKind.Utc)throw new InvalidOperationException("Power Book requires UTC.");
            bool changed=false,paid=false;ulong silver=p.Entity.Money;Revora.Network.GamePackets.ConquerItem material=null;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);
                if(Q.Execute(c,t,@"UPDATE revora_power_book_lives SET consumed=true,dead_at=@now,dead_x=@x,dead_y=@y WHERE id=@id AND uid=@uid AND life=@life
AND stage=@stage AND map=@map AND body=@body AND kind=@kind AND NOT consumed","id",actor.Id,"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,
                    "stage",actor.Stage,"map",(int)actor.Scene.Map.ID,"body",(long)body.UID,"kind",actor.Kind,"now",now,"x",(int)body.X,"y",(int)body.Y)!=1)return false;
                int before=run.Stage;
                if(before==actor.Stage && AllowsFloor(before,actor.Scene.Floor)){
                    material=DropMaterial(c,t,p,run,actor.Kind,actor.Scene.Floor);
                    changed=HuntStep(run,actor.Kind,body.X,body.Y,now) || material!=null;
                }
                if(changed){
                    if(run.Stage!=before){
                        SilverBalance.AssertSaved(c,t,p);silver=checked(silver+100);if(silver>Entity.MaximumSilver)throw new InvalidOperationException("Power Book silver capacity exceeded.");
                        SilverBalance.Persist(c,t,p,silver);paid=true;
                    }
                    if(Q.Execute(c,t,@"UPDATE revora_power_book SET stage=@stage,count_a=@a,count_b=@b,kill_window=@window,last_kill=@last,material_uid=@material,material_type=@type
WHERE uid=@uid AND life=@life AND stage=@before","stage",run.Stage,"a",run.CountA,"b",run.CountB,"window",run.Window.HasValue?(object)run.Window.Value:DBNull.Value,
                        "material",(long)run.MaterialUid,"type",(long)run.MaterialType,"last",run.LastKill.HasValue?(object)run.LastKill.Value:DBNull.Value,"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"before",before)!=1)
                        throw new InvalidOperationException("Power Book hunt progress changed.");
                }
                Ready(p,()=>InScene(p,actor.Scene) && Body(actor.Scene,body) && body.Dead && ReferenceEquals(body.Killer,p.Entity));t.Commit();
            }}
            if(material!=null)Q.Install(p,new uint[0],new[]{material});if(paid){SilverBalance.ApplyCommitted(p,silver);PowerBookJournal.Refresh(p);}return changed;
        }
    }
}
