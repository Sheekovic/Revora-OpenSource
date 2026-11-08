using System;
using System.Reflection;
using Revora;
using Revora.Client;
using Revora.Game;
using Revora.Game.Attacking;
using Revora.Network.GamePackets;
using Console = System.Console;
internal static class CombatTests
{
    static int checks;
    static void Check(bool okay,string name) { if(!okay)throw new Exception("FAILED: "+name);checks++; }
    static readonly Type Rules=typeof(Entity).Assembly.GetType("Revora.Game.Attacking.CombatMath");
    static object Rule(string name,params object[] args) {return Rules.GetMethod(name,BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,args);}
    static bool Critical(Attack p) {return (p.Effect & Attack.AttackEffects.CriticalStrike)!=0;}
    static object Fixture(string name, params object[] args) { return typeof(CompanionTests).GetMethod(name,BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,args); }
    static GameState Player(Map map, byte cls)
    {
        var p=(GameState)Fixture("Player",map,false,(ushort)20,(ushort)20,cls);
        p.Entity.MinAttack=p.Entity.MaxAttack=40000;p.Entity.MagicAttack=40000;
        p.Entity.Defence=10000;p.Entity.MagicDefence=0;
        p.Entity.NobilityRank=Revora.Game.ConquerStructures.NobilityRank.Serf;
        return p;
    }
    static uint Hit(int mode, Entity a, Entity d, out Attack packet)
    {
        packet=new Attack(true);
        var spell=new Revora.Database.SpellInformation{PowerPercent=1,Power=1000};
        switch(mode) {
            case 0:return Calculate.Melee(a,d,spell,ref packet);
            case 1:return Calculate.Rango(a,d,spell,ref packet);
            case 2:return Calculate.Rango(a,d,ref packet);
            case 3:return Calculate.Ranged(a,d,spell,ref packet);
            case 4:return Calculate.Magic(a,d,spell,ref packet);
            default:return Calculate.Magico(a,d,spell,ref packet);
        }
    }
    static void PveNobility(Map map)
    {
        var p = Player(map, 41);
        p.Entity.Level = 133;
        p.Entity.Reborn = 2;
        var boss = (Entity)Fixture("Monster", map, (ushort)21, (ushort)20, 10u);
        boss.MonsterInfo.ID = 9912000;
        boss.MonsterInfo.Boss = true;
        boss.Name = "Moonfang King";
        boss.Defence = 7000;
        boss.MagicDefence = 40;
        boss.MaxHitpoints = boss.Hitpoints = 100000000;
        Check(boss.Owner == null && boss.BattlePower == 0,
            "daily boss representation has no character owner or Battle Power");
        foreach (uint attack in new uint[] { 6379, 20000, 40000 })
        {
            p.Entity.MinAttack = p.Entity.MaxAttack = attack;
            p.Entity.MagicAttack = attack;
            p.Entity.NobilityRank = Revora.Game.ConquerStructures.NobilityRank.Serf;
            Attack packet = new Attack(true);
            uint baseline = Calculate.Melee(p.Entity, boss, ref packet);
            int lowPower = p.Entity.BattlePower;
            p.Entity.NobilityRank = Revora.Game.ConquerStructures.NobilityRank.King;
            uint royal = Calculate.Melee(p.Entity, boss, ref packet);
            Check(p.Entity.BattlePower > lowPower && baseline > 0 && royal == baseline,
                "nobility changes BP but not daily-boss melee damage, attack=" + attack);
        }
        var daily = typeof(Entity).Assembly.GetType("Revora.Game.Features.DailyWorldBosses", true);
        var activeField = daily.GetField("Active", BindingFlags.NonPublic | BindingFlags.Static);
        object saved = activeField.GetValue(null);
        var active = Activator.CreateInstance(activeField.FieldType, true);
        activeField.FieldType.GetField("Entity", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(active, boss);
        activeField.SetValue(null, active);
        try
        {
            foreach (var rank in new[] { Revora.Game.ConquerStructures.NobilityRank.Serf, Revora.Game.ConquerStructures.NobilityRank.King })
            {
                p.Entity.NobilityRank = rank;
                bool bounded = true;
                for (int sample = 0; sample < 32; sample++)
                {
                    var packet = new Attack(true);
                    uint hit = Calculate.Melee(boss, p.Entity, ref packet);
                    bounded &= hit >= 6500 && hit <= 7500;
                }
                Check(bounded, "active daily boss retains6500..7500 retaliation for " + rank);
            }
        }
        finally { activeField.SetValue(null, saved); }
        Console.WriteLine("PvE nobility dependency checks passed: " + checks);
    }
    static void FrostGazeDrain(Map map)
    {
        var caster=Player(map,165);var target=Player(map,15);
        target.Entity.Level=100;
        caster.Screen.Add(target.Entity);
        Check(caster.Entity.BattlePower-target.Entity.BattlePower>=20,"Frost Gaze fixture guarantees proc chance");
        foreach(ushort id in new ushort[]{12830,13070,13080}) {
            var spell=new Revora.Database.SpellInformation{ID=id,Level=0,Power=50,
                WeaponSubtype=new System.Collections.Generic.List<ushort>(),
                OnlyWithThisWeaponSubtype=new System.Collections.Generic.List<ushort>()};
            Revora.Database.SpellTable.SpellInformations[id]=new System.Collections.Generic.SafeDictionary<byte,Revora.Database.SpellInformation>();
            Revora.Database.SpellTable.SpellInformations[id][0]=spell;
            caster.Spells[id]=new Spell(true){ID=id,Level=0,Available=true};
            foreach(uint power in new uint[]{0,1,50,uint.MaxValue}) {
            spell.Power=power;
            foreach(byte stamina in new byte[]{0,1,49,50,51,100,150,255}) {
                target.Entity.Stamina=stamina;
                caster.Entity.AttackStamp=Time32.Now.AddSeconds(-10);
                caster.CantAttack=Time32.Now.AddSeconds(-10);
                new Handle(new Attack(true){AttackType=Attack.Magic,Decoded=true,Damage=id,
                    Attacker=caster.Entity.UID,Attacked=caster.Entity.UID,X=caster.Entity.X,Y=caster.Entity.Y},caster.Entity,caster.Entity);
                Check(target.Entity.Stamina==Math.Max(0L,(long)stamina-power),
                    "Frost Gaze "+id+" drains="+power+" start="+stamina+" actual="+target.Entity.Stamina);
            }
            }
        }
        Console.WriteLine("Frost Gaze drain: "+checks+" checks passed.");
    }
    static void StaminaRecovery(Map map)
    {
        var p=Player(map,15);
        var callback=typeof(Screen).GetMethod("monsterBuffersCallback",BindingFlags.Static|BindingFlags.NonPublic);
        string connection=Revora.Database.DataHolder.ConnectionString;
        try {
            Revora.Database.DataHolder.ConnectionString="RevoraOfflineFixture=1";
            p.Socket.Alive=true;
            Check(!p.Fake && p.Entity.HeavenBlessing==0 && !p.Map.FreezeMonsters,"ordinary unblessed stamina fixture");
            foreach(bool sitting in new[]{false,true}) {
                p.Entity.Action=sitting?Revora.Game.Enums.ConquerAction.Sit:Revora.Game.Enums.ConquerAction.None;
                p.Entity.Stamina=0;p.Entity.StaminaStamp=new Time32(100000);
                callback.Invoke(null,new object[]{p,100499});
                Check(p.Entity.Stamina==0,"stamina waits for500ms boundary");
                int steps=sitting?10:20,amount=sitting?10:5;
                for(int n=1;n<=steps;n++) {
                    int time=100000+n*501;
                    callback.Invoke(null,new object[]{p,time});
                    Check(p.Entity.Stamina==n*amount,"production callback restores stamina, sitting="+sitting+" step="+n);
                    callback.Invoke(null,new object[]{p,time});
                    Check(p.Entity.Stamina==n*amount,"same-time callback cannot double refill");
                }
                callback.Invoke(null,new object[]{p,120000});
                Check(p.Entity.Stamina==100,"unblessed stamina caps at100");
            }
            foreach(ulong flag in new[]{(ulong)FlagsUpdate.Flags.Ride,(ulong)FlagsUpdate.Flags.Fly,(ulong)FlagsUpdate.Flags.FrostArrows}) {
                p.Entity.Stamina=0;p.Entity.StaminaStamp=new Time32(100000);
                p.Entity.AddFlag(flag,60,true);
                callback.Invoke(null,new object[]{p,100501});
                Check(p.Entity.Stamina==0,"stamina blocked by status="+flag);
                p.Entity.RemoveFlag(flag);
                callback.Invoke(null,new object[]{p,101002});
                Check(p.Entity.Stamina==10,"recovery resumes when blocking status ends");
            }
        } finally {p.Socket.Alive=false;Revora.Database.DataHolder.ConnectionString=connection;}
        Console.WriteLine("Stamina recovery: "+checks+" checks passed.");
    }
    static void PowerArenaWindow(Map map)
    {
        var rules=typeof(Entity).Assembly.GetType("Revora.Game.Features.PowerArenaEvent");
        var flags=BindingFlags.Static|BindingFlags.NonPublic;
        var needs=(int[])rules.GetField("Needs",flags).GetValue(null);
        var configure=rules.GetMethod("ConfigureGuardian",flags);
        // Read-only installed spawn snapshot: max_per_gen, not maxnpc, is population.
        int[] population={30,30,30,4,30,30,30};
        var p=Player(map,45);p.Entity.Level=140;p.Entity.Reborn=2;
        p.Entity.MinAttack=p.Entity.MaxAttack=25571;p.Entity.Gems[1]=180;
        p.Entity.PhysicalDamageIncrease=0;p.Entity.CriticalStrike=0;
        int attacks=0,wait=0,kills=0;
        for(int floor=1;floor<=7;floor++) {
            var mob=(Entity)Fixture("Monster",map,(ushort)21,(ushort)20,10u);
            mob.MonsterInfo.ID=(uint)(2132+floor);mob.MonsterInfo.Boss=false;
            configure.Invoke(null,new object[]{mob.MonsterInfo});
            mob.Level=mob.MonsterInfo.Level;mob.Defence=mob.MonsterInfo.Defence;
            mob.MaxHitpoints=mob.Hitpoints=mob.MonsterInfo.Hitpoints;
            var packet=new Attack(true);uint damage=Calculate.Rango(p.Entity,mob,ref packet);
            Check(damage>0,"earned Archer can damage Power Arena floor "+floor);
            int shots=(int)((mob.Hitpoints+damage-1)/damage);
            int waves=(needs[floor-1]+population[floor-1]-1)/population[floor-1];
            attacks+=shots*needs[floor-1];wait+=(waves-1)*46;kills+=needs[floor-1];
            Console.WriteLine("Power floor="+floor+" kills="+needs[floor-1]+" population="+population[floor-1]+" shots-per-kill="+shots+" waves="+waves);
        }
        // One attack per second; wait a full45s respawn plus1s timer margin
        // after each cleared wave, even though earlier deaths respawn sooner.
        // Fifteen minutes for approach/claims/healing is an allowance, not a
        // native navigation or survival measurement; competition is excluded.
        int allowance=900,total=attacks+wait+allowance;
        Check(kills==365 && total<3600,"Power Arena quota and unopposed population fit the one-hour window with explicit allowance");
        Console.WriteLine("Power window: kills="+kills+" attack-seconds="+attacks+" respawn-wait="+wait+" allowance="+allowance+" total="+total+" limit=3600");
    }
    static void FlameGateBudget(Map map)
    {
        // Earned fresh Archer's minimum attack/gems; omit fan, criticals,
        // blessing and temporary buffs. Gate health is from the reviewed
        // candidate sobnpcs catalog (516074 and516075), not a live DB query.
        var p=Player(map,45);p.Entity.Level=140;p.Entity.Reborn=2;
        p.Entity.MinAttack=p.Entity.MaxAttack=25571;p.Entity.Gems[1]=180;
        p.Entity.PhysicalDamageIncrease=0;p.Entity.CriticalStrike=0;
        var gate=new SobNpcSpawn{UID=516074,MapID=1038,Hitpoints=10000000,MaxHitpoints=10000000};
        var packet=new Attack(true);
        uint damage=Calculate.Rango(p.Entity,gate,ref packet);
        Check(damage==44748,"earned minimum Archer damage against Guild gate");
        long hits=(gate.MaxHitpoints+(long)damage-1)/damage;
        Check(hits==224,"ten-million-HP gate needs224 ordinary calculated hits");
        // Budget a second full gate break after returning for the ninth rune.
        // One hit/sec and20min movement/dialogue allowance; stone10 opens
        // at30min in the one-hour war. This excludes enemy players/captures.
        long combatSeconds=2*hits,routeSeconds=1200;
        long finishSeconds=Math.Max(1800,combatSeconds+routeSeconds)+60;
        Check(finishSeconds<3600,"two gate breaks and explicit route allowance fit Flame window");
        Check(2*hits*2<4097,"gate attacks fit the standalone equipment reserve with two wear units per shot");
        Console.WriteLine("Flame gate budget: damage="+damage+" hits-per-gate="+hits+" combat-seconds="+combatSeconds+" route-allowance="+routeSeconds+" finish-seconds="+finishSeconds);
    }
    static void SpookMaintenance(Map map)
    {
        // Selected10529 route uses grade1. Reuse the earned Archer minimum
        // stats, omit fan/crit/buffs and defense, and allow no successful dodge.
        var p=Player(map,45);p.Entity.Level=140;p.Entity.Reborn=2;
        p.Entity.MinAttack=p.Entity.MaxAttack=25571;p.Entity.Gems[1]=180;
        p.Entity.PhysicalDamageIncrease=0;p.Entity.CriticalStrike=0;
        p.Entity.Defence=0;p.Entity.MagicDefence=0;p.Entity.BaseMagicDefence=0;p.Entity.MagicDefencePercent=0;
        p.Entity.MaxHitpoints=30163;p.Entity.Hitpoints=23960;
        var boss=(Entity)Fixture("Monster",map,(ushort)21,(ushort)20,10u);
        boss.MonsterInfo.ID=9916201;boss.MonsterInfo.Boss=true;boss.Name="ThrillingSpook";
        boss.Level=boss.MonsterInfo.Level=90;boss.Defence=2328;
        boss.MaxHitpoints=boss.Hitpoints=10000000;boss.MinAttack=boss.MaxAttack=30000;
        var packet=new Attack(true);uint outgoing=Calculate.Rango(p.Entity,boss,ref packet);
        packet=new Attack(true);uint incoming=Calculate.Melee(boss,p.Entity,ref packet)*3/2;
        Check(outgoing>0 && incoming<23960,"earned Archer survives worst calculated grade1 Spook phase");
        long hp=10000000,health=23960,minimum=health;int seconds=0,hits=0,potions=0,retaliations=0;
        while(hp>0 && seconds<3600) {
            seconds++;
            // Shortest real cycle:2s windup plus4s rest. Budget the worst
            // 1.5x phase and a2s stun every cycle, instead of every third cast.
            if(seconds%6==2){health-=incoming;retaliations++;minimum=Math.Min(minimum,health);}
            if(health<=0)break;
            if(seconds%6!=2 && seconds%6!=3) {
                hp-=outgoing;hits++;
                if(health+7000<=30163){health+=7000;potions++;}
            }
        }
        Check(hp<=0 && seconds+900<3600 && minimum>0,"Spook worst-phase/stun budget leaves15min for approach within one-hour hunt");
        Check(potions<=100,"standalone Spook hunt fits previously purchased100-potion stock");
        Check(hits*2+retaliations*4<4097,"standalone Spook wear fits observed equipment reserve");
        Console.WriteLine("Spook maintenance: hit="+outgoing+" worst-phase="+incoming+" seconds="+seconds+" hits="+hits+" retaliations="+retaliations+" medicines="+potions+" minimum-hp="+minimum);
    }
    static void EarnedArcherDeadlines(Map map)
    {
        // Conservative replay of the earned loadout's minimum attack/gems.
        // Omit its fan, criticals and all favorable temporary buffs.
        var p=Player(map,45);p.Entity.Level=140;p.Entity.Reborn=2;
        p.Entity.MinAttack=p.Entity.MaxAttack=25571;p.Entity.Gems[1]=180;
        p.Entity.PhysicalDamageIncrease=0;p.Entity.CriticalStrike=0;
        int totalMedicines=0,totalWear=0;
        foreach(uint family in new uint[]{9916100,9916804}) {
            var boss=(Entity)Fixture("Monster",map,(ushort)21,(ushort)20,10u);
            bool terato=family==9916100;
            boss.Name=terato?"Terato Dragon":"Sword Master";
            boss.MonsterInfo.ID=family;boss.MonsterInfo.Boss=true;
            boss.Level=(byte)(terato?138:140);boss.Defence=(ushort)(terato?7000:100);
            boss.MaxHitpoints=boss.Hitpoints=terato?50000000u:15000000u;
            boss.MonsterInfo.Level=boss.Level;
            var packet=new Attack(true);uint hit=Calculate.Rango(p.Entity,boss,ref packet);
            Check(hit==71599,"unbuffed minimum Archer profile damage remains71599 against "+boss.Name);
            p.Entity.Defence=0;p.Entity.MagicDefence=0;p.Entity.BaseMagicDefence=0;
            p.Entity.MagicDefencePercent=0;p.Entity.MaxHitpoints=30163;p.Entity.Hitpoints=23960;
            boss.MinAttack=boss.MaxAttack=terato?12000u:2328u;boss.MagicAttack=terato?12000u:3000u;
            packet=new Attack(true);uint melee=Calculate.Melee(boss,p.Entity,ref packet);
            packet=new Attack(true);uint magic=Calculate.Magic(boss,p.Entity,new Revora.Database.SpellInformation{ID=(ushort)(terato?7014:10500),Power=0},ref packet);
            uint incoming=Math.Max(magic,terato?melee:melee*2);
            Check(incoming<23960,"zero-defense Archer survives worst calculated single hit from "+boss.Name);
            // One attack per second: half the500ms dispatch schedule already
            // used by the earned Snow run. Sword heals a full2% every6s here,
            // more often than its actual seven-phase42s cycle.
            long hp=boss.MaxHitpoints;int seconds=0;long healed=0;
            while(hp>0 && seconds<1800) {
                seconds++;
                if(!terato && seconds%6==0) {
                    long amount=Math.Min(boss.MaxHitpoints-hp,boss.MaxHitpoints/50);
                    hp+=amount;healed+=amount;
                }
                // Reserve an entire6s phase each30s for Terato fear/recovery;
                // its actual fear lasts2s. No attacks in this allowance.
                if(!terato || seconds%30<24)hp-=hit;
            }
            Check(hp<=0 && seconds<1800,"conservative damage schedule beats30-minute "+boss.Name+" deadline");
            Console.WriteLine("Archer deadline: family="+family+" minimum-hit="+hit+" seconds="+seconds+" modeled-healing="+healed);
            long playerHp=23960,minimumHp=playerHp;int medicines=0,retaliations=0,attacks=0;bool alive=true;
            // Use the worst phase every6s, even for non-damaging fear/heal
            // phases. One spare500ms action per second can use a7000HP potion.
            for(int second=1;second<=seconds;second++) {
                if(second%6==0){playerHp-=incoming;retaliations++;minimumHp=Math.Min(minimumHp,playerHp);}
                alive &= playerHp>0;
                if(!terato || second%30<24) {
                    attacks++;
                    if(playerHp+7000<=30163){playerHp+=7000;medicines++;}
                }
            }
            Check(alive,"bounded retaliation survives every action and recovery slot for "+boss.Name);
            totalMedicines+=medicines;totalWear+=attacks*2+retaliations*4;
            Console.WriteLine("Archer survival: family="+family+" worst-hit="+incoming+" minimum-hp="+minimumHp+" medicines="+medicines+" retaliations="+retaliations);
        }
        // Reserve up to five extra medicines to restore HP between encounters;
        // the second model must not receive an unexplained free health reset.
        totalMedicines+=5;
        Check(totalMedicines<=100,"saved100-potion stock covers both encounters and between-fight recovery");
        Check(totalWear<4097,"both encounter wear allowances fit lowest saved non-arrow durability4097");
        Console.WriteLine("Archer combined maintenance: medicines="+totalMedicines+" per-item-wear-bound="+totalWear);
    }
    static void ArrowDefensiveWear(Map map)
    {
        var p=Player(map,45);
        var monster=(Entity)Fixture("Monster",map,(ushort)21,(ushort)20,10u);
        var items=(ConquerItem[])typeof(Revora.Game.ConquerStructures.Equipment)
            .GetField("objects",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(p.Equipment);
        var wear=typeof(Calculate).GetMethod("Durability",BindingFlags.Static|BindingFlags.NonPublic);
        string connection=Revora.Database.DataHolder.ConnectionString;
        try {
            // Any unexpected persistence attempt must fail during connection
            // string parsing, before network access or a database write.
            Revora.Database.DataHolder.ConnectionString="RevoraOfflineFixture=1";
            foreach(uint type in new uint[]{1050000,1050001,1050002}) {
                var arrow=new ConquerItem(true){UID=4294909999,ID=type,Position=5,Durability=100,MaximDurability=100};
                items[4]=arrow;
                Check(ReferenceEquals(p.Equipment.TryGetItem((byte)5),arrow),"ammunition fixture occupies the actual offhand slot");
                Check(ItemHandler.IsArrow(type) && ItemHandler.ItemPosition(type)==5,"arrow uses offhand slot");
                for(int n=0;n<100;n++)wear.Invoke(null,new object[]{monster,p.Entity,null});
                Check(arrow.Durability==100,"enemy attacks never wear ammunition as armour: "+type);
            }
        } finally { Revora.Database.DataHolder.ConnectionString=connection; }
        Console.WriteLine("Arrow defensive wear: "+checks+" checks passed.");
    }
    static void SnowHealingBudget()
    {
        var flags = BindingFlags.Static | BindingFlags.NonPublic;
        var combat = typeof(Entity).Assembly.GetType("Revora.Game.Features.SnowBansheeCombat", true);
        var store = typeof(Entity).Assembly.GetType("Revora.Game.Features.SnowBansheeStore", true);
        Func<string,int> setting = name => (int)combat.GetField(name,flags).GetRawConstantValue();
        int phases = ((ushort[])combat.GetField("Spells",flags).GetValue(null)).Length;
        int cycle = phases * (setting("WarningSeconds") + setting("RecoverySeconds"))
            + (setting("HailPulses") - 1) * setting("HailIntervalSeconds");
        uint health = (uint)store.GetField("Health",flags).GetRawConstantValue();
        uint heal = health / (uint)setting("HealDivisor");
        Check(cycle == 47 && heal == 2000000, "reviewed Snow healing cadence");
        // Ordinary non-magic dispatch permits one hit per500ms without Infinity.
        // These are conditional damage budgets, not owned-loadout or survival proofs.
        foreach(int players in new[]{1,2,5}) foreach(int activePercent in new[]{50,100}) {
            long attacks = (long)cycle * 2 * players * activePercent / 100;
            long needed = heal / attacks + 1;
            Check(needed * attacks > heal && (needed - 1) * attacks <= heal,
                "minimum positive progress threshold: players=" + players + " uptime=" + activePercent);
            Console.WriteLine("Snow budget: players=" + players + " active-percent=" + activePercent + " minimum-damage-per-hit=" + needed);
        }
        Check(5000L * 2 * cycle < heal, "fixed5000-damage solo attacks cannot outpace healing");
        long margin = 5000L * 2 * cycle * 5 - heal;
        Check(margin == 350000 && 5000L * cycle * 5 < heal,
            "five fixed5000-damage players lose their positive margin at50percent attack uptime");
        Console.WriteLine("Snow healing budget: " + checks + " checks passed; combat acceptance remains separate.");
    }
    static void TeratoGeometry()
    {
        var type = typeof(Entity).Assembly.GetType("Revora.Game.Features.TeratoCombat", true);
        var contains = type.GetMethod("Contains", BindingFlags.Static | BindingFlags.NonPublic);
        Func<int,int,int,int,int,bool> hit = (phase,x,y,fx,fy) =>
            (bool)contains.Invoke(null, new object[] { phase,x,y,fx,fy });
        foreach(int phase in new[]{0,1,2,3,4})
            foreach(int far in new[]{46341,65535,-46341,-65535})
                foreach(int y in new[]{0,far,-far})
                    Check(!hit(phase,far,y,1,0), "Terato distant target outside phase " + phase + " at " + far + "," + y);
        Check(hit(0,8,0,1,0) && !hit(0,9,0,1,0) && !hit(0,-1,0,1,0), "breath forward radius and rear exclusion");
        Check(hit(0,4,4,1,0) && !hit(0,4,5,1,0), "breath cone edge");
        Check(hit(1,-8,0,1,0) && !hit(1,1,0,1,0), "tail uses rear cone");
        Check(hit(2,6,0,1,0) && !hit(2,7,0,1,0), "shower radius six");
        Check(!hit(3,2,0,1,0) && hit(3,3,0,1,0) && hit(3,8,0,1,0) && !hit(3,9,0,1,0), "mist annulus inner and outer edges");
        Check(hit(4,8,0,1,0) && !hit(4,9,0,1,0), "fear radius eight");
        Check(hit(0,4,4,1,1) && !hit(0,-4,-4,1,1), "diagonal facing preserves front and rear");
        Console.WriteLine("Terato geometry: " + checks + " checks passed.");
    }
    public static int Main(string[] args)
    {
        try {
            if (args.Length > 0 && args[0] == "--snow-healing-only") { SnowHealingBudget(); return 0; }
            if (args.Length > 0 && args[0] == "--terato-geometry-only") { TeratoGeometry(); return 0; }
            Fixture("Reset");var map=(Map)Fixture("World",(ushort)1002);
            if (args.Length > 0 && args[0] == "--stamina-recovery-only") { StaminaRecovery(map); return 0; }
            if (args.Length > 0 && args[0] == "--archer-deadlines-only") { EarnedArcherDeadlines(map); return 0; }
            if (args.Length > 0 && args[0] == "--flame-gate-only") { FlameGateBudget(map); return 0; }
            if (args.Length > 0 && args[0] == "--frost-gaze-only") { FrostGazeDrain(map); return 0; }
            if (args.Length > 0 && args[0] == "--arrow-wear-only") { ArrowDefensiveWear(map); return 0; }
            if (args.Length > 0 && args[0] == "--pve-nobility-only") { PveNobility(map); return 0; }
            TeratoGeometry();
            SnowHealingBudget();
            ArrowDefensiveWear(map);
            FrostGazeDrain(map);
            StaminaRecovery(map);
            EarnedArcherDeadlines(map);
            PowerArenaWindow(map);
            FlameGateBudget(map);
            SpookMaintenance(map);
            byte[] classes={15,25,45,55,65,75,85,135,145,165};
            var players=Array.ConvertAll(classes,c=>Player(map,c));
            bool audit=args.Length>0 && args[0]=="--audit";
            for(int mode=0;mode<6;mode++) {
                uint min=uint.MaxValue,max=0;string low="",high="";
                foreach(var a in players)foreach(var d in players) {
                    Attack packet;uint damage=Hit(mode,a.Entity,d.Entity,out packet);
                    if(!audit && min!=uint.MaxValue)Check(damage==min,"class-neutral mode "+mode+" "+a.Entity.Class+"->"+d.Entity.Class);
                    if(damage<min){min=damage;low=a.Entity.Class+"->"+d.Entity.Class;}
                    if(damage>max){max=damage;high=a.Entity.Class+"->"+d.Entity.Class;}
                }
                Console.WriteLine("MODE "+mode+" MIN "+min+" ("+low+") MAX "+max+" ("+high+")");
            }
            if(audit)return 0;
            foreach(int mode in new[]{0,1,2,3,4,5}) {
                var a=players[0].Entity;var d=players[1].Entity;Attack packet;
                uint baseline=Hit(mode,a,d,out packet);
                bool magic=mode>=4;
                a.CriticalStrike=magic?0:10000;a.SkillCStrike=magic?10000:0;
                uint critical=Hit(mode,a,d,out packet);
                Check(Critical(packet) && Math.Abs(critical-baseline*(magic?2:1.5))<=6,"one correctly sized critical mode "+mode);
                d.Immunity=10000;
                Check(Hit(mode,a,d,out packet)==baseline && !Critical(packet),"immunity cancels critical mode "+mode);
                d.Immunity=0;a.CriticalStrike=magic?10000:0;a.SkillCStrike=magic?0:10000;
                Check(Hit(mode,a,d,out packet)==baseline && !Critical(packet),"attack uses the correct critical rating mode "+mode);
                a.CriticalStrike=a.SkillCStrike=0;
                d.Gems[7]=32;
                uint reduced=Hit(mode,a,d,out packet);
                Check(reduced>0 && Math.Abs(reduced-baseline*.68)<=6,"32 percent tortoise reduction mode "+mode);
                d.Gems[7]=0;d.Block=10000;
                uint blocked=Hit(mode,a,d,out packet);
                Check(blocked>0 && Math.Abs(blocked-baseline*.5)<=6 && (packet.Effect & Attack.AttackEffects.Block)!=0,"block halves damage without division by zero mode "+mode);
                d.Block=0;
                d.Level=160;
                uint highBp=Hit(mode,a,d,out packet);
                a.Breaktrough=1000;
                uint broken=Hit(mode,a,d,out packet);
                Check(broken>highBp && (packet.Effect & Attack.AttackEffects.Penetration)!=0,"break bypasses higher BP reduction mode "+mode);
                d.Counteraction=1000;
                uint counter=Hit(mode,a,d,out packet);
                Check(counter==highBp && (packet.Effect & Attack.AttackEffects.Penetration)==0,"anti-break cancels breakthrough mode "+mode);
                a.Breaktrough=d.Counteraction=0;d.Level=140;
                if(magic) {
                    d.MagicDefence=10000;uint flat=Hit(mode,a,d,out packet);
                    d.BaseMagicDefence=5000;uint chi=Hit(mode,a,d,out packet);
                    d.MagicDefencePercent=50;uint pct=Hit(mode,a,d,out packet);
                    Check(flat<baseline && chi<flat && pct<chi && pct>0,"flat, cultivation and percentage magic defense all mitigate mode "+mode);
                    d.MagicDefence=0;d.BaseMagicDefence=0;d.MagicDefencePercent=0;
                }
            }
            foreach(int strike in new[]{0,1,100,9999,10000,int.MaxValue})foreach(int immunity in new[]{0,1,100,10000,int.MaxValue}) {
                double chance=(double)Rule("CriticalChance",strike,immunity);
                Check(chance==Math.Max(0,Math.Min(100,((long)strike-immunity)/100.0)),"bounded exact critical-minus-immunity chance");
            }
            Check(!(bool)Rule("Roll",0.0) && (bool)Rule("Roll",100.0),"zero never procs; 100 always procs");
            Console.WriteLine("Combat checks passed: "+checks+" (600 class pairs plus critical, immunity, block, defense and breakthrough scenarios)");
            return 0;
        } catch(Exception ex) {Console.WriteLine(ex);return 1;}
    }
}
