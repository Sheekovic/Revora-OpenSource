using System;
using System.Reflection;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Game.Attacking;
using Revora.Network.GamePackets;
using Console=System.Console;

internal static partial class CheckCandidateArenaLifecycle {
    static bool legacyBossAreaFixture;
    static void LegacyBossAreaProcess(bool baseline) {
        legacyBossAreaFixture=true;const uint first=4294919001;
        if((bool)Sql("SELECT to_regclass('revora_legacy_boss_area_probe') IS NULL")) {
            Require(baseline && (long)Sql("SELECT count(*) FROM entities WHERE uid BETWEEN 4294919001 AND 4294919002")==0);
            for(uint uid=first;uid<=first+1;uid++){nextUid=uid;Player(Kernel.Maps[1002]);}
            Sql("CREATE TABLE revora_legacy_boss_area_probe(protected_damage bigint,verified boolean NOT NULL DEFAULT false);INSERT INTO revora_legacy_boss_area_probe VALUES(NULL,false)");
        }
        nextUid=first;var primary=Player(Kernel.Maps[1002],true);var nearby=Player(Kernel.Maps[1002],true);primary.LoadItemStats();nearby.LoadItemStats();Server.GamePool=new[]{primary,nearby};
        uint primaryMax=primary.Entity.MaxHitpoints,nearbyMax=nearby.Entity.MaxHitpoints;
        primary.Entity.Teleport(1002,408,330);nearby.Entity.Teleport(1002,410,330);primary.Attackable=true;
        var map=primary.Map;var info=MonsterInformation.MonsterInformations[4171].Copy();
        var monster=new Entity(EntityFlag.Monster,false){UID=map.EntityUIDCounter.Next,MonsterInfo=info,MapID=map.ID,X=409,Y=330,Name="IceQueen",MapObjType=MapObjectType.Monster};
        info.Owner=monster;monster.MaxHitpoints=monster.Hitpoints=100000;map.AddEntity2(monster);
        // A controlled view models nearby and stale client-screen entries. Gameplay eligibility must still be checked.
        var objects=typeof(Screen).GetField("_objects",BindingFlags.NonPublic|BindingFlags.Instance);
        Action<bool> hit=(includePrimary)=>{
            primary.Entity.MaxHitpoints=nearby.Entity.MaxHitpoints=100000;
            int attempts=0;
            do {
                primary.Entity.Hitpoints=nearby.Entity.Hitpoints=100000;nearby.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.ChaosCycle);
                info.SpellID=30011;objects.SetValue(primary.Screen,includePrimary?new Revora.Interfaces.IMapObject[]{nearby.Entity,primary.Entity}:new Revora.Interfaces.IMapObject[]{nearby.Entity});
                new Handle(null,monster,primary.Entity);attempts++;
            } while(info.SpellID!=30011 && attempts<64);
            Require(info.SpellID==30011 && !PirateFenced(primary) && !PirateFenced(nearby));
        };
        try {
            nearby.Attackable=false;hit(false);uint protectedDamage=100000-nearby.Entity.Hitpoints;
            Console.WriteLine("[legacy-boss-area] protected-damage="+protectedDamage);
            if(baseline){Require(protectedDamage>=1000 && protectedDamage<2000);Sql("UPDATE revora_legacy_boss_area_probe SET protected_damage=@damage","damage",(long)protectedDamage);}
            else {
                Require(Convert.ToUInt32(Sql("SELECT protected_damage FROM revora_legacy_boss_area_probe"))>=1000 && protectedDamage==0 && !nearby.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.ChaosCycle));
                nearby.Attackable=true;hit(true);uint primaryDamage=100000-primary.Entity.Hitpoints,secondaryDamage=100000-nearby.Entity.Hitpoints;
                Require(primaryDamage>=1000 && primaryDamage<2000 && secondaryDamage==primaryDamage && nearby.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.ChaosCycle));
                Require((bool)Call(Feature("HealthBalance"),"Managed",nearby));
                using(var entered=new System.Threading.ManualResetEventSlim(false))
                using(var release=new System.Threading.ManualResetEventSlim(false)) {
                    var owner=System.Threading.Tasks.Task.Run(()=>{lock(nearby.ItemSyncRoot){entered.Set();if(!release.Wait(10000))throw new TimeoutException("Area fixture lock release timed out.");}});
                    try {
                        Require(entered.Wait(5000));hit(false);
                        Console.WriteLine("[legacy-boss-area] busy-secondary-damage="+(100000-nearby.Entity.Hitpoints));
                        Require(primary.Entity.Hitpoints<100000 && nearby.Entity.Hitpoints==100000 && !nearby.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.ChaosCycle));
                    } finally {release.Set();Require(owner.Wait(5000));}
                }
                hit(false);Require(nearby.Entity.Hitpoints==primary.Entity.Hitpoints && nearby.Entity.Hitpoints<100000 && nearby.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.ChaosCycle));
                nearby.BindWorldMap(1011);nearby.Entity.X=410;nearby.Entity.Y=330;hit(false);
                Require(nearby.Entity.Hitpoints==100000 && !nearby.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.ChaosCycle));
                nearby.Entity.Teleport(1002,410,330);GameState removed;Require(Kernel.GamePool.TryRemove(nearby.Entity.UID,out removed));
                try{hit(false);Require(nearby.Entity.Hitpoints==100000 && !nearby.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.ChaosCycle));}
                finally{Kernel.GamePool[nearby.Entity.UID]=nearby;}
                Sql("UPDATE revora_legacy_boss_area_probe SET verified=true");
            }
            primary.Entity.MaxHitpoints=primaryMax;primary.Entity.Hitpoints=primaryMax;nearby.Entity.MaxHitpoints=nearbyMax;nearby.Entity.Hitpoints=nearbyMax;
            Require(EntityTable.SaveEntity(primary) && EntityTable.SaveEntity(nearby));
        } finally {map.RemoveEntity(monster);objects.SetValue(primary.Screen,new Revora.Interfaces.IMapObject[0]);}
        Console.WriteLine("[career-process] career-legacy-boss-area-"+(baseline?"baseline":"verify")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Legacy boss area check did not close.");
    }
}
