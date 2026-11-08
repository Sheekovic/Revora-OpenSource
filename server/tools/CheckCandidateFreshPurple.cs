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
    static string FreshPurpleSnapshot(GameState p){return Convert.ToString(Sql(@"SELECT json_build_object('prior',@prior::json,'purchase',(SELECT json_agg(r ORDER BY purchased_at,id) FROM revora_honor_purchases r WHERE uid=@uid AND item_type=3004259),'runs',(SELECT json_agg(r ORDER BY started_at,id) FROM revora_purple_banshee_runs r WHERE uid=@uid),'loot',(SELECT json_agg(l ORDER BY l.run,l.slot) FROM revora_purple_banshee_loot l JOIN revora_purple_banshee_runs r ON r.id=l.run WHERE r.uid=@uid),'study',(SELECT json_agg(s ORDER BY s.run,s.uid) FROM revora_purple_banshee_study s JOIN revora_purple_banshee_runs r ON r.id=s.run WHERE r.uid=@uid))::text","prior",FreshMasterSnapshot(p),"uid",(long)p.Entity.UID));}
    static void FreshPurplePurchase(bool reload) {
        var p=FreshLoadSaved(true,true,true);EntityVariableTable.Load(p);SubClassTable.Load(p.Entity);ChiTable.Load(p);p.Quests.Load();Revora.Database.KnownPersons.LoadMentor(p);WayofHeroes.Load(p);Server.GamePool=new[]{p};Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p) && !p.Entity.Dead);
        Feature("HonorPurchases").GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>new DateTime(2041,4,5,20,0,0,DateTimeKind.Utc)));
        if(!reload) {
            Require(FreshMasterSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_master_probe WHERE stage=3")));
            Require((bool)Sql("SELECT to_regclass('revora_fresh_purple_probe') IS NULL") && p.CurrentHonor>=35000 && !p.Inventory.Objects.Any(i=>i.ID==3004259));
            FreshClassicTalk(p,1020,10028,0);var bank=p.Warehouses[Revora.Game.ConquerStructures.Warehouse.WarehouseID.ApeCity];int store=Math.Max(0,p.Inventory.Count-35);Require(bank.Count+store<=60);
            foreach(var item in p.Inventory.Objects.Where(i=>i.Days==0).Take(store).ToArray()){var frame=new byte[172];Revora.Network.Writer.WriteUInt16(164,0,frame);Revora.Network.Writer.WriteUInt16(1102,2,frame);Revora.Network.Writer.WriteUInt32(10028,4,frame);frame[12]=1;frame[13]=10;Revora.Network.Writer.WriteUInt32(item.UID,18,frame);Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,164,frame);Warehouse.Process(frame,p);Require(!p.Inventory.ContainsUID(item.UID) && bank.ContainsUID(item.UID));}
            Require(p.Inventory.Count<=35);FreshClassicTalk(p,1002,30156,0);uint honor=p.CurrentHonor;var request=new ItemUsage(true){UID=HonorShop.UID,dwParam=3004259};ItemHandler.HandleBuyFromNPC(request,p);ItemHandler.HandleBuyFromNPC(request,p);
            var soul=p.Inventory.Objects.Single(i=>i.ID==3004259);Require(p.CurrentHonor==honor-35000 && p.TeamArenaStatistic.CurrentHonor==p.CurrentHonor);
            Require((long)Sql("SELECT count(*) FROM revora_honor_purchases WHERE uid=@uid AND item_uid=@item AND item_type=3004259 AND cost=35000 AND honor_before=@before AND honor_after=@after","uid",(long)p.Entity.UID,"item",(long)soul.UID,"before",(long)honor,"after",(long)p.CurrentHonor)==1);
            Require(EntityTable.SaveEntity(p));Sql("CREATE TABLE revora_fresh_purple_probe(item bigint NOT NULL,stage int NOT NULL,snapshot text NOT NULL);INSERT INTO revora_fresh_purple_probe VALUES(@item,1,@snapshot)","item",(long)soul.UID,"snapshot",FreshPurpleSnapshot(p));
            Console.WriteLine("[fresh-purple] purchase-paid=35000 remaining-honor="+p.CurrentHonor+" stored="+store+" bag="+p.Inventory.Count);
        } else {
            Require((int)Sql("SELECT stage FROM revora_fresh_purple_probe")==1 && FreshPurpleSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_purple_probe")));
            var soul=p.Inventory.Objects.Single(i=>i.UID==Convert.ToUInt32(Sql("SELECT item FROM revora_fresh_purple_probe")));Require(soul.ID==3004259 && p.CurrentHonor==p.TeamArenaStatistic.CurrentHonor);
            Require((long)Sql("SELECT count(*) FROM revora_purple_banshee_runs WHERE uid=@uid","uid",(long)p.Entity.UID)==0);
            Console.WriteLine("[fresh-purple] cold-purchase-preserved honor="+p.CurrentHonor+" bag="+p.Inventory.Count+" minattack="+p.Entity.MinAttack+" maxattack="+p.Entity.MaxAttack);
            foreach(var item in p.Equipment.Objects.Where(i=>i!=null))Console.WriteLine("[fresh-purple] equipped="+item.ID+" durability="+item.Durability+" max="+item.MaximDurability);
        }
        Console.WriteLine("[career-process] career-fresh-purple-"+(reload?"purchase-reload":"buy")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Fresh Purple purchase did not close.");
    }
    static string FreshPurpleCombatSnapshot(GameState p){return Convert.ToString(Sql("SELECT json_build_object('career',@career::json,'repairs',(SELECT json_agg(r ORDER BY created_at,id) FROM revora_equipment_repairs r WHERE uid=@uid))::text","career",FreshPurpleSnapshot(p),"uid",(long)p.Entity.UID));}
    static void FreshPurpleMaintain(GameState p,bool initial=false){
        foreach(var item in p.Equipment.GetItems().Where(i=>(i.Position>=4 && i.Position<=6 || i.Position==10) && i.Durability<i.MaximDurability && (initial || i.Durability<750)).OrderBy(i=>i.Position==5?0:i.Position).ToArray()){
            Require(item.Durability>0 && item.MaximDurability>750 && p.Inventory.Count<37);byte slot=item.Position;ulong money=p.Entity.Money;long count=(long)Sql("SELECT count(*) FROM revora_equipment_repairs WHERE uid=@uid","uid",(long)p.Entity.UID);
            ItemHandler.UnequipItem(new ItemUsage(true){dwParam=slot,Action=ItemUsage.Mode.UnequipItem},p);Require(p.Inventory.ContainsUID(item.UID));RetainedUpgradeUsage(p,item.UID,0,ItemUsage.Mode.Repair);Require(item.Durability==item.MaximDurability && p.Entity.Money<money);
            ulong paid=p.Entity.Money;RetainedUpgradeUsage(p,item.UID,0,ItemUsage.Mode.Repair);Require(p.Entity.Money==paid && (long)Sql("SELECT count(*) FROM revora_equipment_repairs WHERE uid=@uid","uid",(long)p.Entity.UID)==count+1);
            ItemHandler.EquipItem(new ItemUsage(true){UID=item.UID,dwParam=slot,Action=ItemUsage.Mode.EquipItem},p);Require(item.Position==slot);Console.WriteLine("[fresh-purple] repair type="+item.ID+" silver="+(money-paid));
        }
    }
    static void FreshPurpleCombat(string phase){
        bool maintain=phase.EndsWith("maintain"),win=phase.EndsWith("win"),loot=phase.EndsWith("loot");
        var p=FreshLoadSaved(true,true,true);EntityVariableTable.Load(p);SubClassTable.Load(p.Entity);ChiTable.Load(p);p.Quests.Load();Revora.Database.KnownPersons.LoadMentor(p);WayofHeroes.Load(p);Server.GamePool=new[]{p};Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p) && !p.Entity.Dead);
        var encounter=Feature("PurpleBansheeEncounter");var rewards=Feature("PurpleBansheeRewards");DateTime now=new DateTime(2041,4,5,20,1,0,DateTimeKind.Utc).AddSeconds(maintain?0:win?10:loot?20:30);Feature("PurpleBansheeAdmission").GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));
        Func<Entity[]> bosses=()=>((System.Collections.IDictionary)encounter.GetField("Actors",Hidden).GetValue(null)).Values.Cast<object>().Where(a=>ReferenceEquals(a.GetType().GetField("Owner",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(a),p)).Select(a=>(Entity)a.GetType().GetField("Body",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(a)).ToArray();
        Action confirm=()=>{var frame=new byte[28];Revora.Network.Writer.WriteUInt16(20,0,frame);Revora.Network.Writer.WriteUInt16(2032,2,frame);Revora.Network.Writer.WriteUInt32(3004259,8,frame);frame[14]=1;Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,20,frame);Revora.Network.PacketHandler.HandlePacket(frame,p);};
        int stage=(int)Sql("SELECT stage FROM revora_fresh_purple_probe");Require((maintain?FreshPurpleSnapshot(p):FreshPurpleCombatSnapshot(p))==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_purple_probe")));
        if(maintain){Require(stage==1);FreshPurpleMaintain(p,true);}
        else if(win){
            Require(stage==2);var soul=p.Inventory.Objects.Single(i=>i.UID==Convert.ToUInt32(Sql("SELECT item FROM revora_fresh_purple_probe")));var home=(Map)Call(Feature("Housing"),"Load",p);HeartTalk(p,30156,2);Require(p.Entity.MapID==home.ID);ushort study=p.Entity.SubClasses.StudyPoints;
            ItemHandler.UseItem(soul,p);confirm();confirm();Require(!p.Inventory.ContainsUID(soul.UID) && bosses().Length==1);var boss=bosses().Single();Require(boss.Hitpoints==54000000);p.Attackable=true;p.CantAttack=Time32.Now.AddSeconds(-1);int hits=0;var timer=System.Diagnostics.Stopwatch.StartNew();
            while(!boss.Dead && hits<30000 && timer.Elapsed.TotalSeconds<240){
                if(hits%500==0)FreshPurpleMaintain(p);p.Entity.X=boss.X;p.Entity.Y=(ushort)(boss.Y+1);p.Screen.Add(boss);p.Entity.AttackStamp=Time32.Now.AddSeconds(-2);uint hp=boss.Hitpoints;Attack.HandleAttack(new Attack(true){Attacker=p.Entity.UID,Attacked=boss.UID,X=boss.X,Y=boss.Y,AttackType=Attack.Melee},p);hits++;if(hits==1)Require(boss.Hitpoints<hp);if(hits%1000==0)Console.WriteLine("[fresh-purple] hits="+hits+" hp="+boss.Hitpoints);
            }
            Require(boss.Dead && !p.Entity.Dead && !PirateFenced(p));Call(rewards,"Tick",true);Require(p.Entity.SubClasses.StudyPoints==study+200);
            Require((long)Sql("SELECT count(*) FROM revora_purple_banshee_runs WHERE uid=@uid AND item_uid=@item AND state='victory'","uid",(long)p.Entity.UID,"item",(long)soul.UID)==1 && (long)Sql("SELECT count(*) FROM revora_purple_banshee_loot l JOIN revora_purple_banshee_runs r ON r.id=l.run WHERE r.uid=@uid AND l.collector IS NULL","uid",(long)p.Entity.UID)==3);Console.WriteLine("[fresh-purple] calculated-melee-hits="+hits);
        }else if(loot){
            Require(stage==3);Call(Feature("Housing"),"Load",p);Call(rewards,"Tick",true);var drops=p.Map.FloorItems.Values.Where(d=>(bool)Call(rewards,"Managed",d)).ToArray();Require(drops.Length==3);int count=p.Inventory.Count;
            foreach(var drop in drops){p.Entity.X=drop.X;p.Entity.Y=drop.Y;var packet=new FloorItem(true){UID=drop.UID,Type=FloorItem.Remove}.ToArray();Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,packet.Length-8,packet);FloorItem.Process(packet,p);FloorItem.Process(packet,p);Require(p.Inventory.ContainsUID(drop.Item.UID));}
            Require(p.Inventory.Count==count+3 && drops.Count(d=>ConquerItemInformation.BaseInformations[d.ItemID].PurificationLevel==7)==1 && drops.Where(d=>ConquerItemInformation.BaseInformations[d.ItemID].PurificationLevel==7).All(d=>d.Item.Bound));
        }else{
            Require(phase.EndsWith("reload") && stage==4);string before=FreshPurpleCombatSnapshot(p);confirm();Call(rewards,"Tick",true);Require(bosses().Length==0 && p.Map.FloorItems.Values.Count(d=>(bool)Call(rewards,"Managed",d))==0 && before==FreshPurpleCombatSnapshot(p));
            var items=(long[])Sql("SELECT array_agg(l.item_uid ORDER BY l.slot) FROM revora_purple_banshee_loot l JOIN revora_purple_banshee_runs r ON r.id=l.run WHERE r.uid=@uid AND l.collector=@uid","uid",(long)p.Entity.UID);Require(items.Length==3 && items.All(id=>p.Inventory.ContainsUID((uint)id)));
            Require((long)Sql("SELECT count(*) FROM revora_purple_banshee_study WHERE uid=@uid AND points=200 AND paid_at IS NOT NULL","uid",(long)p.Entity.UID)==1);
        }
        Require(EntityTable.SaveEntity(p));if(maintain || win || loot){Sql("UPDATE revora_fresh_purple_probe SET stage=stage+1,snapshot=@snapshot","snapshot",FreshPurpleCombatSnapshot(p));Console.WriteLine("[career-process] "+phase+"-passed");Console.Out.Flush();Environment.Exit(0);return;}
        Require(FreshPurpleCombatSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_purple_probe")));Console.WriteLine("[career-process] "+phase+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Fresh Purple combat did not close.");
    }
}
