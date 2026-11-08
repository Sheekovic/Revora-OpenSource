using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
internal static partial class CheckCandidateArenaLifecycle {
    static string FreshDesertHeroSnapshot(GameState p) {
        return Convert.ToString(Sql(@"SELECT json_build_object('prior',@prior::json,
'blood',(SELECT json_agg(r ORDER BY life) FROM revora_desert_blood r WHERE uid=@uid),
'bloodMaterials',(SELECT json_agg(r ORDER BY item_uid) FROM revora_desert_blood_materials r WHERE uid=@uid),
'visits',(SELECT json_agg(r ORDER BY visit) FROM revora_desert_visits r WHERE uid=@uid),
'column',(SELECT json_agg(r ORDER BY life) FROM revora_desert_column r WHERE uid=@uid),
'hero',(SELECT json_agg(r ORDER BY life) FROM revora_hero_wings r WHERE uid=@uid),
'heroMaterials',(SELECT json_agg(r ORDER BY item_uid) FROM revora_hero_wings_materials r WHERE uid=@uid))::text","prior",FreshGhostTomboySnapshot(p),"uid",(long)p.Entity.UID));
    }
    static void FreshDesertHeroSave(GameState p,string stage,int kills,int hits) {
        Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_fresh_desert_hero_probe SET stage=@stage,kills=@kills,hits=@hits,snapshot=@snapshot","stage",stage,"kills",kills,"hits",hits,"snapshot",FreshDesertHeroSnapshot(p));
    }
    static void FreshDesertHeroKill(GameState p,ushort map,uint[] families,ref int kills,ref int hits) {
        if(p.Entity.MapID!=map)p.Entity.Teleport(map,100,100);
        Func<Revora.Game.Entity> target=()=>p.Map.Entities.Values.FirstOrDefault(e=>!e.Dead && e.MonsterInfo!=null && families.Contains(e.MonsterInfo.ID));
        var body=target();if(body==null){System.Console.WriteLine("[fresh-desert] awaiting normal respawn map="+map+" families="+string.Join(",",families));var until=DateTime.UtcNow.AddSeconds(45);do{System.Threading.Thread.Sleep(100);body=target();}while(body==null && DateTime.UtcNow<until);}
        Require(body!=null);FreshClassicKill(p,map,body.MonsterInfo.ID,ref kills,ref hits);
    }
    static void FreshDesertHeroProcess(string phase) {
        var p=FreshLoadSaved(true,true);EntityVariableTable.Load(p);SubClassTable.Load(p.Entity);ChiTable.Load(p);p.Quests.Load();Revora.Database.KnownPersons.LoadMentor(p);WayofHeroes.Load(p);
        Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        if((bool)Sql("SELECT to_regclass('revora_fresh_desert_hero_probe') IS NULL")) {
            Require(phase.EndsWith("start") && FreshGhostTomboySnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_ghost_tomboy_probe")) && (long)Sql("SELECT (SELECT count(*) FROM revora_desert_blood WHERE uid=@uid)+(SELECT count(*) FROM revora_hero_wings WHERE uid=@uid)","uid",(long)p.Entity.UID)==0);
            Sql("CREATE TABLE revora_fresh_desert_hero_probe(stage text,kills integer,hits integer,meteor bigint DEFAULT 0,price bigint DEFAULT 0,snapshot text);INSERT INTO revora_fresh_desert_hero_probe(stage,kills,hits,snapshot) VALUES('new',0,0,@snapshot)","snapshot",FreshDesertHeroSnapshot(p));
        }
        if(phase.EndsWith("inspect")) {
            System.IO.File.WriteAllText("fresh-desert-recovery-before.json",Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_desert_hero_probe")));
            System.IO.File.WriteAllText("fresh-desert-recovery-current.json",FreshDesertHeroSnapshot(p));System.Console.WriteLine("[career-process] "+phase+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Fresh Desert inspection did not close.");
        }
        if(phase.EndsWith("start") && FreshDesertHeroSnapshot(p)!=Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_desert_hero_probe"))) {
            Func<string,string> digest=value=>{using(var sha=System.Security.Cryptography.SHA256.Create())return System.BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(value))).Replace("-","").ToLowerInvariant();};
            Require(digest(Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_desert_hero_probe")))=="17e94a90635b0f7a0b9b55981366db765cf58a2cbf63c89ee966dc243db21e14" && digest(FreshDesertHeroSnapshot(p))=="2594951e035f9b689dfdeeec20d084c01fa6c9802afe9b0dc3ca41a349863c81");
            Require((long)Sql("SELECT count(*) FROM revora_desert_blood WHERE uid=@uid AND life=2 AND stage=3 AND kills=69 AND spell_read AND cardinality(rewards)=0","uid",(long)p.Entity.UID)==1);
            Sql("UPDATE revora_fresh_desert_hero_probe SET stage='hunting',snapshot=@snapshot WHERE stage='new'","snapshot",FreshDesertHeroSnapshot(p));System.Console.WriteLine("[fresh-desert] preserved69 saved hunt defeats; counters below cover continuation combat only");
        }
        Require(FreshDesertHeroSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_desert_hero_probe")));
        string stage=Convert.ToString(Sql("SELECT stage FROM revora_fresh_desert_hero_probe"));int kills=Convert.ToInt32(Sql("SELECT kills FROM revora_fresh_desert_hero_probe")),hits=Convert.ToInt32(Sql("SELECT hits FROM revora_fresh_desert_hero_probe"));
        Action<uint,byte> crypt=(npc,opt)=>{var actor=p.Map.Npcs[npc];p.Entity.X=(ushort)(actor.X-2);p.Entity.Y=actor.Y;p.ActiveNpc=npc;Npcs.GetDialog(new NpcRequest(true){NpcID=npc,OptionID=opt},p);Require(!PirateFenced(p));};
        Action visit=()=>{FreshClassicTalk(p,1000,4622,1);Require(p.Map.BaseID==1801);crypt(4623,1);crypt(9918011,2);Require(p.Entity.MapID==1000);};
        if(phase.EndsWith("start")) {
            Require((stage=="new" || stage=="hunting") && p.Inventory.Count<=28);if(stage=="new"){FreshClassicTalk(p,1002,900071727,141);FreshClassicTalk(p,1000,4621,1);
            for(int i=0;!p.Inventory.Contains(721843,1) && i<100;i++)FreshDesertHeroKill(p,1000,new uint[]{76},ref kills,ref hits);Require(p.Inventory.Contains(721843,1));ItemHandler.UseItem(p.Inventory.Objects.Single(i=>i.ID==721843),p);visit();}
            for(int i=Convert.ToInt32(Sql("SELECT kills FROM revora_desert_blood WHERE uid=@uid AND life=2","uid",(long)p.Entity.UID));i<100;i++)FreshDesertHeroKill(p,1000,new uint[]{17,76},ref kills,ref hits);
            Require((long)Sql("SELECT count(*) FROM revora_desert_blood WHERE uid=@uid AND life=2 AND stage=3 AND kills=100 AND spell_read","uid",(long)p.Entity.UID)==1);FreshDesertHeroSave(p,"half",kills,hits);
        } else if(phase.EndsWith("blood")) {
            Require(stage=="half" && (long)Sql("SELECT count(*) FROM revora_desert_blood WHERE uid=@uid AND life=2 AND stage=3 AND kills=100","uid",(long)p.Entity.UID)==1);
            for(int i=0;i<100;i++)FreshDesertHeroKill(p,1000,new uint[]{17,76},ref kills,ref hits);
            Require((long)Sql("SELECT kills::bigint FROM revora_desert_blood WHERE uid=@uid AND life=2","uid",(long)p.Entity.UID)==200 && p.Inventory.Contains(721845,1));visit();
            for(int i=0;p.Inventory.Objects.Count(item=>item.ID==721846)<5 && i<100;i++)FreshDesertHeroKill(p,1000,new uint[]{76},ref kills,ref hits);Require(p.Inventory.Objects.Count(i=>i.ID==721846)==5);visit();FreshClassicTalk(p,1000,4621,1);visit();
            FreshClassicTalk(p,1015,4633,1);ItemHandler.UseItem(p.Inventory.Objects.Single(i=>i.ID==721844),p);FreshClassicTalk(p,1015,4633,1);FreshClassicTalk(p,1002,900071727,0);
            Require((long)Sql("SELECT count(*) FROM revora_desert_blood WHERE uid=@uid AND life=2 AND stage=10 AND cardinality(rewards)=0","uid",(long)p.Entity.UID)==1 && (long)Sql("SELECT count(*) FROM revora_desert_visits WHERE uid=@uid AND active","uid",(long)p.Entity.UID)==0);FreshDesertHeroSave(p,"blood",kills,hits);
        } else if(phase.EndsWith("hero")) {
            Require(stage=="blood" || stage=="hawking");
            if(stage=="blood") {
                Require(p.Entity.MapID==1002 && !p.Inventory.Contains(1088001,1));uint cp=p.Entity.ConquerPoints,price=ConquerItemInformation.BaseInformations[1088001].ConquerPointsWorth;Require(price>0 && cp>=price);
                ItemHandler.HandleBuyFromNPC(new ItemUsage(true){UID=2888,dwParam=1088001,dwExtraInfo=1},p);Require(p.Entity.ConquerPoints==cp-price && p.Inventory.Objects.Count(i=>i.ID==1088001)==1);uint meteor=p.Inventory.Objects.Single(i=>i.ID==1088001).UID;Sql("UPDATE revora_fresh_desert_hero_probe SET meteor=@meteor,price=@price","meteor",(long)meteor,"price",(long)price);
                FreshClassicTalk(p,1002,900071727,151);FreshClassicTalk(p,1015,4700,1);FreshClassicTalk(p,1015,4701,1);FreshClassicTalk(p,1015,4700,1);FreshClassicTalk(p,1015,4701,1);Require(!p.Inventory.ContainsUID(meteor));string before=FreshDesertHeroSnapshot(p);FreshClassicTalk(p,1015,4701,1);Require(before==FreshDesertHeroSnapshot(p));
                for(int i=0;p.Inventory.Objects.Count(item=>item.ID==721906)<10 && i<150;i++)FreshDesertHeroKill(p,1015,new uint[]{18},ref kills,ref hits);Require(p.Inventory.Objects.Count(i=>i.ID==721906)==10);FreshClassicTalk(p,1015,4701,1);ItemHandler.UseItem(p.Inventory.Objects.Single(i=>i.ID==721912),p);
            }
            if(p.Entity.MapID!=1015)p.Entity.Teleport(1015,701,595);
            for(int i=0;!p.Inventory.Contains(721909,1) && i<20 && p.Map.Entities.Values.Any(e=>!e.Dead && e.MonsterInfo!=null && e.MonsterInfo.ID==19);i++)FreshClassicKill(p,1015,19,ref kills,ref hits);
            if(!p.Inventory.Contains(721909,1))FreshDesertHeroSave(p,"hawking",kills,hits);
            else {
                int site=Convert.ToInt32(Sql("SELECT site FROM revora_hero_wings WHERE uid=@uid AND life=2","uid",(long)p.Entity.UID));var sites=(ushort[,])Feature("HeroWingsStore").GetField("Sites",Hidden).GetValue(null);p.Entity.X=sites[site,0];p.Entity.Y=sites[site,1];ItemHandler.UseItem(p.Inventory.Objects.Single(i=>i.ID==721909),p);Require(p.Inventory.Contains(721908,1));FreshClassicTalk(p,1015,4701,1);
                for(int i=0;i<10;i++)FreshDesertHeroKill(p,1015,new uint[]{78},ref kills,ref hits);Require(p.Inventory.Objects.Count(i=>i.ID==721910)==5);
                FreshClassicTalk(p,1015,4701,1);FreshClassicTalk(p,1015,4700,1);FreshClassicTalk(p,1015,4701,1);FreshClassicTalk(p,1015,4700,1);FreshClassicTalk(p,1002,900071727,0);
                Require((long)Sql("SELECT count(*) FROM revora_hero_wings WHERE uid=@uid AND life=2 AND stage=11 AND cardinality(rewards)=0","uid",(long)p.Entity.UID)==1);FreshDesertHeroSave(p,"ready",kills,hits);
            }
        } else if(phase.EndsWith("claim")) {
            Require(stage=="ready");var prior=p.Inventory.Objects.Select(i=>i.UID).ToArray();FreshClassicTalk(p,1002,900071727,141);FreshClassicTalk(p,1002,900071727,151);var added=p.Inventory.Objects.Where(i=>!prior.Contains(i.UID)).ToArray();Require(added.Length==6 && added.All(i=>i.Bound) && prior.All(uid=>p.Inventory.ContainsUID(uid)));
            Require((long)Sql("SELECT count(*) FROM revora_desert_blood WHERE uid=@uid AND life=2 AND stage=11 AND cardinality(rewards)=3","uid",(long)p.Entity.UID)==1 && (long)Sql("SELECT count(*) FROM revora_hero_wings WHERE uid=@uid AND life=2 AND stage=12 AND cardinality(rewards)=3","uid",(long)p.Entity.UID)==1);FreshDesertHeroSave(p,"paid",kills,hits);
        } else Require(phase.EndsWith("reload") && stage=="paid");
        if(phase.EndsWith("claim") || phase.EndsWith("reload")) {
            var ids=((long[])Sql("SELECT rewards FROM revora_desert_blood WHERE uid=@uid AND life=2","uid",(long)p.Entity.UID)).Concat((long[])Sql("SELECT rewards FROM revora_hero_wings WHERE uid=@uid AND life=2","uid",(long)p.Entity.UID)).ToArray();Require(ids.Length==6 && ids.Distinct().Count()==6);var rewards=p.Inventory.Objects.Where(i=>ids.Contains((long)i.UID)).ToArray();Require(rewards.Length==6 && rewards.All(i=>i.Bound) && rewards.Count(i=>i.ID==728242)==1 && rewards.Count(i=>i.ID==728247)==1 && rewards.Count(i=>i.ID==723911)==2 && rewards.Count(i=>ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7)==2);
            string before=FreshDesertHeroSnapshot(p);FreshClassicTalk(p,1002,900071727,141);FreshClassicTalk(p,1002,900071727,151);Require(before==FreshDesertHeroSnapshot(p));
        }
        Require(FreshDesertHeroSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_desert_hero_probe")) && !PirateFenced(p));System.Console.WriteLine("[career-process] "+phase+"-passed uid="+p.Entity.UID+" level="+p.Entity.Level+" kills="+kills+" hits="+hits+" bag="+p.Inventory.Count+" stage="+Sql("SELECT stage FROM revora_fresh_desert_hero_probe")+" meteorCP="+Sql("SELECT price FROM revora_fresh_desert_hero_probe"));Server.CommandsAI("@close");throw new InvalidOperationException("Fresh Desert/Hero did not close.");
    }
}
