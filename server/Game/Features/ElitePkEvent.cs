using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Revora.Client;
using Npgsql;
using Revora.Database;
using Revora.Interfaces;
using Revora.Network.GamePackets;

namespace Revora.Game.Features
{
    internal sealed class ElitePkEvent
    {
        internal const uint Envoy=448582, Marshal=900000061;
        internal static ElitePkEvent Current;
        private static readonly TimeZoneInfo Zone=TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
        private readonly object gate=TournamentCombat.Gate;
        private readonly Func<DateTime> clock;
        private readonly Func<ushort,Map> factory;
        private readonly Map waiting;
        private readonly Dictionary<int,Map> arenas=new Dictionary<int,Map>();
        private readonly Dictionary<uint,Session> sessions=new Dictionary<uint,Session>();
        private readonly Dictionary<uint,Session> viewers=new Dictionary<uint,Session>();
        private readonly HashSet<uint> transfers=new HashSet<uint>();
        private readonly HashSet<uint> refresh=new HashSet<uint>();
        private readonly List<Group> groups=new List<Group>();
        private int ticking;
        private DateTime next;
        private sealed class Session { internal GameState Player;internal Enums.PKMode Mode;internal bool Withdrawn; }
        private sealed class Group {
            internal string Run;internal int Id, Round;internal DateTime Start, Deadline;
            internal bool Begun, Done;internal List<ElitePkResults.Entry> Alive=new List<ElitePkResults.Entry>();
            internal Dictionary<uint,ElitePkResults.Entry> Entries=new Dictionary<uint,ElitePkResults.Entry>();
            internal List<Match> Matches=new List<Match>();
            internal readonly List<uint> BronzeCandidates=new List<uint>();
        }
        private sealed class Match {
            internal Group Group;internal ElitePkResults.Pair Pair;internal Map Map;
            internal bool Importing, Imported, Exported;internal DateTime End, Ready;
            internal long LeftDamage, RightDamage;internal uint? Winner;internal string Reason;
        }
        internal ElitePkEvent(Func<ushort,Map> makeMap,Func<DateTime> utcClock) {
            factory=makeMap;clock=utcClock;ElitePkResults.Initialize();
            waiting=CreateMap(2068);Constants.PKForbiddenMaps.Add(waiting.ID);CreateMarshal(waiting);
        }
        private Map CreateMap(ushort source) {
            var map=factory(source);var information=MapsTable.MapInformations[source];
            information.ID=map.ID;information.BaseID=source;MapsTable.MapInformations[map.ID]=information;
            return map;
        }
        internal static void Create() {
            Current=new ElitePkEvent(id=>Kernel.Maps[id].MakeDynamicMap(),()=>DateTime.UtcNow);
        }
        internal static void PlaceEnvoy() {
            Map town=Kernel.Maps[1002];ushort mesh;byte type;
            using(var c=DataHolder.MySqlConnection) { c.Open();using(var q=new NpgsqlCommand("SELECT lookface,type FROM npcs WHERE id=@uid",c)) {
                q.Parameters.AddWithValue("uid",(long)Envoy);using(var r=q.ExecuteReader()) {
                    if(!r.Read())throw new InvalidOperationException("Installed Elite PK Envoy is missing.");
                    mesh=Convert.ToUInt16(r[0]);type=Convert.ToByte(r[1]);
                }
            }}
            foreach(var map in Kernel.Maps.Values.ToArray()) {
                INpc previous;if(map.Npcs!=null && map.Npcs.TryGetValue(Envoy,out previous))map.RemoveNpc(previous);
            }
            ushort x=438,y=270;bool found=false;
            for(int radius=0;radius<=8 && !found;radius++)
                for(int dx=-radius;dx<=radius && !found;dx++)for(int dy=-radius;dy<=radius && !found;dy++) {
                    int tx=438+dx,ty=270+dy;
                    if(!town.Floor[tx,ty,MapObjectType.Player] || town.Npcs.Values.Any(n=>n.X==tx && n.Y==ty))continue;
                    x=(ushort)tx;y=(ushort)ty;found=true;
                }
            if(!found)throw new InvalidOperationException("Elite PK Envoy plaza is obstructed.");
            town.AddNpc(new NpcSpawn{UID=Envoy,MapID=1002,X=x,Y=y,Mesh=mesh,Type=(Enums.NpcType)type,Name="ElitePKEnvoy"});
            Console.WriteLine("[elite-pk] envoy map=1002 x="+x+" y="+y);
        }
        internal static int Bracket(int level) { return level>=130?3:level>=120?2:level>=100?1:0; }
        internal static DateTime Start(DateTime utc,int bracket) {
            var local=TimeZoneInfo.ConvertTimeFromUtc(utc,Zone);
            return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local.Date.AddHours(20).AddMinutes((3-bracket)*2),DateTimeKind.Unspecified),Zone);
        }
        private static string Cycle(DateTime utc) { return TimeZoneInfo.ConvertTimeFromUtc(utc,Zone).ToString("yyyyMMdd",CultureInfo.InvariantCulture); }
        private static void CreateMarshal(Map map) {
            var xy=map.RandomCoordinates();ushort x=xy.Item1,y=xy.Item2;
            if(!PotencyGuide.FindLanding(map,ref x,ref y))throw new InvalidOperationException("Elite PK marshal tile unavailable.");
            CityGuardSystem.SpawnTalker(Marshal,map.ID,x,y,50000,"ElitePKClerk");
        }
        private bool Owns(ushort id) { return id==waiting.ID || arenas.Values.Any(m=>m.ID==id); }
        internal static bool InEvent(Entity entity) {
            var e=Current;if(e==null || entity==null)return false;lock(e.gate)return e.Owns(entity.MapID);
        }
        private static bool Ordinary(GameState p) {
            GameState live;
            return p!=null && !p.Fake && p.Entity!=null && p.Action==2 && p.Entity.FullyLoaded && !p.Entity.PersistenceRequiresReload
                && Kernel.GamePool.TryGetValue(p.Entity.UID,out live) && ReferenceEquals(live,p);
        }
        private bool Present(uint uid,ushort mapId) {
            Session s;return sessions.TryGetValue(uid,out s) && !s.Withdrawn && Ordinary(s.Player)
                && s.Player.Entity.MapID==mapId && !s.Player.Entity.Dead;
        }
        private bool Near(GameState p,uint npc,ushort map) {
            INpc actor;
            return Ordinary(p) && !p.Entity.Dead && p.Entity.MapID==map && p.ActiveNpc==npc
                && p.Booth==null && p.Trade!=null && !p.Trade.InTrade && p.Map.Npcs.TryGetValue(npc,out actor)
                && Kernel.GetDistance(p.Entity.X,p.Entity.Y,actor.X,actor.Y)<=6;
        }
        internal bool Enroll(GameState p) {
            if(p==null || p.ItemSyncRoot==null)return false;
            lock(p.ItemSyncRoot)lock(gate) {
                if(!Near(p,Envoy,1002) || p.Entity.OnDeath!=null || p.InQualifier() || p.InTeamQualifier()
                    || p.ElitePKMatch!=null || p.WatchingElitePKMatch!=null)return false;
                var g=groups.FirstOrDefault(x=>!x.Done && !x.Begun && x.Id==Bracket(p.Entity.Level) && clock()<x.Start);
                if(g==null || sessions.ContainsKey(p.Entity.UID) || viewers.ContainsKey(p.Entity.UID))return false;
                var entry=new ElitePkResults.Entry{Uid=p.Entity.UID,Level=p.Entity.Level,Power=(int)p.Entity.BattlePower,Seed=Guid.NewGuid()};
                if(!ElitePkResults.Register(g.Run,entry,clock()))return false;
                sessions[p.Entity.UID]=new Session{Player=p,Mode=p.Entity.PKMode};
                Travel(p,waiting,false);p.Send("Your place is reserved. Stay in the waiting hall until your duel is called.");return true;
            }
        }
        private void Travel(GameState p,Map map,bool heal) {
            transfers.Add(p.Entity.UID);
            try {
                var xy=map==null?Tuple.Create((ushort)408,(ushort)330):map.RandomCoordinates();
                p.Entity.Teleport(map==null?(ushort)1002:map.ID,xy.Item1,xy.Item2);
                if(heal) { p.Entity.BringToLife();p.Entity.Mana=p.Entity.MaxMana; }
            } finally { transfers.Remove(p.Entity.UID); }
        }
        private static void Mode(GameState p,Enums.PKMode mode) {
            p.Entity.PKMode=mode;p.Send(new Data(true){UID=p.Entity.UID,ID=Data.ChangePKMode,dwParam=(uint)mode});
        }
        private void Home(uint uid) {
            Session s;
            if(!sessions.TryGetValue(uid,out s) && !viewers.TryGetValue(uid,out s))return;
            if(Ordinary(s.Player)) { Mode(s.Player,s.Mode);Travel(s.Player,null,true); }
            sessions.Remove(uid);viewers.Remove(uid);
        }
        internal static void OnTeleport(GameState p,ushort destination) {
            var e=Current;if(e==null || p==null || p.Entity==null)return;
            lock(e.gate) {
                if(e.transfers.Contains(p.Entity.UID) || !e.Owns(p.Entity.MapID) || destination==p.Entity.MapID)return;
                Session s;
                if(e.sessions.TryGetValue(p.Entity.UID,out s) && s.Player==p) { s.Withdrawn=true;Mode(p,s.Mode); }
                if(e.viewers.TryGetValue(p.Entity.UID,out s) && s.Player==p) { e.viewers.Remove(p.Entity.UID);Mode(p,s.Mode); }
            }
        }
        private Match Duel(uint uid) { return groups.SelectMany(g=>g.Matches).FirstOrDefault(m=>!m.Exported && (m.Pair.Left==uid || m.Pair.Right==uid)); }
        private bool Fight(Entity attacker,Entity target) {
            if(attacker==null || target==null || attacker.Owner==null || target.Owner==null)return false;
            var match=Duel(attacker.Owner.Entity.UID);
            return match!=null && match.Imported && !match.Winner.HasValue && clock()>=match.Ready && clock()<match.End
                && attacker.MapID==match.Map.ID && target.MapID==match.Map.ID && attacker.Owner!=target.Owner
                && (target.Owner.Entity.UID==match.Pair.Left || target.Owner.Entity.UID==match.Pair.Right)
                && Present(match.Pair.Left,match.Map.ID) && Present(match.Pair.Right,match.Map.ID)
                && ReferenceEquals(sessions[attacker.Owner.Entity.UID].Player,attacker.Owner)
                && ReferenceEquals(sessions[target.Owner.Entity.UID].Player,target.Owner);
        }
        private sealed class CombatScope : IDisposable {
            private object owned;
            internal CombatScope(object value) { owned=value; }
            public void Dispose() { var value=Interlocked.Exchange(ref owned,null);if(value!=null)Monitor.Exit(value); }
        }
        internal static IDisposable CombatLease(Entity attacker,Entity target) {
            var e=Current;if(e==null)return null;
            Monitor.Enter(e.gate);
            if(attacker!=null && e.Owns(attacker.MapID) || target!=null && e.Owns(target.MapID))return new CombatScope(e.gate);
            Monitor.Exit(e.gate);return null;
        }
        internal static bool BlockAttack(Entity attacker,Entity target) {
            var e=Current;if(e==null)return false;
            lock(e.gate) {
                bool owned=attacker!=null && e.Owns(attacker.MapID) || target!=null && e.Owns(target.MapID);
                return owned && !e.Fight(attacker,target);
            }
        }
        internal static bool BlockSpell(GameState p) {
            var e=Current;if(e==null || p==null || p.Entity==null)return false;
            lock(e.gate) {
                if(!e.Owns(p.Entity.MapID))return false;
                var m=e.Duel(p.Entity.UID);
                return m==null || !m.Imported || m.Winner.HasValue || e.clock()<m.Ready || e.clock()>=m.End;
            }
        }
        internal static void Damage(Entity attacker,Entity victim,uint damage) {
            var e=Current;if(e==null || victim==null || victim.EntityFlag!=EntityFlag.Player)return;
            lock(e.gate) {
                if(!e.Fight(attacker,victim))return;
                var m=e.Duel(attacker.Owner.Entity.UID);long actual=Math.Min(damage,victim.Hitpoints);
                if(m.Pair.Left==attacker.Owner.Entity.UID)m.LeftDamage+=actual;else m.RightDamage+=actual;
            }
        }
        internal static void OnDeath(GameState p) {
            var e=Current;if(e==null || p==null || p.Entity==null)return;
            lock(e.gate) {
                var m=e.Duel(p.Entity.UID);
                if(m==null || !m.Imported || m.Winner.HasValue || m.Map==null || p.Entity.MapID!=m.Map.ID
                    || !Ordinary(p) || !e.sessions.ContainsKey(p.Entity.UID) || !ReferenceEquals(e.sessions[p.Entity.UID].Player,p))return;
                m.Winner=m.Pair.Left==p.Entity.UID?m.Pair.Right:m.Pair.Left;m.Reason="defeat";
            }
        }
        internal void Tick() {
            if(Interlocked.CompareExchange(ref ticking,1,0)!=0)return;
            try {
                lock(gate) {
                    DateTime utc=clock();if(utc<next)return;next=utc.AddSeconds(1);
                    DateTime local=TimeZoneInfo.ConvertTimeFromUtc(utc,Zone);
                    if(local.DayOfWeek==DayOfWeek.Friday && local.TimeOfDay>=TimeSpan.FromHours(19).Add(TimeSpan.FromMinutes(55))
                        && local.TimeOfDay<TimeSpan.FromHours(20).Add(TimeSpan.FromMinutes(6))) {
                        for(int id=0;id<4;id++) {
                            string key="epk-"+Cycle(utc)+"-"+id;
                            if(groups.Any(g=>g.Run==key))continue;
                            DateTime start=Start(utc,id),deadline=Start(utc,3).AddMinutes(30);
                            if(utc>=start)continue;
                            if(ElitePkResults.Open(key,Cycle(utc),id,start,deadline)) {
                                groups.RemoveAll(g=>g.Id==id && g.Done);
                                groups.Add(new Group{Run=key,Id=id,Start=start,Deadline=deadline});
                                Broadcast("Elite PK registration is open. Meet the Elite PK Envoy in Twin City before your level bracket begins.");
                            }
                        }
                    }
                    foreach(var g in groups.Where(g=>!g.Done).ToArray()) {
                        if(utc>=g.Deadline) { Finish(g,0,true);continue; }
                        if(!g.Begun) {
                            if(utc<g.Start)continue;
                            g.Alive=ElitePkResults.Begin(g.Run,utc);g.Entries=g.Alive.ToDictionary(x=>x.Uid);g.Begun=true;
                        }
                        if(g.Matches.Count==0) {
                            if(g.Alive.Count<=1) { Finish(g,g.Alive.Count==1?g.Alive[0].Uid:0,false);continue; }
                            NextRound(g);
                        }
                        foreach(var m in g.Matches)Pulse(m,utc);
                        if(g.Matches.All(m=>m.Exported)) {
                            g.Alive=g.Matches.Where(m=>!m.Pair.Bronze && m.Winner.GetValueOrDefault()!=0).Select(m=>g.Entries[m.Winner.Value]).ToList();
                            g.Matches.Clear();
                        }
                    }
                }
                uint[] pending;lock(gate){pending=refresh.ToArray();}
                foreach(uint uid in pending) {
                    GameState p;
                    if(Kernel.GamePool.TryGetValue(uid,out p) && Ordinary(p)) {
                        if(p.Entity.MyAchievement!=null)p.Entity.MyAchievement.Invalidate();
                        TitleRequirements.Reconcile(p,false);
                    }
                    lock(gate)refresh.Remove(uid);
                }
            } catch(Exception ex) { Console.WriteLine("[elite-pk] tick-failed error="+ex.GetType().Name); }
            finally { Volatile.Write(ref ticking,0); }
        }
        internal static List<ElitePkResults.Pair> Pairings(IList<uint> entrants) {
            var pairs=new List<ElitePkResults.Pair>();int size=1;while(size<entrants.Count)size*=2;
            int byes=size-entrants.Count,index=0;
            while(index<byes)pairs.Add(new ElitePkResults.Pair{Slot=pairs.Count,Left=entrants[index++]});
            while(index<entrants.Count)pairs.Add(new ElitePkResults.Pair{Slot=pairs.Count,Left=entrants[index++],Right=entrants[index++]});
            return pairs;
        }
        private void NextRound(Group g) {
            var pairs=Pairings(g.Alive.Select(p=>p.Uid).ToArray());int round=g.Round+1;
            if(g.Alive.Count==2 && g.BronzeCandidates.Count>0){
                if(g.BronzeCandidates.Count>2)throw new InvalidOperationException("Elite bronze roster exceeds two.");
                pairs.Add(new ElitePkResults.Pair{Slot=1,Left=g.BronzeCandidates[0],Right=g.BronzeCandidates.Count==2?g.BronzeCandidates[1]:0,Bronze=true});
            }
            var matches=new List<Match>();
            foreach(var pair in pairs) {
                Map arena=null;
                if(pair.Right!=0) {
                    int key=g.Id*16+pair.Slot;
                    if(!arenas.TryGetValue(key,out arena)) { arena=CreateMap(700);arenas.Add(key,arena);Constants.PKFreeMaps.Add(arena.ID);CreateMarshal(arena); }
                    foreach(var floor in arena.FloorItems.Values.ToArray())arena.RemoveFloorItem(floor);
                    arena.Clouds.Clear();
                }
                matches.Add(new Match{Group=g,Pair=pair,Map=arena});
            }
            ElitePkResults.Round(g.Run,round,pairs);g.Round=round;g.Matches=matches;
        }
        private void Pulse(Match m,DateTime utc) {
            if(m.Exported)return;
            if(!m.Imported && !m.Winner.HasValue) {
                bool a=Present(m.Pair.Left,waiting.ID) || m.Importing && Present(m.Pair.Left,m.Map.ID);
                bool b=m.Pair.Right!=0 && (Present(m.Pair.Right,waiting.ID) || m.Importing && Present(m.Pair.Right,m.Map.ID));
                if(m.Pair.Right==0 || !a || !b) { m.Winner=a?m.Pair.Left:b?m.Pair.Right:0;m.Reason=m.Pair.Right==0?"bye":"no-show"; }
                else {
                    var left=sessions[m.Pair.Left].Player;var right=sessions[m.Pair.Right].Player;
                    // Retry partial server transfers while combat remains blocked.
                    m.Importing=true;
                    Travel(left,m.Map,true);Travel(right,m.Map,true);Mode(left,Enums.PKMode.PK);Mode(right,Enums.PKMode.PK);
                    if(!Present(m.Pair.Left,m.Map.ID) || !Present(m.Pair.Right,m.Map.ID))throw new InvalidOperationException("Elite PK transfer incomplete.");
                    m.Ready=utc.AddSeconds(10);m.End=utc.AddMinutes(m.Group.Alive.Count<=8?5:3);m.Imported=true;
                    string briefing=m.Pair.Bronze?"Third place is at stake. Your duel begins in ten seconds.":"Your duel begins in ten seconds. Defeat your rival or deal more damage before time runs out.";
                    left.Send(briefing);right.Send(briefing);
                }
            }
            if(m.Imported && !m.Winner.HasValue) {
                bool a=Present(m.Pair.Left,m.Map.ID),b=Present(m.Pair.Right,m.Map.ID);
                if(!a || !b) { m.Winner=a?m.Pair.Left:b?m.Pair.Right:0;m.Reason="forfeit"; }
                else if(utc>=m.End) {
                    var x=m.Group.Entries[m.Pair.Left];var y=m.Group.Entries[m.Pair.Right];
                    if(m.LeftDamage+m.RightDamage==0) { m.Winner=0;m.Reason="idle"; }
                    else { m.Winner=m.LeftDamage!=m.RightDamage?(m.LeftDamage>m.RightDamage?x.Uid:y.Uid):
                        x.Power!=y.Power?(x.Power>y.Power?x.Uid:y.Uid):x.Level!=y.Level?(x.Level>y.Level?x.Uid:y.Uid):
                        x.Seed.CompareTo(y.Seed)<0?x.Uid:y.Uid;m.Reason="timeout"; }
                }
            }
            if(!m.Winner.HasValue)return;
            ElitePkResults.Resolve(m.Group.Run,m.Group.Round,m.Pair.Slot,m.Winner.Value,m.LeftDamage,m.RightDamage,m.Reason);
            foreach(uint uid in new[]{m.Pair.Left,m.Pair.Right}) {
                Session s;if(uid==0 || !sessions.TryGetValue(uid,out s))continue;
                bool bronzeCandidate=!m.Pair.Bronze && uid!=m.Winner && m.Winner!=0 && m.Group.Alive.Count>2 && m.Group.Alive.Count<=4
                    && (m.Reason=="defeat" || m.Reason=="timeout") && m.LeftDamage+m.RightDamage>0 && Ordinary(s.Player) && !s.Withdrawn;
                if(bronzeCandidate){
                    if(!m.Group.BronzeCandidates.Contains(uid))m.Group.BronzeCandidates.Add(uid);
                    Travel(s.Player,waiting,true);s.Player.Send("Rest here. You will fight for third place.");
                }else if(!m.Pair.Bronze && uid==m.Winner && Ordinary(s.Player)) { Travel(s.Player,waiting,true);s.Player.Send("You advance to the next round."); }
                else Home(uid);
            }
            if(m.Map!=null)foreach(var v in viewers.Where(v=>v.Value.Player.Entity.MapID==m.Map.ID).Select(v=>v.Key).ToArray())Home(v);
            m.Exported=true;
        }
        private void Finish(Group g,uint winner,bool cancelled) {
            bool earned=ElitePkResults.Finish(g.Run,winner,clock(),cancelled);
            foreach(var entry in g.Entries.Values)Home(entry.Uid);
            // Registration interrupted before Begin also owns sessions in the waiting hall.
            if(!g.Begun)foreach(var s in sessions.Values.Where(s=>Bracket(s.Player.Entity.Level)==g.Id).ToArray())Home(s.Player.Entity.UID);
            foreach(var v in viewers.Where(v=>g.Matches.Any(m=>m.Map!=null && m.Map.ID==v.Value.Player.Entity.MapID)).Select(v=>v.Key).ToArray())Home(v);
            g.Done=true;if(earned)foreach(uint uid in g.Entries.Keys)refresh.Add(uid);
            Broadcast(earned?"Elite PK has crowned its champion. Level 130+ placed fighters earn fourteen-day Revora titles; their champion also earns seven-day Infernal Beast wings.":"An Elite PK bracket ended without a champion.");
        }
        private static void Broadcast(string message) {
            foreach(var p in Kernel.GamePool.Values.ToArray())if(Ordinary(p))p.Send(message);
        }
        private bool Watch(GameState p) {
            lock(gate) {
                if(!Near(p,Envoy,1002) || sessions.ContainsKey(p.Entity.UID) || viewers.ContainsKey(p.Entity.UID))return false;
                var m=groups.OrderByDescending(g=>g.Id).SelectMany(g=>g.Matches).FirstOrDefault(x=>x.Imported && !x.Winner.HasValue
                    && viewers.Values.Count(v=>v.Player.Entity.MapID==x.Map.ID)<20);
                if(m==null)return false;viewers[p.Entity.UID]=new Session{Player=p,Mode=p.Entity.PKMode};
                Travel(p,m.Map,false);Mode(p,Enums.PKMode.Peace);p.Send("Watch the duel in peace. Speak to the Elite PK Clerk when you wish to leave.");return true;
            }
        }
        internal static void SynchronizeChampion(GameState p,DateTime utc) {
            if(p.Entity.MyAchievement==null || p.Entity.Titles==null)return;
            var key=TitlePacket.Titles.ElitePKChamption_High;
            DateTime? expiry=p.Entity.MyAchievement.AwardExpiry(60010020,utc);DateTime previous;
            bool existed=p.Entity.Titles.TryGetValue(key,out previous);
            if(expiry.HasValue) {
                if(existed && previous==expiry.Value.ToLocalTime())return;
                p.Entity.Titles[key]=expiry.Value.ToLocalTime();
            } else {
                if(!existed && p.Entity.MyTitle!=key)return;
                p.Entity.Titles.TryRemove(key,out previous);
                if(p.Entity.MyTitle==key) {
                    p.Entity.MyTitle=TitlePacket.Titles.None;
                    p.Send(new TitlePacket(true){Identifier=p.Entity.UID,Type=TitlePacket.Types.Switch,Title=TitlePacket.Titles.None});
                }
            }
            var packet=new TitlePacket(true){Identifier=p.Entity.UID,Type=TitlePacket.Types.List,Title=p.Entity.MyTitle,Count=(byte)Math.Min(255,p.Entity.Titles.Count)};
            foreach(var title in p.Entity.Titles.Keys.Take(255))packet.Add((byte)title);
            p.Send(packet);
        }
        internal bool HandleNpc(GameState p,NpcRequest request) {
            if(p==null || p.Entity==null || p.ItemSyncRoot==null || request==null || (p.ActiveNpc!=Envoy && p.ActiveNpc!=Marshal))return false;
            // Reward effects take the mentor lock before entering any tournament gate.
            if(ElitePkPrizes.HandleNpc(p,request,clock))return true;
            lock(p.ItemSyncRoot)lock(gate) {
                bool envoy=p.ActiveNpc==Envoy;
                if(!Near(p,p.ActiveNpc,envoy?(ushort)1002:p.Entity.MapID) || !envoy && !Owns(p.Entity.MapID))return true;
                var d=new Npcs(p);
                if(request.OptionID==3 && envoy) { if(!Enroll(p))p.Send("Your bracket is closed, full, or you already have a place.");return true; }
                if(request.OptionID==4 && envoy) { if(!Watch(p))p.Send("No duel is ready for spectators just now.");return true; }
                if(request.OptionID==1 && !envoy) { Home(p.Entity.UID);return true; }
                if(request.OptionID==5 && envoy) { refresh.Add(p.Entity.UID);p.Send("Level 130+ placement titles last fourteen days; champion wings last seven. Find your earned honors in the Wardrobe.");return true; }
                d.Text(envoy?"The dueling halls open each Friday at 19:55 Pacific. Prove your strength among heroes of your level. Level 130+ placements earn fourteen-day titles; their champion also earns Infernal Beast wings.":"A steady blade and a clear mind win duels. Leave the hall and your place in battle is forfeit.");
                if(envoy) { d.Option("Enter my bracket.",3);d.Option("Watch a duel.",4);d.Option("My champion honors.",5);d.Option("My tournament prize.",6); }
                else d.Option("Return to Twin City.",1);
                d.Option("Farewell.",255);d.Send();return true;
            }
        }
    }
}
