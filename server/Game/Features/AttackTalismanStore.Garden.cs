using System;
using System.Linq;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
using Scenes=Revora.Game.Features.AttackTalismanScenes;
namespace Revora.Game.Features
{
    internal static partial class AttackTalismanStore
    {
        internal const uint BoxStart=9910420,Loneliness=9910423;
        internal static Func<int,int> GardenRoll=max=>Kernel.Random.Next(max);
        internal static void GardenActors(Scenes.Scene scene){
            for(int i=0;i<3;i++)CityGuardSystem.SpawnTalker(BoxStart+(uint)i,scene.Map.ID,(ushort)(52+i*6),50,3550,"TreasureBox",0);
            CityGuardSystem.SpawnTalker(Loneliness,scene.Map.ID,60,64,3580,"MrLoneliness",0);
        }
        private static bool InGarden(GameState p){var s=Scenes.Find(p);return s!=null && s.Entered && !s.Completed && s.Map.BaseID==Scenes.GardenBaseID && Scenes.Registered(s,p) && Clock()<s.Until && p.Entity.MapID==s.Map.ID;}
        private static bool GardenObject(GameState p,uint uid,ushort x,ushort y){Revora.Interfaces.INpc npc;return InGarden(p) && p.ActiveNpc==uid && p.Map.Npcs.TryGetValue(uid,out npc) && npc.MapID==p.Entity.MapID && npc.X==x && npc.Y==y && Kernel.GetDistance(p.Entity.X,p.Entity.Y,x,y)<=3;}
        internal static bool OpenBox(GameState p,int box,Func<bool> context){
            Ready(p,context);if(box<0 || box>2 || !GardenObject(p,BoxStart+(uint)box,(ushort)(52+box*6),50))return false;ConquerItem fan=null;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);if(run.Stage!=10 || run.Pillars!=15 || run.BoxWinner<0 || run.BoxWinner>2 || run.PendingGhost>=0 || run.FanTaken || run.BookGiven || (run.Boxes&(1<<box))!=0)return false;
                if(box==run.BoxWinner){if(p.Inventory.Count>=40 || Materials(c,t,p,710221).Count!=0)return false;fan=Q.New(710221,true);Track(c,t,p,fan);}
                if(Q.Execute(c,t,"UPDATE revora_attack_talisman SET boxes=@boxes,pending_ghost=@ghost,fan_taken=@fan WHERE uid=@uid AND stage=10 AND boxes=@before AND pending_ghost=-1 AND NOT fan_taken",
                    "uid",(long)p.Entity.UID,"boxes",run.Boxes|(1<<box),"ghost",fan==null?box:-1,"fan",fan!=null,"before",run.Boxes)!=1)throw new InvalidOperationException("Fan box progress changed.");
                Ready(p,()=>context() && GardenObject(p,BoxStart+(uint)box,(ushort)(52+box*6),50));t.Commit();
            }}if(fan!=null)Q.Install(p,new uint[0],new[]{fan});return true;
        }
        private static int GhostKind(Run run){return run.PendingGhost<0 || run.PendingGhost>2 || run.BoxWinner<0 || run.BoxWinner>2 || run.PendingGhost==run.BoxWinner?-1:(run.PendingGhost-run.BoxWinner+3)%3==1?5:6;}
        internal static int GardenKind(GameState p){if(!Eligible(p) || !InGarden(p))return -1;lock(p.ItemSyncRoot)using(var c=DataHolder.MySqlConnection){c.Open();var run=Read(c,null,p);return run.Stage==10 && run.Pillars==15 && !run.FanTaken && !run.BookGiven?GhostKind(run):-1;}}
        internal static bool GhostKill(GameState p,Entity body,Guid attempt,int kind,Func<bool> context){
            Ready(p,context);if(!InGarden(p) || (kind!=5 && kind!=6) || !AttackTalismanEncounters.Owns(p,body,attempt,kind) || !body.Dead || !ReferenceEquals(body.Killer,p.Entity))return false;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);if(run.Stage!=10 || run.Pillars!=15 || run.FanTaken || run.BookGiven || GhostKind(run)!=kind)return false;
                if(Q.Execute(c,t,"INSERT INTO revora_fan_ghost_kills(uid,box,attempt,body) VALUES(@uid,@box,@attempt,@body) ON CONFLICT DO NOTHING","uid",(long)p.Entity.UID,"box",run.PendingGhost,"attempt",attempt,"body",(long)body.UID)!=1)return false;
                if(Q.Execute(c,t,"UPDATE revora_attack_talisman SET pending_ghost=-1 WHERE uid=@uid AND stage=10 AND pending_ghost=@box","uid",(long)p.Entity.UID,"box",run.PendingGhost)!=1)throw new InvalidOperationException("Fan ghost progress changed.");
                Ready(p,()=>context() && InGarden(p) && AttackTalismanEncounters.Owns(p,body,attempt,kind));t.Commit();return true;
            }}
        }
        internal static bool ExchangeBook(GameState p,Func<bool> context){
            Ready(p,context);if(!GardenObject(p,Loneliness,60,64))return false;uint fan;ConquerItem book;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);if(run.Stage!=10 || !run.FanTaken || run.BookGiven || run.PendingGhost!=-1)return false;
                fan=Materials(c,t,p,710221).FirstOrDefault(id=>Material(c,t,p,id,710221));if(fan==0)return false;
                Consume(c,t,p,fan,710221);book=Q.New(710222,true);Track(c,t,p,book);
                if(Q.Execute(c,t,"UPDATE revora_attack_talisman SET book_given=true WHERE uid=@uid AND stage=10 AND fan_taken AND NOT book_given","uid",(long)p.Entity.UID)!=1)throw new InvalidOperationException("Fan book exchange changed.");
                Ready(p,()=>context() && GardenObject(p,Loneliness,60,64));t.Commit();
            }}Q.Install(p,new[]{fan},new[]{book});Scenes.Find(p).Completed=true;return true;
        }
        internal static bool Claim(GameState p,Func<bool> context){
            Ready(p,context);if(!Near(p,1015,685,599))return false;uint book;ConquerItem[] rewards;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);if(run.Stage!=10 || !run.BookGiven || p.Inventory.Count>39)return false;
                book=Materials(c,t,p,710222).FirstOrDefault(id=>Material(c,t,p,id,710222));if(book==0)return false;
                ConquerItemBaseInformation soul;if(!ConquerItemInformation.BaseInformations.TryGetValue(run.Soul,out soul) || run.Soul<800000 || run.Soul>=900000 || soul.PurificationLevel!=7)throw new InvalidOperationException("Fan soul reward changed.");
                Consume(c,t,p,book,710222);rewards=new[]{Q.New(201009,true),Q.New(run.Soul,true)};foreach(var item in rewards)Q.Insert(c,t,p.Entity.UID,item);
                if(Q.Execute(c,t,"UPDATE revora_attack_talisman SET stage=11,rewards=@rewards WHERE uid=@uid AND stage=10 AND book_given","uid",(long)p.Entity.UID,"rewards",rewards.Select(i=>(long)i.UID).ToArray())!=1)throw new InvalidOperationException("Fan final claim changed.");
                Ready(p,()=>context() && Near(p,1015,685,599));ProgressionEvidence.RecordEquipmentQuestAggregate(c,t,p.Entity.UID);t.Commit();
            }}Q.Install(p,new[]{book},rewards);return true;
        }
    }
}
