using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
internal static partial class CheckCandidateArenaLifecycle
{
    static bool totalBonusFixture;
    static ConquerItem TotalBonusGive(GameState p,uint type,byte plus){var info=ConquerItemInformation.BaseInformations[type];var item=new ConquerItem(true){ID=type,Plus=plus,Bound=true,StackSize=1,Durability=info.Durability,MaximDurability=info.Durability};Require(p.Inventory.Add(item,Revora.Game.Enums.ItemUse.CreateAndAdd));return item;}
    static void TotalBonusProcess(bool reload){
        totalBonusFixture=true;
        if((bool)Sql("SELECT to_regclass('revora_total_bonus_probe') IS NULL")){Require(!reload);Sql("CREATE TABLE revora_total_bonus_probe(uid bigint PRIMARY KEY,snapshot text)");}
        uint[] stages={602,706,802,902,1007},prizes={730002,730003,730003,3005128,3005129};int[] thresholds={15,50,70,100,120};
        for(int n=0;n<5;n++){
            uint uid=4294912401u+(uint)n;bool exists=(long)Sql("SELECT count(*) FROM revora_total_bonus_probe WHERE uid=@uid","uid",(long)uid)==1;
            if(!exists){Require(!reload && (long)Sql("SELECT count(*) FROM entities WHERE uid=@uid","uid",(long)uid)==0);nextUid=uid;Player(Kernel.Maps[1002]);Sql("UPDATE entities SET level=130,reborn=2,class=25,strength=500,agility=500 WHERE uid=@uid","uid",(long)uid);}
            nextUid=uid;var p=Player(Kernel.Maps[1002],true);ChiTable.Load(p);WayofHeroes.Load(p);SkillTable.LoadProficiencies(p);p.LoadItemStats();Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
            if(!exists){
                var catalog=ConquerItemInformation.BaseInformations;p.AddProficiency(new Proficiency(true){ID=410,Level=20});
                int targetPlus=n==4?11:1,remaining=thresholds[n]-1-targetPlus;
                foreach(byte slot in new byte[]{12,10,11,1,2,3,4,5,6}){
                    uint type=slot==12?300000u:slot==10?201009u:slot==11?202009u:catalog.Values.Where(i=>(int)ItemHandler.GetPositionFromID(i.ID)==slot && i.ID%10==5 && i.Level<=130 && (i.Class==0 || i.Class>=20 && i.Class<=25) && i.Gender==0 && (slot!=4 || i.ID/1000==410)).OrderBy(i=>i.Level).ThenBy(i=>i.ID).First().ID;
                    byte plus=(byte)Math.Min(12,remaining);remaining-=plus;var gear=TotalBonusGive(p,type,plus);RetainedUpgradeUsage(p,gear.UID,slot,ItemUsage.Mode.EquipItem);Require(p.Equipment.TryGetItem(slot).UID==gear.UID);
                }
                Require(remaining==0);uint bootType=catalog.Values.Where(i=>i.ID/1000==160 && i.ID%10==5 && i.Level<=15).OrderBy(i=>i.ID).First().ID;
                var boots=TotalBonusGive(p,bootType,(byte)targetPlus);RetainedUpgradeUsage(p,boots.UID,8,ItemUsage.Mode.EquipItem);
                string below=CompositionClaimSnapshot(p);NobilityClaimPacket(p,stages[n]);Require(below==CompositionClaimSnapshot(p));
                var material=TotalBonusGive(p,n==4?730008u:730002u,n==4?(byte)8:(byte)2);CompositionClaimCompose(p,boots,material);Require(boots.Plus==targetPlus+1);
                RetainedUpgradeUsage(p,boots.UID,8,ItemUsage.Mode.UnequipItem);string bag=CompositionClaimSnapshot(p);NobilityClaimPacket(p,stages[n]);Require(bag==CompositionClaimSnapshot(p));
                RetainedUpgradeUsage(p,boots.UID,8,ItemUsage.Mode.EquipItem);Require(GeneralData.SwitchEquipment(p,true));string inactive=CompositionClaimSnapshot(p);NobilityClaimPacket(p,stages[n]);Require(inactive==CompositionClaimSnapshot(p));Require(GeneralData.SwitchEquipment(p,false));
                System.Threading.Tasks.Parallel.For(0,8,_=>NobilityClaimPacket(p,stages[n]));var reward=p.Inventory.Objects.Single();Require(reward.ID==prizes[n] && reward.Bound && reward.StackSize==1);
                if(n<3){byte plus=(byte)(prizes[n]-730000);Require(reward.Plus==plus);var sink=TotalBonusGive(p,bootType,0);CompositionClaimCompose(p,sink,reward);Require(sink.Plus==plus && sink.PlusProgress==0);}
                else{SoulClaimUse(p,reward);Require(p.ChiPoints==(n==3?2000u:3000u));}
                Require(EntityTable.SaveEntity(p));Sql("INSERT INTO revora_total_bonus_probe(uid,snapshot) VALUES(@uid,@snapshot)","uid",(long)uid,"snapshot",CompositionClaimSnapshot(p));
            }
            Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=@stage AND NOT legacy","uid",(long)uid,"stage",(int)stages[n])==1);
            Require((long)Sql("SELECT count(*) FROM revora_plus_compositions WHERE uid=@uid","uid",(long)uid)==(n<3?2:1));
            Require(p.Inventory.Count==(n<3?1:0) && (n<3 || p.ChiPoints==(n==3?2000u:3000u)));
            string exact=CompositionClaimSnapshot(p);Require(exact==Convert.ToString(Sql("SELECT snapshot FROM revora_total_bonus_probe WHERE uid=@uid","uid",(long)uid)));NobilityClaimPacket(p,stages[n]);Require(exact==CompositionClaimSnapshot(p) && !PirateFenced(p));
        }
        System.Console.WriteLine("[career-process] career-total-bonus-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Total bonus did not close.");
    }
}
