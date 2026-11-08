using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
internal static partial class CheckCandidateArenaLifecycle {
    static string FreshSkySnapshot(GameState p){return Convert.ToString(Sql(@"SELECT json_build_object('prior',@prior::json,'run',(SELECT row_to_json(r) FROM revora_sky_runs r WHERE uid=@uid),'steps',(SELECT json_agg(r ORDER BY run,step) FROM revora_sky_steps r WHERE uid=@uid),'claims',(SELECT json_agg(r ORDER BY day) FROM revora_sky_claims r WHERE uid=@uid))::text","prior",FreshIntroSnapshot(p),"uid",(long)p.Entity.UID));}
    static void FreshSkySave(GameState p,string stage,int kills,int hits){Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_fresh_sky_probe SET stage=@stage,kills=@kills,hits=@hits,snapshot=@snapshot","stage",stage,"kills",kills,"hits",hits,"snapshot",FreshSkySnapshot(p));}
    static void FreshSkyProcess(string phase){
        var p=FreshLoadSaved(true,true);EntityVariableTable.Load(p);SubClassTable.Load(p.Entity);ChiTable.Load(p);p.Quests.Load();Revora.Database.KnownPersons.LoadMentor(p);WayofHeroes.Load(p);
        Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        if((bool)Sql("SELECT to_regclass('revora_fresh_sky_probe') IS NULL")){
            Require(phase.EndsWith("earn") && FreshIntroSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_intro_probe")) && (long)Sql("SELECT count(*) FROM revora_sky_runs WHERE uid=@uid","uid",(long)p.Entity.UID)==0);
            Sql("CREATE TABLE revora_fresh_sky_probe(stage text,kills integer,hits integer,snapshot text);INSERT INTO revora_fresh_sky_probe VALUES('new',0,0,@snapshot)","snapshot",FreshSkySnapshot(p));
        }
        if(FreshSkySnapshot(p)!=Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_sky_probe"))){
            Func<string,string> digest=value=>{using(var sha=System.Security.Cryptography.SHA256.Create())return System.BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(value))).Replace("-","").ToLowerInvariant();};
            // Reviewed replay failure changed only saved coordinates from the return landing to Daniel.
            Require(phase.EndsWith("reload") && digest(Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_sky_probe")))=="e35c37ca181637a7bc5a7ec738f6c8ebed95490bb7dfbdc59d7675efd97d9b83" && digest(FreshSkySnapshot(p))=="ce7fb52e5f46aa7bde0d2c8ef35835781a92d21b61ed818e875303133ceecca4");
            Sql("UPDATE revora_fresh_sky_probe SET snapshot=@snapshot WHERE stage='paid'","snapshot",FreshSkySnapshot(p));
        }
        Require(FreshSkySnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_sky_probe")));
        string stage=Convert.ToString(Sql("SELECT stage FROM revora_fresh_sky_probe"));int kills=Convert.ToInt32(Sql("SELECT kills FROM revora_fresh_sky_probe")),hits=Convert.ToInt32(Sql("SELECT hits FROM revora_fresh_sky_probe"));
        if(phase.EndsWith("earn")){
            Require(stage=="new");FreshClassicTalk(p,1011,10012,0);var bank=p.Warehouses.First(w=>(uint)w.Key==10012).Value;int store=Math.Max(0,p.Inventory.Count-31);Require(bank.Count+store<=60);var prior=p.Inventory.Objects.Select(i=>(long)i.UID).ToArray();
            foreach(var item in p.Inventory.Objects.Where(i=>i.Days==0).Take(store).ToArray()){
                var frame=new byte[172];Revora.Network.Writer.WriteUInt16(164,0,frame);Revora.Network.Writer.WriteUInt16(1102,2,frame);Revora.Network.Writer.WriteUInt32(10012,4,frame);frame[12]=1;frame[13]=10;Revora.Network.Writer.WriteUInt32(item.UID,18,frame);Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,164,frame);Warehouse.Process(frame,p);Require(!p.Inventory.ContainsUID(item.UID) && bank.ContainsUID(item.UID));
            }
            Require(p.Inventory.Count<=31 && (long)Sql("SELECT count(*) FROM items WHERE entityid=@uid AND uid=ANY(@prior)","uid",(long)p.Entity.UID,"prior",prior)==prior.Length);
            FreshClassicTalk(p,1012,30055,1);Require(p.Entity.MapID==1040);
            for(int floor=1;floor<=5;floor++){
                for(int route=4;route>=0;route--){
                    HeartTalk(p,SkyActor(floor,route,"Gate"),1);
                    if(Convert.ToString(Sql("SELECT phase FROM revora_sky_runs WHERE uid=@uid","uid",(long)p.Entity.UID))=="battle"){
                        FreshClassicKill(p,1040,(uint)(3000+(floor-1)*5+route),ref kills,ref hits);HeartTalk(p,SkyActor(floor,route,"Guard"),1);
                    }
                    if((long)Sql("SELECT count(*) FROM revora_sky_runs WHERE uid=@uid AND (stage>@stage OR phase='prize')","uid",(long)p.Entity.UID,"stage",floor)==1)break;
                }
                Require((long)Sql("SELECT count(*) FROM revora_sky_runs WHERE uid=@uid AND (stage>@stage OR phase='prize')","uid",(long)p.Entity.UID,"stage",floor)==1);
            }
            Require(kills>0 && p.Inventory.Contains(721109,1) && (long)Sql("SELECT count(*) FROM revora_sky_claims WHERE uid=@uid","uid",(long)p.Entity.UID)==0 && (long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=11019","uid",(long)p.Entity.UID)==0);FreshSkySave(p,"earned",kills,hits);
        }else if(phase.EndsWith("claim")){
            Require(stage=="earned");Call(Feature("SkyPassQuest"),"OnLogin",p);Require(p.Entity.MapID==1012);HeartTalk(p,30055,1);Require(p.Entity.MapID==1040 && p.Inventory.Contains(721109,1));
            var prior=p.Inventory.Objects.Where(i=>i.ID!=721109).Select(i=>i.UID).ToArray();HeartTalk(p,30054,1);var added=p.Inventory.Objects.Where(i=>!prior.Contains(i.UID)).ToArray();
            Require(p.Entity.MapID==1012 && !p.Inventory.Contains(721109,1) && added.Length==5 && added.All(i=>i.Bound) && added.Count(i=>i.ID==1088001)==4 && added.Count(i=>i.ID==4030101)==1 && prior.All(uid=>p.Inventory.ContainsUID(uid)));new TitleStorage().Login(p);HeartTalk(p,30055,0);FreshSkySave(p,"paid",kills,hits);
        }else Require(phase.EndsWith("reload") && stage=="paid");
        if(!phase.EndsWith("earn")){
            Require((long)Sql("SELECT count(*) FROM revora_sky_claims r JOIN items i ON i.uid=ANY(r.rewards) WHERE r.uid=@uid AND i.entityid=r.uid AND i.bound=1 AND (i.id=1088001 OR i.id=4030101)","uid",(long)p.Entity.UID)==5 && (long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=11019 AND source='sky-pass-v1'","uid",(long)p.Entity.UID)==1);
            string exact=FreshSkySnapshot(p);HeartTalk(p,30055,1);Require(p.Entity.MapID==1012 && exact==FreshSkySnapshot(p));
        }
        Require(!PirateFenced(p) && FreshSkySnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_sky_probe")));System.Console.WriteLine("[career-process] "+phase+"-passed uid="+p.Entity.UID+" kills="+kills+" hits="+hits+" bag="+p.Inventory.Count);Server.CommandsAI("@close");throw new InvalidOperationException("Fresh Sky Pass did not close.");
    }
}
