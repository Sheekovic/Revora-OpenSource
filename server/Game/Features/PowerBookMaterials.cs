using System;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
using W=Revora.Game.Features.PowerBookScenes;
namespace Revora.Game.Features
{
    internal static partial class PowerBookStore
    {
        internal static Func<int,int> Roll=max=>Kernel.Random.Next(max);
        private static bool Success(){int value=Roll(100);if(value<0 || value>=100)throw new InvalidOperationException("Power Book random range changed.");return value<50;}
        private static ConquerItem DropMaterial(NpgsqlConnection c,NpgsqlTransaction t,GameState p,Run run,int kind,int floor)
        {
            uint type=run.Stage==6 && kind==3?721926u:run.Stage==9 && kind==5?721928u:
                (run.Stage==10 || run.Stage==11) && kind==4 && floor==2?721931u:0;
            if(type==0 || run.MaterialUid!=0 || p.Inventory.Count>=40 || !Success())return null;
            var item=Q.New(type,true);Q.Insert(c,t,p.Entity.UID,item);run.MaterialUid=item.UID;run.MaterialType=type;return item;
        }
        private static bool Material(NpgsqlConnection c,NpgsqlTransaction t,GameState p,Run run,uint uid)
        {
            ConquerItem item;
            if(uid==0 || uid!=run.MaterialUid || !p.Inventory.TryGetItem(uid,out item) || !Usable(item)
                || item.StackSize!=1 || !Q.Owns(c,t,p,uid,run.MaterialType))return false;
            using(var q=Q.Command(c,t,@"SELECT EXISTS(SELECT 1 FROM items WHERE uid=@item AND entityid=@uid AND locked=0
AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')='0')","item",(long)uid,"uid",(long)p.Entity.UID))
                return Equals(q.ExecuteScalar(),true);
        }
        internal static bool UseMaterial(GameState p,W.Scene scene,uint uid)
        {
            Ready(p,()=>InScene(p,scene));ConquerItem output=null;bool paid=false,cooking=false;ulong silver=p.Entity.Money;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);int before=run.Stage;
                if(!AllowsFloor(before,scene.Floor) || !Material(c,t,p,run,uid))return false;
                if(before==6 && run.MaterialType==721926){if(Success())run.Stage=7;}
                else if(before==9 && run.MaterialType==721928){
                    cooking=true;if(Kernel.GetDistance(p.Entity.X,p.Entity.Y,W.FireX,W.FireY)>3)return false;
                    if(Success())output=Q.New(721929,true);
                }
                else if(before==9 && run.MaterialType==721929)run.Stage=10;
                else return false;
                Q.Consume(c,t,p,uid,run.MaterialType);run.MaterialUid=output==null?0:output.UID;run.MaterialType=output==null?0:output.ID;
                if(output!=null)Q.Insert(c,t,p.Entity.UID,output);
                if(run.Stage!=before){SilverBalance.AssertSaved(c,t,p);silver=checked(silver+100);if(silver>Entity.MaximumSilver)throw new InvalidOperationException("Power Book silver capacity exceeded.");SilverBalance.Persist(c,t,p,silver);paid=true;}
                if(Q.Execute(c,t,"UPDATE revora_power_book SET stage=@stage,material_uid=@item,material_type=@type WHERE uid=@uid AND life=@life AND stage=@before",
                    "stage",run.Stage,"item",(long)run.MaterialUid,"type",(long)run.MaterialType,"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"before",before)!=1)
                    throw new InvalidOperationException("Power Book material stage changed.");
                Ready(p,()=>InScene(p,scene) && (!cooking || Kernel.GetDistance(p.Entity.X,p.Entity.Y,W.FireX,W.FireY)<=3));t.Commit();
            }}
            Q.Install(p,new[]{uid},output==null?new ConquerItem[0]:new[]{output});if(paid)SilverBalance.ApplyCommitted(p,silver);return true;
        }
        internal static bool Burn(GameState p,Entity corpse,uint plume)
        {
            Actor actor;if(corpse==null || !actors.TryGetValue(corpse,out actor) || !ReferenceEquals(actor.Owner,p) || actor.Kind!=6 || actor.Stage!=11)return false;
            Func<bool> context=()=>InScene(p,actor.Scene) && actor.Scene.Floor==3 && Body(actor.Scene,corpse) && corpse.Dead
                && ReferenceEquals(corpse.Killer,p.Entity) && Kernel.GetDistance(p.Entity.X,p.Entity.Y,corpse.X,corpse.Y)<=3;
            Ready(p,context);DateTime now=Clock();if(now.Kind!=DateTimeKind.Utc)throw new InvalidOperationException("Power Book requires UTC.");
            bool finished=false;ulong silver=p.Entity.Money;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);if(run.Stage!=11 || run.MaterialType!=721931 || !Material(c,t,p,run,plume))return false;
                if(Q.Execute(c,t,@"UPDATE revora_power_book_lives SET burned=true WHERE id=@id AND uid=@uid AND life=@life AND stage=11 AND kind=6
AND consumed AND NOT burned AND dead_at<=@now AND dead_at+interval '3 seconds'>@now AND dead_x=@x AND dead_y=@y",
                    "id",actor.Id,"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn,"now",now,"x",(int)corpse.X,"y",(int)corpse.Y)!=1)return false;
                run.CountA++;finished=run.CountA==10;
                if(finished){
                    Q.Consume(c,t,p,plume,721931);run.MaterialUid=run.MaterialType=0;run.Stage=12;run.CountA=0;
                    SilverBalance.AssertSaved(c,t,p);silver=checked(silver+100);if(silver>Entity.MaximumSilver)throw new InvalidOperationException("Power Book silver capacity exceeded.");SilverBalance.Persist(c,t,p,silver);
                }
                if(Q.Execute(c,t,"UPDATE revora_power_book SET stage=@stage,count_a=@a,material_uid=@item,material_type=@type WHERE uid=@uid AND life=@life AND stage=11",
                    "stage",run.Stage,"a",run.CountA,"item",(long)run.MaterialUid,"type",(long)run.MaterialType,"uid",(long)p.Entity.UID,"life",(int)p.Entity.Reborn)!=1)
                    throw new InvalidOperationException("Power Book burning progress changed.");
                Ready(p,context);t.Commit();
            }}
            if(finished){Q.Install(p,new[]{plume},new ConquerItem[0]);SilverBalance.ApplyCommitted(p,silver);}return true;
        }
    }
}
