using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Runtime.CompilerServices;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class GuildContestCoordinator
    {
        internal const uint Registrar=706070,Exit=706071;
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        private static readonly object Gate=new object();
        private sealed class Contest {
            internal Guid Id;internal DateTime Start,End,NextScores;internal Map[] Maps;
            internal readonly List<Entity> Monsters=new List<Entity>();
        }
        private static Contest active;
        private static bool prepared,enabled;
        private static DateTime nextTick;
        private static MonsterInformation[] profiles;
        private sealed class PrizeOffer {internal Guid Run;internal uint Guild;internal DateTime Until;}
        private static readonly ConditionalWeakTable<GameState,PrizeOffer> offers=new ConditionalWeakTable<GameState,PrizeOffer>();
        internal static void Prepare() {
            if(Server.AuthServer!=null || Server.GameServer!=null || Kernel.GamePool.Count!=0)throw new InvalidOperationException("Guild contest preparation requires cold startup.");
            GuildContestTable.Initialize();GuildPkSchedule.Initialize();GuildPkBannerTable.Initialize();GuildPkDefeatTable.Initialize();GuildPkRewardTable.Initialize();GuildContestAdmission.Recover();GuildPkAdmission.Recover();
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,"UPDATE revora_guild_contests SET state='cancelled' WHERE state='open'");}
            GuildPkSchedule.Recover(Clock());
            prepared=true;
        }
        internal static void Activate() {
            lock(Gate) {
                if(enabled)return;if(!prepared)throw new InvalidOperationException("Guild contest recovery has not run.");
                if(!Kernel.Maps.ContainsKey(700))new Map(700,DMaps.MapPaths[700]);
                string[] names={"BanditL97","RedDevilL117","IcySerpent","IcyBladeDevil"};
                profiles=names.Select(name=>MonsterInformation.MonsterInformations.Values.Where(m=>m.Name==name && !m.Guard && !m.Reviver && m.Hitpoints>0).OrderBy(m=>m.ID).FirstOrDefault()).ToArray();
                if(profiles.Any(m=>m==null))throw new InvalidOperationException("Guild contest monster profiles are unavailable.");
                byte[] levels={97,117,120,130};
                for(int band=0;band<4;band++) {
                    uint family=950607u+(uint)band;if(MonsterInformation.MonsterInformations.ContainsKey(family))throw new InvalidOperationException("Guild contest family identity collision.");
                    profiles[band]=profiles[band].Copy();profiles[band].ID=family;profiles[band].Level=levels[band];profiles[band].Boss=false;
                }
                PlaceNpc(Kernel.Maps[1002],Registrar,409,288,"GuildContestEmissary");
                GuildContestAdmission.Clock=()=>Clock();GuildContestCombat.Clock=()=>Clock();GuildPkCoordinator.Activate();enabled=true;
            }
        }
        internal static bool Window(DateTime utc,out DateTime start,out DateTime end) {
            if(utc.Kind!=DateTimeKind.Utc)throw new ArgumentException("Guild contest clock requires UTC.");
            var local=utc.ToLocalTime();start=end=DateTime.MinValue;
            if(local.DayOfWeek!=DayOfWeek.Monday)return false;
            start=DateTime.SpecifyKind(local.Date.AddHours(20).AddMinutes(30),DateTimeKind.Local).ToUniversalTime();end=start.AddHours(2);
            return utc>=start.AddMinutes(-5) && utc<end;
        }
        internal static void PlaceNpc(Map map,uint id,ushort x,ushort y,string name) {
            Revora.Interfaces.INpc existing;
            if(map.Npcs.TryGetValue(id,out existing))throw new InvalidOperationException("Guild contest NPC identity collision.");
            for(int radius=0;radius<=8;radius++)for(int dx=-radius;dx<=radius;dx++)for(int dy=-radius;dy<=radius;dy++) {
                if(Math.Max(Math.Abs(dx),Math.Abs(dy))!=radius)continue;int nx=x+dx,ny=y+dy;
                if(nx<2 || ny<2 || nx>=map.Floor.Bounds.Width-2 || ny>=map.Floor.Bounds.Height-2 || !map.Floor[nx,ny,MapObjectType.Player]
                    || map.Npcs.Values.Any(n=>Kernel.GetDistance(n.X,n.Y,(ushort)nx,(ushort)ny)<3))continue;
                map.AddNpc(new NpcSpawn{UID=id,MapID=map.ID,X=(ushort)nx,Y=(ushort)ny,Mesh=50340,Type=Enums.NpcType.Talker,Name=name});return;
            }
            throw new InvalidOperationException("Guild contest NPC landing unavailable.");
        }
        private static void Open(DateTime start,DateTime end) {
            Guid id;using(var hash=SHA256.Create())id=new Guid(hash.ComputeHash(Encoding.UTF8.GetBytes("revora.guild-contest.v1:"+start.ToString("O"))).Take(16).ToArray());
            using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,"SELECT state FROM revora_guild_contests WHERE id=@id OR starts_at=@start","id",id,"start",start))if(q.ExecuteScalar()!=null)return;}
            var maps=new List<Map>();bool installed=false;
            try {
                var original=Kernel.Maps[700];
                for(int band=0;band<4;band++){var map=Map.CreateDynamic(original.ID,original.Path,false);maps.Add(map);PlaceNpc(map,Exit,50,50,"GuildContestEnvoy");}
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    Q.Execute(c,t,"INSERT INTO revora_guild_contests(id,starts_at,ends_at,state) VALUES(@id,@start,@end,'open')","id",id,"start",start,"end",end);
                    for(int band=0;band<4;band++)Q.Execute(c,t,"INSERT INTO revora_guild_contest_maps(run,band,map) VALUES(@run,@band,@map)","run",id,"band",band,"map",(long)maps[band].ID);
                    t.Commit();
                }}
                active=new Contest{Id=id,Start=start,End=end,Maps=maps.ToArray()};installed=true;
            }finally{if(!installed)foreach(var map in maps)map.Dispose();}
        }
        private static void Retire(Entity body) {
            lock(body.MonsterLifeSyncRoot) {
                Map map;Entity owned;if(!Kernel.Maps.TryGetValue(body.MapID,out map) || !map.Entities.TryGetValue(body.UID,out owned) || !ReferenceEquals(body,owned))return;
                body.MonsterInfo.SendScreen(new Data(true){UID=body.UID,ID=Data.RemoveEntity});map.RemoveEntity(body);
            }
        }
        private static void Populate(Contest contest) {
            foreach(var body in contest.Monsters.Where(m=>m.Dead).ToArray()){Retire(body);contest.Monsters.Remove(body);}
            for(int band=0;band<4;band++) {
                var map=contest.Maps[band];int missing=16-contest.Monsters.Count(m=>m.MapID==map.ID);
                for(int added=0;added<Math.Min(4,missing);added++) {
                    ushort x=0,y=0;
                    for(int yy=30;yy<75 && x==0;yy++)for(int xx=30;xx<75;xx++)
                        if(map.Floor[xx,yy,MapObjectType.Monster,null] && !map.Npcs.Values.Any(n=>Kernel.GetDistance(n.X,n.Y,(ushort)xx,(ushort)yy)<4)) {x=(ushort)xx;y=(ushort)yy;break;}
                    if(x==0)break;
                    var info=profiles[band].Copy();info.BoundX=x;info.BoundY=y;info.BoundCX=info.BoundCY=1;info.RespawnTime=int.MaxValue/2;
                    var body=new Entity(EntityFlag.Monster,false){UID=map.EntityUIDCounter.Next,MapID=map.ID,X=x,Y=y,MapObjType=MapObjectType.Monster,MonsterInfo=info,
                        Name=info.Name,Body=info.Mesh,Level=info.Level,MinAttack=info.MinAttack,MaxAttack=info.MaxAttack,MagicAttack=info.MaxAttack,Defence=info.Defence,IsDropped=true};
                    info.Owner=body;body.MaxHitpoints=body.Hitpoints=info.Hitpoints;
                    // Publish only while the life lock prevents a death racing registration.
                    lock(body.MonsterLifeSyncRoot) {
                        map.AddEntity2(body);
                        try {if(!GuildContestCombat.Register(contest.Id,body,band))throw new InvalidOperationException("Guild contest spawn registration refused.");}
                        catch {map.RemoveEntity(body);throw;}
                    }
                    contest.Monsters.Add(body);body.SendUpdates=true;
                    foreach(var p in Kernel.GamePool.Values)if(Q.Player(p,false) && ReferenceEquals(p.Map,map))body.SendSpawn(p,false);
                }
                map.EnsureMonsterTimer();
            }
        }
        private static bool Return(GameState p,Guid run) {
            if(!Q.Player(p,false) || !SessionInstall.TryEnter(p.Entity.UID))return false;bool held=false;
            try {
                if(!(held=Monitor.TryEnter(p.ItemSyncRoot)))return false;
                var previous=p.Map;GuildContestAdmission.Leave(p,run);
                if(!ReferenceEquals(previous,p.Map))p.Send(new Message("",System.Drawing.Color.Red,Message.FirstRightCorner));
                return true;
            }
            finally{if(held)Monitor.Exit(p.ItemSyncRoot);SessionInstall.Exit(p.Entity.UID);}
        }
        internal static void Tick() {
            if(enabled)GuildPkCoordinator.Tick();
            if(!enabled || !Monitor.TryEnter(Gate))return;
            try {
                var now=Clock();if(now<nextTick)return;nextTick=now.AddSeconds(2);
                DateTime start,end;if(active==null && Window(now,out start,out end) && now<start)Open(start,end);
                if(active==null)return;
                if(now>=active.End) {
                    GuildContestTable.Freeze(active.Id,now);
                    foreach(var body in active.Monsters.ToArray()){Retire(body);active.Monsters.Remove(body);}
                    foreach(var p in Kernel.GamePool.Values.Where(p=>p!=null && active.Maps.Any(m=>ReferenceEquals(p.Map,m))).Take(64).ToArray())Return(p,active.Id);
                    var pending=new List<uint>();
                    using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,"SELECT uid FROM revora_guild_contest_transfers WHERE run=@run AND state<>'returned' ORDER BY uid LIMIT 64","run",active.Id))using(var r=q.ExecuteReader())while(r.Read())pending.Add(checked((uint)r.GetInt64(0)));}
                    foreach(var uid in pending){GameState p;if(Kernel.GamePool.TryGetValue(uid,out p))Return(p,active.Id);else GuildContestAdmission.ReturnOffline(active.Id,uid);}
                    // Retain owned maps while online or disconnected entrants still need return.
                    using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,"SELECT count(*) FROM revora_guild_contest_transfers WHERE run=@run AND state<>'returned'","run",active.Id))if((long)q.ExecuteScalar()!=0)return;}
                    if(Kernel.GamePool.Values.Any(p=>p!=null && active.Maps.Any(m=>ReferenceEquals(p.Map,m))))return;
                    foreach(var map in active.Maps)map.Dispose();GuildPkSchedule.Create(active.Id);active=null;
                } else if(now>=active.Start) {
                    Populate(active);
                    if(now>=active.NextScores){SendScores(active,now);active.NextScores=now.AddSeconds(6);}
                }
            }catch(Exception ex){Console.WriteLine("[guild-contest] tick error="+ex.GetType().Name);}finally{Monitor.Exit(Gate);}
        }
        internal static string[] ScoreLines(Guid run,DateTime now,DateTime end) {
            var rows=GuildContestTable.Scores(run);
            var lines=new List<string>{"Guild Contest - "+Math.Max(0,(int)Math.Ceiling((end-now).TotalMinutes))+" min remaining"};
            for(int i=0;i<rows.Length;i++) {
                ConquerStructures.Society.Guild guild;
                string name=Kernel.Guilds.TryGetValue(rows[i].Guild,out guild)?guild.Name:"Guild "+rows[i].Guild;
                name=new string(name.Where(c=>char.IsLetterOrDigit(c) || c==' ' || c=='_' || c=='-').Take(24).ToArray());
                lines.Add((i+1)+". "+name+" - "+rows[i].Points+" points");
            }
            if(rows.Length==0)lines.Add("The hunt awaits its first victor.");
            return lines.ToArray();
        }
        private static void SendScores(Contest contest,DateTime now) {
            var players=Kernel.GamePool.Values.Where(p=>Q.Player(p,false) && contest.Maps.Any(m=>ReferenceEquals(p.Map,m))).ToArray();
            if(players.Length==0)return;
            var lines=ScoreLines(contest.Id,now,contest.End);
            for(int i=0;i<lines.Length;i++) {
                var message=new Message(lines[i],System.Drawing.Color.Red,i==0?Message.FirstRightCorner:Message.ContinueRightCorner);
                foreach(var p in players)if(Q.Player(p,false) && contest.Maps.Any(m=>ReferenceEquals(p.Map,m)))p.Send(message);
            }
        }
        private static bool Officer(GameState p){return OfficerAt(p,Registrar);}
        internal static bool OfficerAt(GameState p,uint registrar) {
            ConquerStructures.Society.Guild guild;ConquerStructures.Society.Guild.Member member;
            return Q.Near(p,registrar,1002) && p.Map.BaseID==1002 && p.Guild!=null && p.AsMember!=null
                && p.Entity.GuildID==p.Guild.ID && Kernel.Guilds.TryGetValue(p.Guild.ID,out guild) && ReferenceEquals(guild,p.Guild)
                && guild.Members.TryGetValue(p.Entity.UID,out member) && ReferenceEquals(member,p.AsMember) && member.GuildID==guild.ID
                && p.Entity.GuildRank==(ushort)member.Rank && (member.Rank==Enums.GuildMemberRank.GuildLeader || member.Rank==Enums.GuildMemberRank.DeputyLeader);
        }
        private static string ClaimPrize(GameState p) {
            PrizeOffer offer;
            if(!Officer(p) || !offers.TryGetValue(p,out offer) || offer.Run==Guid.Empty || offer.Guild!=p.Guild.ID || Clock()>=offer.Until)
                return "Ask me about your guild's prize first. Its leader or deputy must collect it.";
            if(!SessionInstall.TryEnter(p.Entity.UID))return "Return in a moment.";
            bool itemHeld=false,guildHeld=false;var guild=p.Guild;var gate=GuildLeadership.Gate(guild);
            try {
                if(!(itemHeld=Monitor.TryEnter(p.ItemSyncRoot)) || !(guildHeld=Monitor.TryEnter(gate)))return "Return in a moment.";
                if(!Officer(p) || !ReferenceEquals(p.Guild,guild))return "Your guild's leader or deputy must collect the prize.";
                int result;ulong funds;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    result=GuildContestTable.Prize(c,t,offer.Run,p.Entity.UID,guild.ID,Clock(),out funds);
                    if(result==0)return "No prize is owed to your guild for that hunt.";
                    if(!Officer(p) || !ReferenceEquals(p.Guild,guild))throw new InvalidOperationException("Guild prize owner changed.");
                    t.Commit();
                }}
                guild.SilverFund=funds;guild.SendGuild(p);
                return result==1?"Your guild's silver is safely in its treasury. May your banner fly high!":"Your guild has already received this prize.";
            }catch(Exception ex){Q.Fail(p,"guild-contest-prize",ex);return "Your claim needs attention. Reconnect before trying again.";}
            finally{if(guildHeld)Monitor.Exit(gate);if(itemHeld)Monitor.Exit(p.ItemSyncRoot);SessionInstall.Exit(p.Entity.UID);}
        }
        internal static bool HandleNpc(GameState p,NpcRequest request) {
            if(enabled && GuildPkCoordinator.HandleNpc(p,request))return true;
            if(!enabled || p==null || request==null || (p.ActiveNpc!=Registrar && p.ActiveNpc!=Exit))return false;
            if(request.NpcID!=p.ActiveNpc || !Q.Player(p) || !Monitor.TryEnter(Gate))return true;
            try {
                bool exit=p.ActiveNpc==Exit;
                if(!Q.Near(p,p.ActiveNpc,exit?p.Entity.MapID:(ushort)1002) || exit && (active==null || !active.Maps.Any(m=>ReferenceEquals(p.Map,m))))return true;
                if(request.OptionID==255)return true;
                var dialog=new Npcs(p);
                if(exit) {
                    if(request.OptionID==1) {if(Return(p,active.Id))return true;dialog.Text("Wait a moment, then ask me again.");}
                    else {dialog.Text("Your guild's hunt awaits. Are you leaving?");dialog.Option("Return to Twin City.",1);}
                } else {
                    bool open=active!=null && Clock()>=active.Start.AddMinutes(-5) && Clock()<active.Start;
                    if(request.OptionID==2)dialog.Text(ClaimPrize(p));
                    else if(request.OptionID==1) {
                        if(!open)dialog.Text("The gates are closed. Return on Monday between 20:25 and 20:30.");
                        else if(p.Guild==null)dialog.Text("Find a guild first. This hunt is fought in your guild's name.");
                        else if(p.Entity.Level>0 && GuildContestAdmission.Enter(p,active.Id,active.Maps[GuildPkRules.ContestBand(p.Entity.Level)]))return true;
                        else dialog.Text("You cannot enter just now. Settle your affairs and return before the gates close.");
                    } else {
                        dialog.Text("Guilds gather each Monday, from 20:25 to 20:30. Hunt for your guild's place among the final eight.");
                        if(open)dialog.Option("Let me join the hunt.",1);
                        if(Officer(p)) {
                            var run=GuildContestTable.Unclaimed(p.Guild.ID,Clock());
                            var offer=offers.GetOrCreateValue(p);offer.Run=run;offer.Guild=p.Guild.ID;offer.Until=Clock().AddMinutes(2);
                            if(run!=Guid.Empty)dialog.Option("Claim our oldest treasury prize.",2);
                        }
                    }
                }
                dialog.Option("Farewell.",255);dialog.Send();return true;
            }finally{Monitor.Exit(Gate);}
        }
    }
}
