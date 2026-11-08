using System;
using Revora.Client;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
using S=Revora.Game.Features.FranticMonkeysStore;
using W=Revora.Game.Features.FranticMonkeysScenes;
namespace Revora.Game.Features
{
    // Item coordinator; global dispatch remains disabled until the full quest route exists.
    internal static class FranticMonkeysAdmission
    {
        internal const uint Doctor=4486;
        private static bool Near(GameState p){Revora.Interfaces.INpc npc;
            return S.Eligible(p) && p.Entity.MapID==1002 && p.Map.Npcs.TryGetValue(Doctor,out npc)
                && npc.X==440 && npc.Y==343 && npc.Mesh/10==688 && Kernel.GetDistance(p.Entity.X,p.Entity.Y,npc.X,npc.Y)<=6;
        }
        internal static bool Start(GameState p,ConquerItem item){
            if(item==null || item.ID!=721700 || !Near(p) || !SessionInstall.TryEnter(p.Entity.UID))return false;
            try{lock(p.ItemSyncRoot){
                ConquerItem owned;if(!Near(p) || !p.Inventory.TryGetItem(item.UID,out owned) || !ReferenceEquals(item,owned) || W.HasOwner(p.Entity.UID))return false;
                W.Scene scene=null;
                try{
                    scene=W.Create(p);
                    if(!S.BeginDisguisedTrial(p,item.UID,scene,()=>Near(p))){W.Release(scene);return false;}
                    W.Enter(scene,p,()=>S.Eligible(p) && W.Registered(scene,p) && QuestAppearance.Owns(p,scene.Appearance,QuestAppearance.MonkeyMesh));
                    return true;
                }catch(Exception ex){
                    try{if(scene!=null && !scene.Released){
                        if(scene.Appearance!=Guid.Empty)QuestAppearance.Clear(p,scene.Appearance,QuestAppearance.MonkeyMesh);
                        if(Q.Player(p,false))W.Exit(scene,p);
                        else if(p.Entity.MapID!=scene.Map.ID)W.Release(scene);
                    }}catch(Exception cleanup){Q.Fail(p,"frantic-admission-cleanup",cleanup);}
                    Q.Fail(p,"frantic-admission",ex);return false;
                }
            }}finally{SessionInstall.Exit(p.Entity.UID);}
        }
    }
}
