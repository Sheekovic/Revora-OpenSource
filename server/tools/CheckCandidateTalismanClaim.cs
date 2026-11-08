using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
internal static partial class CheckCandidateArenaLifecycle
{
    static string TalismanClaimSnapshot(GameState p){return Convert.ToString(Sql(@"SELECT json_build_object(
'career',@career::json,
'way',(SELECT row_to_json(w) FROM wayofheroes w WHERE uid=@uid),
'claims',(SELECT json_agg(r ORDER BY stage_uid) FROM revora_way_of_heroes_claims r WHERE uid=@uid),
'bags',(SELECT json_agg(r ORDER BY id) FROM revora_silver_study_bag_uses r WHERE uid=@uid))::text","career",RetainedRevengeSnapshot(p),"uid",(long)CareerUid));}
    static void TalismanClaimPacket(GameState p){var frame=new byte[17];Revora.Network.Writer.WriteUInt16(9,0,frame);Revora.Network.Writer.WriteUInt16(2830,2,frame);frame[4]=1;Revora.Network.Writer.WriteUInt32(408,5,frame);Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,9,frame);Revora.Network.PacketHandler.HandlePacket(frame,p);}
    static void RetainedTalismanClaim(bool reload){
        DateTime now=new DateTime(2035,1,20,6,0,0,DateTimeKind.Utc);GameAssembly.GetType("Revora.TitleRequirements",true).GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));var purse=Feature("GuildWarPurses");lock(purse.GetField("pollGate",Hidden).GetValue(null))purse.GetField("nextPoll",Hidden).SetValue(null,DateTime.MaxValue);
        careerRitesFixture=true;combinedCollectionFixture=true;nextUid=CareerUid;var p=Player(Kernel.Maps[1002],true,1003,true,true);combinedCollectionFixture=false;SkillTable.LoadSpells(p);SkillTable.LoadProficiencies(p);SubClassTable.Load(p.Entity);new CoatStorage().Login(p);new TitleStorage().Login(p);p.LoadItemStats();ChiTable.Load(p);EntityVariableTable.Load(p);nextUid=4294908901;var peer=Player(Kernel.Maps[1002],true);SkillTable.LoadSpells(peer);SkillTable.LoadProficiencies(peer);SubClassTable.Load(peer.Entity);peer.LoadItemStats();EntityVariableTable.Load(peer);Server.GamePool=new[]{p,peer};foreach(var who in new[]{p,peer}){Revora.Database.KnownPersons.LoadPartner(who);Revora.Database.KnownPersons.LoaderFriends(who);Revora.Database.KnownPersons.LoadMentor(who);}
        p.Quests=new Quests(p);p.Quests.Load();
        Revora.Database.KnownPersons.LoadEnemy(p);

