using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Console=System.Console;
internal static partial class CheckCandidateArenaLifecycle {
    static string FreshTreasurySnapshot(GameState p){return Convert.ToString(Sql(@"SELECT json_build_object('career',@career::json,
'keys',(SELECT json_agg(r ORDER BY item_uid) FROM revora_dis_keys r WHERE uid=@uid),
'treasury',(SELECT row_to_json(r) FROM revora_dis_treasury r WHERE uid=@uid),
'openings',(SELECT json_agg(r ORDER BY opening) FROM revora_dis_treasury_claims r WHERE uid=@uid),
'packs',(SELECT json_agg(r ORDER BY result_uid) FROM revora_dis_refinery_packs r WHERE uid=@uid),
'relics',(SELECT json_agg(r ORDER BY item_uid) FROM revora_dis_relics r WHERE uid=@uid),'finish',(SELECT json_agg(r ORDER BY day) FROM revora_dis_finishes r WHERE uid=@uid),'rank',(SELECT json_agg(r ORDER BY day) FROM revora_dis_rank_prizes r WHERE uid=@uid),'horns',(SELECT json_agg(r ORDER BY item_uid) FROM revora_dis_horn_claims r WHERE uid=@uid))::text","career",FreshDisSnapshot(p),"uid",(long)p.Entity.UID));}
    static void FreshTreasuryJourney(bool reload){
        var p=FreshLoadSaved(true,true,true);EntityVariableTable.Load(p);SubClassTable.Load(p.Entity);ChiTable.Load(p);p.Quests.Load();Revora.Database.KnownPersons.LoadMentor(p);WayofHeroes.Load(p);Server.GamePool=new[]{p};Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));DateTime now=new DateTime(2041,3,12,20,0,0,DateTimeKind.Utc);Feature("DisCityQuest").GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));
        if((bool)Sql("SELECT to_regclass('revora_fresh_treasury_probe') IS NULL")){
            Require(!reload && FreshDisSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_dis_probe")));
            Sql("CREATE TABLE revora_fresh_treasury_probe(done boolean NOT NULL,snapshot text);INSERT INTO revora_fresh_treasury_probe VALUES(false,@snapshot)","snapshot",FreshTreasurySnapshot(p));
        }
        if(!reload){
            var key=p.Inventory.Objects.Single(i=>i.ID==3006982);string before=FreshTreasurySnapshot(p);ItemHandler.UseItem(key,p);Require(FreshTreasurySnapshot(p)==before);
            HeartTalk(p,3215,3);Require(p.Action==2 && p.Map.BaseID==3954);HeartTalk(p,900000089,1);Require(p.Inventory.ContainsUID(key.UID));
            HeartTalk(p,900000089,0);System.Threading.Tasks.Parallel.For(0,12,i=>Npcs.GetDialog(new NpcRequest(true){NpcID=900000089,OptionID=1},p));
            Require(p.Action==2 && !p.Inventory.ContainsUID(key.UID));Require((long)Sql("SELECT count(*) FROM revora_dis_treasury_claims WHERE uid=@uid AND opening=1 AND key_uid=@key","uid",(long)p.Entity.UID,"key",(long)key.UID)==1);
            before=FreshTreasurySnapshot(p);ItemHandler.UseItem(key,p);Npcs.GetDialog(new NpcRequest(true){NpcID=900000089,OptionID=1},p);Require(FreshTreasurySnapshot(p)==before);
            HeartTalk(p,3215,9);Require(p.Entity.MapID==1002);HeartTalk(p,3215,0);before=FreshTreasurySnapshot(p);HeartTalk(p,3215,3);Require(p.Entity.MapID==1002 && FreshTreasurySnapshot(p)==before);
            var rankBefore=p.Inventory.Objects.Select(i=>i.UID).ToArray();HeartTalk(p,3215,60);var rankAdded=p.Inventory.Objects.Where(i=>!rankBefore.Contains(i.UID)).ToArray();Require(rankAdded.Length==3 && rankAdded.All(i=>i.ID==3008058 && i.Bound));before=FreshTreasurySnapshot(p);HeartTalk(p,3215,60);Require(FreshTreasurySnapshot(p)==before);
            Require((long)Sql("SELECT count(*) FROM revora_dis_rank_prizes WHERE uid=@uid AND day='20410311' AND rank=1 AND quantity=3 AND claimed_at IS NOT NULL","uid",(long)p.Entity.UID)==1);
            foreach(var pack in p.Inventory.Objects.Where(i=>i.ID==3008057 || i.ID==3008058).ToArray()){
                Require(pack.Bound && pack.StackSize==1);ItemHandler.UseItem(pack,p);Require(p.Action==2 && !p.Inventory.ContainsUID(pack.UID));
                Require((long)Sql("SELECT count(*) FROM revora_dis_refinery_packs r JOIN items i ON i.uid=r.result_uid AND i.entityid=r.uid AND i.id=r.result_type WHERE r.uid=@uid AND r.pack_uid=@pack AND i.bound=1","uid",(long)p.Entity.UID,"pack",(long)pack.UID)==1);
                before=FreshTreasurySnapshot(p);ItemHandler.UseItem(pack,p);Require(FreshTreasurySnapshot(p)==before);
            }
            Require(p.Entity.Level<Config.LevelCap);var horn=p.Inventory.Objects.Single(i=>i.ID==790001);byte level=p.Entity.Level;ulong award=(ulong)p.ExpBall*5;HeartTalk(p,3215,5);HeartTalk(p,3215,50);Require(p.Action==2 && !p.Inventory.ContainsUID(horn.UID));Require((bool)Sql("SELECT EXISTS(SELECT 1 FROM revora_dis_horn_claims h JOIN revora_dis_relics r ON r.item_uid=h.item_uid WHERE h.uid=@uid AND h.item_uid=@item AND h.experience=@exp AND h.level_before=@before AND h.level_after=@after AND r.used_at IS NOT NULL)","uid",(long)p.Entity.UID,"item",(long)horn.UID,"exp",(long)award,"before",(int)level,"after",(int)p.Entity.Level));before=FreshTreasurySnapshot(p);HeartTalk(p,3215,50);Require(FreshTreasurySnapshot(p)==before);Console.WriteLine("[fresh-treasury] horn exp="+award+" level="+level+"->"+p.Entity.Level+" rank-packs="+rankAdded.Length);
            Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_fresh_treasury_probe SET done=true,snapshot=@snapshot","snapshot",FreshTreasurySnapshot(p));
        }
        Require((bool)Sql("SELECT done FROM revora_fresh_treasury_probe") && p.Entity.MapID==1002 && FreshTreasurySnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_treasury_probe")));
        string exact=FreshTreasurySnapshot(p);HeartTalk(p,3215,60);HeartTalk(p,3215,50);Require(FreshTreasurySnapshot(p)==exact && !PirateFenced(p));
        Console.WriteLine("[career-process] career-fresh-treasury-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Treasury did not close.");
    }
}
