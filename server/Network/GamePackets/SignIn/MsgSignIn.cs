using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Revora.Database;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Network.GamePackets
{
    public class MsgSignIn : Interfaces.IPacket
    {
        byte[] buffer;
        public MsgSignIn()
        {
            buffer = new byte[12 + 8];
            Writer.Write((ushort)(buffer.Length - 8), 0, buffer);
            Writer.Write(3200, 2, buffer);
        }
        public Actions Type
        {
            get { return (Actions)buffer[4]; }
            set { buffer[4] = (byte)value; }
        }
        public byte RewardsClaimed
        {
            get { return buffer[5]; }
            set { buffer[5] = value; }
        }
        public byte VIPChance
        {
            get { return buffer[6]; }
            set { buffer[6] = value; }
        }
        public int SignedDays
        {
            get { return BitConverter.ToInt32(buffer, 8); }
            set { Writer.Write(value, 8, buffer); }
        }
        public enum Actions : byte
        {
            Sign,
            LateSignIn,
            Reward,
            Show
        }
        public void Deserialize(byte[] Buffer)
        {
            buffer = Buffer;
        }
        public byte[] ToArray()
        {
            return buffer;
        }
        public void Send(Client.GameState client)
        {
            client.Send(buffer);
        }
        public static void Handle(Client.GameState client, byte[] packet)
        {
            if (packet == null || packet.Length < 20 || packet.Length > 64
                || BitConverter.ToUInt16(packet,0) + 8 != packet.Length
                || BitConverter.ToUInt16(packet,2) != 3200
                || BitConverter.ToUInt64(packet,packet.Length-8) != Revora.Network.PacketHandler.ClientSeal) return;
            Actions action = (Actions)packet[4];
            if (action == Actions.Sign || action == Actions.LateSignIn || action == Actions.Show)
                Run(client, action);
        }
        internal static bool CatalogReady()
        {
            var prizes = Database.SignInTable.Prizes;
            if (prizes == null) return false;
            foreach (byte day in new byte[] { 1, 2, 7, 14, 21, 30 })
            {
                Database.SignInTable.Prize prize;
                if (!prizes.TryGetValue(day, out prize) || prize == null || prize.PrizeItemID == 0)
                    return false;
            }
            return true;
        }
        // Catalog thresholds are shared by normal and late sign-in. Zero means
        // the monthly ladder is complete, never another claim of its final pack.
        internal static byte NextMilestone(byte claimed)
        {
            switch (claimed)
            {
                case 0: return 2;
                case 2: return 7;
                case 7: return 14;
                case 14: return 21;
                case 21: return 30;
                default: return 0;
            }
        }
        internal static Func<DateTime> Clock = () => DateTime.Now;
        private static readonly object SchemaSync = new object();
        private static bool initialized;
        internal static void Initialize()
        {
            lock (SchemaSync)
            {
                if (initialized) return;
                using (var c = DataHolder.MySqlConnection) {
                    c.Open(); Q.Execute(c, null, @"CREATE TABLE IF NOT EXISTS revora_sign_in_state(
uid bigint PRIMARY KEY, month integer NOT NULL, signed text NOT NULL CHECK(signed ~ '^[01]{31}$'),
claimed integer NOT NULL, chances integer NOT NULL CHECK(chances BETWEEN 0 AND 255));
CREATE TABLE IF NOT EXISTS revora_sign_in_claims(
uid bigint NOT NULL, month integer NOT NULL, day integer NOT NULL CHECK(day BETWEEN 1 AND 31),
late boolean NOT NULL, cost integer NOT NULL CHECK(cost IN(4000,10000)),
milestone integer NOT NULL, rewards bigint[] NOT NULL, claimed_at timestamptz NOT NULL,
PRIMARY KEY(uid,month,day));");
                }
                initialized = true;
            }
        }
        private static string LegacyDays(string text)
        {
            var values = (text ?? "").Split('#');
            if (values.Length < 31) return new string('0',31);
            if (values.Take(31).Any(x => x != "0" && x != "1"))
                throw new InvalidOperationException("Invalid saved sign-in days.");
            return string.Concat(values.Take(31));
        }
        private static bool Ready(Client.GameState p, bool claim)
        {
            return Q.Player(p, claim) && p.Booth == null && p.Trade != null && !p.Trade.InTrade;
        }
        private static void Refuse(Client.GameState p, string message)
        {
            p.Send(new Message(message, System.Drawing.Color.Red, Message.Whisper));
        }
        private static void Run(Client.GameState p, Actions action)
        {
            bool claim = action != Actions.Show;
            if (!initialized || !Ready(p, claim)) return;
            lock (p.ItemSyncRoot) try {
                if (!Ready(p, claim)) return;
                if (claim && !CatalogReady()) { Refuse(p,"The sign-in clerk cannot issue rewards right now. Please return later."); return; }
                DateTime now = Clock(); int month = now.Year * 100 + now.Month;
                string days; byte claimed, chances; uint balance;
                var gifts = new List<ConquerItem>();
                using (var c = DataHolder.MySqlConnection) { c.Open(); using (var t = c.BeginTransaction()) {
                    using (var q = Q.Command(c,t,"SELECT dailysigneddays,COALESCE(dailysignrewards,0),COALESCE(dailysignvipchances,0),COALESCE(revorapoint,0) FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                    using (var r = q.ExecuteReader()) {
                        if (!r.Read()) throw new InvalidOperationException("Sign-in owner missing.");
                        days = LegacyDays(Convert.ToString(r[0])); claimed = Convert.ToByte(r[1]);
                        chances = Convert.ToByte(r[2]); balance = Convert.ToUInt32(r[3]);
                    }
                    using (var q = Q.Command(c,t,"SELECT month,signed,claimed,chances FROM revora_sign_in_state WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                    using (var r = q.ExecuteReader()) if (r.Read()) {
                        int savedMonth = Convert.ToInt32(r[0]);
                        if (savedMonth > month) { Refuse(p,"The sign-in calendar is unavailable. Please return later."); return; }
                        if (savedMonth == month) { days = r.GetString(1); claimed = Convert.ToByte(r[2]); chances = Convert.ToByte(r[3]); }
                        else { days = new string('0',31); claimed = 0; chances = 10; }
                    }
                    if (balance != p.Entity.RevoraPoint) throw new InvalidOperationException("Sign-in balance requires reload.");
                    if (claim) {
                        bool late = action == Actions.LateSignIn;
                        uint cost = late ? 10000u : 4000u;
                        if ((!late && (p.Entity.Level < 140 || p.Entity.Reborn != 2 || p.Entity.VIPLevel < 5))
                            || (late && (p.Entity.VIPLevel < 7 || chances == 0))) {
                            Refuse(p,late ? "Late sign-in requires VIP 7 and an unused chance." : "Daily sign-in requires level 140, second rebirth and VIP 5."); return;
                        }
                        int day = late ? days.Substring(0,now.Day).IndexOf('0') : now.Day-1;
                        if (day < 0 || days[day] == '1') return;
                        if (balance < cost) { Refuse(p,"You do not have enough Revora Points to sign in."); return; }
                        var signed = days.ToCharArray(); signed[day] = '1'; days = new string(signed);
                        byte next = NextMilestone(claimed);
                        byte milestone = next != 0 && days.Count(x=>x=='1') >= next ? next : (byte)0;
                        if (milestone != 0) claimed = milestone;
                        uint[] types = milestone == 0 ? new[]{SignInTable.Prizes[1].PrizeItemID}
                            : new[]{SignInTable.Prizes[1].PrizeItemID,SignInTable.Prizes[milestone].PrizeItemID};
                        var bag = new List<uint>();
                        using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid","uid",(long)p.Entity.UID))
                        using(var r=q.ExecuteReader()) while(r.Read()) bag.Add(checked((uint)Convert.ToInt64(r[0])));
                        if (!bag.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i))) throw new InvalidOperationException("Sign-in inventory requires reload.");
                        if (bag.Count + types.Length > 40) { Refuse(p,"Make room for all your sign-in rewards before returning."); return; }
                        foreach(uint type in types) {
                            var gift=Q.New(type,false); gift.Days=2; gift.DayStamp=now; gift.TimeLeftInMinutes=172800;
                            if(Q.Execute(c,t,@"INSERT INTO items(uid,id,entityid,position,warehouse,stacksize,bound,plus,durability,maximdurability,days,daystamp)
VALUES(@item,@type,@uid,0,0,1,0,0,@dur,@dur,'2',@stamp)","item",(long)gift.UID,"type",(int)type,
                                "uid",(long)p.Entity.UID,"dur",(int)gift.Durability,"stamp",now.ToBinary())!=1)
                                throw new InvalidOperationException("Sign-in reward not saved.");
                            gifts.Add(gift);
                        }
                        if(late) chances--;
                        balance-=cost;
                        if(Q.Execute(c,t,@"INSERT INTO revora_sign_in_claims(uid,month,day,late,cost,milestone,rewards,claimed_at)
VALUES(@uid,@month,@day,@late,@cost,@milestone,@rewards,@now)","uid",(long)p.Entity.UID,"month",month,"day",day+1,
                            "late",late,"cost",(int)cost,"milestone",(int)milestone,"rewards",gifts.Select(i=>(long)i.UID).ToArray(),"now",new DateTimeOffset(now))!=1)
                            throw new InvalidOperationException("Sign-in receipt not saved.");
                    }
                    if(Q.Execute(c,t,@"INSERT INTO revora_sign_in_state(uid,month,signed,claimed,chances) VALUES(@uid,@month,@days,@claimed,@chances)
ON CONFLICT(uid) DO UPDATE SET month=EXCLUDED.month,signed=EXCLUDED.signed,claimed=EXCLUDED.claimed,chances=EXCLUDED.chances",
                        "uid",(long)p.Entity.UID,"month",month,"days",days,"claimed",(int)claimed,"chances",(int)chances)!=1)
                        throw new InvalidOperationException("Sign-in state not saved.");
                    if(Q.Execute(c,t,@"UPDATE entities SET dailysigneddays=@days,dailysignrewards=@claimed,dailysignvipchances=@chances,revorapoint=@balance WHERE uid=@uid",
                        "uid",(long)p.Entity.UID,"days",string.Join("#",days.Select(x=>x.ToString()))+"#","claimed",(int)claimed,"chances",(int)chances,"balance",(long)balance)!=1)
                        throw new InvalidOperationException("Sign-in owner state not saved.");
                    if(!Ready(p,claim)) throw new InvalidOperationException("Sign-in session changed.");
                    t.Commit();
                }}
                p.Entity.DailySignDays=days.Select(x=>(byte)(x-'0')).ToArray();
                p.Entity.DailySignRewards=claimed; p.Entity.DailySignVIPChances=chances;
                if(claim) p.Entity.ApplyCommittedRevoraPoints(balance);
                Q.Install(p,new uint[0],gifts);
                SendState(p);
            } catch(Exception ex) {
                foreach(var frame in (new System.Diagnostics.StackTrace(ex,false).GetFrames() ?? new System.Diagnostics.StackFrame[0]).Take(4)) {
                    var method=frame.GetMethod();
                    Console.WriteLine("[sign-in] failure_method="+(method.DeclaringType==null?"unknown":method.DeclaringType.Name)+"."+method.Name);
                }
                Q.Fail(p,"sign-in-issuance",ex);
            }
        }
        public static void Show(Client.GameState client) { Run(client,Actions.Show); }
        private static void SendState(Client.GameState client)
        {
            var msg=new MsgSignIn { Type=Actions.Show };
            byte claimed=client.Entity.DailySignRewards;
            msg.RewardsClaimed=claimed==2?(byte)1:claimed==7?(byte)2:claimed==14?(byte)3:claimed==21?(byte)4:claimed==28||claimed==30?(byte)5:(byte)0;
            msg.VIPChance=client.Entity.VIPLevel==0?(byte)0:client.Entity.DailySignVIPChances;
            int mask=0; for(int n=0;n<31;n++) if(client.Entity.DailySignDays[n]!=0) mask|=1<<n;
            msg.SignedDays=mask; msg.Send(client);
        }
        public static void Reset()
        {
            // Only pre-ledger characters depend on the legacy startup month marker.
            // Migrated characters roll over by full year/month under their owner lock.
            Initialize();
            using(var c=DataHolder.MySqlConnection) { c.Open(); Q.Execute(c,null,@"UPDATE entities e SET dailysigneddays='',dailysignrewards=0,dailysignvipchances=10
WHERE NOT EXISTS(SELECT 1 FROM revora_sign_in_state s WHERE s.uid=e.uid)"); }
        }
    }
}
