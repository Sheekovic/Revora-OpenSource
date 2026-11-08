using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
internal static partial class CheckCandidateArenaLifecycle {
    static string FreshFamilyTreasureSnapshot(GameState p) {
        return Convert.ToString(Sql(@"SELECT json_build_object('prior',@prior::json,
'family',(SELECT json_agg(r ORDER BY life) FROM revora_family_feud r WHERE uid=@uid),
'tokens',(SELECT json_agg(r ORDER BY item_uid) FROM revora_family_feud_materials r WHERE uid=@uid),
'counts',(SELECT json_agg(r ORDER BY life,stage) FROM revora_family_feud_kills r WHERE uid=@uid),
'encounters',(SELECT json_agg(r ORDER BY attempt) FROM revora_family_feud_encounter_kills r WHERE uid=@uid),
'visits',(SELECT json_agg(r ORDER BY visit) FROM revora_family_visits r WHERE uid=@uid),
'treasure',(SELECT json_agg(r ORDER BY life) FROM revora_desert_treasure r WHERE uid=@uid),
'materials',(SELECT json_agg(r ORDER BY item_uid) FROM revora_desert_treasure_materials r WHERE uid=@uid),
'vault',(SELECT json_agg(r ORDER BY life) FROM revora_desert_treasure_vault r WHERE uid=@uid),
'bids',(SELECT json_agg(r ORDER BY id) FROM revora_desert_treasure_bids r WHERE uid=@uid),
'captures',(SELECT json_agg(r ORDER BY attempt) FROM revora_desert_treasure_captures r WHERE uid=@uid),
'quiz',(SELECT json_agg(r ORDER BY life) FROM revora_desert_treasure_quiz r WHERE uid=@uid))::text","prior",FreshDesertHeroSnapshot(p),"uid",(long)p.Entity.UID));
    }
    static void FreshFamilyTreasureSave(GameState p,string stage,int kills,int hits) {
        Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_fresh_family_treasure_probe SET stage=@stage,kills=@kills,hits=@hits,snapshot=@snapshot","stage",stage,"kills",kills,"hits",hits,"snapshot",FreshFamilyTreasureSnapshot(p));
    }
    static void FreshFamilyTreasureProcess(string phase) {
        var p=FreshLoadSaved(true,true);EntityVariableTable.Load(p);SubClassTable.Load(p.Entity);ChiTable.Load(p);p.Quests.Load();Revora.Database.KnownPersons.LoadMentor(p);WayofHeroes.Load(p);
        Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        if((bool)Sql("SELECT to_regclass('revora_fresh_family_treasure_probe') IS NULL")) {
            Require(phase.EndsWith("prepare") && FreshDesertHeroSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_desert_hero_probe")) && (long)Sql("SELECT (SELECT count(*) FROM revora_family_feud WHERE uid=@uid)+(SELECT count(*) FROM revora_desert_treasure WHERE uid=@uid)","uid",(long)p.Entity.UID)==0);
            Sql("CREATE TABLE revora_fresh_family_treasure_probe(stage text,kills integer,hits integer,snapshot text);INSERT INTO revora_fresh_family_treasure_probe VALUES('new',0,0,@snapshot)","snapshot",FreshFamilyTreasureSnapshot(p));
        }
        Require(FreshFamilyTreasureSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_family_treasure_probe")));
        string stage=Convert.ToString(Sql("SELECT stage FROM revora_fresh_family_treasure_probe"));int kills=Convert.ToInt32(Sql("SELECT kills FROM revora_fresh_family_treasure_probe")),hits=Convert.ToInt32(Sql("SELECT hits FROM revora_fresh_family_treasure_probe"));
        if(phase.EndsWith("prepare")) {
            Require(stage=="new");FreshClassicTalk(p,1011,10012,0);var bank=p.Warehouses.First(w=>(uint)w.Key==10012).Value;int store=p.Inventory.Count-15;Require(store>=0 && bank.Count+store<=60);var prior=p.Inventory.Objects.Select(i=>(long)i.UID).ToArray();
            foreach(var item in p.Inventory.Objects.Where(i=>i.Days==0).Take(store).ToArray()) {
                var frame=new byte[172];Revora.Network.Writer.WriteUInt16(164,0,frame);Revora.Network.Writer.WriteUInt16(1102,2,frame);Revora.Network.Writer.WriteUInt32(10012,4,frame);frame[12]=1;frame[13]=10;Revora.Network.Writer.WriteUInt32(item.UID,18,frame);Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,164,frame);Warehouse.Process(frame,p);Require(!p.Inventory.ContainsUID(item.UID) && bank.ContainsUID(item.UID));
            }
            Require(p.Inventory.Count==15 && (long)Sql("SELECT count(*) FROM items WHERE entityid=@uid AND uid=ANY(@prior)","uid",(long)p.Entity.UID,"prior",prior)==prior.Length);FreshFamilyTreasureSave(p,"stored",kills,hits);
        } else if(phase.EndsWith("family")) {
            Require(stage=="stored");var clock=Feature("FamilyFeudStore").GetField("Clock",Hidden);object original=clock.GetValue(null);DateTime now=DateTime.UtcNow;
            try {
                clock.SetValue(null,(Func<DateTime>)(()=>now));
                Action<uint,ushort,ushort> encounter=(kind,x,y)=>{Require(p.Entity.MapID==1015);p.Entity.X=x;p.Entity.Y=y;now=now.AddSeconds(4);Call(Feature("FamilyFeudQuest"),"Tick");FreshClassicKill(p,1015,9918190+kind,ref kills,ref hits);Call(Feature("FamilyFeudQuest"),"Tick");};
                FreshClassicTalk(p,1002,900071727,161);FreshClassicTalk(p,1015,4705,1);for(int i=0;i<50;i++)FreshDesertHeroKill(p,1015,new uint[]{55},ref kills,ref hits);
                encounter(0,363,468);FreshClassicTalk(p,1015,4705,1);for(int i=0;!p.Inventory.Contains(710563,1) && i<100;i++)FreshDesertHeroKill(p,1015,new uint[]{79},ref kills,ref hits);Require(p.Inventory.Contains(710563,1));FreshClassicTalk(p,1015,4705,1);
                for(int i=0;i<10;i++)FreshDesertHeroKill(p,1015,new uint[]{84},ref kills,ref hits);encounter(1,526,344);FreshClassicTalk(p,1015,4705,1);for(int i=0;i<10;i++)encounter(2,526,344);FreshClassicTalk(p,1015,4705,1);
                for(int i=0;!p.Inventory.Contains(710566,1) && i<100;i++)encounter(3,571,415);Require(p.Inventory.Contains(710566,1));FreshClassicTalk(p,1015,4705,1);Require(p.Entity.Mesh==201);FreshClassicTalk(p,1015,4706,1);Require(p.Map.BaseID==1801 && p.Entity.Mesh!=201);
                Action<byte> kerry=opt=>{var n=p.Map.Npcs[4707];p.Entity.X=(ushort)(n.X-2);p.Entity.Y=n.Y;p.ActiveNpc=4707;Npcs.GetDialog(new NpcRequest(true){NpcID=4707,OptionID=opt},p);Require(!PirateFenced(p));};kerry(1);ushort[,] pads={{50,60},{54,64},{58,64},{60,60},{58,60}};
                for(int i=0;i<5;i++){now=now.AddSeconds(1);p.lastJumpDistance=0;GeneralData.PlayerJump(new Data(true){UID=p.Entity.UID,ID=Data.Jump,wParam1=p.Entity.X,wParam2=p.Entity.Y,Xt1=pads[i,0],Yt1=pads[i,1],TimeStamp=Time32.Now},p);Require(p.Entity.X==pads[i,0] && p.Entity.Y==pads[i,1] && Convert.ToInt32(Sql("SELECT puzzle_step FROM revora_family_visits WHERE uid=@uid AND active","uid",(long)p.Entity.UID))==i+1);}
                kerry(2);Require(p.Entity.MapID==1015);FreshClassicTalk(p,1015,4705,1);FreshClassicTalk(p,1002,900071727,0);
            } finally {clock.SetValue(null,original);}
            Require((long)Sql("SELECT count(*) FROM revora_family_feud WHERE uid=@uid AND life=2 AND stage=9 AND cardinality(rewards)=0","uid",(long)p.Entity.UID)==1 && (long)Sql("SELECT count(*) FROM revora_family_visits WHERE uid=@uid AND life=2 AND puzzle_step=5 AND NOT active","uid",(long)p.Entity.UID)==1);FreshFamilyTreasureSave(p,"family",kills,hits);
        } else if(phase.EndsWith("treasure")) {
            Require(stage=="family");var clock=Feature("DesertTreasureStore").GetField("Clock",Hidden);object original=clock.GetValue(null);DateTime now=DateTime.UtcNow;
            try {
                clock.SetValue(null,(Func<DateTime>)(()=>now));FreshClassicTalk(p,1002,900071727,171);FreshClassicTalk(p,1000,4651,1);
                for(int attempt=0;!p.Inventory.Contains(721875,1) && attempt<20;attempt++) {
                    for(int i=0;!p.Inventory.Contains(721876,1) && i<100;i++)FreshDesertHeroKill(p,1001,new uint[]{20,80},ref kills,ref hits);Require(p.Inventory.Contains(721876,1));uint key=p.Inventory.Objects.Single(i=>i.ID==721876).UID;
                    p.Entity.X=90;p.Entity.Y=283;ItemHandler.UseItem(p.Inventory.Objects.Single(i=>i.ID==721873),p);p.lastJumpDistance=0;GeneralData.PlayerJump(new Data(true){UID=p.Entity.UID,ID=Data.Jump,wParam1=90,wParam2=283,Xt1=91,Yt1=283,TimeStamp=Time32.Now},p);Require(p.Entity.X==91 && p.Entity.Y==283);
                    p.ActiveNpc=9918281;Npcs.GetDialog(new NpcRequest(true){NpcID=9918281,OptionID=1},p);Require(!p.Inventory.ContainsUID(key) && !PirateFenced(p));now=now.AddSeconds(1);
                }
                Require(p.Inventory.Contains(721875,1));ItemHandler.UseItem(p.Inventory.Objects.Single(i=>i.ID==721875),p);FreshClassicTalk(p,1000,4651,1);ulong silver=p.Entity.Money;FreshClassicTalk(p,1001,4652,12);Require(p.Entity.Money==silver-99999+100);string paid=FreshFamilyTreasureSnapshot(p);FreshClassicTalk(p,1001,4652,12);Require(paid==FreshFamilyTreasureSnapshot(p));
                ItemHandler.UseItem(p.Inventory.Objects.Single(i=>i.ID==721878),p);FreshClassicTalk(p,1001,4652,1);for(int i=0;p.Inventory.Objects.Count(item=>item.ID==721877)<5 && i<100;i++)FreshDesertHeroKill(p,1001,new uint[]{56,81,86},ref kills,ref hits);Require(p.Inventory.Objects.Count(i=>i.ID==721877)==5);
                FreshClassicTalk(p,1000,4651,0);ItemHandler.UseItem(p.Inventory.Objects.Single(i=>i.ID==721878),p);FreshClassicTalk(p,1000,4651,1);p.Entity.Teleport(1001,727,675);now=now.AddSeconds(4);Call(Feature("DesertTreasureQuest"),"Tick");FreshClassicKill(p,1001,9918280,ref kills,ref hits);ItemHandler.UseItem(p.Inventory.Objects.Single(i=>i.ID==721874),p);Call(Feature("DesertTreasureQuest"),"Tick");Require(p.Inventory.Contains(721872,1));
                FreshClassicTalk(p,1000,4651,1);foreach(byte option in new byte[]{61,65,67})FreshClassicTalk(p,1000,4651,option);Require((long)Sql("SELECT count(*) FROM revora_desert_treasure_quiz WHERE uid=@uid AND life=2 AND step=3","uid",(long)p.Entity.UID)==1);string quiz=FreshFamilyTreasureSnapshot(p);foreach(byte option in new byte[]{61,65,67})FreshClassicTalk(p,1000,4651,option);Require(quiz==FreshFamilyTreasureSnapshot(p));FreshClassicTalk(p,1002,900071727,0);
            } finally {clock.SetValue(null,original);}
            Require((long)Sql("SELECT count(*) FROM revora_desert_treasure WHERE uid=@uid AND life=2 AND stage=7 AND cardinality(rewards)=0","uid",(long)p.Entity.UID)==1 && (long)Sql("SELECT count(*) FROM revora_desert_treasure_captures WHERE uid=@uid AND life=2","uid",(long)p.Entity.UID)==1 && (long)Sql("SELECT count(*) FROM revora_desert_treasure_bids WHERE uid=@uid AND life=2 AND won AND price=99999","uid",(long)p.Entity.UID)==1);FreshFamilyTreasureSave(p,"ready",kills,hits);
        } else if(phase.EndsWith("claim")) {
            Require(stage=="ready");var prior=p.Inventory.Objects.Select(i=>i.UID).ToArray();FreshClassicTalk(p,1002,900071727,161);FreshClassicTalk(p,1002,900071727,171);var added=p.Inventory.Objects.Where(i=>!prior.Contains(i.UID)).ToArray();Require(added.Length==6 && added.All(i=>i.Bound) && prior.All(uid=>p.Inventory.ContainsUID(uid)));
            Require((long)Sql("SELECT count(*) FROM revora_family_feud WHERE uid=@uid AND life=2 AND stage=10 AND cardinality(rewards)=3","uid",(long)p.Entity.UID)==1 && (long)Sql("SELECT count(*) FROM revora_desert_treasure WHERE uid=@uid AND life=2 AND stage=8 AND cardinality(rewards)=3","uid",(long)p.Entity.UID)==1);FreshFamilyTreasureSave(p,"paid",kills,hits);
        } else Require(phase.EndsWith("reload") && stage=="paid");
        if(phase.EndsWith("claim") || phase.EndsWith("reload")) {
            var ids=((long[])Sql("SELECT rewards FROM revora_family_feud WHERE uid=@uid AND life=2","uid",(long)p.Entity.UID)).Concat((long[])Sql("SELECT rewards FROM revora_desert_treasure WHERE uid=@uid AND life=2","uid",(long)p.Entity.UID)).ToArray();Require(ids.Length==6 && ids.Distinct().Count()==6);var rewards=p.Inventory.Objects.Where(i=>ids.Contains((long)i.UID)).ToArray();Require(rewards.Length==6 && rewards.All(i=>i.Bound) && rewards.Count(i=>i.ID==728248)==1 && rewards.Count(i=>i.ID==728249)==1 && rewards.Count(i=>i.ID==723911)==2 && rewards.Count(i=>ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7)==2);
            string before=FreshFamilyTreasureSnapshot(p);FreshClassicTalk(p,1002,900071727,161);FreshClassicTalk(p,1002,900071727,171);Require(before==FreshFamilyTreasureSnapshot(p));
        }
        Require(FreshFamilyTreasureSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_family_treasure_probe")) && !PirateFenced(p));System.Console.WriteLine("[career-process] "+phase+"-passed uid="+p.Entity.UID+" level="+p.Entity.Level+" kills="+kills+" hits="+hits+" bag="+p.Inventory.Count+" phoenix="+p.Warehouses.First(w=>(uint)w.Key==10012).Value.Count);Server.CommandsAI("@close");throw new InvalidOperationException("Fresh Family/Treasure did not close.");
    }
}
