using System;
using System.Linq;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features
{
    internal static partial class DefenseTalismanStore
    {
        internal static readonly uint[] Clinkers={721793,721794,721792,721791};
        private static bool FinalScene(GameState p){var s=DefenseTalismanScenes.Find(p);return s!=null && s.Entered && !s.Completed && s.Map.BaseID==1787
            && DefenseTalismanScenes.Registered(s,p) && p.Entity.MapID==s.Map.ID && Clock()<s.Until;}
        private static bool Cauldron(GameState p){return FinalScene(p) && Near(p,p.Entity.MapID,48,37);}
        private static bool FengReceipt(NpgsqlConnection c,NpgsqlTransaction t,GameState p){
            using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_tower_feng_kills WHERE uid=@uid)","uid",(long)p.Entity.UID))return Equals(q.ExecuteScalar(),true);
        }
        private static bool FinalReady(NpgsqlConnection c,NpgsqlTransaction t,GameState p){var r=Read(c,t,p);return r.Stage==8 && r.DungeonFloor==3
            && Materials(c,t,p,721786).Any(id=>Material(c,t,p,id,721786));}
        internal static int FengKind(GameState p){
            if(!Eligible(p) || !FinalScene(p))return -1;
            lock(p.ItemSyncRoot)using(var c=DataHolder.MySqlConnection){c.Open();if(!FinalReady(c,null,p) || FengReceipt(c,null,p))return -1;
                return GeneralWeapons.All(type=>Materials(c,null,p,type).Any(id=>Material(c,null,p,id,type)))?10:-1;
            }
        }
        internal static bool FengKill(GameState p,Entity body,Guid attempt,Func<bool> context){
            Ready(p,context);if(!FinalScene(p) || !DefenseTalismanEncounters.Owns(p,body,attempt,10) || !body.Dead || !ReferenceEquals(body.Killer,p.Entity))return false;
            ConquerItem[] pieces;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);if(!FinalReady(c,t,p) || p.Inventory.Count>37 || FengReceipt(c,t,p)
                    || !GeneralWeapons.All(type=>Materials(c,t,p,type).Any(id=>Material(c,t,p,id,type))) || Materials(c,t,p,721777).Count!=0)return false;
                if(Q.Execute(c,t,"INSERT INTO revora_tower_feng_kills(uid,attempt,body) VALUES(@uid,@attempt,@body) ON CONFLICT DO NOTHING",
                    "uid",(long)p.Entity.UID,"attempt",attempt,"body",(long)body.UID)!=1)return false;
                pieces=Enumerable.Range(0,3).Select(_=>Q.New(721777,true)).ToArray();foreach(var piece in pieces)Track(c,t,p,piece);
                Ready(p,()=>context() && FinalScene(p) && DefenseTalismanEncounters.Owns(p,body,attempt,10));t.Commit();
            }}Q.Install(p,new uint[0],pieces);return true;
        }
        internal static bool Smelt(GameState p,int slot,Func<bool> context){
            Ready(p,context);if(slot<0 || slot>=4 || !Cauldron(p))return false;uint weapon;ConquerItem clinker;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);if(!FinalReady(c,t,p) || !FengReceipt(c,t,p) || Materials(c,t,p,Clinkers[slot]).Count!=0)return false;
                weapon=Materials(c,t,p,GeneralWeapons[slot]).FirstOrDefault(id=>Material(c,t,p,id,GeneralWeapons[slot]));if(weapon==0)return false;
                Consume(c,t,p,weapon,GeneralWeapons[slot]);clinker=Q.New(Clinkers[slot],true);Track(c,t,p,clinker);
                Ready(p,()=>context() && Cauldron(p));t.Commit();
            }}Q.Install(p,new[]{weapon},new[]{clinker});return true;
        }
        internal static bool Reconstruct(GameState p,Func<bool> context){
            Ready(p,context);if(!Cauldron(p))return false;uint[] pieces,clinkers;uint key;ConquerItem tower;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);if(!FinalReady(c,t,p) || !FengReceipt(c,t,p))return false;
                pieces=Materials(c,t,p,721777).Where(id=>Material(c,t,p,id,721777)).Take(3).ToArray();
                clinkers=Clinkers.Select(type=>Materials(c,t,p,type).FirstOrDefault(id=>Material(c,t,p,id,type))).ToArray();
                key=Materials(c,t,p,721786).FirstOrDefault(id=>Material(c,t,p,id,721786));if(pieces.Length!=3 || clinkers.Any(id=>id==0) || key==0)return false;
                foreach(uint id in pieces)Consume(c,t,p,id,721777);for(int i=0;i<4;i++)Consume(c,t,p,clinkers[i],Clinkers[i]);Consume(c,t,p,key,721786);
                tower=Q.New(721785,true);Track(c,t,p,tower);
                if(Q.Execute(c,t,"UPDATE revora_defense_talisman SET stage=9 WHERE uid=@uid AND stage=8 AND dungeon_floor=3","uid",(long)p.Entity.UID)!=1)throw new InvalidOperationException("Tower reconstruction changed.");
                Ready(p,()=>context() && Cauldron(p));t.Commit();
            }}Q.Install(p,pieces.Concat(clinkers).Concat(new[]{key}).ToArray(),new[]{tower});DefenseTalismanScenes.Find(p).Completed=true;return true;
        }
        internal static bool Claim(GameState p,Func<bool> context){
            Ready(p,context);if(!Near(p,1015,685,599))return false;uint tower;ConquerItem[] rewards;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);if(run.Stage!=9 || p.Inventory.Count>39)return false;
                tower=Materials(c,t,p,721785).FirstOrDefault(id=>Material(c,t,p,id,721785));if(tower==0)return false;
                ConquerItemBaseInformation soul;if(!ConquerItemInformation.BaseInformations.TryGetValue(run.Soul,out soul) || run.Soul<800000 || run.Soul>=900000 || soul.PurificationLevel!=7)throw new InvalidOperationException("Tower soul reward changed.");
                Consume(c,t,p,tower,721785);rewards=new[]{Q.New(202008,true),Q.New(run.Soul,true)};foreach(var item in rewards)Q.Insert(c,t,p.Entity.UID,item);
                if(Q.Execute(c,t,"UPDATE revora_defense_talisman SET stage=10,rewards=@rewards WHERE uid=@uid AND stage=9",
                    "rewards",rewards.Select(i=>(long)i.UID).ToArray(),"uid",(long)p.Entity.UID)!=1)throw new InvalidOperationException("Tower final claim changed.");
                Ready(p,()=>context() && Near(p,1015,685,599));ProgressionEvidence.RecordEquipmentQuestAggregate(c,t,p.Entity.UID);t.Commit();
            }}Q.Install(p,new[]{tower},rewards);return true;
        }
    }
}
