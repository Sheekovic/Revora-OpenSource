using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using E=Revora.Game.Features.SnowBansheeEncounter;
using S=Revora.Game.Features.SnowBansheeStore;

namespace Revora.Game.Features
{
    internal static class SnowBansheeCombat
    {
        internal const int WarningSeconds=2,RecoverySeconds=4,HailPulses=6,HailIntervalSeconds=1,HealDivisor=50;
        private sealed class Cast {internal int Phase,Hail;internal uint Target;internal ushort X,Y;internal double Angle;internal bool Preparing;internal DateTime Due,Next;}
        private sealed class Freeze {internal E.Actor Actor;internal DateTime Until;}
        private static readonly ConditionalWeakTable<E.Actor,Cast> casts=new ConditionalWeakTable<E.Actor,Cast>();
        private static readonly ConditionalWeakTable<GameState,Freeze> freezes=new ConditionalWeakTable<GameState,Freeze>();
        private static readonly Dictionary<E.Actor,HashSet<GameState>> frozen=new Dictionary<E.Actor,HashSet<GameState>>();
        private static readonly ushort[] Spells={30010,30014,30013,10373,30011,30012,10372};
        private static readonly string[] Names={"IceBolt","FrostClaw","ArrowHail","IceBurst","IceShock","Avalanche","Metaplasia"};
        internal static void Initialize() {
            var rows=File.ReadLines(Path.Combine(Constants.DMapsPath,"magictype.txt")).Select(s=>s.Split(new[]{"@@"},StringSplitOptions.None)).Where(s=>s.Length>8).ToArray();
            for(int n=0;n<Spells.Length;n++)if(rows.Count(r=>r[1]==Spells[n].ToString() && r[3]==Names[n] && r[8]=="0")!=1)
                throw new InvalidOperationException("Snow Banshee skill catalog changed.");
        }
        internal static bool IsStunned(GameState p) {
            Freeze f;if(p==null || !freezes.TryGetValue(p,out f))return false;
            lock(E.Gate)return S.Clock()<f.Until && E.Eligible(f.Actor,p,true);
        }
        private static void Unfreeze(E.Actor a,GameState p) {
            Freeze f;if(!freezes.TryGetValue(p,out f) || !ReferenceEquals(f.Actor,a))return;
            freezes.Remove(p);
            // Never remove a foreign feature's Freeze flag or its visual state.
            if(QuestInventory.Player(p,false) && !p.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Freeze)) {
                var update=new GameCharacterUpdates(true){UID=p.Entity.UID};update.Add(GameCharacterUpdates.Freeze,0,0);p.SendScreen(update,true);
            }
        }
        internal static void Clear(E.Actor a) {
            if(!Monitor.IsEntered(E.Gate))throw new InvalidOperationException("Banshee effects require lifecycle ownership.");
            casts.Remove(a);HashSet<GameState> players;if(!frozen.TryGetValue(a,out players))return;
            foreach(var p in players)Unfreeze(a,p);frozen.Remove(a);
        }
        internal static bool Contains(int phase,int dx,int dy,double angle) {
            long distance=(long)dx*dx+(long)dy*dy;
            if(phase==0)return distance<=361;if(phase==1)return distance<=9;
            if(phase==3)return distance<=36;if(phase==4)return distance<=16;
            if(phase!=2 && phase!=5 || distance>324)return false;
            if(distance==0)return true;
            double difference=Math.Atan2(Math.Sin(Math.Atan2(dy,dx)-angle),Math.Cos(Math.Atan2(dy,dx)-angle));
            return Math.Abs(difference)<=Math.PI/6+1e-10;
        }
        internal static void Tick(E.Actor a) {
            lock(E.Gate) {
                if(!E.Current(a))return;HashSet<GameState> players;
                if(frozen.TryGetValue(a,out players))foreach(var p in players.ToArray())if(!IsStunned(p)){Unfreeze(a,p);players.Remove(p);}
                var target=Kernel.GamePool.Values.Where(p=>E.Eligible(a,p,true) && p.Attackable)
                    .OrderBy(p=>(long)(p.Entity.X-a.Body.X)*(p.Entity.X-a.Body.X)+(long)(p.Entity.Y-a.Body.Y)*(p.Entity.Y-a.Body.Y)).FirstOrDefault();
                Execute(a.Body,target==null?null:target.Entity);
            }
        }
        internal static bool Execute(Entity body,Entity target) {
            if(!E.IsEncounter(body))return false;
            lock(E.Gate) {
                var a=E.Active;if(!E.Current(a) || !ReferenceEquals(a.Body,body))return true;
                var c=casts.GetValue(a,_=>new Cast());var now=S.Clock();if(now<c.Next)return true;
                if(!c.Preparing) {
                    if(target==null || target.Owner==null || !ReferenceEquals(target.Owner.Entity,target) || !E.Eligible(a,target.Owner,true) || !target.Owner.Attackable)return true;
                    c.Target=target.UID;c.X=c.Phase==4?target.X:body.X;c.Y=c.Phase==4?target.Y:body.Y;
                    c.Angle=Math.Atan2((int)target.Y-body.Y,(int)target.X-body.X);c.Hail=0;c.Preparing=true;c.Due=now.AddSeconds(WarningSeconds);
                    body.Facing=Kernel.GetAngle(body.X,body.Y,target.X,target.Y);
                    string warning=c.Phase==2?"Arrows gather before the Banshee. Circle away from the turning hail!":c.Phase==3?"Ice cracks around the Banshee. Step away!":c.Phase==4?"Ice gathers at ("+c.X+","+c.Y+"). Move clear!":c.Phase==5?"An avalanche gathers before the Banshee. Flank her!":c.Phase==6?"The Banshee draws strength from the frost.":c.Phase==1?"The Banshee raises her frozen claw.":"The Banshee readies an icy bolt.";
                    foreach(var p in Kernel.GamePool.Values)if(E.Eligible(a,p,false))p.Send(warning);return true;
                }
                if(now<c.Due)return true;int phase=c.Phase;double heading=c.Angle+c.Hail*Math.PI/18;
                var spell=new SpellUse(true){Attacker=body.UID,SpellID=Spells[phase],X=c.X,Y=c.Y};
                if(phase==6) {uint heal=Math.Min(body.MaxHitpoints-body.Hitpoints,body.MaxHitpoints/HealDivisor);body.Hitpoints+=heal;spell.AddTarget(body.UID,heal,null);}
                else foreach(var p in Kernel.GamePool.Values.ToArray()) {
                    if(p==null || p.ItemSyncRoot==null || !Monitor.TryEnter(p.ItemSyncRoot))continue;
                    try {
                        if(!E.Current(a) || !E.Eligible(a,p,true) || !p.Attackable || phase<2 && p.Entity.UID!=c.Target
                            || !Contains(phase,p.Entity.X-c.X,p.Entity.Y-c.Y,heading))continue;
                        var hit=new Attack(true){Attacker=body.UID,Attacked=p.Entity.UID,AttackType=Attack.Magic};
                        uint damage=phase==1 || phase==5?Attacking.Calculate.Melee(body,p.Entity,ref hit):Attacking.Calculate.Magic(body,p.Entity,new SpellInformation{ID=Spells[phase],Level=0,Power=0},ref hit);
                        WarriorModes.Counter(body,p.Entity,hit,damage,Time32.Now);if(!E.Current(a))break;
                        damage=Math.Min(damage,p.Entity.Hitpoints);if(damage>=p.Entity.Hitpoints)p.Entity.Die(body.UID);else if(damage>0)p.Entity.Hitpoints-=damage;
                        if((phase==3 || phase==4) && !p.Entity.Dead) {
                            int seconds=phase==3?5:3;Freeze old;var until=now.AddSeconds(seconds);
                            if(freezes.TryGetValue(p,out old) && ReferenceEquals(old.Actor,a) && old.Until>until)until=old.Until;
                            freezes.Remove(p);freezes.Add(p,new Freeze{Actor=a,Until=until});HashSet<GameState> players;
                            if(!frozen.TryGetValue(a,out players)){players=new HashSet<GameState>();frozen.Add(a,players);}players.Add(p);
                            var update=new GameCharacterUpdates(true){UID=p.Entity.UID};update.Add(GameCharacterUpdates.Freeze,0,(uint)seconds);p.SendScreen(update,true);
                        }
                        spell.AddTarget(p.Entity.UID,damage,hit);
                    }finally{Monitor.Exit(p.ItemSyncRoot);}
                }
                if(E.Current(a))body.MonsterInfo.SendScreen(spell);
                if(phase==2 && ++c.Hail<HailPulses)c.Due=now.AddSeconds(HailIntervalSeconds);
                else {c.Preparing=false;c.Phase=(phase+1)%7;c.Next=now.AddSeconds(RecoverySeconds);}return true;
            }
        }
    }
}
