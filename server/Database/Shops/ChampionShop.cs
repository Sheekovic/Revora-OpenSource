// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;

namespace Revora.Database
{
    public unsafe static class ChampionShop
    {
        public const uint UID = 6003;
        public static ChampionShopBase Shop;
        public class ChampionItem
        {
            public uint ID, Price;
            public bool Bound;
        }
        public class ChampionShopBase
        {
            public int Count { get { return Items.Count; } }
            public Dictionary<uint, ChampionItem> Items;
        }
        public static void Load()
        {
            Console.WriteLine("Loading Champion shop information");
            string[] text = File.ReadAllLines(Constants.ChampionShopPath);
            Shop = new ChampionShopBase();
            Shop.Items = new Dictionary<uint, ChampionItem>();
            for (int x = 0; x < text.Length; x++)
            {
                //System.Console.Write("\b{0}", Cargando.NextChar());
                String line = text[x];
                String[] split = line.Split(' ');
                Shop.Items.Add(uint.Parse(split[0]),
                    new ChampionItem()
                    {
                        ID = uint.Parse(split[0]),
                        Price = uint.Parse(split[1]),
                        Bound = split[2].Contains("Bound")
                    });
            }
            //Console.WriteLine("Done!");
        }
    }
}
namespace Revora.Game.Features
{
    internal static class ChampionPurchases
    {
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        internal static Action BeforeCommit,BeforeInstall;
        internal static void Initialize() {
            ChampionSteedPacks.Initialize();
            MysteryFruit.Initialize();
            MeditationPacks.Initialize();
            using(var c=Database.DataHolder.MySqlConnection){c.Open();QuestInventory.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_champion_purchases(
id uuid PRIMARY KEY,uid bigint NOT NULL REFERENCES entities(uid),item_type integer NOT NULL,item_uid bigint NOT NULL UNIQUE,
cost bigint NOT NULL CHECK(cost>0),points_before bigint NOT NULL,points_after bigint NOT NULL CHECK(points_after>=0),
purchased_at timestamptz NOT NULL,CHECK(points_before-points_after=cost));
CREATE INDEX IF NOT EXISTS revora_champion_purchase_recent ON revora_champion_purchases(uid,item_type,purchased_at);");}
        }
        private static bool Ready(Client.GameState p) {
            return QuestInventory.Player(p) && !p.ShutdownStarted && !p.DisconnectStarted && p.Trade!=null && !p.Trade.InTrade && p.Booth==null
                && p.ChampionStats!=null && p.ChampionStats.UID==p.Entity.UID && !p.ChampionStats.SignedUp && p.ChampionGroup==null;
        }
        internal static void Buy(Client.GameState p,Network.GamePackets.ItemUsage request) {
            if(request==null || request.UID!=Database.ChampionShop.UID || !Ready(p))return;
            byte[] frame=request.ToArray();
            if(frame==null || frame.Length<32 || frame.Length>128 || BitConverter.ToUInt16(frame,0)+8!=frame.Length
                || BitConverter.ToUInt16(frame,2)!=1009 || BitConverter.ToUInt16(frame,20)!=1
                || BitConverter.ToUInt64(frame,frame.Length-8)!=Network.PacketHandler.ClientSeal || request.dwExtraInfo>1)return;
            if(!SessionInstall.TryEnter(p.Entity.UID))return;
            try {lock(p.ItemSyncRoot) {
                if(!Ready(p))return;
                var stats=p.ChampionStats;
                lock(stats)try {
                    Database.ChampionShop.ChampionItem catalog;
                    uint type=request.dwParam;
                    if(Database.ChampionShop.Shop==null || !Database.ChampionShop.Shop.Items.TryGetValue(type,out catalog) || catalog.Price==0
                        || !Database.ConquerItemInformation.BaseInformations.ContainsKey(type))return;
                    if((type<730001 || type>730003) && !ChampionSteedPacks.Handles(type) && type!=3001046)return;
                    if(type==3001046 && !MeditationPacks.Capacity(p)){p.Send("Make room for every Meditation reward before buying this pack.");return;}
                    if(p.Inventory.Count>=40){p.Send("Make one place for your purchase.");return;}
                    if(stats.Points<catalog.Price){p.Send("You do not have enough Champion Points.");return;}
                    Database.ChampionTable.SaveStats(stats);
                    uint before=stats.Points,after=before-catalog.Price;long revision;
                    var item=QuestInventory.New(type,true);item.Plus=type>=730001 && type<=730003?(byte)(type-730000):(byte)0;item.MaxStackSize=1;
                    using(var c=Database.DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                        Database.SkillTable.LockOwner(c,t,p);DateTime now=Clock();
                        using(var q=QuestInventory.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_champion_purchases WHERE uid=@uid AND item_type=@type AND purchased_at>@now-interval '1 second')",
                            "uid",(long)p.Entity.UID,"type",(int)type,"now",now))if(Equals(q.ExecuteScalar(),true))return;
                        var ids=new List<uint>();
                        using(var q=QuestInventory.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid","uid",(long)p.Entity.UID))
                        using(var r=q.ExecuteReader())while(r.Read()){if(ids.Count>=40)throw new InvalidOperationException("Champion inventory exceeds capacity.");ids.Add(checked((uint)Convert.ToInt64(r[0])));}
                        if(!ids.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException("Champion inventory requires reload.");
                        revision=Database.ChampionTable.PersistPoints(c,t,stats,after);
                        QuestInventory.Insert(c,t,p.Entity.UID,item);
                        if(QuestInventory.Execute(c,t,"UPDATE items SET plus=@plus WHERE uid=@item AND entityid=@uid","plus",(int)item.Plus,"item",(long)item.UID,"uid",(long)p.Entity.UID)!=1)
                            throw new InvalidOperationException("Champion stone level was not saved.");
                        QuestInventory.Execute(c,t,@"INSERT INTO revora_champion_purchases(id,uid,item_type,item_uid,cost,points_before,points_after,purchased_at)
VALUES(@id,@uid,@type,@item,@cost,@before,@after,@now)","id",Guid.NewGuid(),"uid",(long)p.Entity.UID,"type",(int)type,"item",(long)item.UID,
                            "cost",(long)catalog.Price,"before",(long)before,"after",(long)after,"now",now);
                        if(BeforeCommit!=null)BeforeCommit();if(!Ready(p) || !ReferenceEquals(p.ChampionStats,stats))throw new InvalidOperationException("Champion purchase owner changed.");t.Commit();
                    }}
                    if(BeforeInstall!=null)BeforeInstall();
                    Database.ChampionTable.PointsCommitted(stats,after,revision);QuestInventory.Install(p,new uint[0],new[]{item});stats.Send(p);
                }catch(Exception ex){Database.ChampionTable.Invalidate(stats,ex);}
            }}finally{SessionInstall.Exit(p.Entity.UID);}
        }
    }
}

