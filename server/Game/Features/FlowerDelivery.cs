using System;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class FlowerDelivery
    {
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        private static bool enabled;
        private sealed class Recent {internal readonly Dictionary<uint,DateTime> Items=new Dictionary<uint,DateTime>();}
        private static readonly ConditionalWeakTable<GameState,Recent> recent=new ConditionalWeakTable<GameState,Recent>();
        private sealed class Row {internal long[] Total,Daily;internal string Day,Free;internal long Revision;}
        internal static void Initialize() {
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_flower_balances(
uid bigint PRIMARY KEY,totals bigint[] NOT NULL,daily bigint[] NOT NULL,day varchar(8) NOT NULL,free_day varchar(8) NOT NULL,revision bigint NOT NULL DEFAULT 0,
CHECK(cardinality(totals)=4 AND cardinality(daily)=4),CHECK(0<=ALL(totals) AND 4294967295>=ALL(totals)),
CHECK(0<=ALL(daily) AND 4294967295>=ALL(daily)));
CREATE TABLE IF NOT EXISTS revora_flower_deliveries(
id uuid PRIMARY KEY,sender bigint NOT NULL,recipient bigint NOT NULL,item_uid bigint NOT NULL,item_type bigint NOT NULL,
before_stack integer NOT NULL,bound boolean NOT NULL,category integer NOT NULL CHECK(category BETWEEN 0 AND 3),
amount integer NOT NULL CHECK(amount BETWEEN 1 AND 999),day varchar(8) NOT NULL,created_at timestamptz NOT NULL,
CHECK(sender<>recipient));");}
            if(!enabled)RestoreStartup();enabled=true;
        }
        internal static void RecordCurrentQueens(NpgsqlConnection c,NpgsqlTransaction t) {
            if(!enabled)return;
            var owners=new List<uint>();
            using(var q=Q.Command(c,t,@"SELECT DISTINCT uid FROM (
SELECT f.uid,row_number() OVER(PARTITION BY g.category ORDER BY f.totals[g.category] DESC,f.uid) AS rank
FROM revora_flower_balances f JOIN entities e ON e.uid=f.uid CROSS JOIN generate_series(1,4) AS g(category)
WHERE e.body IN (2001,2002,2005,2006) AND f.totals[g.category]>0) ranked WHERE rank=1 ORDER BY uid"))
            using(var r=q.ExecuteReader())while(r.Read())owners.Add(checked((uint)r.GetInt64(0)));
            foreach(uint uid in owners)RecordQueen(c,t,uid);
        }
        // Evaluate saved lifetime rankings, never mutable client/rank-cache fields.
        internal static bool RecordQueen(NpgsqlConnection c,NpgsqlTransaction t,uint uid) {
            if(!enabled)return false;
            var categories=new List<int>();
            using(var q=Q.Command(c,t,@"SELECT g.category FROM revora_flower_balances f JOIN entities e ON e.uid=f.uid
CROSS JOIN generate_series(1,4) AS g(category)
WHERE f.uid=@uid AND e.body IN (2001,2002,2005,2006) AND f.totals[g.category]>0
AND NOT EXISTS(SELECT 1 FROM revora_achievement_evidence a WHERE a.uid=f.uid
AND a.achievement_id=CASE g.category WHEN 1 THEN 11124 WHEN 2 THEN 11127 WHEN 3 THEN 11125 ELSE 11126 END)
AND NOT EXISTS(SELECT 1 FROM revora_flower_balances rival JOIN entities owner ON owner.uid=rival.uid
WHERE owner.body IN (2001,2002,2005,2006) AND (rival.totals[g.category]>f.totals[g.category]
OR (rival.totals[g.category]=f.totals[g.category] AND rival.uid<f.uid))) ORDER BY g.category", "uid",(long)uid))
            using(var r=q.ExecuteReader())while(r.Read())categories.Add(r.GetInt32(0));
            bool changed=false;
            foreach(int category in categories) {
                // Catalog order differs from the server balance-array order.
                switch(category) {
                    case 1: changed|=ProgressionEvidence.Complete(c,t,uid,11124,"flower-queen-v1","lifetime-rank-one",1);break;
                    case 2: changed|=ProgressionEvidence.Complete(c,t,uid,11127,"flower-queen-v1","lifetime-rank-one",1);break;
                    case 3: changed|=ProgressionEvidence.Complete(c,t,uid,11125,"flower-queen-v1","lifetime-rank-one",1);break;
                    case 4: changed|=ProgressionEvidence.Complete(c,t,uid,11126,"flower-queen-v1","lifetime-rank-one",1);break;
                }
            }
            return changed;
        }
        private static long[] Totals(Flowers f){return new long[]{f.RedRoses,f.Lilies,f.Orchads,f.Tulips};}
        // Guild flower pages use durable lifetime receipts, not the legacy guild-tenure
        // counters. Read one bounded snapshot, including offline members, without
        // installing lifetime values into fields that EntityTable autosaves.
        internal static Game.ConquerStructures.Society.RankingList[] GuildRanking(uint guild,uint viewer,byte category) {
            if(!enabled || category<5 || category>8)return null;
            int slot=category==5?1:category==6?3:category==7?2:4;
            var rows=new List<Game.ConquerStructures.Society.RankingList>();
            using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,@"SELECT e.uid,e.name,e.guildrank,
COALESCE(e.guildsilverdonation,0),COALESCE(e.guildconquerpointdonation,0),COALESCE(e.guildpkdonation,0),COALESCE(e.arsenaldonation,0),f.totals
FROM entities e LEFT JOIN revora_flower_balances f ON f.uid=e.uid
WHERE e.guildid=@guild AND EXISTS(SELECT 1 FROM entities v WHERE v.uid=@viewer AND v.guildid=@guild)
ORDER BY COALESCE(f.totals[@slot],0) DESC,e.uid LIMIT 20",
                "guild",(long)guild,"viewer",(long)viewer,"slot",slot)) {
                q.CommandTimeout=5;
                using(var r=q.ExecuteReader())while(r.Read()) {
                    var totals=r.IsDBNull(7)?new long[4]:(long[])r[7];
                    if(totals.Length!=4 || totals.Any(v=>v<0 || v>uint.MaxValue))throw new InvalidOperationException("Invalid guild flower balance.");
                    var member=new Game.ConquerStructures.Society.Guild.Member(guild) {
                        SilverDoantion=checked(Convert.ToUInt64(r[3])),ConquerPointDonation=checked(Convert.ToUInt64(r[4])),
                        PKDoantion=checked(Convert.ToUInt64(r[5])),Arsenal_Donation=checked(Convert.ToUInt64(r[6]))
                    };
                    rows.Add(new Game.ConquerStructures.Society.RankingList {
                        EntityID=checked(Convert.ToUInt32(r[0])),Name=Convert.ToString(r[1]),postion=(Game.Enums.GuildMemberRank)Convert.ToUInt16(r[2]),
                        silverdoantion=member.SilverDoantionMath,cpdoantion=member.ConquerPointDonationMath,
                        pkdoantion=member.PKDoantion,arsenaldoantion=member.Arsenal_Donation,totaldoantion=member.TotalDoantion,
                        rosedoantion=(ulong)totals[0],liliesdoantion=(ulong)totals[1],orchidesdoantion=(ulong)totals[2],tuplisedoantion=(ulong)totals[3]
                    });
                }
            }}
            return rows.ToArray();
        }
        private static Row Read(NpgsqlConnection c,NpgsqlTransaction t,uint uid,string day,bool locked) {
            Q.Execute(c,t,@"INSERT INTO revora_flower_balances(uid,totals,daily,day,free_day) VALUES(@uid,ARRAY[0,0,0,0]::bigint[],ARRAY[0,0,0,0]::bigint[],@day,'') ON CONFLICT(uid) DO NOTHING","uid",(long)uid,"day",day);
            Row row;
            using(var q=Q.Command(c,t,"SELECT totals,daily,day,free_day,revision FROM revora_flower_balances WHERE uid=@uid"+(locked?" FOR UPDATE":""),"uid",(long)uid))
            using(var r=q.ExecuteReader()) {
                if(!r.Read())throw new InvalidOperationException("Flower balance missing.");
                row=new Row{Total=(long[])r[0],Daily=(long[])r[1],Day=(string)r[2],Free=(string)r[3],Revision=(long)r[4]};
            }
            Normalize(row,day);return row;
        }
        private static void Normalize(Row row,string day) {
            if(row.Total.Length!=4 || row.Daily.Length!=4 || row.Total.Concat(row.Daily).Any(v=>v<0 || v>uint.MaxValue))throw new InvalidOperationException("Invalid flower balance.");
            if(string.CompareOrdinal(row.Day,day)>0)throw new InvalidOperationException("Flower clock moved backward.");
            if(row.Day!=day){row.Day=day;row.Daily=new long[4];}
        }
        private static void Apply(GameState p,Row row,string day) {
            Apply(p.Entity.MyFlowers,row,day);
        }
        private static void Apply(Flowers f,Row row,string day) {
            f.RedRoses=(uint)row.Total[0];f.Lilies=(uint)row.Total[1];f.Orchads=(uint)row.Total[2];f.Tulips=(uint)row.Total[3];
            f.RedRoses2day=(uint)row.Daily[0];f.Lilies2day=(uint)row.Daily[1];f.Orchads2day=(uint)row.Daily[2];f.Tulips2day=(uint)row.Daily[3];
            f.aFlower=row.Free==day?0u:1u;f.SendDay=uint.Parse(day.Substring(6,2),CultureInfo.InvariantCulture);
            f.PersistentDay=day;
        }
        internal static void RestoreStartup() {
            string day=MagnoliaQuest.Day(Clock());var records=new List<Tuple<Flowers,bool>>();
            using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,@"SELECT f.uid,e.name,COALESCE(e.body,0),f.totals,f.daily,f.day,f.free_day,f.revision
FROM revora_flower_balances f JOIN entities e ON e.uid=f.uid
WHERE f.totals<>ARRAY[0,0,0,0]::bigint[] ORDER BY f.uid LIMIT 100001"))using(var r=q.ExecuteReader())while(r.Read()) {
                if(records.Count>=100000)throw new InvalidOperationException("Flower ranking startup exceeds its bounded owner count.");
                uint uid=checked((uint)Convert.ToInt64(r[0]));if(uid==0)throw new InvalidOperationException("Invalid flower ranking owner.");
                var f=new Flowers(uid,Convert.ToString(r[1],CultureInfo.InvariantCulture));
                var row=new Row{Total=(long[])r[3],Daily=(long[])r[4],Day=(string)r[5],Free=(string)r[6],Revision=(long)r[7]};
                Normalize(row,day);Apply(f,row,day);
                records.Add(Tuple.Create(f,Flowers.IsGirl(checked((uint)Convert.ToInt64(r[2])))));
            }}
            // No online object may be replaced by a startup snapshot. An empty
            // first-install ledger leaves preexisting migration evidence intact.
            if(records.Count==0)return;
            if(records.Select(record=>record.Item1.UID).Distinct().Count()!=records.Count)
                throw new InvalidOperationException("Duplicate flower ranking owner.");
            if(Kernel.GamePool.Count!=0)throw new InvalidOperationException("Flower ranking restoration requires offline startup.");
            Flowers.RestoreRanks(records);
        }
        internal static void EnsureDay(GameState p) {
            if(!enabled)return;
            lock(p.ItemSyncRoot)if(p.Entity.MyFlowers.PersistentDay!=MagnoliaQuest.Day(Clock()))Load(p);
        }
        internal static void Load(GameState p) {
            if(!enabled)return;
            lock(p.ItemSyncRoot) {
                string day=MagnoliaQuest.Day(Clock());
                using(var c=DataHolder.MySqlConnection){c.Open();
                    using(var q=Q.Command(c,null,"SELECT count(*) FROM revora_flower_balances WHERE uid=@uid","uid",(long)p.Entity.UID)) {
                        var f=p.Entity.MyFlowers;
                        if(Convert.ToInt32(q.ExecuteScalar())==0 && (Totals(f).Any(v=>v!=0) || f.RedRoses2day!=0 || f.Lilies2day!=0 || f.Orchads2day!=0 || f.Tulips2day!=0 || f.aFlower!=1))
                            throw new InvalidOperationException("Unsaved flower history requires migration.");
                    }
                    Apply(p,Read(c,null,p.Entity.UID,day,false),day);
                }
                Refresh(p);
            }
        }
        private static void Save(NpgsqlConnection c,NpgsqlTransaction t,uint uid,Row row) {
            if(Q.Execute(c,t,"UPDATE revora_flower_balances SET totals=@total,daily=@daily,day=@day,free_day=@free,revision=revision+1 WHERE uid=@uid AND revision=@revision",
                "total",row.Total,"daily",row.Daily,"day",row.Day,"free",row.Free,"uid",(long)uid,"revision",row.Revision)!=1)
                throw new InvalidOperationException("Flower balance was not saved.");
        }
        private static bool Ready(GameState p){return Q.Player(p) && p.Entity.MyFlowers!=null && p.Booth==null && p.Trade!=null && !p.Trade.InTrade;}
        private static void Refresh(GameState p) {
            Flowers.RefreshReceived(p.Entity.MyFlowers,Flowers.IsGirl(p.Entity.Body));
        }
        internal static void Send(GameState sender,uint target,uint itemUid,uint action) {
            GameState receiver;if(!enabled || !Ready(sender) || !Kernel.GamePool.TryGetValue(target,out receiver) || !Ready(receiver) || ReferenceEquals(sender,receiver))return;
            var first=sender.Entity.UID<receiver.Entity.UID?sender:receiver;var second=ReferenceEquals(first,sender)?receiver:sender;
            lock(first.ItemSyncRoot)lock(second.ItemSyncRoot)try {
                if(!Ready(sender) || !Ready(receiver) || (action==0? !Flowers.IsBoy(sender.Entity.Body) || !Flowers.IsGirl(receiver.Entity.Body)
                    :action!=1 || !Flowers.IsGirl(sender.Entity.Body) || !Flowers.IsBoy(receiver.Entity.Body)))return;
                DateTime now=Clock();string day=MagnoliaQuest.Day(now);ConquerItem item=null;uint type=0,amount=1;int category=0;
                if(itemUid!=0) {
                    if(!sender.Inventory.TryGetItem(itemUid,out item))return;type=item.ID;
                    if(type/1000<751 || type/1000>758 || type%1000==0 || !ConquerItemInformation.BaseInformations.ContainsKey(type)
                        || (action==0?type/1000>754:type/1000<755) || item.Position!=0 || item.Warehouse!=0 || item.Lock!=0 || item.Suspicious || item.InWardrobe
                        || (item.Days==0?item.TimeLeftInMinutes!=0:now.ToLocalTime()>=item.DayStamp.AddDays(item.Days)))return;
                    var last=recent.GetValue(sender,_=>new Recent());
                    foreach(uint old in last.Items.Where(v=>v.Value<=now).Select(v=>v.Key).ToArray())last.Items.Remove(old);
                    if(last.Items.ContainsKey(itemUid))return;
                    amount=type%1000;category=(int)Flowers.GetFlowerTyp(type);
                }
                Row from,to;bool senderAchievement=false,receiverAchievement=false;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    // Serialize deliveries before taking participant rows. This prevents
                    // concurrent gifts to different women from using stale competitors.
                    Q.Execute(c,t,"LOCK TABLE revora_flower_balances IN SHARE ROW EXCLUSIVE MODE");
                    foreach(var p in new[]{first,second})using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                        if(q.ExecuteScalar()==null)throw new InvalidOperationException("Flower participant missing.");
                    var a=Read(c,t,first.Entity.UID,day,true);var b=Read(c,t,second.Entity.UID,day,true);
                    from=ReferenceEquals(first,sender)?a:b;to=ReferenceEquals(first,sender)?b:a;
                    if(!from.Total.SequenceEqual(Totals(sender.Entity.MyFlowers)) || !to.Total.SequenceEqual(Totals(receiver.Entity.MyFlowers)))
                        throw new InvalidOperationException("Flower totals require reload.");
                    if(item==null && from.Free==day)return;
                    if(to.Total[category]>uint.MaxValue-amount || to.Daily[category]>uint.MaxValue-amount)return;
                    if(item!=null) {
                        string sql=item.StackSize<=1?"DELETE FROM items":"UPDATE items SET stacksize=stacksize-1";
                        if(Q.Execute(c,t,sql+@" WHERE uid=@item AND entityid=@uid AND id=@type AND stacksize=@stack AND bound=@bound AND position=0 AND warehouse=0
AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')=@days AND (@permanent OR daystamp=@stamp)",
                            "item",(long)itemUid,"uid",(long)sender.Entity.UID,"type",(int)type,"stack",(int)item.StackSize,"bound",item.Bound?1:0,
                            "days",item.Days.ToString(CultureInfo.InvariantCulture),"permanent",item.Days==0,"stamp",item.DayStamp.ToBinary())!=1)
                            throw new InvalidOperationException("Flower gift ownership changed.");
                    }else from.Free=day;
                    to.Total[category]+=amount;to.Daily[category]+=amount;Save(c,t,sender.Entity.UID,from);Save(c,t,receiver.Entity.UID,to);
                    Guid delivery=Guid.NewGuid();
                    if(Q.Execute(c,t,@"INSERT INTO revora_flower_deliveries(id,sender,recipient,item_uid,item_type,before_stack,bound,category,amount,day,created_at)
VALUES(@id,@sender,@recipient,@item,@type,@stack,@bound,@category,@amount,@day,@now)","id",delivery,"sender",(long)sender.Entity.UID,"recipient",(long)receiver.Entity.UID,
                        "item",(long)itemUid,"type",(long)type,"stack",item==null?0:(int)item.StackSize,"bound",item!=null && item.Bound,"category",category,"amount",(int)amount,"day",day,"now",now)!=1)
                        throw new InvalidOperationException("Flower delivery receipt missing.");
                    // The reviewed 6711 requirement names flowers and kisses;
                    // letters, tins and jade gifts do not establish that fact.
                    if(item==null || type/1000<=755) {
                        senderAchievement=ProgressionEvidence.Complete(c,t,sender.Entity.UID,10127,"flower-delivery-v1",delivery.ToString("N"),1);
                        receiverAchievement=ProgressionEvidence.Complete(c,t,receiver.Entity.UID,10127,"flower-delivery-v1",delivery.ToString("N"),1);
                    }
                    receiverAchievement|=RecordQueen(c,t,receiver.Entity.UID);
                    if(!Ready(sender) || !Ready(receiver))throw new InvalidOperationException("Flower participant changed.");t.Commit();
                }}
                if(item!=null) {
                    var last=recent.GetValue(sender,_=>new Recent());
                    if(last.Items.Count>=64)last.Items.Remove(last.Items.OrderBy(v=>v.Value).First().Key);
                    last.Items[itemUid]=now.AddSeconds(1);
                    if(item.StackSize<=1)Q.Install(sender,new[]{itemUid},new ConquerItem[0]);
                    else {item.StackSize--;item.Mode=Enums.ItemMode.Update;item.Send(sender);item.Mode=Enums.ItemMode.Default;}
                }
                Apply(sender,from,day);Apply(receiver,to,day);Refresh(receiver);
                if(senderAchievement)Q.Refresh(sender);if(receiverAchievement)Q.Refresh(receiver);
                var effect=new SendFlower{Typing=action,Amount=amount,SenderName=sender.Entity.Name,ReceiverName=receiver.Entity.Name,
                    FType=(uint)category+(action==1?4u:0u),Effect=item==null && action==0?4u:(uint)category+1};sender.SendScreen(effect.ToArray(),true);
            }catch(Exception ex){Q.Fail(sender,"flower-delivery",ex);Q.Fail(receiver,"flower-delivery",ex);}
        }
    }
}
