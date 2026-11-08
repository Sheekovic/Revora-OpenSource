using System;
using System.Linq;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network;
using Revora.Network.GamePackets;
using ContactPacket = Revora.Network.GamePackets.KnownPersons;
using FriendStore = Revora.Database.KnownPersons;

internal static partial class PotencyHuntTests
{
    static readonly Type FriendService = typeof(Entity).Assembly.GetType("Revora.Game.Features.Friendships");
    static DateTime friendNow = new DateTime(2030, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    static GameState FriendPlayer(Map map)
    {
        var p = ProgressPlayer(map, ""); FriendStore.LoaderFriends(p); return p;
    }
    static byte[] FriendFrame(byte action, uint uid)
    {
        var bytes = new ContactPacket(true) { Type = action, UID = uid, Name = "untrusted" }.ToArray();
        Writer.WriteUInt64(PacketHandler.ClientSeal, bytes.Length - 8, bytes); return bytes;
    }
    static void FriendSend(GameState p, byte action, GameState peer)
    { ContactPacket.Process(FriendFrame(action, peer.Entity.UID), p); }
    static long FriendRows(GameState p)
    { return Scalar("SELECT count(*) FROM friends WHERE entityid=@uid", p.Entity.UID); }
    static long FriendAwards(GameState p)
    { return Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE achievement_id=10123 AND uid=@uid", p.Entity.UID); }
    static void MarriageSend(GameState p,GameState peer,uint action) {
        new Revora.Game.Attacking.Handle(new Attack(true){AttackType=action,Attacker=p.Entity.UID,Attacked=peer.Entity.UID},p.Entity,null);
    }
    static void VerifyMarriages(Map map) {
        Query("ALTER TABLE entities ADD COLUMN IF NOT EXISTS spouse varchar(32) NOT NULL DEFAULT 'None', ADD COLUMN IF NOT EXISTS x integer NOT NULL DEFAULT 0");
        var service=typeof(Entity).Assembly.GetType("Revora.Game.Features.Marriages");
        DateTime now=DateTime.UtcNow;var clock=service.GetField("Clock",Hidden);var prior=clock.GetValue(null);clock.SetValue(null,new Func<DateTime>(()=>now));
        try {foreach(string mode in new[]{"normal","unsolicited","expired","distant","same-sex","stale-session","spoofed","third-party","stale-db","link-failure","receipt-failure"}) {
            var a=FriendPlayer(map);var b=FriendPlayer(map);a.Entity.Spouse=b.Entity.Spouse="None";a.Entity.Body=1003;b.Entity.Body=2001;
            a.Entity.Name="Groom"+a.Entity.UID;b.Entity.Name="Bride"+b.Entity.UID;
            Query("UPDATE entities SET name=@name WHERE uid=@uid","uid",(long)a.Entity.UID,"name",a.Entity.Name);
            Query("UPDATE entities SET name=@name WHERE uid=@uid","uid",(long)b.Entity.UID,"name",b.Entity.Name);
            if(mode!="unsolicited")MarriageSend(a,b,Attack.MarriageRequest);
            if(mode=="expired")now=now.AddMinutes(3);
            if(mode=="distant")b.Entity.X=(ushort)(a.Entity.X+20);
            if(mode=="same-sex")b.Entity.Body=1004;
            if(mode=="stale-session")Kernel.GamePool[a.Entity.UID]=FriendPlayer(map);
            if(mode=="third-party"){var c=FriendPlayer(map);c.Entity.Body=1003;c.Entity.Spouse="None";MarriageSend(c,b,Attack.MarriageRequest);}
            if(mode=="stale-db")Query("UPDATE entities SET spouse='Existing' WHERE uid=@uid","uid",(long)a.Entity.UID);
            if(mode=="link-failure")Query("CREATE FUNCTION suppress_marriage_link() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_marriage_link BEFORE UPDATE ON entities FOR EACH ROW EXECUTE FUNCTION suppress_marriage_link()");
            if(mode=="receipt-failure")Query("CREATE FUNCTION suppress_marriage_receipt() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN IF NEW.achievement_id=10129 THEN RETURN NULL; END IF; RETURN NEW; END';CREATE TRIGGER suppress_marriage_receipt BEFORE INSERT ON revora_achievement_evidence FOR EACH ROW EXECUTE FUNCTION suppress_marriage_receipt()");
            try{Parallel.For(0,4,n=>{
                if(mode=="spoofed")new Revora.Game.Attacking.Handle(new Attack(true){AttackType=Attack.MarriageAccept,Attacker=a.Entity.UID,Attacked=a.Entity.UID},b.Entity,null);
                else MarriageSend(b,a,Attack.MarriageAccept);
            });}
            finally{
                if(mode=="link-failure")Query("DROP TRIGGER suppress_marriage_link ON entities;DROP FUNCTION suppress_marriage_link()");
                if(mode=="receipt-failure")Query("DROP TRIGGER suppress_marriage_receipt ON revora_achievement_evidence;DROP FUNCTION suppress_marriage_receipt()");
            }
            bool married=mode=="normal";
            Check((a.Entity.Spouse==b.Entity.Name && b.Entity.Spouse==a.Entity.Name)==married
                && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10129",a.Entity.UID)==(married?1:0)
                && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10129",b.Entity.UID)==(married?1:0),
                "mutual marriage and both achievements require valid committed consent: "+mode);
            Check(Scalar("SELECT count(*) FROM entities a JOIN entities b ON a.spouse=b.name AND b.spouse=a.name WHERE a.uid=@uid",a.Entity.UID)==(married?1:0),
                "both saved spouse directions commit together: "+mode);
            if(married){var restored=new Achievement(a.Entity);restored.Load("");Call(typeof(Achievement),restored,"Refresh",DateTime.UtcNow,true);
                int position=(10129/100-101)*32+10129%100-1;
                Check((System.BitConverter.ToUInt32(restored.ToArray(),16+position/32*4)&(1u<<(position%32)))!=0,"production achievement loader restores marriage completion");}
            now=now.AddSeconds(3);
        }}finally{clock.SetValue(null,prior);}
    }
    static void VerifyMarriageRaces(Map map) {
        var service=typeof(Entity).Assembly.GetType("Revora.Game.Features.Marriages");var clock=service.GetField("Clock",Hidden);var prior=clock.GetValue(null);
        long ticks=DateTime.UtcNow.Ticks;clock.SetValue(null,new Func<DateTime>(()=>new DateTime(System.Threading.Interlocked.Read(ref ticks),DateTimeKind.Utc)));
        Func<bool,GameState> create=male=>{var p=FriendPlayer(map);p.Entity.Body=male?(ushort)1003:(ushort)2001;p.Entity.Spouse="None";
            p.Entity.Name="Spouse"+p.Entity.UID;Query("UPDATE entities SET name=@name WHERE uid=@uid","name",p.Entity.Name,"uid",(long)p.Entity.UID);return p;};
        try {
            var a=create(true);var b=create(false);MarriageSend(a,b,Attack.MarriageRequest);
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                using(var q=new Npgsql.NpgsqlCommand("SELECT uid FROM entities WHERE uid=@uid FOR UPDATE",c,t)){q.Parameters.AddWithValue("uid",(long)a.Entity.UID);q.ExecuteScalar();}
                var attempt=Task.Run(()=>MarriageSend(b,a,Attack.MarriageAccept));
                bool blocked=System.Threading.SpinWait.SpinUntil(()=>Scalar("SELECT count(*) FROM pg_stat_activity WHERE pid<>pg_backend_pid() AND wait_event_type='Lock' AND query LIKE '%SELECT uid FROM entities WHERE uid=%' AND @uid>0",a.Entity.UID)>0,5000);
                System.Threading.Interlocked.Add(ref ticks,TimeSpan.FromMinutes(3).Ticks);t.Commit();
                Check(attempt.Wait(5000) && blocked && a.Entity.Spouse=="None" && b.Entity.Spouse=="None" && !Fenced(a) && !Fenced(b)
                    && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10129",a.Entity.UID)==0,
                    "marriage consent expires while waiting for database owner locks without partial links");
            }}
            a=create(true);b=create(false);MarriageSend(a,b,Attack.MarriageRequest);MarriageSend(b,a,Attack.MarriageRequest);
            var cross=Task.Run(()=>Parallel.Invoke(()=>MarriageSend(a,b,Attack.MarriageAccept),()=>MarriageSend(b,a,Attack.MarriageAccept)));
            Check(cross.Wait(5000) && a.Entity.Spouse==b.Entity.Name && b.Entity.Spouse==a.Entity.Name
                && new[]{a,b}.All(p=>Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10129",p.Entity.UID)==1),
                "crossing mutual proposals serialize without deadlock and persist one pair");
            a=create(true);b=create(false);var third=create(false);MarriageSend(a,b,Attack.MarriageRequest);
            System.Threading.Interlocked.Add(ref ticks,TimeSpan.FromSeconds(2).Ticks);MarriageSend(a,third,Attack.MarriageRequest);
            var contenders=Task.Run(()=>Parallel.Invoke(()=>MarriageSend(b,a,Attack.MarriageAccept),()=>MarriageSend(third,a,Attack.MarriageAccept)));
            Check(contenders.Wait(5000) && new[]{b,third}.Count(p=>p.Entity.Spouse==a.Entity.Name)==1
                && new[]{b,third}.Count(p=>p.Entity.Spouse=="None")==1
                && Scalar("SELECT count(*) FROM entities x JOIN entities y ON x.spouse=y.name AND y.spouse=x.name WHERE x.uid=@uid",a.Entity.UID)==1,
                "competing acceptances can establish only one reciprocal marriage");
        }finally{clock.SetValue(null,prior);}
    }
    static void VerifyFriends(Map map)
    {
        Query("ALTER TABLE entities ADD COLUMN level integer NOT NULL DEFAULT 1;CREATE TABLE titles (LIKE public.titles INCLUDING ALL);CREATE TABLE friends (LIKE public.friends INCLUDING ALL)");
        StorageManager.Load(); StorageItem.Load(); PerfectionManager.AbilityScore.Read(out PerfectionManager.AbilitiesScore);
        Call(EvidenceType, null, "Initialize"); Call(FriendService, null, "Initialize");
        FriendService.GetField("Clock", Hidden).SetValue(null, new Func<DateTime>(() => friendNow));
        var a = FriendPlayer(map); var b = FriendPlayer(map); var stranger = FriendPlayer(map);
        Check(a.Account.State == AccountTable.AccountState.Player && b.Account.State == AccountTable.AccountState.Player && !a.Fake && !b.Fake,
            "friend fixtures use ordinary authenticated players");
        FriendSend(b, 11, a);
        Check(FriendRows(a) == 0 && FriendAwards(b) == 0, "unsolicited acceptance cannot add or award friends");
        FriendSend(a, 10, b); FriendSend(stranger, 11, a);
        Check(FriendRows(a) == 0 && FriendAwards(a) == 0 && FriendAwards(stranger) == 0, "request alone and a third-party acceptance cannot earn the achievement");
        var info = (RequestInformation)Call(FriendService, null, "InvitationInfo", a, b);
        Check(info.Requester == a.Entity.UID && info.Receiver == b.Entity.UID, "invitation identifies the actual requester and receiver");
        a.Entity.Body = 1003; b.Entity.Body = 2001;
        var contact = (ContactPacket)Call(FriendService, null, "Contact", b, (byte)11);
        Check(!contact.IsBoy && contact.UID == b.Entity.UID, "accepted contact uses the subject's identity and sex");
        contact.Name = new string('N', 1000);
        Check(contact.ToArray().Skip(36).All(i => i == 0), "long outgoing names cannot overwrite the remaining contact packet");
        FriendSend(b, 11, a);
        Check(!Fenced(a) && !Fenced(b) && FriendRows(a) == 1 && FriendRows(b) == 1 && a.Friends.ContainsKey(b.Entity.UID) && b.Friends.ContainsKey(a.Entity.UID),
            "consented friendship commits both directions and immediately refreshes both caches");
        Check(FriendAwards(a) == 1 && FriendAwards(b) == 1 && HasAchievement(a, 10123) && HasAchievement(b, 10123),
            "both players receive durable and immediately visible friend achievements");
        var repeated = Enumerable.Range(0, 12).Select(i => Task.Run(() => FriendSend(b, 11, a))).ToArray(); Task.WaitAll(repeated);
        Check(FriendRows(a) == 1 && FriendRows(b) == 1 && FriendAwards(a) == 1 && Scalar("SELECT count(*) FROM revora_friend_consents WHERE requester=@uid", a.Entity.UID) == 1,
            "parallel acceptance replay cannot duplicate friend records or evidence");
        FriendSend(a, 14, b); FriendSend(b, 11, a);
        Check(FriendRows(a) == 0 && FriendRows(b) == 0 && a.Friends.Count == 0 && b.Friends.Count == 0 && FriendAwards(a) == 1,
            "removal deletes both directions, retains earned achievement and invalidates old consent");
        friendNow = friendNow.AddSeconds(2); FriendSend(a, 10, b); FriendSend(b, 11, a);
        GameState removed; Kernel.GamePool.TryRemove(b.Entity.UID, out removed); FriendSend(a, 14, b);
        Kernel.GamePool[b.Entity.UID] = b; FriendStore.LoaderFriends(b); EvidenceRefresh(b);
        Check(FriendRows(b) == 0 && b.Friends.Count == 0 && HasAchievement(b, 10123), "offline removal survives the real login loader without revoking earned achievement");

        var c = FriendPlayer(map); var d = FriendPlayer(map);
        FriendSend(c, 10, d); friendNow = friendNow.AddMinutes(2); FriendSend(d, 11, c);
        Check(FriendRows(c) == 0 && FriendAwards(d) == 0, "expired invitation cannot create friendship");
        FriendSend(c, 10, d); FriendSend(d, 10, c);
        Task.WaitAll(Task.Run(() => FriendSend(c, 11, d)), Task.Run(() => FriendSend(d, 11, c)));
        Check(!Fenced(c) && !Fenced(d) && FriendRows(c) == 1 && FriendRows(d) == 1, "crossed requests use consistent owner lock order and one mutual consent");
        var malformedOwner = FriendPlayer(map); var malformedPeer = FriendPlayer(map);
        foreach (int size in new[] { 0, 1, 3, 8, 9, 20, 35, 43, 129, 65536 }) ContactPacket.Process(new byte[size], malformedOwner);
        foreach (int offset in new[] { 0, 2, 72 }) { var bytes = FriendFrame(10, malformedPeer.Entity.UID); bytes[offset]++; ContactPacket.Process(bytes, malformedOwner); }
        FriendSend(malformedPeer, 11, malformedOwner); FriendSend(malformedOwner, 10, malformedOwner); FriendSend(malformedOwner, 255, malformedPeer);
        Check(!Fenced(malformedOwner) && FriendRows(malformedOwner) == 0, "malformed frames, self requests and unknown actions cannot create consent");
        malformedOwner.Fake = true; FriendSend(malformedOwner, 10, malformedPeer); malformedOwner.Fake = false; FriendSend(malformedPeer, 11, malformedOwner);
        malformedOwner.Entity.FullyLoaded = false; FriendSend(malformedOwner, 10, malformedPeer); malformedOwner.Entity.FullyLoaded = true; FriendSend(malformedPeer, 11, malformedOwner);
        Check(FriendRows(malformedOwner) == 0, "fake and incompletely loaded players cannot request friendship");

        foreach (string failure in new[] { "first-direction", "second-direction", "consent", "evidence", "silent-evidence", "silent-direction", "silent-remove" })
        {
            var x = FriendPlayer(map); var y = FriendPlayer(map); FriendSend(x, 10, y);
            bool removing = failure == "silent-remove"; if (removing) FriendSend(y, 11, x);
            string table = failure == "consent" ? "revora_friend_consents" : failure.EndsWith("evidence") ? "revora_achievement_evidence" : "friends";
            string predicate = failure == "first-direction" ? "NEW.entityid=" + Math.Min(x.Entity.UID, y.Entity.UID) : failure == "second-direction" ? "NEW.entityid=" + Math.Max(x.Entity.UID, y.Entity.UID) : "TRUE";
            string effect = failure.StartsWith("silent") ? "RETURN NULL;" : "RAISE EXCEPTION 'fixture friend failure';";
            Query("CREATE FUNCTION fail_friend() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN IF " + predicate + " THEN " + effect.Replace("'", "''") + " END IF; RETURN NEW; END';CREATE TRIGGER fail_friend BEFORE " + (removing ? "DELETE" : "INSERT") + " ON " + table + " FOR EACH ROW EXECUTE FUNCTION fail_friend()");
            if (removing) FriendSend(x, 14, y); else FriendSend(y, 11, x);
            Check(Fenced(x) && Fenced(y) && FriendRows(x) == (removing ? 1 : 0) && FriendRows(y) == (removing ? 1 : 0)
                && FriendAwards(x) == (removing ? 1 : 0) && x.Friends.Count == (removing ? 1 : 0), "atomic friend rollback and stale-session fence: " + failure);
            Query("DROP TRIGGER fail_friend ON " + table + ";DROP FUNCTION fail_friend()");
            DemonReset(x); DemonReset(y); FriendStore.LoaderFriends(x); FriendStore.LoaderFriends(y);
            if (removing) FriendSend(x, 14, y); else FriendSend(y, 11, x);
            Check(!Fenced(x) && !Fenced(y) && FriendRows(x) == (removing ? 0 : 1) && FriendRows(y) == (removing ? 0 : 1), "friend operation retries safely after reload: " + failure);
        }
        var legacy = FriendPlayer(map); var repair = FriendPlayer(map);
        Query("INSERT INTO friends(entityid,friendid,friendname,message) VALUES(@a,@b,'Legacy','keep')", "a", (long)legacy.Entity.UID, "b", (long)repair.Entity.UID);
        FriendStore.LoaderFriends(legacy);
        Check(FriendAwards(legacy) == 0 && legacy.Friends[repair.Entity.UID].Message == "keep", "loading a legacy row does not invent achievement evidence");
        FriendSend(legacy, 10, repair); FriendSend(repair, 11, legacy);
        Check(FriendRows(legacy) == 1 && FriendRows(repair) == 1 && FriendAwards(legacy) == 1 && legacy.Friends[repair.Entity.UID].Message == "keep",
            "fresh mutual consent repairs a one-sided legacy pair while preserving its message");
        VerifyFriendLimitsAndSessions(map);
    }
    static void VerifyFriendLimitsAndSessions(Map map)
    {
        var a = FriendPlayer(map); var b = FriendPlayer(map); FriendSend(a, 10, b);
        var replacement = FriendPlayer(map); Kernel.GamePool.TryRemove(replacement.Entity.UID, out replacement);
        replacement.Entity.UID = a.Entity.UID; replacement.Account.EntityID = a.Entity.UID; Kernel.GamePool[a.Entity.UID] = replacement;
        FriendSend(b, 11, replacement);
        Check(FriendRows(b) == 0, "sender reconnection cannot inherit a pending invitation from the previous session");
        friendNow = friendNow.AddMinutes(2); FriendSend(replacement, 10, b); FriendSend(b, 11, replacement);
        Check(FriendRows(b) == 1 && FriendAwards(replacement) == 1, "a fresh request from the new canonical session can be accepted");
        var full = FriendPlayer(map); var applicant = FriendPlayer(map);
        Query("INSERT INTO friends(entityid,friendid,friendname,message) SELECT @uid,90000000+n,'Fixture','' FROM generate_series(1,50) n", "uid", (long)full.Entity.UID);
        FriendStore.LoaderFriends(full); FriendSend(applicant, 10, full); FriendSend(full, 11, applicant);
        Check(FriendRows(full) == 50 && FriendRows(applicant) == 0 && FriendAwards(applicant) == 0, "durable capacity on either list prevents half-friendships and awards");
        Query("DELETE FROM friends WHERE entityid=@uid AND friendid=90000001", "uid", (long)full.Entity.UID);
        FriendSend(full, 11, applicant);
        Check(FriendRows(full) == 50 && FriendRows(applicant) == 1 && !Fenced(full), "unexpired consent can be accepted after a slot is available");
        var receiver = FriendPlayer(map); var senders = Enumerable.Range(0, 17).Select(i => FriendPlayer(map)).ToArray();
        foreach (var sender in senders) FriendSend(sender, 10, receiver);
        FriendSend(receiver, 11, senders[16]); FriendSend(receiver, 11, senders[0]);
        Check(FriendRows(senders[16]) == 0 && FriendRows(senders[0]) == 1, "at most sixteen live incoming requests are retained per session");
        var slow = FriendPlayer(map); var first = FriendPlayer(map); var second = FriendPlayer(map);
        FriendSend(slow, 10, first); FriendSend(slow, 10, second); FriendSend(second, 11, slow);
        Check(FriendRows(second) == 0, "one sender cannot flood requests to different recipients in the same second");
        friendNow = friendNow.AddSeconds(1); FriendSend(slow, 10, second); FriendSend(second, 11, slow);
        Check(FriendRows(second) == 1, "request throttle permits a later legitimate request");
        var expiring = FriendPlayer(map); var target = FriendPlayer(map); FriendSend(expiring, 10, target);
        friendNow = friendNow.AddSeconds(119); FriendSend(expiring, 10, target); friendNow = friendNow.AddSeconds(1); FriendSend(target, 11, expiring);
        Check(FriendRows(target) == 0, "duplicate requests cannot extend an invitation's original expiry");
        var receiverOld = FriendPlayer(map); var requestor = FriendPlayer(map); FriendSend(requestor, 10, receiverOld);
        var receiverNew = FriendPlayer(map); Kernel.GamePool.TryRemove(receiverNew.Entity.UID, out receiverNew);
        receiverNew.Entity.UID = receiverOld.Entity.UID; receiverNew.Account.EntityID = receiverOld.Entity.UID; Kernel.GamePool[receiverOld.Entity.UID] = receiverNew;
        FriendSend(receiverNew, 11, requestor); FriendSend(receiverOld, 11, requestor);
        Check(FriendRows(requestor) == 0, "neither a replaced receiver nor its stale session can accept old pending consent");
        var locked = FriendPlayer(map); var waiting = FriendPlayer(map); FriendSend(locked, 10, waiting);
        using (var connection = DataHolder.MySqlConnection)
        {
            connection.Open(); using (var transaction = connection.BeginTransaction())
            {
                using (var command = new Npgsql.NpgsqlCommand("SELECT uid FROM entities WHERE uid=@uid FOR UPDATE", connection, transaction))
                { command.Parameters.AddWithValue("uid", (long)Math.Min(locked.Entity.UID, waiting.Entity.UID)); command.ExecuteScalar(); }
                var attempt = Task.Run(() => FriendSend(waiting, 11, locked));
                bool blocked = System.Threading.SpinWait.SpinUntil(() => Scalar("SELECT count(*) FROM pg_stat_activity WHERE pid<>pg_backend_pid() AND wait_event_type='Lock' AND query LIKE '%SELECT uid FROM entities WHERE uid=%' AND @uid>0", locked.Entity.UID) > 0, 5000);
                friendNow = friendNow.AddMinutes(2); transaction.Commit();
                Check(attempt.Wait(5000) && blocked && FriendRows(locked) == 0 && !Fenced(waiting), "consent expiry is checked again after database owner-lock waits");
            }
        }
        var duplicate = FriendPlayer(map); var duplicatePeer = FriendPlayer(map);
        Query("INSERT INTO friends(entityid,friendid,friendname,message) VALUES(@a,@b,'Legacy','one'),(@a,@b,'Legacy','two')", "a", (long)duplicate.Entity.UID, "b", (long)duplicatePeer.Entity.UID);
        FriendSend(duplicate, 10, duplicatePeer); FriendSend(duplicatePeer, 11, duplicate);
        Check(Fenced(duplicate) && Fenced(duplicatePeer) && FriendRows(duplicate) == 2 && FriendRows(duplicatePeer) == 0 && FriendAwards(duplicate) == 0,
            "duplicate historical directions are preserved for repair instead of arbitrarily deduplicated or rewarded");
        VerifyMarriages(map);VerifyMarriageRaces(map);
    }
}
