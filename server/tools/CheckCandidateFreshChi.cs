using System;
using System.Linq;
using System.Reflection;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Console=System.Console;
internal static partial class CheckCandidateArenaLifecycle {
    static void FreshChiSave(GameState p,GameState[] players,int stage){foreach(var who in players)Require(EntityTable.SaveEntity(who));Sql("UPDATE revora_fresh_chi_probe SET stage=@stage,snapshot=@snapshot","stage",stage,"snapshot",FreshNobilitySnapshot(p,players));}
    static void FreshChiJourney(string phase){
        bool prepare=phase.EndsWith("prepare"),study=phase.EndsWith("study"),claim=phase.EndsWith("claim");var p=FreshLoadSaved(true,true,true,true);EntityVariableTable.Load(p);SubClassTable.Load(p.Entity);ChiTable.Load(p);p.Quests.Load();Revora.Database.KnownPersons.LoadMentor(p);WayofHeroes.Load(p);
        var list=new System.Collections.Generic.List<GameState>();foreach(int slot in new[]{1,2,3,0}){uint uid=Convert.ToUInt32(Sql("SELECT uid FROM revora_fresh_contributors WHERE slot=@slot","slot",slot));list.Add(FreshContributor(uid,Convert.ToString(Sql("SELECT name FROM revora_fresh_contributors WHERE slot=@slot","slot",slot))));}list.Add(p);var players=list.ToArray();var peer=players[3];Server.GamePool=players;Require(players.All(w=>!w.Fake && w.Account.State==AccountTable.AccountState.Player && !PirateFenced(w) && !w.Entity.Dead));




        uint[] stages={605,702,804,905,1003};
        if(prepare){
            Require(FreshNobilitySnapshot(p,players)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_nobility_probe WHERE stage=3")) && (bool)Sql("SELECT to_regclass('revora_fresh_chi_probe') IS NULL") && p.ChiPowers.Count==0 && p.ChiPoints==6000);
            int store=Math.Max(0,p.Inventory.Count-20);var bank=p.Warehouses[Revora.Game.ConquerStructures.Warehouse.WarehouseID.DesertCity];Require(bank.Count+store<=60);FreshClassicTalk(p,1000,10011,0);
            foreach(var item in p.Inventory.Objects.Where(i=>i.Days==0).Take(store).ToArray()){var frame=new byte[172];Revora.Network.Writer.WriteUInt16(164,0,frame);Revora.Network.Writer.WriteUInt16(1102,2,frame);Revora.Network.Writer.WriteUInt32(10011,4,frame);frame[12]=1;frame[13]=10;Revora.Network.Writer.WriteUInt32(item.UID,18,frame);Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,164,frame);Warehouse.Process(frame,p);Require(!p.Inventory.ContainsUID(item.UID) && bank.ContainsUID(item.UID));}Require(p.Inventory.Count<=20);p.Entity.Teleport(1002,408,330);
            string before=FreshNobilitySnapshot(p,players);foreach(uint stage in stages)NobilityClaimPacket(p,stage);Require(before==FreshNobilitySnapshot(p,players));Sql("CREATE TABLE revora_fresh_chi_probe(stage int,rolls int,cp_spent bigint,chi_spent bigint,items bigint[],snapshot text);INSERT INTO revora_fresh_chi_probe VALUES(0,0,0,0,NULL,NULL)");FreshChiSave(p,players,0);
        }else{
            Require(FreshNobilitySnapshot(p,players)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_chi_probe")));
            if(study){
                Require((int)Sql("SELECT stage FROM revora_fresh_chi_probe")==0);int rolls=0;var timer=System.Diagnostics.Stopwatch.StartNew();
                while(p.ChiPowers.Sum(x=>x.Points)<1580 && rolls<120 && timer.Elapsed.TotalSeconds<210){
                    byte power=(byte)(Enumerable.Range(0,p.ChiPowers.Count).FirstOrDefault(i=>p.ChiPowers[i].Points<396)+1);if(p.ChiPowers.Count==0 || p.ChiPowers.All(x=>x.Points>=396))power=(byte)(p.ChiPowers.Count+1);Require(power<=4);
                    uint cp=p.Entity.ConquerPoints,wallet=p.ChiPoints;
                    if(power>p.ChiPowers.Count){if(p.ChiPoints<300)ChiClaimRequest(p,Chi.BuyStrength,power);uint before=p.ChiPoints;ChiClaimRequest(p,Chi.Unlock,power);Require(p.ChiPowers.Count==power && p.ChiPoints==before-300);Sql("UPDATE revora_fresh_chi_probe SET cp_spent=cp_spent+@cp,chi_spent=chi_spent+300","cp",(long)(cp-p.Entity.ConquerPoints));FreshChiSave(p,players,0);continue;}
                    MainSixthStudy(p,power,99);rolls++;uint spentCp=cp-p.Entity.ConquerPoints;long spentChi=(spentCp==0?wallet:4000)-p.ChiPoints;Require(spentChi>=50 && spentChi<=200);Sql("UPDATE revora_fresh_chi_probe SET rolls=rolls+1,cp_spent=cp_spent+@cp,chi_spent=chi_spent+@chi","cp",(long)spentCp,"chi",spentChi);FreshChiSave(p,players,p.ChiPowers.Sum(x=>x.Points)>=1580?1:0);
                    if(rolls%20==0 || p.ChiPowers.Sum(x=>x.Points)>=1580)Console.WriteLine("[fresh-chi] rolls="+Sql("SELECT rolls FROM revora_fresh_chi_probe")+" score="+p.ChiPowers.Sum(x=>x.Points)+" cp="+Sql("SELECT cp_spent FROM revora_fresh_chi_probe"));
                }
            }else{
                Require(p.ChiPowers.Count==4 && p.ChiPowers.Sum(x=>x.Points)>=1580);
                if(claim){Require((int)Sql("SELECT stage FROM revora_fresh_chi_probe")==1);var all=new System.Collections.Generic.List<long>();for(int n=0;n<stages.Length;n++){var old=p.Inventory.Objects.Select(i=>i.UID).ToArray();NobilityClaimPacket(p,stages[n]);var added=p.Inventory.Objects.Where(i=>!old.Contains(i.UID)).ToArray();int count=new[]{1,1,3,5,10}[n];Require(added.Length==count && added.All(i=>i.Bound && i.Plus==0 && i.StackSize==1 && i.ID==(n==0?3005126u:3003126u)));if(n==0){uint chi=p.ChiPoints;SoulClaimUse(p,added[0]);Require(p.ChiPoints==chi+500 && !p.Inventory.ContainsUID(added[0].UID));string once=FreshNobilitySnapshot(p,players);SoulClaimUse(p,added[0]);Require(once==FreshNobilitySnapshot(p,players));}else all.AddRange(added.Select(i=>(long)i.UID));}Sql("UPDATE revora_fresh_chi_probe SET items=@items","items",all.ToArray());FreshChiSave(p,players,2);}else Require(phase.EndsWith("reload") && (int)Sql("SELECT stage FROM revora_fresh_chi_probe")==2);
                string saved=FreshNobilitySnapshot(p,players);foreach(uint stage in stages)NobilityClaimPacket(p,stage);Require(saved==FreshNobilitySnapshot(p,players));var items=(long[])Sql("SELECT items FROM revora_fresh_chi_probe");Require(items.Length==19 && items.All(id=>p.Inventory.Objects.Any(i=>i.UID==id && i.ID==3003126 && i.Bound)));
                Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=ANY(@stages) AND NOT legacy","uid",(long)p.Entity.UID,"stages",stages.Select(x=>(int)x).ToArray())==5);foreach(var who in players){var ids=(long[])Sql("SELECT coalesce(array_agg(uid ORDER BY uid),ARRAY[]::bigint[]) FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND coalesce(inwardrobe,0)=0","uid",(long)who.Entity.UID);Require(ids.SequenceEqual(who.Inventory.Objects.Select(i=>(long)i.UID).OrderBy(id=>id)) && EntityTable.SaveEntity(who));}Require(saved==FreshNobilitySnapshot(p,players));
            }
        }
        Require(!PirateFenced(p));Console.WriteLine("[career-process] "+phase+"-passed score="+p.ChiPowers.Sum(x=>x.Points)+" stage="+Sql("SELECT stage FROM revora_fresh_chi_probe"));Console.Out.Flush();if(prepare || study || claim){Environment.Exit(0);return;}Server.CommandsAI("@close");throw new InvalidOperationException("Fresh Chi did not close.");
    }
}