namespace Revora.Game.Features
{
    internal static class ChampionSteedPacks
    {
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        internal static Action BeforeCommit,BeforeInstall;
        internal static bool Handles(uint id){return id>=3001060 && id<=3001065;}
        internal static void Initialize() {
            foreach(uint id in new uint[]{300000,3001060,3001061,3001062,3001063,3001064,3001065})
                if(!Database.ConquerItemInformation.BaseInformations.ContainsKey(id))throw new InvalidOperationException("Champion steed pack catalog is missing.");
            using(var c=Database.DataHolder.MySqlConnection){c.Open();QuestInventory.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_champion_steed_openings(
id uuid PRIMARY KEY,uid bigint NOT NULL REFERENCES entities(uid),pack_uid bigint NOT NULL,pack_type integer NOT NULL CHECK(pack_type BETWEEN 3001060 AND 3001065),
before_stack integer NOT NULL CHECK(before_stack>=0),result_uid bigint NOT NULL UNIQUE,plus integer NOT NULL CHECK(plus IN(1,3)),color bigint NOT NULL CHECK(color IN(16750080,65430,9830655)),
opened_at timestamptz NOT NULL);
CREATE INDEX IF NOT EXISTS revora_champion_steed_recent ON revora_champion_steed_openings(uid,pack_uid,opened_at);");}
        }
        private static bool Ready(Client.GameState p){return QuestInventory.Player(p) && !p.ShutdownStarted && !p.DisconnectStarted && p.Booth==null && p.Trade!=null && !p.Trade.InTrade;}
        internal static bool TryHandle(Client.GameState p,byte[] frame) {
            Network.GamePackets.ConquerItem pack;
            if(p==null || p.Inventory==null || frame==null || frame.Length<12 || !p.Inventory.TryGetItem(BitConverter.ToUInt32(frame,8),out pack) || !Handles(pack.ID))return false;
            if(frame.Length<32 || frame.Length>128 || BitConverter.ToUInt16(frame,0)+8!=frame.Length || BitConverter.ToUInt16(frame,2)!=1009
                || BitConverter.ToUInt16(frame,20)!=4 || BitConverter.ToUInt64(frame,frame.Length-8)!=Network.PacketHandler.ClientSeal || !Ready(p))return true;
            if(!SessionInstall.TryEnter(p.Entity.UID))return true;
            try{lock(p.ItemSyncRoot)try {
                Network.GamePackets.ConquerItem owned;
                if(!Ready(p) || !p.Inventory.TryGetItem(pack.UID,out owned) || !ReferenceEquals(pack,owned) || pack.Position!=0 || pack.Warehouse!=0
                    || pack.Lock!=0 || pack.Suspicious || pack.InWardrobe || pack.Days!=0 || pack.TimeLeftInMinutes!=0 || (pack.StackSize>1 && p.Inventory.Count>=40))return true;
                int index=(int)(pack.ID-3001060);uint color=new uint[]{0xff9600,0x00ff96,0x9600ff}[index%3];
                var reward=QuestInventory.New(300000,true);reward.Plus=index<3?(byte)1:(byte)3;reward.Effect=Enums.ItemEffect.Horse;reward.SocketProgress=color;
                reward.NextGreen=(byte)color;reward.NextBlue=(byte)(color>>8);reward.NextRed=(byte)(color>>16);reward.MaxStackSize=1;
                using(var c=Database.DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    Database.SkillTable.LockOwner(c,t,p);DateTime now=Clock();
                    using(var q=QuestInventory.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_champion_steed_openings WHERE uid=@uid AND pack_uid=@pack AND opened_at>@now-interval '1 second')",
                        "uid",(long)p.Entity.UID,"pack",(long)pack.UID,"now",now))if(Equals(q.ExecuteScalar(),true))return true;
                    var ids=new List<uint>();using(var q=QuestInventory.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid","uid",(long)p.Entity.UID))
                    using(var r=q.ExecuteReader())while(r.Read()){if(ids.Count>=40)throw new InvalidOperationException("Champion pack inventory exceeds capacity.");ids.Add(checked((uint)Convert.ToInt64(r[0])));}
                    if(!ids.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException("Champion pack inventory requires reload.");
                    string debit=pack.StackSize<=1?"DELETE FROM items":"UPDATE items SET stacksize=stacksize-1";
                    if(QuestInventory.Execute(c,t,debit+@" WHERE uid=@pack AND entityid=@uid AND id=@type AND stacksize=@stack AND bound=@bound
AND position=0 AND warehouse=0 AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')='0'",
                        "pack",(long)pack.UID,"uid",(long)p.Entity.UID,"type",(int)pack.ID,"stack",(int)pack.StackSize,"bound",pack.Bound?1:0)!=1)
                        throw new InvalidOperationException("Champion pack ownership changed.");
                    QuestInventory.Execute(c,t,@"INSERT INTO items(uid,id,entityid,position,warehouse,stacksize,bound,durability,maximdurability,plus,effect,socketprogress,nextsteedcolor)
VALUES(@item,300000,@uid,0,0,1,1,@dur,@dur,@plus,@effect,@color,@color)","item",(long)reward.UID,"uid",(long)p.Entity.UID,"dur",(int)reward.Durability,
                        "plus",(int)reward.Plus,"effect",(int)reward.Effect,"color",(long)color);
                    QuestInventory.Execute(c,t,@"INSERT INTO revora_champion_steed_openings(id,uid,pack_uid,pack_type,before_stack,result_uid,plus,color,opened_at)
VALUES(@id,@uid,@pack,@type,@stack,@result,@plus,@color,@now)","id",Guid.NewGuid(),"uid",(long)p.Entity.UID,"pack",(long)pack.UID,"type",(int)pack.ID,"stack",(int)pack.StackSize,
                        "result",(long)reward.UID,"plus",(int)reward.Plus,"color",(long)color,"now",now);
                    if(BeforeCommit!=null)BeforeCommit();if(!Ready(p))throw new InvalidOperationException("Champion pack owner changed.");t.Commit();
                }}
                if(BeforeInstall!=null)BeforeInstall();QuestInventory.Install(p,pack.StackSize<=1?new[]{pack.UID}:new uint[0],new[]{reward});
                if(pack.StackSize>1){pack.StackSize--;pack.Mode=Enums.ItemMode.Update;pack.Send(p);pack.Mode=Enums.ItemMode.Default;}
            }catch(Exception ex){QuestInventory.Fail(p,"champion-steed-pack",ex);}}
            finally{SessionInstall.Exit(p.Entity.UID);}return true;
        }
    }
    internal static class MysteryFruit
    {
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        internal static Func<int,int> Roll=RandomBelow;
        internal static Action BeforeCommit,BeforeInstall;
        internal static int RandomBelow(int bound) {
            if(bound<1 || bound>256)throw new ArgumentOutOfRangeException("bound");
            using(var rng=System.Security.Cryptography.RandomNumberGenerator.Create()) {
                var b=new byte[1];int limit=256-256%bound;do{rng.GetBytes(b);}while(b[0]>=limit);return b[0]%bound;
            }
        }
        internal static void Initialize() {
            using(var c=Database.DataHolder.MySqlConnection){c.Open();QuestInventory.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_mystery_fruit_uses(
 id uuid PRIMARY KEY,uid bigint NOT NULL REFERENCES entities(uid),fruit_uid bigint NOT NULL,dew_uid bigint,
 fruit_stack integer NOT NULL CHECK(fruit_stack>=0),dew_stack integer,
 points integer NOT NULL CHECK(points BETWEEN 1 AND 3),attribute integer NOT NULL CHECK(attribute BETWEEN 2 AND 5),
 bonus_before integer NOT NULL CHECK(bonus_before>=0),bonus_after integer NOT NULL CHECK(bonus_after BETWEEN 1 AND 900),
 used_at timestamptz NOT NULL,CHECK(bonus_after-bonus_before=points),
 CHECK((dew_uid IS NULL AND dew_stack IS NULL) OR (dew_uid IS NOT NULL AND dew_stack>=0 AND points=3)));
 CREATE INDEX IF NOT EXISTS revora_mystery_fruit_recent ON revora_mystery_fruit_uses(uid,fruit_uid,used_at);");}
        }
        // Reserve future ordinary level points and unearned rebirth bonuses. A low
        // reincarnated level must not reopen capacity already filled at the cap.
        internal static long FutureAttributeTotal(CharacterProgression.Profile profile) {
            var v=profile.Values;int cap=Math.Max((int)Config.LevelCap,(int)v[0]);
            long total=v.Skip(2).Take(5).Sum();
            if(v[8]==0 && v[0]<120) {
                int target=Math.Min(120,cap);var old=Database.DataHolder.StatsFor((byte)v[7],(byte)v[0]);
                var next=Database.DataHolder.StatsFor((byte)v[7],(byte)target);
                total+=(long)next.Strength+next.Agility+next.Vitality+next.Spirit-old.Strength-old.Agility-old.Vitality-old.Spirit;
                total+=(long)Math.Max(0,cap-target)*Config.AttributePointsPerLevel;
            } else total+=(long)Math.Max(0,cap-(int)v[0])*Config.AttributePointsPerLevel;
            if(v[8]<2) {
                long firstBonus=v[8]==0?55:RebirthRules.Bonus((byte)v[11],(byte)v[9]);
                long secondLife=82+firstBonus+55+(long)Math.Max(0,cap-15)*Config.AttributePointsPerLevel+v[13];
                total=Math.Max(total,secondLife);
            }
            return total;
        }
        private static bool Ready(Client.GameState p) {
            return QuestInventory.Player(p) && CharacterProgression.Managed(p) && !p.ShutdownStarted && !p.DisconnectStarted
                && p.Trade!=null && !p.Trade.InTrade && p.Booth==null && p.ChampionGroup==null && !p.InQualifier();
        }
        private static bool Usable(Network.GamePackets.ConquerItem item) {
            return item!=null && item.Position==0 && item.Warehouse==0 && item.Lock==0 && !item.Suspicious && !item.InWardrobe && item.Days==0 && item.TimeLeftInMinutes==0;
        }
        internal static void Debit(Npgsql.NpgsqlConnection c,Npgsql.NpgsqlTransaction t,Client.GameState p,Network.GamePackets.ConquerItem item) {
            string sql=item.StackSize<=1?"DELETE FROM items":"UPDATE items SET stacksize=stacksize-1";
            if(QuestInventory.Execute(c,t,sql+@" WHERE uid=@item AND entityid=@uid AND id=@type AND stacksize=@stack AND bound=@bound
 AND position=0 AND warehouse=0 AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')='0'",
                "item",(long)item.UID,"uid",(long)p.Entity.UID,"type",(int)item.ID,"stack",(int)item.StackSize,"bound",item.Bound?1:0)!=1)
                throw new InvalidOperationException("Fruit ownership requires reload.");
        }
        internal static bool TryHandle(Client.GameState p,byte[] frame) {
            Network.GamePackets.ConquerItem clicked;
            if(p==null || p.Inventory==null || frame==null || frame.Length<12 || !p.Inventory.TryGetItem(BitConverter.ToUInt32(frame,8),out clicked)
                || clicked.ID!=3001044 && clicked.ID!=3001045)return false;
            if(frame.Length<32 || frame.Length>128 || BitConverter.ToUInt16(frame,0)+8!=frame.Length || BitConverter.ToUInt16(frame,2)!=1009
                || BitConverter.ToUInt16(frame,20)!=4 || BitConverter.ToUInt64(frame,frame.Length-8)!=Network.PacketHandler.ClientSeal || !Ready(p))return true;
            if(!SessionInstall.TryEnter(p.Entity.UID))return true;
            try{lock(p.ItemSyncRoot)try {
                Network.GamePackets.ConquerItem owned;
                if(!Ready(p) || !p.Inventory.TryGetItem(clicked.UID,out owned) || !ReferenceEquals(clicked,owned) || !Usable(clicked))return true;
                var dew=clicked.ID==3001045?clicked:null;
                var fruit=dew==null?clicked:p.Inventory.Objects.Where(i=>i.ID==3001044 && Usable(i)).OrderBy(i=>i.UID).FirstOrDefault();
                if(fruit==null){p.Send("Bring a Mystery Fruit to use with this dew.");return true;}
                var before=CharacterProgression.Capture(p);var values=(long[])before.Values.Clone();
                if(FutureAttributeTotal(before)>897 || values[13]>897){p.Send("Your earned attributes leave no room for three more points below the 900-point limit.");return true;}
                int count=dew==null?Roll(3)+1:3,attribute=Roll(4)+2;
                if(count<1 || count>3 || attribute<2 || attribute>5)throw new InvalidOperationException("Invalid fruit roll.");
                values[attribute]+=count;values[13]+=count;var after=new CharacterProgression.Profile(values);
                using(var c=Database.DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    Database.SkillTable.LockOwner(c,t,p);DateTime now=Clock();
                    using(var q=QuestInventory.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_mystery_fruit_uses WHERE uid=@uid AND fruit_uid=@fruit AND used_at>@now-interval '1 second')",
                        "uid",(long)p.Entity.UID,"fruit",(long)fruit.UID,"now",now))if(Equals(q.ExecuteScalar(),true))return true;
                    var ids=new List<uint>();using(var q=QuestInventory.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid","uid",(long)p.Entity.UID))
                    using(var r=q.ExecuteReader())while(r.Read()){if(ids.Count>=40)throw new InvalidOperationException("Fruit inventory exceeds capacity.");ids.Add(checked((uint)Convert.ToInt64(r[0])));}
                    if(!ids.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException("Fruit inventory requires reload.");
                    Debit(c,t,p,fruit);if(dew!=null)Debit(c,t,p,dew);CharacterProgression.Persist(c,t,p,after);
                    QuestInventory.Execute(c,t,@"INSERT INTO revora_mystery_fruit_uses(id,uid,fruit_uid,dew_uid,fruit_stack,dew_stack,points,attribute,bonus_before,bonus_after,used_at)
 VALUES(@id,@uid,@fruit,@dew,@fs,@ds,@points,@attribute,@before,@after,@now)","id",Guid.NewGuid(),"uid",(long)p.Entity.UID,"fruit",(long)fruit.UID,
                        "dew",dew==null?(object)DBNull.Value:(long)dew.UID,"fs",(int)fruit.StackSize,"ds",dew==null?(object)DBNull.Value:(int)dew.StackSize,
                        "points",count,"attribute",attribute,"before",before.Values[13],"after",after.Values[13],"now",now);
                    if(BeforeCommit!=null)BeforeCommit();if(!Ready(p))throw new InvalidOperationException("Fruit owner changed.");t.Commit();
                }}
                if(BeforeInstall!=null)BeforeInstall();
                var consumed=dew==null?new[]{fruit}:new[]{fruit,dew};
                QuestInventory.Install(p,consumed.Where(i=>i.StackSize<=1).Select(i=>i.UID).ToArray(),new Network.GamePackets.ConquerItem[0]);
                foreach(var item in consumed.Where(i=>i.StackSize>1)){item.StackSize--;item.Mode=Enums.ItemMode.Update;item.Send(p);item.Mode=Enums.ItemMode.Default;}
                CharacterProgression.ApplyCommitted(p,after);p.CalculateStatBonus();p.CalculateHPBonus();p.GemAlgorithm();
                p.Send("The fruit granted "+count+" "+new[]{"Strength","Agility","Vitality","Spirit"}[attribute-2]+" points.");
            }catch(Exception ex){QuestInventory.Fail(p,"mystery-fruit",ex);}}
            finally{SessionInstall.Exit(p.Entity.UID);}return true;
        }
    }

