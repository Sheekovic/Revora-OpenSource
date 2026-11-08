using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
internal static partial class CheckCandidateArenaLifecycle {
    static string FreshRareSnapshot(GameState p) {return Convert.ToString(Sql(@"SELECT json_build_object('prior',@prior::json,'rare',(SELECT json_agg(r ORDER BY day) FROM revora_rare_runs r WHERE uid=@uid),'mining',(SELECT row_to_json(r) FROM revora_mining_state r WHERE uid=@uid),'passage',(SELECT row_to_json(r) FROM revora_plain_mine_passages r WHERE uid=@uid))::text","prior",FreshMagnoliaSnapshot(p),"uid",(long)p.Entity.UID));}
    static void FreshRareSave(GameState p,string stage,int ticks,int sold) {Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_fresh_rare_probe SET stage=@stage,ticks=@ticks,sold=@sold,snapshot=@snapshot","stage",stage,"ticks",ticks,"sold",sold,"snapshot",FreshRareSnapshot(p));}
    static void FreshRarePacket(GameState p,ItemUsage packet) {var data=packet.ToArray();Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,data.Length-8,data);ItemUsage.Process(data,p);Require(!PirateFenced(p));}
    static void FreshRareMineEntry(GameState p,uint material) {if(material==700031){p.Entity.Teleport(1011,934,562);HeartTalk(p,125,1);Require(p.Entity.MapID==1025);}else{p.Entity.Teleport(1002,423,356);HeartTalk(p,10080,5);Require(p.Entity.MapID==1028);}}
    static void FreshRareProcess(string phase) {
        var p=FreshLoadSaved();EntityVariableTable.Load(p);SubClassTable.Load(p.Entity);ChiTable.Load(p);p.Quests.Load();DialyQuestTable.Load(p);Call(Feature("DailyRareQuest"),"Refresh",p);
        Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        if((bool)Sql("SELECT to_regclass('revora_fresh_rare_probe') IS NULL")) {
            Require(phase.EndsWith("mine") && FreshMagnoliaSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_magnolia_probe")) && (long)Sql("SELECT count(*) FROM revora_rare_runs WHERE uid=@uid","uid",(long)p.Entity.UID)==0);
            Sql("CREATE TABLE revora_fresh_rare_probe(stage text,ticks integer,sold integer,weapon bigint,shield bigint,prior bigint[],snapshot text);INSERT INTO revora_fresh_rare_probe VALUES('new',0,0,@weapon,@shield,@prior,@snapshot)","weapon",(long)p.Equipment.TryGetItem(4).UID,"shield",(long)p.Equipment.TryGetItem(5).UID,"prior",p.Inventory.Objects.Select(i=>(long)i.UID).ToArray(),"snapshot",FreshRareSnapshot(p));
        }
        Require(FreshRareSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_rare_probe")));
        string stage=Convert.ToString(Sql("SELECT stage FROM revora_fresh_rare_probe"));int ticks=Convert.ToInt32(Sql("SELECT ticks FROM revora_fresh_rare_probe")),sold=Convert.ToInt32(Sql("SELECT sold FROM revora_fresh_rare_probe"));uint weapon=Convert.ToUInt32(Sql("SELECT weapon FROM revora_fresh_rare_probe")),shield=Convert.ToUInt32(Sql("SELECT shield FROM revora_fresh_rare_probe"));
        if(phase.EndsWith("mine")) {
            if(stage=="new") {
                p.Entity.Teleport(1002,Config.DailyEnvoyX,Config.DailyEnvoyY);HeartTalk(p,79882,19);Require(p.Entity.MaterialstemId>=700001 && p.Entity.MaterialstemId<=700031);
                HeartTalk(p,1,0);ulong gold=p.Entity.Money;FreshRarePacket(p,new ItemUsage(true){UID=1,dwParam=562000,dwExtraInfo=1,Action=ItemUsage.Mode.BuyFromNPC});var pick=p.Inventory.Objects.Single(i=>i.ID==562000);Require(p.Entity.Money==gold-1000);
                ItemHandler.UnequipItem(new ItemUsage(true){dwParam=5,Action=ItemUsage.Mode.UnequipItem},p);ItemHandler.UnequipItem(new ItemUsage(true){dwParam=4,Action=ItemUsage.Mode.UnequipItem},p);ItemHandler.EquipItem(new ItemUsage(true){UID=pick.UID,dwParam=4,Action=ItemUsage.Mode.EquipItem},p);Require(p.Equipment.TryGetItem(4).UID==pick.UID);FreshRareSave(p,"mine",ticks,sold);
            }
            uint material=p.Entity.MaterialstemId;var protectedIds=((long[])Sql("SELECT prior FROM revora_fresh_rare_probe")).Select(i=>(uint)i).Concat(new[]{weapon,shield}).ToArray();var mining=Feature("MiningRewards");object savedTime=Sql("SELECT next_at AT TIME ZONE 'UTC' FROM revora_mining_state WHERE uid=@uid","uid",(long)p.Entity.UID);DateTime now=savedTime==null?DateTime.UtcNow:DateTime.SpecifyKind(Convert.ToDateTime(savedTime),DateTimeKind.Utc).AddSeconds(3);mining.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));
            Action start=()=>{FreshRareMineEntry(p,material);var frame=new Data(true){ID=Data.SwingPickaxe,UID=p.Entity.UID}.ToArray();Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,frame.Length-8,frame);Data.Process(frame,p);Require(p.Mining);};start();var timer=System.Diagnostics.Stopwatch.StartNew();
            while(p.Inventory.Objects.Count(i=>i.ID==material)<Config.DailyRareGemAmount && ticks<10000 && timer.Elapsed.TotalSeconds<140) {
                if(p.Inventory.Count>=38) {
                    p.Mining=false;p.Entity.Teleport(1002,434,312);HeartTalk(p,1,0);var sale=p.Inventory.Objects.Where(i=>!protectedIds.Contains(i.UID) && i.ID!=562000 && !(i.ID>=700000 && i.ID<701000)).ToArray();Require(sale.Length>0);
                    foreach(var item in sale) {ulong gold=p.Entity.Money;uint expected=ConquerItemInformation.BaseInformations[item.ID].GoldWorth/3;FreshRarePacket(p,new ItemUsage(true){UID=1,dwParam=item.UID,Action=ItemUsage.Mode.SellToNPC});Require(!p.Inventory.ContainsUID(item.UID) && p.Entity.Money==gold+expected);sold++;}
                    start();
                }
                now=now.AddSeconds(3);Revora.Game.ConquerStructures.Mining.Mine(p);ticks++;Require(p.Mining && !PirateFenced(p));if(ticks%100==0)FreshRareSave(p,"mine",ticks,sold);
            }
            p.Mining=false;bool ready=p.Inventory.Objects.Count(i=>i.ID==material)>=Config.DailyRareGemAmount;
            if(ready){ItemHandler.UnequipItem(new ItemUsage(true){dwParam=4,Action=ItemUsage.Mode.UnequipItem},p);ItemHandler.EquipItem(new ItemUsage(true){UID=weapon,dwParam=4,Action=ItemUsage.Mode.EquipItem},p);ItemHandler.EquipItem(new ItemUsage(true){UID=shield,dwParam=5,Action=ItemUsage.Mode.EquipItem},p);Require(p.Equipment.TryGetItem(4).UID==weapon && p.Equipment.TryGetItem(5).UID==shield);p.Entity.Teleport(1002,Config.DailyEnvoyX,Config.DailyEnvoyY);HeartTalk(p,79882,19);}
            FreshRareSave(p,ready?"ready":"mine",ticks,sold);System.Console.WriteLine("[fresh-rare] ready="+ready+" material="+material+" gems="+p.Inventory.Objects.Count(i=>i.ID==material)+" ticks="+ticks+" sold="+sold);
        } else if(phase.EndsWith("claim")) {
            Require(stage=="ready");uint cp=p.Entity.ConquerPoints,rvp=p.Entity.RevoraPoint;ushort study=p.Entity.SubClasses.StudyPoints;uint material=p.Entity.MaterialstemId;int gems=p.Inventory.Objects.Count(i=>i.ID==material);var prior=p.Inventory.Objects.Select(i=>i.UID).ToArray();HeartTalk(p,79882,19);HeartTalk(p,79882,28);
            Require(!PirateFenced(p) && p.Entity.ConquerPoints==cp+Config.DailyQuestCp && p.Entity.RevoraPoint==rvp+Config.DailyQuestRevoraPoints && p.Entity.SubClasses.StudyPoints==study+Config.DailyRareMaterialsStudy && p.Inventory.Objects.Count(i=>i.ID==material)==gems-Config.DailyRareGemAmount);
            var rewards=p.Inventory.Objects.Where(i=>!prior.Contains(i.UID)).ToArray();Require(rewards.Length==2 && rewards.Any(i=>i.ID==Config.DailyQuestChiToken) && rewards.Any(i=>i.ID==4030101 && i.Bound));Require((long)Sql("SELECT count(*) FROM revora_rare_runs WHERE uid=@uid AND completed AND paid_cp=0 AND rune_uid IS NOT NULL AND soul_uid IS NULL","uid",(long)p.Entity.UID)==1);FreshRareSave(p,"paid",ticks,sold);
        } else Require(phase.EndsWith("reload") && stage=="paid");
        if(!phase.EndsWith("mine")){string before=FreshRareSnapshot(p);HeartTalk(p,79882,19);HeartTalk(p,79882,28);HeartTalk(p,79882,30);Require(before==FreshRareSnapshot(p));}
        Require(FreshRareSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_rare_probe")));System.Console.WriteLine("[career-process] "+phase+"-passed uid="+p.Entity.UID+" cps="+p.Entity.ConquerPoints+" rvp="+p.Entity.RevoraPoint+" study="+p.Entity.SubClasses.StudyPoints);Server.CommandsAI("@close");throw new InvalidOperationException("Fresh Rare did not close.");
    }
}
