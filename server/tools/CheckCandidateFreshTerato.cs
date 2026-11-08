using System;
using System.Linq;
using System.Reflection;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
using Console=System.Console;
internal static partial class CheckCandidateArenaLifecycle {
    static string FreshTeratoSnapshot(GameState p){return Convert.ToString(Sql(@"SELECT json_build_object('prior',@prior::json,'runs',(SELECT json_agg(r ORDER BY id) FROM revora_terato_runs r WHERE uid=1096322),'materials',(SELECT json_agg(m ORDER BY m.run,m.item_uid) FROM revora_terato_materials m JOIN revora_terato_runs r ON r.id=m.run WHERE r.uid=1096322),'loot',(SELECT json_agg(l ORDER BY l.run,l.slot) FROM revora_terato_loot l JOIN revora_terato_runs r ON r.id=l.run WHERE r.uid=1096322),'study',(SELECT json_agg(s ORDER BY s.run,s.uid) FROM revora_terato_study s JOIN revora_terato_runs r ON r.id=s.run WHERE r.uid=1096322))::text","prior",FreshDragonSnapshot(p)));}
    static void FreshTeratoJourney(string phase){
        Require(new[]{"career-fresh-terato-summon","career-fresh-terato-win","career-fresh-terato-loot","career-fresh-terato-reload"}.Contains(phase));var p=FreshLoadSaved(true,true,true);EntityVariableTable.Load(p);SubClassTable.Load(p.Entity);ChiTable.Load(p);p.Quests.Load();Revora.Database.KnownPersons.LoadMentor(p);WayofHeroes.Load(p);Server.GamePool=new[]{p};Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        var encounter=Feature("TeratoEncounter");var rewards=Feature("TeratoRewards");var instance=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
        if(phase.EndsWith("summon")){
            Require(FreshDragonSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_dragon_hunts_probe")));
            Sql("CREATE TABLE revora_fresh_terato_probe(stamp timestamptz NOT NULL,stage int NOT NULL,snapshot text NOT NULL);INSERT INTO revora_fresh_terato_probe VALUES(now(),0,@snapshot)","snapshot",FreshTeratoSnapshot(p));
        }
        DateTime start=Convert.ToDateTime(Sql("SELECT stamp FROM revora_fresh_terato_probe")).ToUniversalTime();DateTime now=start.AddSeconds(phase.EndsWith("summon")?0:phase.EndsWith("win")?10:phase.EndsWith("loot")?20:30);encounter.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));
        Require(FreshTeratoSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_terato_probe")));
        Func<Entity[]> actors=()=>((System.Collections.IDictionary)encounter.GetField("Actors",Hidden).GetValue(null)).Values.Cast<object>().Where(a=>ReferenceEquals(a.GetType().GetField("Owner",instance).GetValue(a),p)).Select(a=>(Entity)a.GetType().GetField("Body",instance).GetValue(a)).ToArray();
        if(phase.EndsWith("summon")){
            HeartTalk(p,900000127,0);var pill=p.Inventory.Objects.Single(x=>x.ID==720598);uint uid=pill.UID;ItemHandler.UseItem(pill,p);Require(!p.Inventory.ContainsUID(uid) && actors().Length==1);
            Require((long)Sql("SELECT count(*) FROM revora_terato_runs WHERE uid=@uid AND state='active' AND valuable_reward","uid",(long)p.Entity.UID)==1);
            HeartTalk(p,900000127,3);Require(actors().Length==1 && (long)Sql("SELECT count(*) FROM revora_terato_materials m JOIN revora_terato_runs r ON r.id=m.run WHERE r.uid=@uid","uid",(long)p.Entity.UID)==1);
        }else if(phase.EndsWith("win")){
            Require((int)Sql("SELECT stage FROM revora_fresh_terato_probe")==1 && !p.Inventory.Objects.Any(x=>x.ID==720598));HeartTalk(p,900000127,3);var boss=actors().Single();Require(boss.Hitpoints==50000000);uint study=p.Entity.SubClasses.StudyPoints;
            p.Entity.X=boss.X;p.Entity.Y=(ushort)(boss.Y+1);p.Screen.Add(boss);p.Attackable=true;p.CantAttack=Time32.Now.AddSeconds(-1);int hits=0;while(!boss.Dead && hits++<10000){p.Entity.AttackStamp=Time32.Now.AddSeconds(-2);Attack.HandleAttack(new Attack(true){Attacker=p.Entity.UID,Attacked=boss.UID,X=boss.X,Y=boss.Y,AttackType=Attack.Melee},p);}Require(boss.Dead && p.Action==2 && !p.Entity.Dead && !PirateFenced(p));Call(rewards,"Tick",true);
            Require(p.Entity.SubClasses.StudyPoints==study+50 && (long)Sql("SELECT count(*) FROM revora_terato_loot l JOIN revora_terato_runs r ON r.id=l.run WHERE r.uid=@uid AND l.collector IS NULL","uid",(long)p.Entity.UID)==3);
            Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10526","uid",(long)p.Entity.UID)==1);Console.WriteLine("[career-terato] melee-hits="+hits);
        }else if(phase.EndsWith("loot")){
            Require((int)Sql("SELECT stage FROM revora_fresh_terato_probe")==2);Call(rewards,"Tick",true);var drops=p.Map.FloorItems.Values.Where(d=>(bool)Call(rewards,"Managed",d)).ToArray();Require(drops.Length==3);int count=p.Inventory.Count;
            foreach(var drop in drops){p.Entity.X=drop.X;p.Entity.Y=drop.Y;var packet=new FloorItem(true){UID=drop.UID,Type=FloorItem.Remove}.ToArray();Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,packet.Length-8,packet);FloorItem.Process(packet,p);FloorItem.Process(packet,p);Require(p.Inventory.ContainsUID(drop.Item.UID));}
            Require(p.Inventory.Count==count+3);Require(drops.Count(d=>d.Item.Bound && ConquerItemInformation.BaseInformations[d.ItemID].PurificationLevel==7)==1);
            Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10932","uid",(long)p.Entity.UID)==1);
        }else Require((int)Sql("SELECT stage FROM revora_fresh_terato_probe")==3);
        Require(EntityTable.SaveEntity(p));
        if(!phase.EndsWith("reload")){Sql("UPDATE revora_fresh_terato_probe SET stage=stage+1,snapshot=@snapshot","snapshot",FreshTeratoSnapshot(p));Console.WriteLine("[career-process] "+phase+"-passed");Console.Out.Flush();Environment.Exit(0);return;}
        string beforeReplay=FreshTeratoSnapshot(p);ushort savedX=p.Entity.X,savedY=p.Entity.Y;HeartTalk(p,900000127,3);Call(rewards,"Tick",true);Require(actors().Length==0 && p.Map.FloorItems.Values.Count(d=>(bool)Call(rewards,"Managed",d))==0);
        Require((long)Sql("SELECT count(*) FROM revora_terato_loot l JOIN revora_terato_runs r ON r.id=l.run JOIN items i ON i.uid=l.item_uid WHERE r.uid=@uid AND l.slot=2 AND l.bound AND i.bound=1","uid",(long)p.Entity.UID)==1);
        p.Entity.X=savedX;p.Entity.Y=savedY;Require(EntityTable.SaveEntity(p) && FreshTeratoSnapshot(p)==beforeReplay);
        Console.WriteLine("[career-process] "+phase+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Terato process did not close.");
    }
}
