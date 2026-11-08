using System;
using System.Linq;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features
{
    internal static partial class DefenseTalismanStore
    {
        internal static readonly uint[] GeneralWeapons={721784,721788,721789,721790};
        internal static readonly ushort[,] GeneralPositions={{96,120},{70,60},{113,122},{62,81}};
        internal static int GeneralKind(GameState p){
            var scene=DefenseTalismanScenes.Find(p);if(!Eligible(p) || scene==null || !scene.Entered || scene.Completed || scene.Map.BaseID<1785 || scene.Map.BaseID>1786
                || p.Entity.MapID!=scene.Map.ID || !DefenseTalismanScenes.Registered(scene,p) || Clock()>=scene.Until)return -1;
            lock(p.ItemSyncRoot)using(var c=DataHolder.MySqlConnection){c.Open();var run=Read(c,null,p);
                if(run.Stage!=8 || run.DungeonFloor!=scene.Map.BaseID-1784 || !Materials(c,null,p,721786).Any(id=>Material(c,null,p,id,721786)))return -1;
                int start=(run.DungeonFloor-1)*2;
                for(int slot=start;slot<start+2;slot++)using(var q=Q.Command(c,null,"SELECT EXISTS(SELECT 1 FROM revora_tower_general_kills WHERE uid=@uid AND slot=@slot)","uid",(long)p.Entity.UID,"slot",slot))
                    if(!Equals(q.ExecuteScalar(),true))return 6+slot;
                return -1;
            }
        }
        internal static bool GeneralKill(GameState p,Entity body,Guid attempt,int kind,Func<bool> context){
            Ready(p,context);int slot=kind-6;if(slot<0 || slot>=4 || !DefenseTalismanEncounters.Owns(p,body,attempt,kind) || !body.Dead || !ReferenceEquals(body.Killer,p.Entity))return false;
            var scene=DefenseTalismanScenes.Find(p);if(scene==null || scene.Map.BaseID!=1785+slot/2)return false;ConquerItem weapon;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);uint type=GeneralWeapons[slot];
                if(run.Stage!=8 || run.DungeonFloor!=1+slot/2 || p.Inventory.Count>=40 || Materials(c,t,p,type).Count!=0 || !Materials(c,t,p,721786).Any(id=>Material(c,t,p,id,721786)))return false;
                if(Q.Execute(c,t,"INSERT INTO revora_tower_general_kills(uid,slot,attempt,body) VALUES(@uid,@slot,@attempt,@body) ON CONFLICT DO NOTHING",
                    "uid",(long)p.Entity.UID,"slot",slot,"attempt",attempt,"body",(long)body.UID)!=1)return false;
                weapon=Q.New(type,true);Track(c,t,p,weapon);Ready(p,()=>context() && DefenseTalismanEncounters.Owns(p,body,attempt,kind));t.Commit();
            }}Q.Install(p,new uint[0],new[]{weapon});return true;
        }
        internal static bool AdvanceDungeon(GameState p,Func<bool> context){
            Ready(p,context);var scene=DefenseTalismanScenes.Find(p);
            if(scene==null || !scene.Entered || scene.Completed || !DefenseTalismanScenes.Registered(scene,p) || scene.Map.BaseID<1785 || scene.Map.BaseID>1786 || Clock()>=scene.Until)return false;
            int floor=scene.Map.BaseID-1784;ushort x=(ushort)(floor==1?70:62),y=(ushort)(floor==1?56:77);
            if(!Near(p,scene.Map.ID,x,y))return false;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);if(run.Stage!=8 || run.DungeonFloor!=floor || !Materials(c,t,p,721786).Any(id=>Material(c,t,p,id,721786)))return false;
                for(int slot=0;slot<floor*2;slot++)if(!Materials(c,t,p,GeneralWeapons[slot]).Any(id=>Material(c,t,p,id,GeneralWeapons[slot])))return false;
                if(Q.Execute(c,t,"UPDATE revora_defense_talisman SET dungeon_floor=@next WHERE uid=@uid AND stage=8 AND dungeon_floor=@before",
                    "next",floor+1,"uid",(long)p.Entity.UID,"before",floor)!=1)throw new InvalidOperationException("Tower dungeon floor changed.");
                Ready(p,()=>context() && DefenseTalismanScenes.Registered(scene,p) && !scene.Completed && Clock()<scene.Until && Near(p,scene.Map.ID,x,y));t.Commit();
            }}scene.Completed=true;return true;
        }
    }
}
