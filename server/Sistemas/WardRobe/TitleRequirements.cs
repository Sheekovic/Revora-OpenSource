using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;

namespace Revora
{
    internal static class TitleRequirements
    {
        internal static Func<DateTime> Clock = () => DateTime.UtcNow;
        internal static uint CtfGuild;
        private sealed class Session { internal bool Ready; internal DateTime Next, RequestWindow; internal int Requests; }
        private static readonly ConditionalWeakTable<GameState,Session> Sessions = new ConditionalWeakTable<GameState,Session>();

        internal static void RecordCtfWinner(uint guild)
        {
            using(var connection=DataHolder.MySqlConnection) {
                connection.Open();
                using(var command=new NpgsqlCommand("INSERT INTO revora_title_ctf(id,guild_id) VALUES(1,@guild) ON CONFLICT(id) DO UPDATE SET guild_id=EXCLUDED.guild_id",connection)) {
                    command.Parameters.AddWithValue("guild",(long)guild);command.ExecuteNonQuery();
                }
            }
            CtfGuild=guild;
        }

        internal static void Tick(GameState client)
        {
            if(client==null || client.Fake || client.Entity==null || !client.Entity.FullyLoaded)return;
            var session=Sessions.GetValue(client,_=>new Session());
            lock(session) {
                if(!session.Ready || Clock()<session.Next)return;
                session.Next=Clock().AddSeconds(10);
            }
            try { Reconcile(client,false); }
            catch(Exception ex) { Console.WriteLine("[wardrobe] reconcile-failed uid="+client.Entity.UID+" error="+ex.GetType().Name); }
        }

        internal static bool Ready(GameState client) { return Sessions.GetValue(client,_=>new Session()).Ready; }
        internal static bool AllowRequest(GameState client)
        {
            var session=Sessions.GetValue(client,_=>new Session());
            lock(session) {
                if(!session.Ready)return false;
                if(Clock()>=session.RequestWindow){session.RequestWindow=Clock().AddSeconds(1);session.Requests=0;}
                return ++session.Requests<=8;
            }
        }
        internal static void Login(GameState client)
        {
            Revora.Game.Features.HouseVisits.Recover(client);
            Revora.Game.Features.OriginBind.Login(client);
            Reconcile(client,true);
            var session=Sessions.GetValue(client,_=>new Session());
            lock(session){session.Ready=true;session.Next=Clock().AddSeconds(10);}
        }

        internal static HashSet<int> Eligible(GameState client)
        {
            var result=new HashSet<int>();
            var entity=client.Entity;
            var prestige=PrestigeRanking.Update(client);
            AddPrestige(result,prestige==null?0:prestige.Total);
            int perfection=client.Equipment.GetTPL();
            if(perfection>=216)result.Add(60086008);
            if(perfection>=324)result.Add(60076007);
            if(perfection>=648)result.Add(20392039);
            if(client.ChiData!=null && (long)client.ChiData.DragonPoints+client.ChiData.PhoenixPoints+client.ChiData.TigerPoints+client.ChiData.TurtlePoints>=1600)result.Add(20180001);
            if(entity.MyJiang!=null && entity.MyJiang.Inner_Strength>=81000)result.Add(20040005);
            if(entity.MyAchievement!=null && entity.MyAchievement.CompletedCount>=320)result.Add(20010002);
            if(entity.MyAchievement!=null)result.UnionWith(entity.MyAchievement.AwardKeys(Clock()));
            if(Game.Features.KingdomCrown.IsRuler(client)) {
                result.Add(11000);result.Add(40011000);
            }
            if(CtfGuild!=0 && client.Guild!=null && client.Guild.ID==CtfGuild && client.Guild.Leader!=null && client.Guild.Leader.ID==entity.UID)result.Add(20020003);

            // A wardrobe owns a design once, regardless of duplicate item instances.
            var owned=new HashSet<uint>();
            var permanent=new HashSet<uint>();
            if(entity.StorageItems!=null)
                foreach(var item in entity.StorageItems.Values) {
                    if(item.Days>0 && item.DayStamp.AddDays(item.Days)<=DateTime.Now)continue;
                    owned.Add(item.ID);
                    if(item.Days==0 && item.TimeLeftInMinutes==0)permanent.Add(item.ID);
                }
            if(CountStars(owned,StorageItem.Garments,4)>=5)result.Add(20050006);
            if(CountStars(owned,StorageItem.Mounts,4)>=5)result.Add(20060007);
            if(CountStars(owned,StorageItem.Garments,5)>=1)result.Add(60030022);
            if(CountStars(owned,StorageItem.Mounts,5)>=1)result.Add(60040023);
            var monkeys=Enumerable.Range(200553,8).Select(i=>(uint)i).ToArray();
            if(monkeys.Any(permanent.Contains))result.Add(20130014);
            if(permanent.Contains(200559))result.Add(20140015);
            if(permanent.Contains(200560))result.Add(20150016);
            if(monkeys.All(permanent.Contains))result.Add(20160017);
            if(owned.Contains(200595))result.Add(20502050);
            if(owned.Contains(200596))result.Add(20512051);
            if(new uint[]{194695,194705,194675,194685}.All(owned.Contains))result.Add(20562056);
            // Each of the six Euro designs may be represented by its Fashion or Legend edition.
            var jerseys=new uint[][] {new uint[]{193735,193775},new uint[]{193745,193785},new uint[]{193755,193795},new uint[]{193765,193805},new uint[]{192655,193815},new uint[]{192665,193825}};
            int euro=jerseys.Count(pair=>pair.Any(owned.Contains));
            if(euro>=1 && (owned.Contains(200563)||owned.Contains(200564)))result.Add(20322032);
            if(euro==6)result.Add(20332033);
            if(owned.Contains(194865)||owned.Contains(194875))result.Add(20632063);
            if(new uint[]{194875,200613,200614}.All(owned.Contains)) {
                if(entity.Body==2001 || entity.Body==2002)result.Add(20642064);
                if(entity.Body==1003 || entity.Body==1004)result.Add(20652065);
            }
            if(client.RunesItem!=null) {
                var runes=RuneProgress.LegalRunes(client).Where(r=>r.ID%100==(r.ID/10000==401?1u:r.ID/10000==402?27u:9u));
                if(runes.GroupBy(r=>r.Position>=121?1:0).Any(set=>set.Select(r=>r.Position).Distinct().Count()>=5))result.Add(90001);
            }
            // Historical/cross-server event prizes need authoritative results, never VIP or a legacy grant.
            return result;
        }

