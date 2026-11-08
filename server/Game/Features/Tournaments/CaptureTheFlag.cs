using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Revora.Client;
using Revora.Database;
using Revora.Game.Features;
using Revora.Interfaces;
using Revora.Network.GamePackets;

namespace Revora.Game
{
    public sealed class CaptureTheFlag
    {
        public const ushort MapID=2057;
        internal const uint ReturnNpc=900000060;
        private readonly object sync=new object();
        private readonly Map map;
        private readonly Func<DateTime> clock;
        private readonly Dictionary<uint,Base> bases=new Dictionary<uint,Base>();
        private readonly Dictionary<uint,uint> members=new Dictionary<uint,uint>();
        private readonly HashSet<uint> contributors=new HashSet<uint>();
        private readonly Dictionary<uint,Carry> carriers=new Dictionary<uint,Carry>();
        private readonly Dictionary<uint,Tuple<GameState,Enums.PKMode>> modes=new Dictionary<uint,Tuple<GameState,Enums.PKMode>>();
        private int ticking;
        private string run;
        private DateTime end, nextTick;
        private bool running, needsCleanup;
        private uint nextFlag=910000000;
        private static readonly TimeZoneInfo Zone=TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
        private sealed class Base {
            internal SobNpcSpawn Npc; internal string Name; internal uint Guild;
            internal readonly Dictionary<uint,ulong> Damage=new Dictionary<uint,ulong>();
        }
        private sealed class Carry { internal Guid Token; internal DateTime Deadline; internal GameState Session; }
        public static bool IsWar { get { return Server.Thread!=null && Server.Thread.CTF!=null && Server.Thread.CTF.running; } }
        public CaptureTheFlag():this(Arena(),()=>DateTime.UtcNow) { }
        private static Map Arena() {
            if(!Kernel.Maps.ContainsKey(MapID))new Map(MapID,DMaps.MapPaths[MapID]);
            return Kernel.Maps[MapID];
        }
        internal CaptureTheFlag(Map arena,Func<DateTime> utcClock) {
            map=arena;clock=utcClock;
            foreach(var npc in map.Npcs.Values.OfType<SobNpcSpawn>())
                if(npc.UID>=20571 && npc.UID<=20573)bases.Add(npc.UID,new Base{Npc=npc,Name=npc.Name});
            CtfResults.Initialize(); // An interrupted arena is cancelled, never awarded or replayed.
            ClearWorld();
            if(bases.Count!=3 || bases.Values.Any(b=>b.Npc.MaxHitpoints==0))
                throw new InvalidOperationException("CTF needs the three installed living flag bases.");
            INpc existing;
            if(map.Npcs.TryGetValue(ReturnNpc,out existing) && (existing.Name!="FlagMarshal" || existing.MapID!=MapID))
                throw new InvalidOperationException("CTF return NPC identity collision.");
            ushort x=482,y=367;
            if(!PotencyGuide.FindLanding(map,ref x,ref y))throw new InvalidOperationException("CTF return tile unavailable.");
            CityGuardSystem.SpawnTalker(ReturnNpc,MapID,x,y,50000,"FlagMarshal");
        }
        internal static bool Window(DateTime utc,out string key,out DateTime finish) {
            if(utc.Kind!=DateTimeKind.Utc)throw new ArgumentException("CTF requires UTC.");
            DateTime local=TimeZoneInfo.ConvertTimeFromUtc(utc,Zone);
            key="ctf-"+local.ToString("yyyyMMdd",CultureInfo.InvariantCulture);
            finish=TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local.Date.AddHours(21).AddMinutes(30),DateTimeKind.Unspecified),Zone);
            return local.DayOfWeek==DayOfWeek.Saturday && local.Hour==21 && local.Minute<30;
        }
        public void Tick() {
            if(Interlocked.CompareExchange(ref ticking,1,0)!=0)return;
            DateTime utc=clock();
            try {
                lock(sync) {
                    if(utc<nextTick)return;nextTick=utc.AddSeconds(1);
                    if(running && utc>=end)Finish(utc);
                    if(!running) {
                        string key;DateTime finish;
                        if(Window(utc,out key,out finish) && bases.Count==3 && CtfResults.Start(key,finish)) {
                            ClearWorld();members.Clear();contributors.Clear();run=key;end=finish;running=true;
                            Broadcast("Capture the Flag has begun! Meet the Guild Controller in Twin City. The battle ends at 21:30 Pacific.");
                        }
                    }
                    if(running) {
                        foreach(var pair in carriers.ToArray()) {
                            var p=pair.Value.Session;
                            GameState live;
                            if(utc>=pair.Value.Deadline || !Participant(p) || !Kernel.GamePool.TryGetValue(pair.Key,out live) || !ReferenceEquals(live,p))Drop(p);
                        }
                        SpawnFlags();
                    }
                }
                CtfResults.RetryReturns();
                // No event lock while moving players or reconciling their persisted wardrobe.
                if(needsCleanup) {
                    foreach(var p in Kernel.GamePool.Values.ToArray()) {
                        if(p==null || p.Entity==null || p.Fake)continue;
                        if(p.Entity.MapID==MapID)p.Entity.Teleport(1002,408,330);
                        if(p.Entity.MyAchievement!=null)p.Entity.MyAchievement.Invalidate();
                        if(p.Entity.FullyLoaded && !p.Entity.PersistenceRequiresReload)TitleRequirements.Reconcile(p,false);
                    }
                    needsCleanup=false;
                }
            } catch(Exception ex) { Console.WriteLine("[ctf] tick-failed error="+ex.GetType().Name); }
            finally { Volatile.Write(ref ticking,0); }
        }
        private static void Broadcast(string text) {
            foreach(var p in Kernel.GamePool.Values.ToArray())if(p!=null && !p.Fake && p.Entity!=null && p.Entity.FullyLoaded)p.Send(text);
        }
        private bool Ordinary(GameState p) {
            GameState live;ConquerStructures.Society.Guild guild;
            return p!=null && !p.Fake && p.Action==2 && p.Entity!=null && p.Entity.FullyLoaded && !p.Entity.Dead
                && !p.Entity.PersistenceRequiresReload && p.Guild!=null && p.Guild.ID!=0 && p.Entity.GuildID==p.Guild.ID
                && p.Guild.Members.ContainsKey(p.Entity.UID)
                && Kernel.GamePool.TryGetValue(p.Entity.UID,out live) && ReferenceEquals(live,p)
                && Kernel.Guilds.TryGetValue(p.Guild.ID,out guild) && ReferenceEquals(guild,p.Guild) && p.Booth==null && p.Trade!=null && !p.Trade.InTrade;
        }
        private bool Participant(GameState p) {
            uint guild;
            return running && clock()<end && Ordinary(p) && p.Entity.MapID==MapID
                && members.TryGetValue(p.Entity.UID,out guild) && guild==p.Guild.ID;
        }
        public bool SignUp(GameState p) {
            if(p==null || p.ItemSyncRoot==null)return false;
            lock(p.ItemSyncRoot)lock(sync) {
                INpc npc;
                if(!Ordinary(p) || !running || clock()>=end || p.Entity.MapID!=1002 || p.ActiveNpc!=380
                    || !p.Map.Npcs.TryGetValue(380,out npc) || Kernel.GetDistance(p.Entity.X,p.Entity.Y,npc.X,npc.Y)>6)return false;
                ushort x=482,y=367;
                if(!PotencyGuide.FindLanding(map,ref x,ref y))return false;
                if(!CtfResults.Register(run,p.Entity.UID,p.Guild.ID,clock()) || !CtfResults.ReserveReturn(run,p))return false;
                members[p.Entity.UID]=p.Guild.ID;
                Drop(p);
                modes[p.Entity.UID]=Tuple.Create(p,p.Entity.PKMode);
                SetMode(p,Enums.PKMode.Team);
                p.Entity.Teleport(MapID,x,y);
                if(p.Entity.MapID!=MapID || p.Entity.X!=x || p.Entity.Y!=y || !CtfResults.ConfirmArrival(run,p,clock())){
                    CtfResults.RequestReturn(p.Entity.UID);CtfResults.Return(p,false);return false;
                }
                p.Send("Capture a base, then carry a flag to it within 60 seconds. Each delivery earns 10 points. Speak to the Flag Marshal to leave.");
                return true;
            }
        }
        internal bool CanAttack(GameState p,SobNpcSpawn npc) {
            lock(sync) { Base b;return Participant(p) && npc!=null && bases.TryGetValue(npc.UID,out b)
                && ReferenceEquals(b.Npc,npc) && b.Guild!=p.Guild.ID && OutsideCamp(p.Entity)
                && Kernel.GetDistance(p.Entity.X,p.Entity.Y,npc.X,npc.Y)<=Constants.pScreenDistance; }
        }
        internal void HitBase(GameState p,SobNpcSpawn npc,uint damage) {
            lock(p.ItemSyncRoot)lock(sync) {
                if(damage==0 || !CanAttack(p,npc))return;
                if(!contributors.Contains(p.Entity.UID)) {
                    if(!CtfResults.Contribute(run,p.Entity.UID,p.Guild.ID,clock()))return;
                    contributors.Add(p.Entity.UID);
                }
                var b=bases[npc.UID];uint actual=Math.Min(damage,npc.Hitpoints);
                ulong total;b.Damage.TryGetValue(p.Guild.ID,out total);b.Damage[p.Guild.ID]=total+actual;
                if(actual<npc.Hitpoints)npc.Hitpoints-=actual;
                else {
                    // Tied damage is resolved consistently; the guild ID is only a base tie-break.
                    b.Guild=b.Damage.OrderByDescending(v=>v.Value).ThenBy(v=>v.Key).First().Key;
                    ConquerStructures.Society.Guild guild;
                    npc.Name=Kernel.Guilds.TryGetValue(b.Guild,out guild)?guild.Name:"Captured Base";
                    npc.Hitpoints=npc.MaxHitpoints;b.Damage.Clear();
                }
                Kernel.SendScreen(npc,npc);
            }
        }
        private static bool OutsideCamp(Entity entity) {
            return entity!=null && Kernel.GetDistance(entity.X,entity.Y,482,367)>32;
        }
        public static bool Attackable(Entity entity) {
            var ctf=Server.Thread==null?null:Server.Thread.CTF;
            if(ctf==null || entity==null)return false;
            lock(ctf.sync)return ctf.Participant(entity.Owner) && OutsideCamp(entity);
        }
        internal void Move(GameState p) {
            lock(p.ItemSyncRoot)lock(sync) {
                Carry carry;
                if(!Participant(p)) { Drop(p);return; }
                if(carriers.TryGetValue(p.Entity.UID,out carry)) {
                    if(carry.Session!=p || clock()>=carry.Deadline) { Drop(p);return; }
                    if(!bases.Values.Any(b=>b.Guild==p.Guild.ID && Kernel.GetDistance(p.Entity.X,p.Entity.Y,b.Npc.X,b.Npc.Y)<=9))return;
                    if(!CtfResults.Deliver(run,carry.Token,p.Entity.UID,p.Guild.ID,clock()))return;
                    contributors.Add(p.Entity.UID);Drop(p);
                    if(p.Entity.MyAchievement!=null) {
                        p.Entity.MyAchievement.Invalidate();
                        p.Entity.MyAchievement.Refresh(DateTime.UtcNow,false);
                    }
                    p.Send("Your flag is planted. Your guild earns 10 points!");
                    var scores=CtfResults.Scores(run);
                    foreach(var member in Kernel.GamePool.Values.ToArray())
                        if(member!=null && member.Entity!=null && member.Entity.MapID==MapID && !member.Fake)
                            member.Send(p.Guild.Name+" planted a flag. Guild score: "+scores[p.Guild.ID]+".");
                    return;
                }
                foreach(var flag in map.StaticEntities.Values.ToArray()) {
                    if(flag.NpsEffect.Mesh!=StaticEntity.FlagMesh || !OutsideCamp(p.Entity)
                        || flag.X!=p.Entity.X || flag.Y!=p.Entity.Y)continue;
                    map.RemoveStaticItem(flag);p.RemoveScreenSpawn(flag,true);
                    carriers.Add(p.Entity.UID,new Carry{Token=Guid.NewGuid(),Deadline=clock().AddSeconds(60),Session=p});
                    p.Entity.FlagStamp=Time32.Now;p.Entity.AddFlag((ulong)FlagsUpdate.Flags.CarryingFlag,60,true);
                    p.Send("You have the flag! Reach your guild's base within 60 seconds.");return;
                }
            }
        }
        internal void Killed(GameState victim,GameState killer) {
            lock(sync) {
                Carry carry;
                if(victim==null || victim.Entity==null)return;
                bool held=carriers.TryGetValue(victim.Entity.UID,out carry);
                Drop(victim);
                if(!held || !ReferenceEquals(carry.Session,victim) || clock()>=carry.Deadline || !Participant(killer) || !OutsideCamp(killer.Entity)
                    || killer.Entity.GuildID==victim.Entity.GuildID || carriers.ContainsKey(killer.Entity.UID))return;
                carry.Session=killer;carriers[killer.Entity.UID]=carry;
                killer.Entity.FlagStamp=Time32.Now;
                killer.Entity.AddFlag((ulong)FlagsUpdate.Flags.CarryingFlag,(int)Math.Ceiling((carry.Deadline-clock()).TotalSeconds),true);
                killer.Send("You seized the fallen banner. Bring it to your base before time runs out!");
            }
        }
        private static void SetMode(GameState p,Enums.PKMode mode) {
            p.Entity.PKMode=mode;
            p.Send(new Data(true){UID=p.Entity.UID,ID=Data.ChangePKMode,dwParam=(uint)mode});
        }
        internal void Leave(GameState p) {
            lock(sync) {
                // Teleport calls Leave before updating MapID: never teleport recursively here.
                if(QuestInventory.Player(p,false))CtfResults.RequestReturn(p.Entity.UID);
                Drop(p);Tuple<GameState,Enums.PKMode> previous;
                if(p!=null && p.Entity!=null && modes.TryGetValue(p.Entity.UID,out previous) && previous.Item1==p) {
                    modes.Remove(p.Entity.UID);SetMode(p,previous.Item2);
                }
            }
        }
        private void Drop(GameState p) {
            if(p==null || p.Entity==null)return;
            Carry held;
            if(carriers.TryGetValue(p.Entity.UID,out held) && ReferenceEquals(held.Session,p))carriers.Remove(p.Entity.UID);
            p.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.CarryingFlag);
        }
        private void SpawnFlags() {
            int needed=40-map.StaticEntities.Values.Count(f=>f.NpsEffect.Mesh==StaticEntity.FlagMesh);
            for(int attempt=0;attempt<160 && needed>0;attempt++) {
                var xy=map.RandomCoordinates();
                if(Kernel.GetDistance(xy.Item1,xy.Item2,482,367)<=32
                    || !map.Floor[xy.Item1,xy.Item2,MapObjectType.Player]
                    || map.StaticEntities.Values.Any(f=>f.X==xy.Item1 && f.Y==xy.Item2))continue;
                var flag=new StaticEntity(nextFlag++,xy.Item1,xy.Item2,MapID);flag.DoFlag();map.AddStaticEntity(flag);needed--;
            }
        }
        private void ClearWorld() {
            foreach(var carry in carriers.Values.ToArray())Drop(carry.Session);
            foreach(var previous in modes.Values.ToArray())Leave(previous.Item1);
            foreach(var flag in map.StaticEntities.Values.ToArray())if(flag.NpsEffect.Mesh==StaticEntity.FlagMesh)map.RemoveStaticItem(flag);
            foreach(var b in bases.Values) { b.Guild=0;b.Damage.Clear();b.Npc.Name=b.Name;b.Npc.Hitpoints=b.Npc.MaxHitpoints;Kernel.SendScreen(b.Npc,b.Npc); }
        }
        private void Finish(DateTime utc) {
            var result=CtfResults.Finish(run,utc);
            if(result.Winner!=0)TitleRequirements.CtfGuild=result.Winner;
            running=false;needsCleanup=true;ClearWorld();
            ConquerStructures.Society.Guild guild;
            Broadcast(result.Winner!=0 && Kernel.Guilds.TryGetValue(result.Winner,out guild)
                ? guild.Name+" has won Capture the Flag! Its leader bears the Victor title."
                : "Capture the Flag has ended without a new champion.");
        }
        internal bool HandleNpc(GameState p,NpcRequest request) {
            if(p.ActiveNpc==380 && (request.OptionID==9 || request.OptionID==10 || request.OptionID==12)) {
                if(!QuestInventory.Near(p,380,1002))return true;
                if(request.OptionID==12) { p.Send(CtfResults.Claim(p));return true; }
                var guide=new Npcs(p);
                guide.Text(request.OptionID==9
                    ? "Our banners rise Saturday, 21:00 to 21:30 Pacific. Winning contributors earn a bound P7 soul. Their participating leader earns permanent Conqueror and Radiant Wings."
                    : "Capture a base, then carry flags home within 60 seconds. Each flag earns 10 points. Tied or uncontested battles award no prizes.");
                guide.Option("Join the battle.",11);guide.Option("Claim my victory prize.",12);
                if(request.OptionID==9)guide.Option("Tell me the rules.",10);
                guide.Option("Farewell.",255);guide.Send();return true;
            }
            if(p.ActiveNpc!=ReturnNpc)return false;
            INpc npc;
            if(p.Fake || p.Entity==null || p.Entity.MapID!=MapID || p.Action!=2 || p.Entity.PersistenceRequiresReload
                || !map.Npcs.TryGetValue(ReturnNpc,out npc) || Kernel.GetDistance(p.Entity.X,p.Entity.Y,npc.X,npc.Y)>6)return true;
            if(request.OptionID==1) { Leave(p);p.Entity.Teleport(1002,408,330); }
            else if(request.OptionID==0) {
                var dialog=new Npcs(p);dialog.Text("The banners call for brave hearts. Ready to return to Twin City?");
                dialog.Option("Take me home.",1);dialog.Option("I'll stay.",255);dialog.Send();
            }
            return true;
        }
    }
}
