using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static readonly Type WarriorState=typeof(Entity).Assembly.GetType("Revora.Game.Features.WarriorModes");
    static readonly ushort[] warriorNewArts={10470,11160,11200,12660,12670,12680,12690,12700,12770};
    static readonly FlagsUpdate.Flags[] warriorFlags={FlagsUpdate.Flags.ManiacDance,FlagsUpdate.Flags.BackFire,FlagsUpdate.Flags.ShieldBlock,FlagsUpdate.Flags.MagicDefender,FlagsUpdate.Flags.DefensiveStance,FlagsUpdate.Flags.StarOfAccuracy,FlagsUpdate.Flags.MagicShield,FlagsUpdate.Flags.Superman,FlagsUpdate.Flags.AuraAzul};
    static void WarriorInstructorMap(string runtime){
        var map=World(runtime,1004);Call(WarriorTrainer,null,"Initialize");
        using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new Npgsql.NpgsqlCommand("SELECT cellx,celly,lookface FROM public.npcs WHERE id=10001 AND mapid=1004",c))
        using(var r=q.ExecuteReader()){if(!r.Read())throw new Exception("Warrior God missing.");map.AddNpc(new NpcSpawn{UID=10001,MapID=1004,X=Convert.ToUInt16(r[0]),Y=Convert.ToUInt16(r[1]),Mesh=Convert.ToUInt16(r[2]),Type=Enums.NpcType.Talker});}}
    }
    static void LearnWarriorArts(GameState p){
        var npc=Kernel.Maps[1004].Npcs[10001];p.Entity.Teleport(1004,(ushort)(npc.X-2),npc.Y);p.ActiveNpc=10001;EntityTable.SaveEntity(p);
        foreach(byte option in new byte[]{6,9,10,11,20,21,22,23,24})SoulWire(p,option,10001);
        if(Fenced(p) || warriorNewArts.Any(id=>!p.Spells.ContainsKey(id)))throw new Exception("Warrior rite training failed.");
        foreach(var id in warriorNewArts){byte level=(byte)(id==11160?1:id==12660 || id==12700?0:2);var s=p.Spells[id];s.Level=level;s.PreviousLevel=(byte)(level==2?3:level);s.Experience=51;s.Souls=(Spell.Soul_Level)1;}
        SkillTable.SaveSpells(p);
    }
    static void WarriorActive(GameState p){
        foreach(var flag in warriorFlags)p.Entity.AddFlag((ulong)flag,StatusFlagsBigVector32.PermanentFlag,false);
        var e=p.Entity;e.BackFireTime=10;e.Backfire=true;e.ManiacDanceTime=15;e.KOSpell=12700;e.KOCount=7;e.XPCountTwist=80;
        e.BlockShieldCheck=e.IsShieldBlock=e.MagicDefenderOwner=e.HasMagicDefender=e.IsDefensiveStance=true;
        e.BlockShield=30;e.ShieldBlockPercent=10;e.MagicDefender=22;e.MagicDefenderSecs=22;e.DefensiveStance=120;e.AccuracyTime=e.ShieldTime=e.StarOfAccuracyTime=e.MagicShieldTime=30;
        e.SupermanTime=20;e.ShieldIncrease=5;e.MagicShieldIncrease=3;
    }
    static bool WarriorInactive(GameState p){var e=p.Entity;return warriorFlags.All(f=>!e.ContainsFlag((ulong)f)) && !e.Backfire && e.BackFireTime==0 && e.ManiacDanceTime==0
        && e.XPCountTwist==0 && e.KOSpell==0 && e.KOCount==0 && !e.BlockShieldCheck && !e.IsShieldBlock && e.BlockShield==0 && e.ShieldBlockPercent==0
        && !e.MagicDefenderOwner && !e.HasMagicDefender && e.MagicDefender==0 && e.MagicDefenderSecs==0 && !e.IsDefensiveStance && e.DefensiveStance==0 && e.AccuracyTime==0 && e.ShieldTime==0
        && e.SupermanTime==0 && e.StarOfAccuracyTime==0 && e.MagicShieldTime==0 && e.ShieldIncrease==0 && e.MagicShieldIncrease==0;}
    static void VerifyWarriorRebirth(string runtime,Map town){
        VerifyPaidReincarnation(runtime,town);WarriorInstructorMap(runtime);
        foreach(int rite in new[]{0,1,2})foreach(bool keep in new[]{false,true})foreach(bool fail in new[]{false,true}){
            var p=rite==0?RebirthPlayer(town,25):rite==1?SecondPlayer(town,25,25):PaidPlayer(town,21);if(rite==2)p.Entity.Class=25;
            LearnWarriorArts(p);Check(WarriorReceipts(p)==9,"ordinary Warrior learns nine available newer arts before rite "+rite);
            p.Entity.Teleport(1002,town.Npcs[59558].X,town.Npcs[59558].Y);p.ActiveNpc=59558;EntityTable.SaveEntity(p);WarriorActive(p);
            byte next=keep?(byte)21:(byte)11;if(rite==0)RebirthOffer(p,next);else if(rite==1)SecondOffer(p,next);else PaidOffer(p);
            if(fail)Query("CREATE FUNCTION reject_warrior_rite() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER reject_warrior_rite BEFORE UPDATE ON entities FOR EACH ROW EXECUTE FUNCTION reject_warrior_rite()");
            try{if(rite==0)StoneNpc(p,214);else if(rite==1)StoneNpc(p,180);else PaidRequest(p,next);}finally{if(fail)Query("DROP TRIGGER reject_warrior_rite ON entities;DROP FUNCTION reject_warrior_rite()");}
            if(fail){Check(Fenced(p) && warriorNewArts.All(id=>p.Spells.ContainsKey(id) && p.Spells[id].Experience==51) && p.Spells[12680].PreviousLevel==3
                && warriorFlags.All(f=>p.Entity.ContainsFlag((ulong)f)) && p.Entity.BackFireTime==10 && p.Entity.ManiacDanceTime==15 && p.Entity.BlockShieldCheck && p.Entity.MagicDefenderOwner,"failed Warrior rite preserves learned metadata and active state: "+rite);}
            else{
                Check(!Fenced(p) && p.Entity.Class==next && WarriorInactive(p),"committed Warrior rite retires only the old life's modes: "+rite);
                p=ReloadBall(p);Check(warriorNewArts.All(id=>keep?p.Spells.ContainsKey(id) && p.Spells[id].Level==0 && p.Spells[id].Experience==0 && p.Spells[id].PreviousLevel==(id==12680 || id==12660 || id==12700?0:id==11160?1:3) && (int)p.Spells[id].Souls==1:!p.Spells.ContainsKey(id)),"cold reload applies Warrior school retention and Backfire's previous-level exception: "+rite+" keep="+keep);
                if(keep){var before=p.Spells[12680].PreviousLevel;var npc=Kernel.Maps[1004].Npcs[10001];p.Entity.Teleport(1004,(ushort)(npc.X-2),npc.Y);p.ActiveNpc=10001;p.Entity.Level=100;EntityTable.SaveEntity(p);SoulWire(p,22,10001);
                    Check(!Fenced(p) && WarriorReceipts(p)==9 && p.Spells[12680].PreviousLevel==before && p.Spells[12680].Level==0,"instructor retry cannot restore former Backfire training after rite "+rite);}
            }
        }
        var clean=RebirthPlayer(town,25);var rules=typeof(Entity).Assembly.GetType("Revora.Game.Features.RebirthSkillRules");
        Check(!((Spell[])Call(rules,null,"Plan",clean,(byte)21)).Any(s=>warriorNewArts.Contains(s.ID)),"Warrior retention never grants unlearned newer arts");
        VerifyWarriorModes(town);VerifyWarriorBackfire(runtime,town);VerifyMagicDefender(town);VerifyWarriorStance(town);
    }
    static readonly Type DefenderState=typeof(Entity).Assembly.GetType("Revora.Game.Features.MagicDefenderProtection");
    static GameState DefenderPlayer(Map town) {
        var p=RebirthPlayer(town,25);var npc=Kernel.Maps[1004].Npcs[10001];
        p.Entity.Teleport(1004,(ushort)(npc.X-2),npc.Y);p.ActiveNpc=10001;EntityTable.SaveEntity(p);SoulWire(p,11,10001);
        if(!p.Spells.ContainsKey(11200) || Fenced(p))throw new Exception("Ordinary Magic Defender instruction failed.");
        p.Entity.Teleport(1002,408,330);RebirthGear(p,ConquerItem.LeftWeapon,ConquerItemInformation.BaseInformations.Keys.First(id=>id/1000==900));
        p.Weapons=p.ComputeWeapons();p.Entity.Hitpoints=p.Entity.MaxHitpoints=100000;p.Entity.Stamina=100;p.Attackable=true;return p;
    }
    static void DefenderCast(GameState p) {
        p.Entity.AttackStamp=default(Time32);
        new Revora.Game.Attacking.Handle(new Attack(true){AttackType=Attack.Magic,Decoded=true,Damage=11200,Attacker=p.Entity.UID,Attacked=p.Entity.UID,X=p.Entity.X,Y=p.Entity.Y},p.Entity,p.Entity);
    }
    static bool DefenderBlocks(GameState from,GameState to,Time32 now) {return (bool)Call(DefenderState,null,"Blocks",from.Entity,to.Entity,now);}
    static Time32 DefenderEnd(GameState p) {
        var table=DefenderState.GetField("states",Hidden).GetValue(null);var state=table.GetType().GetMethod("GetOrCreateValue").Invoke(table,new object[]{p});return (Time32)Field(state,"Expires");
    }
    static void VerifyMagicDefender(Map town) {
        foreach(byte level in Enumerable.Range(0,5).Select(n=>(byte)n)) {
            var p=DefenderPlayer(town);var foe=RebirthPlayer(town,142);p.Spells[11200].Level=level;DefenderCast(p);var end=DefenderEnd(p);
            Check(p.Entity.HasMagicDefender && p.Entity.MagicDefenderOwner && p.Entity.Stamina==70 && p.Entity.MagicDefender==11+level,"ordinary decoded Magic Defender cast uses shield, catalog duration and exact stamina: "+level+" flags="+p.Entity.HasMagicDefender+"/"+p.Entity.MagicDefenderOwner+" stamina="+p.Entity.Stamina+" duration="+p.Entity.MagicDefender);
            Call(DefenderState,null,"Refresh",p,end.AddSeconds(-20));Check(p.Entity.HasMagicDefender,"a queued pre-cast observation cannot erase a new shield: "+level);
            if(level==0){var mob=LootMonster(p);mob.Hitpoints=mob.MaxHitpoints=100000;mob.MagicAttack=20000;mob.MonsterInfo.SpellID=1000;p.Screen.Add(mob);
                new Revora.Game.Attacking.Handle(null,mob,p.Entity);Check(p.Entity.Hitpoints==100000,"actual monster magic dispatch respects the owned shield");}
            Check(DefenderBlocks(foe,p,end.AddMilliseconds(-1)) && !DefenderBlocks(foe,p,end),"Magic Defender expires at its owned boundary: "+level);
            Call(DefenderState,null,"Refresh",p,end);Check(!p.Entity.HasMagicDefender && !p.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.MagicDefender),"expired shield display clears without movement: "+level);
        }
        foreach(string bad in new[]{"skill","class","shield","broken","stamina","fake","dead","replaced"}) {
            var p=DefenderPlayer(town);var foe=RebirthPlayer(town,15);
            if(bad=="skill")p.Spells.Remove(11200);if(bad=="class")p.Entity.Class=15;if(bad=="shield")p.Equipment.TryGetItem(ConquerItem.LeftWeapon).ID=410009;
            if(bad=="broken")p.Equipment.TryGetItem(ConquerItem.LeftWeapon).Durability=0;if(bad=="stamina")p.Entity.Stamina=29;if(bad=="fake")p.Fake=true;
            if(bad=="dead")p.Entity.Hitpoints=0;if(bad=="replaced")Kernel.GamePool[p.Entity.UID]=foe;byte stamina=p.Entity.Stamina;
            Call(DefenderState,null,"Cast",p);Check(!DefenderBlocks(foe,p,Time32.Now) && !p.Entity.MagicDefenderOwner && p.Entity.Stamina==stamina,"invalid Magic Defender cast has no payment or protection: "+bad);
            if(bad=="replaced")Kernel.GamePool[p.Entity.UID]=p;
        }
        var caster=DefenderPlayer(town);var mate=RebirthPlayer(town,15);var attacker=RebirthPlayer(town,142);
        mate.Entity.Teleport(1002,412,330);attacker.Entity.Teleport(1002,413,330);caster.Entity.ExtraBattlePower=100;
        caster.Team=new Revora.Game.ConquerStructures.Team(caster);caster.Team.Add(mate);
        Parallel.For(0,8,_=>Call(DefenderState,null,"Cast",caster));var expires=DefenderEnd(caster);var cooldown=caster.Spells[11200].ColdTime;
        Check(caster.Entity.Stamina==70 && mate.Entity.HasMagicDefender && !mate.Entity.MagicDefenderOwner,"concurrent casts charge once and project protection to an ordinary teammate");
        DefenderCast(caster);Check(DefenderEnd(caster)==expires && caster.Entity.Stamina==70 && caster.Spells[11200].ColdTime==cooldown,"recast cannot extend a live shield or charge again");
        attacker.Entity.ExtraBattlePower=(uint)(caster.Entity.BattlePower-attacker.Entity.BattlePower+19);
        Check(attacker.Entity.BattlePower-mate.Entity.BattlePower>20 && DefenderBlocks(attacker,mate,Time32.Now),"team immunity compares attacker BP with caster BP rather than recipient BP");
        attacker.Entity.MagicAttack=20000;var spell=SpellTable.GetSpell(1000,0);var hit=new Attack(true);
        Check(Revora.Game.Attacking.Calculate.Magic(attacker.Entity,mate.Entity,spell,ref hit)==0 && Revora.Game.Attacking.Calculate.Magico(attacker.Entity,mate.Entity,spell,ref hit)==0,"both real magic calculators use owned team immunity");
        attacker.Entity.ExtraBattlePower++;
        Check(!DefenderBlocks(attacker,mate,Time32.Now),"an attacker exactly 20 BP above the caster defeats that shield");attacker.Entity.ExtraBattlePower--;
        mate.Entity.X=413;Call(DefenderState,null,"Refresh",mate,Time32.Now);
        Check(!DefenderBlocks(attacker,mate,Time32.Now) && !mate.Entity.HasMagicDefender,"five paces is outside protection and clears the recipient display");
        mate.Entity.X=412;Call(DefenderState,null,"Refresh",mate,Time32.Now);Check(DefenderBlocks(attacker,mate,Time32.Now) && mate.Entity.HasMagicDefender,"a teammate moving back within four paces receives only the existing source lifetime");
        var second=DefenderPlayer(town);second.Entity.X=409;second.Entity.ExtraBattlePower=100;caster.Team.Add(second);DefenderCast(second);
        caster.Entity.X=407;caster.Entity.X=408;
        Check(!caster.Entity.MagicDefenderOwner && second.Entity.MagicDefenderOwner && DefenderBlocks(attacker,mate,Time32.Now),"moving a caster permanently cancels only its source and preserves an overlapping caster");
        lock(caster.ItemSyncRoot)Call(WarriorState,null,"Clear",caster);
        Check(caster.Entity.HasMagicDefender && !caster.Entity.MagicDefenderOwner && second.Entity.MagicDefenderOwner && DefenderBlocks(attacker,mate,Time32.Now),"life cleanup reconciles a surviving teammate shield after clearing the retired owner's details");
        Call(DefenderState,null,"Cancel",second);Check(!DefenderBlocks(attacker,mate,Time32.Now) && !mate.Entity.HasMagicDefender,"ending the last owned source clears team protection");
        // Old flags alone cannot fabricate protection or relay it to a new team.
        mate.Entity.HasMagicDefender=true;mate.Entity.AddFlag((ulong)FlagsUpdate.Flags.MagicDefender,30,true);
        Check(!DefenderBlocks(attacker,mate,Time32.Now),"legacy recipient flags cannot fabricate a source");Call(DefenderState,null,"Refresh",mate,Time32.Now);
        foreach(string invalid in new[]{"map","team","membership","skill","shield","replaced","retired","death","disconnect-cleanup"}) {
            var p=DefenderPlayer(town);var target=RebirthPlayer(town,15);var foe=RebirthPlayer(town,142);target.Entity.Teleport(1002,409,330);
            p.Team=new Revora.Game.ConquerStructures.Team(p);p.Team.Add(target);DefenderCast(p);
            if(invalid=="map")p.Entity.Teleport(1004,38,42);if(invalid=="team")target.Team=null;
            if(invalid=="membership"){Revora.Game.ConquerStructures.Team.Member ignored;p.Team.Members.TryRemove(target.Entity.UID,out ignored);}
            if(invalid=="skill")p.Spells.Remove(11200);if(invalid=="shield")p.Equipment.TryGetItem(ConquerItem.LeftWeapon).Durability=0;
            if(invalid=="replaced")Kernel.GamePool[p.Entity.UID]=foe;if(invalid=="retired")lock(p.ItemSyncRoot)Call(WarriorState,null,"Clear",p);
            if(invalid=="death")p.Entity.Die(foe.Entity);if(invalid=="disconnect-cleanup")Call(DefenderState,null,"Cancel",p);
            Check(!DefenderBlocks(foe,target,Time32.Now),"team protection rejects invalidated source/recipient: "+invalid);
            Call(DefenderState,null,"Refresh",target,Time32.Now);Check(!target.Entity.HasMagicDefender,"invalidated team display clears: "+invalid);
            if(invalid=="replaced")Kernel.GamePool[p.Entity.UID]=p;
        }
        var reload=DefenderPlayer(town);DefenderCast(reload);reload=ReloadBall(reload);
        Check(reload.Spells.ContainsKey(11200) && !reload.Entity.HasMagicDefender,"cold reload retains earned Magic Defender but never restores an expired-session aura");
    }
    static void WarriorFists(GameState p){uint type=ConquerItemInformation.BaseInformations.Keys.First(id=>id/1000==624);RebirthGear(p,ConquerItem.RightWeapon,type);RebirthGear(p,ConquerItem.LeftWeapon,type);p.Weapons=p.ComputeWeapons();}
    static void WarriorCast(GameState p,ushort id){Call(WarriorState,null,"Cast",p,id);}
    static void WarriorTick(GameState p,Time32 now){Call(WarriorState,null,"DanceTick",p,now);}
    static GameState BackfirePlayer(Map town) {
        var p=RebirthPlayer(town,25);var npc=Kernel.Maps[1004].Npcs[10001];
        p.Entity.Teleport(1004,(ushort)(npc.X-2),npc.Y);p.ActiveNpc=10001;EntityTable.SaveEntity(p);SoulWire(p,22,10001);
        if(!p.Spells.ContainsKey(12680) || Fenced(p))throw new Exception("Ordinary Backfire instruction failed.");
        p.Entity.Teleport(1002,408,330);WarriorFists(p);p.Entity.MaxHitpoints=p.Entity.Hitpoints=100000;p.Entity.Stamina=0;p.Attackable=true;
        return p;
    }
    static uint BackfireHit(GameState from,GameState to,uint damage,uint kind=Attack.Melee) {
        var attack=new Attack(true){Attacker=from.Entity.UID,Attacked=to.Entity.UID,AttackType=kind,Damage=damage};
        Revora.Game.Attacking.Handle.ReceiveAttack(from.Entity,to.Entity,attack,ref damage,null);return damage;
    }
    static void VerifyWarriorBackfire(string runtime,Map town) {
        WarriorInstructorMap(runtime);Query("CREATE TABLE IF NOT EXISTS enemy (LIKE public.enemy INCLUDING ALL);CREATE TABLE IF NOT EXISTS pkexploits (LIKE public.pkexploits INCLUDING ALL)");
        foreach(byte level in Enumerable.Range(0,7).Select(x=>(byte)x)) {
            var p=BackfirePlayer(town);var enemy=RebirthPlayer(town,15);enemy.Entity.MaxHitpoints=enemy.Entity.Hitpoints=100000;
            p.Entity.Hitpoints=40000;p.Spells[12680].Level=level;WarriorCast(p,12680);
            uint expected=(uint)(40000*new[]{55,60,65,70,75,80,90}[level]/100);uint incoming=BackfireHit(enemy,p,123);
            Check(enemy.Entity.Hitpoints==100000-expected && p.Entity.Hitpoints==39877 && incoming==123,"Backfire level "+level+" uses current HP and preserves incoming damage");
            Check(!p.Entity.OnBackFire() && p.Entity.BackFireTime==0 && p.Entity.Stamina==30,"Backfire level "+level+" consumes once and restores 30 stamina");
            BackfireHit(enemy,p,123);Check(enemy.Entity.Hitpoints==100000-expected && p.Entity.Stamina==30,"second hit cannot reuse Backfire level "+level);
            WarriorCast(p,12680);Check(!p.Entity.OnBackFire() && p.Spells[12680].ColdTime>Time32.Now,"consumed Backfire retains catalog fifteen-second cooldown");
        }
        foreach(bool blessed in new[]{false,true}) {
            var p=BackfirePlayer(town);var enemy=RebirthPlayer(town,15);enemy.Entity.MaxHitpoints=enemy.Entity.Hitpoints=200000;
            p.Entity.HeavenBlessing=blessed?100u:0u;p.Entity.Stamina=(byte)(blessed?150:90);WarriorCast(p,12680);BackfireHit(enemy,p,2);
            Check(enemy.Entity.Hitpoints==150000 && p.Entity.Stamina==(blessed?160:100),"counter cap and existing stamina ceiling blessed="+blessed);
        }
        foreach(string bad in new[]{"class","skill","fake","dead","fist","broken","flag","zero","reflected","same","map","expired","old-tick","retired","replaced"}) {
            var p=BackfirePlayer(town);var enemy=RebirthPlayer(town,15);enemy.Entity.MaxHitpoints=enemy.Entity.Hitpoints=100000;
            WarriorCast(p,12680);var now=Time32.Now;var from=enemy.Entity;
            if(bad=="class")p.Entity.Class=15;if(bad=="skill")p.Spells.Remove(12680);if(bad=="fake")p.Fake=true;if(bad=="dead")p.Entity.Hitpoints=0;
            if(bad=="fist")p.Equipment.TryGetItem(ConquerItem.LeftWeapon).ID=410009;if(bad=="broken")p.Equipment.TryGetItem(ConquerItem.LeftWeapon).Durability=0;
            if(bad=="flag")p.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.BackFire);if(bad=="same")from=p.Entity;
            if(bad=="map")enemy.Entity.MapID=1004;if(bad=="expired")now=p.Entity.BackFireStamp.AddSeconds(10);if(bad=="old-tick")now=p.Entity.BackFireStamp.AddMilliseconds(-1);
            if(bad=="retired")lock(p.ItemSyncRoot)Call(WarriorState,null,"Clear",p);
            if(bad=="replaced")Kernel.GamePool[p.Entity.UID]=enemy;
            bool applied=(bool)Call(WarriorState,null,"Counter",from,p.Entity,new Attack(true){AttackType=bad=="reflected"?Attack.Reflect:Attack.Melee},bad=="zero"?0u:10u,now);
            Check(!applied && enemy.Entity.Hitpoints==100000 && p.Entity.Stamina==0,"invalid/stale counter has no effects: "+bad);
            if(bad=="replaced")Kernel.GamePool[p.Entity.UID]=p;
        }
        var defender=BackfirePlayer(town);var hitter=BackfirePlayer(town);WarriorCast(defender,12680);WarriorCast(hitter,12680);
        Parallel.For(0,12,_=>BackfireHit(hitter,defender,1));
        Check(hitter.Entity.Hitpoints==50000 && defender.Entity.Stamina==30 && !defender.Entity.OnBackFire() && hitter.Entity.OnBackFire(),"parallel real hits claim only one counter without recursively firing attacker's Backfire");
        var lethal=BackfirePlayer(town);var victim=RebirthPlayer(town,15);victim.Entity.MaxHitpoints=victim.Entity.Hitpoints=1000;WarriorCast(lethal,12680);
        BackfireHit(victim,lethal,1);Check(victim.Entity.Dead && lethal.Entity.Stamina==30,"lethal counter uses normal death handling");
        foreach(ushort spell in new ushort[]{0,1000}) {
            var p=BackfirePlayer(town);var mob=LootMonster(p);mob.Hitpoints=mob.MaxHitpoints=100000;mob.MinAttack=1000;mob.MaxAttack=1001;mob.MagicAttack=1000;mob.MonsterInfo.SpellID=spell;
            p.Screen.Add(mob);WarriorCast(p,12680);new Revora.Game.Attacking.Handle(null,mob,p.Entity);
            Check(mob.Hitpoints==50000 && !p.Entity.OnBackFire() && p.Entity.Stamina==30 && p.Entity.Hitpoints<100000,"real monster dispatch triggers one counter for spell="+spell);
        }
        var snapshot=BackfirePlayer(town);var opponent=RebirthPlayer(town,15);opponent.Entity.Hitpoints=opponent.Entity.MaxHitpoints=100000;snapshot.Entity.Hitpoints=40000;
        WarriorCast(snapshot,12680);snapshot.Spells[12680].Level=6;BackfireHit(opponent,snapshot,1);
        Check(opponent.Entity.Hitpoints==78000,"active counter retains its cast-level power if training changes");
        VerifyBackfireEvent(runtime,town);
        VerifyBackfireGuards(runtime,town);VerifyBackfireCompanions(runtime,town);
    }
    static void VerifyBackfireGuards(string runtime,Map town) {
        var guards=typeof(Revora.Game.Features.CityGuardSystem);var arenaField=guards.GetField("ArenaMap",Hidden);var eliteField=guards.GetField("ArenaElite",Hidden);
        var oldArena=arenaField.GetValue(null);var oldElite=eliteField.GetValue(null);
        var arena=Kernel.Maps.ContainsKey(1005)?Kernel.Maps[1005]:World(runtime,1005);
        var p=BackfirePlayer(town);
        var officer=(GameState)Call(typeof(GuardTests),null,"Player",town,true,(ushort)409,(ushort)330);
        var darius=(GameState)Call(typeof(GuardTests),null,"Player",arena,true,(ushort)49,(ushort)50);
        var unit=Call(typeof(GuardTests),null,"Register",officer,false);var elite=Call(typeof(GuardTests),null,"Register",darius,true);
        try {
            arenaField.SetValue(null,arena);eliteField.SetValue(null,elite);
            officer.Entity.Hitpoints=1000;officer.Entity.MinAttack=1000;officer.Entity.MaxAttack=1001;
            WarriorCast(p,12680);Call(guards,null,"Strike",unit,officer.Entity,p.Entity);
            var incident=Call(typeof(GuardTests),null,"Incident",p.Entity.UID);
            Check(officer.Entity.Dead && p.Entity.MapID==arena.ID && incident!=null && (bool)incident.GetType().GetField("Arena").GetValue(incident),"lethal Backfire against a street officer starts the real Darius sentence");
            Check(p.Entity.Hitpoints==100000 && p.Entity.Stamina==30 && !p.Entity.OnBackFire(),"old street strike cannot damage a player after the counter teleports them");
            Check(Scalar("SELECT mapid FROM entities WHERE uid=@uid",p.Entity.UID)==arena.ID && (DateTime)unit.GetType().GetField("ReviveAt").GetValue(unit)>DateTime.Now.AddSeconds(10),"counter sentence persists the destination and schedules officer revival");
            p.Attackable=true;p.Spells[12680].ColdTime=Time32.Now.AddSeconds(-20);darius.Entity.Hitpoints=1000;darius.Entity.MinAttack=1000;darius.Entity.MaxAttack=1001;
            WarriorCast(p,12680);Call(guards,null,"Strike",elite,darius.Entity,p.Entity);
            Check(darius.Entity.Dead && p.Entity.MapID==1002 && Call(typeof(GuardTests),null,"Incident",p.Entity.UID)==null,"lethal Darius counter follows victory release and removes the sentence");
            Check(p.Entity.Hitpoints==100000 && p.Entity.Stamina==60 && Scalar("SELECT mapid FROM entities WHERE uid=@uid",p.Entity.UID)==1002,"Darius strike cannot follow the released player and the return map persists");
        } finally {
            arenaField.SetValue(null,oldArena);eliteField.SetValue(null,oldElite);
            var units=(System.Collections.IList)guards.GetField("Units",Hidden).GetValue(null);units.Remove(unit);units.Remove(elite);
            var byUid=(System.Collections.IDictionary)guards.GetField("ByUid",Hidden).GetValue(null);byUid.Remove(officer.Entity.UID);byUid.Remove(darius.Entity.UID);
            ((System.Collections.IDictionary)guards.GetField("Incidents",Hidden).GetValue(null)).Remove(p.Entity.UID);
            Kernel.GamePool.Remove(officer.Entity.UID);Kernel.GamePool.Remove(darius.Entity.UID);
        }
    }
    static void VerifyBackfireCompanions(string runtime,Map town) {
        var field=Kernel.Maps.ContainsKey(1000)?Kernel.Maps[1000]:World(runtime,1000);
        foreach(string kind in new[]{"single","collection","clone"})foreach(bool lethal in new[]{false,true}) {
            var p=BackfirePlayer(town);var owner=RebirthPlayer(town,55);p.Entity.Teleport(field.ID,300,300);owner.Entity.Teleport(field.ID,301,300);p.Attackable=true;
            owner.Pet=(Pet)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Pet));owner.Pet.Owner=owner;owner.Pet.MaxAllowed=1;owner.Pet.Pets=new SafeDictionary<Pet.PetType,Pet.PetInfo>();
            var info=MonsterInformation.MonsterInformations[9003].Copy();info.Mesh=100;info.Hitpoints=100000;info.SpellID=1002;
            owner.Pet.AddPet(info);var collection=owner.Pet.Pets[Pet.PetType.Normal].Entity;
            var single=LootMonster(owner);single.Companion=true;single.Owner=owner;single.Hitpoints=single.MaxHitpoints=100000;owner.Companion=single;
            RebirthSkill(owner,12090);var clones=NinjaClones(owner);var body=kind=="single"?single:kind=="collection"?collection:clones[0];
            body.Hitpoints=body.MaxHitpoints=lethal?1000u:100000u;body.MinAttack=1000;body.MaxAttack=1001;body.MagicAttack=1000;body.MonsterInfo.SpellID=kind=="collection"?(ushort)1000:(ushort)0;
            p.Screen.Add(body);WarriorCast(p,12680);new Revora.Game.Attacking.Handle(null,body,p.Entity);
            Check(body.Hitpoints==(lethal?0u:50000u) && !p.Entity.OnBackFire() && p.Entity.Stamina==30 && p.Entity.Hitpoints<100000,"real companion dispatch preserves incoming hit and counters owned "+kind+" lethal="+lethal);
            Check((kind=="single" && lethal?owner.Companion==null:ReferenceEquals(owner.Companion,single))
                && owner.Pet.Pets.ContainsKey(Pet.PetType.Normal)==!(kind=="collection" && lethal)
                && owner.Entity.MyClones.ContainsKey(clones[0].UID)==!(kind=="clone" && lethal) && owner.Entity.MyClones.ContainsKey(clones[1].UID),"companion death only retires its own ownership slot: "+kind+" lethal="+lethal);
            if(!lethal) {
                p.Spells[12680].ColdTime=Time32.Now.AddSeconds(-20);WarriorCast(p,12680);
                if(kind=="single")owner.Companion=clones[1];
                else if(kind=="collection")owner.Pet.Pets[Pet.PetType.Normal].Entity=clones[1];
                else owner.Entity.MyClones[body.UID]=clones[1];
                Check(!(bool)Call(WarriorState,null,"Counter",body,p.Entity,new Attack(true){AttackType=Attack.Melee},1u,Time32.Now) && p.Entity.OnBackFire(),"replaced companion ownership cannot consume the armed counter: "+kind);
            }
        }
    }
    static void VerifyBackfireEvent(string runtime,Map town) {
        Query("CREATE TABLE IF NOT EXISTS npcs (LIKE public.npcs INCLUDING ALL);INSERT INTO npcs SELECT * FROM public.npcs WHERE id=448582 AND NOT EXISTS(SELECT 1 FROM npcs WHERE id=448582)");
        Call(Epk,null,"PlaceEnvoy");
        var zone=TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");var day=TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,zone).Date;
        while(day.DayOfWeek!=DayOfWeek.Friday)day=day.AddDays(-1);
        epkNow=TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(day.AddHours(19).AddMinutes(55),DateTimeKind.Unspecified),zone);
        var p=BackfirePlayer(town);var enemy=BackfirePlayer(town);
        var e=NewEpk(runtime);Call(Epk,e,"Tick");
        try {
            foreach(var player in new[]{p,enemy}){player.Entity.X=town.Npcs[448582].X;player.Entity.Y=town.Npcs[448582].Y;player.ActiveNpc=448582;EpkJoin(player);}
            EpkTick(e,300);EpkTick(e,11);var match=EpkMatches(e).Single(m=>(bool)Field(m,"Imported"));
            p.Entity.Stamina=0;WarriorCast(p,12680);
            // Put the actual match back behind its preparation barrier while an
            // owned effect is armed, rather than relying on import's buff reset.
            match.GetType().GetField("Ready",Instance).SetValue(match,epkNow.AddSeconds(10));
            uint hp=enemy.Entity.Hitpoints;BackfireHit(enemy,p,10);
            Check(enemy.Entity.Hitpoints==hp && p.Entity.OnBackFire() && p.Entity.Stamina==0,"Elite PK preparation cannot consume or trigger a counter");
            EpkTick(e,11);p.Entity.MaxHitpoints=p.Entity.Hitpoints=100000;enemy.Entity.MaxHitpoints=enemy.Entity.Hitpoints=100000;
            // The event clock advances independently of the active-effect clock.
            BackfireHit(enemy,p,10);string side=EpkFighter(match,"Left")==p?"LeftDamage":"RightDamage";
            Check(enemy.Entity.Hitpoints==50000 && Convert.ToInt64(Field(match,side))==50000 && !p.Entity.OnBackFire(),"active Elite PK counter credits its actual defender once");
            p.Spells[12680].ColdTime=Time32.Now.AddMilliseconds(-1);WarriorCast(p,12680);enemy.Entity.Hitpoints=1000;uint before=p.Entity.Hitpoints;
            uint incoming=BackfireHit(enemy,p,10);
            Check(enemy.Entity.Dead && (uint)Field(match,"Winner")==p.Entity.UID && p.Entity.Hitpoints==before && incoming==0
                && Convert.ToInt64(Field(match,side))==51000,"lethal tournament counter credits only actual HP and suppresses the post-result incoming hit");
        }
        finally {Epk.GetField("Current",Hidden).SetValue(null,null);}
    }
    static void VerifyWarriorModes(Map town){
        foreach(ushort id in new ushort[]{12680,12700})foreach(string bad in new[]{"class","skill","fake","dead","one-fist","broken-fist","no-xp"}){
            var p=RebirthPlayer(town,25);RebirthSkill(p,id);WarriorFists(p);p.Entity.AddFlag((ulong)FlagsUpdate.Flags.XPList,StatusFlagsBigVector32.PermanentFlag,false);
            if(bad=="class")p.Entity.Class=15;if(bad=="skill")p.Spells.Remove(id);if(bad=="fake")p.Fake=true;if(bad=="dead")p.Entity.Hitpoints=0;
            if(bad=="one-fist")p.Equipment.TryGetItem(ConquerItem.LeftWeapon).ID=410009;if(bad=="broken-fist")p.Equipment.TryGetItem(ConquerItem.LeftWeapon).Durability=0;
            if(bad=="no-xp")p.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.XPList);WarriorCast(p,id);
            Check(id==12680 && bad=="no-xp"?p.Entity.OnBackFire():!p.Entity.OnBackFire() && !p.Entity.OnManiacDance(),"self-cast enforces canonical learned Warrior and paired usable fists: "+id+"/"+bad);
        }
        var caster=RebirthPlayer(town,25);RebirthSkill(caster,12680);RebirthSkill(caster,12700);WarriorFists(caster);caster.Entity.AddFlag((ulong)FlagsUpdate.Flags.XPList,StatusFlagsBigVector32.PermanentFlag,false);
        var supplied=RebirthPlayer(town,15);caster.Screen.Add(supplied.Entity);supplied.Entity.BackFireTime=7;supplied.Entity.ManiacDanceTime=9;supplied.Entity.KOSpell=1110;supplied.Entity.KOCount=4;
        foreach(ushort id in new ushort[]{12680,12700}){
            caster.Entity.AttackStamp=default(Time32);
            new Revora.Game.Attacking.Handle(new Attack(true){AttackType=Attack.Magic,Decoded=true,Damage=id,Attacker=caster.Entity.UID,Attacked=supplied.Entity.UID,X=caster.Entity.X,Y=caster.Entity.Y},caster.Entity,supplied.Entity);
        }
        Check(caster.Entity.OnBackFire() && caster.Entity.OnManiacDance() && supplied.Entity.BackFireTime==7 && supplied.Entity.ManiacDanceTime==9 && supplied.Entity.KOSpell==1110 && supplied.Entity.KOCount==4,"actual decoded spell dispatch changes the caster only despite a different supplied target");
        WarriorCast(caster,12680);WarriorCast(caster,12700);var stamp=caster.Entity.ManiacDanceStamp;var back=caster.Entity.BackFireStamp;
        Check(caster.Entity.OnBackFire() && caster.Entity.BackFireTime==10 && caster.Entity.OnManiacDance() && caster.Entity.ManiacDanceTime==15 && caster.Entity.KOSpell==12700 && !caster.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.XPList),"self-casts set the caster's ten/fifteen-second state and consume the XP circle");
        Parallel.For(0,12,_=>{WarriorCast(caster,12680);WarriorCast(caster,12700);});Check(caster.Entity.ManiacDanceStamp==stamp && caster.Entity.BackFireStamp==back,"repeated self-casts do not extend active effects");
        var target=LootMonster(caster);target.Hitpoints=target.MaxHitpoints=100000;caster.Screen.Add(target);caster.Entity.MinAttack=100;caster.Entity.MaxAttack=101;
        WarriorTick(caster,stamp.AddMilliseconds(-1));Check(target.Hitpoints==100000,"a queued timer observation from before the cast cannot pulse the new Dance");
        WarriorTick(caster,stamp);uint first=target.Hitpoints;Check(first<100000,"first scheduled Dance observation can pulse without an extra one-second delay");
        var tick=stamp.AddSeconds(1);WarriorTick(caster,tick);uint hp=target.Hitpoints;Check(hp<first,"owned learned equipped Dance delivers its next one-second pulse");
        Parallel.For(0,12,_=>WarriorTick(caster,tick));Check(target.Hitpoints==hp,"simultaneous callbacks cannot deliver duplicate Dance pulses");
        foreach(string bad in new[]{"skill","class","dead","fist","expired"}){
            if(bad=="skill")caster.Spells.Remove(12700);if(bad=="class")caster.Entity.Class=15;if(bad=="dead")caster.Entity.Hitpoints=0;if(bad=="fist")caster.Equipment.TryGetItem(ConquerItem.LeftWeapon).Durability=0;
            WarriorTick(caster,stamp.AddSeconds(bad=="expired"?15:2));Check(target.Hitpoints==hp,"stale Dance tick grants no damage after losing valid ownership/state: "+bad);
            if(bad=="skill")RebirthSkill(caster,12700);caster.Entity.Class=25;caster.Entity.Hitpoints=100;caster.Equipment.TryGetItem(ConquerItem.LeftWeapon).Durability=1000;
        }
        var other=RebirthPlayer(town,25);caster.Team=new Revora.Game.ConquerStructures.Team(caster);caster.Team.Add(other);WarriorActive(caster);WarriorActive(other);lock(caster.ItemSyncRoot)Call(WarriorState,null,"Clear",caster);
        WarriorTick(caster,stamp.AddSeconds(3));Check(WarriorInactive(caster) && warriorFlags.All(f=>other.Entity.ContainsFlag((ulong)f)) && other.Entity.HasMagicDefender && target.Hitpoints==hp,"cleanup preserves another character's buffs and blocks delayed pulses even if the retired owner retains the art");
    }
}
