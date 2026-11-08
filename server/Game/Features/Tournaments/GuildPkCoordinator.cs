using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class GuildPkCoordinator
    {
        internal const uint Registrar=706072,Exit=706073;
        private static readonly object Gate=new object();
        private sealed class Arena {internal Guid Id;internal uint First,Second;internal DateTime Start,End;internal Map Map;}
        private static readonly Dictionary<Guid,Arena> arenas=new Dictionary<Guid,Arena>();
        private static bool enabled;
        internal static Action<string,uint> RewardProbe;
        private static DateTime nextTick,nextSchedule;
        private static DateTime Now {get{return GuildContestCoordinator.Clock();}}
        private sealed class PrizeOffer {internal Guid Run;internal uint Guild;internal DateTime Until;}
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<GameState,PrizeOffer> offers=new System.Runtime.CompilerServices.ConditionalWeakTable<GameState,PrizeOffer>();
        private static bool OfferPrize(GameState p) {
            offers.Remove(p);if(!GuildContestCoordinator.OfficerAt(p,Registrar))return false;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,@"SELECT w.contest,c.starts_at,w.timezone FROM revora_guild_pk_weeks w
JOIN revora_guild_contests c ON c.id=w.contest JOIN revora_guild_pk_placements p ON p.contest=w.contest
WHERE w.phase='complete' AND p.guild_id=@guild AND c.starts_at>=@now-interval '7 days' AND c.starts_at<=@now ORDER BY c.starts_at DESC LIMIT 8",
                "guild",(long)p.Guild.ID,"now",Now))using(var r=q.ExecuteReader())while(r.Read()) {
                    if(!GuildPkRules.PrizeWindow(r.GetFieldValue<DateTimeOffset>(1).UtcDateTime,TimeZoneInfo.FindSystemTimeZoneById(r.GetString(2)),Now))continue;
                    offers.Add(p,new PrizeOffer{Run=r.GetGuid(0),Guild=p.Guild.ID,Until=Now.AddMinutes(2)});return true;
                }}return false;
        }
        private static string ClaimPrize(GameState p) {
            PrizeOffer offer;if(!GuildContestCoordinator.OfficerAt(p,Registrar) || !offers.TryGetValue(p,out offer) || offer.Guild!=p.Guild.ID || Now>=offer.Until)
                return "Your leader or deputy may collect the guild's prize on Saturday, from 23:30 until midnight.";
            if(!SessionInstall.TryEnter(p.Entity.UID))return "Return in a moment.";
            var guild=p.Guild;var gate=GuildLeadership.Gate(guild);bool item=false,held=false;
            try {
                if(!(item=Monitor.TryEnter(p.ItemSyncRoot)) || !(held=Monitor.TryEnter(gate)))return "Return in a moment.";
                if(!GuildContestCoordinator.OfficerAt(p,Registrar) || !ReferenceEquals(p.Guild,guild))return "Your guild's leader or deputy must collect the prize.";
                int result;ulong silver;uint cps;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    result=GuildPkSchedule.Prize(c,t,offer.Run,p.Entity.UID,guild.ID,Now,out silver,out cps);
                    if(result==0)return "No prize can be collected for that tournament now.";
                    if(!GuildContestCoordinator.OfficerAt(p,Registrar) || !ReferenceEquals(p.Guild,guild))throw new InvalidOperationException("Guild PK prize owner changed.");t.Commit();
                }}
                guild.SilverFund=silver;guild.ConquerPointFund=cps;guild.SendGuild(p);
                return result==1?"Your guild's silver and CPs are in its treasury. Carry your banner proudly!":"Your guild has already collected this prize.";
            }catch(Exception ex){Q.Fail(p,"guild-pk-prize",ex);return "Reconnect before trying this claim again.";}
            finally{if(held)Monitor.Exit(gate);if(item)Monitor.Exit(p.ItemSyncRoot);SessionInstall.Exit(p.Entity.UID);}
        }
        private sealed class PoolOffer {internal Guid Key,Run;internal uint Guild;internal long Amount;internal DateTime Until;}
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<GameState,PoolOffer> pools=new System.Runtime.CompilerServices.ConditionalWeakTable<GameState,PoolOffer>();
        private static string FundPool(GameState p) {
            PoolOffer offer;if(!GuildContestCoordinator.OfficerAt(p,Registrar) || !pools.TryGetValue(p,out offer) || p.Guild.ID!=offer.Guild || Now>=offer.Until)return "Ask me about your guild's next battle first.";
            if(!SessionInstall.TryEnter(p.Entity.UID))return "Return in a moment.";
            var guild=p.Guild;var gate=GuildLeadership.Gate(guild);bool item=false,held=false;
            try {
                if(!(item=Monitor.TryEnter(p.ItemSyncRoot)) || !(held=Monitor.TryEnter(gate)))return "Return in a moment.";
                if(!GuildContestCoordinator.OfficerAt(p,Registrar) || !ReferenceEquals(p.Guild,guild))return "Your leader or deputy must pledge the reward.";
                int result;ulong balance;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    result=GuildPkRewardTable.Fund(c,t,offer.Key,offer.Run,offer.Guild,p.Entity.UID,offer.Amount,Now,out balance);
                    if(result==0)return "The pledge cannot be made. Check your treasury before the battle begins.";
                    if(!GuildContestCoordinator.OfficerAt(p,Registrar) || !ReferenceEquals(p.Guild,guild))throw new InvalidOperationException("Guild PK funding owner changed.");t.Commit();
                }}
                guild.SilverFund=balance;guild.SendGuild(p);
                return result==1?"Your pledge is held for this battle. Your members will earn their shares through combat and aid.":"That pledge is already held for your members.";
            }catch(Exception ex){Q.Fail(p,"guild-pk-funding",ex);return "Reconnect before pledging again.";}
            finally{if(held)Monitor.Exit(gate);if(item)Monitor.Exit(p.ItemSyncRoot);SessionInstall.Exit(p.Entity.UID);}
        }
        private static void RewardPulse(DateTime now) {
            var pending=new List<Tuple<Guid,uint>>();
            using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,@"SELECT p.run,p.guild_id FROM revora_guild_pk_pools p JOIN revora_guild_pk_matches m ON m.id=p.run
WHERE p.state='funded' AND m.state IN('complete','cancelled') ORDER BY m.ends_at,p.run,p.guild_id LIMIT 16"))using(var r=q.ExecuteReader())while(r.Read())pending.Add(Tuple.Create(r.GetGuid(0),checked((uint)r.GetInt64(1))));}
            foreach(var row in pending) {
                Revora.Game.ConquerStructures.Society.Guild guild;Kernel.Guilds.TryGetValue(row.Item2,out guild);
                object gate=guild==null?null:GuildLeadership.Gate(guild);bool held=false;
                try {
                    if(gate!=null && !(held=Monitor.TryEnter(gate)))continue;
                    ulong? refund;using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){GuildPkRewardTable.Allocate(c,t,row.Item1,row.Item2,now,out refund);t.Commit();}}
                    if(refund.HasValue && guild!=null){guild.SilverFund=refund.Value;foreach(var p in Kernel.GamePool.Values.Where(p=>Q.Player(p,false) && ReferenceEquals(p.Guild,guild)))guild.SendGuild(p);}
                }catch(Exception ex){Console.WriteLine("[guild-pk] pool settlement error="+ex.GetType().Name);}finally{if(held)Monitor.Exit(gate);}
            }
            var payments=new List<Tuple<Guid,uint,uint>>();
            using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,"SELECT run,guild_id,uid FROM revora_guild_pk_payouts WHERE paid_at IS NULL ORDER BY attempted_at NULLS FIRST,uid,run,guild_id LIMIT 64"))using(var r=q.ExecuteReader())while(r.Read())payments.Add(Tuple.Create(r.GetGuid(0),checked((uint)r.GetInt64(1)),checked((uint)r.GetInt64(2))));}
            foreach(var row in payments) {
                if(!SessionInstall.TryEnter(row.Item3))continue;GameState p=null;bool held=false;
                try {
                    if(Kernel.GamePool.TryGetValue(row.Item3,out p) && (!Q.Player(p,false) || !(held=Monitor.TryEnter(p.ItemSyncRoot)) || !Q.Player(p,false)))continue;
                    ulong balance;bool paid;
                    using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){paid=GuildPkRewardTable.Pay(c,t,row.Item1,row.Item2,row.Item3,p,now,out balance);t.Commit();}}
                    if(paid && p!=null){var probe=RewardProbe;if(probe!=null)probe("paid-commit",p.Entity.UID);SilverBalance.ApplyCommitted(p,balance);p.Send("Your guild's battle reward has reached your purse.");}
                }catch(Exception ex){if(p!=null)Q.Fail(p,"guild-pk-member-reward",ex);else Console.WriteLine("[guild-pk] offline reward error="+ex.GetType().Name);}
                finally{if(held)Monitor.Exit(p.ItemSyncRoot);SessionInstall.Exit(row.Item3);}
            }
        }
        internal static void Activate() {
            lock(Gate) {
                if(enabled)return;
                GuildContestCoordinator.PlaceNpc(Kernel.Maps[1002],Registrar,412,262,"GuildPKEmissary");
                GuildPkAdmission.Clock=()=>Now;enabled=true;
            }
        }
        private static void Open(Arena arena) {
            var original=Kernel.Maps[700];var map=Map.CreateDynamic(original.ID,original.Path,false);bool installed=false;
            try {
                GuildContestCoordinator.PlaceNpc(map,Exit,50,50,"GuildPKEnvoy");
                if(!GuildPkAdmission.BindMap(arena.Id,map))return;
                GuildPkBanner.Register(arena.Id,map,arena.Start,arena.End);
                arena.Map=map;arenas.Add(arena.Id,arena);installed=true;
            }finally {if(!installed)map.Dispose();}
        }
        private static bool Return(GameState p,Arena arena) {
            if(!Q.Player(p,false) || !SessionInstall.TryEnter(p.Entity.UID))return false;bool held=false;
            try {
                if(!(held=Monitor.TryEnter(p.ItemSyncRoot)))return false;
                GuildPkAdmission.Leave(p,arena.Id);
                // An unrelated/admin arrival has no transfer receipt. It must
                // not pin a finished private map or manufacture participation.
                if(ReferenceEquals(p.Map,arena.Map))p.Entity.Teleport(1002,327,303);
                if(ReferenceEquals(p.Map,arena.Map))return false;
                p.Send(new Message("",System.Drawing.Color.Red,Message.FirstRightCorner));return true;
            }finally {if(held)Monitor.Exit(p.ItemSyncRoot);SessionInstall.Exit(p.Entity.UID);}
        }
        private static bool Close(Arena arena) {
            foreach(var p in Kernel.GamePool.Values.Where(p=>p!=null && ReferenceEquals(p.Map,arena.Map)).Take(64).ToArray())Return(p,arena);
            var pending=new List<uint>();
            using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,"SELECT uid FROM revora_guild_pk_transfers WHERE run=@run AND state<>'returned' ORDER BY uid LIMIT 64","run",arena.Id))using(var r=q.ExecuteReader())while(r.Read())pending.Add(checked((uint)r.GetInt64(0)));}
            foreach(var uid in pending){GameState p;if(Kernel.GamePool.TryGetValue(uid,out p))Return(p,arena);else GuildPkAdmission.ReturnOffline(arena.Id,uid);}
            using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,"SELECT count(*) FROM revora_guild_pk_transfers WHERE run=@run AND state<>'returned'","run",arena.Id))if((long)q.ExecuteScalar()!=0)return false;}
            if(Kernel.GamePool.Values.Any(p=>p!=null && ReferenceEquals(p.Map,arena.Map)))return false;
            if(!GuildPkBanner.Retire(arena.Map))return false;
            arena.Map.Dispose();return true;
        }
        internal static void Tick() {
            if(!enabled || !Monitor.TryEnter(Gate))return;
            try {
                var now=Now;if(now<nextTick)return;nextTick=now.AddSeconds(2);
                if(now>=nextSchedule){GuildPkSchedule.Progress(now);nextSchedule=now.AddSeconds(30);}
                var opening=new List<Arena>();
                using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,@"SELECT id,first_guild,second_guild,starts_at,ends_at FROM revora_guild_pk_matches m
WHERE state='scheduled' AND @now>=starts_at-interval '5 minutes' AND @now<starts_at
AND NOT EXISTS(SELECT 1 FROM revora_guild_pk_maps f WHERE f.run=m.id) ORDER BY starts_at,id LIMIT 4","now",now))using(var r=q.ExecuteReader())
                    while(r.Read())opening.Add(new Arena{Id=r.GetGuid(0),First=checked((uint)r.GetInt64(1)),Second=checked((uint)r.GetInt64(2)),Start=r.GetFieldValue<DateTimeOffset>(3).UtcDateTime,End=r.GetFieldValue<DateTimeOffset>(4).UtcDateTime});}
                foreach(var arena in opening)if(!arenas.ContainsKey(arena.Id) && arenas.Count<4)Open(arena);
                foreach(var arena in arenas.Values.ToArray()) {
                    string state;
                    using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,"SELECT state FROM revora_guild_pk_matches WHERE id=@run","run",arena.Id))state=Convert.ToString(q.ExecuteScalar());}
                    if(now>=arena.Start && state!="complete" && state!="cancelled") {
                        if(state=="scheduled")using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,"UPDATE revora_guild_pk_matches SET state='active' WHERE id=@run AND state='scheduled'","run",arena.Id);}
                        GuildPkBanner.Pulse(arena.Map);
                        using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,"SELECT state FROM revora_guild_pk_matches WHERE id=@run","run",arena.Id))state=Convert.ToString(q.ExecuteScalar());}
                    }
                    // Failed settlement retains the arena for retry, even after its deadline.
                    if(state=="complete" || state=="cancelled")if(Close(arena))arenas.Remove(arena.Id);
                }
                RewardPulse(now);
            }catch(Exception ex){Console.WriteLine("[guild-pk] tick error="+ex.GetType().Name);}finally{Monitor.Exit(Gate);}
        }
        internal static bool HandleNpc(GameState p,NpcRequest request) {
            if(!enabled || p==null || request==null || (p.ActiveNpc!=Registrar && p.ActiveNpc!=Exit))return false;
            if(request.NpcID!=p.ActiveNpc || !Q.Player(p) || !Monitor.TryEnter(Gate))return true;
            try {
                bool exit=p.ActiveNpc==Exit;var arena=exit?arenas.Values.FirstOrDefault(a=>ReferenceEquals(a.Map,p.Map)):null;
                if(!Q.Near(p,p.ActiveNpc,exit?p.Entity.MapID:(ushort)1002) || exit && arena==null)return true;
                if(request.OptionID==255)return true;
                var dialog=new Npcs(p);
                if(exit) {
                    if(request.OptionID==1){if(Return(p,arena))return true;dialog.Text("Wait a moment, then ask again.");}
                    else {dialog.Text("Your guild still needs you. Are you leaving the field?");dialog.Option("Return to Twin City.",1);}
                }else {
                    var now=Now;
                    if(p.Guild!=null)arena=arenas.Values.FirstOrDefault(a=>(a.First==p.Guild.ID || a.Second==p.Guild.ID) && now>=a.Start.AddMinutes(-5) && now<a.Start);
                    if(request.OptionID==6)dialog.Text(FundPool(p));
                    else if(request.OptionID>=3 && request.OptionID<=5) {
                        pools.Remove(p);
                        if(arena!=null && GuildContestCoordinator.OfficerAt(p,Registrar)) {
                            long amount=request.OptionID==3?1000000:request.OptionID==4?5000000:10000000;
                            pools.Add(p,new PoolOffer{Key=Guid.NewGuid(),Run=arena.Id,Guild=p.Guild.ID,Amount=amount,Until=Now.AddMinutes(2)});
                            long pledged;
                            using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,"SELECT amount FROM revora_guild_pk_pools WHERE run=@run AND guild_id=@guild","run",arena.Id,"guild",(long)p.Guild.ID))pledged=Convert.ToInt64(q.ExecuteScalar() ?? 0L);}
                            dialog.Text("Your guild has pledged "+pledged.ToString("N0")+" silver. Add "+amount.ToString("N0")+" from its treasury? Members share it by their exploits. If no one earns a share, it returns to your treasury.");
                            dialog.Option("Confirm the pledge.",6);
                        }else dialog.Text("Your leader or deputy may pledge a reward while the gates are open.");
                    }
                    else if(request.OptionID==2)dialog.Text(ClaimPrize(p));
                    else if(request.OptionID==1) {
                        if(arena!=null && GuildPkAdmission.Enter(p,arena.Id,arena.Map))return true;
                        dialog.Text("The gates admit qualified guilds at their appointed hour. You must have served your guild for seven days.");
                    }else {
                        dialog.Text("Guilds battle Tuesday through Thursday at 20:30. Saturday's finals begin at 21:30. Gather five minutes early.");
                        if(arena!=null)dialog.Option("Join my guild's battle.",1);
                        if(arena!=null && GuildContestCoordinator.OfficerAt(p,Registrar)) {
                            dialog.Option("Pledge 1 million silver.",3);dialog.Option("Pledge 5 million silver.",4);dialog.Option("Pledge 10 million silver.",5);
                        }
                        if(OfferPrize(p))dialog.Option("Collect our tournament prize.",2);
                    }
                }
                dialog.Option("Farewell.",255);dialog.Send();return true;
            }finally{Monitor.Exit(Gate);}
        }
    }
}
