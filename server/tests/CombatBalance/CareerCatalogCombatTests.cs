using System;
using System.Linq;
using System.Reflection;
using Npgsql;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Game.Attacking;
using Revora.Network.GamePackets;
using Console=System.Console;
internal static class CareerCatalogCombatTests
{
    static readonly BindingFlags Hidden=BindingFlags.Static|BindingFlags.NonPublic;
    static object Fixture(string name,params object[] args){return typeof(CompanionTests).GetMethod(name,Hidden).Invoke(null,args);}
    public static int Main(){
        try {
            string input=Environment.GetEnvironmentVariable("REVORA_QUARANTINE_TEST_CONNECTION");
            if(string.IsNullOrEmpty(input))throw new Exception("Explicit disposable connection required.");
            var connection=new NpgsqlConnectionStringBuilder(input);
            if(connection.Host!="127.0.0.1" || connection.Port!=25439 || connection.Database!="revora_candidate_game")throw new Exception("Disposable loopback catalog required.");
            DataHolder.ConnectionString=connection.ConnectionString;
            MonsterInformation.Load();SpellTable.Load();
            Fixture("Reset");var map=(Map)Fixture("World",(ushort)1002);
            var p=(GameState)Fixture("Player",map,false,(ushort)20,(ushort)20,(byte)45);
            p.Entity.Level=140;p.Entity.Reborn=2;p.Entity.MinAttack=p.Entity.MaxAttack=25571;p.Entity.Gems[1]=180;
            p.Entity.NobilityRank=Revora.Game.ConquerStructures.NobilityRank.Serf;
            p.Entity.PhysicalDamageIncrease=0;p.Entity.CriticalStrike=0;
            p.Entity.Defence=0;p.Entity.MagicDefence=0;p.Entity.BaseMagicDefence=0;p.Entity.MagicDefencePercent=0;
            p.Entity.MaxHitpoints=30163;p.Entity.Hitpoints=23960;
            int cases=0;
            var ids=Enumerable.Range(3000,25).Concat(Enumerable.Range(3141,9)).Concat(new[]{3155,3156,18,20,57,58,7006}).Concat(Enumerable.Range(2133,7));
            foreach(int id in ids) {
                var info=MonsterInformation.MonsterInformations[(uint)id].Copy();
                var mob=(Entity)Fixture("Monster",map,(ushort)21,(ushort)20,10u);
                mob.MonsterInfo=info;info.Owner=mob;mob.Name=info.Name;mob.Level=info.Level;
                mob.MinAttack=mob.MaxAttack=info.MaxAttack;mob.MagicAttack=info.MaxAttack;mob.Defence=info.Defence;
                mob.MaxHitpoints=mob.Hitpoints=info.Hitpoints;mob.ApplyMonsterBossHud();
                var packet=new Attack(true);uint damage=Calculate.Rango(p.Entity,mob,ref packet);
                packet=new Attack(true);uint melee=Calculate.Melee(mob,p.Entity,ref packet),magic=0;
                if(info.SpellID!=0){packet=new Attack(true);magic=Calculate.Magic(mob,p.Entity,info.SpellID,0,ref packet);}
                uint incoming=Math.Max(melee,magic);
                if(damage==0 || incoming>=23960 || info.AttackSpeed<1000)throw new Exception("Catalog fight needs separate survival review: "+id);
                long shots=((long)mob.MaxHitpoints+damage-1)/damage;
                long health=23960,minimum=health;int potions=0;
                // One incoming maximum hit per second, even for slower enemies;
                // one ranged shot and optional7000HP medicine in separate500ms slots.
                for(long second=0;second<shots;second++) {
                    health-=incoming;minimum=Math.Min(minimum,health);
                    if(health<=0)throw new Exception("Per-pull survival failed: "+id);
                    if(health+7000<=30163){health+=7000;potions++;}
                }
                if(potions>100 || shots*6>=4097)throw new Exception("Per-pull maintenance exceeds reviewed stock: "+id);
                Console.WriteLine("[career-catalog] "+id+" "+mob.MaxHitpoints+" "+damage+" "+incoming+" "+shots+" "+info.SpellID+" "+info.AttackSpeed+" "+potions+" "+minimum);
                cases++;
            }
            Console.WriteLine("PASS: "+cases+" loaded catalog families fit independent one-enemy survival and maintenance bounds; not crowd, movement, full-hunt or native acceptance.");
            return 0;
        }catch(Exception e){Console.WriteLine(e.GetType().Name+": "+e.Message);return 1;}
    }
}
