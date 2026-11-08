using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Game.Features;
using Revora.Network.GamePackets;
using Console=System.Console;
internal static partial class CheckCandidateArenaLifecycle {
    static string FreshPriceSnapshot(GameState p){return Convert.ToString(Sql(@"SELECT json_build_object('career',@career::json,
'study',(SELECT studypoints FROM entities WHERE uid=@uid),
'runs',(SELECT json_agg(r ORDER BY day) FROM revora_price_runs r WHERE uid=@uid),
'pickups',(SELECT json_agg(r ORDER BY item_uid) FROM revora_daily_hunt_pickups r WHERE collector=@uid))::text","career",FreshCourierSnapshot(p),"uid",(long)p.Entity.UID));}
    static void FreshPriceSave(GameState p,int kills,int hits,string stage){Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_fresh_price_probe SET kills=@kills,hits=@hits,stage=@stage,snapshot=@snapshot","kills",kills,"hits",hits,"stage",stage,"snapshot",FreshPriceSnapshot(p));}
    static void FreshPriceProcess(string phase){
        var p=FreshLoadSaved();uint uid=p.Entity.UID;EntityVariableTable.Load(p);SubClassTable.Load(p.Entity);ChiTable.Load(p);p.Quests.Load();Call(Feature("DailyPriceQuest"),"Refresh",p);
        Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        if((bool)Sql("SELECT to_regclass('revora_fresh_price_probe') IS NULL")){
            Require(phase=="career-fresh-price-hunt" && FreshCourierSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_courier_probe")) && (long)Sql("SELECT count(*) FROM revora_price_runs WHERE uid=@uid","uid",(long)uid)==0);
            Sql("CREATE TABLE revora_fresh_price_probe(uid bigint PRIMARY KEY,kills integer,hits integer,stage text,snapshot text);INSERT INTO revora_fresh_price_probe VALUES(@uid,0,0,'new',@snapshot)","uid",(long)uid,"snapshot",FreshPriceSnapshot(p));
        }
        Require(FreshPriceSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_price_probe")));
        int kills=Convert.ToInt32(Sql("SELECT kills FROM revora_fresh_price_probe")),hits=Convert.ToInt32(Sql("SELECT hits FROM revora_fresh_price_probe"));string stage=Convert.ToString(Sql("SELECT stage FROM revora_fresh_price_probe"));
        if(phase.EndsWith("hunt")){
            if(stage=="new"){
                CombinedStash(p);p.Entity.Teleport(1002,Config.DailyEnvoyX,Config.DailyEnvoyY);HeartTalk(p,DailyQuestEnvoySystem.NpcId,2);
                Require(p.Entity.PriceQuestID>=1 && p.Entity.PriceQuestID<=4 && p.Inventory.Contains(729087,1));FreshPriceSave(p,kills,hits,"hunt");
            }
            int quest=p.Entity.PriceQuestID;uint material=p.Entity.PriceItemId;int amount=p.Entity.Priceamount;
            string[][] names={new[]{"BanditL97","BanditL98"},new[]{"Macaque","MacaqueL48"},new[]{"SandMonster","SandMonsterL68"},new[]{"Bandit","BanditL33"}};
            ushort[] maps={1015,1020,1000,1011};ItemHandler.UseItem(p.Inventory.Objects.Single(i=>i.ID==729087),p);
            var trip=new byte[28];Revora.Network.Writer.WriteUInt16(20,0,trip);Revora.Network.Writer.WriteUInt16(2032,2,trip);trip[14]=1;Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,20,trip);p.Filtering=true;ChoiceWireSend(p,trip);Require(p.Entity.MapID==maps[quest-1]);
            p.Attackable=true;p.CantAttack=Time32.Now.AddSeconds(-1);var timer=System.Diagnostics.Stopwatch.StartNew();
            while(p.Inventory.Objects.Sum(i=>i.ID==material?Math.Max(1,(int)i.StackSize):0)<amount && kills<500 && timer.Elapsed.TotalSeconds<150){
                var body=p.Map.Entities.Values.Where(e=>e.MonsterInfo!=null && !e.Dead && names[quest-1].Contains(e.MonsterInfo.Name) && !e.MonsterInfo.Boss && !e.MonsterInfo.Guard).OrderBy(e=>e.UID).FirstOrDefault();Require(body!=null);
                p.Entity.X=(ushort)(body.X+1);p.Entity.Y=body.Y;p.Screen.Add(body);var before=p.Map.FloorItems.Keys.ToArray();uint hp=body.Hitpoints;int strikes=0;
                while(!body.Dead && strikes<500){p.Entity.AttackStamp=Time32.Now.AddSeconds(-2);Attack.HandleAttack(new Attack(true){Attacker=uid,Attacked=body.UID,X=body.X,Y=body.Y,AttackType=Attack.Melee},p);strikes++;hits++;if(strikes==1)Require(body.Hitpoints<hp);}
                Require(body.Dead && !p.Entity.Dead && !PirateFenced(p));kills++;
                foreach(var drop in p.Map.FloorItems.Values.Where(d=>!before.Contains(d.UID) && d.OwnerUID==uid && d.ItemID==material).ToArray()){
                    p.Entity.X=drop.X;p.Entity.Y=drop.Y;var loot=new FloorItem(true){UID=drop.UID,Type=FloorItem.Remove}.ToArray();Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,loot.Length-8,loot);FloorItem.Process(loot,p);FloorItem.Process(loot,p);Require(p.Inventory.ContainsUID(drop.Item.UID) && !PirateFenced(p));
                }
                FreshPriceSave(p,kills,hits,"hunt");
            }
            Require(p.Inventory.Objects.Sum(i=>i.ID==material?Math.Max(1,(int)i.StackSize):0)==amount && (long)Sql("SELECT count(*) FROM revora_daily_hunt_pickups WHERE collector=@uid AND item_type=@type","uid",(long)uid,"type",(long)material)==amount);
            p.Entity.Teleport(1002,Config.DailyEnvoyX,Config.DailyEnvoyY);HeartTalk(p,DailyQuestEnvoySystem.NpcId,2);FreshPriceSave(p,kills,hits,"ready");
            Console.WriteLine("[fresh-price] quest="+quest+" material="+material+" amount="+amount+" drop-percent="+Config.DailyPriceHuntDropPercent);
        }else if(phase.EndsWith("claim")){
            Require(stage=="ready");uint cp=p.Entity.ConquerPoints,rvp=p.Entity.RevoraPoint;ushort study=p.Entity.SubClasses.StudyPoints;var prior=p.Inventory.Objects.Select(i=>i.UID).ToArray();
            string stale=FreshPriceSnapshot(p);HeartTalk(p,DailyQuestEnvoySystem.NpcId,11);Require(stale==FreshPriceSnapshot(p) && !PirateFenced(p));
            HeartTalk(p,DailyQuestEnvoySystem.NpcId,2);HeartTalk(p,DailyQuestEnvoySystem.NpcId,11);Require(!PirateFenced(p) && p.Entity.ConquerPoints==cp+Config.DailyQuestCp && p.Entity.RevoraPoint==rvp+Config.DailyQuestRevoraPoints && p.Entity.SubClasses.StudyPoints==study+Config.DailyPriceQuestStudy);
            var rewards=p.Inventory.Objects.Where(i=>!prior.Contains(i.UID)).ToArray();Require(rewards.Length==2 && rewards.Any(i=>i.ID==Config.DailyQuestChiToken) && rewards.Any(i=>i.ID==4030101 && i.Bound) && !rewards.Any(i=>ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7));
            Require((long)Sql("SELECT count(*) FROM revora_price_runs WHERE uid=@uid AND completed AND rune_uid IS NOT NULL AND soul_uid IS NULL AND skip_fee=0","uid",(long)uid)==1);FreshPriceSave(p,kills,hits,"paid");
        }else Require(phase.EndsWith("reload") && stage=="paid");
        if(!phase.EndsWith("hunt")){string once=FreshPriceSnapshot(p);HeartTalk(p,DailyQuestEnvoySystem.NpcId,2);HeartTalk(p,DailyQuestEnvoySystem.NpcId,11);HeartTalk(p,DailyQuestEnvoySystem.NpcId,13);Require(once==FreshPriceSnapshot(p));}
        Require(FreshPriceSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_price_probe")) && !PirateFenced(p));
        Console.WriteLine("[career-process] "+phase+"-passed uid="+uid+" kills="+kills+" hits="+hits+" cps="+p.Entity.ConquerPoints+" rvp="+p.Entity.RevoraPoint+" study="+p.Entity.SubClasses.StudyPoints);
        Server.CommandsAI("@close");throw new InvalidOperationException("Fresh daily hunt did not close.");
    }
}
