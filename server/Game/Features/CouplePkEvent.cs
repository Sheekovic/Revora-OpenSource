using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal sealed class CouplePkEvent
    {
        internal const uint Guide=2319,Awarder=23140;
        internal static CouplePkEvent Current;
        private static readonly TimeZoneInfo Zone=TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
        private readonly object gate=TournamentCombat.Gate;
        private readonly Func<DateTime> clock;
        private readonly Map arena;
        private readonly Action<Entity> death;
        private readonly Dictionary<uint,Pair> members=new Dictionary<uint,Pair>();
        private readonly Dictionary<uint,Invitation> invites=new Dictionary<uint,Invitation>();
        private readonly Dictionary<uint,Tuple<GameState,int>> codes=new Dictionary<uint,Tuple<GameState,int>>();
        private readonly HashSet<uint> transfers=new HashSet<uint>(),refresh=new HashSet<uint>();
        private readonly List<Run> runs=new List<Run>();
        private DateTime next;private int ticking;
        private sealed class Invitation {internal GameState Husband,Wife;internal DateTime Until;internal byte Code;}
        private sealed class Fighter {internal GameState Player;internal Enums.PKMode Mode;internal bool Defeated,Retreated;}
        private sealed class Pair {
            internal Guid Id;internal Run Run;internal Fighter Husband,Wife;internal bool Admitted,Competitive,Out;internal long Damage;internal DateTime Ready;
            internal Fighter[] Fighters {get{return new[]{Husband,Wife};}}
        }
        private sealed class Run {
            internal string Id;internal DateTime Start,Deadline,ResultTime;internal bool Finalizing,Done;internal Guid Winner;
            internal Dictionary<Guid,Pair> Pairs=new Dictionary<Guid,Pair>();
            internal CouplePkResults.Standing[] Standings;
        }
        internal CouplePkEvent(Func<ushort,Map> factory,Func<DateTime> utcClock) {
            clock=utcClock;death=Killed;CouplePkResults.Initialize();arena=factory(1090);
            var info=MapsTable.MapInformations[1090];info.ID=arena.ID;info.BaseID=1090;MapsTable.MapInformations[arena.ID]=info;Constants.PKFreeMaps.Add(arena.ID);
            var xy=arena.RandomCoordinates();arena.AddNpc(new NpcSpawn{UID=Awarder,MapID=arena.ID,X=xy.Item1,Y=xy.Item2,Mesh=2920,Type=Enums.NpcType.Talker,Name="CouplePKAwarder"});
        }
        internal static void Create(){Current=new CouplePkEvent(id=>Kernel.Maps[id].MakeDynamicMap(),()=>DateTime.UtcNow);}
        internal static void PlaceGuide() {
            foreach(var map in Kernel.Maps.Values.ToArray())foreach(uint id in new[]{Guide,Awarder}) {
                if(Current!=null && map.ID==Current.arena.ID)continue;
                Revora.Interfaces.INpc npc;if(map.Npcs!=null && map.Npcs.TryGetValue(id,out npc))map.RemoveNpc(npc);
            }
            ushort x=444,y=290;if(!PotencyGuide.FindLanding(Kernel.Maps[1002],ref x,ref y))throw new InvalidOperationException("Couple PK Guide landing unavailable.");
            Kernel.Maps[1002].AddNpc(new NpcSpawn{UID=Guide,MapID=1002,X=x,Y=y,Mesh=2910,Type=Enums.NpcType.Talker,Name="CouplePKGuide"});
        }
        internal static DateTime? Window(DateTime utc) {
            var local=TimeZoneInfo.ConvertTimeFromUtc(utc,Zone);if(local.DayOfWeek!=DayOfWeek.Friday)return null;
            return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local.Date.AddHours(21),DateTimeKind.Unspecified),Zone);
        }
        private static bool Team(GameState a,GameState b) {
            return a!=null && b!=null && a!=b && a.Team!=null && a.Team==b.Team && a.Team.Lider==a && a.Team.Members.Count==2
                && a.Team.Teammates.Contains(a) && a.Team.Teammates.Contains(b);
        }
        private static bool Married(GameState a,GameState b) {
            return a.Entity.Body/1000==1 && b.Entity.Body/1000==2 && a.Entity.Name!=b.Entity.Name
                && a.Entity.Spouse==b.Entity.Name && b.Entity.Spouse==a.Entity.Name && a.Entity.Spouse!="None" && b.Entity.Spouse!="None";
        }
        private static bool Free(GameState p) {
            return Q.Player(p) && p.Entity.OnDeath==null && !p.InQualifier() && !p.InTeamQualifier()
                && (p.ArenaStatistic==null || p.ArenaStatistic.Status==QualifierStatistic.NotSignedUp)
                && (p.TeamArenaStatistic==null || p.TeamArenaStatistic.Status==TeamQualifierStatistic.NotSignedUp)
                && p.ElitePKMatch==null && p.WatchingElitePKMatch==null && p.Booth==null && p.Trade!=null && !p.Trade.InTrade;
        }
        private bool Near(GameState p) {
            Revora.Interfaces.INpc npc;return Q.Player(p) && p.Entity.MapID==1002 && p.Map.Npcs.TryGetValue(Guide,out npc)
                && Kernel.GetDistance(p.Entity.X,p.Entity.Y,npc.X,npc.Y)<=6;
        }
        private static void WithPair(GameState a,GameState b,Action action) {
            if(a==null || b==null || a==b || a.Entity==null || b.Entity==null || a.ItemSyncRoot==null || b.ItemSyncRoot==null)return;
            var first=a.Entity.UID<b.Entity.UID?a:b;var last=first==a?b:a;
            lock(first.ItemSyncRoot)lock(last.ItemSyncRoot)lock(TournamentCombat.Gate)action();
        }
        private Run OpenRun() {DateTime now=clock();return runs.FirstOrDefault(r=>!r.Done && !r.Finalizing && now>=r.Start && now<r.Start.AddMinutes(9));}
        private bool Invite(GameState husband) {
            if(husband.Team==null)return false;var wife=husband.Team.Teammates.FirstOrDefault(p=>p!=husband);bool accepted=false;
            WithPair(husband,wife,()=> {
                if(!Near(husband) || !Near(wife) || !Free(husband) || !Free(wife) || !Team(husband,wife) || !Married(husband,wife)
                    || OpenRun()==null || members.ContainsKey(husband.Entity.UID) || members.ContainsKey(wife.Entity.UID))return;
                Invitation pending;if(invites.TryGetValue(wife.Entity.UID,out pending) && pending.Until>clock())return;
                Tuple<GameState,int> counter;int number=codes.TryGetValue(wife.Entity.UID,out counter) && counter.Item1==wife?counter.Item2:0;
                if(number>=125){wife.Send("Reconnect before asking the guide for another invitation.");return;}
                codes[wife.Entity.UID]=Tuple.Create(wife,number+1);
                invites[wife.Entity.UID]=new Invitation{Husband=husband,Wife=wife,Until=clock().AddSeconds(90),Code=(byte)(130+number)};
                husband.Send("Your names are ready. Let your spouse speak to me and confirm your place together.");
                wife.Send("Your spouse is ready for Couple PK. Speak to the Couple PK Guide and confirm within ninety seconds.");accepted=true;
            });return accepted;
        }
        private bool Confirm(GameState wife,byte code) {
            Invitation invite;lock(gate)if(!invites.TryGetValue(wife.Entity.UID,out invite))return false;bool admitted=false;
            WithPair(invite.Husband,wife,()=> {
                Invitation current;if(!invites.TryGetValue(wife.Entity.UID,out current) || current!=invite || invite.Wife!=wife || invite.Code!=code || invite.Until<=clock())return;
                invites.Remove(wife.Entity.UID);var husband=invite.Husband;var run=OpenRun();
                if(run==null || !Near(husband) || !Near(wife) || !Free(husband) || !Free(wife) || !Team(husband,wife) || !Married(husband,wife)
                    || members.ContainsKey(husband.Entity.UID) || members.ContainsKey(wife.Entity.UID))return;
                Guid id=CouplePkResults.Reservation(run.Id,husband.Entity.UID,wife.Entity.UID);if(!CouplePkResults.Reserve(run.Id,id,husband.Entity.UID,wife.Entity.UID,clock()))return;
                var pair=new Pair{Id=id,Run=run,Husband=new Fighter{Player=husband,Mode=husband.Entity.PKMode},Wife=new Fighter{Player=wife,Mode=wife.Entity.PKMode}};
                run.Pairs[id]=pair;members[husband.Entity.UID]=members[wife.Entity.UID]=pair;husband.Entity.OnDeath=wife.Entity.OnDeath=death;
                Admit(pair,clock());admitted=pair.Admitted;
            });return admitted;
        }
        private static void Mode(GameState p,Enums.PKMode mode){p.Entity.PKMode=mode;p.Send(new Data(true){UID=p.Entity.UID,ID=Data.ChangePKMode,dwParam=(uint)mode});}
        private void Travel(Fighter f,bool home) {
            var p=f.Player;transfers.Add(p.Entity.UID);
            try {
                var xy=home?Tuple.Create((ushort)408,(ushort)330):arena.RandomCoordinates();ushort map=home?(ushort)1002:arena.ID;
                p.Entity.Teleport(map,xy.Item1,xy.Item2);if(p.Entity.MapID!=map)throw new InvalidOperationException("Couple PK transfer incomplete.");
                p.Entity.BringToLife();p.Entity.Mana=p.Entity.MaxMana;
            } finally{transfers.Remove(p.Entity.UID);}
        }
        private bool Canonical(Pair pair) {return Team(pair.Husband.Player,pair.Wife.Player) && Married(pair.Husband.Player,pair.Wife.Player)
            && pair.Fighters.All(f=>Q.Player(f.Player,false) && f.Player.Entity.OnDeath==death);}
        private bool Present(Fighter f){return !f.Defeated && Q.Player(f.Player) && f.Player.Entity.MapID==arena.ID;}
        private bool Contending(Pair p){return !p.Out && p.Admitted && Canonical(p) && p.Fighters.Any(Present) && p.Fighters.All(f=>f.Defeated || Present(f));}
        private void Admit(Pair p,DateTime now) {
            if(p.Admitted || p.Out)return;
            if(!Canonical(p) || p.Fighters.Any(f=>f.Player.Entity.Dead) || now>=p.Run.Start.AddMinutes(9)){Home(p);return;}
            // Reserve both returns before moving either spouse. A partial reservation
            // is harmless and recoverable; no player travels until both are durable.
            foreach(var f in p.Fighters)CouplePkResults.PrepareReturn(p.Run.Id,f.Player,arena.ID,f.Mode);
            foreach(var f in p.Fighters)if(f.Player.Entity.MapID!=arena.ID)Travel(f,false);
            CouplePkResults.Admit(p.Run.Id,p.Id,now);p.Admitted=true;p.Ready=now.AddSeconds(10);
            foreach(var f in p.Fighters){Mode(f.Player,Enums.PKMode.PK);f.Player.Send("Stand together. Your battle begins in ten seconds.");}
        }
        private void Home(Pair p) {
            p.Out=true;foreach(var f in p.Fighters) {
                Pair current;if(!members.TryGetValue(f.Player.Entity.UID,out current) || current!=p)continue;
                if(Q.Player(f.Player,false)){Mode(f.Player,f.Mode);if(f.Player.Entity.MapID==arena.ID)Travel(f,true);CouplePkResults.CompleteReturn(p.Run.Id,f.Player);}
                if(f.Player.Entity.OnDeath==death)f.Player.Entity.OnDeath=null;members.Remove(f.Player.Entity.UID);
            }
        }
        private void Killed(Entity victim) {
            lock(gate) {Pair pair;if(victim==null || !members.TryGetValue(victim.UID,out pair) || pair.Out || !pair.Admitted)return;
                var fighter=pair.Fighters.FirstOrDefault(f=>f.Player.Entity==victim);if(fighter!=null && victim.MapID==arena.ID)fighter.Defeated=true;}
        }
        internal static bool InEvent(Entity e){var c=Current;return c!=null && e!=null && e.MapID==c.arena.ID;}
        internal static bool InSession(GameState p){var c=Current;if(c==null || p==null || p.Entity==null)return false;lock(c.gate){Pair pair;return c.members.TryGetValue(p.Entity.UID,out pair) && pair.Fighters.Any(f=>f.Player==p);}}
        internal static bool AllowTeleport(GameState p,ushort destination) {
            var c=Current;if(c==null || p==null || p.Entity==null)return true;
            lock(c.gate) {
                if(c.transfers.Contains(p.Entity.UID))return true;
                if(destination==c.arena.ID)return destination==p.Entity.MapID;
                Pair pair;if(destination!=p.Entity.MapID && c.members.TryGetValue(p.Entity.UID,out pair) && pair.Fighters.Any(f=>f.Player==p))pair.Out=true;
                return true;
            }
        }
        internal static void OnLogin(GameState p) {
            var c=Current;if(c==null || p==null || p.Entity==null || p.ItemSyncRoot==null)return;
            lock(p.ItemSyncRoot)lock(c.gate) {Pair pair;if(c.members.TryGetValue(p.Entity.UID,out pair)) {var f=pair.Fighters.FirstOrDefault(x=>x.Player.Entity.UID==p.Entity.UID);if(f!=null)Mode(p,f.Mode);c.Home(pair);}
                CouplePkResults.RecoverReturn(p);
                if(p.Entity.MapID==c.arena.ID){p.Entity.Teleport(1002,408,330);p.Entity.BringToLife();}}
        }
        private bool Fight(Entity a,Entity b) {
            Pair x,y;return a!=null && b!=null && a.EntityFlag==EntityFlag.Player && b.EntityFlag==EntityFlag.Player
                && members.TryGetValue(a.UID,out x) && members.TryGetValue(b.UID,out y) && x!=y && x.Run==y.Run && !x.Run.Finalizing && !x.Run.Done
                && clock()<x.Run.Deadline && clock()>=x.Ready && clock()>=y.Ready && x.Competitive && y.Competitive && Contending(x) && Contending(y)
                && x.Fighters.Any(f=>f.Player.Entity==a && f.Player==a.Owner && Present(f)) && y.Fighters.Any(f=>f.Player.Entity==b && f.Player==b.Owner && Present(f));
        }
        private sealed class Lease:IDisposable {private object owned;internal Lease(object value){owned=value;}public void Dispose(){var value=Interlocked.Exchange(ref owned,null);if(value!=null)Monitor.Exit(value);}}
        internal static IDisposable CombatLease(Entity a,Entity b) {
            var c=Current;if(c==null)return null;Monitor.Enter(c.gate);if(InEvent(a) || InEvent(b))return new Lease(c.gate);Monitor.Exit(c.gate);return null;
        }
        internal static bool BlockAttack(Entity a,Entity b){var c=Current;if(c==null)return false;lock(c.gate)return (InEvent(a) || InEvent(b)) && !c.Fight(a,b);}
        internal static bool BlockSpell(GameState p) {
            var c=Current;if(c==null || p==null || !InEvent(p.Entity))return false;lock(c.gate){Pair pair;
                return !c.members.TryGetValue(p.Entity.UID,out pair) || !c.Contending(pair) || !pair.Competitive || pair.Run.Finalizing || pair.Run.Done
                    || c.clock()<pair.Ready || c.clock()>=pair.Run.Deadline || !pair.Fighters.Any(f=>f.Player==p && c.Present(f));}
        }
        internal static void Damage(Entity a,Entity b,uint amount){var c=Current;if(c==null)return;lock(c.gate)if(c.Fight(a,b)){var p=c.members[a.UID];long actual=Math.Min(amount,b.Hitpoints);p.Damage=Math.Min(long.MaxValue-actual,p.Damage)+actual;}}
        private void Pulse(Run r,DateTime now) {
            if(r.Finalizing){Finish(r);return;}
            foreach(var p in r.Pairs.Values.Where(p=>!p.Out && !p.Admitted).ToArray())Admit(p,now);
            foreach(var p in r.Pairs.Values.Where(p=>p.Admitted && !p.Out).ToArray()) {
                if(!Contending(p)){p.Out=true;continue;}
                foreach(var f in p.Fighters.Where(f=>f.Defeated && !f.Retreated)) {
                    Mode(f.Player,f.Mode);Travel(f,true);CouplePkResults.CompleteReturn(p.Run.Id,f.Player);f.Retreated=true;f.Player.Send("Your spouse still fights. Stay online and keep your team to share the result.");
                }
            }
            foreach(var p in r.Pairs.Values.Where(p=>p.Out).ToArray())Home(p);
            var alive=r.Pairs.Values.Where(Contending).ToArray();
            if(now>=r.Deadline || now>=r.Start.AddMinutes(10) && alive.Length<=1) {
                r.Winner=now<r.Deadline && alive.Length==1 && alive[0].Competitive && alive[0].Damage>0?alive[0].Id:Guid.Empty;
                r.Standings=r.Pairs.Values.Select(p=>new CouplePkResults.Standing{Pair=p.Id,Alive=!p.Out && p.Admitted,Damage=p.Damage}).ToArray();
                r.ResultTime=now;r.Finalizing=true;Finish(r);return;
            }
            if(alive.Length>=2 && alive.Any(p=>!p.Competitive)) {
                CouplePkResults.Competitive(r.Id,alive.Select(p=>p.Id));foreach(var p in alive){p.Competitive=true;foreach(var f in p.Fighters)refresh.Add(f.Player.Entity.UID);}
            }
        }
        private void Finish(Run r) {
            bool earned=CouplePkResults.Finish(r.Id,r.Winner,r.Standings,r.ResultTime);
            foreach(var p in r.Pairs.Values) {if(earned && p.Id==r.Winner)foreach(var f in p.Fighters)refresh.Add(f.Player.Entity.UID);Home(p);}
            r.Done=true;if(earned)Broadcast("Couple PK has crowned its champions. The Couple PK Guide holds both victors' purses.");
        }
        private static void Broadcast(string message){foreach(var p in Kernel.GamePool.Values.ToArray())if(Q.Player(p,false))p.Send(message);}
        internal void Tick() {
            if(Interlocked.CompareExchange(ref ticking,1,0)!=0)return;
            try {
                lock(gate) {
                    DateTime now=clock();if(now<next)return;next=now.AddSeconds(1);DateTime? start=Window(now);
                    if(start.HasValue && now>=start.Value && now<start.Value.AddMinutes(9)) {
                        string id="cpk-"+TimeZoneInfo.ConvertTimeFromUtc(now,Zone).ToString("yyyyMMdd",CultureInfo.InvariantCulture);
                        if(!runs.Any(r=>r.Id==id) && CouplePkResults.Open(id,start.Value)) {runs.RemoveAll(r=>r.Done);runs.Add(new Run{Id=id,Start=start.Value,Deadline=start.Value.AddHours(1)});Broadcast("Couple PK is open. Married pairs can meet the guide in Twin City before 21:09 Pacific.");}
                    }
                    foreach(uint uid in invites.Where(i=>i.Value.Until<=now || !Q.Player(i.Value.Husband) || !Q.Player(i.Value.Wife)).Select(i=>i.Key).ToArray())invites.Remove(uid);
                    foreach(uint uid in codes.Where(i=>!Q.Player(i.Value.Item1,false)).Select(i=>i.Key).ToArray())codes.Remove(uid);
                    foreach(var r in runs.Where(r=>!r.Done).ToArray())Pulse(r,now);
                }
                uint[] pending;lock(gate)pending=refresh.ToArray();
                foreach(uint uid in pending){GameState p;if(Kernel.GamePool.TryGetValue(uid,out p) && Q.Player(p,false))lock(p.ItemSyncRoot)Q.Refresh(p);lock(gate)refresh.Remove(uid);}
            }catch(Exception ex){Console.WriteLine("[couple-pk] tick-failed error="+ex.GetType().Name);}finally{Volatile.Write(ref ticking,0);}
        }
        internal static bool HandleNpc(GameState p,NpcRequest request) {
            if(p==null || p.Entity==null || request==null || (p.ActiveNpc!=Guide && p.ActiveNpc!=Awarder))return false;
            var c=Current;if(c==null || p.ItemSyncRoot==null)return true;
            if(!Q.Near(p,p.ActiveNpc,p.ActiveNpc==Guide?(ushort)1002:c.arena.ID))return true;
            try {
                if(p.ActiveNpc==Guide && request.OptionID==1){if(!c.Invite(p))p.Send("Bring your spouse in a team of two, and stand beside me while entry is open.");return true;}
                if(p.ActiveNpc==Guide && request.OptionID>=130 && request.OptionID<255){if(!c.Confirm(p,request.OptionID))p.Send("That invitation is no longer ready. Ask your spouse to speak to me again.");return true;}
                lock(p.ItemSyncRoot) {
                    if(!Q.Near(p,p.ActiveNpc,p.ActiveNpc==Guide?(ushort)1002:c.arena.ID))return true;
                    if(p.ActiveNpc==Guide && request.OptionID==3){p.Send(CouplePkResults.Claim(p,c.clock()));Q.Refresh(p);return true;}
                    lock(c.gate) {
                        if(request.OptionID==4){Pair pair;if(c.members.TryGetValue(p.Entity.UID,out pair) && pair.Fighters.Any(f=>f.Player==p))c.Home(pair);return true;}
                        var d=new Npcs(p);Invitation invite;
                        d.Text(p.ActiveNpc==Guide?"Couple PK opens Friday at 21:00 Pacific. Bring your spouse before 21:09. Win together: each spouse earns a purse and a bound P7 soul in new tournaments.":"Your bond is your strength. Leave now and your pair's place is lost.");
                        if(p.ActiveNpc==Guide){d.Option("Enter with my spouse.",1);if(c.invites.TryGetValue(p.Entity.UID,out invite) && invite.Wife==p && invite.Until>c.clock())d.Option("I will stand with my spouse.",invite.Code);d.Option("Claim my champion's purse.",3);}
                        if(InSession(p))d.Option("Withdraw our pair.",4);d.Option("Farewell.",255);d.Send();
                    }
                }
            }catch(Exception ex){Q.Fail(p,"couple-pk",ex);}return true;
        }
    }
}