        internal static int CountStars(IEnumerable<uint> ids,Dictionary<uint,StorageItem> catalog,int stars)
        {
            return ids.Distinct().Count(id=>catalog.ContainsKey(id) && catalog[id].Stars>=stars);
        }

        internal static void AddPrestige(HashSet<int> result,uint score)
        {
            if(score>=400000)result.Add(20272027);
            if(score>=600000)result.Add(20262026);
        }

        internal static void Reconcile(GameState client,bool login)
        {
            if(client==null || client.Fake || client.Entity==null || client.ItemSyncRoot==null)return;
            lock(client.ItemSyncRoot) {
                var entity=client.Entity;
                if(entity.MyAchievement==null)entity.MyAchievement=new Achievement(entity);
                entity.MyAchievement.Refresh(Clock(),login);
                Game.Features.ElitePkEvent.SynchronizeChampion(client, Clock());
                Game.Features.LocalPkWar.SynchronizeHonors(client, Clock());
                Game.Features.GuildWarPurses.SynchronizeHonor(client, Clock());
                if(entity.WTitles==null)entity.WTitles=StorageManager.Data.GetOrAdd(entity.UID,id=>new WardrobeTitles{Id=id});
                var wardrobe=entity.WTitles;
                lock(wardrobe.Sync) {
                    var previous=wardrobe.Records();
                    var next=Eligible(client).ToDictionary(key=>key,key=>previous.ContainsKey(key)&&previous[key]);
                    wardrobe.Replace(next);
                    next=wardrobe.Records();
                    ApplyVisuals(client);
                    var sender=new TitleStorage();
                    foreach(int removed in previous.Keys.Except(next.Keys))sender.Notify(client,TitleStorage.Action.RemoveTitle,removed,false);
                    foreach(var record in next)
                        if(login || !previous.ContainsKey(record.Key) || previous[record.Key]!=record.Value)sender.Notify(client,TitleStorage.Action.Update,record.Key,record.Value);
                }
            }
        }

        internal static void ApplyVisuals(GameState client)
        {
            var entity=client.Entity;var wardrobe=entity.WTitles;
            int title=0,wing=0;
            foreach(var entry in wardrobe.Records().Where(r=>r.Value)) {
                if(entry.Key/10000>=4000)wing=entry.Key;else title=entry.Key;
            }
            bool changed=entity.EquippedTitle!=title || entity.EquippedWing!=wing;
            entity.EquippedTitle=title;entity.EquippedWing=wing;
            entity.TitlePoints=wardrobe.Points;entity.UTitlePoints=wardrobe.Points;
            if(changed && client.Screen!=null) {
                client.SendScreenSpawn(entity,false);
                entity.SendSpawn(client,false);
            }
        }
    }
}
