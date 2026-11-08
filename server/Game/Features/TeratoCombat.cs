using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using E=Revora.Game.Features.TeratoEncounter;

namespace Revora.Game.Features
{
    internal static class TeratoCombat
    {
        private sealed class Cast {internal int Cycle,Dx,Dy;internal ushort X,Y;internal bool Preparing;internal DateTime Due,Next;}
        private static readonly ConditionalWeakTable<E.Actor,Cast> casts=new ConditionalWeakTable<E.Actor,Cast>();
        private static readonly ushort[] Spells={7014,7011,7013,7017,7015};
        private static readonly string[] Warnings={"Terato draws breath! Leave the path before its jaws.","Terato raises its tail! Keep away from its back.",
            "Flames gather beneath Terato! Move away from the dragon.","Misty flames encircle Terato! Move close or beyond their reach.","Terato is about to roar! Retreat before fear takes hold."};
        internal static bool Contains(int phase,int dx,int dy,int facingX,int facingY) {
            // Coordinate differences can span the full ushort range.
            long distance=(long)dx*dx+(long)dy*dy;
            if(phase==2)return distance<=36;
            if(phase==3)return distance>=9 && distance<=64;
            if(phase==4)return distance<=64;
            if(distance>64)return false;
            if(phase==1){facingX=-facingX;facingY=-facingY;}
            long dot=(long)dx*facingX+(long)dy*facingY,cross=(long)dx*facingY-(long)dy*facingX;
            return distance==0 || dot>0 && cross*cross<=dot*dot;
        }
        internal static void Tick(E.Actor actor) {
            GameState target=null;
            lock(E.Gate){Cast cast;if(actor.Released || !casts.TryGetValue(actor,out cast) || !cast.Preparing || E.Clock()<cast.Due)return;
                foreach(uint uid in actor.Party){GameState p;if(Kernel.GamePool.TryGetValue(uid,out p) && E.Eligible(actor,p,true)){target=p;break;}}}
            if(target!=null)Execute(actor.Body,target.Entity);
        }
        internal static bool Execute(Entity monster,Entity target) {
            if(!E.IsEncounter(monster))return false;
            E.Actor actor;Cast cast;int phase;ushort x,y;int facingX,facingY;
            lock(E.Gate) {
                if(!E.Actors.TryGetValue(monster.UID,out actor) || !ReferenceEquals(actor.Body,monster) || actor.Released || monster.Dead
                    || E.Clock()>=actor.Until || target==null || !E.Eligible(actor,target.Owner,true) || !ReferenceEquals(target.Owner.Entity,target))return true;
                cast=casts.GetValue(actor,_=>new Cast());if(E.Clock()<cast.Next)return true;
                if(!cast.Preparing) {
                    cast.Preparing=true;cast.Due=E.Clock().AddSeconds(2);cast.X=monster.X;cast.Y=monster.Y;
                    cast.Dx=Math.Sign(target.X-monster.X);cast.Dy=Math.Sign(target.Y-monster.Y);if(cast.Dx==0 && cast.Dy==0)cast.Dy=1;
                    monster.Facing=Kernel.GetAngle(monster.X,monster.Y,target.X,target.Y);monster.MonsterInfo.LastMove=Time32.Now.AddSeconds(2);
                    foreach(uint uid in actor.Party){GameState p;if(Kernel.GamePool.TryGetValue(uid,out p) && E.Eligible(actor,p,false))p.Send(Warnings[cast.Cycle]);}
                    return true;
                }
                if(E.Clock()<cast.Due)return true;
                phase=cast.Cycle;x=cast.X;y=cast.Y;facingX=cast.Dx;facingY=cast.Dy;
                cast.Preparing=false;cast.Cycle=(cast.Cycle+1)%Spells.Length;cast.Next=E.Clock().AddSeconds(4);
            }
            var spell=new SpellUse(true){Attacker=monster.UID,SpellID=Spells[phase],X=x,Y=y};
            foreach(uint uid in actor.Party) {
                GameState p;if(!Kernel.GamePool.TryGetValue(uid,out p) || p.ItemSyncRoot==null || !Monitor.TryEnter(p.ItemSyncRoot))continue;
                try{lock(E.Gate) {
                    if(actor.Released || monster.Dead || E.Clock()>=actor.Until || !E.Eligible(actor,p,true) || !p.Attackable
                        || !Contains(phase,p.Entity.X-x,p.Entity.Y-y,facingX,facingY))continue;
                    var hit=new Attack(true){Attacker=monster.UID,Attacked=p.Entity.UID,AttackType=Attack.Magic};uint damage=0;
                    if(phase!=4)damage=phase==1?Attacking.Calculate.Melee(monster,p.Entity,ref hit)
                        :Attacking.Calculate.Magic(monster,p.Entity,new SpellInformation{ID=Spells[phase],Level=0,Power=0},ref hit);
                    WarriorModes.Counter(monster,p.Entity,hit,damage,Time32.Now);
                    damage=Math.Min(damage,p.Entity.Hitpoints);
                    if(damage>=p.Entity.Hitpoints)p.Entity.Die(monster.UID);else if(damage>0)p.Entity.Hitpoints-=damage;
                    if(phase==4 && !p.Entity.Dead){p.FrightenStamp=Time32.Now;p.Entity.Fright=2;
                        var update=new GameCharacterUpdates(true){UID=p.Entity.UID};update.Add(GameCharacterUpdates.Dizzy,0,2);p.SendScreen(update,true);
                        p.Entity.AddFlag((ulong)FlagsUpdate.Flags.FreezeSmall,2,true);}
                    spell.AddTarget(p.Entity.UID,damage,hit);
                }}finally{Monitor.Exit(p.ItemSyncRoot);}
            }
            monster.MonsterInfo.SendScreen(spell);return true;
        }
    }
}
