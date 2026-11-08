using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Revora.Client;
using Revora.Database;
using Revora.Game.ConquerStructures.Society;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features {
    internal static class GuildDonations {
        private sealed class Repeat { internal bool Cp;internal uint Amount;internal DateTime Until; }
        private static readonly ConditionalWeakTable<GameState,Repeat> repeats=new ConditionalWeakTable<GameState,Repeat>();
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        private static bool Member(GameState p,Guild g) {
            Guild live;Guild.Member member;
            return Q.Player(p,false) && p.Trade!=null && !p.Trade.InTrade && p.Booth==null && g!=null
                && ReferenceEquals(p.Guild,g) && p.Entity.GuildID==g.ID && Kernel.Guilds.TryGetValue(g.ID,out live) && ReferenceEquals(live,g)
                && g.Members.TryGetValue(p.Entity.UID,out member) && ReferenceEquals(member,p.AsMember) && member.GuildID==g.ID;
        }
        internal static bool Donate(GameState p,uint amount,bool cp) {
            if(amount==0 || !Q.Player(p,false) || !Monitor.IsEntered(p.ItemSyncRoot))return false;
            uint uid=p.Entity.UID;if(!SessionInstall.TryEnter(uid))return false;
            try {
            var g=p.Guild;if(!Member(p,g))return false;
            if(!Monitor.TryEnter(GuildLeadership.Gate(g)))return false;
            try {
                if(!Member(p,g))return false;var stamp=repeats.GetOrCreateValue(p);var now=Clock();
                if(stamp.Amount==amount && stamp.Cp==cp && now<stamp.Until)return false;
                if(cp ? p.Entity.ConquerPoints<amount : p.Entity.Money<amount)return false;
                ulong donation=cp?p.AsMember.ConquerPointDonation:p.AsMember.SilverDonation;
                ulong nextDonation=checked(donation+amount),silver=p.Entity.Money-(cp?0:amount);
                uint points=p.Entity.ConquerPoints-(cp?amount:0);decimal fund;
                if(nextDonation>long.MaxValue)return false;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    SkillTable.LockOwner(c,t,p);
                    using(var q=Q.Command(c,t,cp?"SELECT COALESCE(guildconquerpointdonation,0) FROM entities WHERE uid=@uid AND guildid=@guild FOR UPDATE":"SELECT COALESCE(guildsilverdonation,0) FROM entities WHERE uid=@uid AND guildid=@guild FOR UPDATE",
                        "uid",(long)p.Entity.UID,"guild",(long)g.ID)) {
                        var value=q.ExecuteScalar();if(value==null || Convert.ToDecimal(value)!=(decimal)donation)throw new InvalidOperationException("Guild donation membership requires reload.");
                    }
                    using(var q=Q.Command(c,t,cp?"SELECT conquerpointfund FROM guilds WHERE id=@guild FOR UPDATE":"SELECT silverfund FROM guilds WHERE id=@guild FOR UPDATE","guild",(long)g.ID)) {
                        var value=q.ExecuteScalar();if(value==null)throw new InvalidOperationException("Guild donation owner missing.");fund=Convert.ToDecimal(value)+amount;
                    }
                    if(fund<amount || fund>(cp?(decimal)uint.MaxValue:long.MaxValue))return false;
                    if(cp) {
                        if(Q.Execute(c,t,"UPDATE entities SET conquerpoints=@next,guildconquerpointdonation=@donation WHERE uid=@uid AND conquerpoints=@before",
                            "next",(long)points,"donation",(long)nextDonation,"uid",(long)p.Entity.UID,"before",(long)p.Entity.ConquerPoints)!=1)throw new InvalidOperationException("Guild CP donation not saved.");
                    }else {
                        SilverBalance.Persist(c,t,p,silver);
                        if(Q.Execute(c,t,"UPDATE entities SET guildsilverdonation=@donation WHERE uid=@uid","donation",(long)nextDonation,"uid",(long)p.Entity.UID)!=1)throw new InvalidOperationException("Guild silver donation not saved.");
                    }
                    if(Q.Execute(c,t,cp?"UPDATE guilds SET conquerpointfund=@fund WHERE id=@guild":"UPDATE guilds SET silverfund=@fund WHERE id=@guild","fund",fund,"guild",(long)g.ID)!=1)
                        throw new InvalidOperationException("Guild fund was not saved.");
                    if(!Member(p,g))throw new InvalidOperationException("Guild donation session changed.");t.Commit();
                }}
                stamp.Amount=amount;stamp.Cp=cp;stamp.Until=now.AddSeconds(1);
                if(cp){p.Entity.ApplyCommittedConquerPoints(points);p.AsMember.ConquerPointDonation=nextDonation;g.ConquerPointFund=(uint)fund;}
                else{SilverBalance.ApplyCommitted(p,silver);p.AsMember.SilverDonation=nextDonation;p.AsMember.SilverDoantion=nextDonation;g.SilverFund=(ulong)fund;}
                GuildTable.BuildDonationRankings(g);
                g.SendGuild(p);return true;
            }catch(Exception ex){Q.Fail(p,"guild-donation",ex);return false;}
            finally{Monitor.Exit(GuildLeadership.Gate(g));}
            }finally{SessionInstall.Exit(uid);}
        }
    }
}
