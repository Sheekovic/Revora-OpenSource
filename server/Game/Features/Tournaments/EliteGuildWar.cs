using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Revora.Game.Features;
using Revora.Client;
using Q=Revora.Game.Features.QuestInventory;
using Revora.Network.GamePackets;
using Revora.Game.ConquerStructures.Society;

namespace Revora.Game
{
    public unsafe class EliteGuildWar
    {
        public static SobNpcSpawn Poles;

        public static SafeDictionary<uint, Guild> Scores = new SafeDictionary<uint, Guild>(100);

        public static bool IsWar = false, Flame10th = false, FirstRound = false;

        public static Time32 ScoreSendStamp, LastWin;

        public static Guild PoleKeeper, CurrentTopLeader;

        private static bool changed = false;

        private static string[] scoreMessages;

        public static DateTime StartTime;
        private static readonly object Gate=new object();
        private static readonly Dictionary<uint,ulong> damageScores=new Dictionary<uint,ulong>();

        private static Guid battleId, pendingStartId;
        private static int captures, pendingCapture;
        private static DateTime nextCaptureRecovery;
        private static bool ledgerReady;
        internal static Func<DateTime> Clock = () => DateTime.UtcNow;
        private static DateTime scheduledUntil, pendingScheduledUntil, nextSchedulePulse;
        private static bool closingWarningSent;

        public static bool Claim
        {
            get { return Server.Vars["egwclaim"]; }
            set { Server.Vars["egwclaim"] = value; }
        }

        public static uint KeeperID
        {
            get { return Server.Vars["egwkeeperid"]; }
            set { Server.Vars["egwkeeperid"] = value; }
        }

