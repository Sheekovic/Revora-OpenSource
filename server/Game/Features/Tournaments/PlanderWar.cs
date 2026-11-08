using Revora.Network.GamePackets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Revora.Game.Features
{
    public class PlanderWar
    {
        public PlanderWar()
        {
            open = false;
        }
        public void StartWar()
        {
            if (!open)
            {
                Start();
            }
        }
        public bool CheckClose()
        {
            if (DateTime.Now.Hour == 18 && DateTime.Now.Minute == 00 && open == true)
                return true;
            return false;
        }
        private IDisposable Subscribe;
        public static bool open = false;
        bool SendInvitation = false;
        public static void GetGold(Client.GameState client)
        {
            var Gold = Kernel.Random.Next(1, 10);
            client.ProgressBar = new ProgressBar(client, "Obtain " + Gold + " GoldBricks", p =>
            {
                p.Entity.PuntosUnion = (uint)Gold;
                _String str = new Network.GamePackets._String(true)
                {
                    Action = _String.Mode.Effect,
                    UID = p.Entity.UID
                };
                str.Texts.Add("accession3");
                str.Texts.Add("end_task");
                p.SendScreen(str.ToArray(), true);
            }, "Done", 5);
        }
        public void Start()
        {
            if (!open)
            {
                SendInvitation = false;
                open = true;
                Subscribe = Thread.Subscribe(Work, 1000);
            }
        }
        public void Close()
        {
            foreach (var client in Kernel.GamePool.Values.Where(U => U.Union != null && U.Entity.OnUnionWar == true))
            {
                client.Entity.PuntosUnion = 0;

                // Add GoldBrick items or Union funds.
             //   client.Inventory.Add(0, 0, 0);
              //  client.Inventory.Add(0, 0, 0);
              //  client.Inventory.Add(0, 0, 0);
                client.Entity.PKMode = Enums.PKMode.Capture;
                client.Send(new Data(true)
                {
                    UID = client.Entity.UID,
                    ID = Data.ChangePKMode,
                    dwParam = (uint)client.Entity.PKMode
                });
            }
            open = false;
            Subscribe.Dispose();
        }
        public void Work(int time)
        {
            if (!SendInvitation)
            {
                //foreach (var client in Kernel.GamePool.Values.Where(U => U.Union != null))
                //{
                //    //if (!client.InQualifier() && client.Player.PokerTable == 0 && client.Map.BaseID != 6001 && client.Map.BaseID != 6000 && !client.Player.Dead)
                //    //{
                //    //    Network.GamePackets.AutoInvite alert = new Network.GamePackets.AutoInvite
                //    //    {
                //    //        InviteID = 10573,
                //    //        Countdown = 60,
                //    //        Action = 1
                //    //    };
                //    //    client.Player.InviteID = 10573;
                //    //    client.Send(alert.ToArray());
                //    //}
                //}
                SendInvitation = true;
            }
            else
            {
                if (CheckClose() == true)
                {
                    Close();
                }
            }
        }
    }
}


