using System;
using System.Linq;
using Revora.Game;
using Revora;
using Revora.Database;
using Revora.Network.GamePackets;

internal static partial class CheckCandidateArenaLifecycle {
    static bool newcomerFixture;
    static bool newcomerClaimFixture;
    static void NewcomerClaimProcess(bool reload){
        newcomerClaimFixture=true;const uint uid=4294917801;
        if((bool)Sql("SELECT to_regclass('revora_newcomer_claim_probe') IS NULL")){
            Require(!reload && (long)Sql("SELECT count(*) FROM entities WHERE uid=@uid","uid",(long)uid)==0);
            nextUid=uid;Player(Kernel.Maps[1002],false,1003,true);
            Sql("UPDATE entities SET level=20,reborn=0,money=100000 WHERE uid=@uid","uid",(long)uid);
            Sql("CREATE TABLE revora_newcomer_claim_probe(snapshot text);INSERT INTO revora_newcomer_claim_probe VALUES(NULL)");
        }
        nextUid=uid;var p=Player(Kernel.Maps[1002],true,1003,true);ChiTable.Load(p);WayofHeroes.Load(p);p.LoadItemStats();
        Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        Func<int> stage=()=>Convert.ToInt32(Sql("SELECT COALESCE((SELECT stage FROM revora_newcomer_intro WHERE uid=@uid),0)","uid",(long)uid));
        Action refused=()=>{string before=PotionClaimSnapshot(p);NobilityClaimPacket(p,104);Require(before==PotionClaimSnapshot(p));};
        if(Sql("SELECT snapshot FROM revora_newcomer_claim_probe")==DBNull.Value){
            Require(!reload);
            if(stage()==0){refused();HeartTalk(p,2001,1);Require(stage()==0);HeartTalk(p,5673,1);Require(stage()==1);}
            if(stage()==1){refused();var npc=p.Map.Npcs[8];p.Entity.X=npc.X;p.Entity.Y=npc.Y;IntroWarehouse(p);Require(stage()==2);}
            if(stage()==2){
                refused();var npc=p.Map.Npcs[3];p.Entity.X=npc.X;p.Entity.Y=npc.Y;
                uint item=ShopFile.Shops[3].Items.Where(id=>ConquerItemInformation.BaseInformations[id].GoldWorth>0).OrderBy(id=>ConquerItemInformation.BaseInformations[id].GoldWorth).First();
                ulong before=p.Entity.Money;uint price=ConquerItemInformation.BaseInformations[item].GoldWorth;
                ItemHandler.HandleBuyFromNPC(new ItemUsage(true){UID=3,dwParam=item,dwExtraInfo=1},p);
                Require(stage()==3 && p.Entity.Money==before-price && p.Inventory.Objects.Any(i=>i.ID==item));
            }
            if(stage()==3){refused();HeartTalk(p,2001,1);Require(stage()==4);}
            System.Threading.Tasks.Parallel.For(0,8,_=>NobilityClaimPacket(p,104));
            if((long)Sql("SELECT count(*) FROM revora_exp_potion_uses WHERE uid=@uid AND item_type=723017","uid",(long)uid)==0){
                var reward=p.Inventory.Objects.Single(i=>i.ID==723017);Require(reward.Bound && reward.StackSize==1);
                SoulClaimUse(p,reward);Require(!p.Inventory.ContainsUID(reward.UID) && p.Entity.DoubleExperienceTime==3600);
                SoulClaimUse(p,reward);Require(p.Entity.DoubleExperienceTime==3600);
            }
            Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_newcomer_claim_probe SET snapshot=@snapshot","snapshot",PotionClaimSnapshot(p));
        }
        Require(stage()==4 && p.Entity.DoubleExperienceTime==3600 && !p.Inventory.Objects.Any(i=>i.ID==723017));
        Require(p.Inventory.Objects.Single(i=>i.ID==4030101).Bound);
        Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=104 AND NOT legacy","uid",(long)uid)==1);
        Require((long)Sql("SELECT count(*) FROM revora_exp_potion_uses WHERE uid=@uid AND item_type=723017 AND seconds=3600","uid",(long)uid)==1);
        string exact=PotionClaimSnapshot(p);Require(exact==Convert.ToString(Sql("SELECT snapshot FROM revora_newcomer_claim_probe")));
        HeartTalk(p,2001,1);NobilityClaimPacket(p,104);Require(exact==PotionClaimSnapshot(p) && !PirateFenced(p));
        System.Console.WriteLine("[career-process] career-newcomer-claim-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Newcomer claim did not close.");
    }

