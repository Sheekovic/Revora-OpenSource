using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Game.Attacking;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static GameState AbyssPvpPlayer(Map town,Map map) {
        var p=SecondPlayer(town);AbyssAt(p,map,3601);p.Attackable=true;p.Entity.PKMode=Enums.PKMode.PK;
        return p;
    }
    static void VerifyAbyssPvp(string runtime,Map town,Map map) {
        Query("CREATE TABLE IF NOT EXISTS enemy (LIKE public.enemy INCLUDING ALL); CREATE TABLE IF NOT EXISTS pkexploits (LIKE public.pkexploits INCLUDING ALL)");
        var jail=World(runtime,6000);Kernel.Maps[6000]=jail;
        var killer=AbyssPvpPlayer(town,map);var victim=AbyssPvpPlayer(town,map);
        Check(AbyssRead(killer)==null && AbyssRead(victim)==null && Handle.CanAttack(killer.Entity,victim.Entity,null,true)
            && !killer.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.FlashingName),
            "public Abyss permits ordinary PvP without flashing-name crime, even without a selected quest");
        killer.Entity.PKMode=Enums.PKMode.Peace;
        Check(!Handle.CanAttack(killer.Entity,victim.Entity,null,true),"Abyss free-PK rules still respect Peace mode");
        killer.Entity.PKMode=Enums.PKMode.PK;victim.Attackable=false;
        Check(!Handle.CanAttack(killer.Entity,victim.Entity,null,true),"Abyss free-PK rules retain normal attackability protection");victim.Attackable=true;
        uint itemUid=Pack(victim,722723,3).UID;victim.Entity.Die(killer.Entity);
        Check(victim.Entity.Dead && killer.Entity.PKPoints==0 && !killer.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.FlashingName)
            && !victim.Enemy.ContainsKey(killer.Entity.UID) && killer.Entity.PkExplorer.Count==0
            && Scalar("SELECT count(*) FROM enemy WHERE entityid=@uid",victim.Entity.UID)==0
            && Scalar("SELECT count(*) FROM pkexploits WHERE uid=@uid",killer.Entity.UID)==0,
            "actual Abyss PvP death writes no PK points, enemy or kill record");
        EntityTable.SaveEntity(victim);victim=ReloadBall(victim);SkillTable.LoadProficiencies(victim);Revora.Database.KnownPersons.LoadEnemy(victim);
        Check(victim.Inventory.ContainsUID(itemUid) && !victim.Enemy.ContainsKey(killer.Entity.UID),"Abyss death inventory and empty enemy record survive production reconnect");
        // Exercise the real high-PK drop branch with every quest material eligible
        // for its legacy random drop gate. No test-only drop implementation.
        var wanted=AbyssPvpPlayer(town,map);foreach(var item in wanted.Inventory.Objects.ToArray())DisRemove(wanted,item);
        for(uint type=722721;type<=722731;type++) {
            var item=Pack(wanted,type,1);item.UnlockEnd=DateTime.Now.AddDays(3);
            Check((bool)Call(AbyssWorld,null,"ProtectDeathItem",wanted.Entity,type),"Abyss protects quest material on death: "+type);
        }
        while(wanted.Inventory.Count<40){var item=Pack(wanted,722723,1);item.UnlockEnd=DateTime.Now.AddDays(3);}
        var ids=wanted.Inventory.Objects.Select(i=>i.UID).OrderBy(id=>id).ToArray();wanted.Entity.PKPoints=100;killer.Entity.Grabber=100;
        wanted.Entity.Die(killer.Entity);
        Check(wanted.Entity.Dead && wanted.Inventory.Objects.Select(i=>i.UID).OrderBy(id=>id).SequenceEqual(ids)
            && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND warehouse=0 AND position=0",wanted.Entity.UID)==40,
            "actual black-name Abyss PvP death retains all quest items under guaranteed ordinary drop rolls");
        Check(!killer.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.FlashingName) && killer.Entity.PKPoints==0
            && !wanted.Enemy.ContainsKey(killer.Entity.UID),"prior black-name status cannot turn an Abyss kill into a crime record");
        EntityTable.SaveEntity(wanted);wanted=ReloadBall(wanted);SkillTable.LoadProficiencies(wanted);
        Check(wanted.Inventory.Objects.Select(i=>i.UID).OrderBy(id=>id).SequenceEqual(ids),"all protected high-PK quest items survive production reconnect");
        var hunted=AbyssPvpPlayer(town,map);foreach(var item in hunted.Inventory.Objects.ToArray())DisRemove(hunted,item);
        for(int n=0;n<40;n++){var item=Pack(hunted,722721u+(uint)(n%11),1);item.UnlockEnd=DateTime.Now.AddDays(3);}
        hunted.Entity.PKPoints=100;var demon=AbyssWild(hunted,3600);demon.Grabber=100;hunted.Entity.Die(demon);map.RemoveEntity(demon);
        Check(hunted.Entity.Dead && hunted.Inventory.Count==40 && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND position=0 AND warehouse=0",hunted.Entity.UID)==40,
            "actual Abyss monster death also preserves quest materials at high PK points");
        var ordinary=AbyssPvpPlayer(town,map);foreach(var item in ordinary.Inventory.Objects.ToArray())DisRemove(ordinary,item);
        for(int n=0;n<40;n++){var item=Pack(ordinary,1000000,1);item.UnlockEnd=DateTime.Now.AddDays(3);}
        ordinary.Entity.PKPoints=100;ordinary.Entity.Die(killer.Entity);
        Check(ordinary.Entity.Dead && ordinary.Inventory.Count<40,"Abyss protects quest materials without disabling ordinary item drops");
        var scoped=AbyssPvpPlayer(town,map);
        Check(!(bool)Call(AbyssWorld,null,"ProtectDeathItem",scoped.Entity,722720u)
            && !(bool)Call(AbyssWorld,null,"ProtectDeathItem",scoped.Entity,722732u),"Abyss death protection does not cover unrelated items");
        scoped.Entity.MapID=1002;
        Check(!(bool)Call(AbyssWorld,null,"ProtectDeathItem",scoped.Entity,722723u),"Abyss materials retain ordinary death rules outside the map");
        scoped.Entity.MapID=1700;map.BaseID=1002;
        try {Check(!(bool)Call(AbyssWorld,null,"SuppressCrime",killer.Entity,scoped.Entity),"altered map identity cannot borrow Abyss crime immunity");}
        finally {map.BaseID=1700;}
        Kernel.GamePool.Remove(scoped.Entity.UID);
        Check(!(bool)Call(AbyssWorld,null,"ProtectDeathItem",scoped.Entity,722723u),"superseded sessions cannot borrow Abyss item protection");
        var outside=SecondPlayer(town);var attacker=SecondPlayer(town);outside.Attackable=attacker.Attackable=true;
        Check(!(bool)Call(AbyssWorld,null,"SuppressCrime",attacker.Entity,outside.Entity),"Abyss crime suppression does not change other public maps");
        foreach(var item in outside.Inventory.Objects.ToArray())DisRemove(outside,item);
        for(int n=0;n<40;n++){var item=Pack(outside,722723,1);item.UnlockEnd=DateTime.Now.AddDays(3);}
        outside.Entity.PKPoints=100;attacker.Entity.Grabber=100;outside.Entity.DropRandomStuff(attacker.Entity);
        Check(outside.Inventory.Count<40,"positive control: the same eligible quest materials still drop outside Abyss");
    }
}
