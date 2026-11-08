using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Interfaces;
using Revora.Network.GamePackets;
using Console=System.Console;
internal static partial class CheckCandidateArenaLifecycle {
    const string FreshStarterName="FreshRoadmap1";
    static GameState FreshStarterSession() {
        var p=new GameState(null);p.Fake=false;p.Socket.Alive=false;
        p.Account=new AccountTable(null){Username=FreshStarterName,State=AccountTable.AccountState.Player};
        p.Screen=(Screen)FormatterServices.GetUninitializedObject(typeof(Screen));GC.SuppressFinalize(p.Screen);p.Screen.Owner=p;
        typeof(Screen).GetField("_objects",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(p.Screen,new IMapObject[0]);
        typeof(Screen).GetField("_objectDictionary",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(p.Screen,new System.Collections.Concurrent.ConcurrentDictionary<uint,IMapObject>());
        foreach(var f in typeof(Screen).GetFields(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance))
            if(f.GetValue(p.Screen)==null && f.FieldType.IsGenericType && f.FieldType.GetConstructor(Type.EmptyTypes)!=null)f.SetValue(p.Screen,Activator.CreateInstance(f.FieldType));
        p.Inventory=new Revora.Game.ConquerStructures.Inventory(p);p.Equipment=new Revora.Game.ConquerStructures.Equipment(p);
        p.Warehouses=new SafeDictionary<Revora.Game.ConquerStructures.Warehouse.WarehouseID,Revora.Game.ConquerStructures.Warehouse>(20);
        p.Weapons=new Tuple<ConquerItem,ConquerItem>(null,null);p.ArenaStatistic=new QualifierStatistic(true);p.Variables=new VariableVault();
        p.Spells=new SafeDictionary<ushort,ISkill>();p.Proficiencies=new SafeDictionary<ushort,IProf>();
        p.Friends=new SafeDictionary<uint,Revora.Game.ConquerStructures.Society.Friend>();p.Enemy=new SafeDictionary<uint,Revora.Game.ConquerStructures.Society.Enemy>();
        p.ItemSyncRoot=new object();p.Trade=new Revora.Game.ConquerStructures.Trade();p.RunesItem=new SafeDictionary<uint,ConquerItem>();p.ChiData=new ChiTable.ChiData{Powers=p.ChiPowers};
        p.Action=1;return p;
    }
    static string FreshStarterSnapshot(GameState p) {return FreshStarterSnapshot(p.Entity.UID);}
    static string FreshStarterSnapshot(uint uid) {
        return Convert.ToString(Sql(@"SELECT json_build_object(
'profile',(SELECT json_build_object('uid',uid,'owner',owner,'name',name,'level',level,'class',class,'reborn',reborn,'mapid',mapid,'x',x,'y',y,'cps',conquerpoints,'bound',boundcps,'silver',money) FROM entities WHERE uid=@uid),
'items',(SELECT json_agg(i ORDER BY uid) FROM items i WHERE entityid=@uid),
'creation',(SELECT row_to_json(c) FROM revora_character_creation c WHERE character_uid=@uid),
'delivery',(SELECT row_to_json(d) FROM revora_custom_starter_items d WHERE uid=@uid),
'claims',(SELECT json_agg(r ORDER BY reborn,milestone) FROM revora_custom_starter_claims r WHERE uid=@uid))::text","uid",(long)uid));
    }
    static void FreshStarterProcess(string phase) {
        var p=FreshStarterSession();bool create=phase.EndsWith("create");
        if(create && (bool)Sql("SELECT to_regclass('revora_fresh_starter_probe') IS NULL")) {
            Require((long)Sql("SELECT count(*) FROM accounts WHERE lower(username)=lower(@name)","name",FreshStarterName)==0);
            p.Account.Password=Guid.NewGuid().ToString("N");p.Account.Insert();Require(p.Account.Password=="");
            string message="";Require(EntityTable.CreateEntity(new EnitityCreate{Name=FreshStarterName,Body=1003,Class=2},p,ref message) && message=="ANSWER_OK");
            Require(p.Entity.Level==1 && p.Entity.Class==10 && p.Entity.MapID==1002 && p.Entity.X==327 && p.Entity.Y==303);
            Sql("CREATE TABLE revora_fresh_starter_probe(uid bigint PRIMARY KEY,stage text NOT NULL,snapshot text);INSERT INTO revora_fresh_starter_probe VALUES(@uid,'created',NULL)","uid",(long)p.Entity.UID);
        } else {
            uint uid=Convert.ToUInt32(Sql("SELECT uid FROM revora_fresh_starter_probe"));p.Account.EntityID=uid;
            p.Entity=new Entity(EntityFlag.Player,false){Owner=p,UID=uid};
            Require((bool)Call(Feature("SessionInstall"),"TryClaim",uid,p));
        }
        p.Quests=new Quests(p);Require((bool)Call(typeof(EntityTable),"LoadEntityState",p));
        p.Action=2;p.Entity.FullyLoaded=true;p.BindWorldMap(p.Entity.MapID);ConquerItemTable.LoadItems(p);
        p.Entity.StorageItems=new Dictionary<uint,ConquerItem>();p.Entity.Titles=new System.Collections.Concurrent.ConcurrentDictionary<TitlePacket.Titles,DateTime>();
        p.InnerPower=new InnerPower(p.Entity.Name,p.Entity.UID);
        Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && p.Entity.Level==1 && p.Entity.Class==10 && p.Entity.Reborn==0);
        Require(p.Entity.MapID==1002 && p.Entity.X==327 && p.Entity.Y==303 && p.Entity.ConquerPoints==10000 && !PirateFenced(p));
        if(create) {
            Require(Convert.ToString(Sql("SELECT stage FROM revora_fresh_starter_probe"))=="created");
            Console.WriteLine("[fresh-starter] before-delivery items="+p.Inventory.Count+" bound="+p.Entity.BoundCps+" silver="+p.Entity.Money);
            Require(p.Inventory.Count==0 && p.Entity.BoundCps==0 && p.Entity.Money==100000);
            Call(Feature("RevoraStarterPack"),"OnLogin",p);
            Require(p.Inventory.Count==1 && p.Inventory.Objects.Single().ID==727990 && p.Inventory.Objects.Single().Bound);
            string once=FreshStarterSnapshot(p);Call(Feature("RevoraStarterPack"),"OnLogin",p);Require(once==FreshStarterSnapshot(p));
            Sql("UPDATE revora_fresh_starter_probe SET stage='delivered',snapshot=@snapshot","snapshot",once);
        } else {
            Require(FreshStarterSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_starter_probe")));
            if(phase.EndsWith("open")) {
                Require(Convert.ToString(Sql("SELECT stage FROM revora_fresh_starter_probe"))=="delivered");
                ItemHandler.UseItem(p.Inventory.Objects.Single(i=>i.ID==727990),p);
                Require(p.Inventory.Count==8 && p.Entity.BoundCps==50000 && p.Entity.Money==1100000 && !PirateFenced(p));
                var gear=p.Inventory.Objects.Where(i=>i.ID!=727990).ToArray();
                Require(gear.Select(i=>i.ID).OrderBy(i=>i).SequenceEqual(new uint[]{118009,130009,120009,150009,160019,410009,420009}.OrderBy(i=>i)));
                Require(gear.All(i=>i.Bound && i.Plus==12 && i.Bless==7 && i.SocketOne==Enums.Gem.SuperDragonGem && i.SocketTwo==Enums.Gem.SuperDragonGem));
                Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_fresh_starter_probe SET stage='opened',snapshot=@snapshot","snapshot",FreshStarterSnapshot(p));
            }
            Require(p.Inventory.Count==8 && p.Entity.BoundCps==50000 && p.Entity.Money==1100000);
            string once=FreshStarterSnapshot(p);Call(Feature("RevoraStarterPack"),"OnLogin",p);ItemHandler.UseItem(p.Inventory.Objects.Single(i=>i.ID==727990),p);
            Require(!PirateFenced(p) && once==FreshStarterSnapshot(p));
            Require((long)Sql("SELECT count(*) FROM revora_custom_starter_claims WHERE uid=@uid AND reborn=0 AND milestone=1","uid",(long)p.Entity.UID)==1);
        }
        Console.WriteLine("[career-process] "+phase+"-passed uid="+p.Entity.UID+" level="+p.Entity.Level+" class="+p.Entity.Class+" items="+p.Inventory.Count);
        Server.CommandsAI("@close");throw new InvalidOperationException("Fresh starter did not close.");
    }
}
