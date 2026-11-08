using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Revora.Game.ConquerStructures;
using Revora.Client;
using Revora.Database;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    // Tracks the last durable purse separately from pending legacy Chi gains/spends.
    internal static class ChiWallet
    {
        private sealed class State { internal uint Points; }
        private static readonly ConditionalWeakTable<GameState,State> states=new ConditionalWeakTable<GameState,State>();
        internal static void Forget(GameState p) {states.Remove(p);}
        internal static void Loaded(GameState p,uint points) {states.GetValue(p,_=>new State()).Points=points;}
        internal static bool Ready(GameState p) {State s;return states.TryGetValue(p,out s);}
        internal static bool Matches(GameState p,uint points) {State s;return states.TryGetValue(p,out s) && s.Points==points;}
        internal static void Committed(GameState p,uint before,uint after) {
            if(!Monitor.IsEntered(p.ItemSyncRoot) || !Matches(p,before))throw new InvalidOperationException("Chi wallet requires its loaded owner revision.");
            states.GetValue(p,_=>new State()).Points=after;p.ChiPoints=after;
        }
        internal static void Save(GameState p,byte[] powers,byte[] retreat) {
            if(!Monitor.IsEntered(p.ItemSyncRoot))throw new InvalidOperationException("Chi save requires its owner lock.");
            State s;GameState current;
            if(p.Fake || p.Entity.PersistenceRequiresReload || !states.TryGetValue(p,out s)
                || !Kernel.GamePool.TryGetValue(p.Entity.UID,out current) || !ReferenceEquals(current,p))return;
            uint after=p.ChiPoints;
            using(var c=DataHolder.MySqlConnection){c.Open();
                if(Q.Execute(c,null,@"UPDATE chi SET points=@after,chipowers=@powers,rchipowers=@retreat WHERE uid=@uid AND points=@before",
                    "after",(long)after,"powers",powers,"retreat",retreat,"uid",(long)p.Entity.UID,"before",(long)s.Points)!=1)
                    throw new InvalidOperationException("Chi wallet changed outside its loaded session.");
            }
            s.Points=after;
        }
        internal static Action<GameState> BeforePowerCommit, BeforePowerInstall;
        private static byte[] EncodePowers(List<ChiPowerStructure> powers) {
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream)) {
                writer.Write(checked((byte)powers.Count));
                foreach(var power in powers)power.Serialize(writer);
                return stream.ToArray();
            }
        }
        internal static bool ChangePower(GameState p,Func<List<ChiPowerStructure>,uint> prepare) {
            if(!Q.Player(p) || !Ready(p) || !SessionInstall.TryEnter(p.Entity.UID))return false;
            uint uid=p.Entity.UID;
            try {lock(p.ItemSyncRoot) {
                if(!Q.Player(p) || !Ready(p) || p.ChiData==null || p.Booth!=null || p.Trade==null || p.Trade.InTrade)return false;
                uint before=p.ChiPoints;
                if(!Matches(p,before)){p.Send("Your Chi is still being saved. Try again shortly.");return false;}
                byte[] original=EncodePowers(p.ChiPowers);
                var proposed=p.ChiPowers.Select(power=>new ChiPowerStructure(power.Power){
                    Attributes=(ChiAttribute[])power.Attributes.Clone(),Points=power.Points,Retreat_TimeLeft=power.Retreat_TimeLeft}).ToList();
                uint cost=prepare(proposed);
                if(cost==0 || before<cost)return false;
                byte[] changed=EncodePowers(proposed);uint after=before-cost;
                byte level=p.Entity.Level,reborn=p.Entity.Reborn;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    Q.Execute(c,t,"SET LOCAL lock_timeout='2s'; SET LOCAL statement_timeout='5s'");
                    using(var q=Q.Command(c,t,"SELECT level,reborn FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)uid))
                    using(var r=q.ExecuteReader()) {
                        if(!r.Read() || Convert.ToByte(r[0])!=level || Convert.ToByte(r[1])!=reborn)
                            throw new InvalidOperationException("Chi eligibility changed.");
                    }
                    using(var q=Q.Command(c,t,"SELECT points,chipowers FROM chi WHERE uid=@uid FOR UPDATE","uid",(long)uid))
                    using(var r=q.ExecuteReader()) {
                        if(!r.Read() || Convert.ToUInt32(r[0])!=before)throw new InvalidOperationException("Chi purse changed.");
                        byte[] saved=r.IsDBNull(1)?new byte[0]:(byte[])r[1];
                        if(saved.Length==0)saved=new byte[]{0};
                        if(!saved.SequenceEqual(original))throw new InvalidOperationException("Chi powers changed.");
                    }
                    if(Q.Execute(c,t,"UPDATE chi SET points=@after,chipowers=@powers WHERE uid=@uid","after",(long)after,"powers",changed,"uid",(long)uid)!=1)
                        throw new InvalidOperationException("Chi mutation was not saved.");
                    var beforeCommit=BeforePowerCommit;if(beforeCommit!=null)beforeCommit(p);
                    if(!Q.Player(p) || !Matches(p,before) || p.ChiPoints!=before || p.Entity.Level!=level || p.Entity.Reborn!=reborn)
                        throw new InvalidOperationException("Chi session changed.");
                    t.Commit();
                }}
                // Any ambiguous commit or installation failure fences the session;
                // background saves must not overwrite a possibly committed result.
                var beforeInstall=BeforePowerInstall;if(beforeInstall!=null)beforeInstall(p);
                Committed(p,before,after);
                lock(ChiTable.SyncRoot){p.ChiPowers=proposed;p.ChiData.Powers=proposed;}
                return true;
            }}catch(Exception ex){Q.Fail(p,"chi-power",ex);return false;}
            finally {SessionInstall.Exit(uid);}
        }
        internal static uint RefillPrice(uint points,uint fullPrice) {
            if(points>=4000)return 0;
            return (uint)(((ulong)(4000-points)*fullPrice+3999)/4000);
        }
        internal static void Refill(GameState p) {
            if(!Q.Player(p) || !Ready(p) || p.Booth!=null || p.Trade==null || p.Trade.InTrade)return;
            lock(p.ItemSyncRoot)try {
                if(!Q.Player(p) || p.Booth!=null || p.Trade==null || p.Trade.InTrade)return;
                uint before=p.ChiPoints,cp=p.Entity.ConquerPoints;
                if(before>=4000)return;
                if(!Matches(p,before)){p.Send("Your Chi is still being saved. Try again shortly.");return;}
                uint cost=RefillPrice(before,Config.ChiFullRefillCp);if(cp<cost)return;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    using(var q=Q.Command(c,t,"SELECT conquerpoints FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID)) {
                        var stored=q.ExecuteScalar();if(stored==null)throw new InvalidOperationException("Chi refill owner missing.");
                        if(Convert.ToUInt32(stored)!=cp){p.Send("Your purse is still being saved. Try again shortly.");return;}
                    }
                    Q.Execute(c,t,"UPDATE entities SET conquerpoints=@cp WHERE uid=@uid","cp",(long)(cp-cost),"uid",(long)p.Entity.UID);
                    if(Q.Execute(c,t,"UPDATE chi SET points=4000 WHERE uid=@uid AND points=@before","uid",(long)p.Entity.UID,"before",(long)before)!=1)
                        throw new InvalidOperationException("Chi refill wallet preimage changed.");
                    t.Commit();
                }}
                Committed(p,before,4000);p.Entity.ApplyCommittedConquerPoints(cp-cost);
                p.Send(new Revora.Network.GamePackets.ChiPowers(true).Query(p));
            }catch(Exception ex){Q.Fail(p,"chi-refill",ex);}
        }
    }
}