namespace Revora.Game.Features
{
    // Persistence foundation for the approved competitive edition. Not yet wired to
    // admissions, NPC callbacks, combat or title eligibility; legacy tokens are not scores.
    internal static class KingdomCompetitionLedger
    {
        internal sealed class Result {
            internal string Day;internal uint Union,Leader;internal long Score;
            internal DateTime Expires,Ends;internal bool Final;
        }
        private static readonly TimeZoneInfo Zone=TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
        private static void Utc(DateTime now){if(now.Kind!=DateTimeKind.Utc)throw new ArgumentException("Kingdom clock must be UTC.");}
        private static DateTime ReadUtc(DateTime value) {
            if(value.Kind==DateTimeKind.Utc)return value;
            if(value.Kind==DateTimeKind.Local)return value.ToUniversalTime();
            throw new InvalidOperationException("Kingdom timestamp has no time zone.");
        }
        private static DateTime At(DateTime date,int hour){return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(date.Date.AddHours(hour),DateTimeKind.Unspecified),Zone);}
        internal static void Initialize() {
            using(var c=Revora.Database.DataHolder.MySqlConnection){c.Open();QuestInventory.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_kingdom_contests(
 day varchar(10) PRIMARY KEY,starts timestamptz NOT NULL,ends timestamptz NOT NULL,expires timestamptz NOT NULL,
 state varchar(10) NOT NULL CHECK(state IN('active','final')),winner bigint NOT NULL DEFAULT 0,leader bigint NOT NULL DEFAULT 0,
 score bigint NOT NULL DEFAULT 0,settled_at timestamptz NULL,CHECK(starts<ends AND ends<expires),
 CHECK(winner>=0 AND leader>=0 AND score>=0),CHECK((winner=0 AND leader=0 AND score=0) OR (winner>0 AND leader>0 AND score>0)));
CREATE TABLE IF NOT EXISTS revora_kingdom_entries(
 day varchar(10) NOT NULL REFERENCES revora_kingdom_contests(day),uid bigint NOT NULL,union_id bigint NOT NULL,
 nonce uuid NULL,due timestamptz NULL,deadline timestamptz NULL,carried boolean NOT NULL DEFAULT false,
 PRIMARY KEY(day,uid),CHECK(uid>0 AND union_id>0),CHECK(NOT carried OR nonce IS NOT NULL));
ALTER TABLE revora_kingdom_entries ADD COLUMN IF NOT EXISTS pkmode integer NOT NULL DEFAULT 3;
ALTER TABLE revora_kingdom_entries ADD COLUMN IF NOT EXISTS returned boolean NOT NULL DEFAULT false;
CREATE TABLE IF NOT EXISTS revora_kingdom_deliveries(
 nonce uuid PRIMARY KEY,day varchar(10) NOT NULL,uid bigint NOT NULL,union_id bigint NOT NULL,delivered_at timestamptz NOT NULL,
 FOREIGN KEY(day,uid) REFERENCES revora_kingdom_entries(day,uid),CHECK(uid>0 AND union_id>0));");}
        }
        // All ledger operations take this bounded lock before character/union rows.
        // The integration layer must acquire session/item ownership before calling.
        private static void Gate(Npgsql.NpgsqlConnection c,Npgsql.NpgsqlTransaction t) {
            QuestInventory.Execute(c,t,"SET LOCAL lock_timeout='2s';SET LOCAL statement_timeout='5s';SELECT pg_advisory_xact_lock(6711,3998)");
        }
        internal static string Open(DateTime now) {
            Utc(now);var local=TimeZoneInfo.ConvertTimeFromUtc(now,Zone);var start=At(local.Date,17);var end=At(local.Date,18);
            if(now<start || now>=end)return null;
            var day=local.ToString("yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture);
            using(var c=Revora.Database.DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                Gate(c,t);QuestInventory.Execute(c,t,@"INSERT INTO revora_kingdom_contests(day,starts,ends,expires,state)
VALUES(@day,@start,@end,@expiry,'active') ON CONFLICT(day) DO NOTHING","day",day,"start",start,"end",end,"expiry",At(local.Date.AddDays(1),18));
                bool active=Active(c,t,day,now);t.Commit();return active?day:null;
            }}
        }
        private static bool Active(Npgsql.NpgsqlConnection c,Npgsql.NpgsqlTransaction t,string day,DateTime now) {
            using(var q=QuestInventory.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_kingdom_contests WHERE day=@day AND state='active' AND starts<=@now AND @now<ends)","day",day,"now",now))return Equals(q.ExecuteScalar(),true);
        }
        private static bool Member(Npgsql.NpgsqlConnection c,Npgsql.NpgsqlTransaction t,uint uid,uint union) {
            if(uid==0 || union==0)return false;
            using(var q=QuestInventory.Command(c,t,"SELECT unionid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)uid)) {
                var value=q.ExecuteScalar();if(value==null || value==DBNull.Value || Convert.ToUInt32(value)!=union)return false;
            }
            using(var q=QuestInventory.Command(c,t,"SELECT EXISTS(SELECT 1 FROM unions WHERE id=@union AND leaderuid>0)","union",(long)union))return Equals(q.ExecuteScalar(),true);
        }
        internal static bool Admit(string day,uint uid,uint union,DateTime now) {return Admit(day,uid,union,now,Enums.PKMode.Capture);}
        internal static bool Admit(string day,uint uid,uint union,DateTime now,Enums.PKMode mode) {
            Utc(now);if(!Enum.IsDefined(typeof(Enums.PKMode),mode))return false;
            using(var c=Revora.Database.DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                Gate(c,t);if(!Active(c,t,day,now) || !Member(c,t,uid,union))return false;
                QuestInventory.Execute(c,t,"INSERT INTO revora_kingdom_entries(day,uid,union_id,pkmode) VALUES(@day,@uid,@union,@mode) ON CONFLICT(day,uid) DO NOTHING","day",day,"uid",(long)uid,"union",(long)union,"mode",(int)mode);
                using(var q=QuestInventory.Command(c,t,"SELECT union_id FROM revora_kingdom_entries WHERE day=@day AND uid=@uid","day",day,"uid",(long)uid))
                    if(Convert.ToUInt32(q.ExecuteScalar())!=union)return false;
                QuestInventory.Execute(c,t,"UPDATE revora_kingdom_entries SET returned=false,pkmode=@mode WHERE day=@day AND uid=@uid AND returned","mode",(int)mode,"day",day,"uid",(long)uid);
                t.Commit();return true;
            }}
        }
        internal static Guid BeginHarvest(string day,uint uid,uint union,DateTime now) {
            Utc(now);
            using(var c=Revora.Database.DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                Gate(c,t);if(!Active(c,t,day,now) || !Member(c,t,uid,union))return Guid.Empty;
                Guid nonce=Guid.NewGuid();
                if(QuestInventory.Execute(c,t,@"UPDATE revora_kingdom_entries SET nonce=@nonce,due=@due,deadline=@deadline
WHERE day=@day AND uid=@uid AND union_id=@union AND NOT returned AND NOT carried AND (nonce IS NULL OR deadline<@now)",
                    "nonce",nonce,"due",now.AddSeconds(5),"deadline",now.AddSeconds(30),"day",day,"uid",(long)uid,"union",(long)union,"now",now)!=1)return Guid.Empty;
                t.Commit();return nonce;
            }}
        }
        internal static bool FinishHarvest(string day,uint uid,uint union,Guid nonce,DateTime now) {
            Utc(now);if(nonce==Guid.Empty)return false;
            using(var c=Revora.Database.DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                Gate(c,t);if(!Active(c,t,day,now) || !Member(c,t,uid,union))return false;
                bool saved=QuestInventory.Execute(c,t,@"UPDATE revora_kingdom_entries SET carried=true WHERE day=@day AND uid=@uid
AND union_id=@union AND nonce=@nonce AND NOT returned AND NOT carried AND due<=@now AND @now<=deadline",
                    "day",day,"uid",(long)uid,"union",(long)union,"nonce",nonce,"now",now)==1;
                t.Commit();return saved;
            }}
        }
        internal static bool Deliver(string day,uint uid,uint union,Guid nonce,DateTime now) {
            Utc(now);if(nonce==Guid.Empty)return false;
            using(var c=Revora.Database.DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                Gate(c,t);if(!Active(c,t,day,now) || !Member(c,t,uid,union))return false;
                if(QuestInventory.Execute(c,t,@"UPDATE revora_kingdom_entries SET carried=false,nonce=NULL,due=NULL,deadline=NULL
WHERE day=@day AND uid=@uid AND union_id=@union AND nonce=@nonce AND NOT returned AND carried",
                    "day",day,"uid",(long)uid,"union",(long)union,"nonce",nonce)!=1)return false;
                if(QuestInventory.Execute(c,t,@"INSERT INTO revora_kingdom_deliveries(nonce,day,uid,union_id,delivered_at) VALUES(@nonce,@day,@uid,@union,@now)",
                    "nonce",nonce,"day",day,"uid",(long)uid,"union",(long)union,"now",now)!=1)throw new InvalidOperationException("Kingdom delivery receipt was not saved.");
                t.Commit();return true;
            }}
        }
        // Death, voluntary exit and interrupted-harvest recovery discard unbanked cargo.
        // The future field adapter supplies these lifecycle boundaries, not client scores.
        internal static bool Discard(string day,uint uid) {
            using(var c=Revora.Database.DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                Gate(c,t);bool changed=QuestInventory.Execute(c,t,"UPDATE revora_kingdom_entries SET carried=false,nonce=NULL,due=NULL,deadline=NULL WHERE day=@day AND uid=@uid AND nonce IS NOT NULL","day",day,"uid",(long)uid)==1;
                t.Commit();return changed;
            }}
        }
        internal static Result Read(string day) {
            using(var c=Revora.Database.DataHolder.MySqlConnection){c.Open();return Read(c,null,day);}
        }
        private static Result Read(Npgsql.NpgsqlConnection c,Npgsql.NpgsqlTransaction t,string day) {
            using(var q=QuestInventory.Command(c,t,"SELECT state,winner,leader,score,expires,ends FROM revora_kingdom_contests WHERE day=@day","day",day))
            using(var r=q.ExecuteReader()){if(!r.Read())return null;return new Result{Day=day,Final=r.GetString(0)=="final",Union=Convert.ToUInt32(r[1]),Leader=Convert.ToUInt32(r[2]),Score=Convert.ToInt64(r[3]),Expires=ReadUtc(r.GetDateTime(4)),Ends=ReadUtc(r.GetDateTime(5))};}
        }
        internal static Result ActiveCrown(DateTime now) {
            Utc(now);
            using(var c=Revora.Database.DataHolder.MySqlConnection){c.Open();
            using(var q=QuestInventory.Command(c,null,@"WITH latest AS (
 SELECT day,winner,ends,expires FROM revora_kingdom_contests WHERE state='final' AND ends<=@now ORDER BY ends DESC,day DESC LIMIT 1)
 SELECT l.day,l.winner,u.leaderuid,l.ends,l.expires FROM latest l
 JOIN unions u ON u.id=l.winner JOIN entities e ON e.uid=u.leaderuid AND e.unionid=u.id AND e.offical=1
 WHERE l.winner>0 AND l.expires>@now","now",now)){q.CommandTimeout=5;using(var r=q.ExecuteReader()) {
                if(!r.Read())return null;
                return new Result{Day=r.GetString(0),Union=Convert.ToUInt32(r[1]),Leader=Convert.ToUInt32(r[2]),Ends=ReadUtc(r.GetDateTime(3)),Expires=ReadUtc(r.GetDateTime(4)),Final=true};
            }}}
        }
        internal static string[] Due(DateTime now) {
            Utc(now);var days=new List<string>();using(var c=Revora.Database.DataHolder.MySqlConnection){c.Open();
            using(var q=QuestInventory.Command(c,null,"SELECT day FROM revora_kingdom_contests WHERE state='active' AND ends<=@now ORDER BY day LIMIT 16","now",now))
            using(var r=q.ExecuteReader())while(r.Read())days.Add(r.GetString(0));}return days.ToArray();
        }
        internal static Enums.PKMode? Return(uint uid) {
            using(var c=Revora.Database.DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                Gate(c,t);object value;
                using(var q=QuestInventory.Command(c,t,"SELECT pkmode FROM revora_kingdom_entries WHERE uid=@uid AND NOT returned ORDER BY day DESC LIMIT 1 FOR UPDATE","uid",(long)uid))value=q.ExecuteScalar();
                if(value==null)return null;
                int mode=Convert.ToInt32(value);if(mode<0 || mode>255 || !Enum.IsDefined(typeof(Enums.PKMode),(byte)mode))throw new InvalidOperationException("Saved Kingdom PK mode is invalid.");
                QuestInventory.Execute(c,t,"UPDATE revora_kingdom_entries SET returned=true,carried=false,nonce=NULL,due=NULL,deadline=NULL WHERE uid=@uid AND NOT returned","uid",(long)uid);
                if(QuestInventory.Execute(c,t,"UPDATE entities SET mapid=1002,x=@x,y=@y WHERE uid=@uid","x",(int)KingdomCompetitionField.ReturnX,"y",(int)KingdomCompetitionField.ReturnY,"uid",(long)uid)!=1)throw new InvalidOperationException("Kingdom return position was not saved.");
                t.Commit();return (Enums.PKMode)mode;
            }}
        }
        internal static Result Settle(string day,DateTime now) {
            Utc(now);
            using(var c=Revora.Database.DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                Gate(c,t);var result=Read(c,t,day);if(result==null || result.Final)return result;
                using(var q=QuestInventory.Command(c,t,"SELECT ends FROM revora_kingdom_contests WHERE day=@day","day",day))
                    if(now<ReadUtc(Convert.ToDateTime(q.ExecuteScalar())))return null;
                // Scores come only from accepted deposits in this contest. Bought bricks,
                // lifetime balance and arbitrary client counters cannot select the ruler.
                using(var q=QuestInventory.Command(c,t,@"SELECT u.id,u.leaderuid,s.points FROM
(SELECT union_id,count(*)::bigint points FROM revora_kingdom_deliveries WHERE day=@day GROUP BY union_id) s
JOIN unions u ON u.id=s.union_id JOIN entities e ON e.uid=u.leaderuid AND e.unionid=u.id
ORDER BY s.points DESC,u.id ASC LIMIT 1 FOR UPDATE OF u,e","day",day))
                using(var r=q.ExecuteReader())if(r.Read()){result.Union=Convert.ToUInt32(r[0]);result.Leader=Convert.ToUInt32(r[1]);result.Score=Convert.ToInt64(r[2]);}
                if(QuestInventory.Execute(c,t,"UPDATE revora_kingdom_contests SET state='final',winner=@union,leader=@leader,score=@score,settled_at=@now WHERE day=@day AND state='active'",
                    "union",(long)result.Union,"leader",(long)result.Leader,"score",result.Score,"now",now,"day",day)!=1)throw new InvalidOperationException("Kingdom result was not saved.");
                t.Commit();result.Final=true;return result;
            }}
        }
    }
}