        private static void InitializeLedger()
        {
            uint previousWinner = 0;
            using (var c = Database.DataHolder.MySqlConnection) { c.Open(); using (var t = c.BeginTransaction()) {
                Q.Execute(c, t, @"CREATE TABLE IF NOT EXISTS revora_elite_guild_battles(
                    id uuid PRIMARY KEY, started_at timestamptz NOT NULL, ended_at timestamptz,
                    state text NOT NULL CHECK(state IN('active','won','empty','cancelled')),
                    keeper bigint NOT NULL CHECK(keeper>=0), captures integer NOT NULL DEFAULT 0 CHECK(captures>=0),
                    gold bigint NOT NULL DEFAULT 0 CHECK(gold>=0), rvp bigint NOT NULL DEFAULT 0 CHECK(rvp BETWEEN 0 AND 4294967295),
                    soul_type integer NOT NULL DEFAULT 0, claimed_at timestamptz, claimed_by bigint, soul_uid bigint UNIQUE, honor_until timestamptz,
                    CHECK((state='won')=(soul_type BETWEEN 800000 AND 899999)),
                    CHECK(state='won' OR (gold=0 AND rvp=0 AND soul_type=0)),
                    CHECK(claimed_at IS NULL OR state='won'),
                    CHECK((claimed_at IS NULL)=(claimed_by IS NULL)), CHECK(claimed_by IS NULL OR claimed_by>0),
                    CHECK((claimed_at IS NULL)=(soul_uid IS NULL)), CHECK(soul_uid IS NULL OR soul_uid>0),
                    CHECK((claimed_at IS NULL)=(honor_until IS NULL)),
                    CHECK((state='active')=(ended_at IS NULL)), CHECK(state<>'won' OR keeper>0));
                ALTER TABLE revora_elite_guild_battles ADD COLUMN IF NOT EXISTS closes_at timestamptz;
                CREATE UNIQUE INDEX IF NOT EXISTS revora_elite_guild_active ON revora_elite_guild_battles((state)) WHERE state='active';
                CREATE TABLE IF NOT EXISTS revora_elite_guild_captures(
                    battle uuid NOT NULL REFERENCES revora_elite_guild_battles(id), number integer NOT NULL CHECK(number>0),
                    guild_id bigint NOT NULL CHECK(guild_id>0), damage bigint NOT NULL CHECK(damage>0),
                    captured_at timestamptz NOT NULL, PRIMARY KEY(battle,number));
                UPDATE revora_elite_guild_battles SET state='cancelled',ended_at=@now WHERE state='active';", "now", Clock());
                using(var q=Q.Command(c,t,"SELECT keeper FROM revora_elite_guild_battles WHERE state='won' ORDER BY ended_at DESC,id LIMIT 1")) {
                    var value=q.ExecuteScalar();if(value!=null)previousWinner=checked((uint)Convert.ToInt64(value));
                }
                t.Commit();
            }}
            Guild previous; PoleKeeper=previousWinner!=0 && Kernel.Guilds.TryGetValue(previousWinner,out previous)?previous:null;
            if(Poles!=null) { Poles.Name=PoleKeeper==null?"Guild Pole":PoleKeeper.Name; Poles.Hitpoints=Poles.MaxHitpoints; }
            battleId = pendingStartId = Guid.Empty; captures = pendingCapture = 0; nextCaptureRecovery = DateTime.MinValue; damageScores.Clear(); Scores.Clear(); scoreMessages=new string[0]; changed=false; IsWar = false; FirstRound = false; Claim = false; KeeperID = previousWinner; ledgerReady = true;
            scheduledUntil=pendingScheduledUntil=nextSchedulePulse=DateTime.MinValue;closingWarningSent=false;
        }
        private static bool SaveStart()
        {
            if (!ledgerReady) throw new InvalidOperationException("Elite Guild War ledger is unavailable.");
            if(pendingStartId==Guid.Empty)pendingStartId=Guid.NewGuid();
            bool scheduled=pendingScheduledUntil!=DateTime.MinValue;
            var id=pendingStartId;long keeper=scheduled || PoleKeeper==null?0:PoleKeeper.ID;
            DateTime started, until=DateTime.MinValue;
            using(var c=Database.DataHolder.MySqlConnection) { c.Open();
                Q.Execute(c,null,@"INSERT INTO revora_elite_guild_battles(id,started_at,state,keeper,closes_at)
SELECT @id,@now,'active',@keeper,@close WHERE @close::timestamptz IS NULL OR @now<@close
ON CONFLICT(id) DO NOTHING", "id",id,"now",Clock(),"keeper",keeper,
                    "close",scheduled?(object)pendingScheduledUntil:DBNull.Value);
                using(var q=Q.Command(c,null,"SELECT started_at,state,keeper,captures,closes_at FROM revora_elite_guild_battles WHERE id=@id", "id",id))
                using(var r=q.ExecuteReader()) {
                    if(!r.Read()) {
                        if(scheduled && Clock()>=pendingScheduledUntil) { pendingStartId=Guid.Empty;pendingScheduledUntil=DateTime.MinValue;return false; }
                        throw new InvalidOperationException("Elite Guild War start was not saved.");
                    }
                    if(scheduled && r.GetString(1)!="active") { pendingStartId=Guid.Empty;pendingScheduledUntil=DateTime.MinValue;return false; }
                    if(!r.IsDBNull(4))until=r.GetFieldValue<DateTimeOffset>(4).UtcDateTime;
                    if(r.GetString(1)!="active" || r.GetInt64(2)!=keeper || r.GetInt32(3)!=0
                        || until!=pendingScheduledUntil)throw new InvalidOperationException("Elite Guild War start ownership changed.");
                    started=r.GetFieldValue<DateTimeOffset>(0).UtcDateTime;
                }
            }
            battleId=id;pendingStartId=Guid.Empty;StartTime=started.ToLocalTime();scheduledUntil=until;pendingScheduledUntil=DateTime.MinValue;closingWarningSent=false;
            if(scheduled) { PoleKeeper=null;KeeperID=0;Poles.Name="Guild Pole"; }
            captures=pendingCapture=0;nextCaptureRecovery=DateTime.MinValue;damageScores.Clear();return true;
        }
        internal static bool IsOpeningTime(DateTime local)
        {
            return local.DayOfWeek!=DayOfWeek.Wednesday && local.DayOfWeek!=DayOfWeek.Saturday
                && local.Hour==21 && local.Minute==1 && local.Second>=5;
        }
        internal static Guid ScheduledIdentity(DateTime local)
        {
            using(var hash=System.Security.Cryptography.SHA256.Create())
                return new Guid(hash.ComputeHash(Encoding.UTF8.GetBytes("revora.elite-guild-war.v1:"+
                    local.ToString("yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture))).Take(16).ToArray());
        }
        internal static void Tick()
        {
            if(!ledgerReady || !Monitor.TryEnter(Gate))return;
            try {
                var now=Clock();if(now<nextSchedulePulse)return;nextSchedulePulse=now.AddSeconds(3);
                if(IsWar) {
                    if(scheduledUntil!=DateTime.MinValue && now>=scheduledUntil)EndCore();
                    else {
                        SendScoresCore();
                        if(scheduledUntil!=DateTime.MinValue && !closingWarningSent && now>=scheduledUntil.AddMinutes(-10)) {
                            closingWarningSent=true;
                            Kernel.SendWorldMessage(new Message("Ten minutes remain in Elite Guild War. Drive the enemy from the pole!",
                                System.Drawing.Color.White,Message.Center),Server.GamePool);
                        }
                    }
                    return;
                }
                if(pendingStartId==Guid.Empty) {
                    var local=now.ToLocalTime();if(!IsOpeningTime(local))return;
                    pendingStartId=ScheduledIdentity(local);pendingScheduledUntil=local.Date.AddHours(22).ToUniversalTime();
                }
                StartCore();
                if(IsWar && scheduledUntil!=DateTime.MinValue && Clock()>=scheduledUntil)EndCore();
            } finally {Monitor.Exit(Gate);}
        }
        private static void SaveCapture(Guild winner, ulong damage)
        {
            int number = checked(captures + 1);
            using (var c = Database.DataHolder.MySqlConnection) { c.Open(); using (var t = c.BeginTransaction()) {
                if (Q.Execute(c,t,"UPDATE revora_elite_guild_battles SET keeper=@guild,captures=@number WHERE id=@id AND state='active' AND captures=@before",
                    "guild",(long)winner.ID,"number",number,"id",battleId,"before",captures)!=1)
                    throw new InvalidOperationException("Elite Guild War capture ownership changed.");
                if (Q.Execute(c,t,"INSERT INTO revora_elite_guild_captures(battle,number,guild_id,damage,captured_at) VALUES(@id,@number,@guild,@damage,@now)",
                    "id",battleId,"number",number,"guild",(long)winner.ID,"damage",checked((long)damage),"now",Clock())!=1)
                    throw new InvalidOperationException("Elite Guild War capture was not saved.");
                var ids = new[]{winner.ID, PoleKeeper == null || FirstRound ? 0u : PoleKeeper.ID}.Where(id=>id!=0).Distinct().OrderBy(id=>id).ToArray();
                using(var q=Q.Command(c,t,"SELECT id FROM guilds WHERE id=ANY(@ids) ORDER BY id FOR UPDATE","ids",ids.Select(id=>(long)id).ToArray()))
                using(var r=q.ExecuteReader()) { int count=0; while(r.Read())count++; if(count!=ids.Length)throw new InvalidOperationException("Elite Guild War guild disappeared."); }
                if(PoleKeeper!=null && !FirstRound)
                    if(Q.Execute(c,t,"UPDATE guilds SET losts=CASE WHEN wins=0 THEN losts+1 ELSE losts END,wins=CASE WHEN wins>0 THEN wins-1 ELSE wins END WHERE id=@id","id",(long)PoleKeeper.ID)!=1)throw new InvalidOperationException("Elite Guild War defender statistics were not saved.");
                if(Q.Execute(c,t,"UPDATE guilds SET wins=CASE WHEN losts=0 THEN wins+1 ELSE wins END,losts=CASE WHEN losts>0 THEN losts-1 ELSE losts END WHERE id=@id","id",(long)winner.ID)!=1)throw new InvalidOperationException("Elite Guild War winner statistics were not saved.");
                if(Q.Execute(c,t,"UPDATE sobnpcs SET name=@name,life=@life WHERE id=88816 AND mapid=2071","name",winner.Name,"life",(long)Poles.MaxHitpoints)!=1)
                    throw new InvalidOperationException("Elite Guild War pole disappeared.");
                t.Commit();
            }}
            captures = number;
        }
        private static bool RecoverPendingCapture()
        {
            if(pendingCapture==0)return true;
            var now=Clock();
            if(now<nextCaptureRecovery)return false;
            nextCaptureRecovery=now.AddSeconds(1);
            try {
                uint keeper; int number; bool receipt;
                var statistics=new Dictionary<uint,Tuple<uint,uint>>();
                using(var c=Database.DataHolder.MySqlConnection) { c.Open(); using(var t=c.BeginTransaction()) {
                    using(var q=Q.Command(c,t,@"SELECT b.captures,b.keeper,c.guild_id IS NOT NULL FROM revora_elite_guild_battles b
LEFT JOIN revora_elite_guild_captures c ON c.battle=b.id AND c.number=b.captures AND c.guild_id=b.keeper
WHERE b.id=@id AND b.state='active' FOR UPDATE OF b","id",battleId))
                    using(var r=q.ExecuteReader()) {
                        if(!r.Read())throw new InvalidOperationException("Elite Guild War recovery result is unavailable.");
                        number=r.GetInt32(0);keeper=checked((uint)r.GetInt64(1));receipt=r.GetBoolean(2);
                    }
                    if(number==pendingCapture-1 && captures==number) {
                        // The failed transaction did not advance its battle counter.
                        t.Commit();pendingCapture=0;nextCaptureRecovery=DateTime.MinValue;return true;
                    }
                    if(number!=pendingCapture || !receipt)throw new InvalidOperationException("Elite Guild War recovery receipt does not match its battle.");
                    using(var q=Q.Command(c,t,@"SELECT id,wins,losts FROM guilds WHERE id=@keeper OR id IN
(SELECT guild_id FROM revora_elite_guild_captures WHERE battle=@id) ORDER BY id","keeper",(long)keeper,"id",battleId))
                    using(var r=q.ExecuteReader())while(r.Read())statistics.Add(checked((uint)Convert.ToInt64(r[0])),
                        Tuple.Create(checked((uint)Convert.ToInt64(r[1])),checked((uint)Convert.ToInt64(r[2]))));
                    t.Commit();
                }}
                Guild winner;
                if(!Kernel.Guilds.TryGetValue(keeper,out winner) || !statistics.ContainsKey(keeper))
                    throw new InvalidOperationException("Elite Guild War recovered guild is unavailable.");
                foreach(var pair in statistics){Guild guild;if(Kernel.Guilds.TryGetValue(pair.Key,out guild)){guild.Wins=pair.Value.Item1;guild.Losts=pair.Value.Item2;}}
                captures=number;PoleKeeper=winner;FirstRound=false;LastWin=Time32.Now;
                Poles.Name=winner.Name;Poles.Hitpoints=Poles.MaxHitpoints;damageScores.Clear();Scores.Clear();
                pendingCapture=0;nextCaptureRecovery=DateTime.MinValue;
                KeeperID=keeper;Reset();Kernel.SendWorldMessage(Poles,Server.GamePool,(ushort)2071);
                return true;
            }catch(Exception ex){Console.WriteLine("[elite-guild-war] capture_recovery_failure="+ex.GetType().Name);return false;}
        }
        public static void EliteGwint()
        {
            var Map = Kernel.Maps[2071];
            Poles = (SobNpcSpawn)Map.Npcs[88816];
            if(!ledgerReady)InitializeLedger();
            PlaceEnvoy();
        }

        internal static void PlaceEnvoy()
        {
            Map town;
            if(!Kernel.Maps.TryGetValue(1002,out town) || town.ID!=1002 || town.BaseID!=1002)
                throw new InvalidOperationException("Elite Guild envoy town is unavailable.");
            Map source=null;Revora.Interfaces.INpc actor=null;
            foreach(var map in Kernel.Maps.Values) {
                Revora.Interfaces.INpc found;if(!map.Npcs.TryGetValue(115522,out found))continue;
                if(actor!=null)throw new InvalidOperationException("Elite Guild envoy identity collision.");
                actor=found;source=map;
            }
            if(actor==null || actor.Type!=Enums.NpcType.Talker || (source.ID!=6000 && source.ID!=1002))
                throw new InvalidOperationException("Elite Guild envoy profile is unavailable.");
            if(source==town && actor.X==282 && actor.Y==161)return;
            if(!town.Floor[282,161,MapObjectType.Player] || town.Npcs.Values.Any(n=>n.UID!=115522 && Kernel.GetDistance(n.X,n.Y,282,161)<2))
                throw new InvalidOperationException("Elite Guild envoy destination is occupied.");
            Revora.Interfaces.INpc secondary;
            if(Map.SecoundNpc.TryGetValue(115522,out secondary) && !ReferenceEquals(secondary,actor))
                throw new InvalidOperationException("Elite Guild envoy secondary identity collision.");
            source.RemoveNpc(actor);Map.SecoundNpc.Remove(115522);
            actor.MapID=1002;actor.X=282;actor.Y=161;town.AddNpc(actor);
        }
        internal static bool AtEnvoy(GameState p)
        {
            Revora.Interfaces.INpc actor;
            return p!=null && p.Map!=null && p.Map.ID==1002 && p.Map.BaseID==1002 && Q.Near(p,115522,1002)
                && p.Map.Npcs.TryGetValue(115522,out actor) && actor.MapID==1002 && actor.X==282 && actor.Y==161 && actor.Type==Enums.NpcType.Talker;
        }

        public static void Start()
        {
            if(!Monitor.TryEnter(Gate))return;
            try{if(!IsWar)StartCore();}finally{Monitor.Exit(Gate);}
        }
        private static void StartCore()
        {
            if (Poles == null) return;
            if(!SaveStart())return;
            damageScores.Clear();
            Poles.Hitpoints=Poles.MaxHitpoints;
            Claim = false;
            Scores = new SafeDictionary<uint, Guild>(100);
            Kernel.SendWorldMessage(
                new Message("Elite Guild War has begun!", System.Drawing.Color.Red, Message.Center), Server.GamePool);
            FirstRound = true;
            IsWar = true;
        }

        private static void Reset()
        {
            Scores.Clear(); damageScores.Clear(); changed=true; scoreMessages=new string[0];
            Poles.Hitpoints=Poles.MaxHitpoints;
        }

        public static void FinishRound()
        {
            if(!Monitor.TryEnter(Gate))return;
            try{if(IsWar && RecoverPendingCapture() && Poles.Hitpoints==0)FinishRoundCore();}finally{Monitor.Exit(Gate);}
        }
        private static void FinishRoundCore()
        {
            Guild next;
            SortScores(out next);
            if (next == null) return;
            pendingCapture=checked(captures+1);nextCaptureRecovery=DateTime.MinValue;
            SaveCapture(next, damageScores[next.ID]);
            // Install the committed capture before sending any notifications.
            Poles.Hitpoints = Poles.MaxHitpoints;
            if (PoleKeeper != null && !FirstRound) {
                if (PoleKeeper.Wins == 0) PoleKeeper.Losts++;
                else PoleKeeper.Wins--;
            }
            if (next.Losts == 0) next.Wins++;
            else next.Losts--;
            LastWin = Time32.Now; FirstRound = false; PoleKeeper = next;
            Poles.Name = next.Name; damageScores.Clear(); Scores.Clear();
            pendingCapture=0;nextCaptureRecovery=DateTime.MinValue;
            KeeperID = next.ID;
            Reset();
            Kernel.SendWorldMessage(Poles, Server.GamePool, (ushort)2071);
            Kernel.SendWorldMessage(new Message("The Guild, " + next.Name + ", directed by " + next.LeaderName + " has won this round of Elite Guild War!",
                System.Drawing.Color.Red, Message.Center), Server.GamePool);
        }
        public static void End()
        {
            if(!Monitor.TryEnter(Gate))return;
            try{if(IsWar)EndCore();}finally{Monitor.Exit(Gate);}
        }
        private static void EndCore()
        {
            if(!RecoverPendingCapture())return;
            if(Poles.Hitpoints==0)FinishRoundCore();
            SaveResult();
            IsWar=false; Claim=PoleKeeper!=null;
            Kernel.SendWorldMessage(new Message(PoleKeeper==null?"Elite Guild War ended without a winner."
                :PoleKeeper.Name+" has won Elite Guild War! Its leader may collect the prize.",System.Drawing.Color.Red,Message.Center),Server.GamePool);
        }

        internal static bool HitPole(Entity attacker,SobNpcSpawn target,Attack packet,uint damage)
        {
            if(target==null || target.MapID!=2071 || target.UID!=88816)return false;
            if(!Monitor.TryEnter(Gate))return true;
            try{
                var p=attacker==null?null:attacker.Owner;Guild guild;Guild.Member member;
                if(!IsWar || (scheduledUntil!=DateTime.MinValue && Clock()>=scheduledUntil) || !ReferenceEquals(target,Poles) || !Q.Player(p) || !ReferenceEquals(p.Entity,attacker)
                    || attacker.MapID!=2071 || p.Map.ID!=2071 || p.Guild==null || attacker.GuildID!=p.Guild.ID
                    || !Kernel.Guilds.TryGetValue(p.Guild.ID,out guild) || !ReferenceEquals(guild,p.Guild)
                    || !guild.Members.TryGetValue(attacker.UID,out member) || !ReferenceEquals(member,p.AsMember) || member.GuildID!=guild.ID
                    || damage==0
                    || Kernel.GetDistance(attacker.X,attacker.Y,Poles.X,Poles.Y)>18)return true;
                if(!RecoverPendingCapture() || ReferenceEquals(PoleKeeper,guild))return true;
                if(Poles.Hitpoints==0){FinishRoundCore();return true;}
                uint accepted=Math.Min(damage,Poles.Hitpoints);ulong before;damageScores.TryGetValue(guild.ID,out before);
                damageScores[guild.ID]=checked(before+accepted);Scores[guild.ID]=guild;changed=true;
                Poles.Hitpoints-=accepted;
                if(Poles.Hitpoints==0){try{FinishRoundCore();}catch{RecoverPendingCapture();throw;}}
                if(packet!=null && packet.AttackType!=Attack.Magic)p.SendScreen(packet,true);
                Kernel.SendWorldMessage(Poles,Server.GamePool,(ushort)2071);
                return true;
            }finally{Monitor.Exit(Gate);}
        }
        public static void SendScores()
        {
            if(!Monitor.TryEnter(Gate))return;
            try {
                if(IsWar) {
                    if(!RecoverPendingCapture())return;
                    // A rolled-back lethal hit still owns its damage. The normal
                    // score pulse retries even when a zero-HP pole cannot be hit.
                    if(Poles.Hitpoints==0)FinishRoundCore();
                }
                SendScoresCore();
            }finally{Monitor.Exit(Gate);}
        }
        private static void SendScoresCore()
        {
            if (scoreMessages == null)
                scoreMessages = new string[0];
            if (Scores.Count == 0)
                return;
            if (changed)
                SortScores(out CurrentTopLeader);

            for (int c = 0; c < scoreMessages.Length; c++)
            {
                Message msg = new Message(scoreMessages[c], System.Drawing.Color.Red,
                    c == 0 ? Message.FirstRightCorner : Message.ContinueRightCorner);
                Kernel.SendWorldMessage(msg, Server.GamePool, (ushort)2071);
                Kernel.SendWorldMessage(msg, Server.GamePool, (ushort)6001);
            }
        }

        private static void SortScores(out Guild winner)
        {
            winner = null;
            List<string> ret = new List<string>();

            int Place = 0;
            foreach (Guild guild in Scores.Values.Where(p=>damageScores.ContainsKey(p.ID) && Kernel.Guilds.ContainsKey(p.ID) && ReferenceEquals(Kernel.Guilds[p.ID],p)).OrderByDescending(p=>damageScores[p.ID]).ThenBy(p=>p.ID))
            {
                if (Place == 0)
                    winner = guild;
                string str = "No  " + (Place + 1).ToString() + ": " + guild.Name + "(" + damageScores[guild.ID] + ")";
                ret.Add(str);
                Place++;
                if (Place == 4)
                    break;
            }

            changed = false;
            scoreMessages = ret.ToArray();
        }

        private static void SaveResult()
        {
            uint keeper=PoleKeeper==null?0:PoleKeeper.ID;
            using(var c=Database.DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                using(var q=Q.Command(c,t,"SELECT state,keeper,captures FROM revora_elite_guild_battles WHERE id=@id FOR UPDATE","id",battleId))
                using(var r=q.ExecuteReader()) {
                    if(!r.Read() || r.GetInt64(1)!=keeper || r.GetInt32(2)!=captures)throw new InvalidOperationException("Elite Guild result changed.");
                    string state=r.GetString(0);
                    if(state==(keeper==0?"empty":"won"))return; // A lost commit reply must not freeze a new prize.
                    if(state!="active")throw new InvalidOperationException("Elite Guild battle is no longer active.");
                }
                uint soul=keeper==0?0:QuestSoulReward.Create().ID;
                if(Q.Execute(c,t,@"UPDATE revora_elite_guild_battles SET state=@state,ended_at=@now,gold=@gold,rvp=@rvp,soul_type=@soul
WHERE id=@id AND state='active'","state",keeper==0?"empty":"won","now",Clock(),"gold",keeper==0?0L:500000000000L,
                    "rvp",keeper==0?0L:200000L,"soul",(int)soul,"id",battleId)!=1)throw new InvalidOperationException("Elite Guild result was not saved.");
                t.Commit();
            }}
        }

        internal static string CollectPrize(GameState p)
        {
            if(!Q.Player(p) || !SessionInstall.TryEnter(p.Entity.UID))return "Your prize will wait.";
            try {
                if(!Monitor.TryEnter(p.ItemSyncRoot))return "Your prize will wait.";
                try {
                    if(!Monitor.TryEnter(Gate))return "Your prize will wait.";
                    try {
                        if(!ledgerReady || IsWar || !AtEnvoy(p) || !GuildLeadership.Leader(p))return "The winning guild's leader must speak with me after the war.";
                        var guild=p.Guild;if(!Monitor.TryEnter(GuildLeadership.Gate(guild)))return "Your prize will wait.";
                        try{return CollectPrizeCore(p,guild);}finally{Monitor.Exit(GuildLeadership.Gate(guild));}
                    }finally{Monitor.Exit(Gate);}
                }finally{Monitor.Exit(p.ItemSyncRoot);}
            }catch(Exception ex){Q.Fail(p,"elite-guild-prize",ex);return "Your prize is saved. Reconnect before collecting it.";}
            finally{SessionInstall.Exit(p.Entity.UID);}
        }
        private static string CollectPrizeCore(GameState p,Guild guild)
        {
            if(!GuildLeadership.Leader(p) || !ReferenceEquals(p.Guild,guild))return "Only the winning guild's leader may collect.";
            Guid id;long gold,rvp;uint soulType;var now=Clock();var until=now.AddDays(1);ConquerItem soul;
            using(var c=Database.DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                // Match leadership transfer's entity-before-guild row lock order.
                using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid AND guildid=@guild AND guildrank=@rank AND name=@name FOR UPDATE",
                    "uid",(long)p.Entity.UID,"guild",(long)guild.ID,"rank",(int)Enums.GuildMemberRank.GuildLeader,"name",p.Entity.Name))
                    if(q.ExecuteScalar()==null)throw new InvalidOperationException("Elite Guild claimant changed.");
                using(var q=Q.Command(c,t,"SELECT leadername FROM guilds WHERE id=@guild FOR UPDATE","guild",(long)guild.ID))
                    if(!Equals(q.ExecuteScalar(),p.Entity.Name))throw new InvalidOperationException("Elite Guild leadership changed.");
                using(var q=Q.Command(c,t,@"SELECT id,gold,rvp,soul_type FROM revora_elite_guild_battles
WHERE keeper=@guild AND state='won' AND claimed_at IS NULL ORDER BY ended_at,id LIMIT 1 FOR UPDATE","guild",(long)guild.ID))
                using(var r=q.ExecuteReader()) {
                    if(!r.Read())return "Your guild has no unclaimed prize.";
                    id=r.GetGuid(0);gold=r.GetInt64(1);rvp=r.GetInt64(2);soulType=checked((uint)r.GetInt32(3));
                }
                Database.ConquerItemBaseInformation info;
                if(!Database.ConquerItemInformation.BaseInformations.TryGetValue(soulType,out info) || info.PurificationLevel!=7)
                    throw new InvalidOperationException("Elite Guild prize catalog changed.");
                if(p.Inventory.Count>=40)return "Make one place in your bag. Your prize is safe.";
                decimal nextGold=(decimal)p.Entity.Money+gold;long nextRvp=p.Entity.RevoraPoint+rvp;
                if(nextGold>Entity.MaximumSilver || nextRvp>uint.MaxValue)return "Make room in your purse. Your prize is safe.";
                SilverBalance.AssertSaved(c,t,p);
                using(var q=Q.Command(c,t,"SELECT count(*) FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0","uid",(long)p.Entity.UID))
                    if(Convert.ToInt64(q.ExecuteScalar())!=p.Inventory.Count)throw new InvalidOperationException("Elite Guild inventory requires reload.");
                if(Q.Execute(c,t,"UPDATE entities SET money=@gold,revorapoint=@rvp WHERE uid=@uid AND revorapoint=@before",
                    "gold",nextGold,"rvp",nextRvp,"uid",(long)p.Entity.UID,"before",(long)p.Entity.RevoraPoint)!=1)throw new InvalidOperationException("Elite Guild purse changed.");
                soul=Q.New(soulType,true);Q.Insert(c,t,p.Entity.UID,soul);
                long honor=checked((long)p.Entity.TopStatusToInt((ulong)FlagsUpdate.Flags.JQ_dunpaichuantou));
                long expiry=checked((long)Kernel.ToDateTimeInt(until.ToLocalTime()));
                using(var q=Q.Command(c,t,"SELECT MAX(time) FROM status WHERE entityid=@uid AND status=@honor","uid",(long)p.Entity.UID,"honor",honor)) {
                    var previous=q.ExecuteScalar();if(previous!=null && previous!=DBNull.Value && Convert.ToInt64(previous)>expiry)expiry=Convert.ToInt64(previous);
                }
                Q.Execute(c,t,"DELETE FROM status WHERE entityid=@uid AND status=@honor","uid",(long)p.Entity.UID,"honor",honor);
                if(Q.Execute(c,t,"INSERT INTO status(entityid,status,time) VALUES(@uid,@honor,@until)","uid",(long)p.Entity.UID,"honor",honor,"until",expiry)!=1)
                    throw new InvalidOperationException("Elite Guild effect was not saved.");
                until=Kernel.FromDateTimeInt(checked((ulong)expiry)).ToUniversalTime();
                if(Q.Execute(c,t,@"UPDATE revora_elite_guild_battles SET claimed_at=@now,claimed_by=@uid,soul_uid=@soul,honor_until=@until
WHERE id=@id AND state='won' AND keeper=@guild AND claimed_at IS NULL","now",now,"uid",(long)p.Entity.UID,"soul",(long)soul.UID,
                    "until",until,"id",id,"guild",(long)guild.ID)!=1)throw new InvalidOperationException("Elite Guild claim was not saved.");
                if(!AtEnvoy(p) || !GuildLeadership.Leader(p) || !ReferenceEquals(p.Guild,guild))throw new InvalidOperationException("Elite Guild session changed.");
                t.Commit();SilverBalance.ApplyCommitted(p,(ulong)nextGold);p.Entity.ApplyCommittedRevoraPoints((uint)nextRvp);
            }}
            Q.Install(p,new uint[0],new[]{soul});
            p.Entity.AddFlag((ulong)FlagsUpdate.Flags.JQ_dunpaichuantou,checked((int)Math.Min(int.MaxValue,(until-now).TotalSeconds)),false);
            Q.Refresh(p);return "Your guild's prize is yours: 500 billion silver, 200,000 RVPs and a bound P7 soul.";
        }
    }
}
