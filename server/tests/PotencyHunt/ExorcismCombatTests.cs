using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
using Combat=Revora.Game.Attacking.Handle;

internal static partial class PotencyHuntTests
{
    static readonly Type SpookCombat=typeof(Entity).Assembly.GetType("Revora.Game.Features.ExorcismCombat");
    static bool SpookStunned(GameState p){return (bool)Call(SpookCombat,null,"IsStunned",p);}
    static void SpookCast(Entity boss,GameState target){new Combat(null,boss,target.Entity);}
    static uint SpookDamage(Entity boss,GameState target){var hit=new Attack(true){Attacker=boss.UID,Attacked=target.Entity.UID,AttackType=Attack.Melee};return Revora.Game.Attacking.Calculate.Melee(boss,target.Entity,ref hit);}
    static void VerifySpookCombat(Map market){
        foreach(var entry in new[]{new[]{0,2,0,1},new[]{0,2,1,0},new[]{1,3,4,1},new[]{1,4,4,0},new[]{2,8,0,1},new[]{2,8,1,0}})
            Check((bool)Call(SpookCombat,null,"Contains",entry[0],entry[1],entry[2])==(entry[3]==1),"Spook footprint boundary "+entry[0]+"/"+entry[1]+"/"+entry[2]);
        var leader=SpookPlayer(market);var armor=SpookPlayer(market);var far=SpookPlayer(market);var stranger=SpookPlayer(market);TeratoTeam(leader,armor,far);
        foreach(var p in new[]{leader,armor,far})SpookNpc(p,3);
        SpookNpc(leader,12);SpookNpc(armor,12);SpookNpc(far,12);EarthNpc(leader,3);var h=SpookHunt(leader);var boss=SpookBoss(leader);
        stranger.BindWorldMap(leader.Map.ID); // Deliberate unadmitted occupant: private damage must still reject it.
        foreach(var p in new[]{leader,armor,far,stranger}){p.Attackable=true;p.Entity.MaxHitpoints=p.Entity.Hitpoints=1000000;p.Entity.Defence=0;p.Entity.X=(ushort)(boss.X+5);p.Entity.Y=boss.Y;}
        boss.MinAttack=boss.MaxAttack=20000;armor.Entity.Defence=60000;far.Entity.X=(ushort)(boss.X+12);
        uint baseDamage=SpookDamage(boss,leader);Check(baseDamage>0,"Spook uses a real nonzero common monster damage calculation");
        SpookCast(boss,leader);Check(leader.Entity.Hitpoints==1000000 && !SpookStunned(leader),"Soaring Leap warning causes no early damage or stun");
        spookNow=spookNow.AddSeconds(1);SpookCast(boss,leader);Check(leader.Entity.Hitpoints==1000000,"Soaring Leap cannot resolve before its two-second warning");
        spookNow=spookNow.AddSeconds(1);Call(SpookCombat,null,"Tick",h);
        Check(leader.Entity.Hitpoints==1000000-baseDamage && SpookStunned(leader) && armor.Entity.Hitpoints==1000000 && stranger.Entity.Hitpoints==1000000,"Leap hits and stuns only its marked admitted player, using normal damage");
        ushort x=leader.Entity.X,y=leader.Entity.Y;uint hp=boss.Hitpoints;
        GeneralData.PlayerGroundMovment(new GroundMovement(true){UID=leader.Entity.UID,Direction=Enums.ConquerAngle.East,GroundMovementType=GroundMovement.Walk},leader);
        GeneralData.PlayerJump(new Data(true){UID=leader.Entity.UID,ID=Data.Jump,wParam1=x,wParam2=y,Xt1=(ushort)(x+1),Yt1=y,TimeStamp=Time32.Now},leader);
        leader.Entity.Teleport(1013,leader.Map.ID,(ushort)(x+1),y);
        uint lethal=hp;Combat.ReceiveAttack(leader.Entity,boss,new Attack(true){Attacker=leader.Entity.UID,Attacked=boss.UID,AttackType=Attack.Melee},ref lethal,null);
        Check(leader.Entity.X==x && leader.Entity.Y==y && !Combat.CanAttack(leader.Entity,boss,null,true) && lethal==0 && boss.Hitpoints==hp,"owned two-second stun blocks walking, jumping, short teleport and direct attack application");
        uint health=leader.Entity.Hitpoints;Parallel.For(0,12,n=>SpookCast(boss,leader));Check(leader.Entity.Hitpoints==health,"concurrent callbacks cannot repeat a resolved Leap");
        spookNow=spookNow.AddSeconds(2);Check(!SpookStunned(leader) && Combat.CanAttack(leader.Entity,boss,null,true),"stun expires independently of the generic flag timer");
        SkyJump(leader,(ushort)(x+1),y);Check(leader.Entity.X==x+1,"ordinary jump resumes when the owned stun expires");leader.Entity.X=x;
        spookNow=spookNow.AddSeconds(2);SpookCast(boss,leader);uint protectedHp=armor.Entity.Hitpoints;
        spookNow=spookNow.AddSeconds(5);Call(SpookCombat,null,"Tick",h);Check(leader.Entity.Hitpoints==health && armor.Entity.Hitpoints==protectedHp,"Bloody Battle gives all six seconds of warning");
        spookNow=spookNow.AddSeconds(1);Call(SpookCombat,null,"Tick",h);
        Check(health-leader.Entity.Hitpoints==baseDamage && protectedHp-armor.Entity.Hitpoints<baseDamage && armor.Entity.Hitpoints<protectedHp && far.Entity.Hitpoints==1000000 && stranger.Entity.Hitpoints==1000000,"Bloody Battle respects physical defense and excludes outside-area or unadmitted players");
        health=leader.Entity.Hitpoints;spookNow=spookNow.AddSeconds(4);SpookCast(boss,leader);armor.Entity.X=(ushort)(boss.X+10);
        spookNow=spookNow.AddSeconds(2);Call(SpookCombat,null,"Tick",h);
        Check(health-leader.Entity.Hitpoints==baseDamage*3/2 && far.Entity.Hitpoints==1000000 && !SpookStunned(armor),"Surging Shake applies the stronger post-defense hit around the boss");
        health=leader.Entity.Hitpoints;spookNow=spookNow.AddSeconds(4);SpookCast(boss,leader);leader.Entity.X=(ushort)(leader.Entity.X+3);
        spookNow=spookNow.AddSeconds(2);Call(SpookCombat,null,"Tick",h);Check(leader.Entity.Hitpoints==health && !SpookStunned(leader),"leaving the marked Leap location evades its damage and stun");
        spookNow=spookNow.AddSeconds(4);SpookCast(boss,leader);leader.Entity.X=(ushort)(boss.X+18);armor.Entity.X=leader.Entity.X;spookNow=spookNow.AddSeconds(6);Call(SpookWorld,null,"Tick");
        Check(leader.Entity.Hitpoints==health,"world tick resolves a warned area at its original location after players retreat beyond melee reach");
        ClearSpooks();stranger.BindWorldMap(1036);
        foreach(string invalid in new[]{"immunity","departed","replaced","counterfeit","expired","released","dead-boss","busy"}){
            var p=SpookPlayer(market);SpookBegin(p,1);boss=SpookBoss(p);h=SpookHunt(p);p.Entity.X=(ushort)(boss.X+4);p.Entity.Y=boss.Y;p.Attackable=true;p.Entity.MaxHitpoints=p.Entity.Hitpoints=200000;
            SpookCast(boss,p);uint before=p.Entity.Hitpoints;
            if(invalid=="immunity")p.Attackable=false;if(invalid=="departed")p.BindWorldMap(1036);if(invalid=="replaced")Kernel.GamePool[p.Entity.UID]=new GameState(null);
            if(invalid=="counterfeit"){var fake=new Entity(EntityFlag.Monster,false){UID=boss.UID,MapID=boss.MapID,MonsterInfo=boss.MonsterInfo.Copy(),Hitpoints=100};fake.MonsterInfo.Owner=fake;p.Map.Entities[boss.UID]=fake;}
            if(invalid=="expired")spookNow=(DateTime)DragonField(h,"Until");if(invalid=="released")Call(SpookWorld,null,"Release",h);if(invalid=="dead-boss")boss.Hitpoints=0;
            var ready=new ManualResetEventSlim();var release=new ManualResetEventSlim();Task holder=null;
            if(invalid=="busy"){holder=Task.Run(()=>{lock(p.ItemSyncRoot){ready.Set();release.Wait();}});ready.Wait();}
            try{spookNow=spookNow.AddSeconds(2);Call(SpookCombat,null,"Tick",h);}finally{if(holder!=null){release.Set();holder.Wait();}ready.Dispose();release.Dispose();}
            Check(p.Entity.Hitpoints==before && !SpookStunned(p),"pending Spook attack cannot affect "+invalid+" actor/session");ClearSpooks();
        }
        var solo=SpookPlayer(market);SpookBegin(solo,2);boss=SpookBoss(solo);h=SpookHunt(solo);solo.Attackable=true;solo.Entity.X=(ushort)(boss.X+4);solo.Entity.Y=boss.Y;solo.Entity.Hitpoints=1;
        SpookCast(boss,solo);spookNow=spookNow.AddSeconds(2);Call(SpookCombat,null,"Tick",h);Check(solo.Entity.Dead && solo.Entity.Hitpoints==0 && !SpookStunned(solo),"lethal named attack uses normal player death and does not leave a corpse stunned");ClearSpooks();
        solo=SpookPlayer(market);SpookBegin(solo,1);boss=SpookBoss(solo);h=SpookHunt(solo);solo.Attackable=true;solo.Entity.X=(ushort)(boss.X+4);solo.Entity.Y=boss.Y;solo.Entity.MaxHitpoints=solo.Entity.Hitpoints=200000;
        SpookCast(boss,solo);spookNow=spookNow.AddSeconds(2);Call(SpookCombat,null,"Tick",h);Check(SpookStunned(solo),"new owned hunt creates its own temporary stun");
        Call(SpookWorld,null,"Release",h);Check(solo.Entity.MapID==1036 && !SpookStunned(solo),"instance cleanup ends its stun authority when the player returns to Market");
        solo=SpookPlayer(market);SpookBegin(solo,1);boss=SpookBoss(solo);h=SpookHunt(solo);solo.Attackable=true;solo.Entity.X=(ushort)(boss.X+21);solo.Entity.Y=boss.Y;solo.Entity.MaxHitpoints=solo.Entity.Hitpoints=200000;
        SpookCast(boss,solo);spookNow=spookNow.AddSeconds(10);Call(SpookCombat,null,"Tick",h);Check(solo.Entity.Hitpoints==200000,"Spook cannot begin a ranged cast beyond twenty tiles");
        solo.Entity.X=(ushort)(boss.X+4);SpookCast(boss,solo);solo.Entity.X=(ushort)(boss.X+1);boss.Die(solo.Entity);spookNow=spookNow.AddSeconds(2);Call(SpookCombat,null,"Tick",h);
        Check(solo.Entity.Hitpoints==200000 && SpookDays(solo,"state='victory'")==1,"boss victory cancels its pending cast without damaging the winner");ClearSpooks();
        foreach(bool kill in new[]{false,true}){
            solo=SpookPlayer(market);solo.Entity.Class=25;EntityTable.SaveEntity(solo);RebirthSkill(solo,12680);WarriorFists(solo);SpookBegin(solo,1);
            boss=SpookBoss(solo);h=SpookHunt(solo);solo.Attackable=true;solo.Entity.MaxHitpoints=solo.Entity.Hitpoints=200000;solo.Entity.Stamina=0;
            solo.Entity.X=(ushort)(boss.X+4);solo.Entity.Y=boss.Y;if(kill)boss.Hitpoints=50000;uint bossHp=boss.Hitpoints;WarriorCast(solo,12680);
            SpookCast(boss,solo);spookNow=spookNow.AddSeconds(2);Call(SpookCombat,null,"Tick",h);
            Check(!Fenced(solo) && !solo.Entity.OnBackFire() && solo.Entity.Stamina==30 && solo.Entity.Hitpoints<200000
                && (kill?boss.Dead && SpookDays(solo,"state='victory'")==1:boss.Hitpoints==bossHp-50000),"Spook named hit triggers one counter and preserves owned victory: lethal="+kill);
            ClearSpooks();
        }
    }
}
