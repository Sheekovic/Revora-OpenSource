using System;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Game.Attacking;
using Console=System.Console;

internal static partial class CheckCandidateArenaLifecycle {
    static bool legacyBossHitFixture;
    static void LegacyBossHitProcess(string phase) {
        legacyBossHitFixture=true;const uint uid=4294918901;bool baseline=phase.EndsWith("baseline"),reload=phase.EndsWith("reload");
        if((bool)Sql("SELECT to_regclass('revora_legacy_boss_hit_probe') IS NULL")) {
            Require(baseline && (long)Sql("SELECT count(*) FROM entities WHERE uid=@uid","uid",(long)uid)==0);
            nextUid=uid;Player(Kernel.Maps[1002]);
            Sql("CREATE TABLE revora_legacy_boss_hit_probe(baseline bigint,verified boolean NOT NULL DEFAULT false,hp bigint);INSERT INTO revora_legacy_boss_hit_probe VALUES(NULL,false,NULL)");
        }
        nextUid=uid;var p=Player(Kernel.Maps[1002],true);p.LoadItemStats();Server.GamePool=new[]{p};
        Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p) && p.Inventory.Count==0);
        if(reload) {
            Require((bool)Sql("SELECT verified FROM revora_legacy_boss_hit_probe") && p.Entity.Hitpoints==Convert.ToUInt32(Sql("SELECT hp FROM revora_legacy_boss_hit_probe")));
        } else {
            p.Entity.Teleport(1002,408,330);p.Attackable=true;uint originalMaximum=p.Entity.MaxHitpoints;p.Entity.MaxHitpoints=100000;
            var map=p.Map;var info=MonsterInformation.MonsterInformations[4171].Copy();info.SpellID=10001;
            var monster=new Entity(EntityFlag.Monster,false){UID=map.EntityUIDCounter.Next,MonsterInfo=info,MapID=map.ID,X=409,Y=330,Name="ChaosRipper",MapObjType=MapObjectType.Monster};
            info.Owner=monster;monster.MaxHitpoints=monster.Hitpoints=100000;map.AddEntity2(monster);
            try {
                if(baseline) {
                    Require(Sql("SELECT baseline FROM revora_legacy_boss_hit_probe")==DBNull.Value);
                    p.Entity.Hitpoints=100000;new Handle(null,monster,p.Entity);uint damage=100000-p.Entity.Hitpoints;
                    Console.WriteLine("[legacy-boss-hit] before-fix-chaos-damage="+damage);Require(damage>=2000);
                    Sql("UPDATE revora_legacy_boss_hit_probe SET baseline=@damage","damage",(long)damage);
                } else {
                    Require(Convert.ToUInt32(Sql("SELECT baseline FROM revora_legacy_boss_hit_probe"))>=2000);
                    monster.MagicAttack=5000; // Positive magic input for the generic spell fallback regression.
                    string[] names={"IceQueen","SnowBansheeSoul","PurpleBanshee","ChaosRipper","NemesisTyrant","DragonKing","FuriousBear","IceKing","GoldOctopus","ThunderDragon","FatDemon","LavaBeast","WhiteDragon","SunDevil"};
                    foreach(var name in names) {
                        uint min=uint.MaxValue,max=0;int raw=0,fallback=0;
                        for(int n=0;n<128 && raw<8;n++) {
                            monster.Name=info.Name=name;info.SpellID=10001;p.Entity.Hitpoints=100000;
                            new Handle(null,monster,p.Entity);uint damage=100000-p.Entity.Hitpoints;
                            bool scripted=info.SpellID==10001 || info.SpellID==30010 || info.SpellID==30011 || info.SpellID==30012
                                || ((name=="DragonKing" || name=="LavaBeast") && (info.SpellID==7014 || info.SpellID==7017 || info.SpellID==7013 || info.SpellID==7015))
                                || name=="WhiteDragon" || name=="SunDevil";
                            if(scripted) {Require(damage>=1000 && damage<2000 && !PirateFenced(p));raw++;min=Math.Min(min,damage);max=Math.Max(max,damage);}
                            else {if(damage==0)Console.WriteLine("[legacy-boss-hit] zero-fallback spell="+info.SpellID);Require(damage>0 && !PirateFenced(p));fallback++;}
                        }
                        Require(raw==8);Console.WriteLine("[legacy-boss-hit] single-hit="+name+" raw="+raw+" fallback="+fallback+" min="+min+" max="+max);
                    }
                    monster.Name=info.Name="OrdinaryProbe";info.SpellID=0;monster.MinAttack=monster.MaxAttack=10000;p.Entity.Hitpoints=100000;
                    new Handle(null,monster,p.Entity);Require(p.Entity.Hitpoints<100000 && !PirateFenced(p));
                    p.Entity.MaxHitpoints=originalMaximum;p.Entity.Hitpoints=originalMaximum;Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_legacy_boss_hit_probe SET verified=true,hp=@hp","hp",(long)p.Entity.Hitpoints);
                }
            } finally {map.RemoveEntity(monster);}
        }
        Console.WriteLine("[career-process] "+phase+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Legacy boss hit check did not close.");
    }
}
