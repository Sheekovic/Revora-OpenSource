using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;

namespace Revora.Game.Features
{
    internal static class ArenaBots
    {
        private sealed class Actor
        {
            internal GameState Player, Opponent;
            internal ArenaBotBrain Brain;
            internal IDisposable Buffers;
            internal Qualifier.QualifierList.QualifierGroup LastMatch;
        }
        private static readonly object gate=new object();
        private static readonly Dictionary<uint,Actor> actors=new Dictionary<uint,Actor>();
        private static readonly Dictionary<GameState,DateTime> waiting=new Dictionary<GameState,DateTime>();
        private static DateTime retryAfter;
        private static int rosterCursor;
        private static readonly Dictionary<uint,List<byte>> recentClasses=new Dictionary<uint,List<byte>>();
        internal static bool Enabled {get;private set;}
        internal static bool TryDie(Entity body,uint killer)
        {
            if(body==null || !ArenaActors.IsBot(body.Owner))return false;
            var p=body.Owner;var group=p.QualifierGroup;
            if(group==null || !group.OwnsImportedMap)return true;
            body.Hitpoints=0;
            body.AddFlag((ulong)FlagsUpdate.Flags.Dead,StatusFlagsBigVector32.PermanentFlag,false);
            p.SendScreen(new Attack(true){Attacker=killer,Attacked=body.UID,AttackType=Attack.Kill,X=body.X,Y=body.Y},true);
            ArenaResultCoordinator.EndForPlayer(group,p,ArenaResultIntents.Cause.Defeat);
            return true;
        }
        internal static void Initialize()
        {
            // Explicit operator escape hatch; normal production restarts retain matchmaking.
            Enabled=!string.Equals(Environment.GetEnvironmentVariable("REVORA_ARENA_BOTS"),"0",StringComparison.Ordinal);
            Console.WriteLine("[arena-bot] enabled="+Enabled+" human-threshold=10 capacity=9");
            rosterCursor=Kernel.Random.Next(ArenaBotStore.Capacity);
            // Roster schema/cache is prepared by ArenaRankingSettlement before ranking loads.
        }
        private static string CombatSummary(Entity e)
        { return e.UID+"/class="+e.Class+"/hp="+e.MaxHitpoints+"/bp="+e.BattlePower+"/atk="+e.MaxAttack
            +"/magic="+e.MagicAttack+"/def="+e.Defence+"/mdef="+e.MagicDefence+"/crit="+e.CriticalStrike
            +"/immunity="+e.Immunity+"/break="+e.Breaktrough+"/counter="+e.Counteraction; }
        internal static int HumanSignups()
        {
            return Qualifier.PlayerList.Values.Concat(Qualifier.WaitingPlayerList.Values)
                .Concat(Qualifier.QualifierList.Groups.Values.SelectMany(g=>new[]{g.Player1,g.Player2}))
                .Where(p=>QuestInventory.Player(p,false)).Select(p=>p.Entity.UID).Distinct().Count();
        }
        // Called after the human matcher; at most one new opponent per scheduling pulse.
        internal static void Fill()
        {
            if(!Enabled || !ArenaStartupPreparation.Prepared || ArenaShutdownDrain.Stopping || !Monitor.TryEnter(gate))return;
            string stage="queue";
            try
            {
                var now=DateTime.UtcNow;
                var candidates=Qualifier.PlayerList.Values.Where(p=>QuestInventory.Player(p) && p.ArenaState==Qualifier.FindMatch && Qualifier.CanJoin(p,Time32.Now)).ToArray();
                foreach(var stale in waiting.Keys.Where(p=>!candidates.Contains(p)).ToArray())waiting.Remove(stale);
                foreach(var p in candidates)if(!waiting.ContainsKey(p))waiting.Add(p,now);
                int count=HumanSignups();
                if(count>=ArenaBotPolicy.HumanSignupThreshold || now<retryAfter || actors.Count>=ArenaBotStore.Capacity)return;
                var human=candidates.OrderBy(p=>waiting[p]).FirstOrDefault(p=>ArenaBotPolicy.ShouldFill(count,(int)Math.Min(int.MaxValue,(now-waiting[p]).TotalMilliseconds)));
                if(human==null || !Monitor.TryEnter(human.ItemSyncRoot))return;
                try
                {
                    GameState queued;
                    if(!QuestInventory.Player(human) || human.QualifierGroup!=null || human.ArenaStatistic.PlayWith!=null
                        || !Qualifier.PlayerList.TryGetValue(human.Entity.UID,out queued) || !ReferenceEquals(queued,human) || HumanSignups()>=10)return;
                    uint uid=ArenaBotPolicy.SelectRoster(rosterCursor,id=>!actors.ContainsKey(id) && !Kernel.GamePool.ContainsKey(id));
                    if(uid==0)return;
                    List<byte> recent;recentClasses.TryGetValue(human.Entity.UID,out recent);
                    byte profession=ArenaBotSnapshot.ChooseFreshClass(human.Entity.Class,Kernel.Random.Next(),recent??new List<byte>());
                    stage="store";ArenaBotStore.Prepare(uid,human,profession);
                    if(!Kernel.Maps.ContainsKey(700))new Map(700,DMaps.MapPaths[700]);
                    GameState bot=null;
                    try
                    {
                        stage="snapshot";bot=ArenaBotSnapshot.Create(uid,human,profession);
                        if(HumanSignups()>=10 || !QuestInventory.Player(human) || human.ArenaState!=Qualifier.FindMatch
                            || human.ArenaStatistic.PlayWith!=null || !Qualifier.PlayerList.TryGetValue(human.Entity.UID,out queued) || !ReferenceEquals(queued,human))
                        { DisposeUnowned(bot);return; }
                        stage="invitation";if(!ArenaActors.Install(bot))throw new InvalidOperationException("Arena bot session collision.");
                        var actor=new Actor{Player=bot,Opponent=human,Brain=new ArenaBotBrain(bot)};
                        actors.Add(uid,actor);
                        Qualifier.ArenaStatistics[uid]=bot.ArenaStatistic;
                        TeamQualifier.ArenaStatistics[uid]=bot.TeamArenaStatistic;
                        bot.ArenaState=Qualifier.WaitForOther;bot.ArenaStatistic.AcceptBox=true;
                        human.ArenaState=Qualifier.WaitForBox;human.ArenaStatistic.AcceptBox=false;
                        bot.ArenaStatistic.PlayWith=human;human.ArenaStatistic.PlayWith=bot;
                        bot.ArenaStatistic.AcceptBoxShow=human.ArenaStatistic.AcceptBoxShow=Time32.Now;
                        bot.ArenaStatistic.Status=human.ArenaStatistic.Status=QualifierStatistic.WaitingInactive;
                        Qualifier.PlayerList.Remove(human.Entity.UID);
                        Qualifier.WaitingPlayerList.Add(human.Entity.UID,human);Qualifier.WaitingPlayerList.Add(uid,bot);
                        human.Send(human.ArenaStatistic);
                        human.Send(new Qualifier.ArenaSignup{DialogID=Qualifier.ArenaSignup.MainIDs.StartCountDown,Stats=human.ArenaStatistic}.BuildPacket());
                        human.Send("Arena opponent: "+bot.Entity.Name+". This match counts toward normal Arena progress.");
                        rosterCursor=(int)((uid-ArenaBotStore.FirstUid+1)%ArenaBotStore.Capacity);
                        if(recent==null) {
                            if(recentClasses.Count>=256)recentClasses.Remove(recentClasses.Keys.First());
                            recentClasses[human.Entity.UID]=recent=new List<byte>();
                        }
                        recent.Add(profession);if(recent.Count>8)recent.RemoveAt(0);
                        waiting.Remove(human);
                        Console.WriteLine("[arena-bot] stats bot="+CombatSummary(bot.Entity)+" player="+CombatSummary(human.Entity));
                        Console.WriteLine("[arena-bot] offered uid="+uid+" opponent="+human.Entity.UID+" class="+bot.Entity.Class);
                    }
                    catch
                    {
                        if(bot!=null && !actors.ContainsKey(uid))DisposeUnowned(bot);
                        throw;
                    }
                }
                finally{Monitor.Exit(human.ItemSyncRoot);}
            }
            catch(Exception ex)
            {
                retryAfter=DateTime.UtcNow.AddSeconds(30);
                Console.WriteLine("[arena-bot] matchmaking deferred stage="+stage+" type="+ex.GetType().Name+" at="+(ex.TargetSite==null?"unknown":ex.TargetSite.Name));
            }
            finally{Monitor.Exit(gate);}
        }
        internal static void Tick()
        {
            if(!Enabled || !Monitor.TryEnter(gate))return;
            try
            {
                foreach(var actor in actors.Values.ToArray())
                {
                    var p=actor.Player;
                    try
                    {
                        if(p.QualifierGroup!=null)
                        {
                            actor.LastMatch=p.QualifierGroup;
                            if(actor.Buffers==null && p.QualifierGroup.ParticipationRecorded)
                            {
                                actor.Buffers=Server.Thread.Buffers.Add(p);
                                Console.WriteLine("[arena-bot] fighting uid="+p.Entity.UID+" opponent="+actor.Opponent.Entity.UID+" class="+p.Entity.Class);
                            }
                            actor.Brain.Tick(actor.Opponent);
                        }
                        else if(p.ArenaShutdownRetention==null && p.ArenaStatistic.PlayWith==null
                            && !Qualifier.WaitingPlayerList.ContainsKey(p.Entity.UID))
                        {
                            if(actor.Buffers!=null)actor.Buffers.Dispose();
                            DisposeUnowned(p);actors.Remove(p.Entity.UID);
                            var match=actor.LastMatch;
                            Console.WriteLine("[arena-bot] retired uid="+p.Entity.UID+(match==null?" invitation-only":
                                " bot-damage="+(ReferenceEquals(match.Player1,p)?match.Player1Damage:match.Player2Damage)
                                +" player-damage="+(ReferenceEquals(match.Player1,p)?match.Player2Damage:match.Player1Damage)
                                +" spells="+actor.Brain.SpellAttempts+" rejected="+actor.Brain.RejectedSpells+" basic="+actor.Brain.BasicAttempts+" moves="+actor.Brain.Movements));
                        }
                    }
                    catch(Exception ex)
                    {
                        // Retain ownership and let the existing match deadline settle the result.
                        actor.Brain.Stop();
                        Console.WriteLine("[arena-bot] combat suspended uid="+p.Entity.UID+" type="+ex.GetType().Name);
                    }
                }
            }
            finally{Monitor.Exit(gate);}
        }
        private static void DisposeUnowned(GameState p)
        {
            if(ArenaActors.Registered(p))ArenaActors.Remove(p);
            if(p.Screen!=null)p.Screen.DisposeTimers();
            if(p.Pet!=null)p.Pet.DisposeTimers();
            if(p.Entity!=null && p.Map!=null)p.Map.RemoveEntity(p.Entity);
        }
    }
}
