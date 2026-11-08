using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
internal static partial class CheckCandidateArenaLifecycle
{
    static bool battlePowerClaimFixture;
    static string BattlePowerClaimSnapshot(GameState p){return Convert.ToString(Sql(@"SELECT json_build_object('base',@base::json,
'effects',(SELECT json_agg(a ORDER BY a.uid,a.addingtype) FROM itemadding a JOIN items i ON i.uid=a.uid WHERE i.entityid=@uid))::text","base",CompositionClaimSnapshot(p),"uid",(long)p.Entity.UID));}
    static ConquerItem BattlePowerGear(GameState p,uint type,byte plus,byte gem){var info=ConquerItemInformation.BaseInformations[type];var item=new ConquerItem(true){ID=type,Plus=plus,Bound=true,StackSize=1,SocketOne=(Enums.Gem)gem,SocketTwo=(Enums.Gem)gem,Durability=info.Durability,MaximDurability=info.Durability};Require(p.Inventory.Add(item,Enums.ItemUse.CreateAndAdd));return item;}
    static void BattlePowerClaimProcess(bool reload){
        battlePowerClaimFixture=true;const uint uid=4294912701;
        if((bool)Sql("SELECT to_regclass('revora_battle_power_claim_probe') IS NULL")){
            Require(!reload && (long)Sql("SELECT count(*) FROM entities WHERE uid=@uid","uid",(long)uid)==0);nextUid=uid;Player(Kernel.Maps[1002]);
            Sql("UPDATE entities SET level=140,reborn=2,class=25,strength=500,agility=500 WHERE uid=@uid","uid",(long)uid);Sql("CREATE TABLE revora_battle_power_claim_probe(snapshot text);INSERT INTO revora_battle_power_claim_probe VALUES(NULL)");
        }
        nextUid=uid;var p=Player(Kernel.Maps[1002],true);ChiTable.Load(p);WayofHeroes.Load(p);SkillTable.LoadProficiencies(p);p.LoadItemStats();Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        if(Sql("SELECT snapshot FROM revora_battle_power_claim_probe")==DBNull.Value){Require(!reload);
            var catalog=ConquerItemInformation.BaseInformations;p.AddProficiency(new Proficiency(true){ID=410,Level=20});p.AddProficiency(new Proficiency(true){ID=900,Level=20});
            foreach(byte slot in new byte[]{1,2,3,4,5,6,10,11,12,18}){
                if(p.Equipment.TryGetItem(slot)!=null)continue;
                uint type=slot==12?300000u:slot==10?201009u:slot==11?202009u:catalog.Values.Where(i=>(int)ItemHandler.GetPositionFromID(i.ID)==slot && i.ID%10==9 && i.Level<=140 && (i.Class==0 || i.Class>=20 && i.Class<=25) && i.Gender==0 && (slot!=4 || i.ID/1000==410)).OrderBy(i=>i.Level).ThenBy(i=>i.ID).First().ID;
                var gear=p.Inventory.Objects.FirstOrDefault(i=>i.ID==type) ?? BattlePowerGear(p,type,12,slot==12 || slot==18?(byte)0:slot==10?(byte)103:slot==11?(byte)123:(byte)13);RetainedUpgradeUsage(p,gear.UID,slot,ItemUsage.Mode.EquipItem);System.Console.WriteLine("[bp-fixture] slot="+slot+" type="+type+" bp="+p.Entity.BattlePower);Require(p.Equipment.TryGetItem(slot)!=null && p.Equipment.TryGetItem(slot).UID==gear.UID);
            }
            var boots=p.Equipment.TryGetItem(8);if(boots==null && p.Entity.BattlePower>340){
                var crop=p.Equipment.TryGetItem(18);int adjusted=crop.Plus-(p.Entity.BattlePower-340);Require(adjusted>=0 && adjusted<=12);
                RetainedUpgradeUsage(p,crop.UID,18,ItemUsage.Mode.UnequipItem);var replacement=BattlePowerGear(p,crop.ID,(byte)adjusted,0);RetainedUpgradeUsage(p,replacement.UID,18,ItemUsage.Mode.EquipItem);
            }
            if(boots==null){int plus=349-p.Entity.BattlePower-8;System.Console.WriteLine("[bp-fixture] before-boots="+p.Entity.BattlePower+" needed-plus="+plus);Require(plus>=0 && plus<12);boots=BattlePowerGear(p,160249,(byte)plus,13);RetainedUpgradeUsage(p,boots.UID,8,ItemUsage.Mode.EquipItem);}
            if(boots.Plus==1){
            Require(p.Entity.BattlePower==349);string below=BattlePowerClaimSnapshot(p);NobilityClaimPacket(p,1001);Require(below==BattlePowerClaimSnapshot(p));
            uint stale=p.Entity._ExtraBattlePower;p.Entity._ExtraBattlePower=999;Require(p.Entity.BattlePower==349);NobilityClaimPacket(p,1001);p.Entity._ExtraBattlePower=stale;Require(below==BattlePowerClaimSnapshot(p));
            byte old=boots.Plus;uint[] points={20,20,80,240,720,2160,6480,19440,58320,2700,5500,9000};uint[] stonePoints={0,10,40,120,360,1080,3240,9720,29160};
            // Smallest single supplied composition stone that advances exactly one level.
            int grade=Enumerable.Range(1,8).First(n=>stonePoints[n]>=points[old] && stonePoints[n]<points[old]+points[old+1]);
            var material=TotalBonusGive(p,(uint)(730000+grade),(byte)grade);CompositionClaimCompose(p,boots,material);Require(boots.Plus==old+1 && p.Entity.BattlePower==350);
            RetainedUpgradeUsage(p,boots.UID,8,ItemUsage.Mode.UnequipItem);string bagged=BattlePowerClaimSnapshot(p);NobilityClaimPacket(p,1001);Require(p.Entity.BattlePower<350 && bagged==BattlePowerClaimSnapshot(p));RetainedUpgradeUsage(p,boots.UID,8,ItemUsage.Mode.EquipItem);
            }
            Require(p.Entity.BattlePower==350);
            // Empty alternate slots retain main gear. Occupied alternate boots replace it.
            var alternateBoots=p.Equipment.TryGetItem(28);if(alternateBoots==null){alternateBoots=BattlePowerGear(p,160249,0,13);RetainedUpgradeUsage(p,alternateBoots.UID,28,ItemUsage.Mode.EquipItem);}
            Require(p.Entity.BattlePower==350);Require(GeneralData.SwitchEquipment(p,true));Require(p.Entity.BattlePower==348);
            string alternate=BattlePowerClaimSnapshot(p);NobilityClaimPacket(p,1001);Require(alternate==BattlePowerClaimSnapshot(p));Require(GeneralData.SwitchEquipment(p,false));Require(p.Entity.BattlePower==350);
            System.Threading.Tasks.Parallel.For(0,8,_=>NobilityClaimPacket(p,1001));var reward=p.Inventory.Objects.Single(i=>i.ID==723695);Require(reward.ID==723695 && reward.Bound && reward.StackSize==1 && reward.Plus==0);
            uint low=Kernel.DatabaseRefinery.Values.Where(i=>i.Level==1 && (bool)Call(Feature("RefineryProgress"),"Fits",boots,i)).OrderBy(i=>i.Identifier).First().Identifier;RefineryClaimApply(p,boots,low);
            var stable=RetainedArtifactStones(boots.UID,new[]{reward.UID});stable[4]=0;ItemAddingStabilization.Process(stable,p);Require(!PirateFenced(p) && !p.Inventory.ContainsUID(reward.UID));
            Require((long)Sql("SELECT count(*) FROM itemadding WHERE uid=@uid AND addingtype=1 AND duration=0 AND addinglevel=1","uid",(long)boots.UID)==1);
            string done=BattlePowerClaimSnapshot(p);ItemAddingStabilization.Process(stable,p);Require(done==BattlePowerClaimSnapshot(p));Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_battle_power_claim_probe SET snapshot=@snapshot","snapshot",BattlePowerClaimSnapshot(p));
        }
        Require(p.Entity.BattlePower==350 && p.Inventory.Objects.All(i=>(int)ItemHandler.GetPositionFromID(i.ID)==18));Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=1001 AND NOT legacy","uid",(long)uid)==1);
        string exact=BattlePowerClaimSnapshot(p);Require(exact==Convert.ToString(Sql("SELECT snapshot FROM revora_battle_power_claim_probe")));NobilityClaimPacket(p,1001);Require(exact==BattlePowerClaimSnapshot(p) && !PirateFenced(p));
        var active=p.Equipment.TryGetItem(8);var inactive=p.Equipment.TryGetItem(28);bool worn=inactive.IsWorn;ushort durability=active.Durability;
        try{inactive.IsWorn=true;Require(p.Entity.BattlePower==350);active.Durability=0;Require(p.Entity.BattlePower==340);}
        finally{inactive.IsWorn=worn;active.Durability=durability;}
        Require(exact==BattlePowerClaimSnapshot(p) && p.Entity.BattlePower==350);
        System.Console.WriteLine("[career-process] career-battle-power-claim-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Battle Power claim did not close.");
    }
}
