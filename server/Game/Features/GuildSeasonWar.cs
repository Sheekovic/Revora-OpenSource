using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Revora.Client;
using Revora.Database;
using Revora.Game.ConquerStructures.Society;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    // The weekly battlefield supplies evidence; loose legacy tokens never do.
    internal static class GuildSeasonWar
    {
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        private static readonly object Gate=new object();
        private static readonly DateTime Epoch=new DateTime(2026,9,20);
        internal static bool Enabled { get; private set; }
        private sealed class Battle {
            internal string Day;internal DateTime Starts,Until;internal uint Keeper;internal int Captures;
            internal readonly Dictionary<uint,ulong> Scores=new Dictionary<uint,ulong>();
            internal readonly Dictionary<Tuple<uint,uint>,ulong> Fighters=new Dictionary<Tuple<uint,uint>,ulong>();
        }
        private static Battle active;
        internal sealed class FlameAccess:IDisposable {
            private readonly Battle battle;
            private bool released;
            internal string Day {get{return battle.Day;}}
            internal DateTime Starts {get{return battle.Starts;}}
            internal DateTime Ends {get{return battle.Until;}}
            private FlameAccess(Battle value){battle=value;}
            internal bool Valid() {
                if(released || !Monitor.IsEntered(Gate) || !Enabled || !ReferenceEquals(active,battle) || !GuildWar.IsWar
                    || Clock()<battle.Starts || Clock()>=battle.Until)return false;
                var structures=Structures();
                return ReferenceEquals(structures[0],GuildWar.Pole) && ReferenceEquals(structures[1],GuildWar.LeftGate)
                    && ReferenceEquals(structures[2],GuildWar.RightGate);
            }
            public void Dispose(){if(!released){released=true;Monitor.Exit(Gate);}}
            internal static FlameAccess Acquire() {
                if(!Monitor.TryEnter(Gate))return null;
                try {
                    if(active==null){Monitor.Exit(Gate);return null;}
                    var access=new FlameAccess(active);
                    if(access.Valid())return access;
                    access.Dispose();return null;
                }catch{Monitor.Exit(Gate);throw;}
            }
        }
        // Acquire before opening a quest transaction, while holding only its
        // owner item lock. A busy battlefield defers admission instead of waiting.
        internal static FlameAccess AcquireFlame(){return FlameAccess.Acquire();}
        private static DateTime nextPoll;
        internal static bool Window(DateTime utc,out string day,out string season,out int round,out DateTime start,out DateTime end,out DateTime seasonEnd) {
            if(utc.Kind!=DateTimeKind.Utc)throw new ArgumentException("Guild season clock requires UTC.");
            var local=utc.ToLocalTime();var date=local.Date;
            day=season=null;round=0;start=end=seasonEnd=DateTime.MinValue;
            if(date<Epoch || date.DayOfWeek!=DayOfWeek.Sunday)return false;
            int week=(int)(date-Epoch).TotalDays/7;var first=Epoch.AddDays((week/5)*35);
            day=date.ToString("yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture);
            season=first.ToString("yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture);round=week%5+1;
            start=DateTime.SpecifyKind(date.AddHours(15),DateTimeKind.Local).ToUniversalTime();
            end=DateTime.SpecifyKind(date.AddHours(16),DateTimeKind.Local).ToUniversalTime();
            seasonEnd=DateTime.SpecifyKind(first.AddDays(35).AddHours(15),DateTimeKind.Local).ToUniversalTime();
            return utc>=start && utc<end;
        }
        private static SobNpcSpawn[] Structures() {
            Map map;if(!Kernel.Maps.TryGetValue(1038,out map) || map.ID!=1038 || map.BaseID!=1038)throw new InvalidOperationException("Guild battlefield unavailable.");
            var list=new List<SobNpcSpawn>();
            foreach(uint id in new uint[]{810,516074,516075}) {
                Revora.Interfaces.INpc npc;var body=map.Npcs.TryGetValue(id,out npc)?npc as SobNpcSpawn:null;
                if(body==null || body.MapID!=1038 || body.MaxHitpoints==0)throw new InvalidOperationException("Guild structure identity changed.");list.Add(body);
            }
            return list.ToArray();
        }
        internal static void Initialize() {
            lock(Gate) {
                if(Enabled)return;var bodies=Structures();
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    Q.Execute(c,t,@"CREATE TABLE IF NOT EXISTS revora_guild_seasons(
id varchar(10) PRIMARY KEY,ends_at timestamptz NOT NULL,timezone text NOT NULL);
CREATE TABLE IF NOT EXISTS revora_guild_battles(
day varchar(10) PRIMARY KEY,season varchar(10) NOT NULL REFERENCES revora_guild_seasons(id),round integer NOT NULL CHECK(round BETWEEN 1 AND 5),
starts_at timestamptz NOT NULL,ends_at timestamptz NOT NULL,state varchar(12) NOT NULL CHECK(state IN('active','won','empty','cancelled')),
keeper bigint NOT NULL DEFAULT 0,captures integer NOT NULL DEFAULT 0,UNIQUE(season,round));
CREATE TABLE IF NOT EXISTS revora_guild_captures(
day varchar(10) NOT NULL REFERENCES revora_guild_battles(day),number integer NOT NULL,guild_id bigint NOT NULL CHECK(guild_id>0),
damage bigint NOT NULL CHECK(damage>0),captured_at timestamptz NOT NULL,PRIMARY KEY(day,number));
CREATE TABLE IF NOT EXISTS revora_guild_contributors(
day varchar(10) NOT NULL,number integer NOT NULL,uid bigint NOT NULL CHECK(uid>0),guild_id bigint NOT NULL CHECK(guild_id>0),damage bigint NOT NULL CHECK(damage>0),
PRIMARY KEY(day,number,uid,guild_id),FOREIGN KEY(day,number) REFERENCES revora_guild_captures(day,number));
UPDATE revora_guild_battles SET state='cancelled' WHERE state='active';");t.Commit();
                }}
                GuildWarPurses.Initialize();
                GuildPoleCredit.Initialize();
                GuildWar.Pole=bodies[0];GuildWar.LeftGate=bodies[1];GuildWar.RightGate=bodies[2];
                active=null;GuildWar.IsWar=false;GuildWar.PoleKeeper=null;nextPoll=DateTime.MinValue;Enabled=true;
            }
        }
        private static void Broadcast(SobNpcSpawn body) {Kernel.SendWorldMessage(body,Server.GamePool,(ushort)1038);}
        private static void ResetField() {
            var bodies=Structures();foreach(var b in bodies) {
                b.Hitpoints=b.MaxHitpoints;if(b.UID==516074)b.Mesh=(ushort)(240+b.Mesh%10);else if(b.UID==516075)b.Mesh=(ushort)(270+b.Mesh%10);
                Broadcast(b);
            }
            GuildWar.Scores.Clear();foreach(var g in Kernel.Guilds.Values)g.WarScore=0;
        }
        internal static void Start() {
            if(!Enabled)return;
            lock(Gate)try {
                if(active!=null)return;string day,season;int round;DateTime start,end,seasonEnd;
                if(!Window(Clock(),out day,out season,out round,out start,out end,out seasonEnd))return;Structures();
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    Q.Execute(c,t,"INSERT INTO revora_guild_seasons(id,ends_at,timezone) VALUES(@id,@end,@zone) ON CONFLICT DO NOTHING",
                        "id",season,"end",seasonEnd,"zone",TimeZoneInfo.Local.Id);
                    using(var q=Q.Command(c,t,"SELECT timezone=@zone AND ends_at=@end FROM revora_guild_seasons WHERE id=@id FOR UPDATE",
                        "id",season,"zone",TimeZoneInfo.Local.Id,"end",seasonEnd))if(!Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("Guild season calendar changed.");
                    if(Q.Execute(c,t,"INSERT INTO revora_guild_battles(day,season,round,starts_at,ends_at,state) VALUES(@day,@season,@round,@start,@end,'active') ON CONFLICT DO NOTHING",
                        "day",day,"season",season,"round",round,"start",start,"end",end)!=1) {
                        // This single-world owner has no matching runtime battle. An
                        // uncertain earlier start must not become an invented result.
                        Q.Execute(c,t,"UPDATE revora_guild_battles SET state='cancelled' WHERE day=@day AND state='active'","day",day);t.Commit();return;
                    }t.Commit();
                }}
                active=new Battle{Day=day,Starts=start,Until=end};GuildWar.PoleKeeper=null;GuildWar.CurrentTopLeader=null;
                GuildWar.Pole.Name="Guild Pole";GuildWar.FirstRound=true;GuildWar.Flame10th=false;
                ResetField();GuildWar.StartTime=Clock().ToLocalTime();GuildWar.IsWar=true;
                Kernel.SendWorldMessage(new Message("Guild War has begun! Hold the pole when the battle ends.",System.Drawing.Color.Red,Message.Center),Server.GamePool);
                foreach(var p in Kernel.GamePool.Values)if(Q.Player(p) && !p.InQualifier() && p.Entity.PokerTable==0 && p.PlayRouletteUID==0
                    && p.Map.BaseID!=6001 && p.Map.BaseID!=6000 && p.Map.BaseID!=6004) {
                    p.Entity.StrResID=10515;p.Send(new AutoInvite{StrResID=10515,Countdown=60,Action=1}.ToArray());
                }
            }catch(Exception ex){Console.WriteLine("[guild-season] start_failure="+ex.GetType().Name);}
        }
        internal static bool Managed(SobNpcSpawn target) {return Enabled && target!=null && target.MapID==1038 && (target.UID==810 || target.UID==516074 || target.UID==516075);}
        private static bool Fighter(Entity attacker,SobNpcSpawn target) {
            var p=attacker==null?null:attacker.Owner;Guild guild;Guild.Member member;Revora.Interfaces.INpc current;
            return active!=null && GuildWar.IsWar && Clock()>=active.Starts && Clock()<active.Until && Q.Player(p) && ReferenceEquals(p.Entity,attacker)
                && attacker.MapID==1038 && p.Map.ID==1038 && p.Map.BaseID==1038 && p.Map.Npcs.TryGetValue(target.UID,out current) && ReferenceEquals(current,target)
                && p.Guild!=null && p.Guild.ID!=0 && attacker.GuildID==p.Guild.ID && Kernel.Guilds.TryGetValue(p.Guild.ID,out guild) && ReferenceEquals(guild,p.Guild)
                && guild.Members.TryGetValue(attacker.UID,out member) && ReferenceEquals(member,p.AsMember) && member.GuildID==guild.ID
                && target.Hitpoints>0 && Kernel.GetDistance(attacker.X,attacker.Y,target.X,target.Y)<=18
                && (target.UID!=810 || active.Keeper!=guild.ID);
        }
        internal static bool CanAttack(Entity attacker,SobNpcSpawn target) {lock(Gate)return Managed(target) && Fighter(attacker,target);}
        internal static bool TryUseBomb(ConquerItem item,GameState p){
            if(item==null || item.ID!=721261)return false;
            if(!Enabled || !Q.Player(p) || p.Entity.Companion || p.Entity.EntityFlag!=EntityFlag.Player || !SessionInstall.TryEnter(p.Entity.UID))return true;
            try{lock(p.ItemSyncRoot){
                if(!Q.Player(p) || p.Trade==null || p.Trade.InTrade || p.Booth!=null || item.Position!=0 || item.Warehouse!=0 || item.StackSize==0)return true;
                ConquerItem owned;if(!p.Inventory.TryGetItem(item.UID,out owned) || !ReferenceEquals(owned,item))return true;
                if(!Monitor.TryEnter(Gate))return true;
                try{
                    var target=new[]{GuildWar.LeftGate,GuildWar.RightGate}.Where(b=>b!=null && Managed(b) && Fighter(p.Entity,b)
                        && b.Mesh/10==(b.UID==516074?24:27)).OrderBy(b=>Kernel.GetDistance(p.Entity.X,p.Entity.Y,b.X,b.Y)).ThenBy(b=>b.UID).FirstOrDefault();
                    if(target==null)return true;
                    uint before=target.Hitpoints,damage=(uint)Math.Min((ulong)before,((ulong)target.MaxHitpoints+3)/4);
                    if(damage==0)return true;ushort stack=item.StackSize;
                    using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                        SkillTable.LockOwner(c,t,p);
                        using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid AND guildid=@guild","uid",(long)p.Entity.UID,"guild",(long)p.Guild.ID))
                            if(q.ExecuteScalar()==null)throw new InvalidOperationException("Siege bomb membership changed.");
                        using(var q=Q.Command(c,t,"SELECT state='active' AND starts_at<=@now AND ends_at>@now AND captures=@captures FROM revora_guild_battles WHERE day=@day FOR UPDATE",
                            "now",Clock(),"captures",active.Captures,"day",active.Day))
                            if(!Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("Siege bomb battle changed.");
                        string sql=stack==1?"DELETE FROM items WHERE uid=@item AND entityid=@uid AND id=721261 AND position=0 AND warehouse=0 AND stacksize=@stack AND bound=@bound"
                            :"UPDATE items SET stacksize=stacksize-1 WHERE uid=@item AND entityid=@uid AND id=721261 AND position=0 AND warehouse=0 AND stacksize=@stack AND bound=@bound";
                        if(Q.Execute(c,t,sql,"item",(long)item.UID,"uid",(long)p.Entity.UID,"stack",(int)stack,"bound",item.Bound?1:0)!=1)
                            throw new InvalidOperationException("Siege bomb ownership changed.");
                        ProgressionEvidence.Complete(c,t,p.Entity.UID,10231,"guild-siege-bomb-v1",active.Day+":"+active.Captures+":"+target.UID+":"+item.UID,1);
                        if(!Fighter(p.Entity,target) || target.Hitpoints!=before)throw new InvalidOperationException("Siege bomb target changed.");t.Commit();
                    }}
                    target.Hitpoints=before-damage;if(target.Hitpoints==0)target.Mesh=(ushort)((target.UID==516074?250:280)+target.Mesh%10);
                    if(stack==1)Q.Install(p,new[]{item.UID},new ConquerItem[0]);else{item.StackSize--;item.Mode=Enums.ItemMode.Update;item.Send(p);item.Mode=Enums.ItemMode.Default;}
                    Broadcast(target);p.Entity.Update(10,"bombarrow",target.UID,true);p.Entity.Update(10,"firemagic",target.UID,true);
                    p.Entity.Die(0);Q.Refresh(p);
                }catch(Exception ex){Q.Fail(p,"guild-siege-bomb",ex);}
                finally{Monitor.Exit(Gate);}
            }}finally{SessionInstall.Exit(p.Entity.UID);}
            return true;
        }
        private static void ChangeStats(Npgsql.NpgsqlConnection c,Npgsql.NpgsqlTransaction t,uint id,bool won) {
            uint wins,losses;
            using(var q=Q.Command(c,t,"SELECT wins,losts FROM guilds WHERE id=@id FOR UPDATE","id",(long)id))
            using(var r=q.ExecuteReader()){if(!r.Read()){if(won)throw new InvalidOperationException("Winning guild no longer exists.");return;}
                wins=checked((uint)Convert.ToInt64(r[0]));losses=checked((uint)Convert.ToInt64(r[1]));}
            if(won){if(losses>0)losses--;else wins=checked(wins+1);}else{if(wins>0)wins--;else losses=checked(losses+1);}
            if(Q.Execute(c,t,"UPDATE guilds SET wins=@wins,losts=@losses WHERE id=@id","wins",(long)wins,"losses",(long)losses,"id",(long)id)!=1)
                throw new InvalidOperationException("Guild round statistics not saved.");
        }
        private static void ReloadStats() {
            using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,@"SELECT g.id,g.wins,g.losts FROM guilds g
WHERE g.id IN(SELECT guild_id FROM revora_guild_captures WHERE day=@day)","day",active.Day))
            using(var r=q.ExecuteReader())while(r.Read()){Guild g;if(Kernel.Guilds.TryGetValue(checked((uint)Convert.ToInt64(r[0])),out g)) {
                g.Wins=checked((uint)Convert.ToInt64(r[1]));g.Losts=checked((uint)Convert.ToInt64(r[2]));
            }}}
        }
        internal static bool Hit(Entity attacker,SobNpcSpawn target,Attack packet,uint damage,SpellInformation spell) {
            if(!Managed(target))return false;
            var owner=attacker==null?null:attacker.Owner;if(owner==null || owner.Entity==null || owner.ItemSyncRoot==null)return true;
            uint ownerUid=owner.Entity.UID;if(!SessionInstall.TryEnter(ownerUid))return true;
            if(!Monitor.TryEnter(owner.ItemSyncRoot)){SessionInstall.Exit(ownerUid);return true;}
            bool gate=false,credit=false;int pending=0;
            try {
                if(!Monitor.TryEnter(Gate))return true;gate=true;
                if(!Fighter(attacker,target) || damage==0 || packet==null)return true;
                uint accepted=Math.Min(damage,target.Hitpoints);var p=attacker.Owner;
                if(target.UID==810) {
                    var scores=new Dictionary<uint,ulong>(active.Scores);ulong score;scores.TryGetValue(p.Guild.ID,out score);scores[p.Guild.ID]=score+accepted;
                    var fighters=new Dictionary<Tuple<uint,uint>,ulong>(active.Fighters);var key=Tuple.Create(attacker.UID,p.Guild.ID);
                    if(!fighters.ContainsKey(key) && fighters.Count>=4096)return true;
                    fighters.TryGetValue(key,out score);fighters[key]=score+accepted;
                    if(accepted==target.Hitpoints) {
                        var winner=scores.Where(pair=>Kernel.Guilds.ContainsKey(pair.Key)).OrderByDescending(pair=>pair.Value).ThenBy(pair=>pair.Key).First();
                        int number=active.Captures+1;
                        pending=number;
                        using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                            GuildPoleCredit.Record(c,t,p,active.Day,accepted,Clock());
                            if(Q.Execute(c,t,"UPDATE revora_guild_battles SET keeper=@guild,captures=@number WHERE day=@day AND state='active' AND captures=@before AND ends_at>@now",
                                "guild",(long)winner.Key,"number",number,"day",active.Day,"before",active.Captures,"now",Clock())!=1)throw new InvalidOperationException("Guild capture ownership changed.");
                            if(Q.Execute(c,t,"INSERT INTO revora_guild_captures(day,number,guild_id,damage,captured_at) VALUES(@day,@number,@guild,@damage,@now)",
                                "day",active.Day,"number",number,"guild",(long)winner.Key,"damage",checked((long)winner.Value),"now",Clock())!=1)throw new InvalidOperationException("Guild capture not saved.");
                            foreach(var f in fighters)if(Q.Execute(c,t,"INSERT INTO revora_guild_contributors(day,number,uid,guild_id,damage) VALUES(@day,@number,@uid,@guild,@damage)",
                                "day",active.Day,"number",number,"uid",(long)f.Key.Item1,"guild",(long)f.Key.Item2,"damage",checked((long)f.Value))!=1)throw new InvalidOperationException("Guild contribution not saved.");
                            if(active.Keeper!=0)ChangeStats(c,t,active.Keeper,false);ChangeStats(c,t,winner.Key,true);
                            t.Commit();
                        }}
                        active.Keeper=winner.Key;active.Captures=number;active.Scores.Clear();active.Fighters.Clear();
                        GuildWar.PoleKeeper=Kernel.Guilds[winner.Key];GuildWar.Pole.Name=GuildWar.PoleKeeper.Name;GuildWar.FirstRound=false;GuildWar.LastWin=Time32.Now;
                        ResetField();ReloadStats();Kernel.SendWorldMessage(new Message(GuildWar.PoleKeeper.Name+" has captured the guild pole!",System.Drawing.Color.Red,Message.Center),Server.GamePool);
                    }else {
                        if(!GuildPoleCredit.Known(attacker))using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                            GuildPoleCredit.Record(c,t,p,active.Day,accepted,Clock());t.Commit();}}
                        active.Scores.Clear();foreach(var s in scores)active.Scores.Add(s.Key,s.Value);
                        active.Fighters.Clear();foreach(var f in fighters)active.Fighters.Add(f.Key,f.Value);
                        p.Guild.WarScore=checked((uint)Math.Min(uint.MaxValue,scores[p.Guild.ID]));GuildWar.Scores[p.Guild.ID]=p.Guild;
                        target.Hitpoints-=accepted;Broadcast(target);
                    }
                    credit=!GuildPoleCredit.Known(attacker);
                }else {
                    target.Hitpoints-=accepted;if(target.Hitpoints==0)target.Mesh=(ushort)((target.UID==516074?250:280)+target.Mesh%10);Broadcast(target);
                }
                if(spell!=null)p.IncreaseSpellExperience(accepted,spell.ID);
                if(packet.AttackType!=Attack.Magic)p.SendScreen(packet,true);
                attacker.AttackPacket=packet;attacker.AttackStamp=Time32.Now;
            }catch(Exception ex){Console.WriteLine("[guild-season] hit_failure="+ex.GetType().Name);if(pending>0)RecoverCapture(pending);}
            finally{
                if(gate)Monitor.Exit(Gate);
                try{if(credit && Q.Player(owner,false))GuildPoleCredit.Project(owner);}catch(Exception ex){Q.Fail(owner,"guild-pole-projection",ex);}
                finally{Monitor.Exit(owner.ItemSyncRoot);SessionInstall.Exit(ownerUid);}
            }return true;
        }
        private static void RecoverCapture(int number) {
            // A commit may have reached PostgreSQL even if its acknowledgment did not.
            if(active==null)return;
            try {using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,@"SELECT c.guild_id FROM revora_guild_captures c
JOIN revora_guild_battles b ON b.day=c.day AND b.captures=c.number AND b.keeper=c.guild_id
WHERE b.day=@day AND b.state='active' AND c.number=@number","day",active.Day,"number",number)) {
                object value=q.ExecuteScalar();if(value==null)return;uint keeper=checked((uint)Convert.ToInt64(value));Guild guild;
                if(!Kernel.Guilds.TryGetValue(keeper,out guild))return;
                active.Keeper=keeper;active.Captures=number;active.Scores.Clear();active.Fighters.Clear();
                GuildWar.PoleKeeper=guild;GuildWar.Pole.Name=guild.Name;GuildWar.FirstRound=false;GuildWar.LastWin=Time32.Now;ResetField();ReloadStats();
            }}}catch(Exception ex){Console.WriteLine("[guild-season] capture_recovery_failure="+ex.GetType().Name);}
        }
        internal static void Finish() {
            bool announce=false;uint[] occupationRecipients=new uint[0];
            lock(Gate)try {
                if(active==null || Clock()<active.Until)return;
                Guild guild;uint winner=active.Captures>0 && Kernel.Guilds.TryGetValue(active.Keeper,out guild)?active.Keeper:0;
                var recipients=GuildWarPurses.Snapshot(winner);
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    GuildWarPurses.LockOwners(c,t,recipients);
                    string state;int captures;
                    using(var q=Q.Command(c,t,"SELECT state,keeper,captures FROM revora_guild_battles WHERE day=@day FOR UPDATE","day",active.Day))
                    using(var r=q.ExecuteReader()) {
                        if(!r.Read())throw new InvalidOperationException("Guild battle missing.");state=r.GetString(0);captures=r.GetInt32(2);
                        if(state=="won" || state=="empty")winner=checked((uint)r.GetInt64(1));
                    }
                    if(captures>active.Captures && state=="active") {
                        RecoverCapture(captures);
                        // Retry with a fresh winner and recipient snapshot, including when the old list was empty.
                        throw new InvalidOperationException("Guild capture recovered; recipient snapshot requires retry.");
                    }
                    if(captures!=active.Captures)throw new InvalidOperationException("Guild capture projection requires recovery.");
                    if(state=="active") {
                        if(Q.Execute(c,t,"UPDATE revora_guild_battles SET state=@state,keeper=@winner WHERE day=@day AND state='active' AND captures=@captures AND ends_at<=@now",
                            "state",winner==0?"empty":"won","winner",(long)winner,"day",active.Day,"captures",active.Captures,"now",Clock())!=1)throw new InvalidOperationException("Guild result not saved.");
                        GuildWarPurses.Freeze(c,t,active.Day,winner,active.Until,recipients);
                    }else if(state!="won" && state!="empty")throw new InvalidOperationException("Guild battle cancelled.");
                    t.Commit();
                }}
                GuildWar.PoleKeeper=winner!=0 && Kernel.Guilds.TryGetValue(winner,out guild)?guild:null;GuildWar.IsWar=false;active=null;announce=true;occupationRecipients=recipients.Select(r=>r.Uid).ToArray();
            }catch(Exception ex){Console.WriteLine("[guild-season] finish_failure="+ex.GetType().Name);}
            // Purse entitlements commit with the result; delivery runs outside this gate.
            // Never take inventory locks while holding the battlefield gate.
            if(announce) {
                foreach(uint uid in occupationRecipients) {
                    GameState p;if(Kernel.GamePool.TryGetValue(uid,out p) && Q.Player(p,false) && p.Entity.MyAchievement!=null)
                        p.Entity.MyAchievement.Invalidate();
                }
                GuildWar.EndLegacy(false);
            }
        }
        internal static void Tick() {
            if(!Enabled || Clock()<nextPoll)return;nextPoll=Clock().AddSeconds(3);Finish();Start();SendScores();
        }
        internal static void SendScores() {
            lock(Gate) {
                if(active==null)return;int place=0;
                foreach(var score in active.Scores.OrderByDescending(s=>s.Value).ThenBy(s=>s.Key).Take(4)) {
                    Guild g;if(!Kernel.Guilds.TryGetValue(score.Key,out g))continue;
                    Kernel.SendWorldMessage(new Message((++place)+". "+g.Name+" ("+score.Value+")",System.Drawing.Color.Red,place==1?Message.FirstRightCorner:Message.ContinueRightCorner),Server.GamePool,(ushort)1038);
                }
            }
        }
    }
}