    internal static class MeditationPacks
    {
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        internal static Func<int,int> Roll=MysteryFruit.RandomBelow;
        internal static Action BeforeCommit,BeforeInstall;
        internal static void Initialize() {
            using(var c=Database.DataHolder.MySqlConnection){c.Open();QuestInventory.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_meditation_openings(
 id uuid PRIMARY KEY,uid bigint NOT NULL REFERENCES entities(uid),pack_uid bigint NOT NULL,before_stack integer NOT NULL CHECK(before_stack>=0),
 outcome integer NOT NULL CHECK(outcome BETWEEN 0 AND 2),fruit_uid bigint UNIQUE,
 chi_before bigint NOT NULL CHECK(chi_before>=0),chi_after bigint NOT NULL CHECK(chi_after>=0),
 study_before integer NOT NULL CHECK(study_before BETWEEN 0 AND 65535),study_after integer NOT NULL CHECK(study_after BETWEEN 0 AND 65535),
 opened_at timestamptz NOT NULL,
 CHECK((outcome=0 AND fruit_uid IS NOT NULL) OR (outcome<>0 AND fruit_uid IS NULL)),
 CHECK(chi_after-chi_before=CASE WHEN outcome=1 THEN 1000 ELSE 0 END),
 CHECK(study_after-study_before=CASE WHEN outcome=2 THEN 3000 ELSE 0 END));
 CREATE INDEX IF NOT EXISTS revora_meditation_recent ON revora_meditation_openings(uid,pack_uid,opened_at);");}
        }
        internal static bool Capacity(Client.GameState p) {
            return CharacterProgression.Managed(p) && ChiWallet.Ready(p) && ChiWallet.Matches(p,p.ChiPoints)
                && p.ChiPoints<=uint.MaxValue-1000 && p.Entity.SubClasses!=null && p.Entity.SubClasses.StudyPoints<=ushort.MaxValue-3000
                && MysteryFruit.FutureAttributeTotal(CharacterProgression.Capture(p))<=897;
        }
        private static bool Ready(Client.GameState p) {
            return QuestInventory.Player(p) && !p.ShutdownStarted && !p.DisconnectStarted && p.Trade!=null && !p.Trade.InTrade && p.Booth==null && p.ChampionGroup==null && !p.InQualifier();
        }
        internal static bool TryHandle(Client.GameState p,byte[] frame) {
            Network.GamePackets.ConquerItem pack;
            if(p==null || p.Inventory==null || frame==null || frame.Length<12 || !p.Inventory.TryGetItem(BitConverter.ToUInt32(frame,8),out pack) || pack.ID!=3001046)return false;
            if(frame.Length<32 || frame.Length>128 || BitConverter.ToUInt16(frame,0)+8!=frame.Length || BitConverter.ToUInt16(frame,2)!=1009
                || BitConverter.ToUInt16(frame,20)!=4 || BitConverter.ToUInt64(frame,frame.Length-8)!=Network.PacketHandler.ClientSeal || !Ready(p))return true;
            if(!SessionInstall.TryEnter(p.Entity.UID))return true;
            try{lock(p.ItemSyncRoot)try {
                Network.GamePackets.ConquerItem owned;
                if(!Ready(p) || !p.Inventory.TryGetItem(pack.UID,out owned) || !ReferenceEquals(pack,owned) || pack.Position!=0 || pack.Warehouse!=0
                    || pack.Lock!=0 || pack.Suspicious || pack.InWardrobe || pack.Days!=0 || pack.TimeLeftInMinutes!=0)return true;
                if(pack.StackSize>1 && p.Inventory.Count>=40){p.Send("Make room for the pack's reward.");return true;}
                if(!Capacity(p)){p.Send("Make room for a Fruit, 1,000 Chi Points and 3,000 Study Points before opening this pack.");return true;}
                var profile=CharacterProgression.Capture(p);uint chi=p.ChiPoints;int study=p.Entity.SubClasses.StudyPoints;
                int outcome=Roll(3);if(outcome<0 || outcome>2)throw new InvalidOperationException("Invalid Meditation roll.");
                var fruit=outcome==0?QuestInventory.New(3001044,true):null;
                uint nextChi=chi+(outcome==1?1000u:0u);int nextStudy=study+(outcome==2?3000:0);
                using(var c=Database.DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    Database.SkillTable.LockOwner(c,t,p);DateTime now=Clock();
                    using(var q=QuestInventory.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_meditation_openings WHERE uid=@uid AND pack_uid=@pack AND opened_at>@now-interval '1 second')",
                        "uid",(long)p.Entity.UID,"pack",(long)pack.UID,"now",now))if(Equals(q.ExecuteScalar(),true))return true;
                    var ids=new List<uint>();using(var q=QuestInventory.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid","uid",(long)p.Entity.UID))
                    using(var r=q.ExecuteReader())while(r.Read()){if(ids.Count>=40)throw new InvalidOperationException("Meditation inventory exceeds capacity.");ids.Add(checked((uint)Convert.ToInt64(r[0])));}
                    if(!ids.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException("Meditation inventory requires reload.");
                    CharacterProgression.Persist(c,t,p,profile);
                    if(QuestInventory.Execute(c,t,"UPDATE chi SET points=@after WHERE uid=@uid AND points=@before","uid",(long)p.Entity.UID,"before",(long)chi,"after",(long)nextChi)!=1)
                        throw new InvalidOperationException("Meditation Chi wallet requires reload.");
                    if(QuestInventory.Execute(c,t,"UPDATE entities SET studypoints=@after WHERE uid=@uid AND COALESCE(studypoints,0)=@before","uid",(long)p.Entity.UID,"before",study,"after",nextStudy)!=1)
                        throw new InvalidOperationException("Meditation Study balance requires reload.");
                    MysteryFruit.Debit(c,t,p,pack);if(fruit!=null)QuestInventory.Insert(c,t,p.Entity.UID,fruit);
                    QuestInventory.Execute(c,t,@"INSERT INTO revora_meditation_openings(id,uid,pack_uid,before_stack,outcome,fruit_uid,chi_before,chi_after,study_before,study_after,opened_at)
 VALUES(@id,@uid,@pack,@stack,@outcome,@fruit,@cb,@ca,@sb,@sa,@now)","id",Guid.NewGuid(),"uid",(long)p.Entity.UID,"pack",(long)pack.UID,"stack",(int)pack.StackSize,
                        "outcome",outcome,"fruit",fruit==null?(object)DBNull.Value:(long)fruit.UID,"cb",(long)chi,"ca",(long)nextChi,"sb",study,"sa",nextStudy,"now",now);
                    if(BeforeCommit!=null)BeforeCommit();if(!Ready(p) || !ChiWallet.Matches(p,chi))throw new InvalidOperationException("Meditation owner changed.");t.Commit();
                }}
                if(BeforeInstall!=null)BeforeInstall();QuestInventory.Install(p,pack.StackSize<=1?new[]{pack.UID}:new uint[0],fruit==null?new Network.GamePackets.ConquerItem[0]:new[]{fruit});
                if(pack.StackSize>1){pack.StackSize--;pack.Mode=Enums.ItemMode.Update;pack.Send(p);pack.Mode=Enums.ItemMode.Default;}
                CharacterProgression.ApplyCommitted(p,profile);ChiWallet.Committed(p,chi,nextChi);p.Entity.SubClasses.StudyPoints=(ushort)nextStudy;
                if(outcome==1)p.Send(new Network.GamePackets.ChiPowers(true).Query(p));
                if(outcome==2)p.Send(new Network.GamePackets.SubClassShowFull(0){ID=8,Study=(ushort)nextStudy,StudyReceive=3000}.ToArray());
                p.Send(outcome==0?"You received a bound Mystery Fruit.":outcome==1?"You received 1,000 Chi Points.":"You received 3,000 Study Points.");
            }catch(Exception ex){QuestInventory.Fail(p,"meditation-pack",ex);}}
            finally{SessionInstall.Exit(p.Entity.UID);}return true;
        }
    }

}
