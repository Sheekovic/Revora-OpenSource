// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Q = Revora.Game.Features.QuestInventory;
using Revora.Network.GamePackets;
using Revora.Game.ConquerStructures.Society;

namespace Revora.Game
{
    public unsafe class SuperGuildWar
    {
        public static SobNpcSpawn Pole, RightGate, LeftGate;
        public static SobNpcSpawn Poles;
        public static SafeDictionary<uint, Guild> Scores = new SafeDictionary<uint, Guild>(100);
        public static bool IsWar = false, FirstRound = false;
        public static Time32 ScoreSendStamp, LastWin;
        public static Guild PoleKeeper1, CurrentTopLeader;
        private static bool changed = false;
        private static string[] scoreMessages;
        public static DateTime StartTime;
        private static readonly object LifecycleGate = new object();
        private static Guid battleId, pendingStartId;
        private static int captures, pendingCapture;
        private static DateTime nextCaptureRecovery;
        private static bool ledgerReady;
        internal static Func<DateTime> Clock = () => DateTime.UtcNow;
        private static DateTime scheduledUntil, pendingScheduledUntil, nextSchedulePulse;
        private static readonly Dictionary<uint, ulong> roundScores = new Dictionary<uint, ulong>();

        private static void InitializeLedger()
        {
            uint previousWinner = 0;
            using (var c = Database.DataHolder.MySqlConnection) { c.Open(); using (var t = c.BeginTransaction()) {
                Q.Execute(c, t, @"CREATE TABLE IF NOT EXISTS revora_super_guild_battles(
                    id uuid PRIMARY KEY, started_at timestamptz NOT NULL, ended_at timestamptz,
                    state text NOT NULL CHECK(state IN('active','won','empty','cancelled')),
                    keeper bigint NOT NULL CHECK(keeper>=0), captures integer NOT NULL DEFAULT 0 CHECK(captures>=0),
                    CHECK((state='active')=(ended_at IS NULL)), CHECK(state<>'won' OR keeper>0));
                ALTER TABLE revora_super_guild_battles ADD COLUMN IF NOT EXISTS closes_at timestamptz;
                CREATE UNIQUE INDEX IF NOT EXISTS revora_super_guild_active ON revora_super_guild_battles((state)) WHERE state='active';
                CREATE TABLE IF NOT EXISTS revora_super_guild_captures(
                    battle uuid NOT NULL REFERENCES revora_super_guild_battles(id), number integer NOT NULL CHECK(number>0),
                    guild_id bigint NOT NULL CHECK(guild_id>0), damage bigint NOT NULL CHECK(damage>0),
                    captured_at timestamptz NOT NULL, PRIMARY KEY(battle,number));
                UPDATE revora_super_guild_battles SET state='cancelled',ended_at=@now WHERE state='active';", "now", Clock());
                using(var q=Q.Command(c,t,"SELECT keeper FROM revora_super_guild_battles WHERE state='won' ORDER BY ended_at DESC,id LIMIT 1")) {
                    var value=q.ExecuteScalar();if(value!=null)previousWinner=checked((uint)Convert.ToInt64(value));
                }
                t.Commit();
            }}
            Guild previous; PoleKeeper1=previousWinner!=0 && Kernel.Guilds.TryGetValue(previousWinner,out previous)?previous:null;
            if(Pole!=null) { Pole.Name=PoleKeeper1==null?"Guild Pole":PoleKeeper1.Name; Pole.Hitpoints=Pole.MaxHitpoints; }
            battleId = pendingStartId = Guid.Empty; captures = pendingCapture = 0; nextCaptureRecovery = DateTime.MinValue; roundScores.Clear(); IsWar = false; ledgerReady = true;
            scheduledUntil=pendingScheduledUntil=nextSchedulePulse=DateTime.MinValue;
        }
        private static bool SaveStart()
        {
            if (!ledgerReady) throw new InvalidOperationException("Super Guild War ledger is unavailable.");
            if(pendingStartId==Guid.Empty)pendingStartId=Guid.NewGuid();
            bool scheduled=pendingScheduledUntil!=DateTime.MinValue;
            var id=pendingStartId;long keeper=scheduled || PoleKeeper1==null?0:PoleKeeper1.ID;
            DateTime started, until=DateTime.MinValue;
            using(var c=Database.DataHolder.MySqlConnection) { c.Open();
                Q.Execute(c,null,@"INSERT INTO revora_super_guild_battles(id,started_at,state,keeper,closes_at)
SELECT @id,@now,'active',@keeper,@close WHERE @close::timestamptz IS NULL OR @now<@close
ON CONFLICT(id) DO NOTHING", "id",id,"now",Clock(),"keeper",keeper,
                    "close",scheduled?(object)pendingScheduledUntil:DBNull.Value);
                using(var q=Q.Command(c,null,"SELECT started_at,state,keeper,captures,purses_frozen,closes_at FROM revora_super_guild_battles WHERE id=@id", "id",id))
                using(var r=q.ExecuteReader()) {
                    if(!r.Read()) {
                        if(scheduled && Clock()>=pendingScheduledUntil) { pendingStartId=Guid.Empty;pendingScheduledUntil=DateTime.MinValue;return false; }
                        throw new InvalidOperationException("Super Guild War start was not saved.");
                    }
                    if(scheduled && r.GetString(1)!="active") { pendingStartId=Guid.Empty;pendingScheduledUntil=DateTime.MinValue;return false; }
                    if(!r.IsDBNull(5))until=r.GetFieldValue<DateTimeOffset>(5).UtcDateTime;
                    if(r.GetString(1)!="active" || r.GetInt64(2)!=keeper || r.GetInt32(3)!=0 || r.GetBoolean(4)
                        || until!=pendingScheduledUntil)throw new InvalidOperationException("Super Guild War start ownership changed.");
                    started=r.GetFieldValue<DateTimeOffset>(0).UtcDateTime;
                }
            }
            battleId=id;pendingStartId=Guid.Empty;StartTime=started.ToLocalTime();scheduledUntil=until;pendingScheduledUntil=DateTime.MinValue;
            if(scheduled) { PoleKeeper1=null;KeeperID=0;Pole.Name="Guild Pole"; }
            captures=pendingCapture=0;nextCaptureRecovery=DateTime.MinValue;roundScores.Clear();return true;
        }
        internal static bool IsOpeningTime(DateTime local)
        {
            return local.DayOfWeek==DayOfWeek.Wednesday && local.Hour==20 && local.Minute==0 && local.Second>=1;
        }
        internal static Guid ScheduledIdentity(DateTime local)
        {
            using(var hash=System.Security.Cryptography.SHA256.Create())
                return new Guid(hash.ComputeHash(Encoding.UTF8.GetBytes("revora.super-guild-war.v1:"+
                    local.ToString("yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture))).Take(16).ToArray());
        }
        internal static void Tick()
        {
            if(!ledgerReady || !Monitor.TryEnter(LifecycleGate))return;
            try {
                var now=Clock();if(now<nextSchedulePulse)return;nextSchedulePulse=now.AddSeconds(3);
                if(IsWar) {
                    if(scheduledUntil!=DateTime.MinValue && now>=scheduledUntil)EndCore();
                    else SendScoresCore();
                    return;
                }
                if(pendingStartId==Guid.Empty) {
                    var local=now.ToLocalTime();if(!IsOpeningTime(local))return;
                    pendingStartId=ScheduledIdentity(local);pendingScheduledUntil=local.Date.AddHours(21).ToUniversalTime();
                }
                StartCore();
                if(IsWar && scheduledUntil!=DateTime.MinValue && Clock()>=scheduledUntil)EndCore();
            } finally {Monitor.Exit(LifecycleGate);}
        }
        private static void SaveCapture(Guild winner, ulong damage)
        {
            int number = checked(captures + 1);
            using (var c = Database.DataHolder.MySqlConnection) { c.Open(); using (var t = c.BeginTransaction()) {
                if (Q.Execute(c,t,"UPDATE revora_super_guild_battles SET keeper=@guild,captures=@number WHERE id=@id AND state='active' AND captures=@before",
                    "guild",(long)winner.ID,"number",number,"id",battleId,"before",captures)!=1)
                    throw new InvalidOperationException("Super Guild War capture ownership changed.");
                if (Q.Execute(c,t,"INSERT INTO revora_super_guild_captures(battle,number,guild_id,damage,captured_at) VALUES(@id,@number,@guild,@damage,@now)",
                    "id",battleId,"number",number,"guild",(long)winner.ID,"damage",checked((long)damage),"now",Clock())!=1)
                    throw new InvalidOperationException("Super Guild War capture was not saved.");
                var ids = new[]{winner.ID, PoleKeeper1 == null || FirstRound ? 0u : PoleKeeper1.ID}.Where(id=>id!=0).Distinct().OrderBy(id=>id).ToArray();
                using(var q=Q.Command(c,t,"SELECT id FROM guilds WHERE id=ANY(@ids) ORDER BY id FOR UPDATE","ids",ids.Select(id=>(long)id).ToArray()))
                using(var r=q.ExecuteReader()) { int count=0; while(r.Read())count++; if(count!=ids.Length)throw new InvalidOperationException("Super Guild War guild disappeared."); }
                if(PoleKeeper1!=null && !FirstRound)
                    if(Q.Execute(c,t,"UPDATE guilds SET losts=CASE WHEN wins=0 THEN losts+1 ELSE losts END,wins=CASE WHEN wins>0 THEN wins-1 ELSE wins END WHERE id=@id","id",(long)PoleKeeper1.ID)!=1)throw new InvalidOperationException("Super Guild War defender statistics were not saved.");
                if(Q.Execute(c,t,"UPDATE guilds SET wins=CASE WHEN losts=0 THEN wins+1 ELSE wins END,losts=CASE WHEN losts>0 THEN losts-1 ELSE losts END WHERE id=@id","id",(long)winner.ID)!=1)throw new InvalidOperationException("Super Guild War winner statistics were not saved.");
                if(Q.Execute(c,t,"UPDATE sobnpcs SET name=@name,life=@life WHERE id=811 AND mapid=1234","name",winner.Name,"life",(long)Pole.MaxHitpoints)!=1)
                    throw new InvalidOperationException("Super Guild War pole disappeared.");
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
                    using(var q=Q.Command(c,t,@"SELECT b.captures,b.keeper,c.guild_id IS NOT NULL FROM revora_super_guild_battles b
LEFT JOIN revora_super_guild_captures c ON c.battle=b.id AND c.number=b.captures AND c.guild_id=b.keeper
WHERE b.id=@id AND b.state='active' FOR UPDATE OF b","id",battleId))
                    using(var r=q.ExecuteReader()) {
                        if(!r.Read())throw new InvalidOperationException("Super Guild War recovery result is unavailable.");
                        number=r.GetInt32(0);keeper=checked((uint)r.GetInt64(1));receipt=r.GetBoolean(2);
                    }
                    if(number==pendingCapture-1 && captures==number) {
                        // The failed transaction did not advance its battle counter.
                        t.Commit();pendingCapture=0;nextCaptureRecovery=DateTime.MinValue;return true;
                    }
                    if(number!=pendingCapture || !receipt)throw new InvalidOperationException("Super Guild War recovery receipt does not match its battle.");
                    using(var q=Q.Command(c,t,@"SELECT id,wins,losts FROM guilds WHERE id=@keeper OR id IN
(SELECT guild_id FROM revora_super_guild_captures WHERE battle=@id) ORDER BY id","keeper",(long)keeper,"id",battleId))
                    using(var r=q.ExecuteReader())while(r.Read())statistics.Add(checked((uint)Convert.ToInt64(r[0])),
                        Tuple.Create(checked((uint)Convert.ToInt64(r[1])),checked((uint)Convert.ToInt64(r[2]))));
                    t.Commit();
                }}
                Guild winner;
                if(!Kernel.Guilds.TryGetValue(keeper,out winner) || !statistics.ContainsKey(keeper))
                    throw new InvalidOperationException("Super Guild War recovered guild is unavailable.");
                foreach(var pair in statistics){Guild guild;if(Kernel.Guilds.TryGetValue(pair.Key,out guild)){guild.Wins=pair.Value.Item1;guild.Losts=pair.Value.Item2;}}
                captures=number;PoleKeeper1=winner;FirstRound=false;LastWin=Time32.Now;
                Pole.Name=winner.Name;Pole.Hitpoints=Pole.MaxHitpoints;roundScores.Clear();Scores.Clear();
                pendingCapture=0;nextCaptureRecovery=DateTime.MinValue;
                KeeperID=keeper;Reset();Kernel.SendWorldMessage(Pole,Server.GamePool,(ushort)1234);
                return true;
            }catch(Exception ex){Console.WriteLine("[super-guild-war] capture_recovery_failure="+ex.GetType().Name);return false;}
        }
        private static void SaveResult()
        {
            uint winner=PoleKeeper1==null?0:PoleKeeper1.ID;
            var recipients=Features.SuperGuildWarPurses.Snapshot(winner);
            var now=Clock();
            using (var c = Database.DataHolder.MySqlConnection) { c.Open(); using(var t=c.BeginTransaction()) {
                Features.SuperGuildWarPurses.LockOwners(c,t,recipients);
                int changed=Q.Execute(c,t,"UPDATE revora_super_guild_battles SET state=@state,ended_at=@now WHERE id=@id AND state='active' AND NOT purses_frozen AND captures=@captures AND keeper=@keeper",
                    "state",winner==0?"empty":"won","now",now,"id",battleId,"captures",captures,"keeper",(long)winner);
                if(changed==1)Features.SuperGuildWarPurses.Freeze(c,t,battleId,winner,now,recipients);
                else using(var q=Q.Command(c,t,"SELECT count(*) FROM revora_super_guild_battles WHERE id=@id AND state=@state AND purses_frozen AND captures=@captures AND keeper=@keeper",
                    "id",battleId,"state",winner==0?"empty":"won","captures",captures,"keeper",(long)winner))
                    if(Convert.ToInt64(q.ExecuteScalar())!=1)throw new InvalidOperationException("Super Guild War result ownership changed.");
                t.Commit();
            }}
        }
        public static bool Claim
        {
            get { return Server.Vars["gwclaim"]; }
            set { Server.Vars["gwclaim"] = value; }
        }
        public static uint KeeperID
        {
            get { return Server.Vars["gwkeeperid"]; }
            set { Server.Vars["gwkeeperid"] = value; }
        }
        public static void Initiate()
        {
            var Map = Kernel.Maps[1234];
            Pole = (SobNpcSpawn)Map.Npcs[811];
            LeftGate = (SobNpcSpawn)Map.Npcs[516076];
            RightGate = (SobNpcSpawn)Map.Npcs[516077];
            if (!ledgerReady) InitializeLedger();
            Features.SuperGuildWarPurses.Initialize();
        }
        public static void Start()
        {
            if (!Monitor.TryEnter(LifecycleGate)) return;
            try { if (!IsWar) StartCore(); }
            finally { Monitor.Exit(LifecycleGate); }
        }
        private static void StartCore()
        {
            if (LeftGate == null || RightGate == null || Pole == null) return;
            if(!SaveStart())return;
            IsWar = true;
            Pole.Hitpoints = Pole.MaxHitpoints;
            LeftGate.Hitpoints = LeftGate.MaxHitpoints;
            RightGate.Hitpoints = RightGate.MaxHitpoints;
            Scores = new SafeDictionary<uint, Guild>(100);
            LeftGate.Mesh = (ushort)(240 + LeftGate.Mesh % 10);
            RightGate.Mesh = (ushort)(270 + LeftGate.Mesh % 10);
            Kernel.SendWorldMessage(new Message("super Guild War!", System.Drawing.Color.Red, Message.Center), Server.GamePool);
            FirstRound = true;
            FlagsUpdate upd = new FlagsUpdate(true);
            upd.UID = LeftGate.UID;
            upd.Append((byte)FlagsUpdate.DataType.Mesh, LeftGate.Mesh);
            upd.Append((byte)FlagsUpdate.DataType.Hitpoints, LeftGate.Hitpoints);
            Kernel.SendWorldMessage(upd, Server.GamePool, (ushort)1234);
            upd.Clear();
            upd.UID = RightGate.UID;
            upd.Append((byte)FlagsUpdate.DataType.Mesh, RightGate.Mesh);
            upd.Append((byte)FlagsUpdate.DataType.Hitpoints, RightGate.Hitpoints);
            Kernel.SendWorldMessage(upd, Server.GamePool, (ushort)1234);
            Claim = false;
            IsWar = true;
        }
        public static void Reset()
        {
            Scores = new SafeDictionary<uint, Guild>(100);

            LeftGate.Mesh = (ushort)(240 + LeftGate.Mesh % 10);
            RightGate.Mesh = (ushort)(270 + LeftGate.Mesh % 10);

            LeftGate.Hitpoints = LeftGate.MaxHitpoints;
            RightGate.Hitpoints = RightGate.MaxHitpoints;
            Pole.Hitpoints = Pole.MaxHitpoints;

            FlagsUpdate upd = new FlagsUpdate(true);
            upd.UID = LeftGate.UID;
            upd.Append((byte)FlagsUpdate.DataType.Mesh, LeftGate.Mesh);
            upd.Append((byte)FlagsUpdate.DataType.Hitpoints, LeftGate.Hitpoints);
            Kernel.SendWorldMessage(upd, Server.GamePool, (ushort)1234);
            upd.Clear();
            upd.UID = RightGate.UID;
            upd.Append((byte)FlagsUpdate.DataType.Mesh, RightGate.Mesh);
            upd.Append((byte)FlagsUpdate.DataType.Hitpoints, RightGate.Hitpoints);
            Kernel.SendWorldMessage(upd, Server.GamePool, (ushort)1234);

            IsWar = true;
        }
        public static void FinishRound()
        {
            if (!Monitor.TryEnter(LifecycleGate)) return;
            try { if (IsWar && RecoverPendingCapture() && Pole.Hitpoints == 0) FinishRoundCore(); }
            finally { Monitor.Exit(LifecycleGate); }
        }
        private static void FinishRoundCore()
        {
            Guild next;
            SortScores(out next);
            if (next == null) return;
            pendingCapture=checked(captures+1);nextCaptureRecovery=DateTime.MinValue;
            SaveCapture(next, roundScores[next.ID]);
            // Install the committed capture before sending any notifications.
            Pole.Hitpoints = Pole.MaxHitpoints;
            if (PoleKeeper1 != null && !FirstRound) {
                if (PoleKeeper1.Wins == 0) PoleKeeper1.Losts++;
                else PoleKeeper1.Wins--;
            }
            if (next.Losts == 0) next.Wins++;
            else next.Losts--;
            LastWin = Time32.Now; FirstRound = false; PoleKeeper1 = next;
            Pole.Name = next.Name; roundScores.Clear(); Scores.Clear();
            pendingCapture=0;nextCaptureRecovery=DateTime.MinValue;
            KeeperID = next.ID;
            Reset();
            Kernel.SendWorldMessage(Pole, Server.GamePool, (ushort)1234);
            Kernel.SendWorldMessage(new Message("The Guild, " + next.Name + ", directed by " + next.LeaderName + " has won this round in the war!",
                System.Drawing.Color.Red, Message.Center), Server.GamePool);
        }
        public static void End()
        {
            if (!Monitor.TryEnter(LifecycleGate)) return;
            try { if (IsWar) EndCore(); }
            finally { Monitor.Exit(LifecycleGate); }
        }
        private static void EndCore()
        {
            if(!RecoverPendingCapture())return;
            SaveResult();
            IsWar = false;
            Claim = true;
            string message=PoleKeeper1==null?"Super Guild War has ended without a winner."
                :PoleKeeper1.Name+" has won Super Guild War! The victors' purses await them.";
            Kernel.SendWorldMessage(new Message(message,System.Drawing.Color.Red,Message.Center),Server.GamePool);
        }
        internal static bool HitPole(Entity attacker, SobNpcSpawn target, Attack packet, uint damage)
        {
            if (target == null || target.MapID != 1234 || target.UID != 811) return false;
            if (!Monitor.TryEnter(LifecycleGate)) return true;
            try {
                var p = attacker == null ? null : attacker.Owner;
                Guild guild; Guild.Member member;
                if (!IsWar || (scheduledUntil!=DateTime.MinValue && Clock()>=scheduledUntil) || !ReferenceEquals(target, Pole) || !Q.Player(p) || !ReferenceEquals(p.Entity, attacker)
                    || attacker.MapID != 1234 || p.Map.ID != 1234 || p.Guild == null || p.Guild.ID == 0
                    || attacker.GuildID != p.Guild.ID || !Kernel.Guilds.TryGetValue(p.Guild.ID, out guild) || !ReferenceEquals(guild,p.Guild)
                    || !guild.Members.TryGetValue(attacker.UID,out member) || !ReferenceEquals(member,p.AsMember) || member.GuildID != guild.ID
                    || damage == 0 || Kernel.GetDistance(attacker.X,attacker.Y,Pole.X,Pole.Y)>18) return true;
                if(!RecoverPendingCapture() || ReferenceEquals(PoleKeeper1,guild) || Pole.Hitpoints==0)return true;
                uint before = Pole.Hitpoints, accepted = Math.Min(before,damage);
                ulong old; roundScores.TryGetValue(guild.ID,out old);
                roundScores[guild.ID] = checked(old + accepted);
                Scores[guild.ID] = guild; changed = true;
                Pole.Hitpoints -= accepted;
                try { if (Pole.Hitpoints == 0) FinishRoundCore(); }
                catch {
                    // Restore the uncommitted hit, then consult the receipt in case
                    // PostgreSQL committed but its acknowledgment never reached us.
                    if (Pole.Hitpoints == 0) { Pole.Hitpoints = before; roundScores[guild.ID] = old; changed=true; }
                    RecoverPendingCapture();
                    throw;
                }
                if (packet != null && packet.AttackType != Attack.Magic) p.SendScreen(packet,true);
                Kernel.SendWorldMessage(Pole,Server.GamePool,(ushort)1234);
                return true;
            } finally { Monitor.Exit(LifecycleGate); }
        }
        public static void SendScores()
        {
            if (!Monitor.TryEnter(LifecycleGate)) return;
            try { if(RecoverPendingCapture())SendScoresCore(); }
            finally { Monitor.Exit(LifecycleGate); }
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
                Message msg = new Message(scoreMessages[c], System.Drawing.Color.Red, c == 0 ? Message.FirstRightCorner : Message.ContinueRightCorner);
                Kernel.SendWorldMessage(msg, Server.GamePool, (ushort)1234);
                Kernel.SendWorldMessage(msg, Server.GamePool, (ushort)6001);
            }
        }
        private static bool CurrentGuild(Guild guild)
        {
            Guild current;
            return guild != null && Kernel.Guilds.TryGetValue(guild.ID,out current) && ReferenceEquals(guild,current);
        }
        private static void SortScores(out Guild winner)
        {
            winner = null;
            List<string> ret = new List<string>();

            int Place = 0;
            foreach (Guild guild in Scores.Values.Where(p => CurrentGuild(p) && roundScores.ContainsKey(p.ID)).OrderByDescending(p => roundScores[p.ID]).ThenBy(p => p.ID))
            {
                if (Place == 0)
                    winner = guild;
                string str = "No  " + (Place + 1).ToString() + ": " + guild.Name + "(" + roundScores[guild.ID] + ")";
                ret.Add(str);
                Place++;
                if (Place == 4)
                    break;
            }

            changed = false;
            scoreMessages = ret.ToArray();
        }

    }

}