namespace Revora.Game.Features
{
    using Q=Revora.Game.Features.QuestInventory;
    using Player=Revora.Client.GameState;
    using Monitor=System.Threading.Monitor;
    internal static class KingdomCompetitionField
    {
        internal const ushort MapId=3998,ReturnX=316,ReturnY=325;
        internal const uint Entry=903980,Depot=903981;
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        internal static bool Enabled {get;private set;}
        private static bool recoveryReady;
        private static Map field,town;
        private static DateTime nextSettle;
        private static uint cursor;
        private static int ticking;
        private sealed class Admission {
            internal Player Player;internal string Day;internal uint Union,Actor;
            internal DateTime End,Started,ProtectedUntil;internal Guid Pending,Carried;
            internal ushort X,Y;internal Time32 Move,Death;
        }
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<uint,Admission> Players=new System.Collections.Concurrent.ConcurrentDictionary<uint,Admission>();
        private static readonly ushort[][] Nodes={new ushort[]{300,153},new ushort[]{500,300},new ushort[]{151,300},new ushort[]{300,498}};
        internal static void ConfigureStartup(string setting) {
            bool enable=false;
            if(!string.IsNullOrEmpty(setting) && !bool.TryParse(setting,out enable))throw new InvalidOperationException("Kingdom competition setting must be true or false.");
            if(!enable){
                using(var c=Revora.Database.DataHolder.MySqlConnection){c.Open();
                    using(var q=QuestInventory.Command(c,null,"SELECT to_regclass(@table) IS NOT NULL","table","revora_kingdom_entries")){q.CommandTimeout=5;recoveryReady=Convert.ToBoolean(q.ExecuteScalar());}}
                Console.WriteLine("[kingdom] startup-disabled");return;
            }
            if(!Kernel.Maps.ContainsKey(MapId)) {
                string path;if(!Revora.Database.DMaps.MapPaths.TryGetValue(MapId,out path))throw new InvalidOperationException("Kingdom terrain catalog is missing.");
                new Map(MapId,path);
            }
            Initialize();
            Console.WriteLine("[kingdom] startup-ready map=3998 actors=5 crown="+Kernel.ServerKingdom);
        }
        internal static void Initialize() {
            if(Enabled)return;
            if(!Kernel.Maps.TryGetValue(MapId,out field) || field.BaseID!=MapId || !Kernel.Maps.TryGetValue(1002,out town) || town.BaseID!=1002)
                throw new InvalidOperationException("Kingdom maps are missing.");
            foreach(var map in Kernel.Maps.Values)for(uint id=Entry;id<=Depot+3;id++)if(map.Npcs.ContainsKey(id))throw new InvalidOperationException("Kingdom NPC identity collision.");
            if(!town.Floor[314,325,MapObjectType.Player] || !town.Floor[ReturnX,ReturnY,MapObjectType.Player])throw new InvalidOperationException("Kingdom town approach is blocked.");
            ValidateField(field.Floor);
            KingdomCompetitionLedger.Initialize();recoveryReady=true;
            town.AddNpc(new NpcSpawn{UID=Entry,MapID=1002,X=314,Y=325,Mesh=10,Name="UnionMarshal",Type=Enums.NpcType.Talker});
            for(int i=0;i<Nodes.Length;i++)field.AddNpc(new NpcSpawn{UID=Depot+(uint)i,MapID=MapId,X=Nodes[i][0],Y=Nodes[i][1],Mesh=10,Name=i==0?"BrickKeeper":"RoyalQuarry",Type=Enums.NpcType.Talker});
            if(!town.Floor[ReturnX,ReturnY,MapObjectType.Player] || Nodes.Any(n=>!field.Floor[n[0]+2,n[1],MapObjectType.Player]))throw new InvalidOperationException("Kingdom actor footprint blocks an approach.");
            KingdomCrown.Initialize();Enabled=true;
        }
        private static bool Clear(Floor floor,int x,int y) {
            if(!floor[x,y,MapObjectType.Player])return false;
            foreach(var n in Nodes)if(Math.Abs(n[0]-x)<=1 && Math.Abs(n[1]-y)<=1)return false;return true;
        }
        internal static void ValidateField(Floor floor) {
            if(floor==null || floor.Bounds.Width<503 || floor.Bounds.Height<500 || (long)floor.Bounds.Width*floor.Bounds.Height>2000000)throw new InvalidOperationException("Kingdom terrain dimensions are invalid.");
            Func<int,int,bool> walk=(x,y)=>Clear(floor,x,y);
            foreach(var n in Nodes)if(!floor[n[0],n[1],MapObjectType.Player] || !walk(n[0]+2,n[1]))throw new InvalidOperationException("Kingdom actor approach is blocked.");
            if(!walk(296,153))throw new InvalidOperationException("Kingdom arrival is blocked.");
            int width=floor.Bounds.Width;var seen=new bool[width*floor.Bounds.Height];var queue=new Queue<int>();queue.Enqueue(153*width+296);seen[153*width+296]=true;
            var dx=new[]{-1,1,0,0};var dy=new[]{0,0,-1,1};
            while(queue.Count!=0) {
                int point=queue.Dequeue(),x=point%width,y=point/width;
                for(int direction=0;direction<4;direction++) {
                    int nx=x+dx[direction],ny=y+dy[direction];if(nx<0 || ny<0 || nx>=width || ny>=floor.Bounds.Height || !walk(nx,ny))continue;
                    int next=ny*width+nx;if(seen[next])continue;seen[next]=true;queue.Enqueue(next);
                }
            }
            if(Nodes.Any(n=>!seen[n[1]*width+n[0]+2]))throw new InvalidOperationException("Kingdom field approaches are disconnected.");
        }
        private static bool Owns(Player p,Admission a) {
            Revora.Network.GamePackets.Union.UnionClass union;
            return Q.Player(p,false) && ReferenceEquals(a.Player,p) && p.Entity.MapID==MapId && ReferenceEquals(p.Map,field)
                && p.UnionID==a.Union && p.Union!=null && Kernel.Unions.TryGetValue(a.Union,out union) && ReferenceEquals(p.Union,union);
        }
        internal static bool InField(Entity body) { return body!=null && body.MapID==MapId; }
        internal static bool Touches(Entity attacker,Entity target) {return InField(attacker) || InField(target);}
        private static Admission Fighter(Entity body,DateTime now) {
            Admission a;
            if(!Enabled || body==null || body.EntityFlag!=EntityFlag.Player || body.Owner==null || !ReferenceEquals(body.Owner.Entity,body)
                || !Players.TryGetValue(body.UID,out a) || now>=a.End || !Owns(body.Owner,a)
                || body.Owner.InQualifier() || body.Owner.IsWatching() || body.Owner.Booth!=null || body.Owner.Trade==null || body.Owner.Trade.InTrade)return null;
            return a;
        }
        private static bool Opponents(Entity attacker,Entity target) {
            var now=Clock();var a=Fighter(attacker,now);var b=Fighter(target,now);
            return a!=null && b!=null && attacker.UID!=target.UID && a.Day==b.Day && a.Union!=b.Union
                && Kernel.GetDistance(attacker.X,attacker.Y,target.X,target.Y)<=Constants.pScreenDistance;
        }
        internal static bool BlockAttack(Entity attacker,Entity target) {
            if(!Touches(attacker,target))return false;
            if(!Opponents(attacker,target) || attacker.Dead || target.Dead || attacker.PKMode!=Enums.PKMode.Union)return true;
            var now=Clock();var a=Fighter(attacker,now);var b=Fighter(target,now);
            return a==null || b==null || now<a.ProtectedUntil || now<b.ProtectedUntil;
        }
        internal static bool TryDie(Entity body,Entity killer) {
            if(!Touches(killer,body))return false;
            // Field kills award no legacy counters, currency, crime or dropped items.
            // The ordinary death packet/flags remain owned by Entity.Die(uint).
            if(BlockAttack(killer,body) || !body.Owner.Attackable || Time32.Now<killer.Owner.CantAttack
                || body.ContainsFlag((ulong)FlagsUpdate.Flags.Dead) || !Monitor.TryEnter(body.Owner.ItemSyncRoot))return true;
            try {
                if(!Opponents(killer,body))return true;
                Admission a;if(!Players.TryGetValue(body.UID,out a))return true;
                Cancel(a);body.Killer=killer;body.Die(killer.UID);a.Death=body.DeathStamp;
            }catch(Exception ex){Q.Fail(body.Owner,"kingdom-death",ex);}
            finally{Monitor.Exit(body.Owner.ItemSyncRoot);}return true;
        }
        private static void Remove(Admission a) {((ICollection<KeyValuePair<uint,Admission>>)Players).Remove(new KeyValuePair<uint,Admission>(a.Player.Entity.UID,a));}
        private static void Mode(Player p,Enums.PKMode mode) {
            p.Entity.PKMode=mode;p.Send(new Data(true){UID=p.Entity.UID,ID=Data.ChangePKMode,dwParam=(uint)mode});
        }
        private static void Leave(Player p,Admission a) {
            var mode=KingdomCompetitionLedger.Return(p.Entity.UID);Remove(a);
            if(mode.HasValue)Mode(p,mode.Value);
            p.Entity.Teleport(1002,ReturnX,ReturnY);
            if(p.Entity.MapID!=1002 || p.Entity.X!=ReturnX || p.Entity.Y!=ReturnY)throw new InvalidOperationException("Kingdom return needs reload.");
        }
        private static void Cancel(Admission a) {KingdomCompetitionLedger.Discard(a.Day,a.Player.Entity.UID);a.Pending=a.Carried=Guid.Empty;}
        private static bool Maintain(Player p,Admission a) {
            DateTime now=Clock();
            if(!Owns(p,a) || now>=a.End){Leave(p,a);return false;}
            if(p.Entity.Dead || p.Entity.DeathStamp.Value!=a.Death.Value){Cancel(a);a.Death=p.Entity.DeathStamp;return !p.Entity.Dead;}
            if(a.Pending!=Guid.Empty) {
                if(p.LastMove.Value!=a.Move.Value || p.Entity.X!=a.X || p.Entity.Y!=a.Y || !Q.Near(p,a.Actor,MapId) || now>a.Started.AddSeconds(30)){Cancel(a);return true;}
                if(now>=a.Started.AddSeconds(5)) {
                    if(KingdomCompetitionLedger.FinishHarvest(a.Day,p.Entity.UID,a.Union,a.Pending,now)) {a.Carried=a.Pending;a.Pending=Guid.Empty;p.Send("The brick is yours. Bring it to the keeper.");}
                    else Cancel(a);
                }
            }
            return true;
        }
        internal static bool HandleNpc(Player p,NpcRequest request) {
            if(p==null || request==null || p.ActiveNpc<Entry || p.ActiveNpc>Depot+3)return false;
            // Reserved IDs never fall through to the unsafe legacy event branch.
            if(!Enabled || !Q.Player(p,false) || !SessionInstall.TryEnter(p.Entity.UID))return true;
            bool held=false;
            try {
                if(!(held=Monitor.TryEnter(p.ItemSyncRoot)) || !Q.Player(p,false))return true;
                var now=Clock();uint npc=p.ActiveNpc;Admission a;
                if(npc==Entry) {
                    if(!Q.Near(p,Entry,1002) || p.InQualifier() || p.IsWatching())return true;
                    string day=KingdomCompetitionLedger.Open(now);
                    if(request.OptionID==1 && day!=null && p.Union!=null && p.UnionID==p.Union.ID) {
                        if(!KingdomCompetitionLedger.Admit(day,p.Entity.UID,p.Union.ID,now,p.Entity.PKMode))return true;
                        KingdomCompetitionLedger.Discard(day,p.Entity.UID);
                        a=new Admission{Player=p,Day=day,Union=p.Union.ID,End=KingdomCompetitionLedger.Read(day).Ends,ProtectedUntil=now.AddSeconds(10),Death=p.Entity.DeathStamp};
                        Players[p.Entity.UID]=a;Mode(p,Enums.PKMode.Union);p.Entity.Teleport(MapId,296,153);
                        if(!Owns(p,a))throw new InvalidOperationException("Kingdom entry needs reload.");
                        p.Send("Gather royal bricks and carry them to the keeper. Death or departure loses your load.");return true;
                    }
                    var d=new Npcs(p);d.Text(day==null?"The royal contest opens daily at five and closes at six, Pacific time.":"The crown awaits the union that brings home the most royal bricks.");
                    if(day!=null && p.Union!=null)d.Option("Stand with my union.",1);d.Option("Farewell.",255);d.Send();return true;
                }
                if(!Players.TryGetValue(p.Entity.UID,out a) || !ReferenceEquals(a.Player,p) || !Maintain(p,a) || !Q.Near(p,npc,MapId))return true;
                if(npc==Depot && request.OptionID==2){Leave(p,a);return true;}
                if(request.OptionID==1) {
                    if(npc==Depot) {
                        if(a.Carried!=Guid.Empty && KingdomCompetitionLedger.Deliver(a.Day,p.Entity.UID,a.Union,a.Carried,now)){a.Carried=Guid.Empty;p.Send("One royal brick banked for your union.");}
                    } else if(a.Pending==Guid.Empty && a.Carried==Guid.Empty) {
                        a.Pending=KingdomCompetitionLedger.BeginHarvest(a.Day,p.Entity.UID,a.Union,now);
                        if(a.Pending!=Guid.Empty){a.Actor=npc;a.Started=now;a.X=p.Entity.X;a.Y=p.Entity.Y;a.Move=p.LastMove;p.Send("Work the stone for five seconds. Hold your ground.");}
                    }
                }
                var dialog=new Npcs(p);dialog.Text(npc==Depot?"Bring me your royal bricks. Only a safe delivery counts.":"A royal brick lies in this stone. Keep still while you free it.");
                dialog.Option(npc==Depot?"Bank my brick.":"Work the stone.",1);if(npc==Depot)dialog.Option("Return to the city.",2);dialog.Option("Farewell.",255);dialog.Send();
            }catch(Exception ex){Q.Fail(p,"kingdom-field",ex);}
            finally{if(held)Monitor.Exit(p.ItemSyncRoot);SessionInstall.Exit(p.Entity.UID);}return true;
        }
        internal static void Loaded(Player p) {
            if(p==null || p.Entity==null)return;
            var mode=recoveryReady?KingdomCompetitionLedger.Return(p.Entity.UID):(Enums.PKMode?)null;
            if(mode.HasValue || p.Entity.MapID==MapId){p.Entity.MapID=1002;p.Entity.X=ReturnX;p.Entity.Y=ReturnY;p.Entity.PKMode=mode??Enums.PKMode.Capture;}
        }
        internal static void Tick() {
            if(!Enabled || System.Threading.Interlocked.CompareExchange(ref ticking,1,0)!=0)return;
            try {
                var snapshot=Players.Values.OrderBy(a=>a.Player.Entity.UID).ToArray();
                uint after=cursor;
                foreach(var a in snapshot.Where(a=>a.Player.Entity.UID>after).Concat(snapshot.Where(a=>a.Player.Entity.UID<=after)).Take(32)) {
                    var p=a.Player;cursor=p.Entity.UID;if(!SessionInstall.TryEnter(p.Entity.UID))continue;bool held=false;
                    try {
                        if(!(held=Monitor.TryEnter(p.ItemSyncRoot)))continue;
                        if(!Q.Player(p,false)){Remove(a);continue;}
                        Maintain(p,a);
                    }catch(Exception ex){Q.Fail(p,"kingdom-pulse",ex);}
                    finally{if(held)Monitor.Exit(p.ItemSyncRoot);SessionInstall.Exit(p.Entity.UID);}
                }
                var now=Clock();if(now>=nextSettle){nextSettle=now.AddSeconds(5);foreach(string day in KingdomCompetitionLedger.Due(now))KingdomCompetitionLedger.Settle(day,now);KingdomCrown.Tick();}
            }finally{System.Threading.Volatile.Write(ref ticking,0);}
        }
    }
}

