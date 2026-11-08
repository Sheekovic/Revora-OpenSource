using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
internal static partial class CheckCandidateArenaLifecycle {
    static string FreshMysticFranticSnapshot(GameState p) {
        return Convert.ToString(Sql(@"SELECT json_build_object('prior',@prior::json,
'mystic',(SELECT json_agg(r ORDER BY life) FROM revora_mystic_ring r WHERE uid=@uid),
'medicine',(SELECT json_agg(r ORDER BY life,item_uid) FROM revora_mystic_medicine r WHERE uid=@uid),
'frantic',(SELECT json_agg(r ORDER BY life) FROM revora_frantic_monkeys r WHERE uid=@uid),
'trials',(SELECT json_agg(r ORDER BY id) FROM revora_frantic_trials r WHERE uid=@uid),
'trialKills',(SELECT json_agg(r ORDER BY attempt,body) FROM revora_frantic_trial_kills r WHERE attempt IN(SELECT id FROM revora_frantic_trials WHERE uid=@uid)),
'apes',(SELECT json_agg(r ORDER BY id) FROM revora_frantic_apes r WHERE uid=@uid),
'apeKills',(SELECT json_agg(r ORDER BY attempt,body) FROM revora_frantic_ape_kills r WHERE attempt IN(SELECT id FROM revora_frantic_apes WHERE uid=@uid)))::text","prior",FreshAzureArtisanSnapshot(p),"uid",(long)p.Entity.UID));
    }
    static void FreshMysticFranticSave(GameState p,string stage,int kills,int hits) {
        Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_fresh_mystic_frantic_probe SET stage=@stage,kills=@kills,hits=@hits,snapshot=@snapshot","stage",stage,"kills",kills,"hits",hits,"snapshot",FreshMysticFranticSnapshot(p));
    }
    static void FreshMysticFranticProcess(string phase) {
        var p=FreshLoadSaved(true,true);EntityVariableTable.Load(p);SubClassTable.Load(p.Entity);ChiTable.Load(p);p.Quests.Load();Revora.Database.KnownPersons.LoadMentor(p);WayofHeroes.Load(p);
        Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        if((bool)Sql("SELECT to_regclass('revora_fresh_mystic_frantic_probe') IS NULL")) {
            Require(phase.EndsWith("prepare") && FreshAzureArtisanSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_azure_artisan_probe")) && (long)Sql("SELECT (SELECT count(*) FROM revora_mystic_ring WHERE uid=@uid)+(SELECT count(*) FROM revora_frantic_monkeys WHERE uid=@uid)","uid",(long)p.Entity.UID)==0);
            Sql("CREATE TABLE revora_fresh_mystic_frantic_probe(stage text,kills integer,hits integer,snapshot text);INSERT INTO revora_fresh_mystic_frantic_probe VALUES('new',0,0,@snapshot)","snapshot",FreshMysticFranticSnapshot(p));
        }
        Require(FreshMysticFranticSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_mystic_frantic_probe")));
        string stage=Convert.ToString(Sql("SELECT stage FROM revora_fresh_mystic_frantic_probe"));int kills=Convert.ToInt32(Sql("SELECT kills FROM revora_fresh_mystic_frantic_probe")),hits=Convert.ToInt32(Sql("SELECT hits FROM revora_fresh_mystic_frantic_probe"));
        if(phase.EndsWith("prepare")) {
            Require(stage=="new");FreshClassicTalk(p,1011,10012,0);var bank=p.Warehouses.First(w=>(uint)w.Key==10012).Value;int store=p.Inventory.Count-10;Require(store>=0 && bank.Count+store<=60);
            var prior=p.Inventory.Objects.Select(i=>(long)i.UID).ToArray();
            foreach(var item in p.Inventory.Objects.Where(i=>i.Days==0).OrderByDescending(i=>i.ID==1000000 || i.ID==1001000).Take(store).ToArray()) {
                var frame=new byte[172];Revora.Network.Writer.WriteUInt16(164,0,frame);Revora.Network.Writer.WriteUInt16(1102,2,frame);Revora.Network.Writer.WriteUInt32(10012,4,frame);frame[12]=1;frame[13]=10;Revora.Network.Writer.WriteUInt32(item.UID,18,frame);Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,164,frame);Warehouse.Process(frame,p);
                Require(!p.Inventory.ContainsUID(item.UID) && bank.ContainsUID(item.UID));
            }
            Require(p.Inventory.Count==10 && (long)Sql("SELECT count(*) FROM items WHERE entityid=@uid AND uid=ANY(@prior)","uid",(long)p.Entity.UID,"prior",prior)==prior.Length);FreshMysticFranticSave(p,"stored",kills,hits);
        } else if(phase.EndsWith("mystic")) {
            Require(stage=="stored");FreshClassicTalk(p,1002,900071727,81);FreshClassicTalk(p,1011,4500,1);FreshClassicTalk(p,1011,4501,1);
            foreach(uint medicine in new uint[]{1000000,1001000}) {
                p.Entity.Teleport(1002,327,303);var shop=ShopFile.Shops.Values.First(s=>s.MoneyType==ShopFile.MoneyType.Gold && s.Items.Contains(medicine) && p.Map.Npcs.ContainsKey(s.UID));var npc=p.Map.Npcs[shop.UID];p.Entity.X=npc.X;p.Entity.Y=npc.Y;
                int beforeCount=p.Inventory.Objects.Where(i=>i.ID==medicine).Sum(i=>Math.Max(1,(int)i.StackSize));ulong before=p.Entity.Money;uint price=ConquerItemInformation.BaseInformations[medicine].GoldWorth;Require(price>0 && before>=12*price);
                ItemHandler.HandleBuyFromNPC(new ItemUsage(true){UID=shop.UID,dwParam=medicine,dwExtraInfo=12},p);
                Require(p.Entity.Money==before-12*price && p.Inventory.Objects.Where(i=>i.ID==medicine).Sum(i=>Math.Max(1,(int)i.StackSize))==beforeCount+12 && !PirateFenced(p));
            }
            FreshClassicTalk(p,1011,4501,1);Require((long)Sql("SELECT sum(taken)::bigint FROM revora_mystic_medicine WHERE uid=@uid AND life=2","uid",(long)p.Entity.UID)==24);
            for(int i=0;i<20;i++)FreshClassicKill(p,1011,8,ref kills,ref hits);
            FreshClassicTalk(p,1011,4501,1);FreshClassicTalk(p,1011,900071742,1);FreshClassicTalk(p,1011,4501,1);FreshClassicTalk(p,1011,4500,1);
            ItemHandler.UseItem(p.Inventory.Objects.Single(i=>i.ID==721708),p);Require(p.Entity.Mesh==30040 && !PirateFenced(p));FreshClassicTalk(p,1002,900071727,0);Call(Feature("QuestAppearance"),"Tick");
            Require((long)Sql("SELECT count(*) FROM revora_mystic_ring WHERE uid=@uid AND life=2 AND stage=9 AND kills=20 AND cardinality(rewards)=0","uid",(long)p.Entity.UID)==1);FreshMysticFranticSave(p,"mystic",kills,hits);
        } else if(phase.EndsWith("frantic")) {
            Require(stage=="mystic");FreshClassicTalk(p,1002,900071727,91);FreshClassicTalk(p,1020,4485,1);
            for(int i=0;!p.Inventory.Contains(721699,1) && i<100;i++)FreshClassicKill(p,1020,10,ref kills,ref hits);Require(p.Inventory.Contains(721699,1));FreshClassicTalk(p,1020,4485,1);FreshClassicTalk(p,1002,4486,1);
            var clock=Feature("FranticMonkeysScenes").GetField("Clock",Hidden);object oldClock=clock.GetValue(null);DateTime now=DateTime.UtcNow;
            try {
                clock.SetValue(null,(Func<DateTime>)(()=>now));ItemHandler.UseItem(p.Inventory.Objects.Single(i=>i.ID==721700),p);Require(p.Entity.MapID>=11000 && p.Entity.Mesh==156 && !PirateFenced(p));Call(Feature("FranticMonkeysQuest"),"Tick");Require(!p.Map.Entities.Values.Any(e=>e.MonsterInfo!=null && e.MonsterInfo.ID==9917450));
                now=now.AddSeconds(63);Call(Feature("FranticMonkeysQuest"),"Tick");for(int i=0;i<5;i++){FreshClassicKill(p,p.Entity.MapID,9917450,ref kills,ref hits);now=now.AddSeconds(3);Call(Feature("FranticMonkeysQuest"),"Tick");}Require(p.Entity.MapID==1002);
                FreshClassicTalk(p,1002,4486,1);p.Entity.Teleport(1020,795,471);ItemHandler.UseItem(p.Inventory.Objects.Single(i=>i.ID==721701),p);
                for(int i=0;i<3;i++){FreshClassicKill(p,1020,i==2?9917452u:9917451u,ref kills,ref hits);now=now.AddSeconds(3);Call(Feature("FranticMonkeysQuest"),"Tick");}
                FreshClassicTalk(p,1020,4485,1);FreshClassicTalk(p,1002,900071727,0);
            } finally {clock.SetValue(null,oldClock);}
            Require((long)Sql("SELECT count(*) FROM revora_frantic_monkeys WHERE uid=@uid AND life=2 AND stage=9 AND cardinality(rewards)=0","uid",(long)p.Entity.UID)==1);
            Require((long)Sql("SELECT count(*) FROM revora_frantic_trial_kills WHERE attempt IN(SELECT id FROM revora_frantic_trials WHERE uid=@uid)","uid",(long)p.Entity.UID)==5 && (long)Sql("SELECT count(*) FROM revora_frantic_ape_kills WHERE attempt IN(SELECT id FROM revora_frantic_apes WHERE uid=@uid)","uid",(long)p.Entity.UID)==3);FreshMysticFranticSave(p,"ready",kills,hits);
        } else if(phase.EndsWith("claim")) {
            Require(stage=="ready");var prior=p.Inventory.Objects.Select(i=>i.UID).ToArray();FreshClassicTalk(p,1002,900071727,81);FreshClassicTalk(p,1002,900071727,91);var added=p.Inventory.Objects.Where(i=>!prior.Contains(i.UID)).ToArray();Require(added.Length==4 && added.All(i=>i.Bound) && prior.All(uid=>p.Inventory.ContainsUID(uid)));
            Require((long)Sql("SELECT count(*) FROM revora_mystic_ring WHERE uid=@uid AND life=2 AND stage=10 AND cardinality(rewards)=2","uid",(long)p.Entity.UID)==1 && (long)Sql("SELECT count(*) FROM revora_frantic_monkeys WHERE uid=@uid AND life=2 AND stage=10 AND cardinality(rewards)=2","uid",(long)p.Entity.UID)==1);FreshMysticFranticSave(p,"paid",kills,hits);
        } else Require(phase.EndsWith("reload") && stage=="paid");
        if(phase.EndsWith("claim") || phase.EndsWith("reload")) {
            var ids=((long[])Sql("SELECT rewards FROM revora_mystic_ring WHERE uid=@uid AND life=2","uid",(long)p.Entity.UID)).Concat((long[])Sql("SELECT rewards FROM revora_frantic_monkeys WHERE uid=@uid AND life=2","uid",(long)p.Entity.UID)).ToArray();Require(ids.Length==4 && ids.Distinct().Count()==4);var rewards=p.Inventory.Objects.Where(i=>ids.Contains((long)i.UID)).ToArray();Require(rewards.Length==4 && rewards.All(i=>i.Bound) && rewards.Count(i=>i.ID==728240)==1 && rewards.Count(i=>i.ID==728243)==1 && rewards.Count(i=>ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7)==2);
            string before=FreshMysticFranticSnapshot(p);FreshClassicTalk(p,1002,900071727,81);FreshClassicTalk(p,1002,900071727,91);Require(before==FreshMysticFranticSnapshot(p));
        }
        Require(FreshMysticFranticSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_mystic_frantic_probe")) && !PirateFenced(p));System.Console.WriteLine("[career-process] "+phase+"-passed uid="+p.Entity.UID+" level="+p.Entity.Level+" kills="+kills+" hits="+hits+" bag="+p.Inventory.Count+" phoenix="+p.Warehouses.First(w=>(uint)w.Key==10012).Value.Count);Server.CommandsAI("@close");throw new InvalidOperationException("Fresh Mystic/Frantic did not close.");
    }
}
