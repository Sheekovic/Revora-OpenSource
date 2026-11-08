using System;
using System.Globalization;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Npgsql;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    // Owner-approved local edition; normal player entry uses the Twin City contest guide.
    internal static partial class MrMs2017
    {
        internal static Func<DateTime> Clock = () => DateTime.UtcNow;
        internal static Action<GameState> BeforeInstall;
        private static volatile bool initialized;
        internal static string SeasonOf(DateTime utc) { return MrMs2016.SeasonOf(utc); }
        private static bool Ready(GameState p) { return Q.Player(p) && p.Booth == null && p.Trade != null && !p.Trade.InTrade; }
        internal static int FlowerPoints(uint type)
        {
            uint family = type / 1000, amount = type % 1000;
            return family >= 751 && family <= 754 && amount > 0 ? checked((int)amount * (family == 754 ? 2 : 1)) : 0;
        }
        internal static void Initialize()
        {
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open(); Q.Execute(c, null, @"CREATE TABLE IF NOT EXISTS revora_mrms2017_seasons(
season varchar(7) PRIMARY KEY,closed_at timestamptz);
CREATE TABLE IF NOT EXISTS revora_mrms2017_entrants(
season varchar(7) NOT NULL REFERENCES revora_mrms2017_seasons(season),uid bigint NOT NULL CHECK(uid>0),
contest varchar(2) NOT NULL CHECK(contest IN ('mr','ms')),registered_at timestamptz NOT NULL,
PRIMARY KEY(season,uid));
CREATE TABLE IF NOT EXISTS revora_mrms2017_donations(
season varchar(7) NOT NULL,sender bigint NOT NULL CHECK(sender>0),request_id uuid NOT NULL,candidate bigint NOT NULL,
item_uid bigint NOT NULL,item_type bigint NOT NULL,before_stack integer NOT NULL CHECK(before_stack>=0),
points integer NOT NULL CHECK(points BETWEEN 1 AND 1998),donated_at timestamptz NOT NULL,
PRIMARY KEY(sender,request_id),FOREIGN KEY(season,candidate) REFERENCES revora_mrms2017_entrants(season,uid),
CHECK(sender<>candidate));
CREATE TABLE IF NOT EXISTS revora_mrms2017_results(
season varchar(7) NOT NULL,uid bigint NOT NULL,contest varchar(2) NOT NULL CHECK(contest IN ('mr','ms')),
placement integer NOT NULL CHECK(placement BETWEEN 1 AND 3),support bigint NOT NULL CHECK(support>0),
award_recorded boolean NOT NULL DEFAULT false,PRIMARY KEY(season,uid),UNIQUE(season,contest,placement),
FOREIGN KEY(season,uid) REFERENCES revora_mrms2017_entrants(season,uid));
CREATE INDEX IF NOT EXISTS revora_mrms2017_support ON revora_mrms2017_donations(season,candidate);");
                initialized=true;
            }
        }
        private static void EnsureOpen(NpgsqlConnection c, NpgsqlTransaction t, string season)
        {
            if (season != SeasonOf(Clock())) throw new InvalidOperationException("This month's contest has changed.");
            int number;
            if (!int.TryParse(season.Replace("-", ""), out number)) throw new InvalidOperationException("Invalid contest month.");
            Q.Execute(c,t,"SELECT pg_advisory_xact_lock(6711,@key)","key",LockKey(season));
            Q.Execute(c,t,"INSERT INTO revora_mrms2017_seasons(season) VALUES(@season) ON CONFLICT DO NOTHING","season",season);
            using (var q=Q.Command(c,t,"SELECT closed_at IS NULL FROM revora_mrms2017_seasons WHERE season=@season FOR UPDATE","season",season))
                if (!object.Equals(q.ExecuteScalar(),true)) throw new InvalidOperationException("The contest is closed.");
            if (season != SeasonOf(Clock())) throw new InvalidOperationException("This month's contest has changed.");
        }
        internal static bool Register(GameState p, string season)
        {
            if (!Ready(p)) return false;
            lock(p.ItemSyncRoot)
            {
                if (!Ready(p)) return false;
                ushort body=p.Entity.Body;
                string contest=body==1003 || body==1004?"mr":body==2001 || body==2002?"ms":null;
                if (contest==null) return false;
                using(var c=DataHolder.MySqlConnection)
                {
                    c.Open(); using(var t=c.BeginTransaction())
                    {
                        EnsureOpen(c,t,season);
                        using(var q=Q.Command(c,t,"SELECT body FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                            if(Convert.ToUInt32(q.ExecuteScalar())!=body) throw new InvalidOperationException("Contestant body requires reload.");
                        int inserted=Q.Execute(c,t,@"INSERT INTO revora_mrms2017_entrants(season,uid,contest,registered_at)
VALUES(@season,@uid,@contest,@now) ON CONFLICT(season,uid) DO NOTHING","season",season,"uid",(long)p.Entity.UID,"contest",contest,"now",Clock());
                        using(var q=Q.Command(c,t,"SELECT contest FROM revora_mrms2017_entrants WHERE season=@season AND uid=@uid","season",season,"uid",(long)p.Entity.UID))
                            if(!object.Equals(q.ExecuteScalar(),contest)) throw new InvalidOperationException("Contest category is already fixed.");
                        if(!Ready(p) || season!=SeasonOf(Clock())) throw new InvalidOperationException("Contestant session changed.");
                        t.Commit(); return inserted==1;
                    }
                }
            }
        }
        internal static bool Donate(GameState p,string season,uint candidate,uint itemUid,Guid requestId)
        {
            if(!Ready(p) || candidate==0 || candidate==p.Entity.UID || requestId==Guid.Empty) return false;
            lock(p.ItemSyncRoot) try
            {
                if(!Ready(p)) return false;
                ConquerItem item;
                if(!p.Inventory.TryGetItem(itemUid,out item)) return false;
                int points=FlowerPoints(item.ID); DateTime now=Clock();
                if(points==0 || !ConquerItemInformation.BaseInformations.ContainsKey(item.ID) || item.Bound || item.Position!=0 || item.Warehouse!=0 ||
                    item.Lock!=0 || item.Suspicious || item.InWardrobe || (item.Days==0?item.TimeLeftInMinutes!=0:now.ToLocalTime()>=item.DayStamp.AddDays(item.Days))) return false;
                using(var c=DataHolder.MySqlConnection)
                {
                    c.Open(); using(var t=c.BeginTransaction())
                    {
                        EnsureOpen(c,t,season);
                        using(var q=Q.Command(c,t,"SELECT 1 FROM revora_mrms2017_donations WHERE sender=@uid AND request_id=@request","uid",(long)p.Entity.UID,"request",requestId))
                            if(q.ExecuteScalar()!=null) return false;
                        using(var q=Q.Command(c,t,"SELECT 1 FROM revora_mrms2017_entrants WHERE season=@season AND uid=@uid","season",season,"uid",(long)candidate))
                            if(q.ExecuteScalar()==null) return false;
                        string consume=item.StackSize<=1?"DELETE FROM items":"UPDATE items SET stacksize=stacksize-1";
                        if(Q.Execute(c,t,consume+@" WHERE uid=@item AND entityid=@uid AND id=@type AND stacksize=@stack AND bound=0 AND position=0 AND warehouse=0
AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')=@days AND (@permanent OR daystamp=@stamp)",
                            "item",(long)itemUid,"uid",(long)p.Entity.UID,"type",(int)item.ID,"stack",(int)item.StackSize,
                            "days",item.Days.ToString(CultureInfo.InvariantCulture),"permanent",item.Days==0,"stamp",item.DayStamp.ToBinary())!=1)
                            throw new InvalidOperationException("Contest flower ownership changed.");
                        if(Q.Execute(c,t,@"INSERT INTO revora_mrms2017_donations(season,sender,request_id,candidate,item_uid,item_type,before_stack,points,donated_at)
VALUES(@season,@sender,@request,@candidate,@item,@type,@stack,@points,@now)","season",season,"sender",(long)p.Entity.UID,"request",requestId,
                            "candidate",(long)candidate,"item",(long)itemUid,"type",(long)item.ID,"stack",(int)item.StackSize,"points",points,"now",now)!=1)
                            throw new InvalidOperationException("Contest support receipt was not saved.");
                        if(!Ready(p) || season!=SeasonOf(Clock())) throw new InvalidOperationException("Contest changed before donation.");
                        t.Commit();
                    }
                }
                var before=BeforeInstall;if(before!=null)before(p);
                if(item.StackSize<=1) Q.Install(p,new[]{itemUid},new ConquerItem[0]);
                else {item.StackSize--;item.Mode=Enums.ItemMode.Update;item.Send(p);item.Mode=Enums.ItemMode.Default;}
                return true;
            }
            catch(Exception error) { Q.Fail(p,"mrms2017-donation",error); return false; }
        }
    }
}
