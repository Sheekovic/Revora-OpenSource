using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.IO;
using System.Security.Cryptography;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class TeratoEncounter
    {
        internal const uint Family=9916100,Health=50000000,Royal=180810;
        internal const int Capacity=16;
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        internal static readonly object Gate=new object();
        internal sealed class Actor {
            internal Guid Run;internal GameState Owner;internal Map Map;internal Entity Body;internal uint[] Party;
            internal DateTime Until;internal bool Released;
        }
        internal static readonly ConcurrentDictionary<uint,Actor> Actors=new ConcurrentDictionary<uint,Actor>();
        private sealed class Take {internal ConquerItem Item;internal uint Uid,Type;internal ushort Stack,Count;internal bool Bound;}
        private sealed class Offer {internal List<Take> Parts;internal DateTime Until;}
        private static readonly ConditionalWeakTable<GameState,Offer> offers=new ConditionalWeakTable<GameState,Offer>();
        internal static void Initialize() {
            var template=MonsterInformation.MonsterInformations[4152];
            if(template==null || template.Name!="DragonKing" || template.Mesh!=950)throw new InvalidOperationException("Terato presentation template changed.");
            foreach(uint id in new uint[]{720598,1088001})if(!ConquerItemInformation.BaseInformations.ContainsKey(id))throw new InvalidOperationException("Terato offering catalog missing.");
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_terato_runs(
id uuid PRIMARY KEY,uid bigint NOT NULL,route integer NOT NULL CHECK(route IN(0,1)),party bigint[] NOT NULL,
state varchar(10) NOT NULL CHECK(state IN('active','victory')),map integer NOT NULL CHECK(map IN(1002,2056)),
x integer NOT NULL,y integer NOT NULL,killer bigint NOT NULL DEFAULT 0,created_at timestamptz NOT NULL DEFAULT now(),defeated_at timestamptz);
ALTER TABLE revora_terato_runs ADD COLUMN IF NOT EXISTS valuable_reward boolean NOT NULL DEFAULT false;
CREATE UNIQUE INDEX IF NOT EXISTS revora_terato_active_owner ON revora_terato_runs(uid) WHERE state='active';
CREATE TABLE IF NOT EXISTS revora_terato_materials(run uuid NOT NULL REFERENCES revora_terato_runs(id),item_uid bigint NOT NULL,
item_type integer NOT NULL,before_stack integer NOT NULL,consumed integer NOT NULL,bound boolean NOT NULL,PRIMARY KEY(run,item_uid));");}
            TeratoRewards.Initialize();PlaceRoyal();
            var sixth=Kernel.Maps[2056];Revora.Interfaces.INpc seal;
            if(sixth.Npcs.TryGetValue(900000127,out seal)) {if(seal.X!=532 || seal.Y!=394 || seal.Mesh!=8630)throw new InvalidOperationException("Dragon seal identity collision.");}
            else {if(!sixth.Floor[532,394,MapObjectType.Player])throw new InvalidOperationException("Dragon seal is blocked.");
                sixth.AddNpc(new NpcSpawn{UID=900000127,MapID=2056,X=532,Y=394,Mesh=8630,Name="SealKeeper",Type=Enums.NpcType.Talker});}
        }
        private static void PlaceRoyal() {
            using(var hash=SHA256.Create())using(var file=File.OpenRead(Path.Combine(Constants.DMapsPath,"maps","1002.map")))
                if(System.BitConverter.ToString(hash.ComputeHash(file)).Replace("-","")!="9F75CC638480A0485E6B17BDF9F9F76886A9AB7449317BEBE18E72EF6AD34A10")throw new InvalidOperationException("Royal Guardian terrain changed.");
            Map map;if(!Kernel.Maps.TryGetValue(1002,out map) || map.ID!=1002 || map.BaseID!=1002)throw new InvalidOperationException("Royal Guardian world missing.");
            var old=Kernel.Maps.Values.Where(m=>m.Npcs.ContainsKey(Royal)).ToArray();
            foreach(var world in old){var n=world.Npcs[Royal];if(n.Mesh!=8630 || !(world.ID==5000 && n.X==90 && n.Y==743 || world.ID==1002 && n.X==517 && n.Y==742))
                throw new InvalidOperationException("Royal Guardian identity collision.");}
            if(!map.Npcs.ContainsKey(Royal)) {
                if(!map.Floor[517,742,MapObjectType.Player] || map.Npcs.Values.Any(n=>Kernel.GetDistance(n.X,n.Y,517,742)<2))throw new InvalidOperationException("Royal Guardian site blocked.");
                map.AddNpc(new NpcSpawn{UID=Royal,MapID=1002,X=517,Y=742,Mesh=8630,Name="RoyalGuardian",Type=Enums.NpcType.Talker});
            }
            foreach(var world in old.Where(m=>m.ID!=1002))world.RemoveNpc(world.Npcs[Royal]);
        }
        private static bool Ready(GameState p) {return Q.Player(p) && p.Trade!=null && !p.Trade.InTrade && p.Booth==null && p.Map!=null && p.Map.BaseID==p.Map.ID;}
        private static bool Site(GameState p,int route) {return Ready(p) && (route==0?p.Entity.MapID==2056 && Kernel.GetDistance(p.Entity.X,p.Entity.Y,532,394)<=6
            :p.Entity.MapID==1002 && Q.Near(p,Royal,1002) && p.Map.Npcs[Royal].X==517 && p.Map.Npcs[Royal].Y==742);}
        internal static bool IsEncounter(Entity e) {return e!=null && e.MonsterInfo!=null && e.MonsterInfo.ID==Family;}
        internal static bool Eligible(Actor a,GameState p,bool alive) {
            if(a.Released || !Q.Player(a.Owner,false) || a.Owner.Entity.MapID!=a.Map.ID || !Q.Player(p,alive) || p.Entity.MapID!=a.Map.ID
                || !ReferenceEquals(p.Map,a.Map) || !a.Party.Contains(p.Entity.UID))return false;
            if(ReferenceEquals(p,a.Owner))return true;
            GameState member;var team=a.Owner.Team;
            return team!=null && ReferenceEquals(p.Team,team) && team.TryGetMember(p.Entity.UID,out member) && ReferenceEquals(member,p)
                && team.TryGetMember(a.Owner.Entity.UID,out member) && ReferenceEquals(member,a.Owner);
        }
        private static GameState Fighter(Actor a,Entity entity) {
            if(entity==null || entity.Dead)return null;var p=entity.Owner;if(!Eligible(a,p,true))return null;
            if(ReferenceEquals(p.Entity,entity))return p;Entity current;
            return entity.Companion && ReferenceEquals(p.Companion,entity) && entity.MapID==a.Map.ID
                && a.Map.Companions.TryGetValue(entity.UID,out current) && ReferenceEquals(current,entity)?p:null;
        }
        internal static bool BlockAttack(Entity attacker,Entity target) {
            var mob=IsEncounter(attacker)?attacker:IsEncounter(target)?target:null;if(mob==null)return false;
            Actor a;return !Actors.TryGetValue(mob.UID,out a) || !ReferenceEquals(a.Body,mob) || Clock()>=a.Until
                || Fighter(a,ReferenceEquals(mob,attacker)?target:attacker)==null;
        }
        private static uint[] Party(GameState p) {
            var members=p.Team==null?new[]{p}:p.Team.Teammates.Concat(new[]{p}).ToArray();GameState current;
            return members.Where(m=>Q.Player(m,false) && m.Entity.MapID==p.Entity.MapID && Kernel.GetDistance(m.Entity.X,m.Entity.Y,p.Entity.X,p.Entity.Y)<=Constants.pScreenDistance
                && (ReferenceEquals(m,p) || ReferenceEquals(m.Team,p.Team) && p.Team.TryGetMember(m.Entity.UID,out current) && ReferenceEquals(m,current)))
                .Select(m=>m.Entity.UID).Distinct().OrderBy(uid=>uid).Take(5).ToArray();
        }
        private static bool Material(ConquerItem i) {return i!=null && i.Position==0 && i.Warehouse==0 && i.Lock==0 && !i.Suspicious && !i.InWardrobe && i.Days==0;}
        private static List<Take> Plan(GameState p,ConquerItem pill,int meteors) {
            if(!Material(pill) || pill.ID!=720598 || !p.Inventory.Objects.Any(i=>ReferenceEquals(i,pill)))return null;
            var plan=new List<Take>{new Take{Item=pill,Uid=pill.UID,Type=pill.ID,Stack=pill.StackSize,Count=1,Bound=pill.Bound}};
            foreach(var item in p.Inventory.Objects.Where(i=>i.ID==1088001 && Material(i)).OrderByDescending(i=>i.Bound).ThenBy(i=>i.UID)) {
                if(meteors==0)break;int take=Math.Min(meteors,Math.Max(1,(int)item.StackSize));
                plan.Add(new Take{Item=item,Uid=item.UID,Type=item.ID,Stack=item.StackSize,Count=(ushort)take,Bound=item.Bound});meteors-=take;
            }
            return meteors==0?plan:null;
        }
        private static bool Landing(Map map,ushort x,ushort y,out ushort rx,out ushort ry) {
            for(int radius=3;radius<=15;radius++)for(int dx=-radius;dx<=radius;dx++)for(int dy=-radius;dy<=radius;dy++) {
                if(Math.Max(Math.Abs(dx),Math.Abs(dy))!=radius)continue;int tx=x+dx,ty=y+dy;
                if(tx<0 || ty<0 || tx>ushort.MaxValue || ty>ushort.MaxValue || !map.Floor[tx,ty,MapObjectType.Monster]
                    || map.Npcs.Values.Any(n=>Kernel.GetDistance(n.X,n.Y,(ushort)tx,(ushort)ty)<3)
                    || Kernel.GamePool.Values.Any(p=>p!=null && p.Entity!=null && p.Entity.MapID==map.ID && Kernel.GetDistance(p.Entity.X,p.Entity.Y,(ushort)tx,(ushort)ty)<2))continue;
                rx=(ushort)tx;ry=(ushort)ty;return true;
            }rx=ry=0;return false;
        }
        private static string Summon(GameState p,int route,List<Take> parts) {
            if(!Site(p,route))return "Stand beside the dragon's seal.";
            lock(Gate) {
                var existing=Actors.Values.FirstOrDefault(a=>a.Owner.Entity.UID==p.Entity.UID);
                if(existing!=null){if(ReferenceEquals(existing.Owner,p) && Clock()<existing.Until)return "Your dragon still waits at ("+existing.Body.X+","+existing.Body.Y+").";Release(existing);}
                ushort x,y;if(Actors.Count>=Capacity || !Landing(p.Map,route==0?(ushort)532:(ushort)517,route==0?(ushort)394:(ushort)742,out x,out y))return "The seal cannot hold another dragon yet. Return shortly.";
                Guid run=Guid.NewGuid();uint[] party=Party(p);if(!party.Contains(p.Entity.UID))return "Gather your companions beside the seal.";bool paid=false;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    using(var q=Q.Command(c,t,"SELECT mapid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                        if(Convert.ToUInt32(q.ExecuteScalar())!=p.Entity.MapID)throw new InvalidOperationException("Terato summoner location changed.");
                    using(var q=Q.Command(c,t,"SELECT id,route FROM revora_terato_runs WHERE uid=@uid AND state='active' FOR UPDATE","uid",(long)p.Entity.UID))
                    using(var r=q.ExecuteReader())if(r.Read()){if(r.GetInt32(1)!=route)return "Your offering belongs to the other dragon seal.";run=r.GetGuid(0);paid=true;}
                    if(!paid) {
                        if(parts==null)return "Bring a Dragon Pill"+(route==1?" and five Meteors.":".");
                        using(var q=Q.Command(c,t,"SELECT count(*) FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0","uid",(long)p.Entity.UID))
                            if(Convert.ToInt64(q.ExecuteScalar())!=p.Inventory.Count)throw new InvalidOperationException("Terato offering inventory requires reload.");
                        if(Q.Execute(c,t,"INSERT INTO revora_terato_runs(id,uid,route,party,state,map,x,y,valuable_reward) VALUES(@id,@uid,@route,@party,'active',@map,@x,@y,true)",
                            "id",run,"uid",(long)p.Entity.UID,"route",route,"party",party.Select(u=>(long)u).ToArray(),"map",(int)p.Entity.MapID,"x",(int)x,"y",(int)y)!=1)throw new InvalidOperationException("Terato summon not saved.");
                        foreach(var part in parts) {
                            ConquerItem item;if(!p.Inventory.TryGetItem(part.Uid,out item) || !ReferenceEquals(item,part.Item) || !Material(item) || item.ID!=part.Type || item.StackSize!=part.Stack || item.Bound!=part.Bound)
                                throw new InvalidOperationException("Terato offering changed.");
                            int after=Math.Max(1,(int)part.Stack)-part.Count;
                            string sql=after==0?"DELETE FROM items":"UPDATE items SET stacksize=@after";
                            if(Q.Execute(c,t,sql+@" WHERE uid=@item AND entityid=@uid AND id=@type AND stacksize=@stack AND bound=@bound AND position=0 AND warehouse=0
AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')='0'", "after",after,
                                "item",(long)part.Uid,"uid",(long)p.Entity.UID,"type",(int)part.Type,"stack",(int)part.Stack,"bound",part.Bound?1:0)!=1)throw new InvalidOperationException("Terato material ownership changed.");
                            if(Q.Execute(c,t,"INSERT INTO revora_terato_materials(run,item_uid,item_type,before_stack,consumed,bound) VALUES(@run,@item,@type,@stack,@count,@bound)",
                                "run",run,"item",(long)part.Uid,"type",(int)part.Type,"stack",(int)part.Stack,"count",(int)part.Count,"bound",part.Bound)!=1)throw new InvalidOperationException("Terato payment not recorded.");
                        }
                    }else if(Q.Execute(c,t,"UPDATE revora_terato_runs SET party=@party,x=@x,y=@y WHERE id=@id AND state='active'", "party",party.Select(u=>(long)u).ToArray(),"x",(int)x,"y",(int)y,"id",run)!=1)throw new InvalidOperationException("Terato resume changed.");
                    t.Commit();
                }}
                if(!paid)foreach(var part in parts){int after=Math.Max(1,(int)part.Stack)-part.Count;if(after==0)Q.Install(p,new[]{part.Uid},new ConquerItem[0]);
                    else{part.Item.StackSize=(ushort)after;part.Item.Mode=Enums.ItemMode.Update;part.Item.Send(p);}}
                Spawn(p,run,party,x,y);return "Terato Dragon rises! Your companions may join the hunt.";
            }
        }
        private static void Spawn(GameState p,Guid run,uint[] party,ushort x,ushort y) {
            var info=MonsterInformation.MonsterInformations[4152].Copy();info.ID=Family;info.Name="TeratoDragon";info.Boss=true;info.Guard=info.Reviver=false;
            info.Hitpoints=Health;info.Level=138;info.MinAttack=8000;info.MaxAttack=12000;info.Defence=7000;info.SpellID=0;info.AttackType=1;
            info.ViewRange=18;info.AttackRange=3;info.AttackSpeed=2000;info.MoveSpeed=600;info.RunSpeed=600;info.RespawnTime=int.MaxValue/2;
            info.BoundX=x;info.BoundY=y;info.BoundCX=info.BoundCY=1;info.InSight=p.Entity.UID;
            var body=new Entity(EntityFlag.Monster,false){UID=p.Map.EntityUIDCounter.Next,MonsterInfo=info,MapID=p.Map.ID,X=x,Y=y,Name=info.Name,Body=info.Mesh,
                Level=138,MinAttack=8000,MaxAttack=12000,MagicAttack=12000,Defence=7000,MagicDefence=40,MapObjType=MapObjectType.Monster};
            while(p.Map.Entities.ContainsKey(body.UID) || p.Map.Npcs.ContainsKey(body.UID) || Actors.ContainsKey(body.UID))body.UID=p.Map.EntityUIDCounter.Next;
            info.Owner=body;body.MaxHitpoints=body.Hitpoints=Health;body.ApplyMonsterBossHud();body.SendUpdates=true;
            var a=new Actor{Run=run,Owner=p,Map=p.Map,Body=body,Party=party,Until=Clock().AddMinutes(30)};
            if(!Actors.TryAdd(body.UID,a))throw new InvalidOperationException("Terato actor identity collision.");
            try{p.Map.AddEntity2(body);foreach(var player in Kernel.GamePool.Values)if(Q.Player(player,false) && player.Entity.MapID==p.Map.ID && Kernel.GetDistance(player.Entity.X,player.Entity.Y,x,y)<Constants.nScreenDistance)body.SendSpawn(player,false);}
            catch{Release(a);throw;}
        }
        internal static void Release(Actor a) {lock(Gate){if(a.Released)return;a.Released=true;a.Body.MonsterInfo.SendScreen(new Data(true){UID=a.Body.UID,ID=Data.RemoveEntity});a.Map.RemoveEntity(a.Body);Actor ignored;Actors.TryRemove(a.Body.UID,out ignored);}}
        internal static bool TryUse(ConquerItem item,GameState p) {
            if(item==null || item.ID!=720598)return false;if(!Ready(p))return true;
            lock(p.ItemSyncRoot)try{if(!Material(item) || !p.Inventory.Objects.Any(i=>ReferenceEquals(i,item)))return true;
                if(Site(p,0))p.Send(Summon(p,0,Plan(p,item,0)));else p.Send("Use this pill at the sixth-floor seal (532,394), or bring it and five Meteors to the Royal Guardian in Wind Plain (517,742).");}
            catch(Exception ex){Q.Fail(p,"terato-summon",ex);}return true;
        }
        internal static bool HandleNpc(GameState p,NpcRequest request) {
            if(p==null || request==null || p.ActiveNpc!=Royal && p.ActiveNpc!=900000127)return false;
            if(p.ItemSyncRoot==null)return true;int route=p.ActiveNpc==Royal?1:0;
            lock(p.ItemSyncRoot)try {
                if(request.NpcID!=0 && request.NpcID!=p.ActiveNpc || !Site(p,route) || !Q.Near(p,p.ActiveNpc,(ushort)(route==0?2056:1002)))return true;
                var d=new Npcs(p);
                if(request.OptionID==0){offers.Remove(p);d.Text(route==1?"A Dragon Pill and five Meteors will awaken Terato. Gather your companions before making the offering.":"The dragon sleeps beneath this seal. Use a Dragon Pill to awaken it.");
                    if(route==1)d.Option("Offer the pill and Meteors.",1);d.Option("Resume my paid hunt.",3);d.Option("Leave the seal.",255);}
                else if(request.OptionID==1 && route==1){offers.Remove(p);var pill=p.Inventory.Objects.Where(i=>i.ID==720598 && Material(i)).OrderByDescending(i=>i.Bound).ThenBy(i=>i.UID).FirstOrDefault();var plan=Plan(p,pill,5);
                    if(plan==null)d.Text("Bring a Dragon Pill and five Meteors.");else{offers.Add(p,new Offer{Parts=plan,Until=Clock().AddMinutes(2)});d.Text("This offering spends one Dragon Pill and five Meteors. Shall I break the seal?");d.Option("Awaken the dragon.",2);d.Option("Keep my offering.",255);}}
                else if(request.OptionID==2 && route==1){Offer offer;if(!offers.TryGetValue(p,out offer))return true;offers.Remove(p);if(Clock()>=offer.Until)return true;d.Text(Summon(p,route,offer.Parts));}
                else if(request.OptionID==3){offers.Remove(p);d.Text(Summon(p,route,null));}
                else {offers.Remove(p);return true;}d.Send();
            }catch(Exception ex){Q.Fail(p,"terato-guardian",ex);}return true;
        }
        internal static bool TryDie(Entity mob,Entity killer) {
            if(!IsEncounter(mob))return false;Actor a;GameState p;string spiritMessage;
            if(killer==null || killer.Owner==null || killer.Owner.ItemSyncRoot==null)return true;
            var ownerLock=killer.Owner.ItemSyncRoot;
            if(!Monitor.TryEnter(ownerLock))return true;
            try {
            lock(Gate) {
                if(!Actors.TryGetValue(mob.UID,out a) || !ReferenceEquals(a.Body,mob) || mob.Dead || Clock()>=a.Until || (p=Fighter(a,killer))==null
                    || Kernel.GetDistance(p.Entity.X,p.Entity.Y,mob.X,mob.Y)>Constants.pScreenDistance)return true;
                Entity current;if(!a.Map.Entities.TryGetValue(mob.UID,out current) || !ReferenceEquals(current,mob))return true;
                var eligible=a.Party.Select(uid=>{GameState member;Kernel.GamePool.TryGetValue(uid,out member);return member;})
                    .Where(member=>Eligible(a,member,false) && Kernel.GetDistance(member.Entity.X,member.Entity.Y,mob.X,mob.Y)<=Constants.pScreenDistance).ToArray();
                try{using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    SkillTable.LockOwner(c,t,p);
                    if(Q.Execute(c,t,"UPDATE revora_terato_runs SET state='victory',killer=@killer,x=@x,y=@y,defeated_at=@now WHERE id=@id AND state='active'",
                        "killer",(long)p.Entity.UID,"x",(int)mob.X,"y",(int)mob.Y,"now",Clock(),"id",a.Run)!=1)return true;
                    TeratoRewards.Record(c,t,a.Run,p.Entity.UID,eligible.Select(m=>m.Entity.UID).ToArray());
                    ProgressionEvidence.Complete(c,t,p.Entity.UID,10526,"terato-kill-v1",a.Run.ToString("N"),1);
                    EquipmentAchievements.RecordUnarmedVictory(c,t,p,mob,killer,"terato:"+a.Run.ToString("N"));
                    using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10526)","uid",(long)p.Entity.UID))
                        if(!Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("Terato kill achievement was not saved.");
                    spiritMessage=DailySoulBall.RecordTerato(c,t,p,a);t.Commit();
                }}}catch(Exception ex){Q.Fail(p,"terato-defeat",ex);return true;}
                Release(a);
            }
            mob.DieCore(p.Entity);Q.Refresh(p);TeratoRewards.Tick(true);if(spiritMessage!=null)p.Send(spiritMessage);p.Send("Terato Dragon has fallen. Claim the spoils before they fade.");return true;
            }finally{Monitor.Exit(ownerLock);}
        }
        internal static void Tick() {
            lock(Gate)foreach(var a in Actors.Values.ToArray()) {
                if(!Q.Player(a.Owner,false) || a.Owner.Entity.MapID!=a.Map.ID || Clock()>=a.Until){Release(a);continue;}
                var nearest=a.Party.Select(uid=>{GameState p;Kernel.GamePool.TryGetValue(uid,out p);return p;}).Where(p=>Eligible(a,p,true))
                    .OrderBy(p=>Kernel.GetDistance(p.Entity.X,p.Entity.Y,a.Body.X,a.Body.Y)).FirstOrDefault();
                if(nearest==null){Release(a);continue;}a.Body.MonsterInfo.InSight=nearest.Entity.UID;
            }
            foreach(var a in Actors.Values.ToArray())TeratoCombat.Tick(a);
            TeratoRewards.Tick();
        }
    }
}