namespace Revora.Game.Features
{
    internal static class KingdomCrown
    {
        private static KingdomCompetitionLedger.Result current;
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<uint,byte> Pending=new System.Collections.Concurrent.ConcurrentDictionary<uint,byte>();
        internal static bool Enabled {get;private set;}
        private static bool recoveryReady;
        internal static void Initialize() {
            lock(UnionFormation.Sync) {
                var next=KingdomCompetitionLedger.ActiveCrown(KingdomCompetitionField.Clock());
                Enabled=true;Publish(next);
            }
        }
        private static void Publish(KingdomCompetitionLedger.Result next) {
            var previous=System.Threading.Volatile.Read(ref current);
            System.Threading.Volatile.Write(ref current,next);Kernel.ServerKingdom=next==null?0:next.Union;
            if((previous==null && next==null) || previous!=null && next!=null && previous.Day==next.Day && previous.Union==next.Union && previous.Leader==next.Leader && previous.Expires==next.Expires)return;
            foreach(var p in Kernel.GamePool.Values)if(p!=null && p.Entity!=null)Pending[p.Entity.UID]=0;
        }
        internal static void RefreshAuthority() {
            if(!Enabled)return;
            lock(UnionFormation.Sync) {
                try{Publish(KingdomCompetitionLedger.ActiveCrown(KingdomCompetitionField.Clock()));}
                catch{Publish(null);throw;}
            }
        }
        internal static bool IsUnion(uint id) {
            var crown=System.Threading.Volatile.Read(ref current);Revora.Network.GamePackets.Union.UnionClass union;
            return Enabled && crown!=null && id!=0 && crown.Union==id && KingdomCompetitionField.Clock()<crown.Expires
                && KingdomCompetitionField.Clock()>=crown.Ends && Kernel.Unions.TryGetValue(id,out union) && union.LeaderUID==crown.Leader;
        }
        internal static bool IsRuler(Revora.Client.GameState p) {
            var crown=System.Threading.Volatile.Read(ref current);Revora.Network.GamePackets.Union.UnionClass union;
            return crown!=null && QuestInventory.Player(p,false) && p.Entity.UID==crown.Leader && p.UnionID==crown.Union
                && Enabled && KingdomCompetitionField.Clock()>=crown.Ends && KingdomCompetitionField.Clock()<crown.Expires && Kernel.Unions.TryGetValue(crown.Union,out union)
                && union.LeaderUID==crown.Leader && ReferenceEquals(p.Union,union) && ReferenceEquals(crown,System.Threading.Volatile.Read(ref current));
        }
        private static void Reconcile(uint uid) {
            Revora.Client.GameState p;byte ignored;
            if(!Kernel.GamePool.TryGetValue(uid,out p) || !QuestInventory.Player(p,false)){Pending.TryRemove(uid,out ignored);return;}
            if(!SessionInstall.TryEnter(uid))return;bool held=false;
            try {
                if(!(held=System.Threading.Monitor.TryEnter(p.ItemSyncRoot)) || !QuestInventory.Player(p,false))return;
                TitleRequirements.Reconcile(p,false);p.CommitKingdomPresentation();Pending.TryRemove(uid,out ignored);
            }catch(Exception ex){QuestInventory.Fail(p,"kingdom-crown",ex);}
            finally{if(held)System.Threading.Monitor.Exit(p.ItemSyncRoot);SessionInstall.Exit(uid);}
        }
        internal static void Tick() {
            if(!Enabled)return;
            var prior=System.Threading.Volatile.Read(ref current);RefreshAuthority();var next=System.Threading.Volatile.Read(ref current);
            if(prior!=null && Pending.ContainsKey(prior.Leader))Reconcile(prior.Leader);
            if(next!=null && Pending.ContainsKey(next.Leader))Reconcile(next.Leader);
            foreach(uint uid in Pending.Keys.Take(32))Reconcile(uid);
        }
    }
}