        if((bool)Sql("SELECT to_regclass('revora_retained_talisman_probe') IS NULL")){
            Require(!reload && RetainedRevengeSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_retained_travel_confirm_probe")));
            Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=408","uid",(long)CareerUid)==0);
            Sql("CREATE TABLE revora_retained_talisman_probe(step integer NOT NULL,snapshot text NOT NULL,study bigint NOT NULL,equipped bigint[] NOT NULL);INSERT INTO revora_retained_talisman_probe VALUES(0,@snapshot,@study,@gear)","snapshot",TalismanClaimSnapshot(p),"study",(long)p.Entity.SubClasses.StudyPoints,"gear",p.Equipment.GetItems().Where(i=>i.Position==10 || i.Position==11).OrderBy(i=>i.Position).Select(i=>(long)i.UID).ToArray());
        }
        string checkpoint=Convert.ToString(Sql("SELECT snapshot FROM revora_retained_talisman_probe"));
        string loaded=TalismanClaimSnapshot(p);
        if(checkpoint!=loaded){Require(!reload && (int)Sql("SELECT step FROM revora_retained_talisman_probe")==0);
            Require((bool)Sql(@"WITH RECURSIVE nodes(side,path,v,depth) AS(
SELECT 0,ARRAY[]::text[],@before::jsonb,0
UNION ALL SELECT 1,ARRAY[]::text[],@now::jsonb,0
UNION ALL SELECT n.side,n.path||e.key,
CASE WHEN jsonb_typeof(e.value)='string' AND left(e.value#>>'{}',1)='{' THEN (e.value#>>'{}')::jsonb ELSE e.value END,n.depth+1
FROM nodes n CROSS JOIN LATERAL(
SELECT key,value FROM jsonb_each(CASE WHEN jsonb_typeof(n.v)='object' THEN n.v ELSE '{}'::jsonb END)
UNION ALL SELECT ord::text,value FROM jsonb_array_elements(CASE WHEN jsonb_typeof(n.v)='array' THEN n.v ELSE '[]'::jsonb END) WITH ORDINALITY a(value,ord))e
WHERE n.depth<256),flat AS(
SELECT side,jsonb_object_agg(array_to_string(path,'/'),v) AS v FROM nodes
WHERE jsonb_typeof(v) NOT IN('object','array')
AND lower(path[array_length(path,1)]) NOT IN('x','y','mapid','previousmapid','previousx','previousy') GROUP BY side)
SELECT (SELECT max(depth) FROM nodes)<256 AND (SELECT v FROM flat WHERE side=0)=(SELECT v FROM flat WHERE side=1)","before",checkpoint,"now",loaded));
            Sql("UPDATE revora_retained_talisman_probe SET snapshot=@snapshot","snapshot",loaded);
        }
        Require(TalismanClaimSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_retained_talisman_probe")));
        int step=(int)Sql("SELECT step FROM revora_retained_talisman_probe");Action<int> save=n=>{Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_retained_talisman_probe SET step=@step,snapshot=@snapshot","step",n,"snapshot",TalismanClaimSnapshot(p));};
        if((bool)Sql("SELECT to_regclass('revora_retained_talisman_hold') IS NULL")){Require(!reload);Sql("CREATE TABLE revora_retained_talisman_hold(uid bigint PRIMARY KEY,banked boolean NOT NULL)");}
        Action<GameState,GameState,uint> trade=(from,to,id)=>{
            foreach(var who in new[]{from,to}){who.Entity.Teleport(1002,400,350);HeartTalk(who,8,0);}
            CareerTradePacket(from,Trade.Request,to.Entity.UID);CareerTradePacket(to,Trade.Request,from.Entity.UID);CareerTradePacket(from,Trade.AddItem,id);Require(from.Trade.Items.Count==1);
            CareerTradePacket(from,Trade.Accept,0);CareerTradePacket(to,Trade.Accept,0);Require(!from.Inventory.ContainsUID(id) && to.Inventory.ContainsUID(id));Require(EntityTable.SaveEntity(peer));save(step);
        };
        if(!reload){
            if(step==0){
                while(p.Inventory.Count>35){var item=p.Inventory.Objects.Where(i=>(bool)Call(Feature("AtomicTrade"),"Tradable",p,peer,i)).OrderBy(i=>i.UID).First();Sql("INSERT INTO revora_retained_talisman_hold VALUES(@uid,false)","uid",(long)item.UID);trade(p,peer,item.UID);}
                var bank=p.Warehouses.First(w=>(uint)w.Key==4101).Value;
                while(bank.Objects.Count()>58){p.Entity.Teleport(1213,425,269);var item=bank.Objects.Where(i=>!i.Bound && i.Lock==0 && i.StackSize==1 && !i.Suspicious && ConquerItemInformation.BaseInformations[i.ID].Type==ConquerItemBaseInformation.ItemType.Dropable).OrderBy(i=>i.UID).First();Sql("INSERT INTO revora_retained_talisman_hold VALUES(@uid,true)","uid",(long)item.UID);GrailWarehouse(p,item.UID,2,4101);Require(p.Inventory.ContainsUID(item.UID));trade(p,peer,item.UID);}
                p.Entity.Teleport(1213,425,269);
                foreach(byte slot in new byte[]{10,11}){var gear=p.Equipment.TryGetItem(slot);if(gear==null)continue;uint id=gear.UID;RetainedUpgradeUsage(p,id,slot,ItemUsage.Mode.UnequipItem);Require(p.Inventory.ContainsUID(id));GrailWarehouse(p,id,1,4101);Require(!p.Inventory.ContainsUID(id));save(0);}
                p.Entity.Teleport(1002,400,350);
                Require(p.Inventory.Objects.Count(i=>i.ID==201009)==1 && p.Inventory.Objects.Count(i=>i.ID==202008)==1);
                Require(p.Inventory.Objects.Where(i=>i.ID==201009 || i.ID==202008).All(i=>i.Plus==0 && i.Bound));
                Require((long)Sql("SELECT count(*) FROM revora_defense_talisman q JOIN items i ON i.uid=ANY(q.rewards) AND i.entityid=q.uid WHERE q.uid=@uid AND i.id=202008 AND i.plus=0","uid",(long)CareerUid)==1);
                Require((long)Sql("SELECT count(*) FROM revora_attack_talisman q JOIN items i ON i.uid=ANY(q.rewards) AND i.entityid=q.uid WHERE q.uid=@uid AND i.id=201009 AND i.plus=0","uid",(long)CareerUid)==1);
                WayofHeroes.Load(p);
                TalismanClaimPacket(p);Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=408","uid",(long)CareerUid)==0);Require(!PirateFenced(p));save(1);step=1;
            }
            if(step==1){
                var tower=p.Inventory.Objects.Single(i=>i.ID==202008);uint id=tower.UID;CareerBuy(p,1088000);var material=p.Inventory.Objects.First(i=>i.ID==1088000 && !i.Bound);ushort before=material.StackSize;
                var roll=Feature("EquipmentUpgrades").GetField("Roll",Hidden);var original=roll.GetValue(null);
                try{roll.SetValue(null,(Func<int,int>)(n=>0));RetainedUpgradeUsage(p,id,material.UID,ItemUsage.Mode.DragonBallUpgrade);}finally{roll.SetValue(null,original);}
                Require(tower.ID==202009 && tower.Plus==0 && tower.Bound && (before==1?!p.Inventory.ContainsUID(material.UID):material.StackSize==before-1));save(2);step=2;
            }
            if(step==2){
                var before=p.Inventory.Objects.Select(i=>i.UID).ToArray();System.Threading.Tasks.Parallel.For(0,8,_=>TalismanClaimPacket(p));
                Require(!PirateFenced(p) && (long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=408 AND NOT legacy","uid",(long)CareerUid)==1);
                var reward=p.Inventory.Objects.Single(i=>!before.Contains(i.UID));Require(reward.ID==3005123 && reward.Bound && reward.Plus==0 && reward.StackSize==1);
                var use=new ItemUsage(true){UID=reward.UID,Action=ItemUsage.Mode.EquipItem}.ToArray();Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,use.Length-8,use);ItemUsage.Process(use,p);ItemUsage.Process(use,p);
                Require(!PirateFenced(p) && !p.Inventory.ContainsUID(reward.UID) && p.Entity.SubClasses.StudyPoints==Convert.ToInt64(Sql("SELECT study FROM revora_retained_talisman_probe"))+200);save(3);step=3;
            }
            if(step==3){
                var gear=(long[])Sql("SELECT equipped FROM revora_retained_talisman_probe");p.Entity.Teleport(1213,425,269);
                for(int n=0;n<gear.Length;n++){uint id=(uint)gear[n];GrailWarehouse(p,id,2,4101);Require(p.Inventory.ContainsUID(id));RetainedUpgradeUsage(p,id,(uint)(10+n),ItemUsage.Mode.EquipItem);Require(p.Equipment.TryGetItem((byte)(10+n)).UID==id);}
                var held=new System.Collections.Generic.List<Tuple<uint,bool>>();using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new Npgsql.NpgsqlCommand("SELECT uid,banked FROM revora_retained_talisman_hold ORDER BY banked DESC,uid",c))using(var r=q.ExecuteReader())while(r.Read())held.Add(Tuple.Create((uint)r.GetInt64(0),r.GetBoolean(1)));}
                foreach(var heldItem in held){uint id=heldItem.Item1;if(peer.Inventory.ContainsUID(id))trade(peer,p,id);if(heldItem.Item2 && p.Inventory.ContainsUID(id)){p.Entity.Teleport(1213,425,269);GrailWarehouse(p,id,1,4101);Require(!p.Inventory.ContainsUID(id));save(3);}}
                p.Entity.Teleport(1002,400,350);save(4);step=4;
            }
        }
        Require(step==4 && p.Inventory.Objects.Any(i=>i.ID==201009 && i.Plus==0) && p.Inventory.Objects.Any(i=>i.ID==202009 && i.Plus==0));
        Require(p.Entity.SubClasses.StudyPoints==Convert.ToInt64(Sql("SELECT study FROM revora_retained_talisman_probe"))+200);
        string exact=TalismanClaimSnapshot(p);TalismanClaimPacket(p);Require(!PirateFenced(p) && exact==TalismanClaimSnapshot(p) && exact==Convert.ToString(Sql("SELECT snapshot FROM revora_retained_talisman_probe")));
        System.Console.WriteLine("[career-process] career-talisman-claim-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Talisman claim did not close.");
    }
}
