using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static readonly Type Lab=typeof(Entity).Assembly.GetType("Revora.Game.Features.LabyrinthQuest");
    static readonly Type LabItems=typeof(Entity).Assembly.GetType("Revora.Game.Features.LabyrinthItems");
    static int labRoll;
    static void LabNear(GameState p,uint npc) {
        var actor=p.Map.Npcs[npc];int w=p.Map.Floor.Bounds.Width,h=p.Map.Floor.Bounds.Height;
        int start=p.Entity.Y*w+p.Entity.X,end=-1;var parent=new Dictionary<int,int>{{start,start}};var queue=new SortedDictionary<int,Queue<int>>();
        Action<int,int> add=(key,priority)=>{Queue<int> list;if(!queue.TryGetValue(priority,out list)){list=new Queue<int>();queue.Add(priority,list);}list.Enqueue(key);};
        add(start,0);int expanded=0;
        while(queue.Count>0 && expanded++<w*h) {
            var first=queue.First();int at=first.Value.Dequeue();if(first.Value.Count==0)queue.Remove(first.Key);int x=at%w,y=at/w;
            if(Kernel.GetDistance((ushort)x,(ushort)y,actor.X,actor.Y)<=6){end=at;break;}
            foreach(int stride in new[]{20,10,1})for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++) {
                int nx=x+dx*stride,ny=y+dy*stride;if(nx<0 || ny<0 || nx>=w || ny>=h || dx==0 && dy==0 || Kernel.GetDistance((ushort)x,(ushort)y,(ushort)nx,(ushort)ny)>20)continue;
                int next=ny*w+nx;if(parent.ContainsKey(next) || !p.Map.Floor[nx,ny,MapObjectType.Player])continue;
                parent.Add(next,at);add(next,Math.Abs(nx-actor.X)+Math.Abs(ny-actor.Y));
            }
        }
        if(end<0)throw new Exception("No labyrinth jump route to NPC "+npc);
        var path=new Stack<int>();while(end!=start){path.Push(end);end=parent[end];}
        while(path.Count>0){int step=path.Pop();SkyJump(p,(ushort)(step%w),(ushort)(step/w));}p.ActiveNpc=npc;
    }
    static void LabNpc(GameState p,uint npc,byte option) {LabNear(p,npc);Npcs.GetDialog(new NpcRequest(true){NpcID=npc,OptionID=option},p);}
    static void LabSimon(GameState p,byte preview) {LabNpc(p,1152,preview);LabNpc(p,1152,230);}
    static Entity LabMob(GameState p,uint type) {
        var mob=(Entity)typeof(CompanionTests).GetMethod("Monster",Hidden).Invoke(null,new object[]{p.Map,p.Entity.X,p.Entity.Y,1u});mob.MonsterInfo.ID=type;
        mob.MonsterInfo.Level=MonsterInformation.MonsterInformations[type].Level;mob.MonsterInfo.Name=mob.Name=MonsterInformation.MonsterInformations[type].Name;mob.IsDropped=true;return mob;
    }
    static long LabCount(GameState p,string table) {return Scalar("SELECT count(*) FROM "+table+" WHERE uid=@uid",p.Entity.UID);}
    static void VerifyLabyrinth(string runtime,Map town) {
        Query("ALTER TABLE entities ADD COLUMN previousmapid integer DEFAULT 0,ADD COLUMN previousx integer DEFAULT 0,ADD COLUMN previousy integer DEFAULT 0,ADD COLUMN my_title integer DEFAULT 0,ADD COLUMN level integer DEFAULT 140,ADD COLUMN mapid integer DEFAULT 1002,ADD COLUMN x integer DEFAULT 0,ADD COLUMN y integer DEFAULT 0,ADD COLUMN virtuepoints bigint DEFAULT 0");
        Query("CREATE TABLE titles (LIKE public.titles INCLUDING ALL)");Query("CREATE TABLE itemadding (LIKE public.itemadding INCLUDING ALL)");
        Query("CREATE TABLE npcs (LIKE public.npcs INCLUDING ALL)");Query("INSERT INTO npcs SELECT * FROM public.npcs WHERE id BETWEEN 1152 AND 1156");
        StorageManager.Load();StorageItem.Load();PerfectionManager.AbilityScore.Read(out PerfectionManager.AbilitiesScore);ConquerItem.ItemUID=new Counter(7500000);Server.GamePool=new GameState[0];
        for(ushort map=1351;map<=1354;map++)World(runtime,map);
        Lab.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(n=>labRoll%n));Call(Lab,null,"Initialize");Call(Lab,null,"PlaceNpcs");
        Check(town.Npcs.ContainsKey(1152) && Enumerable.Range(0,4).All(i=>Kernel.Maps[(ushort)(1351+i)].Npcs.ContainsKey((uint)(1153+i))),"Simon and all four generals are restored on their reviewed maps");
        var p=MoonPlayer(town);p.Entity.X=211;p.Entity.Y=376;p.VirtuePoints=1999;
        LabSimon(p,1);Check(LabCount(p,"revora_lab_entries")==0 && p.Entity.MapID==1002,"ordinary entry requires the full 2,000 Virtue fee");
        p.VirtuePoints=2500;Query("UPDATE entities SET virtuepoints=2500 WHERE uid=@uid","uid",(long)p.Entity.UID);LoadVirtue(p);
        p.Fake=true;LabSimon(p,1);p.Fake=false;Check(LabCount(p,"revora_lab_entries")==0,"fake characters cannot buy an expedition");
        p.Entity.Teleport(1351,18,127);p.Entity.TeleportHouse(1352,27,227);p.Entity.Teleport(1354,1354,5,290);Check(p.Entity.MapID==1002,"all cross-map teleport overloads reject bypassing paid admission");
        Query("ALTER TABLE revora_lab_entries ADD CONSTRAINT lab_fee_failure CHECK(fee=1) NOT VALID");LabSimon(p,1);
        Check(Fenced(p) && p.VirtuePoints==2500 && Scalar("SELECT virtuepoints FROM entities WHERE uid=@uid",p.Entity.UID)==2500 && LabCount(p,"revora_lab_entries")==0,"failed entry receipt rolls back the entire Virtue charge");
        Query("ALTER TABLE revora_lab_entries DROP CONSTRAINT lab_fee_failure");DemonReset(p);Call(Lab,null,"OnLogin",p);
        LabNpc(p,1152,1);Parallel.For(0,12,i=>Npcs.GetDialog(new NpcRequest(true){NpcID=1152,OptionID=230},p));
        Check(p.Entity.MapID==1351 && p.VirtuePoints==500 && Scalar("SELECT virtuepoints FROM entities WHERE uid=@uid",p.Entity.UID)==500 && LabCount(p,"revora_lab_entries")==1,"concurrent entry confirmations pay once and reach the first floor");
        p.Entity.Teleport(1354,5,290);Check(p.Entity.MapID==1351,"an admitted player cannot skip floors through external teleportation");
        labRoll=99;LabMob(p,3141).Die(p.Entity);Check(LabCount(p,"revora_lab_loot")==0,"a failed material roll produces no satchel entry");labRoll=0;
        LabMob(p,3144).Die(p.Entity);var foreign=LabMob(p,3141);p.Map.Entities.Remove(foreign.UID);foreign.Die(p.Entity);
        Check(LabCount(p,"revora_lab_loot")==0,"wrong-floor and unowned monsters create no quest loot");
        var corpse=LabMob(p,3141);Parallel.For(0,12,i=>corpse.Die(p.Entity));Check(LabCount(p,"revora_lab_loot")==1,"one actual concurrent monster death creates one owned satchel item");
        Query("ALTER TABLE revora_lab_loot ADD CONSTRAINT lab_loot_failure CHECK(type=0) NOT VALID");LabMob(p,3141).Die(p.Entity);
        Check(Fenced(p) && LabCount(p,"revora_lab_loot")==1,"loot persistence failure fences the session without a phantom item");
        Query("ALTER TABLE revora_lab_loot DROP CONSTRAINT lab_loot_failure");DemonReset(p);Call(Lab,null,"OnLogin",p);
        Check(p.Entity.MapID==1351 && p.VirtuePoints==500 && LabCount(p,"revora_lab_entries")==1,"reconnect restores the paid floor without another entry charge");
        var fillers=Enumerable.Range(0,40).Select(i=>Pack(p,1088001,1)).ToArray();LabNpc(p,1153,3);
        Check(p.Inventory.Count==40 && Scalar("SELECT count(*) FROM revora_lab_loot WHERE uid=@uid AND NOT claimed",p.Entity.UID)==1,"full bags preserve rolled loot in the saved satchel");
        foreach(var item in fillers){Query("DELETE FROM items WHERE uid=@id","id",(long)item.UID);p.Inventory.Remove(item.UID,Enums.ItemUse.None,false);}
        uint[] diamondMobs={3141,3144,3147,3155},tokenMobs={3142,3145,3148};
        for(int floor=0;floor<4;floor++) {
            uint npc=(uint)(1153+floor);int kills=floor==0?16:floor==3?51:17;
            if(floor==2) {
                Check(!HasAchievement(p,10525),"floors below level 120 give no unarmed achievement");
                var retry=LabMob(p,3147);Check(retry.MonsterInfo.Level==140,"AzureBird uses its loaded qualifying level");
                long before=LabCount(p,"revora_lab_loot");int rolls=0;
                Lab.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(n=>{rolls++;return labRoll%n;}));
                Query("ALTER TABLE revora_lab_loot ADD CONSTRAINT lab_high_failure CHECK(type=0) NOT VALID");retry.Die(p.Entity);
                Check(!retry.Dead && !Fenced(p) && LabCount(p,"revora_lab_loot")==before && !HasAchievement(p,10525),"failed high-level loot write preserves living monster and no phantom achievement");
                Query("ALTER TABLE revora_lab_loot DROP CONSTRAINT lab_high_failure");
                Query("CREATE FUNCTION reject_lab_unarmed() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER reject_lab_unarmed BEFORE INSERT ON revora_achievement_evidence FOR EACH ROW WHEN(NEW.achievement_id=10525) EXECUTE FUNCTION reject_lab_unarmed()");
                labRoll=99;retry.Die(p.Entity);
                Check(!retry.Dead && LabCount(p,"revora_lab_loot")==before && rolls==1,"achievement failure rolls back loot and reuses the original winning roll");
                Query("DROP TRIGGER reject_lab_unarmed ON revora_achievement_evidence;DROP FUNCTION reject_lab_unarmed()");
                var victory=typeof(Entity).Assembly.GetType("Revora.Game.Features.WildMonsterVictory");
                victory.GetField("AfterCommit",Hidden).SetValue(null,(Action)(()=>{throw new System.IO.IOException("fixture acknowledgement lost");}));
                try{retry.Die(p.Entity);}finally{victory.GetField("AfterCommit",Hidden).SetValue(null,null);}
                Check(!retry.Dead && LabCount(p,"revora_lab_loot")==before+1 && rolls==1,"lost acknowledgement retains exactly one saved loot row and original roll");
                Parallel.For(0,12,i=>retry.Die(p.Entity));
                Check(retry.Dead && HasAchievement(p,10525) && LabCount(p,"revora_lab_loot")==before+1 && rolls==1,"concurrent retry completes one life with immediate unarmed credit and no extra loot roll");
                var miss=LabMob(p,3147);miss.Die(p.Entity);
                Check(miss.Dead && LabCount(p,"revora_lab_loot")==before+1 && rolls==2,"a new life uses a new roll and can drop nothing");
                var excluded=LabMob(p,3149);excluded.Die(p.Entity);
                Check(excluded.Dead && LabCount(p,"revora_lab_loot")==before+1 && rolls==2,"qualifying Talon still has no diamond or token roll");
                retry.Hitpoints=1;retry.Die(p.Entity);
                Check(retry.Dead && LabCount(p,"revora_lab_loot")==before+1 && rolls==3,"respawn resets the former winning roll and gives the new life a fresh losing roll");
                labRoll=0;Lab.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(n=>labRoll%n));kills--;
            }
            for(int i=0;i<kills;i++)LabMob(p,diamondMobs[floor]).Die(p.Entity);
            if(floor<3)LabMob(p,tokenMobs[floor]).Die(p.Entity);
            LabNpc(p,npc,3);if(floor<3)LabNpc(p,npc,3);
            Check(p.Inventory.Objects.Any(i=>i.ID==721533+floor && i.StackSize==(floor==3?51:17) && i.Bound),"floor "+(floor+1)+" yields its earned bound diamonds through owned deaths and satchel delivery");
            if(floor<3) {
                if(floor==0) {
                    Query("ALTER TABLE revora_lab_steps ADD CONSTRAINT lab_step_failure CHECK(floor=-1) NOT VALID");LabNpc(p,npc,1);
                    Check(Fenced(p) && p.Entity.MapID==1351 && p.Inventory.Objects.Any(i=>i.ID==721537),"failed floor transition preserves its token and floor");
                    Query("ALTER TABLE revora_lab_steps DROP CONSTRAINT lab_step_failure");DemonReset(p);Call(Lab,null,"OnLogin",p);
                }
                LabNpc(p,npc,1);Check(p.Entity.MapID==1352+floor && !p.Inventory.Objects.Any(i=>i.ID==721537+floor),"general consumes the matching token once and enters floor "+(floor+2));
            }
        }
        Check(skyJumps>60,"ordinary production jumps reach every installed general across all four floors");
        LabNpc(p,1156,2);Check(p.Entity.MapID==1002 && Scalar("SELECT count(*) FROM revora_lab_visits WHERE uid=@uid AND active",p.Entity.UID)==0,"North General closes the expedition and returns its player to Simon");
        LabSimon(p,10);LabSimon(p,11);LabSimon(p,12);
        Check(!HasAchievement(p,11027) && p.Inventory.Objects.Count(i=>i.ID==1088001)==6 && p.Inventory.Objects.Count(i=>i.ID==700001)==1,"Sun, Moon and Star exchanges grant their classic rewards without the Ancestor achievement");
        var tight=new List<ConquerItem>();while(p.Inventory.Count<39)tight.Add(Pack(p,1088001,1));LabSimon(p,13);
        Check(!Fenced(p) && !HasAchievement(p,11027) && !p.Inventory.Contains(721540,1)
            && p.Inventory.Objects.Any(i=>i.ID==721536 && i.StackSize==51),"one free slot cannot fit Ancestor Box and guaranteed soul while diamonds remain stacked");
        foreach(var item in tight)DisRemove(p,item);
        Query("ALTER TABLE revora_lab_exchanges ADD CONSTRAINT lab_exchange_failure CHECK(kind<>3) NOT VALID");LabSimon(p,13);
        Check(Fenced(p) && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND id BETWEEN 800000 AND 899999",p.Entity.UID)==0 && !HasAchievement(p,11027) && p.Inventory.Objects.Any(i=>i.ID==721536 && i.StackSize==51),"failed Ancestor exchange preserves its earned diamonds and rolls back the achievement");
        Query("ALTER TABLE revora_lab_exchanges DROP CONSTRAINT lab_exchange_failure");DemonReset(p);Call(Lab,null,"OnLogin",p);LabSimon(p,13);
        var box=p.Inventory.Objects.Single(i=>i.ID==721540);
        Check(HasAchievement(p,11027) && box.Bound && p.Inventory.Objects.Any(i=>i.ID==721536 && i.StackSize==34),"Simon atomically exchanges seventeen Cloud Diamonds for the real bound Ancestor Box and achievement 11027");
        VerifyIslandSoul(p,"revora_lab_exchanges");
        LabNpc(p,1152,230);Check(p.Inventory.Objects.Count(i=>i.ID==721540)==1,"replayed exchange confirmation cannot create another box");
        int draws=0;Lab.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(n=>{draws++;return labRoll%n;}));
        var packed=Enumerable.Range(0,40-p.Inventory.Count).Select(i=>Pack(p,1088001,1)).ToArray();ItemHandler.UseItem(box,p);
        Check(draws==0 && p.Inventory.ContainsUID(box.UID),"a full bag cannot consume a box or sample a prize roll");
        foreach(var item in packed){Query("DELETE FROM items WHERE uid=@id","id",(long)item.UID);p.Inventory.Remove(item.UID,Enums.ItemUse.None,false);}
        Query("UPDATE items SET entityid=1 WHERE uid=@box","box",(long)box.UID);ItemHandler.UseItem(box,p);
        Check(draws==0 && p.Inventory.ContainsUID(box.UID),"stale cache cannot open a box owned by another persisted character");
        Query("UPDATE items SET entityid=@uid WHERE uid=@box","uid",(long)p.Entity.UID,"box",(long)box.UID);
        Query("ALTER TABLE revora_lab_boxes ADD CONSTRAINT lab_box_failure CHECK(roll=-1) NOT VALID");ItemHandler.UseItem(box,p);
        Check(Fenced(p) && p.Inventory.ContainsUID(box.UID) && LabCount(p,"revora_lab_boxes")==0,"failed box opening preserves its box and rolls back all prize items");
        Query("ALTER TABLE revora_lab_boxes DROP CONSTRAINT lab_box_failure");DemonReset(p);Parallel.For(0,12,i=>ItemHandler.UseItem(box,p));
        Check(!p.Inventory.ContainsUID(box.UID) && LabCount(p,"revora_lab_boxes")==1 && p.Inventory.Objects.Count(i=>i.ID==1088001)==8,"concurrent box opening delivers its saved two-Meteor result exactly once");
        LabSimon(p,13);labRoll=60;ItemHandler.UseItem(p.Inventory.Objects.Single(i=>i.ID==721540),p);
        Check(p.Inventory.Objects.Any(i=>i.ID==700041 && i.Bound) && LabCount(p,"revora_lab_boxes")==2,"the gem prize boundary yields its recorded normal gem");
        LabSimon(p,13);labRoll=90;ItemHandler.UseItem(p.Inventory.Objects.Single(i=>i.ID==721540),p);
        Check(p.Inventory.Objects.Any(i=>i.ID==1088000 && i.Bound) && LabCount(p,"revora_lab_boxes")==3,"the rare prize boundary yields one recorded bound DragonBall");
        foreach(var item in p.Inventory.Objects)p.Inventory.Remove(item.UID,Enums.ItemUse.None,false);ConquerItemTable.LoadItems(p);EvidenceRefresh(p);
        Check(HasAchievement(p,11027) && HasAchievement(p,10525) && p.Inventory.Objects.Count(i=>i.ID==1088001)==8,"quest reward inventory and achievement survive a persisted reload");
        Check(p.Inventory.Objects.Count(i=>i.ID>=800000 && i.ID<900000 && i.Bound && ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7)==3
            && Scalar("SELECT count(*) FROM revora_lab_exchanges WHERE uid=@uid AND kind=3 AND reward_version=1 AND cardinality(rewards)=2",p.Entity.UID)==3,
            "three fresh diamond exchanges save three bound P7 souls through box opening and item reload");
        p.VirtuePoints=2500;LabSimon(p,1);p.Entity.Die(LabMob(p,3141));Call(Lab,null,"Tick");
        Check(p.Entity.MapID==1002 && !p.Entity.Dead && Scalar("SELECT count(*) FROM revora_lab_visits WHERE uid=@uid AND active",p.Entity.UID)==0,"real dungeon death ends the paid visit and revives its player at Simon");
        p.VirtuePoints=2500;var firstFloor=Kernel.Maps[1351];var savedFloor=firstFloor.Floor;
        firstFloor.Floor=new Floor(1,1,1351);
        try{LabSimon(p,1);}finally{firstFloor.Floor=savedFloor;}
        Check(Fenced(p) && p.Entity.MapID==1002 && p.VirtuePoints==500 && LabCount(p,"revora_lab_entries")==3,"a transfer failure after committed payment retains its paid expedition");
        DemonReset(p);Call(Lab,null,"OnLogin",p);LabSimon(p,1);
        Check(p.Entity.MapID==1351 && p.VirtuePoints==500 && LabCount(p,"revora_lab_entries")==3,"the paid transfer resumes after reload without charging the fee again");
        object visits=Lab.GetField("active",Hidden).GetValue(null);object old=visits.GetType().GetProperty("Item").GetValue(visits,new object[]{p.Entity.UID});
        LabNpc(p,1153,2);p.VirtuePoints=2500;LabSimon(p,1);labRoll=0;long loot=LabCount(p,"revora_lab_loot");
        Call(Lab,null,"Credit",p.Entity,old,(ushort)1351,p.Entity.X,p.Entity.Y,3141u);
        Check(LabCount(p,"revora_lab_loot")==loot,"a delayed kill from an older paid visit cannot add loot to its replacement");
        var sample=LabMob(p,3147);sample.MapID=1353;
        Check((bool)Call(Lab,null,"SuppressLegacyQuestDrop",sample),"labyrinth families use one quest drop producer on their matching floor");sample.MapID=1015;
        Check(!(bool)Call(Lab,null,"SuppressLegacyQuestDrop",sample),"the city AzureBird summon retains its separate ordinary drop behavior");
        var helper=MoonPlayer(town);LoadVirtue(helper);helper.Entity.X=211;helper.Entity.Y=376;helper.VirtuePoints=2500;
        LabNpc(helper,1152,1);
        Parallel.For(0,17,i=>{if(i==0)Npcs.GetDialog(new NpcRequest(true){NpcID=1152,OptionID=230},helper);else Call(typeof(GameState),helper,"AddVirtuePoints",100u);});
        Check(helper.VirtuePoints==2100 && LabCount(helper,"revora_lab_entries")==1 && helper.Entity.MapID==1351,"concurrent training credits and one entry fee preserve every Virtue increment without another player's inventory lock");
        var stale=MoonPlayer(town);LoadVirtue(stale);stale.Entity.X=211;stale.Entity.Y=376;stale.VirtuePoints=2500;
        Query("UPDATE entities SET virtuepoints=9000 WHERE uid=@uid","uid",(long)stale.Entity.UID);LabSimon(stale,1);
        Check(Fenced(stale) && stale.VirtuePoints==2500 && Scalar("SELECT virtuepoints FROM entities WHERE uid=@uid",stale.Entity.UID)==9000
            && LabCount(stale,"revora_lab_entries")==0,"Labyrinth admission rejects stale durable Virtue without overriding external points");

    }
}
