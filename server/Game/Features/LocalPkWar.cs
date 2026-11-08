using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
using Rules=Revora.Game.Features.LocalPkRules;

namespace Revora.Game.Features
{
    internal sealed class LocalPkWar
    {
        internal const uint General=2314, Clerk=2315, RetiredClerk=2317, ClassEnvoy=705, ClassClerk=706;
        internal static LocalPkWar Current;
        private static readonly TimeZoneInfo Zone=TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
        private readonly object gate=TournamentCombat.Gate;
        private readonly Func<DateTime> clock;
        private readonly Func<ushort,Map> factory;
        private readonly Dictionary<int,Map> maps=new Dictionary<int,Map>();
        private readonly List<Run> runs=new List<Run>();
        private readonly Dictionary<uint,Member> members=new Dictionary<uint,Member>();
        private readonly HashSet<uint> transfers=new HashSet<uint>(),refresh=new HashSet<uint>();
        private readonly Action<Entity> death;
        private int ticking;
        private DateTime next;
        private sealed class Member {
            internal GameState Player;internal Run Run;internal Enums.PKMode Mode;internal int Level;
            internal bool Admitted,Out,Competitive;internal long Damage;internal DateTime Ready;
        }
        private sealed class Run {
            internal string Key;internal int Kind,Bracket,Stage,Target;
            internal DateTime Start,Deadline,ResultTime;internal uint Winner;
            internal bool Moving,Finalizing,Done;internal List<Member> Entries=new List<Member>();
        }
        internal LocalPkWar(Func<ushort,Map> mapFactory,Func<DateTime> utcClock) {
            factory=mapFactory;clock=utcClock;death=Killed;LocalPkResults.Initialize();
        }
        internal static void Create() {Current=new LocalPkWar(id=>Kernel.Maps[id].MakeDynamicMap(),()=>DateTime.UtcNow);}
        internal static DateTime Start(DateTime utc) {
            var local=TimeZoneInfo.ConvertTimeFromUtc(utc,Zone);
            return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local.Date.AddHours(20),DateTimeKind.Unspecified),Zone);
        }
        internal static bool Scheduled(DateTime utc,int kind) {
            var local=TimeZoneInfo.ConvertTimeFromUtc(utc,Zone);
            return Rules.BpWar(kind)?local.DayOfWeek==DayOfWeek.Sunday && local.Day<=28:kind==0?local.DayOfWeek==DayOfWeek.Saturday:kind==1?local.Day==1:Rules.ClassWar(kind) && local.DayOfWeek==DayOfWeek.Monday;
        }
        internal static DateTime ScheduleStart(DateTime utc,int kind) {return Rules.ClassWar(kind)?Start(utc).AddMinutes(-45):Start(utc);}
        private static bool Division(Member m) {return Rules.BpWar(m.Run.Kind)?BattlePowerTournament.Eligible(m.Run.Kind,m.Player.Entity.BattlePower,m.Admitted):Bracket(m.Player.Entity.Level)==m.Run.Bracket && (!Rules.ClassWar(m.Run.Kind) || Rules.ClassKind(m.Player.Entity.Class)==m.Run.Kind);}
        internal static int Bracket(int level) {return ElitePkEvent.Bracket(level);}
        internal static void PlaceGeneral() {
            foreach(var map in Kernel.Maps.Values.ToArray())foreach(uint id in new[]{General,Clerk,RetiredClerk,70500u}.Concat(Enumerable.Range(705,41).Select(i=>(uint)i))) {
                Revora.Interfaces.INpc npc;if(map.Npcs!=null && map.Npcs.TryGetValue(id,out npc))map.RemoveNpc(npc);
            }
            ushort x=446,y=246;var town=Kernel.Maps[1002];
            if(!PotencyGuide.FindLanding(town,ref x,ref y))throw new InvalidOperationException("PK General landing unavailable.");
            town.AddNpc(new NpcSpawn{UID=General,MapID=1002,X=x,Y=y,Mesh=310,Type=Enums.NpcType.Talker,Name="GeneralBravery"});
            x=452;y=291;if(!PotencyGuide.FindLanding(town,ref x,ref y))throw new InvalidOperationException("Class PK Envoy landing unavailable.");
            town.AddNpc(new NpcSpawn{UID=ClassEnvoy,MapID=1002,X=x,Y=y,Mesh=1360,Type=Enums.NpcType.Talker,Name="ClassPKEnvoy"});
            BattlePowerTournament.PlaceManager();
        }
        private Map Arena(Run run,int stage) {
            int key=(run.Kind*4+run.Bracket)*3+stage;Map map;if(maps.TryGetValue(key,out map))return map;
            ushort source=Rules.ClassWar(run.Kind) || Rules.BpWar(run.Kind)?(ushort)1730:new ushort[]{1505,1508,700}[stage];map=factory(source);
            var info=MapsTable.MapInformations[source];info.ID=map.ID;info.BaseID=source;MapsTable.MapInformations[map.ID]=info;
            Constants.PKFreeMaps.Add(map.ID);var xy=map.RandomCoordinates();
            map.AddNpc(new NpcSpawn{UID=Rules.ClassWar(run.Kind)?ClassClerk:Clerk,MapID=map.ID,X=xy.Item1,Y=xy.Item2,Mesh=(ushort)(Rules.ClassWar(run.Kind)?1370:310),Type=Enums.NpcType.Talker,Name="PKClerk"});
            maps.Add(key,map);return map;
        }
        private bool Owns(ushort id) {return maps.Values.Any(m=>m.ID==id);}
        internal static bool InEvent(Entity e) {var c=Current;if(c==null || e==null)return false;lock(c.gate)return c.Owns(e.MapID);}
        private bool Present(Member m) {
            return !m.Out && m.Admitted && Q.Player(m.Player) && Division(m)
                && m.Player.Entity.OnDeath==death && m.Player.Entity.MapID==Arena(m.Run,m.Run.Stage).ID;
        }
        private static void Mode(GameState p,Enums.PKMode mode) {
            p.Entity.PKMode=mode;p.Send(new Data(true){UID=p.Entity.UID,ID=Data.ChangePKMode,dwParam=(uint)mode});
        }
        private void Travel(Member m,Map map,bool heal) {
            var p=m.Player;transfers.Add(p.Entity.UID);
            try {
                if(map!=null && !Rules.BpWar(m.Run.Kind))LocalPkResults.PrepareReturn(m.Run.Key,p,map.ID,m.Mode);
                var xy=map==null?Tuple.Create((ushort)408,(ushort)330):map.RandomCoordinates();
                p.Entity.Teleport(map==null?(ushort)1002:map.ID,xy.Item1,xy.Item2);
                if(p.Entity.MapID!=(map==null?1002:map.ID))throw new InvalidOperationException("PK transfer incomplete.");
                if(heal){p.Entity.BringToLife();p.Entity.Mana=p.Entity.MaxMana;}
            } finally {transfers.Remove(p.Entity.UID);}
        }
        private void Home(Member m) {
            var p=m.Player;m.Out=true;
            Member current;if(!members.TryGetValue(p.Entity.UID,out current) || current!=m)return;
            if(Q.Player(p,false)) {Mode(p,m.Mode);if(Owns(p.Entity.MapID))Travel(m,null,true);if(!Rules.BpWar(m.Run.Kind))LocalPkResults.CompleteReturn(m.Run.Key,p);}
            if(p.Entity.OnDeath==death)p.Entity.OnDeath=null;
            members.Remove(p.Entity.UID);
        }
        internal static bool AllowTeleport(GameState p,ushort destination) {
            var c=Current;if(c==null || p==null || p.Entity==null)return true;
            lock(c.gate) {
                if(c.transfers.Contains(p.Entity.UID))return true;
                if(c.Owns(destination))return destination==p.Entity.MapID;
                Member m;if(c.members.TryGetValue(p.Entity.UID,out m) && m.Player==p) {
                    m.Out=true;Mode(p,m.Mode);if(p.Entity.OnDeath==c.death)p.Entity.OnDeath=null;
                }
                return true;
            }
        }
        internal static void OnLogin(GameState p) {
            var c=Current;if(c==null || p==null || p.Entity==null || p.ItemSyncRoot==null)return;
            lock(p.ItemSyncRoot)lock(c.gate) {
                BattlePowerTournament.Recover(p);
                Member old;if(c.members.TryGetValue(p.Entity.UID,out old)) {
                    old.Out=true;if(old.Player.Entity.OnDeath==c.death)old.Player.Entity.OnDeath=null;
                    Mode(p,old.Mode);c.members.Remove(p.Entity.UID);
                }
                LocalPkResults.RecoverReturn(p);
                if(c.Owns(p.Entity.MapID)) {
                    p.Entity.Teleport(1002,408,330);p.Entity.BringToLife();
                    p.Send("Your former place in the PK War is forfeit. General Bravery keeps any earned prize.");
                }
            }
        }
        private void Killed(Entity victim) {
            lock(gate) {Member m;if(victim!=null && members.TryGetValue(victim.UID,out m) && m.Player.Entity==victim && m.Admitted)m.Out=true;}
        }
        private bool Fight(Entity a,Entity b) {
            Member x,y;
            return a!=null && b!=null && a.EntityFlag==EntityFlag.Player && b.EntityFlag==EntityFlag.Player && a!=b
                && members.TryGetValue(a.UID,out x) && members.TryGetValue(b.UID,out y) && x.Run==y.Run
                && x.Player.Entity==a && y.Player.Entity==b && a.Owner==x.Player && b.Owner==y.Player
                && !x.Run.Finalizing && !x.Run.Done && !x.Run.Moving && clock()<x.Run.Deadline
                && clock()>=x.Ready && clock()>=y.Ready && x.Competitive && y.Competitive && Present(x) && Present(y);
        }
        private sealed class Lease:IDisposable {
            private object owned;internal Lease(object value){owned=value;}
            public void Dispose(){var value=Interlocked.Exchange(ref owned,null);if(value!=null)Monitor.Exit(value);}
        }
        internal static IDisposable CombatLease(Entity a,Entity b) {
            var c=Current;if(c==null)return null;Monitor.Enter(c.gate);
            if(a!=null && c.Owns(a.MapID) || b!=null && c.Owns(b.MapID))return new Lease(c.gate);
            Monitor.Exit(c.gate);return null;
        }
        internal static bool BlockAttack(Entity a,Entity b) {
            var c=Current;if(c==null)return false;lock(c.gate)return (a!=null && c.Owns(a.MapID) || b!=null && c.Owns(b.MapID)) && !c.Fight(a,b);
        }
        internal static bool BlockSpell(GameState p) {
            var c=Current;if(c==null || p==null || p.Entity==null)return false;
            lock(c.gate) {if(!c.Owns(p.Entity.MapID))return false;Member m;
                return !c.members.TryGetValue(p.Entity.UID,out m) || m.Player!=p || !c.Present(m) || !m.Competitive
                    || m.Run.Moving || m.Run.Finalizing || m.Run.Done || c.clock()<m.Ready || c.clock()>=m.Run.Deadline;}
        }
        internal static void Damage(Entity a,Entity b,uint amount) {
            var c=Current;if(c==null)return;lock(c.gate)if(c.Fight(a,b)) {
                var m=c.members[a.UID];m.Damage=Math.Min(long.MaxValue-(long)Math.Min(amount,b.Hitpoints),m.Damage)+Math.Min(amount,b.Hitpoints);
            }
        }
        internal bool Enroll(GameState p,int kind) {
            if(p==null || p.ItemSyncRoot==null)return false;
            lock(p.ItemSyncRoot)lock(gate) {
                if(!Q.Near(p,Rules.BpWar(kind)?BattlePowerTournament.Manager:Rules.ClassWar(kind)?ClassEnvoy:General,1002) || p.Entity.OnDeath!=null || p.InQualifier() || p.InTeamQualifier()
                    || p.ArenaStatistic!=null && p.ArenaStatistic.Status!=QualifierStatistic.NotSignedUp
                    || p.TeamArenaStatistic!=null && p.TeamArenaStatistic.Status!=TeamQualifierStatistic.NotSignedUp
                    || p.ElitePKMatch!=null || p.WatchingElitePKMatch!=null || members.ContainsKey(p.Entity.UID))return false;
                DateTime now=clock();var r=runs.FirstOrDefault(x=>x.Kind==kind && x.Bracket==(Rules.BpWar(kind)?0:Bracket(p.Entity.Level)) && !x.Done && !x.Finalizing
                    && now>=x.Start && now<x.Start.AddMinutes(Rules.JoinMinutes(x.Kind)));
                if(r==null)return false;
                if(Rules.BpWar(kind)) {if(!BattlePowerTournament.Register(r.Key,p,now))return false;}
                else if(!LocalPkResults.Register(r.Key,p.Entity.UID,p.Entity.Level,now,p.Entity.Class))return false;
                var m=new Member{Player=p,Run=r,Mode=p.Entity.PKMode,Level=p.Entity.Level};r.Entries.Add(m);members.Add(p.Entity.UID,m);p.Entity.OnDeath=death;
                Admit(m,now);return true;
            }
        }
        private void Admit(Member m,DateTime now) {
            if(m.Out || m.Admitted)return;
            if(!Q.Player(m.Player) || now>=m.Run.Start.AddMinutes(Rules.JoinMinutes(m.Run.Kind)) || !Division(m)){Home(m);return;}
            var arena=Arena(m.Run,0);
            if(Rules.BpWar(m.Run.Kind))BattlePowerTournament.PrepareArena(m.Run.Key,m.Player.Entity.UID,arena.ID);
            Travel(m,arena,true);
            if(Rules.BpWar(m.Run.Kind))BattlePowerTournament.Admit(m.Run.Key,m.Player.Entity.UID,m.Player.Entity.MapID,now);
            else LocalPkResults.Admit(m.Run.Key,m.Player.Entity.UID,now);
            m.Admitted=true;m.Ready=now.AddSeconds(10);
            if(Rules.ClassWar(m.Run.Kind) && m.Ready<m.Run.Start.AddMinutes(15))m.Ready=m.Run.Start.AddMinutes(15);Mode(m.Player,Enums.PKMode.PK);
            if(Rules.BpWar(m.Run.Kind) && m.Ready<m.Run.Start.AddMinutes(10))m.Ready=m.Run.Start.AddMinutes(10);
            m.Player.Send(Rules.BpWar(m.Run.Kind)?"The BP trial begins at 20:10 Pacific. Remain within your division's power limit; the last worthy survivor wins.":Rules.ClassWar(m.Run.Kind)?"Your class battles at 19:30 Pacific. Prepare yourself; the last worthy fighter will be champion.":"Your battle begins in ten seconds. Hold your ground until one champion remains.");
        }
        private void MarkField(Run r) {
            var present=r.Entries.Where(Present).ToArray();if(present.Length<2 || present.All(m=>m.Competitive))return;
            LocalPkResults.Competitive(r.Key,r.Kind,present.Select(m=>m.Player.Entity.UID));
            foreach(var m in present){m.Competitive=true;refresh.Add(m.Player.Entity.UID);}
        }
        private void Pulse(Run r,DateTime now) {
            if(r.Finalizing){Finish(r);return;}
            foreach(var m in r.Entries.Where(m=>!m.Out && !m.Admitted).ToArray())Admit(m,now);
            foreach(var m in r.Entries.Where(m=>!m.Out && m.Admitted).ToArray()) {
                bool moving=r.Moving && Q.Player(m.Player) && Division(m)
                    && m.Player.Entity.OnDeath==death && m.Player.Entity.MapID==Arena(r,r.Target).ID;
                if(!Present(m) && !moving)m.Out=true;
            }
            foreach(var m in r.Entries.Where(m=>m.Out && members.ContainsKey(m.Player.Entity.UID)).ToArray())Home(m);
            var alive=r.Entries.Where(m=>!m.Out && m.Admitted).ToArray();
            if(now>=r.Deadline || now>=r.Start.AddMinutes(Rules.WinMinutes(r.Kind)) && alive.Length<=1) {
                r.Winner=now<r.Deadline && alive.Length==1 && alive[0].Competitive && alive[0].Damage>0?alive[0].Player.Entity.UID:0;
                r.ResultTime=now;r.Finalizing=true;Finish(r);return;
            }
            int stage=Rules.ClassWar(r.Kind) || Rules.BpWar(r.Kind)?0:now>=r.Start.AddMinutes(40)?2:now>=r.Start.AddMinutes(28)?1:0;
            if(stage>r.Stage || r.Moving) {
                r.Moving=true;r.Target=stage;Map target=Arena(r,stage);
                foreach(var m in alive)if(m.Player.Entity.MapID!=target.ID)Travel(m,target,false);
                if(alive.Any(m=>m.Player.Entity.MapID!=target.ID))throw new InvalidOperationException("PK stage transfer incomplete.");
                r.Stage=stage;r.Moving=false;foreach(var m in alive){m.Ready=now.AddSeconds(10);m.Player.Send("The arena narrows. Steel yourself: battle resumes in ten seconds.");}
            }
            MarkField(r);
        }
        private void Finish(Run r) {
            bool earned=LocalPkResults.Finish(r.Key,r.Kind,r.Winner,r.Entries.Select(m=>new LocalPkResults.Standing{Uid=m.Player.Entity.UID,Damage=m.Damage,Alive=!m.Out && m.Admitted}),r.ResultTime,false);
            foreach(var m in r.Entries) {
                if(!earned && !m.Out && Q.Player(m.Player,false))m.Player.Send("This division has ended without a champion. Return stronger for the next war.");
                Home(m);
            }
            r.Done=true;
            if(earned)refresh.Add(r.Winner);
            if(earned)Broadcast(Rules.Name(r.Kind)+" has crowned its "+(Rules.BpWar(r.Kind)?"division":new[]{"level 1-99","level 100-119","level 120-129","level 130+"}[r.Bracket])+" champion. The envoy holds the victor's purse.");
        }
        private static void Broadcast(string message) {foreach(var p in Kernel.GamePool.Values.ToArray())if(Q.Player(p,false))p.Send(message);}
        internal void Tick() {
            if(Interlocked.CompareExchange(ref ticking,1,0)!=0)return;
            try {
                lock(gate) {
                    DateTime now=clock();if(now<next)return;next=now.AddSeconds(1);
                    foreach(int kind in Rules.Kinds) {
                        DateTime start=ScheduleStart(now,kind);
                        if(now<start || now>=start.AddMinutes(Rules.JoinMinutes(kind)) || !Scheduled(now,kind))continue;
                        for(int bracket=0;bracket<(Rules.BpWar(kind)?1:4);bracket++) {
                            string key="pk-"+kind+"-"+TimeZoneInfo.ConvertTimeFromUtc(now,Zone).ToString("yyyyMMdd",CultureInfo.InvariantCulture)+"-"+bracket;
                            if(runs.Any(r=>r.Key==key))continue;
                            if(LocalPkResults.Open(key,kind,bracket,start)) {
                                runs.RemoveAll(r=>r.Done && r.Kind==kind && r.Bracket==bracket);
                                runs.Add(new Run{Key=key,Kind=kind,Bracket=bracket,Start=start,Deadline=start.AddMinutes(Rules.Duration(kind))});
                                if(bracket==3 && (!Rules.ClassWar(kind) || kind==19))Broadcast(Rules.ClassWar(kind)?"Class PK registration is open. Meet the Class PK Envoy before 19:30 Pacific.":Rules.Name(kind)+" is open. Meet General Bravery before 20:19 Pacific.");
                                if(kind==22)Broadcast("The BP trials are open. Meet the BP Tournament Manager in Twin City (295,141) before 20:10 Pacific.");
                            }
                        }
                    }
                    foreach(var r in runs.Where(r=>!r.Done).ToArray())Pulse(r,now);
                }
                uint[] ids;lock(gate)ids=refresh.ToArray();
                foreach(uint uid in ids){GameState p;if(Kernel.GamePool.TryGetValue(uid,out p) && Q.Player(p,false))lock(p.ItemSyncRoot)Q.Refresh(p);lock(gate)refresh.Remove(uid);}
            } catch(Exception ex){Console.WriteLine("[local-pk] tick-failed error="+ex.GetType().Name);}
            finally{Volatile.Write(ref ticking,0);}
        }
        internal static bool LegacyHonor(ulong status) {return Current!=null && Rules.LegacyKeys.Contains(status);}
        internal static void SynchronizeHonors(GameState p,DateTime utc) {
            if(Current==null || p.Entity.Titles==null)return;
            var honors=LocalPkResults.Honors(p.Entity.UID,utc);bool changed=false;
            if(CouplePkEvent.Current!=null){DateTime? spouse=CouplePkResults.Honor(p.Entity.UID,utc);if(spouse.HasValue)honors[2]=spouse.Value;}
            // Compact legacy database keys are not native title-list IDs.
            foreach(var key in Rules.LegacyKeys.Select(i=>(TitlePacket.Titles)i)) {
                DateTime prior;if(p.Entity.Titles.TryRemove(key,out prior))changed=true;
                if(p.Entity.MyTitle==key){p.Entity.MyTitle=TitlePacket.Titles.None;p.Send(new TitlePacket(true){Identifier=p.Entity.UID,Type=TitlePacket.Types.Switch,Title=TitlePacket.Titles.None});changed=true;}
            }
            // Prefer the current class, then monthly, weekly, then a former class honor.
            int family=Rules.ClassKind(p.Entity.Class);
            int choice=honors.ContainsKey(family)?family:honors.ContainsKey(1)?1:honors.ContainsKey(0)?0:
                honors.OrderByDescending(h=>h.Value).Select(h=>h.Key).DefaultIfEmpty(-1).First();
            ulong selected=Rules.Halo(choice);
            foreach(ulong flag in Rules.ManagedFlags()) {
                if(flag==selected)p.Entity.AddFlag(flag,StatusFlagsBigVector32.PermanentFlag,false);
                else p.Entity.RemoveFlag(flag);
            }
            if(changed){var packet=new TitlePacket(true){Identifier=p.Entity.UID,Type=TitlePacket.Types.List,Title=p.Entity.MyTitle,Count=(byte)Math.Min(255,p.Entity.Titles.Count)};
                foreach(var title in p.Entity.Titles.Keys.Take(255))packet.Add((byte)title);p.Send(packet);}
        }
        internal static bool HandleNpc(GameState p,NpcRequest request) {
            if(p!=null && p.ActiveNpc==BattlePowerTournament.Manager)return BattlePowerTournament.HandleNpc(p,request,Current==null?DateTime.UtcNow:Current.clock());
            if(p==null || p.Entity==null || request==null || (p.ActiveNpc!=General && p.ActiveNpc!=Clerk && p.ActiveNpc!=RetiredClerk && p.ActiveNpc!=70500 && (p.ActiveNpc<ClassEnvoy || p.ActiveNpc>745)))return false;
            var c=Current;if(c==null || p.ItemSyncRoot==null)return true;
            lock(p.ItemSyncRoot) {
                bool classEnvoy=p.ActiveNpc==ClassEnvoy;bool general=p.ActiveNpc==General || classEnvoy;
                if(!Q.Near(p,p.ActiveNpc,general?(ushort)1002:p.Entity.MapID) || !general && !InEvent(p.Entity))return true;
                try {
                    if(general && (request.OptionID==1 || request.OptionID==2 && !classEnvoy)){int kind=classEnvoy?Rules.ClassKind(p.Entity.Class):request.OptionID-1;
                        if(classEnvoy && kind<0){p.Send("Choose your school with a class master before entering the Class War.");return true;}
                        if(!c.Enroll(p,kind))p.Send("That war is closed, full, or your place has already been used.");return true;}
                    if(general && request.OptionID==3){p.Send(LocalPkResults.Claim(p,c.clock()));Q.Refresh(p);return true;}
                    if(!general && request.OptionID==4){lock(c.gate){Member m;if(c.members.TryGetValue(p.Entity.UID,out m) && m.Player==p)c.Home(m);}return true;}
                    var d=new Npcs(p);d.Text(classEnvoy?"The Class War opens Monday. Enroll from 19:15 to 19:30 Pacific, then prove yourself against heroes of your class and level. New champions earn a purse and a bound P7 soul.":general?"The Weekly War opens Saturday at 20:00 Pacific; the Monthly War opens on the first. Enter before 20:19. Only the last worthy fighter wins. New champions earn a purse and a bound P7 soul.":"The weak retreat. The strong endure. Leave now and your place is lost.");
                    if(general){if(classEnvoy)d.Option("Enter my Class War.",1);else{d.Option("Join the Weekly War.",1);d.Option("Join the Monthly War.",2);}d.Option("Claim my champion's purse.",3);}else d.Option("Return to Twin City.",4);
                    d.Option("Farewell.",255);d.Send();
                } catch(Exception ex){Q.Fail(p,"local-pk",ex);}return true;
            }
        }
    }
}
