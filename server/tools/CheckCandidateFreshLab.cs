using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
internal static partial class CheckCandidateArenaLifecycle {
    static string FreshLabSnapshot(GameState p){return Convert.ToString(Sql(@"SELECT json_build_object('prior',@prior::json,'visits',(SELECT row_to_json(r) FROM revora_lab_visits r WHERE uid=@uid),'entries',(SELECT json_agg(r ORDER BY id) FROM revora_lab_entries r WHERE uid=@uid),'steps',(SELECT json_agg(s ORDER BY visit,floor) FROM revora_lab_steps s JOIN revora_lab_entries e ON e.id=s.visit WHERE e.uid=@uid),'loot',(SELECT json_agg(r ORDER BY id) FROM revora_lab_loot r WHERE uid=@uid),'exchanges',(SELECT json_agg(r ORDER BY id) FROM revora_lab_exchanges r WHERE uid=@uid),'boxes',(SELECT json_agg(r ORDER BY box_uid) FROM revora_lab_boxes r WHERE uid=@uid))::text","prior",FreshVirtueSnapshot(p,Convert.ToUInt32(Sql("SELECT trainee FROM revora_fresh_virtue_probe"))),"uid",(long)p.Entity.UID));}
    static void FreshLabSave(GameState p,string stage,int kills,int hits){Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_fresh_lab_probe SET stage=@stage,kills=@kills,hits=@hits,snapshot=@snapshot","stage",stage,"kills",kills,"hits",hits,"snapshot",FreshLabSnapshot(p));}
    static void FreshLabProcess(string phase){
        var p=FreshLoadSaved(true,true);EntityVariableTable.Load(p);SubClassTable.Load(p.Entity);ChiTable.Load(p);p.Quests.Load();Revora.Database.KnownPersons.LoadMentor(p);WayofHeroes.Load(p);Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        if((bool)Sql("SELECT to_regclass('revora_fresh_lab_probe') IS NULL")){
            Require(phase.EndsWith("earn") && FreshVirtueSnapshot(p,Convert.ToUInt32(Sql("SELECT trainee FROM revora_fresh_virtue_probe")))==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_virtue_probe")) && p.VirtuePoints==2005 && (long)Sql("SELECT count(*) FROM revora_lab_entries WHERE uid=@uid","uid",(long)p.Entity.UID)==0);
            Sql("CREATE TABLE revora_fresh_lab_probe(stage text,kills integer,hits integer,box bigint,snapshot text);INSERT INTO revora_fresh_lab_probe VALUES('new',0,0,NULL,@snapshot)","snapshot",FreshLabSnapshot(p));
        }
        Require(FreshLabSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_lab_probe")));string stage=Convert.ToString(Sql("SELECT stage FROM revora_fresh_lab_probe"));int kills=Convert.ToInt32(Sql("SELECT kills FROM revora_fresh_lab_probe")),hits=Convert.ToInt32(Sql("SELECT hits FROM revora_fresh_lab_probe"));
        if(phase.EndsWith("earn")){
            Require(stage=="new");FreshClassicTalk(p,1011,10012,0);var bank=p.Warehouses.First(w=>(uint)w.Key==10012).Value;int store=Math.Max(0,p.Inventory.Count-32);Require(bank.Count+store<=60);var prior=p.Inventory.Objects.Select(i=>(long)i.UID).ToArray();
            foreach(var item in p.Inventory.Objects.Where(i=>i.Days==0).Take(store).ToArray()){
                var frame=new byte[172];Revora.Network.Writer.WriteUInt16(164,0,frame);Revora.Network.Writer.WriteUInt16(1102,2,frame);Revora.Network.Writer.WriteUInt32(10012,4,frame);frame[12]=1;frame[13]=10;Revora.Network.Writer.WriteUInt32(item.UID,18,frame);Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,164,frame);Warehouse.Process(frame,p);Require(!p.Inventory.ContainsUID(item.UID) && bank.ContainsUID(item.UID));
            }
            Require(p.Inventory.Count<=32 && (long)Sql("SELECT count(*) FROM items WHERE entityid=@uid AND uid=ANY(@prior)","uid",(long)p.Entity.UID,"prior",prior)==prior.Length);FreshClassicTalk(p,1002,1152,1);HeartTalk(p,1152,230);Require(p.Entity.MapID==1351 && p.VirtuePoints==5);
            for(int floor=0;floor<4;floor++){
                uint family=new uint[]{3142,3145,3148,3155}[floor],material=(uint)(floor<3?721537+floor:721536);int needed=floor<3?1:17,attempts=0;
                while((long)Sql("SELECT count(*) FROM revora_lab_loot WHERE uid=@uid AND type=@type","uid",(long)p.Entity.UID,"type",(int)material)<needed && attempts++<1500){
                    if(!p.Map.Entities.Values.Any(e=>e.MonsterInfo!=null && e.MonsterInfo.ID==family && !e.Dead))Call(typeof(Map),"_timerCallBack",p.Map,Time32.Now.AddSeconds(600).TotalMilliseconds);FreshClassicKill(p,p.Entity.MapID,family,ref kills,ref hits);
                }
                Require((long)Sql("SELECT count(*) FROM revora_lab_loot WHERE uid=@uid AND type=@type","uid",(long)p.Entity.UID,"type",(int)material)==needed);HeartTalk(p,(uint)(1153+floor),3);Require(p.Inventory.Objects.Any(i=>i.ID==material && i.Bound && i.StackSize==needed));
                if(floor<3){HeartTalk(p,(uint)(1153+floor),1);Require(p.Entity.MapID==1352+floor && !p.Inventory.Objects.Any(i=>i.ID==material));}
                System.Console.WriteLine("[fresh-lab] floor="+floor+" kills="+kills+" hits="+hits);
            }
            Require((long)Sql("SELECT count(*) FROM revora_lab_steps s JOIN revora_lab_entries e ON e.id=s.visit WHERE e.uid=@uid","uid",(long)p.Entity.UID)==3 && (long)Sql("SELECT count(*) FROM revora_lab_exchanges WHERE uid=@uid","uid",(long)p.Entity.UID)==0);FreshLabSave(p,"earned",kills,hits);
        }else if(phase.EndsWith("claim")){
            Require(stage=="earned" && p.VirtuePoints==5);Call(Feature("LabyrinthQuest"),"OnLogin",p);Require(p.Entity.MapID==1354 && p.VirtuePoints==5);HeartTalk(p,1156,2);Require(p.Entity.MapID==1002);var diamonds=p.Inventory.Objects.Single(i=>i.ID==721536);var prior=p.Inventory.Objects.Where(i=>i.UID!=diamonds.UID).Select(i=>i.UID).ToArray();HeartTalk(p,1152,13);HeartTalk(p,1152,230);var added=p.Inventory.Objects.Where(i=>!prior.Contains(i.UID)).ToArray();
            Require(added.Length==2 && added.All(i=>i.Bound) && added.Count(i=>i.ID==721540)==1 && added.Count(i=>ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7)==1 && !p.Inventory.ContainsUID(diamonds.UID) && prior.All(uid=>p.Inventory.ContainsUID(uid)));Sql("UPDATE revora_fresh_lab_probe SET box=@box","box",(long)added.Single(i=>i.ID==721540).UID);new TitleStorage().Login(p);FreshLabSave(p,"paid",kills,hits);
        }else if(phase.EndsWith("open")){
            Require(stage=="paid");var box=p.Inventory.Objects.Single(i=>i.UID==Convert.ToUInt32(Sql("SELECT box FROM revora_fresh_lab_probe")));var prior=p.Inventory.Objects.Where(i=>i.UID!=box.UID).Select(i=>i.UID).ToArray();SoulClaimUse(p,box);Require(!p.Inventory.ContainsUID(box.UID) && prior.All(uid=>p.Inventory.ContainsUID(uid)));
            Require((bool)Sql("SELECT EXISTS(SELECT 1 FROM revora_lab_boxes r WHERE uid=@uid AND box_uid=@box AND cardinality(rewards)=CASE WHEN roll<60 THEN 2 ELSE 1 END AND NOT EXISTS(SELECT 1 FROM unnest(rewards) reward LEFT JOIN items i ON i.uid=reward WHERE i.uid IS NULL OR i.entityid<>r.uid OR i.bound<>1 OR (r.roll<60 AND i.id<>1088001) OR (r.roll>=90 AND i.id<>1088000) OR (r.roll>=60 AND r.roll<90 AND i.id NOT IN(700001,700011,700021,700031,700041,700051,700061,700071))))","uid",(long)p.Entity.UID,"box",(long)box.UID));FreshLabSave(p,"opened",kills,hits);
        }else Require(phase.EndsWith("reload") && stage=="opened");
        if(!phase.EndsWith("earn")){
            Require(p.VirtuePoints==5 && (long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=11027 AND source='classic-labyrinth-v1'","uid",(long)p.Entity.UID)==1 && (long)Sql("SELECT count(*) FROM revora_lab_exchanges WHERE uid=@uid","uid",(long)p.Entity.UID)==1);string exact=FreshLabSnapshot(p);HeartTalk(p,1152,230);HeartTalk(p,1152,13);HeartTalk(p,1152,230);if(phase.EndsWith("open") || phase.EndsWith("reload"))RetainedUpgradeUsage(p,Convert.ToUInt32(Sql("SELECT box FROM revora_fresh_lab_probe")),0,ItemUsage.Mode.EquipItem);Require(exact==FreshLabSnapshot(p));
        }
        Require(!PirateFenced(p) && FreshLabSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_lab_probe")));System.Console.WriteLine("[career-process] "+phase+"-passed uid="+p.Entity.UID+" kills="+kills+" hits="+hits+" bag="+p.Inventory.Count);Server.CommandsAI("@close");throw new InvalidOperationException("Fresh Labyrinth did not close.");
    }
}
