// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.Threading;
using Q=Revora.Game.Features.QuestInventory;
using System.Runtime.CompilerServices;
using System.Linq;
using Npgsql;
using Revora.Client;
using Revora.Database;

namespace Revora.Network.GamePackets
{
    public unsafe class Enlight : Writer, Interfaces.IPacket
    {
        byte[] Buffer;
        public Enlight(bool Create)
        {
            if (Create)
            {
                Buffer = new byte[40];
                WriteUInt16(32, 0, Buffer);
                WriteUInt16(1127, 2, Buffer);
                WriteUInt32((uint)Time32.timeGetTime().GetHashCode(), 4, Buffer);
            }
        }
        public uint Enlighter
        {
            get { return BitConverter.ToUInt32(Buffer, 12); }
            set { WriteUInt32(value, 12, Buffer); }
        }
        public uint Enlighted
        {
            get { return BitConverter.ToUInt32(Buffer, 16); }
            set { WriteUInt32(value, 16, Buffer); }
        }
        public byte[] ToArray()
        {
            return Buffer;
        }
        public void Deserialize(byte[] buffer)
        {
            Buffer = buffer;
        }
        public void Send(Client.GameState client)
        {
            client.Send(Buffer);
        }
        public static void Process(byte[] packet, Client.GameState client)
        {
            if(packet==null || packet.Length!=40 || BitConverter.ToUInt16(packet,0)!=32
                || BitConverter.ToUInt16(packet,2)!=1127 || BitConverter.ToUInt64(packet,32)!=Revora.Network.PacketHandler.ClientSeal
                || !Q.Player(client) || BitConverter.ToUInt32(packet,12)!=client.Entity.UID)return;
            Client.GameState target;
            if(!Kernel.GamePool.TryGetValue(BitConverter.ToUInt32(packet,16),out target) || !Eligible(client,target))return;
            object first=client.Entity.UID<target.Entity.UID?client.ItemSyncRoot:target.ItemSyncRoot;
            object second=client.Entity.UID<target.Entity.UID?target.ItemSyncRoot:client.ItemSyncRoot;
            bool one=false,two=false;
            try {
                if(!(one=Monitor.TryEnter(first)) || !(two=Monitor.TryEnter(second)) || !Eligible(client,target))return;
                ProcessCurrent(packet,client,target);
            }finally{if(two)Monitor.Exit(second);if(one)Monitor.Exit(first);}
        }
        internal static bool Eligible(Client.GameState sender,Client.GameState target)
        {
            return Q.Player(sender) && Q.Player(target) && sender.Entity.UID!=target.Entity.UID
                && sender.Entity.Level>=90 && sender.Entity.Level>=target.Entity.Level+20
                && sender.Map!=null && ReferenceEquals(sender.Map,target.Map) && sender.Entity.MapID==target.Entity.MapID
                && Kernel.GetDistance(sender.Entity.X,sender.Entity.Y,target.Entity.X,target.Entity.Y)<=18
                && sender.Trade!=null && target.Trade!=null && !sender.Trade.InTrade && !target.Trade.InTrade
                && sender.Booth==null && target.Booth==null;
        }
        private static void ProcessCurrent(byte[] packet,Client.GameState client,Client.GameState target)
        {
            Revora.Game.Features.EnlightenmentProgress.Apply(packet,client,target);
        }
    }

}

