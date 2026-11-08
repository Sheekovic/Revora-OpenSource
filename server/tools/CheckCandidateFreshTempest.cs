using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
internal static partial class CheckCandidateArenaLifecycle {
    static string FreshTempestSnapshot(GameState p) {
        return Convert.ToString(Sql(@"SELECT json_build_object('prior',@prior::json,
'run',(SELECT json_agg(r ORDER BY uid) FROM revora_tempest_wings r WHERE uid=@uid),
'permits',(SELECT json_agg(r ORDER BY uid) FROM revora_tempest_permits r WHERE uid=@uid),
'payments',(SELECT json_agg(r ORDER BY item_uid) FROM revora_tempest_payments r WHERE uid=@uid),
'kills',(SELECT json_agg(r ORDER BY uid) FROM revora_tempest_kills r WHERE uid=@uid),
'feathers',(SELECT json_agg(r ORDER BY item_uid) FROM revora_tempest_feathers r WHERE uid=@uid),
'visits',(SELECT json_agg(r ORDER BY visit) FROM revora_tempest_visits r WHERE uid=@uid))::text","prior",FreshFanSnapshot(p),"uid",(long)p.Entity.UID));
    }
    static void FreshTempestSave(GameState p,string stage,int kills,int hits) {
        Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_fresh_tempest_probe SET stage=@stage,kills=@kills,hits=@hits,snapshot=@snapshot","stage",stage,"kills",kills,"hits",hits,"snapshot",FreshTempestSnapshot(p));
    }
    static void FreshTempestProcess(string phase) {
        Require((bool)Feature("TempestWingsQuest").GetProperty("Enabled",Hidden).GetValue(null,null));
        var p=FreshLoadSaved(true,true);EntityVariableTable.Load(p);SubClassTable.Load(p.Entity);ChiTable.Load(p);p.Quests.Load();Revora.Database.KnownPersons.LoadMentor(p);WayofHeroes.Load(p);
        Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        if((bool)Sql("SELECT to_regclass('revora_fresh_tempest_probe') IS NULL")) {
            Require(phase.EndsWith("earn") && FreshFanSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_fan_probe")) && (long)Sql("SELECT count(*) FROM revora_tempest_wings WHERE uid=@uid","uid",(long)p.Entity.UID)==0);
            Sql("CREATE TABLE revora_fresh_tempest_probe(stage text,kills integer,hits integer,snapshot text);INSERT INTO revora_fresh_tempest_probe VALUES('new',0,0,@snapshot)","snapshot",FreshTempestSnapshot(p));
        }
        Require(FreshTempestSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_tempest_probe")));
        string stage=Convert.ToString(Sql("SELECT stage FROM revora_fresh_tempest_probe"));int kills=Convert.ToInt32(Sql("SELECT kills FROM revora_fresh_tempest_probe")),hits=Convert.ToInt32(Sql("SELECT hits FROM revora_fresh_tempest_probe"));
        if(phase.EndsWith("earn")) {
            Require(stage=="new");FreshClassicTalk(p,1011,10012,0);var bank=p.Warehouses.First(w=>(uint)w.Key==10012).Value;int store=p.Inventory.Count-20;Require(store>=0 && bank.Count+store<=60);var prior=p.Inventory.Objects.Select(i=>(long)i.UID).ToArray();
            foreach(var item in p.Inventory.Objects.Where(i=>i.Days==0).Take(store).ToArray()) {
                var frame=new byte[172];Revora.Network.Writer.WriteUInt16(164,0,frame);Revora.Network.Writer.WriteUInt16(1102,2,frame);Revora.Network.Writer.WriteUInt32(10012,4,frame);frame[12]=1;frame[13]=10;Revora.Network.Writer.WriteUInt32(item.UID,18,frame);Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,164,frame);Warehouse.Process(frame,p);Require(!p.Inventory.ContainsUID(item.UID) && bank.ContainsUID(item.UID));
            }
            Require(p.Inventory.Count==20 && (long)Sql("SELECT count(*) FROM items WHERE entityid=@uid AND uid=ANY(@prior)","uid",(long)p.Entity.UID,"prior",prior)==prior.Length);
            uint cp=p.Entity.ConquerPoints,price=ConquerItemInformation.BaseInformations[1088001].ConquerPointsWorth;Require(price==3);
            for(int pack=0;pack<5;pack++) {
                p.Entity.Teleport(1002,304,435);ItemHandler.HandleBuyFromNPC(new ItemUsage(true){UID=2888,dwParam=1088001,dwExtraInfo=10},p);Require(p.Inventory.Objects.Where(i=>i.ID==1088001).Sum(i=>(int)i.StackSize)==10);
                FreshClassicTalk(p,1002,11275,0);FreshClassicTalk(p,1002,11275,1);Require(p.Inventory.Objects.Count(i=>i.ID==720027)==pack+1 && !p.Inventory.Contains(1088001,1));
            }
            Require(p.Entity.ConquerPoints==cp-50*price);FreshClassicTalk(p,1004,8796,1);FreshClassicTalk(p,1011,10850,1);Require(p.Map.BaseID==1100 && !p.Inventory.Contains(720027,1));
            var until=DateTime.UtcNow.AddSeconds(4);while(p.Map.Entities.Count==0 && DateTime.UtcNow<until){Call(Feature("TempestWingsQuest"),"Tick");System.Threading.Thread.Sleep(50);}
            FreshClassicKill(p,p.Entity.MapID,9905500,ref kills,ref hits);Call(Feature("TempestWingsQuest"),"Tick");Require(p.Inventory.Contains(3006392,1));FreshClassicTalk(p,p.Entity.MapID,9910500,1);Require(p.Entity.MapID==1011);FreshClassicTalk(p,1011,10850,0);
            Require((long)Sql("SELECT count(*) FROM revora_tempest_wings WHERE uid=@uid AND stage=3 AND cardinality(rewards)=0","uid",(long)p.Entity.UID)==1 && Convert.ToInt32(Sql("SELECT sum(spent) FROM revora_tempest_payments WHERE uid=@uid","uid",(long)p.Entity.UID))==5 && (long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10931","uid",(long)p.Entity.UID)==0);FreshTempestSave(p,"ready",kills,hits);
        } else if(phase.EndsWith("claim")) {
            Require(stage=="ready");var prior=p.Inventory.Objects.Where(i=>i.ID!=3006392).Select(i=>i.UID).ToArray();FreshClassicTalk(p,1011,10850,3);var added=p.Inventory.Objects.Where(i=>!prior.Contains(i.UID)).ToArray();Require(added.Length==2 && added.All(i=>i.Bound) && prior.All(uid=>p.Inventory.ContainsUID(uid)));
            Require((long)Sql("SELECT count(*) FROM revora_tempest_wings WHERE uid=@uid AND stage=4 AND cardinality(rewards)=2","uid",(long)p.Entity.UID)==1);new TitleStorage().Login(p);FreshTempestSave(p,"paid",kills,hits);
        } else Require(phase.EndsWith("reload") && stage=="paid");
        if(phase.EndsWith("claim") || phase.EndsWith("reload")) {
            var ids=(long[])Sql("SELECT rewards FROM revora_tempest_wings WHERE uid=@uid","uid",(long)p.Entity.UID);Require(ids.Length==2 && ids.Distinct().Count()==2);var rewards=p.Inventory.Objects.Where(i=>ids.Contains((long)i.UID)).ToArray();Require(rewards.Length==2 && rewards.All(i=>i.Bound) && rewards.Count(i=>i.ID==204008)==1 && rewards.Count(i=>ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7)==1);
            Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10931 AND source='equipment-quests-revora-v1' AND source_key='nineteen-chains-cross-life' AND rule_version=1","uid",(long)p.Entity.UID)==1);
            new TitleStorage().Login(p);const int bit=(10931/100-101)*32+10931%100-1;Require((System.BitConverter.ToUInt32(p.Entity.MyAchievement.ToArray(),16+bit/32*4)&(1u<<(bit%32)))!=0);
            string before=FreshTempestSnapshot(p);FreshClassicTalk(p,1011,10850,3);Require(before==FreshTempestSnapshot(p));
        }
        Require(FreshTempestSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_tempest_probe")) && !PirateFenced(p));System.Console.WriteLine("[career-process] "+phase+"-passed uid="+p.Entity.UID+" level="+p.Entity.Level+" kills="+kills+" hits="+hits+" bag="+p.Inventory.Count);Server.CommandsAI("@close");throw new InvalidOperationException("Fresh Tempest Wings did not close.");
    }
}
