using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Game;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static void VerifyPlanetCombat(GameState p,GameState outsider,Entity[] guards)
    {
        var mob=guards[0];uint hp=mob.Hitpoints,damage=100;
        var hit=new Attack(true){Attacker=outsider.Entity.UID,Attacked=mob.UID,AttackType=Attack.Melee};
        Revora.Game.Attacking.Handle.ReceiveAttack(outsider.Entity,mob,hit,ref damage,null);
        Check(damage==0 && mob.Hitpoints==hp,"direct damage application rejects outsiders before changing a Planet guard's HP");
        var fake=new Entity(EntityFlag.Monster,false){UID=mob.UID,MapID=mob.MapID,X=mob.X,Y=mob.Y,
            MonsterInfo=mob.MonsterInfo.Copy(),Hitpoints=100,MaxHitpoints=100,MinAttack=1200,MaxAttack=1200,Level=mob.Level};fake.MonsterInfo.Owner=fake;
        p.Attackable=true;uint before=p.Entity.Hitpoints;
        new Revora.Game.Attacking.Handle(null,fake,p.Entity);
        Check(p.Entity.Hitpoints==before,"copied Planet monster identity cannot bypass object ownership through direct execution");
        using(var entered=new ManualResetEventSlim(false))using(var release=new ManualResetEventSlim(false)) {
            var holder=Task.Run(()=>{lock(p.ItemSyncRoot){entered.Set();if(!release.Wait(5000))throw new Exception("Planet combat lock fixture timed out.");}});
            try {
                if(!entered.Wait(2000))throw new Exception("Planet combat fixture failed to acquire owner lock.");
                new Revora.Game.Attacking.Handle(null,mob,p.Entity);damage=100;
                Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,mob,hit,ref damage,null);
                Check(p.Entity.Hitpoints==before && mob.Hitpoints==hp && damage==0,
                    "contended owner transition prevents both incoming and outgoing Planet damage");
            }finally{release.Set();holder.GetAwaiter().GetResult();}
        }
        var clock=planetNow;planetNow=planetNow.AddMinutes(16);
        try {
            new Revora.Game.Attacking.Handle(null,mob,p.Entity);damage=100;
            Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,mob,hit,ref damage,null);
            Check(p.Entity.Hitpoints==before && mob.Hitpoints==hp && damage==0,
                "expired Planet encounter rejects direct damage before its cleanup tick");
        }finally{planetNow=clock;}
        VerifyPlanetMonsterAi(p,guards);
    }
    static void VerifyPlanetMonsterAi(GameState p,Entity[] guards)
    {
        var mob=guards[0];var positions=guards.Select(g=>Tuple.Create(g.X,g.Y,g.MonsterInfo.LastMove,g.MonsterInfo.InSight)).ToArray();
        uint hp=p.Entity.Hitpoints,max=p.Entity.MaxHitpoints;ushort x=p.Entity.X,y=p.Entity.Y;bool alive=p.Socket.Alive,attackable=p.Attackable;
        var wrapper=typeof(Revora.Network.Sockets.ClientWrapper);object queue=wrapper.GetField("SendQueue",Instance).GetValue(p.Socket),sync=wrapper.GetField("SendSyncRoot",Instance).GetValue(p.Socket);
        var sent=new Queue<byte[]>();wrapper.GetField("SendQueue",Instance).SetValue(p.Socket,sent);wrapper.GetField("SendSyncRoot",Instance).SetValue(p.Socket,new object());
        try {
            p.Socket.Alive=true;p.Attackable=true;p.Entity.MaxHitpoints=p.Entity.Hitpoints=100000;
            p.Entity.X=48;p.Entity.Y=41;
            foreach(var g in guards){p.Screen.Add(g);g.MonsterInfo.LastMove=Time32.Now.AddSeconds(3600);g.MonsterInfo.InSight=0;}
            mob.MonsterInfo.LastMove=Time32.Now.AddSeconds(-5);
            Call(typeof(Screen),null,"aliveMonstersCallback",p,Time32.Now.GetHashCode());
            Check(mob.MonsterInfo.InSight==p.Entity.UID,"normal screen AI acquires the admitted Planet hunter");
            short initial=Kernel.GetDistance(mob.X,mob.Y,p.Entity.X,p.Entity.Y);
            for(int i=0;i<20 && Kernel.GetDistance(mob.X,mob.Y,p.Entity.X,p.Entity.Y)>1;i++) {
                mob.MonsterInfo.LastMove=Time32.Now.AddSeconds(-5);Call(typeof(Screen),null,"aliveMonstersCallback",p,Time32.Now.GetHashCode());
            }
            Check(initial>1 && Kernel.GetDistance(mob.X,mob.Y,p.Entity.X,p.Entity.Y)<=1,
                "normal screen AI moves the owned sentry into melee range on the loaded arena floor");
            uint before=p.Entity.Hitpoints;mob.MonsterInfo.LastMove=Time32.Now.AddSeconds(-5);
            Call(typeof(Screen),null,"aliveMonstersCallback",p,Time32.Now.GetHashCode());
            Check(p.Entity.Hitpoints<before && !p.Entity.Dead,"normal screen AI deals actual melee damage to an attackable admitted player");
            System.Console.WriteLine("Planet unarmored fixture sentry melee damage: "+(before-p.Entity.Hitpoints));
            before=p.Entity.Hitpoints;mob.MonsterInfo.LastMove=Time32.Now;
            Call(typeof(Screen),null,"aliveMonstersCallback",p,mob.MonsterInfo.LastMove.GetHashCode());
            Check(p.Entity.Hitpoints==before,"Planet sentry respects the existing attack cooldown between screen callbacks");
            p.Entity.AddFlag((ulong)FlagsUpdate.Flags.Fly,60,true);mob.MonsterInfo.InSight=0;mob.MonsterInfo.LastMove=Time32.Now.AddSeconds(-5);
            Call(typeof(Screen),null,"aliveMonstersCallback",p,Time32.Now.GetHashCode());
            Check(mob.MonsterInfo.InSight==0 && p.Entity.Hitpoints==before,"melee-only Planet sentry does not acquire a flying player");p.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.Fly);
            p.Entity.AddFlag((ulong)FlagsUpdate.Flags.Invisibility,60,true);mob.MonsterInfo.InSight=0;mob.MonsterInfo.LastMove=Time32.Now.AddSeconds(-5);
            Call(typeof(Screen),null,"aliveMonstersCallback",p,Time32.Now.GetHashCode());
            Check(mob.MonsterInfo.InSight==0 && p.Entity.Hitpoints==before,"Planet sentry does not acquire an invisible player");p.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.Invisibility);
            Check(sent.Count>0 && sent.Count<500,"combat fixture captures a bounded set of real outgoing screen packets without network access");
        }finally {
            p.Socket.Alive=false;p.Attackable=attackable;p.Entity.Hitpoints=hp;p.Entity.MaxHitpoints=max;p.Entity.X=x;p.Entity.Y=y;
            p.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.Fly);p.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.Invisibility);
            for(int i=0;i<guards.Length;i++) {
                var g=guards[i];p.Map.RemoveEntity(g);g.X=positions[i].Item1;g.Y=positions[i].Item2;g.MonsterInfo.LastMove=positions[i].Item3;g.MonsterInfo.InSight=positions[i].Item4;p.Map.AddEntity2(g);
            }
            wrapper.GetField("SendQueue",Instance).SetValue(p.Socket,queue);wrapper.GetField("SendSyncRoot",Instance).SetValue(p.Socket,sync);p.Socket.Alive=alive;
        }
    }
    static void PlanetCombatDefeat(GameState p,Entity guard)
    {
        uint min=p.Entity.MinAttack,max=p.Entity.MaxAttack;ushort x=p.Entity.X,y=p.Entity.Y;
        try {
            // Controlled offensive profile: exercise real calculation/application, not balance claims.
            p.Entity.MinAttack=p.Entity.MaxAttack=20000;
            if(!p.Map.Floor[guard.X,guard.Y-1,MapObjectType.Player])throw new Exception("Planet combat fixture melee landing is blocked.");
            p.Entity.X=guard.X;p.Entity.Y=(ushort)(guard.Y-1);
            for(int i=0;i<100 && !guard.Dead;i++) {
                var hit=new Attack(true){Attacker=p.Entity.UID,Attacked=guard.UID,AttackType=Attack.Melee};
                if(!Revora.Game.Attacking.Handle.CanAttack(p.Entity,guard,null,true))throw new Exception("Planet earned combat target was rejected.");
                uint damage=Revora.Game.Attacking.Calculate.Melee(p.Entity,guard,ref hit);
                Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,guard,hit,ref damage,null);
            }
            Check(guard.Dead,"calculated player melee damage earns the owned guard defeat: "+guard.Name);
        }finally{p.Entity.MinAttack=min;p.Entity.MaxAttack=max;p.Entity.X=x;p.Entity.Y=y;}
    }
}
