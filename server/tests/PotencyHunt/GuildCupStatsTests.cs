using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static long[] CupSnapshot(GameState p) {
        var e=p.Entity;return new long[]{e.ItemHP,e.ItemMP,e.MinAttack,e.MaxAttack,e.MagicAttack,e.Defence,e.CriticalStrike,e.SkillCStrike,e.Immunity,e.Breaktrough,e.Counteraction,e.MaxHitpoints,e.MaxMana};
    }
    static long[] CupDelta(GameState p,long[] before){return CupSnapshot(p).Select((value,index)=>value-before[index]).ToArray();}
    static long[] CupExpected(uint id) {
        long hp=id==2100055?900:id==2100065?1200:id==2100095?1600:1500;
        long attack=id==2100075?1000:id==2100095?1100:0,defense=id==2100075?1000:id==2100095?1200:0;
        long crit=id==2100085?100:id==2100075 || id==2100095?300:0;
        return new[]{hp,hp,attack,attack,attack,defense,crit,crit,crit,crit/10,crit/10,hp,hp};
    }
    static uint CupStructureHit(int mode,GameState p,SobNpcSpawn target) {
        var packet=new Attack(true);var spell=new SpellInformation{Power=1000,PowerPercent=1};
        if(mode==0)return Revora.Game.Attacking.Calculate.Melee(p.Entity,target,ref packet);
        if(mode==1)return Revora.Game.Attacking.Calculate.Rango(p.Entity,target,ref packet);
        if(mode==2)return Revora.Game.Attacking.Calculate.Ranged(p.Entity,target,ref packet);
        return Revora.Game.Attacking.Calculate.Magic(p.Entity,target,spell,ref packet);
    }
    static void VerifyGuildCupStats() {
        uint[] ids={2100055,2100065,2100075,2100085,2100095};
        foreach(uint id in ids) {
            var info=ConquerItemInformation.BaseInformations[id];var expected=CupExpected(id);
            Check(info.ItemHP==expected[0] && info.ItemMP==expected[1] && info.MinAttack==expected[2] && info.MaxAttack==expected[3]
                && info.MagicAttack==expected[4] && info.PhysicalDefence==expected[5] && info.CriticalStrike==expected[6] && info.SkillCStrike==expected[7]
                && info.Immunity==expected[8] && info.BreakThrough==expected[9] && info.CounterAction==expected[10],
                "production catalog parses the installed 6711 cup fields, including independent skill critical: "+id);
        }
        foreach(byte cls in new byte[]{15,25,45,55,65,75,85,135,145,165}) {
            var p=TravelPlayer(Kernel.Maps[1002]);p.Entity.Class=cls;p.Entity.NobilityRank=Revora.Game.ConquerStructures.NobilityRank.Serf;
            p.LoadItemStats();var baseline=CupSnapshot(p);
            foreach(uint id in ids) {
                var item=new ConquerItem(true){ID=id,UID=ConquerItem.ItemUID.Next,Position=7,Durability=10000,MaximDurability=10000,StackSize=1,Bound=true};
                p.Equipment.Objects[6]=item;p.LoadItemStats();var delta=CupDelta(p,baseline);
                Check(delta.SequenceEqual(CupExpected(id)),"class "+cls+" receives exactly the installed cup bonuses once: "+id+" ["+string.Join(",",delta)+"]");
                var once=CupSnapshot(p);p.LoadItemStats();p.LoadItemStats();Check(once.SequenceEqual(CupSnapshot(p)),"repeated cup recalculation cannot accumulate HP, mana or combat bonuses: "+cls+"/"+id);
            }
            p.Equipment.Objects[6]=null;p.LoadItemStats();Check(CupSnapshot(p).SequenceEqual(baseline),"removing the cup restores the class "+cls+" baseline without a lingering Gold modifier");
        }
        var sets=TravelPlayer(Kernel.Maps[1002]);sets.LoadItemStats();var empty=CupSnapshot(sets);
        var primary=new ConquerItem(true){ID=2100075,UID=ConquerItem.ItemUID.Next,Position=7,Durability=10000,StackSize=1};
        var secondary=new ConquerItem(true){ID=2100085,UID=ConquerItem.ItemUID.Next,Position=27,Durability=10000,StackSize=1};
        sets.Equipment.Objects[6]=primary;sets.Equipment.Objects[26]=secondary;sets.LoadItemStats();
        Check(CupDelta(sets,empty).SequenceEqual(CupExpected(2100075)),"inactive alternate trophy cannot stack with the primary Gold Prize");
        sets.AlternateEquipment=true;sets.LoadItemStats();Check(CupDelta(sets,empty).SequenceEqual(CupExpected(2100085)),"alternate equipment selects the trophy's own bonuses without the primary Gold Prize");
        sets.Equipment.Objects[26]=null;sets.LoadItemStats();Check(CupDelta(sets,empty).SequenceEqual(CupExpected(2100075)),"empty alternate cup slot uses the existing primary-slot fallback once");
        primary.Durability=0;sets.LoadItemStats();Check(CupSnapshot(sets).SequenceEqual(empty),"broken seasonal cup contributes no catalog bonus");
        var wearing=TravelPlayer(Kernel.Maps[1002]);var bare=TravelPlayer(Kernel.Maps[1002]);primary.Durability=10000;primary.Position=7;wearing.Equipment.Objects[6]=primary;
        foreach(var p in new[]{wearing,bare}) {
            p.Entity.Class=15;p.LoadItemStats();p.Entity.MinAttack=p.Entity.MaxAttack=40000;p.Entity.MagicAttack=30000;
            p.Entity.CriticalStrike=p.Entity.SkillCStrike=p.Entity.Immunity=0;p.Entity.Gems=new ushort[10];
        }
        var target=new SobNpcSpawn{UID=990123,MapID=1002,X=317,Y=323,Hitpoints=1000000,MaxHitpoints=1000000};
        for(int mode=0;mode<4;mode++)Check(CupStructureHit(mode,wearing,target)==CupStructureHit(mode,bare,target),
            "equal calculated attack stats deal equal structure damage without a hidden Gold Prize multiplier: mode "+mode);
    }
}