    static void IntroWarehouse(Revora.Client.GameState p){var bytes=new byte[172];Revora.Network.Writer.WriteUInt16(164,0,bytes);Revora.Network.Writer.WriteUInt16(1102,2,bytes);Revora.Network.Writer.WriteUInt32(8,4,bytes);Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,164,bytes);Warehouse.Process(bytes,p);}
    static int IntroStage(){return Convert.ToInt32(Sql("SELECT COALESCE((SELECT stage FROM revora_newcomer_intro WHERE uid=4294907301),0)"));}
    static void NewcomerJourney(string phase){
        newcomerFixture=true;nextUid=4294907301;if(phase=="newcomer-start"){Player(Kernel.Maps[1002],false,1003,true);nextUid=4294907301;}
        var p=Player(Kernel.Maps[1002],true,1003,true);p.LoadItemStats();
        Require(((NpcSpawn)p.Map.Npcs[8]).Type==(Enums.NpcType)3 && ((NpcSpawn)p.Map.Npcs[3]).Type==Enums.NpcType.Shop);
        if(phase=="newcomer-start"){
            HeartTalk(p,2001,1);Require(IntroStage()==0 && p.Inventory.Count==0);
            HeartTalk(p,5673,1);HeartTalk(p,5673,1);Require(IntroStage()==1);
            IntroWarehouse(p);Require(IntroStage()==1);p.Entity.X=406;p.Entity.Y=312;IntroWarehouse(p);IntroWarehouse(p);Require(IntroStage()==2);
            Require(EntityTable.SaveEntity(p));
        }else if(phase=="newcomer-fault"){
            Require(IntroStage()==2);p.Entity.X=400;p.Entity.Y=331;
            uint item=ShopFile.Shops[3].Items.Where(id=>ConquerItemInformation.BaseInformations[id].GoldWorth>0).OrderBy(id=>ConquerItemInformation.BaseInformations[id].GoldWorth).First();ulong silver=p.Entity.Money;uint price=ConquerItemInformation.BaseInformations[item].GoldWorth;
            ItemHandler.HandleBuyFromNPC(new ItemUsage(true){UID=3,dwParam=item,dwExtraInfo=1},p);Require(IntroStage()==3 && p.Entity.Money==silver-price && p.Inventory.Objects.Any(i=>i.ID==item));
            while(p.Inventory.Count<40)SuppliedMountItem(p,1000000,1,false);
            HeartTalk(p,2001,1);Require(IntroStage()==3 && !p.Inventory.Contains(4030101,1));
            CombinedStash(p);Require(p.Inventory.Count==0);Require(EntityTable.SaveEntity(p));
            Sql("CREATE FUNCTION reject_intro_reward() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.achievement_id=10930 THEN RETURN NULL; END IF; RETURN NEW; END $$;CREATE TRIGGER reject_intro_reward BEFORE INSERT ON revora_achievement_evidence FOR EACH ROW EXECUTE FUNCTION reject_intro_reward();");
            HeartTalk(p,2001,1);Require(IntroStage()==3 && (long)Sql("SELECT count(*) FROM items WHERE entityid=4294907301 AND id=4030101")==0);
            Sql("DROP TRIGGER reject_intro_reward ON revora_achievement_evidence;DROP FUNCTION reject_intro_reward();");
        }else{
            if(phase=="newcomer-finish")Require(IntroStage()==3);
            HeartTalk(p,2001,1);HeartTalk(p,2001,1);Require(IntroStage()==4);
            System.Threading.Tasks.Parallel.For(0,8,i=>Npcs.GetDialog(new NpcRequest(true){NpcID=2001,OptionID=1},p));
            var journal=new System.Collections.Generic.List<MsgTaskStatus.QuestData>();var seen=new System.Collections.Generic.HashSet<uint>();Call(Feature("NewcomerIntroduction"),"Fill",p,journal,seen);Require(journal.Count==4 && journal.All(q=>q.Status==MsgTaskStatus.QuestData.QuestStatus.Finished));
            var rune=p.Inventory.Objects.Single(i=>i.ID==4030101);Require(rune.Bound && rune.Days==0);
            Require((bool)Sql("SELECT reward_uid=@item FROM revora_newcomer_intro WHERE uid=4294907301","item",(long)rune.UID));
            Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=4294907301 AND achievement_id=10930 AND source='newcomer-intro-v1'")==1);
            p.Entity.MyAchievement.Send();int bit=(10930/100-101)*32+10930%100-1;Require((System.BitConverter.ToUInt32(p.Entity.MyAchievement.ToArray(),16+bit/32*4)&(1u<<(bit%32)))!=0);Require(EntityTable.SaveEntity(p));
        }
        System.Console.WriteLine("[newcomer] "+phase+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Newcomer journey did not close.");
    }
    static void NewcomerInspect() {
        foreach(uint id in new uint[]{5673,8,3,2001}){
            Revora.Interfaces.INpc npc;if(Kernel.Maps[1002].Npcs.TryGetValue(id,out npc))System.Console.WriteLine("[newcomer-npc] id="+id+" xy="+npc.X+","+npc.Y+" mesh="+npc.Mesh+" type="+(npc is NpcSpawn?((NpcSpawn)npc).Type.ToString():npc.GetType().Name));else System.Console.WriteLine("[newcomer-npc] id="+id+" absent");
            System.Console.WriteLine("[newcomer-shop] id="+id+" stocked="+(ShopFile.Shops.ContainsKey(id)?"silver-or-cp:"+ShopFile.Shops[id].MoneyType+":"+ShopFile.Shops[id].Items.Count:"none"));
        }
        System.Console.WriteLine("[newcomer] newcomer-inspect-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Newcomer inspection did not close.");
    }
}
