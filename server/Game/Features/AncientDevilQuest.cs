using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
using S=Revora.Game.Features.AncientDevilStore;
using W=Revora.Game.Features.AncientDevilWorld;

namespace Revora.Game.Features
{
    // Initialized before listeners open; private encounters recover before admission.
    internal static class AncientDevilQuest
    {
        internal const uint Exorcist=300010,Exit=300016;
        private static bool enabled;
        private sealed class Offer {internal uint Npc;internal ushort Map;internal byte Profession,Option;internal DateTime Until;internal Guid Run;internal uint Certificate;}
        private static readonly ConditionalWeakTable<GameState,Offer> offers=new ConditionalWeakTable<GameState,Offer>();
        internal static bool Handles(uint npc){return npc>=Exorcist && npc<=AncientDevilSummons.Switch;}
        private static readonly ushort[] meshes={7690,7710,7720,7730,7740};
        private static readonly ushort[] xs={229,191,181,206,243},ys={233,201,176,179,219};
        private static readonly string[] names={"WarriorGuard","TrojanGuard","FireGuard","WaterGuard","ArcherGuard"};
        internal static void Initialize() {
            Map island,bird;
            if(!Kernel.Maps.TryGetValue(1052,out island) || island.BaseID!=1082 || !Kernel.Maps.TryGetValue(1015,out bird) || bird.BaseID!=1015)
                throw new InvalidOperationException("Ancient travel maps are missing.");
            var actors=new List<NpcSpawn>();
            using(var c=DataHolder.MySqlConnection){c.Open();
                using(var q=Q.Command(c,null,"SELECT id,name,lookface,mapid,cellx,celly FROM npcs WHERE id BETWEEN 300011 AND 300015 ORDER BY id"))
                using(var r=q.ExecuteReader())while(r.Read()) {
                    int index=Convert.ToInt32(r[0])-300011;
                    if(r.GetString(1)!=names[index] || Convert.ToUInt16(r[2])!=meshes[index] || Convert.ToInt32(r[3])!=1052
                        || Convert.ToUInt16(r[4])!=xs[index] || Convert.ToUInt16(r[5])!=ys[index])throw new InvalidOperationException("Ancient guard catalog changed.");
                    actors.Add(new NpcSpawn{UID=(uint)(300011+index),Name=names[index],Mesh=meshes[index],MapID=1052,X=xs[index],Y=ys[index],Type=Enums.NpcType.Talker});
                }
                if(actors.Count!=5)throw new InvalidOperationException("Ancient guard catalog incomplete.");
                using(var q=Q.Command(c,null,"SELECT (SELECT count(*) FROM npcs WHERE id IN(300010,300016,300017))+(SELECT count(*) FROM sobnpcs WHERE id IN(300010,300016,300017))"))
                    if(Convert.ToInt64(q.ExecuteScalar())!=0)throw new InvalidOperationException("Ancient travel NPC identities are occupied.");
            }
            actors.Add(new NpcSpawn{UID=Exorcist,Name="Exorcist",Mesh=2177,MapID=1015,X=715,Y=584,Type=Enums.NpcType.Talker});
            actors.Add(ReturnNpc(1052));
            actors.Add(new NpcSpawn{UID=AncientDevilSummons.Switch,Name="Ancient Seal",Mesh=2177,MapID=1052,X=218,Y=208,Type=Enums.NpcType.Talker});
            foreach(var actor in actors) {
                if(Kernel.Maps.Values.Any(m=>m.ID!=actor.MapID && m.Npcs.ContainsKey(actor.UID)))throw new InvalidOperationException("Ancient NPC identity collision.");
                var map=Kernel.Maps[actor.MapID];bool approach=false;
                for(int dx=-6;dx<=6;dx++)for(int dy=-6;dy<=6;dy++)if(Math.Max(Math.Abs(dx),Math.Abs(dy))>1
                    && map.Floor[actor.X+dx,actor.Y+dy,MapObjectType.Player])approach=true;
                if(!approach)throw new InvalidOperationException("Ancient NPC approach blocked.");
            }
            W.Initialize();AncientDevilCollection.Initialize();AncientDevilBossWorld.Initialize();
            foreach(var actor in actors){var map=Kernel.Maps[actor.MapID];Revora.Interfaces.INpc prior;
                if(map.Npcs.TryGetValue(actor.UID,out prior))map.RemoveNpc(prior);map.AddNpc(actor);}
            enabled=true;
        }
        internal static NpcSpawn ReturnNpc(ushort map) {
            return new NpcSpawn{UID=Exit,Name="Exorcist",Mesh=2177,MapID=map,X=214,Y=208,Type=Enums.NpcType.Talker};
        }
        private static bool Near(GameState p,uint npc) {
            if(!Q.Player(p) || p.Entity.PokerTable!=0)return false;
            ushort map=npc==Exorcist?(ushort)1015:p.Entity.MapID;
            if(npc!=Exorcist && map!=1052 && (npc!=Exit || !W.Hunts.ContainsKey(map) && !AncientDevilBossWorld.Hunts.ContainsKey(map)))return false;
            return Q.Near(p,npc,map) && p.Map.Npcs[npc].MapID==map;
        }
        private static S.Run Pending(GameState p) {
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                SkillTable.LockOwner(c,t,p);Guid id=Guid.Empty;
                using(var q=Q.Command(c,t,"SELECT id FROM revora_ancient_trials WHERE uid=@uid AND state IN('hunting','victory','certificate')","uid",(long)p.Entity.UID)){
                    var value=q.ExecuteScalar();if(value!=null)id=(Guid)value;
                }
                var run=id==Guid.Empty?null:S.Read(c,t,p.Entity.UID,id);t.Commit();return run;
            }}
        }
        private static void Travel(GameState p,bool enter) {
            var map=Kernel.Maps[enter?(ushort)1052:(ushort)1015];ushort x=enter?(ushort)216:(ushort)713,y=enter?(ushort)208:(ushort)584;
            if(!map.Floor[x,y,MapObjectType.Player])throw new InvalidOperationException("Ancient passage landing blocked.");
            p.Entity.Teleport(map.BaseID,map.ID,x,y);
            if(p.Entity.MapID!=map.ID || p.Entity.X!=x || p.Entity.Y!=y || !EntityTable.SaveEntity(p))throw new InvalidOperationException("Ancient passage was not saved.");
        }
        private static void Menu(GameState p,uint npc,string message=null) {
            offers.Remove(p);var dialog=new Npcs(p);byte option=0;S.Run run=null;
            if(npc==Exorcist){dialog.Text(message??(p.Entity.Level<50?"Return at level 50. The island devils show no mercy.":"The ancient seals are breaking. The island guards need your help."));if(p.Entity.Level>=50){option=1;dialog.Option("Take me to the island.",option);}}
            else if(npc==Exit){dialog.Text(message??"I can guide you back. There is no shame in returning alive.");option=1;dialog.Option(p.Entity.MapID==1052?"Return to Bird Island.":"Leave the trial.",option);}
            else {
                AncientDevilRules.Trial rule;
                if(!AncientDevilRules.TryTrial(p.Entity.Class,p.Entity.Level,npc,out rule))dialog.Text(message??"Reach level 50 and seek the guard of your own path.");
                else {
                    run=Pending(p);
                    if(run!=null && run.Guard!=npc)dialog.Text(message??"Return to the guard who sent you. Your trial is not finished.");
                    else if(run==null || run.State=="hunting"){dialog.Text(message??"Defeat my devil and return for your certificate. I will exchange it for your class amulet.");option=1;dialog.Option("Enter the trial.",option);}
                    else if(run.State=="victory"){dialog.Text(message??"You have proven your strength. Your certificate awaits.");option=2;dialog.Option("Claim my certificate.",option);}
                    else {dialog.Text(message??"Give me your certificate for your class amulet. Trade amulets with other paths to gather nine different kinds.");option=3;dialog.Option("Exchange my certificate.",option);}
                }
            }
            if(option!=0)offers.Add(p,new Offer{Npc=npc,Map=p.Entity.MapID,Profession=p.Entity.Class,Option=option,Until=W.Clock().AddSeconds(60),
                Run=run==null?Guid.Empty:run.Id,Certificate=run==null?0:run.CertificateUid});
            dialog.Option("Farewell.",255);dialog.Send();
        }
        internal static bool HandleNpc(GameState p,NpcRequest request) {
            if(!enabled || p==null || request==null || !Handles(p.ActiveNpc))return false;
            uint npc=p.ActiveNpc;if(p.ItemSyncRoot==null || request.NpcID!=0 && request.NpcID!=npc)return true;
            if(npc==AncientDevilSummons.Switch)return AncientDevilSeal.Handle(p,request);
            lock(p.ItemSyncRoot)try {
                if(!Near(p,npc))return true;
                if(request.OptionID==255){offers.Remove(p);p.ActiveNpc=0;return true;}
                if(request.OptionID==0){Menu(p,npc);return true;}
                Offer offer;
                if(!offers.TryGetValue(p,out offer) || offer.Npc!=npc || offer.Map!=p.Entity.MapID || offer.Profession!=p.Entity.Class
                    || offer.Option!=request.OptionID || W.Clock()>=offer.Until)return true;
                offers.Remove(p);
                if(npc==Exorcist){if(p.Entity.Level>=50)Travel(p,true);p.ActiveNpc=0;return true;}
                if(npc==Exit){W.Hunt hunt;if(W.Hunts.TryGetValue(p.Entity.MapID,out hunt)){if(ReferenceEquals(hunt.Player,p))W.Leave(hunt);}
                    else {AncientDevilBossWorld.Hunt battle;
                        if(AncientDevilBossWorld.Hunts.TryGetValue(p.Entity.MapID,out battle)){if(ReferenceEquals(battle.Player,p))AncientDevilBossWorld.Leave(battle);}
                        else Travel(p,false);
                    }p.ActiveNpc=0;return true;}
                AncientDevilRules.Trial rule;if(!AncientDevilRules.TryTrial(p.Entity.Class,p.Entity.Level,npc,out rule))return true;
                if(offer.Option==1){if(W.Start(p,npc)==null)Menu(p,npc,"The trials are occupied. Return shortly.");else p.ActiveNpc=0;return true;}
                ConquerItem item;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    item=offer.Option==2?S.Claim(c,t,p,offer.Run):S.Exchange(c,t,p,offer.Run,offer.Certificate);t.Commit();
                }}
                if(item!=null){Q.Install(p,offer.Option==2?new uint[0]:new[]{offer.Certificate},new[]{item});Q.Refresh(p);}
                Menu(p,npc,item==null?"Keep your certificate close and make room in your bag.":offer.Option==2?"Well fought. Bring me that certificate for your amulet.":"Your amulet is ready. Seek the other paths for the rest.");
            }catch(Exception ex){Q.Fail(p,"ancient-dialogue",ex);}
            return true;
        }
        internal static void Tick(){if(enabled){W.Tick();AncientDevilBossWorld.Tick();}}
    }
}