namespace Revora.Game.Features
{
    internal static class EnlightenmentProgress
    {
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        internal static Action<string> BeforeCommit,BeforeInstall;
        private static bool enabled;
        private sealed class Online {internal int Day;internal DateTime Minute;}
        private static readonly ConditionalWeakTable<GameState,Online> online=new ConditionalWeakTable<GameState,Online>();
        private static int Day(DateTime now){return int.Parse(now.ToLocalTime().ToString("yyyyMMdd",System.Globalization.CultureInfo.InvariantCulture));}
        internal static void Initialize(){
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_enlight_days(
uid bigint PRIMARY KEY REFERENCES entities(uid),day integer NOT NULL);
CREATE TABLE IF NOT EXISTS revora_enlight_awards(
id bigserial PRIMARY KEY,donor bigint REFERENCES entities(uid),recipient bigint NOT NULL REFERENCES entities(uid),
day integer NOT NULL,nonce bigint,experience bigint NOT NULL CHECK(experience>=0),
remaining integer NOT NULL CHECK(remaining BETWEEN 0 AND 1200),legacy boolean NOT NULL,
created_at timestamptz NOT NULL,paid_at timestamptz,
CHECK((remaining=0 AND paid_at IS NOT NULL) OR (remaining>0 AND paid_at IS NULL)),
CHECK((legacy AND donor IS NULL AND nonce IS NULL) OR (NOT legacy AND donor IS NOT NULL AND donor<>recipient AND nonce IS NOT NULL)));
CREATE UNIQUE INDEX IF NOT EXISTS revora_enlight_request ON revora_enlight_awards(donor,recipient,day,nonce) WHERE NOT legacy;
CREATE INDEX IF NOT EXISTS revora_enlight_pending ON revora_enlight_awards(recipient,id) WHERE paid_at IS NULL;");}enabled=true;
        }
        private static ushort DailyPoints(GameState p){
            if(p.Entity.Level<90)return 0;int rank=(int)p.Entity.NobilityRank;
            int noble=rank==1 || rank==3?100:rank==5 || rank==7?200:rank==9?300:rank==12?400:0;
            int vip=p.Entity.VIPLevel==0?0:p.Entity.VIPLevel<=3?100:p.Entity.VIPLevel<=5?200:300;
            return (ushort)(100+noble+vip);
        }
        internal static void Loaded(GameState p,DateTime savedReset){
            online.Remove(p);if(!enabled || p.Fake)return;
            lock(p.ItemSyncRoot)try{RefreshDay(p,savedReset);}
            catch(Exception ex){Q.Fail(p,"enlightenment-day-load",ex);throw;}
        }
        private static void RefreshDay(GameState p,DateTime? seed=null){
            DateTime now=Clock();int today=Day(now);ushort points=p.Entity.EnlightenPoints,wait=p.Entity.EnlightmentTime;byte received=p.Entity.ReceivedEnlightenPoints;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Q.Execute(c,t,"SET LOCAL lock_timeout='2s';SET LOCAL statement_timeout='5s'");
                using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))if(q.ExecuteScalar()==null)throw new InvalidOperationException("Enlightenment owner missing.");
                object value;using(var q=Q.Command(c,t,"SELECT day FROM revora_enlight_days WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))value=q.ExecuteScalar();
                bool fresh=value==null;int previous=fresh?int.Parse((seed ?? p.LastResetTime).ToString("yyyyMMdd",System.Globalization.CultureInfo.InvariantCulture)):Convert.ToInt32(value);
                if(previous!=today){points=DailyPoints(p);received=0;}
                if(fresh){
                    Q.Execute(c,t,"INSERT INTO revora_enlight_days(uid,day) VALUES(@uid,@day)","uid",(long)p.Entity.UID,"day",today);
                    if(wait>100)throw new InvalidOperationException("Legacy enlightenment wait exceeds five awards.");
                    // Preserve remaining online minutes, replacing the old ten-minute payout schedule.
                    for(int minutes=wait;minutes>0;){int part=minutes%20==0?20:minutes%20;minutes-=part;
                        Q.Execute(c,t,@"INSERT INTO revora_enlight_awards(recipient,day,experience,remaining,legacy,created_at)
VALUES(@uid,@day,@xp,@seconds,true,@now)","uid",(long)p.Entity.UID,"day",today,"xp",checked((long)ExperienceMath.Scale(p.ExpBall,1,10)),"seconds",part*60,"now",now);
                    }
                }else if(previous!=today)Q.Execute(c,t,"UPDATE revora_enlight_days SET day=@day WHERE uid=@uid","day",today,"uid",(long)p.Entity.UID);
                EnlightenmentBalance.Persist(c,t,p,points,received,wait);t.Commit();
            }}
            EnlightenmentBalance.ApplyCommitted(p,points,received,wait);
            var state=online.GetValue(p,_=>new Online{Minute=now});state.Day=today;
        }
        internal static ushort PrepareCredit(GameState p,ushort units){
            if(!enabled || !Monitor.IsEntered(p.ItemSyncRoot) || !Q.Player(p))throw new InvalidOperationException("Enlightenment credit requires a loaded owner.");
            RefreshDay(p);return checked((ushort)(p.Entity.EnlightenPoints+units));
        }
        internal static void PersistCredit(NpgsqlConnection c,NpgsqlTransaction t,GameState p,ushort points){
            EnlightenmentBalance.Persist(c,t,p,points,p.Entity.ReceivedEnlightenPoints,p.Entity.EnlightmentTime);
            if(BeforeCommit!=null)BeforeCommit("credit");
        }
        internal static void InstallCredit(GameState p,ushort points){
            if(BeforeInstall!=null)BeforeInstall("credit");
            EnlightenmentBalance.ApplyCommitted(p,points,p.Entity.ReceivedEnlightenPoints,p.Entity.EnlightmentTime);
        }
        private static bool Ready(GameState p){return Q.Player(p) && CharacterProgression.Managed(p) && p.Spells!=null
            && !p.Entity.ContainsFlag((ulong)Revora.Network.GamePackets.FlagsUpdate.Flags.Cursed)
            && !(Config.TrainingGroundBlocksExperience && p.Map.BaseID==1039);}
        private static ExperienceRewardPlan Plan(GameState p,ulong xp){return new ExperienceRewardPlan(CharacterProgression.Capture(p),xp,Config.LevelCap,Config.AttributePointsPerLevel,ReincarnationRecovery.Snapshot(p));}
        private static ulong AutoHunt(GameState p,ulong xp){return p.Entity.InAutoHunt?checked(p.Entity.AutoHuntEXP+xp/Math.Max(1u,Config.AutoHuntExperienceDivisor)):p.Entity.AutoHuntEXP;}
        private static void InstallXp(GameState p,ExperienceRewardPlan plan,ExperienceRewards.Effects effects,ulong autoHunt){
            ReincarnationRecovery.Committed(p,plan.Restored);MentorExperience.Apply(p,effects.MentorCredit);
            p.ApplyExperienceReward(plan,effects,autoHunt,true);
        }
        internal static void Apply(byte[] packet,GameState donor,GameState recipient){
            if(!enabled || !Ready(donor) || !Ready(recipient) || !SessionInstall.TryEnter(donor.Entity.UID))return;
            bool second=false;
            try{if(!(second=SessionInstall.TryEnter(recipient.Entity.UID)))return;
                lock(MentorExperience.Sync){
                    if(!Revora.Network.GamePackets.Enlight.Eligible(donor,recipient))return;
                    RefreshDay(donor);RefreshDay(recipient);
                    if(donor.Entity.EnlightenPoints<100 || recipient.Entity.ReceivedEnlightenPoints>=5 || recipient.Entity.EnlightmentTime>80)return;
                    DateTime now=Clock();int day=Day(now);long nonce=BitConverter.ToUInt32(packet,4);ulong xp=ExperienceMath.Scale(recipient.ExpBall,1,10);if(xp==0)return;
                    var plan=Plan(recipient,xp);ulong autoHunt=AutoHunt(recipient,xp);ExperienceRewards.Effects effects;
                    bool firstAward=recipient.Entity.EnlightmentTime==0;
                    ushort donorPoints=(ushort)(donor.Entity.EnlightenPoints-100),wait=(ushort)(recipient.Entity.EnlightmentTime+20);byte count=(byte)(recipient.Entity.ReceivedEnlightenPoints+1);
                    using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                        Q.Execute(c,t,"SET LOCAL lock_timeout='2s';SET LOCAL statement_timeout='5s'");
                        using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=ANY(@ids) ORDER BY uid FOR UPDATE","ids",new long[]{donor.Entity.UID,recipient.Entity.UID}))using(var r=q.ExecuteReader()){int rows=0;while(r.Read())rows++;if(rows!=2)throw new InvalidOperationException("Enlightenment pair missing.");}
                        using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_enlight_awards WHERE donor=@donor AND recipient=@recipient AND day=@day AND nonce=@nonce AND NOT legacy)","donor",(long)donor.Entity.UID,"recipient",(long)recipient.Entity.UID,"day",day,"nonce",nonce))if((bool)q.ExecuteScalar())return;
                        EnlightenmentBalance.Persist(c,t,donor,donorPoints,donor.Entity.ReceivedEnlightenPoints,donor.Entity.EnlightmentTime);
                        EnlightenmentBalance.Persist(c,t,recipient,recipient.Entity.EnlightenPoints,count,wait);
                        CharacterProgression.Persist(c,t,recipient,plan.After);effects=ExperienceRewards.Persist(c,t,recipient,plan);ReincarnationRecovery.Consume(c,t,recipient,plan.Restored);
                        Q.Execute(c,t,@"INSERT INTO revora_enlight_awards(donor,recipient,day,nonce,experience,remaining,legacy,created_at)
VALUES(@donor,@recipient,@day,@nonce,@xp,1200,false,@now)","donor",(long)donor.Entity.UID,"recipient",(long)recipient.Entity.UID,"day",day,"nonce",nonce,"xp",checked((long)xp),"now",now);
                        if(BeforeCommit!=null)BeforeCommit("accept");if(!Ready(donor) || !Ready(recipient) || !Revora.Network.GamePackets.Enlight.Eligible(donor,recipient))throw new InvalidOperationException("Enlightenment pair changed.");t.Commit();
                    }}
                    if(BeforeInstall!=null)BeforeInstall("accept");
                    EnlightenmentBalance.ApplyCommitted(donor,donorPoints,donor.Entity.ReceivedEnlightenPoints,donor.Entity.EnlightmentTime);
                    EnlightenmentBalance.ApplyCommitted(recipient,recipient.Entity.EnlightenPoints,count,wait);
                    if(firstAward)online.GetValue(recipient,_=>new Online{Day=day}).Minute=now;
                    InstallXp(recipient,plan,effects,autoHunt);
                    recipient.SendScreen(packet,true);
                }
            }catch(Exception ex){Q.Fail(donor,"enlightenment-accept",ex);Q.Fail(recipient,"enlightenment-receive",ex);}
            finally{if(second)SessionInstall.Exit(recipient.Entity.UID);SessionInstall.Exit(donor.Entity.UID);}
        }
        internal static void Tick(GameState p){
            if(!enabled || !Ready(p))return;DateTime now=Clock();Online state;
            if(!online.TryGetValue(p,out state)){Loaded(p,p.LastResetTime);return;}
            if(state.Day==Day(now) && now<state.Minute.AddMinutes(1))return;
            if(!SessionInstall.TryEnter(p.Entity.UID))return;
            try{lock(p.ItemSyncRoot)lock(MentorExperience.Sync){
                if(!Ready(p))return;if(state.Day!=Day(now))RefreshDay(p);
                if(now<state.Minute.AddMinutes(1))return;state.Minute=now;
                if(p.Entity.EnlightmentTime==0)return;
                ExperienceRewardPlan plan=null;ExperienceRewards.Effects effects=null;ushort wait;ulong autoHunt=p.Entity.AutoHuntEXP;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    Q.Execute(c,t,"SET LOCAL lock_timeout='2s';SET LOCAL statement_timeout='5s'");
                    using(var q=Q.Command(c,t,"SELECT uid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))if(q.ExecuteScalar()==null)throw new InvalidOperationException("Enlightenment recipient missing.");
                    long id,xp;int remaining;
                    using(var q=Q.Command(c,t,"SELECT id,experience,remaining FROM revora_enlight_awards WHERE recipient=@uid AND paid_at IS NULL ORDER BY id LIMIT 1 FOR UPDATE","uid",(long)p.Entity.UID))using(var r=q.ExecuteReader()){
                        if(!r.Read())throw new InvalidOperationException("Enlightenment pending award missing.");id=r.GetInt64(0);xp=r.GetInt64(1);remaining=r.GetInt32(2);
                    }
                    long seconds;using(var q=Q.Command(c,t,"SELECT COALESCE(sum(remaining),0) FROM revora_enlight_awards WHERE recipient=@uid AND paid_at IS NULL","uid",(long)p.Entity.UID))seconds=Convert.ToInt64(q.ExecuteScalar());
                    if((seconds+59)/60!=p.Entity.EnlightmentTime)throw new InvalidOperationException("Enlightenment timer changed.");
                    int spent=Math.Min(60,remaining);remaining-=spent;wait=checked((ushort)((seconds-spent+59)/60));
                    if(remaining==0){autoHunt=AutoHunt(p,checked((ulong)xp));plan=Plan(p,checked((ulong)xp));CharacterProgression.Persist(c,t,p,plan.After);effects=ExperienceRewards.Persist(c,t,p,plan);ReincarnationRecovery.Consume(c,t,p,plan.Restored);}
                    Q.Execute(c,t,"UPDATE revora_enlight_awards SET remaining=@remaining,paid_at=CASE WHEN @remaining=0 THEN @now ELSE NULL END WHERE id=@id AND paid_at IS NULL","remaining",remaining,"now",now,"id",id);
                    EnlightenmentBalance.Persist(c,t,p,p.Entity.EnlightenPoints,p.Entity.ReceivedEnlightenPoints,wait);
                    if(BeforeCommit!=null)BeforeCommit("tick");if(!Ready(p))throw new InvalidOperationException("Enlightenment timer owner changed.");t.Commit();
                }}
                if(BeforeInstall!=null)BeforeInstall("tick");EnlightenmentBalance.ApplyCommitted(p,p.Entity.EnlightenPoints,p.Entity.ReceivedEnlightenPoints,wait);if(plan!=null)InstallXp(p,plan,effects,autoHunt);
            }}catch(Exception ex){Q.Fail(p,"enlightenment-tick",ex);}finally{SessionInstall.Exit(p.Entity.UID);}
        }
    }
    // The durable image is distinct from unsaved daily resets/countdown updates.
    internal static class EnlightenmentBalance
    {
        private static readonly string[] columns={"enlightenpoints","enlightsreceived","enlightmentwait"};
        private sealed class Image {internal Entity Entity;internal long?[] Saved;}
        private static readonly ConditionalWeakTable<GameState,Image> images=new ConditionalWeakTable<GameState,Image>();
        internal static void Forget(GameState p){images.Remove(p);}
        private static void RequireOwner(GameState p){
            if(p==null || !Monitor.IsEntered(p.ItemSyncRoot ?? p) || p.Entity.PersistenceRequiresReload)
                throw new InvalidOperationException("Enlightenment requires a healthy owner lock.");
        }
        private static Image State(GameState p){Image image;return images.TryGetValue(p,out image) && ReferenceEquals(image.Entity,p.Entity)?image:null;}
        private static void Validate(long?[] values){
            if(values==null || values.Length!=3)throw new InvalidOperationException("Enlightenment image length.");
            for(int i=0;i<3;i++)if(values[i]<0 || values[i]>(i==1?byte.MaxValue:ushort.MaxValue))throw new OverflowException("Invalid enlightenment balance.");
        }
        private static long?[] Capture(GameState p){return new long?[]{p.Entity.EnlightenPoints,p.Entity.ReceivedEnlightenPoints,p.Entity.EnlightmentTime};}
        private static void Remember(GameState p,long?[] values){Validate(values);images.Remove(p);images.Add(p,new Image{Entity=p.Entity,Saved=(long?[])values.Clone()});}
        internal static void Loaded(GameState p,MySqlReader reader){
            lock(p.ItemSyncRoot ?? p){images.Remove(p);try{Remember(p,new[]{reader.ReadNullableInt64Strict(columns[0]),reader.ReadNullableInt64Strict(columns[1]),reader.ReadNullableInt64Strict(columns[2])});}
                catch(Exception ex){Q.Fail(p,"enlightenment-load",ex);throw;}}
        }
        private static long?[] Baseline(GameState p){
            var image=State(p);if(image!=null)return image.Saved;
            // Bootstrap only unchanged fields; do not turn pending gains into a preimage.
            using(var q=new MySqlCommand(MySqlCommandType.SELECT).Select("entities").Where("uid",p.Entity.UID))
            using(var reader=new MySqlReader(q)){
                if(!reader.Read())throw new InvalidOperationException("Enlightenment owner missing.");
                var saved=new[]{reader.ReadNullableInt64Strict(columns[0]),reader.ReadNullableInt64Strict(columns[1]),reader.ReadNullableInt64Strict(columns[2])};Validate(saved);
                var current=Capture(p);for(int i=0;i<3;i++)if(saved[i].GetValueOrDefault()!=current[i])throw new InvalidOperationException("Enlightenment was not loaded.");
                return saved;
            }
        }
        internal static long?[] PrepareSave(GameState p,MySqlCommand command){
            RequireOwner(p);var saved=Baseline(p);var current=Capture(p);
            for(int i=0;i<3;i++){command.SetInt64Parameter(columns[i],current[i]);command.AndInt64Parameter(columns[i],saved[i]);}
            return current;
        }
        internal static void Committed(GameState p,long?[] values){RequireOwner(p);Remember(p,values);}
        internal static void Persist(NpgsqlConnection c,NpgsqlTransaction t,GameState p,ushort points,byte received,ushort wait){
            RequireOwner(p);if(t==null || t.Connection!=c)throw new InvalidOperationException("Enlightenment requires its owner transaction.");
            var saved=Baseline(p);
            using(var q=Q.Command(c,t,@"UPDATE entities SET enlightenpoints=@points,enlightsreceived=@received,enlightmentwait=@wait
WHERE uid=@uid AND enlightenpoints IS NOT DISTINCT FROM @oldpoints AND enlightsreceived IS NOT DISTINCT FROM @oldreceived
AND enlightmentwait IS NOT DISTINCT FROM @oldwait","uid",(long)p.Entity.UID,"points",(int)points,"received",(int)received,"wait",(int)wait)){
                string[] names={"oldpoints","oldreceived","oldwait"};for(int i=0;i<3;i++)q.Parameters.Add(names[i],NpgsqlTypes.NpgsqlDbType.Bigint).Value=(object)saved[i] ?? DBNull.Value;
                if(q.ExecuteNonQuery()!=1)throw new InvalidOperationException("Enlightenment balance changed; reload required.");
            }
        }
        internal static void ApplyCommitted(GameState p,ushort points,byte received,ushort wait){
            RequireOwner(p);Remember(p,new long?[]{points,received,wait});p.Entity.EnlightenPoints=points;p.Entity.ReceivedEnlightenPoints=received;p.Entity.EnlightmentTime=wait;
        }
    }
}
