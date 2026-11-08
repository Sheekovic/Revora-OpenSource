using System;
using System.Globalization;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    // Calendar contract for the owner-approved Revora Planet expedition.
    // Ordinary Rowan service; all stages and the fixed-term award use durable receipts.
    internal static class PlanetWeekend
    {
        internal const int CollectionLimit=13;
        internal static Func<DateTime> Clock = () => DateTime.UtcNow;
        private static bool initialized;
        private static readonly object ActiveGate = new object();
        private static ConditionalWeakTable<GameState, Run> active = new ConditionalWeakTable<GameState, Run>();
        internal sealed class Run
        {
            internal Guid Id;
            internal string Weekend, Stage;
            internal int Kills, CollectionStep;
            internal DateTime AcceptedUtc, StartUtc, EndUtc;
        }
        internal static void Initialize()
        {
            using (var c = DataHolder.MySqlConnection) {
                c.Open(); Q.Execute(c, null, @"CREATE TABLE IF NOT EXISTS revora_planet_runs(
id uuid PRIMARY KEY,uid bigint NOT NULL CHECK(uid>0),weekend varchar(8) NOT NULL,
accepted_at timestamptz NOT NULL,starts_at timestamptz NOT NULL,ends_at timestamptz NOT NULL,
kills integer NOT NULL DEFAULT 0 CHECK(kills BETWEEN 0 AND 100),
stage varchar(16) NOT NULL DEFAULT 'hunt' CHECK(stage IN('hunt','encounter','delivery','complete')),
rule_version integer NOT NULL DEFAULT 1 CHECK(rule_version=1),UNIQUE(uid,weekend),
CHECK(accepted_at>=starts_at AND accepted_at<ends_at),CHECK(ends_at>starts_at),
CHECK((stage='hunt' AND kills<100) OR (stage<>'hunt' AND kills=100)));
CREATE TABLE IF NOT EXISTS revora_planet_completions(
uid bigint NOT NULL CHECK(uid>0),weekend varchar(8) NOT NULL,
run uuid NOT NULL UNIQUE REFERENCES revora_planet_runs(id),attempt uuid NOT NULL UNIQUE,
completed_at timestamptz NOT NULL,PRIMARY KEY(uid,weekend));
ALTER TABLE revora_planet_runs ADD COLUMN IF NOT EXISTS collection_step integer NOT NULL DEFAULT 0 CHECK(collection_step BETWEEN 0 AND 8);
ALTER TABLE revora_planet_runs ADD COLUMN IF NOT EXISTS reward_uids bigint[] NOT NULL DEFAULT '{}';
DO $planet$ BEGIN
IF NOT EXISTS(SELECT 1 FROM pg_constraint WHERE conrelid='revora_planet_runs'::regclass AND conname='revora_planet_collection_step_v2') THEN
ALTER TABLE revora_planet_runs DROP CONSTRAINT IF EXISTS revora_planet_runs_collection_step_check;
ALTER TABLE revora_planet_runs ADD CONSTRAINT revora_planet_collection_step_v2 CHECK(collection_step BETWEEN 0 AND 13);
END IF; END $planet$;
CREATE UNIQUE INDEX IF NOT EXISTS revora_planet_collection_steps ON revora_planet_runs(uid,collection_step) WHERE stage='complete' AND collection_step>0;");
            }
            PlanetCollectionExchange.Initialize();
            lock (ActiveGate) active = new ConditionalWeakTable<GameState, Run>();
            initialized = true;
        }
        private static Run Read(NpgsqlConnection c, NpgsqlTransaction t, uint uid, string weekend)
        {
            using (var q = Q.Command(c, t, @"SELECT id,weekend,stage,kills,accepted_at,starts_at,ends_at,collection_step
FROM revora_planet_runs WHERE uid=@uid AND weekend=@weekend FOR UPDATE", "uid", (long)uid, "weekend", weekend))
            using (var r = q.ExecuteReader()) return !r.Read() ? null : new Run {
                Id=r.GetGuid(0), Weekend=r.GetString(1), Stage=r.GetString(2), Kills=r.GetInt32(3),
                AcceptedUtc=r.GetFieldValue<DateTimeOffset>(4).UtcDateTime,
                StartUtc=r.GetFieldValue<DateTimeOffset>(5).UtcDateTime, EndUtc=r.GetFieldValue<DateTimeOffset>(6).UtcDateTime, CollectionStep=r.GetInt32(7)
            };
        }
        private static void Cache(GameState p, Run run)
        {
            lock (ActiveGate) {
                active.Remove(p);
                if (run != null && run.Stage == "hunt") active.Add(p, run);
            }
        }
        private static void Remove(GameState p, Guid expected)
        {
            lock (ActiveGate) {
                Run cached;
                if (active.TryGetValue(p,out cached) && cached.Id==expected) active.Remove(p);
            }
        }
        // The character lock serializes distinct-weekend admissions and final delivery.
        internal static Run Accept(GameState p)
        {
            if (!initialized || !Q.Player(p)) return null;
            lock (p.ItemSyncRoot) {
                if (!Q.Near(p, PathHuntSystem.NpcId, 1002) || p.Map.ID != 1002) return null;
                var now=Clock(); var window=Current(now); if (window == null) return null;
                using (var c=DataHolder.MySqlConnection) {
                    c.Open(); using (var t=c.BeginTransaction()) {
                        using (var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                            if (q.ExecuteScalar()==null) throw new InvalidOperationException("Planet character is missing.");
                        int step=CollectionCompleted(c,t,p.Entity.UID)+1;
                        if(step>CollectionLimit)return null;
                        var run=Read(c,t,p.Entity.UID,window.Key);
                        if (run==null) {
                            Guid id=Guid.NewGuid();
                            if (Q.Execute(c,t,@"INSERT INTO revora_planet_runs(id,uid,weekend,accepted_at,starts_at,ends_at,collection_step)
VALUES(@id,@uid,@weekend,@now,@start,@end,@step)","id",id,"uid",(long)p.Entity.UID,"weekend",window.Key,
                                "now",now,"start",window.StartUtc,"end",window.EndUtc,"step",step)!=1)
                                throw new InvalidOperationException("Planet admission was not saved.");
                            run=Read(c,t,p.Entity.UID,window.Key);
                            if (run==null || run.Id!=id) throw new InvalidOperationException("Planet admission receipt is missing.");
                        }
                        t.Commit(); Cache(p,run); return run;
                    }
                }
            }
        }
        internal static void OnLogin(GameState p)
        {
            if (!initialized || !Q.Player(p,false)) return;
            lock (p.ItemSyncRoot) {
                if (!Q.Player(p,false)) return;
                var window=Current(Clock()); if (window==null) {Cache(p,null);return;}
                try {
                    using (var c=DataHolder.MySqlConnection) {
                        c.Open(); using (var t=c.BeginTransaction()) {
                            var run=Read(c,t,p.Entity.UID,window.Key); t.Commit(); Cache(p,run);
                        }
                    }
                } catch (Exception ex) {Q.Fail(p,"planet-login",ex);}
            }
        }
        // Only the authoritative owned-monster single-death wrapper calls this counter-only seam.
        internal static void Credit(Entity killer, ushort map, ushort x, ushort y, uint type, DateTime occurred)
        {
            if (!initialized || map!=1015 || type!=18 || killer==null || killer.EntityFlag!=EntityFlag.Player
                || killer.Owner==null || !ReferenceEquals(killer.Owner.Entity,killer)) return;
            var p=killer.Owner; Run accepted;
            if (!Q.Player(p) || p.Entity.MapID!=map || p.Map.ID!=map
                || Kernel.GetDistance(p.Entity.X,p.Entity.Y,x,y)>Constants.pScreenDistance) return;
            lock (ActiveGate) if (!active.TryGetValue(p,out accepted)) return;
            var window=Current(occurred);
            if (window==null || accepted.Weekend!=window.Key || occurred>Clock()) return;
            try {
                using (var c=DataHolder.MySqlConnection) {
                    c.Open(); using (var t=c.BeginTransaction()) {
                        var run=Read(c,t,p.Entity.UID,window.Key);
                        if (run==null || run.Id!=accepted.Id || run.Stage!="hunt" || occurred<run.AcceptedUtc
                            || occurred<run.StartUtc || occurred>=run.EndUtc) return;
                        if (Q.Execute(c,t,@"UPDATE revora_planet_runs SET kills=kills+1,
stage=CASE WHEN kills=99 THEN 'encounter' ELSE 'hunt' END WHERE id=@id AND kills=@before AND stage='hunt'",
                            "id",run.Id,"before",run.Kills)!=1) throw new InvalidOperationException("Planet hunt progress was not saved.");
                        t.Commit();
                        if (run.Kills==99) {Remove(p,run.Id);p.Send("The Birdmen have scattered. Return to Rowan for the guarded cache.");}
                    }
                }
            } catch (Exception ex) {Q.Fail(p,"planet-hunt",ex);}
        }
        private static int Completed(NpgsqlConnection c,NpgsqlTransaction t,uint uid)
        {
            using(var q=Q.Command(c,t,"SELECT count(*) FROM revora_planet_completions WHERE uid=@uid","uid",(long)uid))
                return Convert.ToInt32(q.ExecuteScalar());
        }
        // Owner-approved recurring Revora collection edition; old admissions stay step zero.
        internal static uint[] CollectionRewards(int step) {
            if(step<1 || step>CollectionLimit)throw new ArgumentOutOfRangeException("step");
            var gifts=new List<uint>{step<=8?(uint)(200552+step):4030101u};
            if(step<=6)gifts.Add(new uint[]{193735,193745,193755,193765,192655,192665}[step-1]);
            if(step<=4)gifts.Add(new uint[]{194675,194685,194695,194705}[step-1]);
            if(step==1)gifts.Add(200595);
            foreach(uint id in gifts)if(!ConquerItemInformation.BaseInformations.ContainsKey(id))throw new InvalidOperationException("Planet collection catalog is missing.");
            return gifts.ToArray();
        }
        private static int CollectionCompleted(NpgsqlConnection c,NpgsqlTransaction t,uint uid) {
            using(var q=Q.Command(c,t,"SELECT COALESCE(max(collection_step),0) FROM revora_planet_runs WHERE uid=@uid AND stage='complete'","uid",(long)uid))return Convert.ToInt32(q.ExecuteScalar());
        }
        internal static bool Deliver(GameState p)
        {
            if(!initialized || !Q.Player(p))return false;
            lock(p.ItemSyncRoot) {
                if(!Q.Near(p,PathHuntSystem.NpcId,1002) || p.Map.ID!=1002)return false;
                var now=Clock();if(now.Kind!=DateTimeKind.Utc)throw new ArgumentException("Planet timestamps must be UTC.");
                now=new DateTime(now.Ticks/10*10,DateTimeKind.Utc);
                var window=Current(now);if(window==null)return false;
                int count;ConquerItem[] gifts=new ConquerItem[0];
                using(var c=DataHolder.MySqlConnection) {
                    c.Open();using(var t=c.BeginTransaction()) {
                        using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                            if(q.ExecuteScalar()==null)throw new InvalidOperationException("Planet character is missing.");
                        var run=Read(c,t,p.Entity.UID,window.Key);
                        if(run==null || run.Stage!="delivery" || now<run.AcceptedUtc || now>=run.EndUtc)return false;
                        Guid attempt;
                        using(var q=Q.Command(c,t,@"SELECT id FROM revora_planet_attempts
WHERE run=@run AND uid=@uid AND state='victory' AND defeated=7 AND returned
AND cache_at>=@accepted AND cache_at<=@now AND cache_at<@end FOR UPDATE",
                            "run",run.Id,"uid",(long)p.Entity.UID,"accepted",run.AcceptedUtc,"now",now,"end",run.EndUtc)) {
                            object saved=q.ExecuteScalar();if(saved==null)throw new InvalidOperationException("Planet cache proof is missing.");attempt=(Guid)saved;
                        }
                        if(run.CollectionStep>0) {
                            if(run.CollectionStep!=CollectionCompleted(c,t,p.Entity.UID)+1)throw new InvalidOperationException("Planet collection sequence changed.");
                            uint[] types=CollectionRewards(run.CollectionStep);
                            // Check both durable inventory and the canonical session before minting.
                            var owned=new List<uint>();
                            using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid FOR UPDATE","uid",(long)p.Entity.UID))
                            using(var r=q.ExecuteReader())while(r.Read())owned.Add(Convert.ToUInt32(r[0]));
                            if(!owned.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException("Planet inventory changed.");
                            if(owned.Count+types.Length>40){p.Send("Make "+types.Length+" spaces for your expedition gifts, then return before Monday.");return false;}
                            gifts=types.Select(id=>Q.New(id,true)).ToArray();
                            foreach(var gift in gifts)Q.Insert(c,t,p.Entity.UID,gift);
                        }
                        if(Q.Execute(c,t,@"INSERT INTO revora_planet_completions(uid,weekend,run,attempt,completed_at)
VALUES(@uid,@weekend,@run,@attempt,@now)","uid",(long)p.Entity.UID,"weekend",window.Key,"run",run.Id,"attempt",attempt,"now",now)!=1)
                            throw new InvalidOperationException("Planet completion was not saved.");
                        if(Q.Execute(c,t,"UPDATE revora_planet_runs SET stage='complete',reward_uids=@gifts WHERE id=@run AND stage='delivery' AND cardinality(reward_uids)=0","run",run.Id,"gifts",gifts.Select(i=>(long)i.UID).ToArray())!=1)
                            throw new InvalidOperationException("Planet delivery was not acknowledged.");
                        count=Completed(c,t,p.Entity.UID);
                        if(count==4) {
                            if(StorageManager.Wing<int>(6009,6009)!=60096009)throw new InvalidOperationException("Planet wing catalog changed.");
                            ProgressionEvidence.Grant(c,t,p.Entity.UID,"planet-revora-v1","four-weekends-v1",60096009,now);
                            using(var q=Q.Command(c,t,@"SELECT EXISTS(SELECT 1 FROM revora_event_awards
WHERE uid=@uid AND source='planet-revora-v1' AND source_key='four-weekends-v1'
AND title_key=60096009 AND earned_at=@now AND expires_at=@expiry)","uid",(long)p.Entity.UID,"now",now,"expiry",now.AddDays(30)))
                                if(!Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("Planet wing award was not saved.");
                        }
                        t.Commit();
                    }
                }
                Q.Install(p,new uint[0],gifts);Cache(p,null);Q.Refresh(p);
                p.Send(count==4?"Four expeditions, well earned. Your Planet wings are yours for thirty days.":"The cache is safe. Your expedition gifts are yours. Return another weekend for the next designs.");
                return true;
            }
        }
        internal static void Offer(Npcs dialog)
        {
            if(initialized){dialog.Option("Planet expedition.",210);dialog.Option("Exchange expedition credits.",220);}
        }
        internal static string Status(GameState p)
        {
            if(!initialized || !Q.Player(p) || !Q.Near(p,PathHuntSystem.NpcId,1002))return null;
            using(var c=DataHolder.MySqlConnection) {
                c.Open();using(var t=c.BeginTransaction()) {
                    int collected=CollectionCompleted(c,t,p.Entity.UID);
                    if(collected>=CollectionLimit){t.Commit();return "Your expedition ledger is complete. Spend your remaining credits with me.";}
                    var window=Current(Clock());var run=window==null?null:Read(c,t,p.Entity.UID,window.Key);t.Commit();
                    string prefix="Collection deliveries: "+collected+"/"+CollectionLimit+". ";
                    if(window==null)return prefix+"The passage opens on Saturday and Sunday, Pacific time.";
                    if(run==null)return prefix+"Hunt 100 Birdmen on Bird Island, recover the guarded cache, then bring it to me before Monday.";
                    if(run.Stage=="hunt")return prefix+"Birdmen: "+run.Kills+"/100. Finish the hunt, then return for the guarded cache.";
                    if(run.Stage=="encounter")return prefix+"Your hunt is done. Defeat the three sentries and recover their cache.";
                    if(run.Stage=="delivery")return prefix+"You carry the cache. Hand it over before the weekend ends.";
                    return prefix+"This weekend's cache is safe. Return another weekend.";
                }
            }
        }
        internal static bool HandleNpc(GameState p,NpcRequest request)
        {
            if(initialized && PlanetCollectionExchange.HandleNpc(p,request))return true;
            if(!initialized || request==null || request.OptionID<210 || request.OptionID>214
                || p==null || p.ActiveNpc!=PathHuntSystem.NpcId)return false;
            if(!Q.Player(p))return true;
            lock(p.ItemSyncRoot)try {
                if(!Q.Near(p,PathHuntSystem.NpcId,1002) || p.Map.ID!=1002)return true;
                if(request.OptionID==211)Accept(p);
                else if(request.OptionID==212 && PlanetEncounter.Enter(p))return true;
                else if(request.OptionID==213)Deliver(p);
                if(!Q.Player(p))return true;
                var d=new Npcs(p);
                d.Text(request.OptionID==214?"Complete one expedition on each of four different Pacific weekends. Your Planet wings last thirty days from the fourth delivery. One wing award per adventurer. The first eight deliveries earn collection gifts. Five more earn runes. Each delivery grants twenty collection credits.":Status(p));
                if(request.OptionID!=214) {
                    d.Option("Take the weekend hunt.",211);d.Option("Enter the guarded passage.",212);
                    d.Option("Deliver the cache.",213);d.Option("About the wings.",214);
                }
                d.Option("Farewell.",255);d.Send();
            }catch(Exception ex){Q.Fail(p,"planet-rowan",ex);}return true;
        }
        private static readonly TimeZoneInfo Zone=TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
        internal sealed class Window
        {
            internal string Key;
            internal DateTime StartUtc,EndUtc;
            internal bool Contains(DateTime utc)
            {
                if(utc.Kind!=DateTimeKind.Utc)throw new ArgumentException("Planet timestamps must be UTC.");
                return utc>=StartUtc && utc<EndUtc;
            }
        }
        internal static Window Current(DateTime utc)
        {
            if(utc.Kind!=DateTimeKind.Utc)throw new ArgumentException("Planet timestamps must be UTC.");
            DateTime local=TimeZoneInfo.ConvertTimeFromUtc(utc,Zone);
            if(local.DayOfWeek!=DayOfWeek.Saturday && local.DayOfWeek!=DayOfWeek.Sunday)return null;
            DateTime saturday=local.Date.AddDays(local.DayOfWeek==DayOfWeek.Sunday?-1:0);
            return new Window {
                Key=saturday.ToString("yyyyMMdd",CultureInfo.InvariantCulture),
                StartUtc=TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(saturday,DateTimeKind.Unspecified),Zone),
                EndUtc=TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(saturday.AddDays(2),DateTimeKind.Unspecified),Zone)
            };
        }
    }
}

namespace Revora.Game.Features
{
    internal static class PlanetCollectionExchange
    {
        // Frozen numeric stock; highest-star unsupplied designs first, then catalog ID.
        internal static readonly uint[] Garments = {189135u,193055u,193275u,187415u,187775u,187785u,187795u,188095u,188185u,188495u,188515u,188625u,188715u,188755u,188845u,189085u,189145u,189265u,189645u,192135u,192311u,192335u,192365u,192465u,192565u,192575u,192605u,192615u,192635u,192895u,192925u,192935u,193195u,193205u,193225u,193295u,193325u,193685u,193695u,193785u,193795u,193805u,193815u,193825u,194385u,194405u,194715u,194725u,194885u,194895u,194905u,194915u,194925u,194935u,194945u,195045u,183465u,184315u,184345u,184385u,184405u,187325u,187425u,187515u,187665u,187675u,187685u,187735u,187765u,187855u,187865u,187875u,187885u,187905u,187915u,187925u,187945u,187955u,187965u,187975u,187985u,188035u,188065u,188085u,188105u,188115u,188155u,188205u,188225u,188245u,188255u,188265u,188285u,188295u,188405u,188545u,188575u,188655u,188675u,188705u,188725u,188885u,188895u,188905u,188925u,188935u,188945u,188955u};
        internal static readonly uint[] Mounts = {200021u,200022u,200220u,200221u,200410u,200428u,200525u,200453u,200471u,200477u,200478u,200479u,200480u,200485u,200486u,200495u,200549u,200589u,200590u,200591u,200593u,200618u,200619u,200620u,200009u,200010u,200107u,200108u,200403u,200409u,200425u,200426u,200433u,200437u,200439u,200440u,200441u,200443u,200445u,200446u,200447u,200454u,200455u,200456u,200458u,200459u,200461u,200463u,200621u,200622u,200464u,200465u,200466u,200474u,200475u,200476u,200487u,200488u,200490u,200491u,200492u,200493u,200503u,200514u,200515u,200516u,200527u,200528u,200529u,200530u,200541u,200542u,200562u,200584u,200585u,200586u,200587u,200588u,200002u,200003u,200005u,200007u,200008u,200011u,200012u,200100u,200103u,200104u,200105u,200106u,200109u,200110u,200111u,200208u,200209u,200217u,200218u,200219u,200307u,200308u,200311u,200312u,200319u,200320u,200321u,200322u,200323u,200467u,200538u,200539u,200000u,200001u,200004u,200006u,200015u,200016u,200017u,200018u,200019u,200020u,200101u,200102u,200120u,200121u,200122u,200123u,200124u,200125u,200126u,200200u,200201u,200202u,200203u,200204u,200205u,200206u,200207u,200210u,200211u,200300u,200301u,200302u,200303u,200304u,200305u,200306u,200309u,200310u,200547u};
        internal static Action BeforeCommit = () => {};
        private static uint[] Stock { get { return Garments.Concat(Mounts).ToArray(); } }
        internal static void Initialize()
        {
            if(Garments.Length!=108 || Mounts.Length!=149 || Stock.Distinct().Count()!=257)
                throw new InvalidOperationException("Expedition exchange stock changed.");
            foreach(uint id in Garments)if(!CollectionAchievementCatalog.Garments.Contains(id) || !StorageItem.Garments.ContainsKey(id)
                || StorageItem.Garments[id].ID!=id || StorageItem.Garments[id].Type!=1 || !ConquerItemInformation.BaseInformations.ContainsKey(id))
                throw new InvalidOperationException("Expedition garment unavailable.");
            foreach(uint id in Mounts)if(!CollectionAchievementCatalog.Mounts.Contains(id) || !StorageItem.Mounts.ContainsKey(id)
                || StorageItem.Mounts[id].ID!=id || StorageItem.Mounts[id].Type!=2 || !ConquerItemInformation.BaseInformations.ContainsKey(id))
                throw new InvalidOperationException("Expedition mount unavailable.");
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_planet_exchange(
uid bigint NOT NULL CHECK(uid>0),design bigint NOT NULL CHECK(design>0),item_uid bigint NOT NULL UNIQUE,
claimed_at timestamptz NOT NULL,rule_version integer NOT NULL DEFAULT 1 CHECK(rule_version=1),PRIMARY KEY(uid,design));");}
        }
        private static int Balance(NpgsqlConnection c,NpgsqlTransaction t,uint uid)
        {
            using(var q=Q.Command(c,t,@"SELECT
20*(SELECT count(DISTINCT r.collection_step) FROM revora_planet_runs r
JOIN revora_planet_completions c ON c.run=r.id AND c.uid=r.uid AND c.weekend=r.weekend
WHERE r.uid=@uid AND r.stage='complete' AND r.kills=100 AND r.collection_step BETWEEN 1 AND 13
AND c.completed_at>=r.starts_at AND c.completed_at<r.ends_at)
-(SELECT count(*) FROM revora_planet_exchange WHERE uid=@uid)","uid",(long)uid))return Convert.ToInt32(q.ExecuteScalar());
        }
        internal static string Buy(GameState p,int index)
        {
            if(index<0 || index>=Stock.Length || !Q.Near(p,PathHuntSystem.NpcId,1002))return "Speak with Rowan to choose your gift.";
            if(!SessionInstall.TryEnter(p.Entity.UID))return "Please try again in a moment.";
            try {lock(p.ItemSyncRoot) {
                if(!Q.Near(p,PathHuntSystem.NpcId,1002))return "Speak with Rowan to choose your gift.";
                uint type=Stock[index];ConquerItem gift;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    Q.Execute(c,t,"SET LOCAL lock_timeout='2s';SET LOCAL statement_timeout='5s'");
                    using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                        if(q.ExecuteScalar()==null)throw new InvalidOperationException("Exchange character is missing.");
                    using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_planet_exchange WHERE uid=@uid AND design=@type)","uid",(long)p.Entity.UID,"type",(long)type))
                        if(Equals(q.ExecuteScalar(),true))return "You have already claimed that design.";
                    using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM items WHERE entityid=@uid AND id=@type AND days::text='0')","uid",(long)p.Entity.UID,"type",(long)type))
                        if(Equals(q.ExecuteScalar(),true))return "You already own that permanent design. Choose another.";
                    if(Balance(c,t,p.Entity.UID)<1)return "Complete a Planet collection delivery to earn twenty credits.";
                    var bag=new List<uint>();
                    using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid FOR UPDATE","uid",(long)p.Entity.UID))
                    using(var r=q.ExecuteReader())while(r.Read())bag.Add(Convert.ToUInt32(r[0]));
                    if(!bag.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException("Exchange inventory changed.");
                    if(bag.Count>=40)return "Make one space in your bag first.";
                    gift=Q.New(type,true);Q.Insert(c,t,p.Entity.UID,gift);
                    if(Q.Execute(c,t,"INSERT INTO revora_planet_exchange(uid,design,item_uid,claimed_at) VALUES(@uid,@type,@item,@now)",
                        "uid",(long)p.Entity.UID,"type",(long)type,"item",(long)gift.UID,"now",DateTime.UtcNow)!=1)
                        throw new InvalidOperationException("Exchange receipt was not saved.");
                    BeforeCommit();
                    if(!Q.Near(p,PathHuntSystem.NpcId,1002))throw new InvalidOperationException("Exchange owner changed.");
                    t.Commit();
                }}
                Q.Install(p,new uint[0],new[]{gift});Q.Refresh(p);return "Your gift is yours. One expedition credit spent.";
            }}catch(Exception ex){Q.Fail(p,"planet-exchange",ex);return "Your records need checking. Reconnect before trying again.";}
            finally{SessionInstall.Exit(p.Entity.UID);}
        }
        private sealed class CatalogView { internal int Offset; }
        private static readonly ConditionalWeakTable<GameState,CatalogView> catalogViews=new ConditionalWeakTable<GameState,CatalogView>();
        internal static bool HandleNpc(GameState p,NpcRequest request)
        {
            if(p==null || request==null || p.ActiveNpc!=PathHuntSystem.NpcId)return false;
            lock(p.ItemSyncRoot){return HandleCatalog(p,request);}
        }
        private static bool HandleCatalog(GameState p,NpcRequest request)
        {
            int option=request.OptionID;
            bool buy=option>=40 && option<196;
            if(!buy && (option<220 || option>247))return false;
            if(!Q.Near(p,PathHuntSystem.NpcId,1002))return true;
            var view=catalogViews.GetValue(p,_=>new CatalogView());
            if(option==247)view.Offset=0;
            if(option==246)view.Offset=156;
            int page=option>=246?0:buy?(option-40)/6:option-220;
            int count=Math.Min(156,Stock.Length-view.Offset);
            if(page<0 || page*6>=count || buy && option-40>=count)return true;
            string result=buy?Buy(p,view.Offset+option-40):null;
            if(!Q.Player(p))return true;
            int credits;
            using(var c=DataHolder.MySqlConnection){c.Open();credits=Balance(c,null,p.Entity.UID);}
            var d=new Npcs(p);d.Text((result==null?"":result+"~")+"Expedition credits: "+credits+". Each design costs one; gifts are permanent and bound.");
            var stock=Stock;
            for(int i=page*6;i<Math.Min(page*6+6,count);i++)
                d.Option(ConquerItemInformation.BaseInformations[stock[view.Offset+i]].Name,(byte)(40+i));
            if(page>0)d.Option("Previous designs.",(byte)(219+page));
            if((page+1)*6<count)d.Option("More designs.",(byte)(221+page));
            d.Option(view.Offset==0?"More mount collections.":"Original collections.",view.Offset==0?(byte)246:(byte)247);
            d.Option("Farewell.",255);d.Send();return true;
        }
    }
}
