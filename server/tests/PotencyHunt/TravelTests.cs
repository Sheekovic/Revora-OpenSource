using System;
using System.Linq;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static readonly Type Travel=typeof(Entity).Assembly.GetType("Revora.Game.Features.TravelProgress");
    static void Visit(GameState p,Map map,ushort x,ushort y,int arrival=1) {
        Call(Travel,null,"Arrived",p,map,x,y,Enum.ToObject(Travel.GetNestedType("Arrival",System.Reflection.BindingFlags.NonPublic),arrival));
    }
    static long Visits(GameState p) {return Scalar("SELECT count(*) FROM revora_travel_visits WHERE uid=@uid",p.Entity.UID);}
    static GameState TravelPlayer(Map map,ushort x=424,ushort y=339) {
        var p=AppearancePlayer(map);p.Entity.X=x;p.Entity.Y=y;return p;
    }
    static Map TravelAlias(string runtime,ushort id,ushort profile) {
        var floor=World(runtime,profile);
        var map=(Map)typeof(CompanionTests).GetMethod("World",Hidden).Invoke(null,new object[]{id});
        map.Floor=floor.Floor;map.BaseID=profile;map.Npcs=new System.Collections.Generic.Dictionary<uint,Revora.Interfaces.INpc>();
        foreach(var field in typeof(Map).GetFields(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance))
            if(field.GetValue(map)==null && field.FieldType.IsGenericType && field.FieldType.GetConstructor(Type.EmptyTypes)!=null)
                field.SetValue(map,Activator.CreateInstance(field.FieldType));
        MapsTable.MapInformations[id]=new MapsTable.MapInformation{ID=id,BaseID=profile,Status=2};
        return map;
    }
    static void VerifyTravel(string runtime,Map town) {
        Query("DROP TABLE entities;CREATE TABLE entities AS TABLE public.entities WITH NO DATA;CREATE TABLE titles (LIKE public.titles INCLUDING ALL)");
        StorageManager.Load();StorageItem.Load();PerfectionManager.AbilityScore.Read(out PerfectionManager.AbilitiesScore);
        Call(EvidenceType,null,"Initialize");Call(Travel,null,"Initialize");Call(Travel,null,"Initialize");
        var p=TravelPlayer(town);
        Check(p.Account.State==AccountTable.AccountState.Player && town.Floor[424,339,MapObjectType.Player],"ordinary travel fixture stands on installed Twin City terrain");
        Visit(p,town,424,339,4);
        Check(!Fenced(p) && Visits(p)==2 && HasAchievement(p,10612) && HasAchievement(p,10613),"accepted world entry immediately persists both overlapping native regions");
        Parallel.For(0,20,i=>Visit(p,town,425,339,2));
        Check(Visits(p)==2 && Scalar("SELECT count(*) FROM revora_travel_visits WHERE uid=@uid AND x=424 AND y=339 AND arrival=4",p.Entity.UID)==2,"parallel replay preserves first-visit coordinates and arrival type");
        Check(EntityTable.SaveEntity(p) && Visits(p)==2,"full character save preserves durable travel evidence");
        foreach(string state in new[]{"fake","dead","unloaded","inactive","fenced","replaced"}) {
            var f=TravelPlayer(town);
            if(state=="fake")f.Fake=true;if(state=="dead")f.Entity.Hitpoints=0;
            if(state=="unloaded")f.Entity.FullyLoaded=false;if(state=="inactive")f.Action=1;
            if(state=="fenced")typeof(Entity).GetField("PersistenceRequiresReload",Instance).SetValue(f.Entity,true);
            if(state=="replaced")Kernel.GamePool.Remove(f.Entity.UID);
            Visit(f,town,424,339);Check(Visits(f)==0,"travel rejects "+state+" sessions");
        }
        var invalid=TravelPlayer(town);
        Visit(invalid,null,424,339);Visit(invalid,town,65535,65535);Visit(invalid,town,424,339,0);Visit(invalid,town,424,339,5);
        World(runtime,1002);Visit(invalid,town,424,339);Kernel.Maps[1002]=town;
        Check(Visits(invalid)==0 && !Fenced(invalid),"invalid geometry, arrival enums and replaced maps cannot earn visits");
        ushort oldBase=town.BaseID;town.BaseID=1011;Visit(invalid,town,424,339);town.BaseID=oldBase;
        Check(Visits(invalid)==0,"unrelated dynamic base-floor identity cannot impersonate a static destination");
        Achievement.Process(AchievementRequest(invalid.Entity.UID,2,10612),invalid);
        Check(Visits(invalid)==0 && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10612",invalid.Entity.UID)==0,"client-authored travel completion remains rejected");
        foreach(string table in new[]{"revora_travel_visits","revora_achievement_evidence"}) {
            var f=TravelPlayer(town);
            Query("CREATE FUNCTION suppress_travel() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER suppress_travel BEFORE INSERT ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_travel()");
            try{Visit(f,town,424,339);}finally{Query("DROP TRIGGER suppress_travel ON "+table+";DROP FUNCTION suppress_travel()");}
            Check(Fenced(f) && Visits(f)==0 && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND source='travel-region-v1'",f.Entity.UID)==0,"suppressed "+table+" write rolls back the complete overlapping visit");
            DemonReset(f);Visit(f,town,424,339);
            Check(!Fenced(f) && Visits(f)==2 && HasAchievement(f,10612),"failed transaction leaves no session cache that would prevent a valid retry");
        }
        var walking=TravelPlayer(town,424,339);
        Check(walking.Entity.Move(Enums.ConquerAngle.East,1) && Visits(walking)==2
            && Scalar("SELECT count(*) FROM revora_travel_visits WHERE uid=@uid AND arrival=1",walking.Entity.UID)==2,"production walking records the accepted point");
        var sliding=TravelPlayer(town,424,339);
        Check(sliding.Entity.Move(Enums.ConquerAngle.East,true) && Visits(sliding)==2,"production two-step movement records the accepted point");
        var keeper=TravelPlayer(town);
        var pet=new Entity(EntityFlag.Monster,false){MapID=1002,X=430,Y=340,Owner=keeper};
        Check(pet.Move(Enums.ConquerAngle.East,1) && Visits(keeper)==0,"owned monster movement cannot earn a player's travel achievement");
        var jumping=TravelPlayer(town,424,339);SkyJump(jumping,425,339);
        Check(!Fenced(jumping) && Visits(jumping)==2 && Scalar("SELECT count(*) FROM revora_travel_visits WHERE uid=@uid AND arrival=2",jumping.Entity.UID)==2,"production bounded jump records the actual landing");
        var badJump=TravelPlayer(town,424,339);
        typeof(Screen).GetField("DisposalSyncRoot",System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).SetValue(badJump.Screen,new object());
        Server.Thread=(Revora.Thread)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Revora.Thread));
        GeneralData.PlayerJump(new Data(true){UID=badJump.Entity.UID,ID=Data.Jump,wParam1=424,wParam2=339,Xt1=600,Yt1=339,TimeStamp=Time32.Now},badJump);
        Check(Visits(badJump)==0,"out-of-range jump earns no travel completion");
        var teleport=TravelPlayer(town,424,339);teleport.Entity.Teleport(425,339);
        Check(Visits(teleport)==2 && Scalar("SELECT count(*) FROM revora_travel_visits WHERE uid=@uid AND arrival=3",teleport.Entity.UID)==2,"production same-map teleport records accepted travel");
        var market=World(runtime,1036);teleport.Entity.Teleport(1036,240,249);
        Check(teleport.Entity.MapID==1036 && HasAchievement(teleport,10812) && Visits(teleport)==3,"production map transition earns the Market visit");
        teleport.Entity.Teleport(1002,424,339);
        Check(HasAchievement(teleport,10812) && Visits(teleport)==3,"leaving immediately preserves a brief visit without waiting for a periodic refresh");
        var desert=World(runtime,1000);var mystic=World(runtime,1001);
        typeof(Map).GetMethod("LoadPortals",Instance).Invoke(desert,null);
        var portal=desert.Portals.First(v=>v.DestinationMapID==1001);
        var traveller=TravelPlayer(desert,portal.CurrentX,portal.CurrentY);
        GeneralData.UsePortal(new Data(true){UID=traveller.Entity.UID,dwParam=0},traveller);
        Check(traveller.Entity.MapID==1001 && HasAchievement(traveller,10703),"ordinary player reaches Mystic Castle through the installed Desert portal using server position");
        var remote=TravelPlayer(desert,500,500);
        GeneralData.UsePortal(new Data(true){UID=remote.Entity.UID,dwParam=(uint)(portal.CurrentX|(portal.CurrentY<<16))},remote);
        Check(remote.Entity.MapID==1002 && !HasAchievement(remote,10703),"forged portal coordinates cannot claim the remote destination");
        var facts=System.IO.File.ReadAllLines(System.IO.Path.Combine(runtime,"../../docs/research/TRAVEL_REGION_FACTS.csv")).Skip(1)
            .Select(line=>line.Split(',').Select(int.Parse).ToArray()).ToArray();
        var installed=facts.Select(row=>row[0]).Distinct().ToDictionary(id=>id,id=>Scalar("SELECT COALESCE((SELECT mapdoc FROM public.maps WHERE id=@uid),0)",(uint)id));
        Check(installed[1217]==1075 && installed[1219]==1212,"reviewed fixed world aliases match installed map metadata");
        var identityPacket=new MapStatus{ID=1217,BaseID=1075,Status=2}.ToArray();
        Check(System.BitConverter.ToUInt16(identityPacket,2)==1110 && identityPacket.Skip(4).Take(8).SequenceEqual(new byte[]{8,0xc1,9,16,0xb3,8,24,2}),"1110 emits logical map as field one and terrain as field two, matching the reviewed native parser");
        TravelAlias(runtime,1217,1075);TravelAlias(runtime,1219,1212);
        var pending=new System.Collections.Generic.HashSet<int>();
        foreach(var group in facts.GroupBy(row=>row[6])) {
            bool tested=false;
            foreach(var row in group) {
                ushort id=(ushort)row[0];bool alias=id==1217 || id==1219;
                string path=System.IO.Path.Combine(runtime,"database/maps/"+(alias?installed[id]:id)+".map");
                if((!alias && installed[id]!=id) || !System.IO.File.Exists(path))continue;
                Map destination;if(!Kernel.Maps.TryGetValue(id,out destination))destination=World(runtime,id);
                for(int y=row[3];y<Math.Min(row[3]+row[5],destination.Floor.Bounds.Height) && !tested;y++)
                    for(int x=row[2];x<Math.Min(row[2]+row[4],destination.Floor.Bounds.Width) && !tested;x++) {
                        if(!destination.Floor[x,y,MapObjectType.Player])continue;
                        var visitor=TravelPlayer(destination,(ushort)x,(ushort)y);Visit(visitor,destination,(ushort)x,(ushort)y);
                        Check(!Fenced(visitor) && HasAchievement(visitor,group.Key),"installed terrain supports visit evidence for "+group.Key+" on map "+id);
                        tested=true;
                    }
                if(tested)break;
            }
            if(!tested)pending.Add(group.Key);
        }
        Check(pending.SetEquals(new[]{10726,10801,10802,10808}),"four baseline gaps before startup defaults; forty-two regions have reviewed installed terrain");
        foreach(ushort id in new ushort[]{1217,1219}) {
            var map=Kernel.Maps[id];var visitor=TravelPlayer(map,448,272);
            ushort good=map.BaseID;map.BaseID=1002;Visit(visitor,map,448,272);map.BaseID=good;
            Check(Visits(visitor)==0,"fixed world alias rejects a substituted floor: "+id);
        }
        var unrelated=TravelAlias(runtime,62000,1075);var intruder=TravelPlayer(unrelated,363,13);
        Visit(intruder,unrelated,363,13);Check(Visits(intruder)==0,"arbitrary instance cannot inherit the fixed Joint Canyon achievement");
        var globe=Kernel.Maps[1214];typeof(Map).GetMethod("LoadPortals",Instance).Invoke(globe,null);
        var jointPortal=globe.Portals.First(v=>v.DestinationMapID==1217);
        var joint=TravelPlayer(globe,jointPortal.CurrentX,jointPortal.CurrentY);
        GeneralData.UsePortal(new Data(true){UID=joint.Entity.UID},joint);
        Check(joint.Entity.MapID==1217 && joint.Map.BaseID==1075 && HasAchievement(joint,10719),"installed portal earns Joint Canyon using its logical ID without changing Potency Hunt terrain");
        foreach(string table in new[]{"teamarena","championarena","quests","status","reward","rewardclameid","merchant","dialyquests","activeness","house","pkexploits","achievement","itemadding"})
            Query("CREATE TABLE IF NOT EXISTS "+table+" AS TABLE public."+table+" WITH NO DATA");
        Query("CREATE TABLE prizes(uid bigint PRIMARY KEY,name text,prizes bytea)");
        teleport.LastResetTime=DateTime.Now;Check(EntityTable.SaveEntity(teleport),"travel character saves before reconnect");
        var reloaded=TravelPlayer(town);uint placeholder=reloaded.Entity.UID;reloaded.Account.EntityID=teleport.Entity.UID;reloaded.Account.Username="travel_fixture";reloaded.Quests=new Quests(reloaded);
        Check((bool)Call(typeof(EntityTable),null,"LoadEntityState",reloaded),"production character loader restores a travelled ordinary player");
        Kernel.GamePool.Remove(placeholder);Kernel.GamePool[teleport.Entity.UID]=reloaded;reloaded.Action=2;EvidenceRefresh(reloaded);Visit(reloaded,town,424,339,4);
        Check(!Fenced(reloaded) && Visits(reloaded)==3 && HasAchievement(reloaded,10612) && HasAchievement(reloaded,10613) && HasAchievement(reloaded,10812),"new session restores earned bits and re-entry adds no duplicate visits");
        VerifyExplorationRoutes(runtime,town);
    }
    static bool TravelEarned(GameState p,int id) {return p.Entity.MyAchievement!=null && HasAchievement(p,id);}
    static void ExplorationNpc(GameState p,uint id,byte option) {
        p.ActiveNpc=id;Npcs.GetDialog(new NpcRequest(true){NpcID=id,OptionID=option},p);
    }
    static void VerifyExplorationRoutes(string runtime,Map town) {
        var feature=typeof(Entity).Assembly.GetType("Revora.Game.Features.ExplorationWorld");
        MapsTable.MapInformations.Remove(1806);MapsTable.MapInformations.Remove(1927);
        Call(feature,null,"PrepareMaps");Call(feature,null,"PrepareMaps");
        Check(MapsTable.MapInformations[1806].ID==1806 && MapsTable.MapInformations[1806].BaseID==1806
            && MapsTable.MapInformations[1927].ID==1927 && MapsTable.MapInformations[1927].BaseID==1927,"startup restores missing map metadata idempotently from reviewed terrain");
        var ownerPolicy=MapsTable.MapInformations[1806];ownerPolicy.Status=7;MapsTable.MapInformations[1806]=ownerPolicy;
        Call(feature,null,"PrepareMaps");Check(MapsTable.MapInformations[1806].Status==7,"compatible explicit owner map policy is preserved");
        var collision=MapsTable.MapInformations[1927];collision.Owner=42;MapsTable.MapInformations[1927]=collision;
        MapsTable.MapInformations.Remove(1806);bool rejected=false;
        try{Call(feature,null,"PrepareMaps");}catch(InvalidOperationException){rejected=true;}
        Check(rejected && !MapsTable.MapInformations.ContainsKey(1806) && MapsTable.MapInformations[1927].Owner==42,"conflicting ownership rejects setup without partially installing another map");
        MapsTable.MapInformations.Remove(1927);Call(feature,null,"PrepareMaps");
        string badTerrain=System.IO.Path.GetFullPath(System.IO.Path.Combine(runtime,"../../obj/chi-refill-validation/exploration-invalid"));
        string badMaps=System.IO.Path.Combine(badTerrain,"database/maps");System.IO.Directory.CreateDirectory(badMaps);
        foreach(int id in new[]{1762,1806})System.IO.File.Copy(System.IO.Path.Combine(runtime,"database/maps/"+id+".map"),System.IO.Path.Combine(badMaps,id+".map"),true);
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(badMaps,"1927.map"),new byte[]{0});rejected=false;
        try{Environment.CurrentDirectory=badTerrain;Call(feature,null,"PrepareMaps");}catch(InvalidOperationException){rejected=true;}
        finally{Environment.CurrentDirectory=runtime;}
        Check(rejected && MapsTable.MapInformations[1927].BaseID==1927,"unexpected terrain hash rejects startup without replacing installed metadata");
        var garden=World(runtime,1806);var grotto=World(runtime,1927);var first=World(runtime,1762);var desert=Kernel.Maps[1000];
        town.Npcs[79882]=new NpcSpawn{UID=79882,MapID=1002,X=319,Y=307};
        garden.Npcs[44100]=new NpcSpawn{UID=44100,MapID=1806,X=102,Y=105};
        desert.Npcs[27837]=new NpcSpawn{UID=27837,MapID=1000,X=472,Y=645};
        var flower=TravelPlayer(town,319,307);ulong money=flower.Entity.Money;uint cps=flower.Entity.ConquerPoints;
        ExplorationNpc(flower,79882,5);
        Check(!Fenced(flower) && flower.Entity.MapID==1806 && HasAchievement(flower,10808)
            && flower.Entity.Money==money && flower.Entity.ConquerPoints==cps && flower.Inventory.Count==0,"eligible ordinary player reaches Orchid Garden through Daily Quest Envoy without payment or invented quest rewards");
        flower.Entity.Teleport(107,109);
        Check(flower.Entity.MapID==1806,"same-map garden movement no longer falls back to Twin City for missing metadata");
        flower.Entity.X=102;flower.Entity.Y=105;ExplorationNpc(flower,44100,2);
        Check(flower.Entity.MapID==1002 && HasAchievement(flower,10808),"Magnolia keeper returns the player to Twin City while preserving the garden visit");
        var young=TravelPlayer(town,319,307);young.Entity.Level=(byte)(Config.DailyMagnoliaMinLevel-1);ExplorationNpc(young,79882,5);
        Check(young.Entity.MapID==1002 && !TravelEarned(young,10808),"garden admission preserves the configured minimum level");
        var exact=TravelPlayer(town,319,307);exact.Entity.Level=Config.DailyMagnoliaMinLevel;ExplorationNpc(exact,79882,5);
        Check(exact.Entity.MapID==1806 && HasAchievement(exact,10808),"garden admission accepts the exact configured minimum level");
        foreach(string state in new[]{"remote","trade","dead","fake","replaced"}) {
            var denied=TravelPlayer(town,319,307);
            if(state=="remote")denied.Entity.X=10;if(state=="trade")denied.Trade.InTrade=true;
            if(state=="dead")denied.Entity.Hitpoints=0;if(state=="fake")denied.Fake=true;
            if(state=="replaced")Kernel.GamePool.Remove(denied.Entity.UID);
            ExplorationNpc(denied,79882,5);
            Check(denied.Entity.MapID==1002 && !TravelEarned(denied,10808),"garden route rejects "+state+" request");
        }
        var missing=TravelPlayer(town,319,307);var saved=MapsTable.MapInformations[1806];MapsTable.MapInformations.Remove(1806);
        try{ExplorationNpc(missing,79882,5);}finally{MapsTable.MapInformations[1806]=saved;}
        Check(missing.Entity.MapID==1002 && !TravelEarned(missing,10808),"missing destination metadata blocks garden travel before changing player location");
        var ice=TravelPlayer(desert,472,645);ExplorationNpc(ice,27837,1);
        Check(ice.Entity.MapID==1762 && !Fenced(ice) && HasAchievement(ice,10801)
            && Scalar("SELECT map_id FROM revora_travel_visits WHERE uid=@uid AND achievement_id=10801",ice.Entity.UID)==1762,
            "Frozen Grotto Envoy earns the first-floor visit while retaining the actual server world in its receipt");
        typeof(Map).GetMethod("LoadPortals",Instance).Invoke(first,null);
        var passage=first.Portals.First(v=>v.DestinationMapID==1927);ice.Entity.X=passage.CurrentX;ice.Entity.Y=passage.CurrentY;
        GeneralData.UsePortal(new Data(true){UID=ice.Entity.UID},ice);
        Check(ice.Entity.MapID==1927 && HasAchievement(ice,10802),"installed first-to-second-floor portal earns the Frozen Grotto 2 visit");
        ice.Entity.Teleport(388,628);Check(ice.Entity.MapID==1927,"same-map Frozen Grotto 2 teleport retains the intended map");
        var direct=TravelPlayer(desert,472,645);ExplorationNpc(direct,27837,2);
        Check(direct.Entity.MapID==1927 && HasAchievement(direct,10802),"Envoy's second-floor option no longer sends players to Twin City");
        AdventureWalk(direct,370,365);
        var remote=TravelPlayer(desert,10,10);ExplorationNpc(remote,27837,2);
        Check(remote.Entity.MapID==1000 && !TravelEarned(remote,10802),"remote Envoy request cannot teleport or earn the destination");
        typeof(Map).GetMethod("LoadPortals",Instance).Invoke(grotto,null);
        var back=grotto.Portals.First(v=>v.DestinationMapID==1762);direct.Entity.X=back.CurrentX;direct.Entity.Y=back.CurrentY;
        GeneralData.UsePortal(new Data(true){UID=direct.Entity.UID},direct);
        var exit=first.Portals.First(v=>v.DestinationMapID==1000);direct.Entity.X=exit.CurrentX;direct.Entity.Y=exit.CurrentY;
        GeneralData.UsePortal(new Data(true){UID=direct.Entity.UID},direct);
        Check(direct.Entity.MapID==1000 && HasAchievement(direct,10802),"installed return portals bring the player back to Desert City with evidence intact");
        var copy=TravelAlias(runtime,62001,1762);var copiedVisitor=TravelPlayer(copy,334,354);
        Visit(copiedVisitor,copy,334,354);Check(Visits(copiedVisitor)==0,"arbitrary copy of first-floor terrain cannot earn its visit");
        flower.LastResetTime=DateTime.Now;Check(EntityTable.SaveEntity(flower),"garden traveller saves after returning home");
        var restored=TravelPlayer(town,319,307);uint placeholder=restored.Entity.UID;restored.Account.EntityID=flower.Entity.UID;
        restored.Account.Username="garden_travel_fixture";restored.Quests=new Quests(restored);
        Check((bool)Call(typeof(EntityTable),null,"LoadEntityState",restored),"production character loader restores garden traveller");
        Kernel.GamePool.Remove(placeholder);Kernel.GamePool[flower.Entity.UID]=restored;restored.Action=2;EvidenceRefresh(restored);
        Check(!Fenced(restored) && restored.Entity.MapID==1002 && HasAchievement(restored,10808) && !restored.Entity.MagniloQuest,
            "fresh session retains garden visit without falsely completing its flowering quest");
    }
}
