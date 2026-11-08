using System;
using System.Linq;
using System.Threading.Tasks;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Game.Features;
using Revora.Network;
using Revora.Network.GamePackets;
using Status = Revora.Network.GamePackets.MsgTaskStatus.QuestData.QuestStatus;

internal static partial class PotencyHuntTests
{
    static readonly Type WarriorEpicStorage=typeof(Entity).Assembly.GetType("Revora.Game.Features.WarriorEpicStore");
    static object WarriorEpicStage(int stage){return Enum.ToObject(WarriorEpicStorage.GetNestedType("Stage",System.Reflection.BindingFlags.NonPublic),stage);}
    static object WarriorEpicCall(GameState p,string method,params object[] tail){
        lock(p.ItemSyncRoot)return Call(WarriorEpicStorage,null,method,new object[]{p}.Concat(tail).ToArray());
    }
    static int WarriorEpicProgress(GameState p){return Convert.ToInt32(Field(WarriorEpicCall(p,"Load"),"Stage"));}
    static readonly Type WarriorJournal=typeof(Entity).Assembly.GetType("Revora.Game.Features.WarriorEpicJournal");
    static readonly uint[] WarriorMissions={3678,3679,3695,3680,3681,3682,3683};
    static readonly Type WarriorScenes=typeof(Entity).Assembly.GetType("Revora.Game.Features.WarriorEpicScenes");
    static readonly Type WarriorCombat=typeof(Entity).Assembly.GetType("Revora.Game.Features.WarriorEpicCombat");
    static readonly Type WarriorQuest=typeof(Entity).Assembly.GetType("Revora.Game.Features.WarriorEpicQuest");
    static void VerifyWarriorEpicJourney(Map town){
        Call(WarriorQuest,null,"Initialize");var p=WarriorEpicPlayer(town);p.Quests=new Quests(p);p.Quests.Load();
        EpicNpc(p,10001,86);Check(WarriorEpicProgress(p)==0 && !p.Inventory.Objects.Any(x=>x.ID==3200206),"brief ancient-book introduction does not issue an unaccepted reward");
        EpicNpc(p,10001,87);var jade=p.Inventory.Objects.Single(x=>x.ID==3200206);
        Check(p.Account.State==AccountTable.AccountState.Player && WarriorEpicProgress(p)==1 && jade.Bound,"ordinary NPC acceptance issues the unique memory jade");
        SoulWire(p,87,10001);Check(p.Inventory.Objects.Count(x=>x.ID==3200206)==1,"replayed NPC acceptance cannot issue another jade");
        SoulWire(p,9,10001);Check(p.Spells.ContainsKey(10470) && WarriorEpicProgress(p)==1,"active epic route preserves Warrior God's normal skill lessons");
        ItemHandler.UseItem(new ConquerItem(true){UID=jade.UID,ID=jade.ID},p);
        Check(p.Entity.MapID==1004 && WarriorEpicProgress(p)==1,"forged jade object cannot enter its owner's memory");
        Drink(p,jade);
        Check(!Fenced(p) && p.Map.BaseID==10009 && WarriorEpicProgress(p)==2 && !p.Inventory.ContainsUID(jade.UID),"native item-use handler enters memory and consumes only the issued jade");
        var map=p.Map;EpicNpc(p,19253,1);
        Check(!Fenced(p) && p.Map.BaseID==10010 && WarriorEpicProgress(p)==3 && !Revora.Kernel.Maps.ContainsKey(map.ID),"memory chest dialogue leads to the owned formation and releases the prior scene");
        var body=p.Map.Entities.Values.Single(x=>x.MonsterInfo!=null && x.MonsterInfo.Name=="SealingStele");
        EpicNpc(p,19252,254);Check(p.Entity.MapID==1004 && WarriorEpicProgress(p)==3,"formation guardian provides a free exit before combat");
        EpicNpc(p,10001,88);body=p.Map.Entities.Values.Single(x=>x.MonsterInfo!=null && x.MonsterInfo.Name=="SealingStele");
        Check(p.Map.BaseID==10010 && body.Hitpoints==30000,"Warrior God resumes an abandoned formation with a fresh owned actor");
        SpookKill(p,body);
        Check(!Fenced(p) && WarriorEpicProgress(p)==4 && p.Map.BaseID==10011,"breaking the stele opens and enters the final cave through the combat callback");
        EpicNpc(p,19254,1);body=p.Map.Entities.Values.Single(x=>x.MonsterInfo!=null && x.MonsterInfo.Name=="TinyMindofEvil");
        Check(WarriorEpicProgress(p)==5 && body.Hitpoints==50000,"opening the final chest releases its owned evil encounter");
        SpookKill(p,body);Check(WarriorEpicProgress(p)==6 && WarriorEpicItems(p)==0,"native route victory waits for the Spirit's claim");
        while(p.Inventory.Count<39)Pack(p,1088001,1);
        EpicNpc(p,19259,1);Check(!Fenced(p) && WarriorEpicProgress(p)==6 && WarriorEpicItems(p)==0,"Spirit preserves the earned claim when only one bag slot is free");
        DisRemove(p,p.Inventory.Objects.First());EpicNpc(p,19259,1);
        Check(!Fenced(p) && WarriorEpicProgress(p)==7 && p.Inventory.Objects.Count(x=>x.ID==624439 && !x.Bound && x.Plus==12 && x.Bless==7 && x.SocketOne==Enums.Gem.EmptySocket && x.SocketTwo==Enums.Gem.EmptySocket)==2,"ordinary NPC/item/combat journey earns exactly two unbound maximum enhanced fists");
        SoulWire(p,1,19259);Check(WarriorEpicItems(p)==2,"replayed Spirit claim cannot duplicate the pair");
        VerifyWarriorMaxReward(p);
        var earned=p.Inventory.Objects.Where(x=>x.ID==624439).ToArray();
        p.Entity.Atributes+=210;p.Entity.Level=140;EntityTable.SaveEntity(p); // Level-140 equipment fixture, including 70 levels of allocatable stat points.
        var required=ConquerItemInformation.BaseInformations[624439].Agility;
        if(p.Entity.Agility<required){var allocated=required-p.Entity.Agility;if(p.Entity.Atributes<allocated)throw new Exception("Fixture lacks legitimate stat points.");p.Entity.Atributes-=(ushort)allocated;p.Entity.Agility=required;EntityTable.SaveEntity(p);}
        foreach(var pair in earned.Select((item,index)=>new {item,index}))ItemHandler.EquipItem(new ItemUsage(true){UID=pair.item.UID,dwParam=(uint)(4+pair.index),Action=ItemUsage.Mode.EquipItem},p);
        Check(p.Equipment.TryGetItem(4)!=null && p.Equipment.TryGetItem(5)!=null && p.Equipment.TryGetItem(4).ID==624439 && p.Equipment.TryGetItem(5).ID==624439,"ordinary Warrior equips both earned fists through the normal equipment handler");
        var invitation=Pack(p,3006347,1);Drink(p,invitation);
        Check(p.Inventory.ContainsUID(invitation.UID) && WarriorEpicItems(p)==2,"Pandora's Call stays intact and cannot grant an unrelated maximum-level Warrior fist");
        EpicNpc(p,19259,254);Check(p.Entity.MapID==1004,"Spirit offers a working free exit to Warrior God");
        p=ReloadBall(p);VerifyWarriorMaxReward(p);EpicNpc(p,10001,88);Check(WarriorEpicProgress(p)==7 && p.Entity.MapID==1004 && WarriorEpicItems(p)==2,"cold-reloaded completion cannot restart or duplicate the route");
        Check(p.Equipment.TryGetItem(4)!=null && p.Equipment.TryGetItem(5)!=null && p.Equipment.TryGetItem(4).ID==624439 && p.Equipment.TryGetItem(5).ID==624439,"both equipped earned fists persist through cold reload");
        var resumed=WarriorEpicPlayer(town);EpicNpc(resumed,10001,87);Drink(resumed,resumed.Inventory.Objects.Single(x=>x.ID==3200206));
        resumed=ReloadBall(resumed);Call(WarriorQuest,null,"Tick");resumed.Entity.Teleport(1004,44,74);EntityTable.SaveEntity(resumed);
        EpicNpc(resumed,10001,88);
        Check(!Fenced(resumed) && resumed.Map.BaseID==10009 && WarriorEpicProgress(resumed)==2,"Warrior God resumes an interrupted memory without issuing or charging another jade");
        EpicNpc(resumed,19253,254);
    }
    static Entity WarriorSpawn(object scene,GameState p){lock(p.ItemSyncRoot)return (Entity)Call(WarriorCombat,null,"Spawn",scene,p);}
    static void VerifyWarriorEpicCombat(Map town){
        var p=WarriorEpicPlayer(town);WarriorEpicMemory(p);var scene=WarriorScene(p,3);
        lock(p.ItemSyncRoot)Call(WarriorScenes,null,"Enter",scene,p);
        var body=WarriorSpawn(scene,p);var map=p.Map;p.Entity.X=81;p.Entity.Y=86;
        Check(body.Body==916 && body.Name=="SealingStele" && body.MaxHitpoints==30000 && ReferenceEquals(body,WarriorSpawn(scene,p)),"stele uses evidenced geometry and repeated summons retain one encounter");
        var outsider=WarriorEpicPlayer(town);outsider.Entity.MapID=map.ID;outsider.Entity.X=81;outsider.Entity.Y=86;
        Check(Revora.Game.Attacking.Handle.CanAttack(p.Entity,body,null,true) && !Revora.Game.Attacking.Handle.CanAttack(outsider.Entity,body,null,true),"only the registered Warrior can attack the owned stele");
        uint damage=body.Hitpoints;Revora.Game.Attacking.Handle.ReceiveAttack(outsider.Entity,body,new Attack(true){AttackType=Attack.Melee,Attacker=outsider.Entity.UID,Attacked=body.UID},ref damage,null);
        Check(damage==0 && body.Hitpoints==30000 && WarriorEpicProgress(p)==3,"direct damage path rejects a foreign lethal strike without advancing the quest");
        outsider.Entity.MapID=town.ID;
        uint partial=100;Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,body,new Attack(true){AttackType=Attack.Melee,Attacker=p.Entity.UID,Attacked=body.UID},ref partial,null);
        Check(body.Hitpoints==29900 && WarriorEpicProgress(p)==3,"accepted nonlethal damage reduces health without recording victory");
        p.Trade.InTrade=true;body.Die(p.Entity);
        Check(body.Hitpoints==29900 && WarriorEpicProgress(p)==3,"trade suspends combat completion without granting a victory");p.Trade.InTrade=false;
        SpookKill(p,body);
        Check(!Fenced(p) && WarriorEpicProgress(p)==4 && !map.Entities.ContainsKey(body.UID)
            && Scalar("SELECT count(*) FROM revora_warrior_epic_encounters WHERE uid=@uid AND state='victory'",p.Entity.UID)==1,"accepted lethal combat records stele victory and removes its actor");
        body.Die(p.Entity);Check(WarriorEpicProgress(p)==4,"replayed corpse death cannot advance another stage");
        lock(p.ItemSyncRoot)Call(WarriorScenes,null,"Exit",scene,p);
        scene=WarriorScene(p,4);lock(p.ItemSyncRoot)Call(WarriorScenes,null,"Enter",scene,p);
        bool denied=false;try{WarriorSpawn(scene,p);}catch(InvalidOperationException){denied=true;}
        Check(denied,"final cave cannot summon evil before its chest step is saved");
        WarriorEpicCall(p,"Advance",WarriorEpicStage(4),WarriorEpicContext);body=WarriorSpawn(scene,p);map=p.Map;
        Check(body.Body==917 && body.Name=="TinyMindofEvil" && body.MaxHitpoints==50000 && body.MonsterInfo.Boss,"evil uses a documented Revora model choice and the installed health profile");
        SpookKill(p,body);
        Check(!Fenced(p) && WarriorEpicProgress(p)==6 && !map.Entities.ContainsKey(body.UID) && WarriorEpicItems(p)==0,"evil victory earns a pending claim through actual lethal damage");
        WarriorEpicCall(p,"Claim",WarriorEpicContext);
        Check(WarriorEpicItems(p)==2 && WarriorEpicProgress(p)==7,"both registered combat victories permit the atomic fist pair");
        lock(p.ItemSyncRoot)Call(WarriorScenes,null,"Exit",scene,p);
        var interrupted=WarriorEpicPlayer(town);WarriorEpicMemory(interrupted);scene=WarriorScene(interrupted,3);
        lock(interrupted.ItemSyncRoot)Call(WarriorScenes,null,"Enter",scene,interrupted);body=WarriorSpawn(scene,interrupted);map=interrupted.Map;
        lock(interrupted.ItemSyncRoot)Call(WarriorScenes,null,"Exit",scene,interrupted);
        body.Die(interrupted.Entity);Check(WarriorEpicProgress(interrupted)==3 && !map.Entities.ContainsKey(body.UID),"leaving cancels the actor and rejects late victory without losing the pending trial");
    }
    static object WarriorScene(GameState p,int stage){lock(p.ItemSyncRoot)return Call(WarriorScenes,null,"Create",p,WarriorEpicStage(stage));}
    static void VerifyWarriorEpicScenes(Map town){
        var p=WarriorEpicPlayer(town);WarriorEpicCall(p,"Begin",WarriorEpicContext);
        var other=WarriorEpicPlayer(town);WarriorEpicCall(other,"Begin",WarriorEpicContext);
        var first=WarriorScene(p,1);var second=WarriorScene(other,1);
        var a=(Map)Field(first,"Map");var b=(Map)Field(second,"Map");
        Check(a.ID!=b.ID && a.BaseID==10009 && b.BaseID==10009 && !ReferenceEquals(a.Floor,b.Floor)
            && a.Floor.Bounds.Width==80 && a.Floor.Bounds.Height==80,"Warrior memories own separate maps and terrain");
        Check(a.Npcs.Count==1 && a.Npcs[19253].Mesh==41310 && a.Npcs[19253].X==42 && a.Npcs[19253].Y==40
            && a.Floor[50,55,MapObjectType.Player],"memory chest uses the installed profile and leaves its landing walkable");
        Check((bool)Call(WarriorScenes,null,"Registered",first,p) && !(bool)Call(WarriorScenes,null,"Registered",first,other)
            && !(bool)Call(WarriorScenes,null,"Owns",first,p),"scene registration is owner-specific and does not fake arrival");
        bool denied=false;try{WarriorScene(p,1);}catch(InvalidOperationException){denied=true;}
        Check(denied,"a character cannot allocate a duplicate scene");
        denied=false;try{lock(p.ItemSyncRoot)Call(WarriorScenes,null,"Create",p,WarriorEpicStage(4));}catch(InvalidOperationException){denied=true;}
        Check(denied,"scene selection cannot skip saved Warrior progress");
        var prior=p.Entity.MapID;p.Entity.MapID=a.ID;
        denied=false;try{Call(WarriorScenes,null,"Release",first);}catch(InvalidOperationException){denied=true;}
        p.Entity.MapID=prior;
        Check(denied && Revora.Kernel.Maps.ContainsKey(a.ID),"cleanup refuses to strand a registered occupant");
        Call(WarriorScenes,null,"Release",first);Call(WarriorScenes,null,"Release",first);Call(WarriorScenes,null,"Release",second);
        Check(!Revora.Kernel.Maps.ContainsKey(a.ID) && !MapsTable.MapInformations.ContainsKey(a.ID)
            && !(bool)Call(WarriorScenes,null,"Registered",first,p),"scene release removes its registrations once and revokes ownership");
        WarriorEpicCall(p,"Advance",WarriorEpicStage(1),WarriorEpicContext);WarriorEpicCall(p,"Advance",WarriorEpicStage(2),WarriorEpicContext);
        var formation=WarriorScene(p,3);var map=(Map)Field(formation,"Map");
        Check(map.BaseID==10010 && map.Floor.Bounds.Width==120 && map.Floor[35,45,MapObjectType.Player] && map.Npcs.Count==1 && map.Npcs.ContainsKey(19252),
            "formation scene loads real terrain without a dialogue substitute for the stele");
        Call(WarriorScenes,null,"Release",formation);WarriorEpicFight(p,3);
        var final=WarriorScene(p,4);map=(Map)Field(final,"Map");
        Check(map.BaseID==10011 && map.Npcs.Count==2 && map.Npcs[19254].Mesh==41310 && map.Npcs[19259].Mesh==41360
            && map.Floor[50,55,MapObjectType.Player],"final cave installs the chest and Spirit while reserving combat ownership");
        var replacement=WarriorEpicPlayer(town);Revora.Kernel.GamePool[p.Entity.UID]=replacement;
        Check(!(bool)Call(WarriorScenes,null,"Registered",final,p),"replaced sessions lose private-scene ownership");
        Revora.Kernel.GamePool[p.Entity.UID]=p;Call(WarriorScenes,null,"Release",final);
        Check(WarriorEpicProgress(p)==4 && WarriorEpicItems(p)==0,"creating and releasing scenes awards no stage or quest reward");
        var traveler=WarriorEpicPlayer(town);WarriorEpicCall(traveler,"Begin",WarriorEpicContext);
        var journey=WarriorScene(traveler,1);map=(Map)Field(journey,"Map");
        Check(!(bool)Call(WarriorScenes,null,"AllowTeleport",other,map.ID,(ushort)50,(ushort)55)
            && !(bool)Call(WarriorScenes,null,"AllowTeleport",traveler,map.ID,(ushort)50,(ushort)55),"private entry rejects outsiders and an owner without a server transfer");
        lock(traveler.ItemSyncRoot)Call(WarriorScenes,null,"Enter",journey,traveler);
        Check(traveler.Entity.MapID==map.ID && (bool)Call(WarriorScenes,null,"Owns",journey,traveler)
            && Scalar("SELECT mapid FROM entities WHERE uid=@uid",traveler.Entity.UID)==map.ID,"server transfer enters and durably saves the owned scene");
        Check((bool)Call(WarriorScenes,null,"AllowTeleport",traveler,map.ID,(ushort)50,(ushort)55)
            && !(bool)Call(WarriorScenes,null,"AllowTeleport",traveler,map.ID,(ushort)42,(ushort)40),"owned local travel accepts its floor and rejects occupied chest terrain");
        other.Entity.Teleport(map.ID,50,55);Check(other.Entity.MapID!=map.ID,"actual teleport overload blocks a foreign scene admission");
        lock(traveler.ItemSyncRoot)Call(WarriorScenes,null,"Exit",journey,traveler);
        Check(traveler.Entity.MapID==1004 && traveler.Entity.X==44 && traveler.Entity.Y==74
            && Scalar("SELECT mapid FROM entities WHERE uid=@uid",traveler.Entity.UID)==1004 && !Revora.Kernel.Maps.ContainsKey(map.ID),"free exit saves the instructor return before releasing its scene");
        journey=WarriorScene(traveler,1);map=(Map)Field(journey,"Map");
        lock(traveler.ItemSyncRoot)Call(WarriorScenes,null,"Enter",journey,traveler);
        traveler.Entity.Hitpoints=0;Call(WarriorScenes,null,"Tick");
        Check(!traveler.Entity.Dead && traveler.Entity.MapID==1004 && !Revora.Kernel.Maps.ContainsKey(map.ID),"scene recovery revives and returns a defeated Warrior");
        journey=WarriorScene(traveler,1);map=(Map)Field(journey,"Map");
        lock(traveler.ItemSyncRoot)Call(WarriorScenes,null,"Enter",journey,traveler);
        var restored=ReloadBall(traveler);
        Check(restored.Entity.MapID==1002 && WarriorEpicProgress(restored)==1,"cold entity reload returns a dynamic-scene occupant to safety without losing route progress");
        Call(WarriorScenes,null,"Tick");
        Check(!Revora.Kernel.Maps.ContainsKey(map.ID),"replaced connection releases its abandoned private terrain");
        journey=WarriorScene(restored,1);map=(Map)Field(journey,"Map");
        lock(restored.ItemSyncRoot)Call(WarriorScenes,null,"Enter",journey,restored);
        journey.GetType().GetField("Until",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(journey,DateTime.UtcNow.AddSeconds(-1));
        Check(!(bool)Call(WarriorScenes,null,"AllowTeleport",restored,map.ID,restored.Entity.X,restored.Entity.Y),"expired scene rejects further private travel");
        Call(WarriorScenes,null,"Tick");
        Check(restored.Entity.MapID==1004 && !Revora.Kernel.Maps.ContainsKey(map.ID) && WarriorEpicProgress(restored)==1,"expired scene evacuates without erasing the resumable route");
    }
    static void WarriorJournalCheck(GameState p,int stage){
        var list=new System.Collections.Generic.List<MsgTaskStatus.QuestData>();QuestListSystem.Fill(p,list);
        var route=list.Where(x=>WarriorMissions.Contains((uint)x.UID)).ToArray();
        Check(route.Length==7 && route.Select(x=>x.UID).Distinct().Count()==7 && WarriorMissions.Select((id,index)=>
            route.Single(x=>(uint)x.UID==id).Status==(stage>index?Status.Finished:stage==index && stage!=0?Status.Accepted:Status.Available)).All(x=>x),
            "Warrior journal projects saved stage "+stage+" in native mission order without duplicate legacy entries");
    }
    static void VerifyWarriorEpicJournal(Map map){
        Call(WarriorJournal,null,"Initialize");var p=WarriorEpicPlayer(map);p.Quests=new Quests(p);p.Quests.Load();
        foreach(uint mission in WarriorMissions){p.Quests.Accept((QuestID)mission);p.Quests.IncreaseQuestKills((QuestID)mission,9);}
        p.Quests.Save();WarriorJournalCheck(p,0);
        foreach(uint mission in WarriorMissions){
            var begin=new MsgTaskStatus(true,1){Action=MsgTaskStatus.QuestAction.Begin};
            begin.Apend(MsgTaskStatus.QuestData.Create((QuestID)mission,Status.Finished,0));
            MsgTaskStatus.Process(begin.ToArray(),p);PacketHandler.HandlePacket(AbyssQuit(mission),p);
            Check(WarriorEpicProgress(p)==0 && p.Quests.GetQuest((QuestID)mission).Kills==9 && p.Quests.CheckQuest((QuestID)mission)==Status.Accepted,
                "Warrior client Begin/quit cannot author saved progress or erase legacy data: "+mission);
        }
        WarriorEpicCall(p,"Begin",WarriorEpicContext);WarriorJournalCheck(p,1);
        WarriorEpicCall(p,"Advance",WarriorEpicStage(1),WarriorEpicContext);WarriorJournalCheck(p,2);
        WarriorEpicCall(p,"Advance",WarriorEpicStage(2),WarriorEpicContext);WarriorJournalCheck(p,3);
        WarriorEpicFight(p,3);WarriorJournalCheck(p,4);
        WarriorEpicCall(p,"Advance",WarriorEpicStage(4),WarriorEpicContext);WarriorJournalCheck(p,5);
        WarriorEpicFight(p,5);WarriorJournalCheck(p,6);
        WarriorEpicCall(p,"Claim",WarriorEpicContext);WarriorJournalCheck(p,7);
        p=ReloadBall(p);p.Quests=new Quests(p);p.Quests.Load();WarriorJournalCheck(p,7);
        p.Entity.Class=15;WarriorJournalCheck(p,7);
        var denied=WarriorEpicPlayer(map);denied.Entity.Level=39;denied.Quests=new Quests(denied);denied.Quests.Load();denied.Quests.Accept((QuestID)3678);
        var hidden=new System.Collections.Generic.List<MsgTaskStatus.QuestData>();QuestListSystem.Fill(denied,hidden);
        Check(!hidden.Any(x=>WarriorMissions.Contains((uint)x.UID)),"ineligible unadmitted Warrior cannot display a forged legacy route");
        Check(!(bool)Call(WarriorJournal,null,"Protect",3677u) && !(bool)Call(WarriorJournal,null,"Protect",3684u),"Warrior journal protection leaves neighboring quests alone");
    }
    static Func<bool> WarriorEpicContext=()=>true; // Storage fixture; runtime world proof is tested separately when connected.
    static GameState WarriorEpicPlayer(Map map){return PromotionPlayer(map,23,70,10001);}
    static void WarriorEpicMemory(GameState p){
        if(!(bool)WarriorEpicCall(p,"Begin",WarriorEpicContext)
            || !(bool)WarriorEpicCall(p,"Advance",WarriorEpicStage(1),WarriorEpicContext)
            || !(bool)WarriorEpicCall(p,"Advance",WarriorEpicStage(2),WarriorEpicContext))throw new Exception("Warrior memory storage setup failed.");
    }
    static Guid WarriorEpicFight(GameState p,int stage){
        var id=(Guid)WarriorEpicCall(p,"OpenEncounter",WarriorEpicStage(stage),WarriorEpicContext);
        if(id==Guid.Empty || !(bool)WarriorEpicCall(p,"Defeat",WarriorEpicStage(stage),id,WarriorEpicContext))throw new Exception("Warrior combat storage setup failed.");
        return id;
    }
    static void WarriorEpicVictory(GameState p){WarriorEpicMemory(p);WarriorEpicFight(p,3);WarriorEpicCall(p,"Advance",WarriorEpicStage(4),WarriorEpicContext);WarriorEpicFight(p,5);}
    static void VerifyWarriorMaxReward(GameState p){
        Check(Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND id=624439 AND bound=0 AND plus=12 AND bless=7 AND socketone=255 AND sockettwo=255",p.Entity.UID)==2,
            "both maximum Warrior rewards persist their complete unbound enhancement profile");
    }
    static long WarriorEpicItems(GameState p){return Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND id=624439",p.Entity.UID);}
    static void VerifyWarriorEpicStore(string runtime){
        VerifyWarriorTraining(runtime);var map=Revora.Kernel.Maps[1004];Call(WarriorEpicStorage,null,"Initialize");Call(WarriorEpicStorage,null,"Initialize");
        var p=WarriorEpicPlayer(map);uint cp=p.Entity.ConquerPoints;var money=p.Entity.Money;
        Check(p.Account.State==AccountTable.AccountState.Player && WarriorEpicProgress(p)==0,"ordinary Warrior starts with no invented epic progress");
        Check(!(bool)WarriorEpicCall(p,"Claim",WarriorEpicContext) && !(bool)WarriorEpicCall(p,"Advance",WarriorEpicStage(4),WarriorEpicContext),"unearned final reward and skipped chest stage are denied");
        Parallel.For(0,12,_=>WarriorEpicCall(p,"Begin",WarriorEpicContext));
        var jade=p.Inventory.Objects.Single(i=>i.ID==3200206);
        Check(jade.Bound && WarriorEpicProgress(p)==1 && Scalar("SELECT count(*) FROM revora_warrior_epic_steps WHERE uid=@uid",p.Entity.UID)==1,"concurrent admission issues one bound jade and one step receipt");
        var forged=Pack(p,3200206,1);forged.Bound=true;Query("UPDATE items SET bound=1 WHERE uid=@uid","uid",(long)forged.UID);
        DisRemove(p,jade);
        Check(!(bool)WarriorEpicCall(p,"Advance",WarriorEpicStage(1),WarriorEpicContext),"an unissued jade cannot advance the owner's journey");
        Check((bool)WarriorEpicCall(p,"RecoverJade",WarriorEpicContext) && p.Inventory.Objects.Any(i=>i.UID==jade.UID),"lost jade recovery restores the original issued identity");
        Check(!(bool)WarriorEpicCall(p,"RecoverJade",WarriorEpicContext),"repeated recovery cannot create a second issued jade");
        Check((bool)WarriorEpicCall(p,"Advance",WarriorEpicStage(1),WarriorEpicContext) && !p.Inventory.Objects.Any(i=>i.UID==jade.UID) && p.Inventory.Objects.Any(i=>i.UID==forged.UID),"memory entry consumes only the issued jade atomically");
        Check((bool)WarriorEpicCall(p,"Advance",WarriorEpicStage(2),WarriorEpicContext),"memory witness advances its own ordered stage");
        var stale=(Guid)WarriorEpicCall(p,"OpenEncounter",WarriorEpicStage(3),WarriorEpicContext);
        var current=(Guid)WarriorEpicCall(p,"OpenEncounter",WarriorEpicStage(3),WarriorEpicContext);
        Check(stale!=current && !(bool)WarriorEpicCall(p,"Defeat",WarriorEpicStage(3),stale,WarriorEpicContext),"recreated encounter retires the prior combat receipt");
        var other=WarriorEpicPlayer(map);WarriorEpicMemory(other);
        Check(!(bool)WarriorEpicCall(other,"Defeat",WarriorEpicStage(3),current,WarriorEpicContext) && WarriorEpicProgress(other)==3,"another character cannot claim the encounter");
        bool rejected=false;try{WarriorEpicCall(p,"Defeat",WarriorEpicStage(3),current,(Func<bool>)(()=>false));}catch(InvalidOperationException){rejected=true;}
        Check(rejected && WarriorEpicProgress(p)==3,"missing runtime victory proof preserves the pending trial");
        Check((bool)WarriorEpicCall(p,"Defeat",WarriorEpicStage(3),current,WarriorEpicContext) && !(bool)WarriorEpicCall(p,"Defeat",WarriorEpicStage(3),current,WarriorEpicContext),"owned stele victory advances once");
        WarriorEpicCall(p,"Advance",WarriorEpicStage(4),WarriorEpicContext);WarriorEpicFight(p,5);
        Check(WarriorEpicProgress(p)==6 && WarriorEpicItems(p)==0,"evil victory preserves an unclaimed reward");
        Parallel.For(0,12,_=>WarriorEpicCall(p,"Claim",WarriorEpicContext));
        var fists=p.Inventory.Objects.Where(i=>i.ID==624439).ToArray();
        Check(WarriorEpicProgress(p)==7 && fists.Length==2 && fists.All(i=>!i.Bound && i.Plus==12 && i.Bless==7 && i.SocketOne==Enums.Gem.EmptySocket && i.SocketTwo==Enums.Gem.EmptySocket && i.StackSize==1) && fists[0].UID!=fists[1].UID
            && WarriorEpicItems(p)==2 && Scalar("SELECT count(*) FROM revora_warrior_epic_rewards WHERE uid=@uid",p.Entity.UID)==2,"concurrent final claims deliver exactly two separate unbound maximum enhanced fists");
        Check(p.Entity.ConquerPoints==cp && p.Entity.Money==money,"the quest storage journey charges no CPs or gold");
        p=ReloadBall(p);Check(WarriorEpicProgress(p)==7 && p.Inventory.Objects.Count(i=>i.ID==624439)==2 && !(bool)WarriorEpicCall(p,"Claim",WarriorEpicContext),"cold reload preserves both rewards and the permanent replay guard");
        var legacy=WarriorEpicPlayer(map);WarriorEpicVictory(legacy);
        Query("UPDATE revora_warrior_epic SET valuable_reward=false WHERE uid=@uid","uid",(long)legacy.Entity.UID);
        WarriorEpicCall(legacy,"Claim",WarriorEpicContext);legacy=ReloadBall(legacy);
        Check(legacy.Inventory.Objects.Count(i=>i.ID==624078 && i.Bound && i.Plus==0)==2 && WarriorEpicItems(legacy)==0
            && !(bool)WarriorEpicCall(legacy,"Claim",WarriorEpicContext),"old contract keeps the original bound pair across reload and replay");
        var full=WarriorEpicPlayer(map);WarriorEpicVictory(full);while(full.Inventory.Count<39)Pack(full,1088001,1);
        Check(!(bool)WarriorEpicCall(full,"Claim",WarriorEpicContext) && WarriorEpicProgress(full)==6 && WarriorEpicItems(full)==0,"one free bag slot preserves the claim without partial reward");
        DisRemove(full,full.Inventory.Objects.First());Check((bool)WarriorEpicCall(full,"Claim",WarriorEpicContext) && full.Inventory.Count==40,"exactly two free bag slots permit the complete pair");
        foreach(string target in new[]{"revora_warrior_epic","revora_warrior_epic_steps","revora_warrior_epic_rewards","items"}){
            var fault=WarriorEpicPlayer(map);WarriorEpicVictory(fault);
            Query("CREATE FUNCTION warrior_epic_suppress() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END'");
            Query("CREATE TRIGGER warrior_epic_suppress BEFORE INSERT OR UPDATE ON "+target+" FOR EACH ROW EXECUTE FUNCTION warrior_epic_suppress()");
            bool failed=false;try{WarriorEpicCall(fault,"Claim",WarriorEpicContext);}catch(InvalidOperationException){failed=true;}
            finally{Query("DROP TRIGGER warrior_epic_suppress ON "+target);Query("DROP FUNCTION warrior_epic_suppress()");}
            Check(failed && WarriorEpicProgress(fault)==6 && WarriorEpicItems(fault)==0 && !fault.Inventory.Objects.Any(i=>i.ID==624439)
                && Scalar("SELECT count(*) FROM revora_warrior_epic_rewards WHERE uid=@uid",fault.Entity.UID)==0,"failed final write rolls back the whole pair and retains victory: "+target);
            Check((bool)WarriorEpicCall(fault,"Claim",WarriorEpicContext),"claim can retry after a rolled-back write: "+target);
        }
        var enhancement=WarriorEpicPlayer(map);WarriorEpicVictory(enhancement);
        Query("CREATE FUNCTION warrior_epic_enhancement() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER warrior_epic_enhancement BEFORE UPDATE ON items FOR EACH ROW EXECUTE FUNCTION warrior_epic_enhancement()");
        bool enhancementFailed=false;try{WarriorEpicCall(enhancement,"Claim",WarriorEpicContext);}catch(InvalidOperationException){enhancementFailed=true;}
        finally{Query("DROP TRIGGER warrior_epic_enhancement ON items;DROP FUNCTION warrior_epic_enhancement()");}
        Check(enhancementFailed && WarriorEpicItems(enhancement)==0 && WarriorEpicProgress(enhancement)==6
            && Scalar("SELECT count(*) FROM revora_warrior_epic_rewards WHERE uid=@uid",enhancement.Entity.UID)==0,"failed enhancement update rolls back the pair and completion");
        WarriorEpicCall(enhancement,"Claim",WarriorEpicContext);VerifyWarriorMaxReward(enhancement);
        var late=WarriorEpicPlayer(map);WarriorEpicVictory(late);
        Query("CREATE FUNCTION warrior_epic_second_item() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN IF NEW.id=624439 AND EXISTS(SELECT 1 FROM items WHERE entityid=NEW.entityid AND id=624439) THEN RETURN NULL; END IF; RETURN NEW; END'");
        Query("CREATE TRIGGER warrior_epic_second_item BEFORE INSERT ON items FOR EACH ROW EXECUTE FUNCTION warrior_epic_second_item()");
        bool lateFailed=false;try{WarriorEpicCall(late,"Claim",WarriorEpicContext);}catch(InvalidOperationException){lateFailed=true;}
        finally{Query("DROP TRIGGER warrior_epic_second_item ON items");Query("DROP FUNCTION warrior_epic_second_item()");}
        Check(lateFailed && WarriorEpicItems(late)==0 && WarriorEpicProgress(late)==6 && Scalar("SELECT count(*) FROM revora_warrior_epic_rewards WHERE uid=@uid",late.Entity.UID)==0,"failure on the second fist rolls back the first fist and its receipt");
        foreach(string target in new[]{"revora_warrior_epic","revora_warrior_epic_steps","items"}){
            var fault=WarriorEpicPlayer(map);
            Query("CREATE FUNCTION warrior_epic_begin_fail() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END'");
            Query("CREATE TRIGGER warrior_epic_begin_fail BEFORE INSERT ON "+target+" FOR EACH ROW EXECUTE FUNCTION warrior_epic_begin_fail()");
            bool failed=false;try{WarriorEpicCall(fault,"Begin",WarriorEpicContext);}catch(InvalidOperationException){failed=true;}
            finally{Query("DROP TRIGGER warrior_epic_begin_fail ON "+target);Query("DROP FUNCTION warrior_epic_begin_fail()");}
            Check(failed && WarriorEpicProgress(fault)==0 && !fault.Inventory.Objects.Any(i=>i.ID==3200206)
                && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND id=3200206",fault.Entity.UID)==0,"failed admission does not leave a jade or partial journey: "+target);
        }
        var proof=WarriorEpicPlayer(map);WarriorEpicMemory(proof);
        var encounter=(Guid)WarriorEpicCall(proof,"OpenEncounter",WarriorEpicStage(3),WarriorEpicContext);int observations=0;bool changed=false;
        try{WarriorEpicCall(proof,"Defeat",WarriorEpicStage(3),encounter,(Func<bool>)(()=>++observations==1));}catch(InvalidOperationException){changed=true;}
        Check(changed && WarriorEpicProgress(proof)==3 && Scalar("SELECT count(*) FROM revora_warrior_epic_encounters WHERE uid=@uid AND state='active'",proof.Entity.UID)==1,"lost victory ownership before commit rolls back both encounter and stage");
        foreach(string condition in new[]{"level","class","fake","dead","trade","session"}){
            var denied=WarriorEpicPlayer(map);
            if(condition=="level")denied.Entity.Level=39;if(condition=="class")denied.Entity.Class=15;
            if(condition=="fake")denied.Fake=true;if(condition=="dead")denied.Entity.Hitpoints=0;
            if(condition=="trade")denied.Trade.InTrade=true;if(condition=="session")Revora.Kernel.GamePool[denied.Entity.UID]=proof;
            bool refused=false;try{WarriorEpicCall(denied,"Begin",WarriorEpicContext);}catch(InvalidOperationException){refused=true;}
            Check(refused && Scalar("SELECT count(*) FROM revora_warrior_epic WHERE uid=@uid",denied.Entity.UID)==0,"invalid admission creates no persistent route: "+condition);
        }
        VerifyWarriorEpicJournal(map);
        VerifyWarriorEpicScenes(map);
        VerifyWarriorEpicCombat(map);
        VerifyWarriorEpicJourney(map);
    }
}
