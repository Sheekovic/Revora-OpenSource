using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Revora.Client;
using Revora.Network.GamePackets;
using E=Revora.Game.Features.ExorcismWorld;

namespace Revora.Game.Features
{
    internal static class ExorcismCombat
    {
        private sealed class Cast {internal int Phase;internal bool Preparing;internal uint Target;internal ushort X,Y;internal DateTime Due,Next;}
        private sealed class Stun {internal E.Hunt Hunt;internal DateTime Until;}
        private static readonly ConditionalWeakTable<E.Hunt,Cast> casts=new ConditionalWeakTable<E.Hunt,Cast>();
        private static readonly ConditionalWeakTable<GameState,Stun> stuns=new ConditionalWeakTable<GameState,Stun>();
        private static readonly ushort[] Spells={11321,11310,11309};
        private static readonly string[] Names={"SoaringLeap","BloodyBattle","SurgingShake"};
        private static readonly int[] Windup={2,6,2};
        internal static void Initialize(){
            var rows=File.ReadLines(Path.Combine(Constants.DMapsPath,"magictype.txt")).Select(s=>s.Split(new[]{"@@"},StringSplitOptions.None)).Where(s=>s.Length>8).ToArray();
            for(int n=0;n<Spells.Length;n++)if(rows.Count(r=>r[1]==Spells[n].ToString() && r[3]==Names[n] && r[8]=="0")!=1)throw new InvalidOperationException("Spook skill catalog changed.");
        }
        private static bool Live(E.Hunt h,Entity body){Entity current;E.Hunt registered;return h!=null && !h.Released && h.State=="active" && body!=null && !body.Dead
            && ReferenceEquals(h.Body,body) && ExorcismQuest.Clock()<h.Until && E.Maps.TryGetValue(h.Map.ID,out registered) && ReferenceEquals(registered,h)
            && h.Map.Entities.TryGetValue(body.UID,out current) && ReferenceEquals(current,body);}
        internal static bool IsStunned(GameState p){
            Stun s;if(p==null || !stuns.TryGetValue(p,out s))return false;
            lock(E.Gate)return ExorcismQuest.Clock()<s.Until && E.Eligible(s.Hunt,p,true) && Live(s.Hunt,s.Hunt.Body);
        }
        internal static bool Contains(int phase,int dx,int dy){long distance=(long)dx*dx+(long)dy*dy;return phase==0?distance<=4:phase==1?distance<=25:phase==2 && distance<=64;}
        internal static void Tick(E.Hunt h){
            lock(E.Gate){Cast c;if(!Live(h,h.Body) || !casts.TryGetValue(h,out c) || !c.Preparing || ExorcismQuest.Clock()<c.Due)return;}
            Execute(h.Body,null);
        }
        internal static bool Execute(Entity body,Entity target){
            if(!E.IsEncounter(body))return false;
            E.Hunt h;Cast c;int phase;uint victim;ushort x,y;
            lock(E.Gate){
                if(!E.Maps.TryGetValue(body.MapID,out h) || !Live(h,body))return true;
                c=casts.GetValue(h,_=>new Cast());var now=ExorcismQuest.Clock();if(now<c.Next)return true;
                if(!c.Preparing){
                    var p=target==null?null:target.Owner;
                    if(p==null || !ReferenceEquals(p.Entity,target) || !E.Eligible(h,p,true) || !p.Attackable
                        || (long)(target.X-body.X)*(target.X-body.X)+(long)(target.Y-body.Y)*(target.Y-body.Y)>400)return true;
                    c.Preparing=true;c.Target=target.UID;c.X=c.Phase==2?body.X:target.X;c.Y=c.Phase==2?body.Y:target.Y;c.Due=now.AddSeconds(Windup[c.Phase]);
                    body.Facing=Kernel.GetAngle(body.X,body.Y,target.X,target.Y);body.MonsterInfo.LastMove=Time32.Now.AddSeconds(Windup[c.Phase]);
                    string warning=c.Phase==0?"The Spook prepares Soaring Leap at ("+c.X+","+c.Y+").":c.Phase==1?"Bloody Battle gathers at ("+c.X+","+c.Y+"). Move clear!":"Surging Shake builds at ("+c.X+","+c.Y+"). Retreat!";
                    foreach(var member in h.Connections.Values)if(E.Eligible(h,member,false))member.Send(warning);return true;
                }
                if(now<c.Due)return true;
                phase=c.Phase;victim=c.Target;x=c.X;y=c.Y;c.Preparing=false;c.Phase=(phase+1)%3;c.Next=now.AddSeconds(4);
            }
            var spell=new SpellUse(true){Attacker=body.UID,SpellID=Spells[phase],X=x,Y=y};
            foreach(var p in h.Connections.Values.ToArray()){
                if(p.ItemSyncRoot==null || !Monitor.TryEnter(p.ItemSyncRoot))continue;
                try{lock(E.Gate){
                    if(!Live(h,body) || !E.Eligible(h,p,true) || !p.Attackable || phase==0 && p.Entity.UID!=victim || !Contains(phase,p.Entity.X-x,p.Entity.Y-y))continue;
                    var hit=new Attack(true){Attacker=body.UID,Attacked=p.Entity.UID,AttackType=Attack.Melee};
                    uint damage=Attacking.Calculate.Melee(body,p.Entity,ref hit);
                    if(phase==2)damage=(uint)Math.Min(uint.MaxValue,(ulong)damage*3/2);
                    WarriorModes.Counter(body,p.Entity,hit,damage,Time32.Now);
                    damage=Math.Min(damage,p.Entity.Hitpoints);
                    if(damage>=p.Entity.Hitpoints)p.Entity.Die(body.UID);else if(damage>0)p.Entity.Hitpoints-=damage;
                    if(phase==0 && !p.Entity.Dead){stuns.Remove(p);stuns.Add(p,new Stun{Hunt=h,Until=ExorcismQuest.Clock().AddSeconds(2)});
                        var update=new GameCharacterUpdates(true){UID=p.Entity.UID};update.Add(GameCharacterUpdates.Dizzy,0,2);p.SendScreen(update,true);}
                    spell.AddTarget(p.Entity.UID,damage,hit);
                }}finally{Monitor.Exit(p.ItemSyncRoot);}
            }
            lock(E.Gate)if(Live(h,body))body.MonsterInfo.SendScreen(spell);return true;
        }
    }
}
