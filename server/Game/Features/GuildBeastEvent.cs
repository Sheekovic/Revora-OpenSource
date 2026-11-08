using System;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Revora.Client;
using Revora.Database;
using Revora.Game.ConquerStructures.Society;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
using R=Revora.Game.Features.GuildBeastRuns;
namespace Revora.Game.Features {
    internal static class GuildBeastEvent {
        internal const uint Guard=8150,Family=7006,Prize=1088000;
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        private static readonly object gate=new object();
        private sealed class Life {internal Map Map;internal Entity Body;internal MonsterInformation Info;internal R.Run Run;internal bool Ended;}
        private static readonly ConditionalWeakTable<Entity,Life> bodies=new ConditionalWeakTable<Entity,Life>();
        private static Life active;private static bool enabled;private static DateTime nextPoll;
        private static string Day(DateTime now){return now.ToLocalTime().ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);}
        private static Map Field() {
            Map map;Revora.Interfaces.INpc npc;
            if(!Kernel.Maps.TryGetValue(1038,out map) || map.ID!=1038 || map.BaseID!=1038 || !map.Npcs.TryGetValue(Guard,out npc)
                || npc.MapID!=1038 || npc.X!=56 || npc.Y!=56 || npc.Mesh!=8150)throw new InvalidOperationException("Installed Beast Guard is unavailable.");
            return map;
        }
        internal static void Initialize() {
            lock(gate) {
                if(enabled)return;Field();MonsterInformation info;ConquerItemBaseInformation item;
                if(!MonsterInformation.MonsterInformations.TryGetValue(Family,out info) || info.Name.Replace(" ","")!="GuildBeast" || info.Hitpoints==0
                    || !ConquerItemInformation.BaseInformations.TryGetValue(Prize,out item) || item.Name.Replace(" ","")!="DragonBall")throw new InvalidOperationException("Guild Beast catalog changed.");
                R.Initialize();if(active!=null)Retire(active);nextPoll=DateTime.MinValue;enabled=true;
            }
        }
        internal static bool Managed(Entity body){Life value;return body!=null && bodies.TryGetValue(body,out value);}
        private static bool Owned(Life life) {
            Entity actual;Map map;
            return ReferenceEquals(active,life) && !life.Ended && life.Body.UID==life.Run.Actor && life.Body.MapID==1038 && !life.Body.Companion
                && life.Body.EntityFlag==EntityFlag.Monster && ReferenceEquals(life.Body.MonsterInfo,life.Info) && life.Info.ID==Family && ReferenceEquals(life.Info.Owner,life.Body)
                && Kernel.Maps.TryGetValue(1038,out map) && ReferenceEquals(map,life.Map) && map.Entities.TryGetValue(life.Run.Actor,out actual) && ReferenceEquals(actual,life.Body);
        }
        private static bool Approach(Map map,out ushort x,out ushort y) {
            for(int radius=1;radius<=5;radius++)for(int yy=56-radius;yy<=56+radius;yy++)for(int xx=56-radius;xx<=56+radius;xx++)
                if(Kernel.GetDistance((ushort)xx,(ushort)yy,56,56)<=5 && map.Floor[xx,yy,MapObjectType.Player,null] && !map.Npcs.Values.Any(n=>n.X==xx && n.Y==yy)) {x=(ushort)xx;y=(ushort)yy;return true;}
            x=y=0;return false;
        }
        private static bool Near(GameState p){return Q.Near(p,Guard,1038) && ReferenceEquals(p.Map,Field());}
        private static bool Member(GameState p) {
            Guild g;Guild.Member m;
            return p.Guild!=null && p.Entity.GuildID==p.Guild.ID && Kernel.Guilds.TryGetValue(p.Guild.ID,out g) && ReferenceEquals(g,p.Guild)
                && g.Members.TryGetValue(p.Entity.UID,out m) && ReferenceEquals(m,p.AsMember) && m.GuildID==g.ID;
        }
        private static string Fund(GameState p) {
            if(!GuildLeadership.Leader(p))return "The winning guild's leader must arrange the hunt.";
            var g=p.Guild;if(!Monitor.TryEnter(GuildLeadership.Gate(g)))return "Return in a moment.";
            try {
                if(!Near(p) || !GuildLeadership.Leader(p) || !ReferenceEquals(p.Guild,g))return "Bring your guild's leader to me.";
                GuildBeastFunding.Result result;ulong funds;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    SkillTable.LockOwner(c,t,p);result=GuildBeastFunding.Reserve(c,t,p.Entity.UID,g.ID,Clock());
                    if(result==GuildBeastFunding.Result.Rejected)return "The winning guild needs 500,000 silver in its fund. Arrange the hunt before 21:00.";
                    using(var q=Q.Command(c,t,"SELECT silverfund FROM guilds WHERE id=@guild","guild",(long)g.ID))funds=Convert.ToUInt64(q.ExecuteScalar());
                    if(!Near(p) || !GuildLeadership.Leader(p) || !ReferenceEquals(p.Guild,g))throw new InvalidOperationException("Beast funding owner changed.");t.Commit();
                }}
                g.SilverFund=funds;g.SendGuild(p);return result==GuildBeastFunding.Result.Funded?"It is settled. The beast will roam from 21:00 until 22:00.":"Today's hunt is already arranged. No further silver is needed.";
            }finally{Monitor.Exit(GuildLeadership.Gate(g));}
        }
        private static string Claim(GameState p) {
            if(p.Inventory.Count>=40)return "Make one place in your bag.";
            var g=Member(p)?p.Guild:null;object guildGate=g==null?null:GuildLeadership.Gate(g);bool held=false;
            if(guildGate!=null && !(held=Monitor.TryEnter(guildGate)))return "Return in a moment.";
            try {
                ConquerItem item,soul=null;string day=null;var now=Clock();
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    SkillTable.LockOwner(c,t,p);
                    if(g!=null)using(var q=Q.Command(c,t,"SELECT id FROM guilds WHERE id=@guild FOR UPDATE","guild",(long)g.ID))
                        if(q.ExecuteScalar()==null)throw new InvalidOperationException("Beast claim guild disappeared.");
                    using(var q=Q.Command(c,t,@"SELECT to_char(day,'YYYY-MM-DD') FROM revora_guild_beast_days
WHERE claimed_at IS NULL AND settled_at<=@now AND ((state='killed' AND killer=@uid) OR (state='fallback' AND guild_id=@guild AND expires_at>@now))
ORDER BY day LIMIT 1 FOR UPDATE","now",now,"uid",(long)p.Entity.UID,"guild",g==null?0L:(long)g.ID)) {
                        var value=q.ExecuteScalar();if(value!=null)day=Convert.ToString(value);
                    }
                    if(day==null)return "No Dragon Ball awaits you. If the beast survives, our winning leader or deputy may claim it before midnight.";
                    using(var q=Q.Command(c,t,"SELECT count(*) FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0","uid",(long)p.Entity.UID))
                        if(Convert.ToInt64(q.ExecuteScalar())!=p.Inventory.Count)throw new InvalidOperationException("Beast prize inventory requires reload.");
                    var run=R.Read(c,t,day);
                    if(run.State=="killed" && run.Soul!=0) {
                        ConquerItemBaseInformation info;
                        if(run.Soul<800000 || run.Soul>=900000 || !ConquerItemInformation.BaseInformations.TryGetValue(run.Soul,out info) || info.PurificationLevel!=7)
                            throw new InvalidOperationException("Beast soul contract is invalid.");
                        if(p.Inventory.Count>38)return "Leave two places for your Dragon Ball and soul.";
                        soul=Q.New(run.Soul,true);
                    }
                    item=Q.New(Prize,false);
                    if(!R.MarkClaim(c,t,day,p.Entity.UID,item.UID,now))return "That Dragon Ball is not yours to claim.";
                    Q.Insert(c,t,p.Entity.UID,item);
                    if(soul!=null) {
                        Q.Insert(c,t,p.Entity.UID,soul);
                        if(Q.Execute(c,t,@"UPDATE revora_guild_beast_days SET soul_uid=@soul WHERE day=CAST(@day AS date)
AND claimed_uid=@uid AND reward_item=@item AND soul_uid=0",
                            "day",day,"uid",(long)p.Entity.UID,"item",(long)item.UID,"soul",(long)soul.UID)!=1)
                            throw new InvalidOperationException("Beast soul receipt was not saved.");
                    }
                    if(!Near(p) || g!=null && (!Member(p) || !ReferenceEquals(p.Guild,g)))throw new InvalidOperationException("Beast prize owner changed.");t.Commit();
                }}
                Q.Install(p,new uint[0],soul==null?new[]{item}:new[]{item,soul});Q.Refresh(p);
                return soul==null?"A Dragon Ball, as promised. May it serve you well.":"The Dragon Ball and bound P7 soul are yours.";
            }finally{if(held)Monitor.Exit(guildGate);}
        }
        internal static bool HandleNpc(GameState p,NpcRequest request) {
            if(p!=null && request!=null && p.ActiveNpc==GuildSeasonClaims.Director && request.OptionID==89) {
                if(!enabled || !Q.Player(p) || request.NpcID!=0 && request.NpcID!=GuildSeasonClaims.Director)return true;
                uint traveler=p.Entity.UID;if(!SessionInstall.TryEnter(traveler))return true;
                try{lock(p.ItemSyncRoot)try {
                    if(!GuildSeasonClaims.Near(p))return true;
                    if(GuildWar.IsWar){p.Send("The battlefield is closed to visitors during Guild War.");return true;}
                    var map=Field();ushort x,y;if(!Approach(map,out x,out y)){p.Send("The way is blocked. Return shortly.");return true;}
                    p.Entity.Teleport(1038,x,y);
                    if(p.Entity.MapID!=1038 || !ReferenceEquals(p.Map,map) || !EntityTable.SaveEntity(p))throw new InvalidOperationException("Beast Guard travel did not persist.");
                }catch(Exception ex){Q.Fail(p,"guild-beast-travel",ex);}}finally{SessionInstall.Exit(traveler);}return true;
            }
            if(p==null || request==null || p.ActiveNpc!=Guard)return false;
            if(request.NpcID!=0 && request.NpcID!=Guard || !Q.Player(p) || !enabled)return true;
            uint uid=p.Entity.UID;if(!SessionInstall.TryEnter(uid))return true;
            try{lock(p.ItemSyncRoot)try {
                if(!Near(p))return true;var d=new Npcs(p);d.Avatar(110);
                if(request.OptionID==0) {
                    d.Text("The Guild Beast roams from 21:00 to 22:00. Slay it and return for a Dragon Ball.");
                    d.Option("Arrange today's hunt: 500,000 guild silver.",1);d.Option("Claim my reward.",2);d.Option("Farewell.",255);
                }else if(request.OptionID==1 || request.OptionID==2) {
                    d.Text(request.OptionID==1?Fund(p):Claim(p));d.Option("Return.",0);
                }else return true;d.Send();
            }catch(Exception ex){Q.Fail(p,"guild-beast-npc",ex);}}finally{SessionInstall.Exit(uid);}return true;
        }
        private static bool Landing(Map map,out ushort x,out ushort y) {
            // Installed monster bounds are 86,78 + 50,50. Check legal terrain;
            // never fabricate a walkable tile or overlap another monster.
            for(int yy=78;yy<128;yy++)for(int xx=86;xx<136;xx++)if(map.Floor[xx,yy,MapObjectType.Monster,null]){x=(ushort)xx;y=(ushort)yy;return true;}
            x=y=0;return false;
        }
        private static void Retire(Life life) {
            life.Ended=true;
            Entity current;if(life.Map.Entities.TryGetValue(life.Body.UID,out current) && ReferenceEquals(current,life.Body)) {
                life.Info.SendScreen(new Data(true){UID=life.Body.UID,ID=Data.RemoveEntity});life.Map.RemoveEntity(life.Body);
            }
            if(ReferenceEquals(active,life))active=null;
        }
        internal static void Tick() {
            if(!enabled || !Monitor.TryEnter(gate))return;
            try {
                var now=Clock();if(now<nextPoll)return;nextPoll=now.AddSeconds(5);
                if(active!=null && (now<active.Run.Start || now>=active.Run.End))Retire(active);
                R.Run run;var map=Field();Life next=null;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    R.Advance(c,t,now);run=R.Read(c,t,Day(now));
                    if(active!=null && (!Owned(active) || run==null || run.State!="active" || run.Life!=active.Run.Life || now<run.Start || now>=run.End))Retire(active);
                    if(active==null && run!=null && (run.State=="funded" || run.State=="active") && now>=run.Start && now<run.End) {
                        ushort x,y;if(Landing(map,out x,out y)) {
                            var info=MonsterInformation.MonsterInformations[Family].Copy();info.BoundX=x;info.BoundY=y;info.BoundCX=info.BoundCY=1;info.RespawnTime=int.MaxValue/2;
                            var body=new Entity(EntityFlag.Monster,false){UID=map.EntityUIDCounter.Next,MapID=1038,X=x,Y=y,MapObjType=MapObjectType.Monster,
                                MonsterInfo=info,Name=info.Name,Body=info.Mesh,Level=info.Level,MinAttack=info.MinAttack,MaxAttack=info.MaxAttack,MagicAttack=info.MaxAttack,Defence=info.Defence};
                            info.Owner=body;body.MaxHitpoints=body.Hitpoints=info.Hitpoints;body.ApplyMonsterBossHud();body.SendUpdates=true;
                            var life=Guid.NewGuid();if(!R.Bind(c,t,run.Day,life,body.UID,now))throw new InvalidOperationException("Beast spawn reservation changed.");
                            run.State="active";run.Life=life;run.Actor=body.UID;next=new Life{Map=map,Body=body,Info=info,Run=run};
                        }
                    }t.Commit();
                }}
                if(next!=null) {
                    active=next;bodies.Add(next.Body,next);map.AddEntity2(next.Body);map.EnsureMonsterTimer();
                    foreach(var p in Kernel.GamePool.Values)if(Q.Player(p,false) && p.Entity.MapID==1038 && Kernel.GetDistance(p.Entity.X,p.Entity.Y,next.Body.X,next.Body.Y)<Constants.nScreenDistance)next.Body.SendSpawn(p,false);
                    Kernel.SendWorldMessage(new Message("The Guild Beast prowls the Guild Grounds at "+next.Body.X+","+next.Body.Y+" until 22:00. Visit the Guild Director to join the hunt.",System.Drawing.Color.White,Message.Center),Server.GamePool);
                }
            }catch(Exception ex){Console.WriteLine("[guild-beast] tick_failure="+ex.GetType().Name);}finally{Monitor.Exit(gate);}
        }
        private static GameState Fighter(Life life,Entity killer) {
            if(killer==null || killer.Dead || killer.MapID!=1038)return null;var p=killer.Owner;
            if(!Q.Player(p) || !ReferenceEquals(p.Map,life.Map) || Kernel.GetDistance(killer.X,killer.Y,life.Body.X,life.Body.Y)>Constants.pScreenDistance)return null;
            if(ReferenceEquals(killer,p.Entity) && killer.EntityFlag==EntityFlag.Player && !killer.Companion)return p;
            Entity current;return killer.Companion && ReferenceEquals(p.Companion,killer) && life.Map.Companions.TryGetValue(killer.UID,out current) && ReferenceEquals(current,killer)?p:null;
        }
        internal static bool TryDie(Entity body,Entity killer) {
            Life life;if(body==null || !bodies.TryGetValue(body,out life))return false;
            var p=Fighter(life,killer);if(p==null)return true;uint uid=p.Entity.UID;
            if(!SessionInstall.TryEnter(uid))return true;
            bool owner=false,world=false,mob=false,health=false;
            try {
                if(!(owner=Monitor.TryEnter(p.ItemSyncRoot)) || !(world=Monitor.TryEnter(gate)) || !(mob=Monitor.TryEnter(body)) || !(health=Monitor.TryEnter(body.MonsterLifeSyncRoot)))return true;
                var now=Clock();if(!Owned(life) || body.Dead || Fighter(life,killer)!=p || now<life.Run.Start || now>=life.Run.End)return true;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    SkillTable.LockOwner(c,t,p);
                    if(!R.Defeat(c,t,life.Run.Day,life.Run.Life,body.UID,uid,now))return true;
                    uint soul=QuestSoulReward.Create().ID;
                    if(Q.Execute(c,t,@"UPDATE revora_guild_beast_days SET soul_type=@soul WHERE day=CAST(@day AS date)
AND state='killed' AND killer=@uid AND life=@life AND soul_type=0",
                        "soul",(long)soul,"day",life.Run.Day,"uid",(long)uid,"life",life.Run.Life)!=1)
                        throw new InvalidOperationException("Beast soul contract was not saved.");
                    EquipmentAchievements.RecordUnarmedVictory(c,t,p,body,killer,"guild-beast:"+life.Run.Life.ToString("N"));
                    if(!Owned(life) || Fighter(life,killer)!=p)throw new InvalidOperationException("Beast killer ownership changed.");t.Commit();
                }}
                // The committed result owns the death and reward entitlement.
                // Suppress generic random loot; delivery is the saved NPC claim.
                life.Ended=true;body.IsDropped=true;
                try{body.DieCore(killer);}finally{body.Hitpoints=0;Retire(life);}
                Q.Refresh(p);p.Send("The Guild Beast is slain. Beast Guard holds your Dragon Ball and bound P7 soul.");
            }catch(Exception ex){Q.Fail(p,"guild-beast-death",ex);}
            finally{if(health)Monitor.Exit(body.MonsterLifeSyncRoot);if(mob)Monitor.Exit(body);if(world)Monitor.Exit(gate);if(owner)Monitor.Exit(p.ItemSyncRoot);SessionInstall.Exit(uid);}
            return true;
        }
    }
}
