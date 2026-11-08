using System;
using System.Reflection;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Game.Attacking;
using Revora.Network.GamePackets;
using Console=System.Console;
internal static class UnarmedExtractionBudgetTests {
    static object Fixture(string name,params object[] args){return typeof(CompanionTests).GetMethod(name,BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,args);}
    public static int Main(){try {
        ConquerItemInformation.Load();Fixture("Reset");
        var map=(Map)Fixture("World",(ushort)1002);
        var p=(GameState)Fixture("Player",map,false,(ushort)20,(ushort)20,(byte)45);
        uint attack=0,hp=0;
        // Earned fresh paid Archer kit non-hand types: no weapon, vitality, fan or Chi bonuses. Permanent P7 souls are part of this earned kit.
        foreach(uint id in new uint[]{113309,120269,133309,150269,160249}){
            var item=new ConquerItemInformation(id,12);
            attack+=item.BaseInformation.MinAttack+item.PlusInformation.MinAttack;
            hp+=(uint)(item.BaseInformation.ItemHP+item.PlusInformation.ItemHP);
        }
        foreach(uint id in new uint[]{820073,821033,822071,823058,824018}){
            var soul=ConquerItemInformation.BaseInformations[id];attack+=soul.MinAttack;hp+=soul.ItemHP;
        }
        // The earned profile has30163HP. Check the removed bow/arrow/soul HP components.
        uint handHp=0;
        foreach(uint id in new uint[]{500429,1050002,800618}){
            var hand=new ConquerItemInformation(id,(byte)(id==500429?12:0));
            handHp+=(uint)(hand.BaseInformation.ItemHP+hand.PlusInformation.ItemHP);
        }
        Console.WriteLine("[unarmed-hand-hp] "+handHp);
        if(handHp!=0)throw new Exception("Hand removal needs full HP reconciliation.");
        hp=23960; // Below the earned30163 maximum; hands have no HP or HP gems.
        p.Entity.Level=140;p.Entity.Reborn=2;p.Entity.MinAttack=p.Entity.MaxAttack=attack;
        p.Entity.Gems[1]=150;p.Entity.PhysicalDamageIncrease=0;p.Entity.CriticalStrike=0;
        p.Entity.NobilityRank=Revora.Game.ConquerStructures.NobilityRank.Serf;
        p.Entity.Defence=0;p.Entity.MagicDefence=0;p.Entity.BaseMagicDefence=0;
        p.Entity.PhysicalDamageDecrease=0;p.Entity.MaxHitpoints=p.Entity.Hitpoints=hp;
        if(p.Equipment.TryGetItem(4)!=null || p.Equipment.TryGetItem(5)!=null)throw new Exception("Fixture has a weapon.");
        var mob=(Entity)Fixture("Monster",map,(ushort)21,(ushort)20,10u);
        mob.MonsterInfo.ID=9913002;mob.MonsterInfo.Boss=true;mob.MonsterInfo.Level=mob.Level=140;
        mob.MonsterInfo.SpellID=0;mob.MonsterInfo.AttackSpeed=2000;
        mob.MaxHitpoints=mob.Hitpoints=5000000;mob.Defence=3000;
        mob.MinAttack=mob.MaxAttack=5000;mob.MagicAttack=5000;mob.ApplyMonsterBossHud();
        var packet=new Attack(true);uint outgoing=Calculate.Melee(p.Entity,mob,ref packet);
        // ExpeditionField.TryHit supplies direct4000..5500 damage before ordinary PvM math.
        uint incoming=5500;
        Console.WriteLine("[unarmed-input] "+attack+" "+hp+" "+outgoing+" "+incoming);
        if(outgoing==0 || hp<=incoming)throw new Exception("Insufficient basic unarmed stats.");
        // Verified paid bow profile weakens the guardian, leaving it alive.
        p.Entity.MinAttack=p.Entity.MaxAttack=25571;p.Entity.Gems[1]=180;
        packet=new Attack(true);uint armed=Calculate.Rango(p.Entity,mob,ref packet);
        long armedShots=(5000000L-1)/armed;
        long remaining=5000000L-armedShots*armed;
        p.Entity.MinAttack=p.Entity.MaxAttack=attack;p.Entity.Gems[1]=150;
        long unarmedShots=(remaining+outgoing-1)/outgoing;
        long shots=armedShots+unarmedShots,health=hp,minimum=hp;int medicines=0;
        // Include four seconds to remove both hands; heal after each hit even when mostly wasted.
        // One incoming hit each second; real guardian interval is2seconds.
        for(long second=0;second<shots+4;second++){
            health-=incoming;minimum=Math.Min(minimum,health);
            if(health<=0)throw new Exception("Survival failed.");
            if(health<hp){health=Math.Min((long)hp,health+7000);medicines++;}
        }
        if(medicines>100 || shots*6>=4097 || shots+180+10+4>=600)throw new Exception("Stock, wear or event window exceeded.");
        Console.WriteLine("[unarmed-sequence] "+armed+" "+armedShots+" "+remaining+" "+unarmedShots);
        Console.WriteLine("[unarmed-budget] "+attack+" "+hp+" "+outgoing+" "+incoming+" "+shots+" "+medicines+" "+minimum);
        Console.WriteLine("PASS: weapon-assisted unarmed final blow fits independent guardian combat budget;180seconds is a travel allowance, not native movement evidence.");return 0;
    }catch(Exception e){Console.WriteLine(e.GetType().Name+": "+e.Message);return 1;}}
}
