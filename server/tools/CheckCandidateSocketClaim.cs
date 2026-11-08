using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
internal static partial class CheckCandidateArenaLifecycle
{
    static bool socketClaimFixture;
    static string SocketClaimSnapshot(GameState p){return Convert.ToString(Sql(@"SELECT json_build_object('base',@snapshot::json,
'upgrades',(SELECT json_agg(r ORDER BY id) FROM revora_equipment_upgrades r WHERE uid=@uid),
'packs',(SELECT json_agg(r ORDER BY id) FROM revora_dis_refinery_packs r WHERE uid=@uid))::text","snapshot",SoulClaimSnapshot(p),"uid",(long)p.Entity.UID));}
    static void SocketClaimProcess(bool reload){
        socketClaimFixture=true;const uint uid=4294912201;
        if((bool)Sql("SELECT to_regclass('revora_socket_claim_probe') IS NULL")){
            Require(!reload && (long)Sql("SELECT count(*) FROM entities WHERE uid=@uid","uid",(long)uid)==0);
            nextUid=uid;Player(Kernel.Maps[1002]);Sql("UPDATE entities SET level=130,reborn=2,class=25,strength=500,agility=500,conquerpoints=500000,money=10000000 WHERE uid=@uid","uid",(long)uid);
            Sql("CREATE TABLE revora_socket_claim_probe(snapshot text);INSERT INTO revora_socket_claim_probe VALUES(NULL)");
        }
        nextUid=uid;var p=Player(Kernel.Maps[1002],true);ChiTable.Load(p);WayofHeroes.Load(p);SkillTable.LoadProficiencies(p);p.LoadItemStats();
        Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        if(Sql("SELECT snapshot FROM revora_socket_claim_probe")==DBNull.Value){
            Require(!reload);
            bool claimed=(long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=904","uid",(long)uid)==1;
            if(!claimed){Require(p.Inventory.Count==0);var catalog=ConquerItemInformation.BaseInformations;
            p.AddProficiency(new Proficiency(true){ID=410,Level=20});
            foreach(byte slot in new byte[]{1,2,3,4,5,6,10,11}){
                uint type=slot==10?201009u:slot==11?202009u:catalog.Values.Where(i=>(int)ItemHandler.GetPositionFromID(i.ID)==slot && i.ID%10==5 && i.Level<=130 && (i.Class==0 || i.Class>=20 && i.Class<=25) && i.Gender==0 && (slot!=4 || i.ID/1000==410)).OrderBy(i=>i.Level).ThenBy(i=>i.ID).First().ID;
                var info=catalog[type];var gear=new ConquerItem(true){ID=type,Bound=true,StackSize=1,Durability=info.Durability,MaximDurability=info.Durability,SocketOne=Revora.Game.Enums.Gem.EmptySocket,SocketTwo=Revora.Game.Enums.Gem.EmptySocket};
                Require(p.Inventory.Add(gear,Revora.Game.Enums.ItemUse.CreateAndAdd));RetainedUpgradeUsage(p,gear.UID,slot,ItemUsage.Mode.EquipItem);Require(p.Equipment.TryGetItem(slot).UID==gear.UID);
            }
            uint bootType=catalog.Values.Where(i=>i.ID/1000==160 && i.ID%10==5 && i.Level<=15 && catalog.ContainsKey(i.ID+1)).OrderBy(i=>i.ID).First().ID;
            var boots=RefineryClaimGive(p,bootType);string sixteen=SocketClaimSnapshot(p);NobilityClaimPacket(p,904);Require(sixteen==SocketClaimSnapshot(p));
            var upgrade=Feature("EquipmentUpgrades");var roll=upgrade.GetField("Roll",Hidden);var clock=upgrade.GetField("Clock",Hidden);object oldRoll=roll.GetValue(null),oldClock=clock.GetValue(null);DateTime now=DateTime.UtcNow;bool checkedSeventeen=false;
            try{roll.SetValue(null,(Func<int,int>)(max=>max-1));clock.SetValue(null,(Func<DateTime>)(()=>now));
                for(int n=0;n<40;n++){
                    CareerBuy(p,1088000);var db=p.Inventory.Objects.Single(i=>i.ID==1088000);now=now.AddSeconds(2);
                    RetainedUpgradeUsage(p,boots.UID,db.UID,ItemUsage.Mode.DragonBallUpgrade);Require(!p.Inventory.ContainsUID(db.UID) && boots.Durability<boots.MaximDurability);
                    RetainedUpgradeUsage(p,boots.UID,0,ItemUsage.Mode.Repair);Require(boots.Durability==boots.MaximDurability && !PirateFenced(p));
                    if(!checkedSeventeen && boots.SocketOne!=0 && boots.SocketTwo==0){RetainedUpgradeUsage(p,boots.UID,8,ItemUsage.Mode.EquipItem);string seventeen=SocketClaimSnapshot(p);NobilityClaimPacket(p,904);Require(seventeen==SocketClaimSnapshot(p));RetainedUpgradeUsage(p,boots.UID,8,ItemUsage.Mode.UnequipItem);checkedSeventeen=true;}
                }
            }finally{roll.SetValue(null,oldRoll);clock.SetValue(null,oldClock);}
            Require(checkedSeventeen && boots.SocketOne!=0 && boots.SocketTwo!=0);RetainedUpgradeUsage(p,boots.UID,8,ItemUsage.Mode.EquipItem);
            Require(GeneralData.SwitchEquipment(p,true));string inactive=SocketClaimSnapshot(p);NobilityClaimPacket(p,904);Require(inactive==SocketClaimSnapshot(p));Require(GeneralData.SwitchEquipment(p,false));
            System.Threading.Tasks.Parallel.For(0,8,_=>NobilityClaimPacket(p,904));var packs=p.Inventory.Objects.OrderBy(i=>i.UID).ToArray();Require(packs.Length==3 && packs.All(i=>i.ID==728919 && i.Bound));
            var refineryClock=Feature("RefineryProgress").GetField("Clock",Hidden);object oldRefineryClock=refineryClock.GetValue(null);DateTime refineryNow=DateTime.UtcNow;refineryClock.SetValue(null,(Func<DateTime>)(()=>refineryNow));
            var service=Feature("DisCityPacks");var draw=service.GetField("Roll",Hidden);object oldDraw=draw.GetValue(null);
            try{int[] qualityDraws={69,70,99};for(int n=0;n<3;n++){
                int quality=qualityDraws[n],level=quality<70?4:5;
                var pool=Kernel.DatabaseRefinery.Values.Where(i=>i.Level==level && !i.Untradable && catalog.ContainsKey(i.Identifier)).Select(i=>i.Identifier).Distinct().OrderBy(i=>i).ToArray();
                int index=Array.FindIndex(pool,id=>(bool)Call(Feature("RefineryProgress"),"Fits",boots,Kernel.DatabaseRefinery[id]));Require(index>=0);int call=0;draw.SetValue(null,(Func<int,int>)(max=>call++==0?quality:index));
                SoulClaimUse(p,packs[n]);var material=p.Inventory.Objects.Single(i=>i.ID==pool[index]);Require(material.Bound && Kernel.DatabaseRefinery[material.ID].Level==level);
                refineryNow=refineryNow.AddSeconds(2);var frame=RetainedArtifactPacket(boots.UID,material.UID);frame[4]=0;Purification.Process(frame,p);Require(!p.Inventory.ContainsUID(material.UID) && boots.RefineItem==pool[index] && !PirateFenced(p));
                string applied=SocketClaimSnapshot(p);Purification.Process(frame,p);Require(applied==SocketClaimSnapshot(p));
            }}finally{draw.SetValue(null,oldDraw);refineryClock.SetValue(null,oldRefineryClock);}
            }
            foreach(var remaining in p.Inventory.Objects.Where(i=>Kernel.DatabaseRefinery.ContainsKey(i.ID)).ToArray()){
                var target=p.Equipment.TryGetItem(8);var frame=RetainedArtifactPacket(target.UID,remaining.UID);frame[4]=0;Purification.Process(frame,p);Require(!p.Inventory.ContainsUID(remaining.UID) && target.RefineItem==remaining.ID && !PirateFenced(p));
                string applied=SocketClaimSnapshot(p);Purification.Process(frame,p);Require(applied==SocketClaimSnapshot(p));
            }
            Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_socket_claim_probe SET snapshot=@snapshot","snapshot",SocketClaimSnapshot(p));
        }
        Require(p.Inventory.Count==0 && !PirateFenced(p));
        Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=904 AND NOT legacy","uid",(long)uid)==1);
        Require((long)Sql("SELECT count(*) FROM revora_dis_refinery_packs WHERE uid=@uid AND pack_type=728919","uid",(long)uid)==3);
        Require((long)Sql("SELECT count(*) FROM revora_equipment_upgrades WHERE uid=@uid AND operation=3","uid",(long)uid)==40);
        string exact=SocketClaimSnapshot(p);Require(exact==Convert.ToString(Sql("SELECT snapshot FROM revora_socket_claim_probe")));NobilityClaimPacket(p,904);Require(exact==SocketClaimSnapshot(p));
        System.Console.WriteLine("[career-process] career-socket-claim-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Socket claim did not close.");
    }
}
