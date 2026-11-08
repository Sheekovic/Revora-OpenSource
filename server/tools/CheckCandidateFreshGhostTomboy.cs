using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
internal static partial class CheckCandidateArenaLifecycle {
    static string FreshGhostTomboySnapshot(GameState p) {
        return Convert.ToString(Sql(@"SELECT json_build_object('prior',@prior::json,
'ghost',(SELECT json_agg(r ORDER BY life) FROM revora_ghost_city r WHERE uid=@uid),
'combat',(SELECT json_agg(r ORDER BY life) FROM revora_ghost_combat r WHERE uid=@uid),
'kills',(SELECT json_agg(r ORDER BY visit,body) FROM revora_ghost_kills r WHERE uid=@uid),
'children',(SELECT json_agg(r ORDER BY life,child) FROM revora_ghost_children r WHERE uid=@uid),
'visits',(SELECT json_agg(r ORDER BY visit) FROM revora_ghost_visits r WHERE uid=@uid),
'tomboy',(SELECT json_agg(r ORDER BY life) FROM revora_tomboy r WHERE uid=@uid),
'materials',(SELECT json_agg(r ORDER BY item_uid) FROM revora_tomboy_materials r WHERE uid=@uid),
'fusions',(SELECT json_agg(r ORDER BY id) FROM revora_tomboy_fusions r WHERE uid=@uid))::text","prior",FreshGeneralMadSnapshot(p),"uid",(long)p.Entity.UID));
    }
    static void FreshGhostTomboySave(GameState p,string stage,int kills,int hits) {
        Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_fresh_ghost_tomboy_probe SET stage=@stage,kills=@kills,hits=@hits,snapshot=@snapshot","stage",stage,"kills",kills,"hits",hits,"snapshot",FreshGhostTomboySnapshot(p));
    }
    static void FreshGhostTomboyProcess(string phase) {
        var p=FreshLoadSaved(true,true);EntityVariableTable.Load(p);SubClassTable.Load(p.Entity);ChiTable.Load(p);p.Quests.Load();Revora.Database.KnownPersons.LoadMentor(p);WayofHeroes.Load(p);
        Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        if((bool)Sql("SELECT to_regclass('revora_fresh_ghost_tomboy_probe') IS NULL")) {
            Require(phase.EndsWith("ghost") && FreshGeneralMadSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_general_mad_probe")) && (long)Sql("SELECT (SELECT count(*) FROM revora_ghost_city WHERE uid=@uid)+(SELECT count(*) FROM revora_tomboy WHERE uid=@uid)","uid",(long)p.Entity.UID)==0);
            Sql("CREATE TABLE revora_fresh_ghost_tomboy_probe(stage text,kills integer,hits integer,snapshot text);INSERT INTO revora_fresh_ghost_tomboy_probe VALUES('new',0,0,@snapshot)","snapshot",FreshGhostTomboySnapshot(p));
        }
        if(phase.EndsWith("inspect")) {
            System.IO.File.WriteAllText("fresh-ghost-recovery-before.json",Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_ghost_tomboy_probe")));
            System.IO.File.WriteAllText("fresh-ghost-recovery-current.json",FreshGhostTomboySnapshot(p));
            System.Console.WriteLine("[career-process] "+phase+"-passed map="+p.Entity.MapID+" stage="+Sql("SELECT stage FROM revora_ghost_city WHERE uid=@uid AND life=2","uid",(long)p.Entity.UID)+" kills="+Sql("SELECT count(*) FROM revora_ghost_kills WHERE uid=@uid","uid",(long)p.Entity.UID)+" children="+Sql("SELECT count(*) FROM revora_ghost_children WHERE uid=@uid","uid",(long)p.Entity.UID));Server.CommandsAI("@close");throw new InvalidOperationException("Fresh Ghost inspection did not close.");
        }
        if(phase.EndsWith("ghost") && FreshGhostTomboySnapshot(p)!=Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_ghost_tomboy_probe"))) {
            // Resume only the reviewed interrupted snapshot; no quest/player/item rows are reset.
            Func<string,string> digest=value=>{using(var sha=System.Security.Cryptography.SHA256.Create())return System.BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(value))).Replace("-","").ToLowerInvariant();};
            Require(digest(Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_ghost_tomboy_probe")))=="a8687c9467d05771284b08c21f516a06d03966c243a9ebc031d5599df612a69f" && digest(FreshGhostTomboySnapshot(p))=="57d42c3ffee8583bfa1bf72862dce5e9c78fde14d6d8cce8f33bd8ff004f93b2");
            Require((long)Sql("SELECT count(*) FROM revora_ghost_city WHERE uid=@uid AND life=2 AND stage=8 AND cardinality(rewards)=0","uid",(long)p.Entity.UID)==1 && (long)Sql("SELECT count(*) FROM revora_ghost_kills WHERE uid=@uid AND life=2","uid",(long)p.Entity.UID)==102);
            Sql("UPDATE revora_fresh_ghost_tomboy_probe SET stage='rescue',snapshot=@snapshot WHERE stage='new'","snapshot",FreshGhostTomboySnapshot(p));System.Console.WriteLine("[fresh-ghost] preserved102 saved defeats; counters below cover continuation combat only");
        }
        Require(FreshGhostTomboySnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_ghost_tomboy_probe")));
        string stage=Convert.ToString(Sql("SELECT stage FROM revora_fresh_ghost_tomboy_probe"));int kills=Convert.ToInt32(Sql("SELECT kills FROM revora_fresh_ghost_tomboy_probe")),hits=Convert.ToInt32(Sql("SELECT hits FROM revora_fresh_ghost_tomboy_probe"));
        if(phase.EndsWith("ghost")) {
            Require((stage=="new" || stage=="rescue") && p.Inventory.Count<=25);var clock=Feature("GhostCityScenes").GetField("Clock",Hidden);object original=clock.GetValue(null);DateTime now=DateTime.UtcNow;
            Action<uint> talk=npc=>{var actor=p.Map.Npcs[npc];p.Entity.X=(ushort)(actor.X-2);p.Entity.Y=actor.Y;p.ActiveNpc=npc;Npcs.GetDialog(new NpcRequest(true){NpcID=npc,OptionID=1},p);Require(!PirateFenced(p));};
            Action enter=()=>{Require(p.Entity.MapID==1002);p.Entity.X=440;p.Entity.Y=308;ItemHandler.UseItem(p.Inventory.Objects.Single(i=>i.ID==721713),p);Require(p.Entity.MapID>=11000 && !PirateFenced(p));};
            try {
                clock.SetValue(null,(Func<DateTime>)(()=>now));if(stage=="new"){FreshClassicTalk(p,1002,900071727,121);FreshClassicTalk(p,1002,4503,1);FreshClassicTalk(p,1002,4504,1);
                ushort[,] sparks={{440,308},{443,344},{465,356}};for(int i=0;i<3;i++){p.Entity.X=sparks[i,0];p.Entity.Y=sparks[i,1];ItemHandler.UseItem(p.Inventory.Objects.Single(item=>item.ID==721717),p);}
                FreshClassicTalk(p,1002,4504,1);FreshClassicTalk(p,1002,4505,1);enter();talk(4526);talk(9917911);Require(p.Entity.MapID==1002 && p.Inventory.Contains(721712,1));
                FreshClassicTalk(p,1002,4505,1);enter();now=now.AddSeconds(3);Call(Feature("GhostCityQuest"),"Tick");
                for(int i=0;i<102;i++) {
                    var target=p.Map.Entities.Values.First(e=>!e.Dead && e.MonsterInfo!=null);uint family=target.MonsterInfo.ID;Require(i<100?family==9917920 || family==9917921:family==(i==100?9917922u:9917923u));
                    FreshClassicKill(p,p.Entity.MapID,family,ref kills,ref hits);now=now.AddSeconds(3);Call(Feature("GhostCityQuest"),"Tick");
                }
                }else enter();
                Require(p.Inventory.Contains(721714,1));for(uint npc=9917930;npc<9917935;npc++){talk(npc);string once=FreshGhostTomboySnapshot(p);talk(npc);Require(once==FreshGhostTomboySnapshot(p));}
                Require((long)Sql("SELECT count(*) FROM revora_ghost_children WHERE uid=@uid AND life=2","uid",(long)p.Entity.UID)==5);talk(4526);FreshClassicTalk(p,1002,4503,1);FreshClassicTalk(p,1002,900071727,0);
            } finally {clock.SetValue(null,original);}
            Require((long)Sql("SELECT count(*) FROM revora_ghost_city WHERE uid=@uid AND life=2 AND stage=10 AND sparks=3 AND cardinality(rewards)=0","uid",(long)p.Entity.UID)==1 && (long)Sql("SELECT count(*) FROM revora_ghost_kills WHERE uid=@uid AND life=2","uid",(long)p.Entity.UID)==102);FreshGhostTomboySave(p,"ghost",kills,hits);
        } else if(phase.EndsWith("tomboy")) {
            Require(stage=="ghost");FreshClassicTalk(p,1002,900071727,131);FreshClassicTalk(p,1000,4718,1);
            for(int i=0;p.Inventory.Objects.Count(item=>item.ID==710581)<10 && i<200;i++)FreshClassicKill(p,1000,16,ref kills,ref hits);Require(p.Inventory.Objects.Count(i=>i.ID==710581)==10);FreshClassicTalk(p,1000,4718,1);Require(!p.Inventory.Contains(710581,1));
            uint[] stones={721951,721952,721953},jades={710582,710583,710584};
            for(int i=0;!jades.All(id=>p.Inventory.Contains(id,1)) && i<300;i++) {
                FreshClassicKill(p,1000,75,ref kills,ref hits);
                for(int recipe=0;recipe<3;recipe++) {
                    if(p.Inventory.Contains(jades[recipe],1) || !p.Inventory.Contains(stones[recipe],1) || !p.Inventory.Contains(stones[(recipe+1)%3],1))continue;
                    var clicked=p.Inventory.Objects.First(item=>item.ID==stones[recipe]);var prior=p.Inventory.Objects.Select(item=>(long)item.UID).ToArray();long count=(long)Sql("SELECT count(*) FROM revora_tomboy_fusions WHERE uid=@uid","uid",(long)p.Entity.UID);
                    ItemHandler.UseItem(clicked,p);Require(!PirateFenced(p) && !p.Inventory.ContainsUID(clicked.UID) && (long)Sql("SELECT count(*) FROM revora_tomboy_fusions WHERE uid=@uid","uid",(long)p.Entity.UID)==count+1);
                    long partner=(long)Sql("SELECT partner FROM revora_tomboy_fusions WHERE uid=@uid AND clicked=@clicked","uid",(long)p.Entity.UID,"clicked",(long)clicked.UID);Require(!p.Inventory.ContainsUID((uint)partner) && prior.Where(uid=>uid!=clicked.UID && uid!=partner).All(uid=>p.Inventory.ContainsUID((uint)uid)));
                    bool success=(bool)Sql("SELECT success FROM revora_tomboy_fusions WHERE uid=@uid AND clicked=@clicked","uid",(long)p.Entity.UID,"clicked",(long)clicked.UID);Require(p.Inventory.Contains(jades[recipe],1)==success && p.Inventory.Count==prior.Length-(success?1:2));
                }
            }
            Require(jades.All(id=>p.Inventory.Objects.Count(i=>i.ID==id)==1));FreshClassicTalk(p,1000,4718,1);FreshClassicTalk(p,1000,4719,0);string before=FreshGhostTomboySnapshot(p);FreshClassicTalk(p,1000,4719,11);Require(before==FreshGhostTomboySnapshot(p));FreshClassicTalk(p,1000,4719,12);FreshClassicTalk(p,1002,900071727,0);
            Require((long)Sql("SELECT count(*) FROM revora_tomboy WHERE uid=@uid AND life=2 AND stage=5 AND cardinality(rewards)=0","uid",(long)p.Entity.UID)==1 && (long)Sql("SELECT count(*) FROM revora_tomboy_fusions WHERE uid=@uid AND life=2 AND success","uid",(long)p.Entity.UID)==3);FreshGhostTomboySave(p,"ready",kills,hits);
        } else if(phase.EndsWith("claim")) {
            Require(stage=="ready");var prior=p.Inventory.Objects.Select(i=>i.UID).ToArray();FreshClassicTalk(p,1002,900071727,121);FreshClassicTalk(p,1002,900071727,131);var added=p.Inventory.Objects.Where(i=>!prior.Contains(i.UID)).ToArray();Require(added.Length==6 && added.All(i=>i.Bound) && prior.All(uid=>p.Inventory.ContainsUID(uid)));
            Require((long)Sql("SELECT count(*) FROM revora_ghost_city WHERE uid=@uid AND life=2 AND stage=11 AND cardinality(rewards)=3","uid",(long)p.Entity.UID)==1 && (long)Sql("SELECT count(*) FROM revora_tomboy WHERE uid=@uid AND life=2 AND stage=6 AND cardinality(rewards)=3","uid",(long)p.Entity.UID)==1);FreshGhostTomboySave(p,"paid",kills,hits);
        } else Require(phase.EndsWith("reload") && stage=="paid");
        if(phase.EndsWith("claim") || phase.EndsWith("reload")) {
            var ids=((long[])Sql("SELECT rewards FROM revora_ghost_city WHERE uid=@uid AND life=2","uid",(long)p.Entity.UID)).Concat((long[])Sql("SELECT rewards FROM revora_tomboy WHERE uid=@uid AND life=2","uid",(long)p.Entity.UID)).ToArray();Require(ids.Length==6 && ids.Distinct().Count()==6);var rewards=p.Inventory.Objects.Where(i=>ids.Contains((long)i.UID)).ToArray();Require(rewards.Length==6 && rewards.All(i=>i.Bound) && rewards.Count(i=>i.ID==728246)==1 && rewards.Count(i=>i.ID==728241)==1 && rewards.Count(i=>i.ID==723911)==2 && rewards.Count(i=>ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7)==2);
            string before=FreshGhostTomboySnapshot(p);FreshClassicTalk(p,1002,900071727,121);FreshClassicTalk(p,1002,900071727,131);Require(before==FreshGhostTomboySnapshot(p));
        }
        Require(FreshGhostTomboySnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_ghost_tomboy_probe")) && !PirateFenced(p));System.Console.WriteLine("[career-process] "+phase+"-passed uid="+p.Entity.UID+" level="+p.Entity.Level+" kills="+kills+" hits="+hits+" bag="+p.Inventory.Count+" phoenix="+p.Warehouses.First(w=>(uint)w.Key==10012).Value.Count+" fusions="+Sql("SELECT count(*) FROM revora_tomboy_fusions WHERE uid=@uid","uid",(long)p.Entity.UID));Server.CommandsAI("@close");throw new InvalidOperationException("Fresh Ghost/Tomboy did not close.");
    }
}
