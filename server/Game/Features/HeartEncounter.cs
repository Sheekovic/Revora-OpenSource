using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    // Owned campaign scenes; ordinary admission is through the City Commander.
    internal static class HeartEncounter
    {
        internal const uint Family=9926200,CacheNpc=900000191;
        private static readonly object Gate=new object();
        private static volatile bool initialized;
        private static DateTime nextSweep;
        internal static void Initialize()
        {
            lock(Gate){
                if(Scenes.Count!=0)throw new InvalidOperationException("Cannot recover Heart while scenes exist.");
                HeartCampaign.Initialize();HeartCampaign.RecoverInterruptedEncounters();
                nextSweep=DateTime.MinValue;initialized=true;
            }
        }
        internal sealed class Scene
        {
            internal Guid Id,Run;
            internal GameState Player;
            internal Map Map;
            internal int Mask;
            internal DateTime Until;
            internal bool Ended;
            internal readonly Entity[] Mobs=new Entity[11];
            internal readonly Guid[] Actors=new Guid[12];
        }
        internal static readonly ConcurrentDictionary<ushort,Scene> Scenes=new ConcurrentDictionary<ushort,Scene>();
        private static readonly ushort[,] Locations={{40,44},{44,40},{52,40},{56,44},{40,50},{56,50},{42,56},{46,58},{50,58},{54,56},{48,53}};
        [ThreadStatic] private static Guid? transfer;
        internal static bool IsEncounter(Entity e){return e!=null && e.MonsterInfo!=null && e.MonsterInfo.ID>=Family && e.MonsterInfo.ID<Family+11;}
        private static bool Eligible(Scene s,GameState p,bool alive=true)
        {
            Scene owned;
            return !s.Ended && Q.Player(p,alive) && ReferenceEquals(s.Player,p) && p.Entity.MapID==s.Map.ID && ReferenceEquals(p.Map,s.Map)
                && HeartCampaign.Clock()<s.Until && Scenes.TryGetValue(s.Map.ID,out owned) && ReferenceEquals(owned,s);
        }
        internal static bool BlockAttack(Entity attacker,Entity target)
        {
            var mob=IsEncounter(attacker)?attacker:IsEncounter(target)?target:null;
            lock(Gate){
                if(mob==null)return attacker!=null && target!=null && (Scenes.ContainsKey(attacker.MapID)||Scenes.ContainsKey(target.MapID));
                Scene s;Entity saved;
                int slot=(int)(mob.MonsterInfo.ID-Family);
                if(!Scenes.TryGetValue(mob.MapID,out s) || !ReferenceEquals(s.Mobs[slot],mob) || !s.Map.Entities.TryGetValue(mob.UID,out saved)
                    || !ReferenceEquals(saved,mob) || mob.Dead || (s.Mask&(1<<slot))!=0)return true;
                var fighter=ReferenceEquals(mob,attacker)?target:attacker;
                return fighter==null || !Eligible(s,fighter.Owner) || fighter.Dead ||
                    !(ReferenceEquals(fighter,s.Player.Entity) || fighter.Companion && ReferenceEquals(fighter,s.Player.Companion)
                    && s.Map.Companions.TryGetValue(fighter.UID,out saved) && ReferenceEquals(saved,fighter));
            }
        }
        private sealed class Lease:IDisposable {private readonly object gate;internal Lease(object g){gate=g;}public void Dispose(){Monitor.Exit(gate);}}
        internal static IDisposable DamageLease(Entity a,Entity b)
        {
            var mob=IsEncounter(a)?a:IsEncounter(b)?b:null;Scene s;
            return mob!=null && Scenes.TryGetValue(mob.MapID,out s) && Monitor.TryEnter(s.Player.ItemSyncRoot)?new Lease(s.Player.ItemSyncRoot):null;
        }
        internal static bool BlockDamage(Entity a,Entity b)
        {
            if(BlockAttack(a,b))return true;
            var mob=IsEncounter(a)?a:IsEncounter(b)?b:null;Scene s;
            return mob!=null && (!Scenes.TryGetValue(mob.MapID,out s)||!Monitor.IsEntered(s.Player.ItemSyncRoot));
        }
        internal static bool AllowTeleport(GameState p,ushort destination,ushort x,ushort y)
        {
            Scene s;if(!Scenes.TryGetValue(destination,out s))return true;
            lock(Gate)return !s.Ended && Q.Player(p,false) && ReferenceEquals(p,s.Player) && HeartCampaign.Clock()<s.Until
                && s.Map.Floor[x,y,MapObjectType.Player] && (transfer==s.Id || Eligible(s,p,false) && Kernel.GetDistance(p.Entity.X,p.Entity.Y,x,y)<22);
        }
        internal static bool Enter(GameState p)
        {
            if(!Q.Player(p))return false;
            lock(p.ItemSyncRoot)lock(Gate){
                if(p.Entity.MapID!=1002 || p.Booth!=null || p.Trade==null || p.Trade.InTrade || Scenes.Count>=16)return false;
                var run=HeartCampaign.Resume(p);if(run==null || run.Delivered || run.Mask==4095)return false;
                Map source;if(!Kernel.Maps.TryGetValue(700,out source))throw new InvalidOperationException("Heart arena terrain missing.");
                Scene s=null;
                try {
                    DateTime now=HeartCampaign.Clock(),end=PlanetWeekend.Current(now).EndUtc;
                    s=new Scene{Player=p,Run=run.Id,Mask=run.Mask,Until=end<now.AddMinutes(15)?end:now.AddMinutes(15)};
                    s.Map=Map.CreateDynamic(700,source.Path,false);s.Map.Portals.Clear();
                    for(int i=0;i<11;i++)if(!s.Map.Floor[Locations[i,0],Locations[i,1],MapObjectType.Player])throw new InvalidOperationException("Heart spawn blocked.");
                    if(!s.Map.Floor[48,41,MapObjectType.Player] || !s.Map.Floor[48,48,MapObjectType.Player])throw new InvalidOperationException("Heart landing blocked.");
                    s.Id=HeartCampaign.BeginEncounter(p,run.Id,s.Map.ID);
                    if(s.Id==Guid.Empty){s.Map.Dispose();return false;}
                    if(!Scenes.TryAdd(s.Map.ID,s))throw new InvalidOperationException("Heart scene collision.");
                    MapsTable.MapInformations[s.Map.ID]=new MapsTable.MapInformation{ID=s.Map.ID,BaseID=700,Status=2};
                    for(int i=0;i<12;i++)if((s.Mask&(1<<i))==0){s.Actors[i]=HeartCampaign.ActorIdentity(p,s.Run,s.Id,i);if(s.Actors[i]==Guid.Empty)throw new InvalidOperationException("Heart actor reservation missing.");}
                    for(int i=0;i<10;i++)if((s.Mask&(1<<i))==0)Spawn(s,i);
                    if(s.Mask==1023)Spawn(s,10);
                    s.Map.AddNpc(new NpcSpawn{UID=CacheNpc,MapID=s.Map.ID,X=48,Y=48,Mesh=10,Name="BannerSupplies",Type=Enums.NpcType.Talker});
                    transfer=s.Id;try{p.Entity.Teleport(700,s.Map.ID,48,41);}finally{transfer=null;}
                    if(p.Entity.MapID!=s.Map.ID)throw new InvalidOperationException("Heart arena arrival failed.");
                    return true;
                }catch(Exception ex){
                    if(s!=null){
                        if(s.Id!=Guid.Empty){try{s.Ended=HeartCampaign.EndEncounter(p.Entity.UID,s.Id);}catch(Exception close){Console.WriteLine("[heart] close_failure="+close.GetType().Name);}}
                        else s.Ended=true;
                        if(s.Ended)Cleanup(s);
                    }
                    Q.Fail(p,"heart-entry",ex);return false;
                }
            }
        }
        private static void Spawn(Scene s,int slot)
        {
            var info=MonsterInformation.MonsterInformations[1413].Copy();
            info.ID=Family+(uint)slot;info.Name=slot==10?"BannerGuardian":"BannerRaider";
            info.Boss=slot==10;info.Guard=info.Reviver=false;info.SpellID=0;info.AttackType=1;
            info.Hitpoints=slot==10?210300u:35050u;info.MinAttack=info.MaxAttack=slot==10?1800u:900u;
            info.AttackRange=1;info.ViewRange=18;info.MoveSpeed=800;info.RunSpeed=500;info.AttackSpeed=1500;
            info.RespawnTime=int.MaxValue/2;info.BoundX=48;info.BoundY=48;info.BoundCX=info.BoundCY=20;
            var mob=new Entity(EntityFlag.Monster,false){UID=s.Map.EntityUIDCounter.Next,MapID=s.Map.ID,X=Locations[slot,0],Y=Locations[slot,1],
                MonsterInfo=info,MapObjType=MapObjectType.Monster,Name=info.Name,Body=info.Mesh,Level=info.Level,
                MinAttack=info.MinAttack,MaxAttack=info.MaxAttack,MagicAttack=info.MaxAttack,Defence=info.Defence};
            while(s.Map.Entities.ContainsKey(mob.UID)||Kernel.GamePool.ContainsKey(mob.UID))mob.UID=s.Map.EntityUIDCounter.Next;
            info.Owner=mob;mob.Hitpoints=mob.MaxHitpoints=info.Hitpoints;mob.ApplyMonsterBossHud();mob.SendUpdates=true;
            s.Mobs[slot]=mob;s.Map.AddEntity2(mob);
        }
        internal static bool TryDie(Entity mob,Entity killer)
        {
            if(!IsEncounter(mob))return false;
            Scene s;if(!Scenes.TryGetValue(mob.MapID,out s)||!Monitor.TryEnter(s.Player.ItemSyncRoot))return true;
            try{lock(Gate){
                if(BlockAttack(killer,mob)||Kernel.GetDistance(killer.X,killer.Y,mob.X,mob.Y)>Constants.pScreenDistance)return true;
                int slot=(int)(mob.MonsterInfo.ID-Family);
                try{
                    if(!HeartCampaign.Record(s.Player,s.Run,slot,s.Actors[slot],HeartCampaign.Clock()))throw new InvalidOperationException("Heart death credit refused.");
                    s.Mask|=1<<slot;mob.Hitpoints=0;mob.MonsterInfo.SendScreen(new Data(true){UID=mob.UID,ID=Data.RemoveEntity});s.Map.RemoveEntity(mob);
                    if(s.Mask==1023)Spawn(s,10);
                }catch(Exception ex){Q.Fail(s.Player,"heart-death",ex);}
            }}finally{Monitor.Exit(s.Player.ItemSyncRoot);}return true;
        }
        internal static bool Collect(GameState p)
        {
            if(!Q.Player(p))return false;
            lock(p.ItemSyncRoot)lock(Gate){
                Scene s;if(!Scenes.TryGetValue(p.Entity.MapID,out s)||!Eligible(s,p)||s.Mask!=2047||p.ActiveNpc!=CacheNpc||Kernel.GetDistance(p.Entity.X,p.Entity.Y,48,48)>3)return false;
                if(!HeartCampaign.Record(p,s.Run,11,s.Actors[11],HeartCampaign.Clock()))return false;
                s.Mask=4095;Stop(s);return true;
            }
        }
        private static void Cleanup(Scene s)
        {
            if(s.Map==null || Kernel.GamePool.Values.Any(p=>p!=null&&!p.Fake&&p.Entity!=null&&p.Entity.MapID==s.Map.ID))return;
            foreach(var mob in s.Mobs)if(mob!=null)s.Map.RemoveEntity(mob);
            s.Map.Npcs.Clear();Scene removed;Scenes.TryRemove(s.Map.ID,out removed);MapsTable.MapInformations.Remove(s.Map.ID);s.Map.Dispose();
        }
        private static void Stop(Scene s)
        {
            if(!s.Ended){if(!HeartCampaign.EndEncounter(s.Player.Entity.UID,s.Id))throw new InvalidOperationException("Heart encounter close was not saved.");s.Ended=true;}
            if(Q.Player(s.Player,false))HeartCampaign.Return(s.Player);
            Cleanup(s);
        }
        internal static void Tick()
        {
            if(!initialized)return;
            lock(Gate){var now=HeartCampaign.Clock();if(now<nextSweep)return;nextSweep=now.AddSeconds(1);}
            foreach(var s in Scenes.Values.ToArray()){
                if(!Monitor.TryEnter(s.Player.ItemSyncRoot))continue;
                try{lock(Gate){if(s.Ended||!Eligible(s,s.Player))Stop(s);}}
                catch(Exception ex){Console.WriteLine("[heart] lifecycle_failure="+ex.GetType().Name);}
                finally{Monitor.Exit(s.Player.ItemSyncRoot);}
            }
        }
        internal static void OnLogin(GameState p)
        {
            if(!initialized || !Q.Player(p,false))return;
            lock(p.ItemSyncRoot)try{lock(Gate){
                if(!Q.Player(p,false))return;
                // The gate excludes old-session combat; only the new canonical player is returned.
                foreach(var s in Scenes.Values.Where(s=>s.Player.Entity.UID==p.Entity.UID).ToArray()){
                    if(!HeartCampaign.EndEncounter(p.Entity.UID,s.Id))throw new InvalidOperationException("Heart old session did not close.");
                    s.Ended=true;
                }
                using(var c=DataHolder.MySqlConnection){
                    c.Open();Guid pending=Guid.Empty;
                    using(var q=Q.Command(c,null,"SELECT id FROM revora_heart_encounters WHERE uid=@uid AND NOT returned AND state='active'","uid",(long)p.Entity.UID)){
                        object value=q.ExecuteScalar();if(value!=null)pending=(Guid)value;
                    }
                    if(pending!=Guid.Empty && !HeartCampaign.EndEncounter(p.Entity.UID,pending))throw new InvalidOperationException("Heart interrupted session did not close.");
                }
                if(!HeartCampaign.Return(p))return;
                foreach(var s in Scenes.Values.Where(s=>s.Player.Entity.UID==p.Entity.UID && s.Ended).ToArray())Cleanup(s);
            }}catch(Exception ex){Q.Fail(p,"heart-login",ex);}
        }
    }
}
