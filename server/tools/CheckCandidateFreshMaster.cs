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
    static string FreshMasterSnapshot(GameState p){return Convert.ToString(Sql(@"SELECT json_build_object('prior',@prior::json,'purchase',(SELECT json_agg(r ORDER BY purchased_at,id) FROM revora_honor_purchases r WHERE uid=@uid AND item_type=720842),'runs',(SELECT json_agg(r ORDER BY started_at,id) FROM revora_sword_master_runs r WHERE uid=@uid),'loot',(SELECT json_agg(l ORDER BY l.run,l.slot) FROM revora_sword_master_loot l JOIN revora_sword_master_runs r ON r.id=l.run WHERE r.uid=@uid),'study',(SELECT json_agg(s ORDER BY s.run,s.uid) FROM revora_sword_master_study s JOIN revora_sword_master_runs r ON r.id=s.run WHERE r.uid=@uid))::text","prior",FreshHonorSnapshot(p),"uid",(long)p.Entity.UID));}
    static void FreshMasterJourney(string phase){
        bool buy=phase.EndsWith("buy"),win=phase.EndsWith("win"),loot=phase.EndsWith("loot");
        var p=FreshLoadSaved(true,true,true);EntityVariableTable.Load(p);SubClassTable.Load(p.Entity);ChiTable.Load(p);p.Quests.Load();Revora.Database.KnownPersons.LoadMentor(p);WayofHeroes.Load(p);Server.GamePool=new[]{p};Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p) && !p.Entity.Dead);
        var encounter=Feature("SwordMasterEncounter");var rewards=Feature("SwordMasterRewards");DateTime now=new DateTime(2041,4,4,20,0,0,DateTimeKind.Utc).AddSeconds(buy?0:win?10:loot?20:30);Feature("SwordMasterAdmission").GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));Feature("HonorPurchases").GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));
        Func<Entity[]> bosses=()=>((System.Collections.IDictionary)encounter.GetField("Actors",Hidden).GetValue(null)).Values.Cast<object>().Where(a=>ReferenceEquals(a.GetType().GetField("Owner",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(a),p)).Select(a=>(Entity)a.GetType().GetField("Body",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(a)).ToArray();
        Action confirm=()=>{var frame=new byte[28];Revora.Network.Writer.WriteUInt16(20,0,frame);Revora.Network.Writer.WriteUInt16(2032,2,frame);Revora.Network.Writer.WriteUInt32(720842,8,frame);frame[14]=1;Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,20,frame);Revora.Network.PacketHandler.HandlePacket(frame,p);};
        if(buy){
            Require(FreshHonorSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_honor_probe WHERE stage=3")) && p.CurrentHonor>=8000 && p.Inventory.Count<=37 && !p.Inventory.Objects.Any(i=>i.ID==720842));Require((bool)Sql("SELECT to_regclass('revora_fresh_master_probe') IS NULL"));
            HeartTalk(p,30156,0);uint honor=p.CurrentHonor;var purchase=new ItemUsage(true){UID=HonorShop.UID,dwParam=720842};ItemHandler.HandleBuyFromNPC(purchase,p);ItemHandler.HandleBuyFromNPC(purchase,p);
            var soul=p.Inventory.Objects.Single(x=>x.ID==720842);Require(p.Action==2 && p.CurrentHonor==honor-8000 && p.TeamArenaStatistic.CurrentHonor==p.CurrentHonor);
            Require((long)Sql("SELECT count(*) FROM revora_honor_purchases WHERE uid=@uid AND item_uid=@item AND cost=8000 AND honor_before=@before AND honor_after=@after","uid",(long)p.Entity.UID,"item",(long)soul.UID,"before",(long)honor,"after",(long)(honor-8000))==1);
            Sql("CREATE TABLE revora_fresh_master_probe(item bigint NOT NULL,stage int NOT NULL,snapshot text);INSERT INTO revora_fresh_master_probe VALUES(@item,0,NULL)","item",(long)soul.UID);Console.WriteLine("[fresh-master] purchased-honor=8000 remaining-honor="+p.CurrentHonor);
        }else{
            Require(FreshMasterSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_master_probe")));
            if(win){
                Require((int)Sql("SELECT stage FROM revora_fresh_master_probe")==1);var soul=p.Inventory.Objects.Single(x=>x.UID==Convert.ToUInt32(Sql("SELECT item FROM revora_fresh_master_probe")));var home=(Map)Call(Feature("Housing"),"Load",p);HeartTalk(p,30156,2);Require(p.Entity.MapID==home.ID);uint study=p.Entity.SubClasses.StudyPoints;
                ItemHandler.UseItem(soul,p);confirm();confirm();Require(p.Action==2 && !p.Inventory.ContainsUID(soul.UID) && bosses().Length==1);var boss=bosses().Single();Require(boss.Hitpoints==15000000);
                p.Attackable=true;p.CantAttack=Time32.Now.AddSeconds(-1);int hits=0;while(!boss.Dead && hits<5000){p.Entity.X=boss.X;p.Entity.Y=(ushort)(boss.Y+1);p.Screen.Add(boss);p.Entity.AttackStamp=Time32.Now.AddSeconds(-2);Attack.HandleAttack(new Attack(true){Attacker=p.Entity.UID,Attacked=boss.UID,X=boss.X,Y=boss.Y,AttackType=Attack.Melee},p);hits++;}Require(boss.Dead && p.Action==2 && !p.Entity.Dead);Call(rewards,"Tick",true);
                Require(p.Entity.SubClasses.StudyPoints==study+20 && (long)Sql("SELECT count(*) FROM revora_sword_master_runs WHERE uid=@uid AND state='victory' AND item_uid=@item","uid",(long)p.Entity.UID,"item",(long)soul.UID)==1);
                Require((long)Sql("SELECT count(*) FROM revora_sword_master_loot l JOIN revora_sword_master_runs r ON r.id=l.run WHERE r.uid=@uid AND l.collector IS NULL","uid",(long)p.Entity.UID)==3);Console.WriteLine("[fresh-master] calculated-melee-hits="+hits);
            }else if(loot){
                Require((int)Sql("SELECT stage FROM revora_fresh_master_probe")==2);Call(Feature("Housing"),"Load",p);Call(rewards,"Tick",true);var drops=p.Map.FloorItems.Values.Where(d=>(bool)Call(rewards,"Managed",d)).ToArray();Require(drops.Length==3);int count=p.Inventory.Count;
                foreach(var drop in drops){p.Entity.X=drop.X;p.Entity.Y=drop.Y;var packet=new FloorItem(true){UID=drop.UID,Type=FloorItem.Remove}.ToArray();Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,packet.Length-8,packet);FloorItem.Process(packet,p);FloorItem.Process(packet,p);Require(p.Inventory.ContainsUID(drop.Item.UID));}
                Require(p.Inventory.Count==count+3 && drops.Count(d=>ConquerItemInformation.BaseInformations[d.ItemID].PurificationLevel==7)==1 && drops.Where(d=>ConquerItemInformation.BaseInformations[d.ItemID].PurificationLevel==7).All(d=>d.Item.Bound));
            }else{
                Require(phase.EndsWith("reload") && (int)Sql("SELECT stage FROM revora_fresh_master_probe")==3);string before=FreshMasterSnapshot(p);confirm();Call(rewards,"Tick",true);Require(bosses().Length==0 && p.Map.FloorItems.Values.Count(d=>(bool)Call(rewards,"Managed",d))==0 && before==FreshMasterSnapshot(p));
            }
        }
        Require(!PirateFenced(p) && EntityTable.SaveEntity(p));
        if(buy || win || loot){Sql("UPDATE revora_fresh_master_probe SET stage=stage+1,snapshot=@snapshot","snapshot",FreshMasterSnapshot(p));Console.WriteLine("[career-process] "+phase+"-passed");Console.Out.Flush();Environment.Exit(0);return;}
        Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10528 AND source='sword-master-kill-v1'","uid",(long)p.Entity.UID)==1);
        var collected=(long[])Sql("SELECT array_agg(l.item_uid ORDER BY l.slot) FROM revora_sword_master_loot l JOIN revora_sword_master_runs r ON r.id=l.run WHERE r.uid=@uid AND l.collector=@uid","uid",(long)p.Entity.UID);Require(collected.Length==3 && collected.All(id=>p.Inventory.ContainsUID((uint)id)));
        Require((long)Sql("SELECT count(*) FROM revora_sword_master_study WHERE uid=@uid AND points=20 AND paid_at IS NOT NULL","uid",(long)p.Entity.UID)==1);
        Require(FreshMasterSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_master_probe")));Console.WriteLine("[career-process] "+phase+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Fresh Sword Master did not close.");
    }
}
