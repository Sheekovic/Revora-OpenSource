using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Npgsql;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Interfaces;
using Revora.Network.GamePackets;
using Tournament=Revora.Sistemas.Quarantine;
internal static partial class LedgerTests {
    // Offline world/player setup follows the existing CompanionAI and PotencyHunt fixtures.
    private static uint nextUid=1260000;
    private static GameState WorldPlayer(Map map, bool fake, ushort x, ushort y, byte cls)
    {
        GameState state = new GameState(null);
        state.Account = new AccountTable(null);
        state.Account.EntityID = nextUid++;
        // Do not register production pet/screen timers in an offline fixture.
        state.Screen = (Screen)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Screen));
        GC.SuppressFinalize(state.Screen);
        state.Screen.Owner = state;
        typeof(Screen).GetField("_objects", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(state.Screen, new IMapObject[0]);
        typeof(Screen).GetField("_objectDictionary", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(state.Screen,
            new System.Collections.Concurrent.ConcurrentDictionary<uint, IMapObject>());
        state.Equipment = new Revora.Game.ConquerStructures.Equipment(state);
        state.Inventory = new Revora.Game.ConquerStructures.Inventory(state);
        state.Weapons = new Tuple<ConquerItem, ConquerItem>(null, null);
        state.ArenaStatistic = new QualifierStatistic(true);
        state.Variables = new VariableVault();
        state.Entity = new Entity(EntityFlag.Player, false);
        state.Entity.Owner = state;
        state.Entity.SendUpdates = false;
        state.Entity.UID = state.Account.EntityID;
        state.Entity.Name = fake ? "[Adv] Fixture" + state.Entity.UID : "Player" + state.Entity.UID;
        state.Entity.Body = 1003;
        state.Entity.Level = 140;
        state.Entity.Class = cls;
        state.Entity.MaxHitpoints = state.Entity.Hitpoints = 10000;
        state.Entity.MaxMana = state.Entity.Mana = 800;
        state.Entity.MinAttack = 1000;
        state.Entity.MaxAttack = 1200;
        state.Entity.MagicAttack = 1200;
        state.Entity.Stamina = 100;
        state.Entity.X = x; state.Entity.Y = y;
        state.Spells = new SafeDictionary<ushort, ISkill>();
        state.Proficiencies = new SafeDictionary<ushort, IProf>();
        state.Friends = new SafeDictionary<uint, Revora.Game.ConquerStructures.Society.Friend>();
        state.Enemy = new SafeDictionary<uint, Revora.Game.ConquerStructures.Society.Enemy>();
        state.BindWorldMap(map.ID);
        state.Socket.Alive = false;
        state.Fake = fake;
        if(!fake && Environment.GetEnvironmentVariable("REVORA_TEST_HEALTH_LEASE")=="1")
            typeof(Entity).GetField("HealthSynchronizationRequired",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(state.Entity,true);
        Kernel.GamePool[state.Entity.UID] = state;
        return state;
    }
    static Map TournamentMap(string runtime,ushort id) {
        var map=(Map)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Map));
        map.ID=map.BaseID=id;map.Entities=new System.Collections.Concurrent.SafeConcurrentDictionary<uint,Entity>();map.Companions=new System.Collections.Concurrent.SafeConcurrentDictionary<uint, Entity>();Kernel.Maps[id]=map;
        using(var r=new BinaryReader(File.OpenRead(Path.Combine(runtime,"database/maps/"+id+".map")))){
            int w=r.ReadInt32(),h=r.ReadInt32();map.Floor=new Floor(w,h,id);
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)map.Floor[x,y,MapObjectType.InvalidCast]=r.ReadByte()!=1;
        }
        map.Npcs=new Dictionary<uint,INpc>();map.Zones=new List<Zoning.Zone>();map.FloorItems=new ConcurrentDictionary<uint,FloorItem>();map.EntityUIDCounter=new Counter(400000);
        foreach(var f in typeof(Map).GetFields(BindingFlags.Public|BindingFlags.Instance))
            if(f.GetValue(map)==null && f.FieldType.IsGenericType && f.FieldType.GetConstructor(Type.EmptyTypes)!=null)f.SetValue(map,Activator.CreateInstance(f.FieldType));
        MapsTable.MapInformations[id]=new MapsTable.MapInformation{ID=id,BaseID=id,Status=2};return map;
    }
    static GameState TournamentPlayer(Map town) {
        var p=WorldPlayer(town,false,275,140,15);
        foreach(var f in typeof(Screen).GetFields(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance))
            if(f.GetValue(p.Screen)==null && f.FieldType.IsGenericType && f.FieldType.GetConstructor(Type.EmptyTypes)!=null)f.SetValue(p.Screen,Activator.CreateInstance(f.FieldType));
        p.Entity.Vitality=100;p.Entity.Strength=100;p.Entity.Agility=50;p.Entity.Spirit=50;
        p.Action=2;p.Account.State=AccountTable.AccountState.Player;p.Trade=new Revora.Game.ConquerStructures.Trade();
        p.InnerPower=new InnerPower(p.Entity.Name,p.Entity.UID);p.RunesItem=new SafeDictionary<uint,ConquerItem>();p.ChiData=new ChiTable.ChiData{Powers=p.ChiPowers};p.Entity.FullyLoaded=true;
        using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new NpgsqlCommand("INSERT INTO entities(uid,name,conquerpoints,revorapoint,money) VALUES(@uid,@name,0,0,0)",c)){q.Parameters.AddWithValue("uid",(long)p.Entity.UID);q.Parameters.AddWithValue("name",p.Entity.Name);q.ExecuteNonQuery();}}
        var armor=new ConquerItem(true){UID=ConquerItem.ItemUID.Next,ID=130009,Position=3,Durability=1000,MaximDurability=1000};WorldValue("INSERT INTO items(uid,id,entityid,position,warehouse) VALUES(@item,130009,@uid,3,0)","item",(long)armor.UID,"uid",(long)p.Entity.UID);
        p.Equipment.Add(armor,Enums.ItemUse.None);
        p.ActiveNpc=Tournament.NpcId;p.Entity.PKMode=Enums.PKMode.Capture;return p;
    }
    static object WorldValue(string sql,params object[] parameters) {
        using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new NpgsqlCommand(sql,c)){
            for(int i=0;i<parameters.Length;i+=2)q.Parameters.AddWithValue((string)parameters[i],parameters[i+1]);return q.ExecuteScalar();
        }}
    }
    static void VerifyTournamentWorld() {
        const string runtime=@"C:\Revora\DisposablePostgres\moon-box-candidate\game";
        string prior=Environment.CurrentDirectory;
        try {
            Environment.CurrentDirectory=runtime;
            Sql("DROP TABLE entities;CREATE TABLE entities AS TABLE public.entities WITH NO DATA;CREATE TABLE IF NOT EXISTS skills AS TABLE public.skills WITH NO DATA;CREATE TABLE IF NOT EXISTS arena AS TABLE public.arena WITH NO DATA;CREATE TABLE titles (LIKE public.titles INCLUDING ALL);CREATE TABLE status (LIKE public.status INCLUDING ALL);CREATE TABLE chi (LIKE public.chi INCLUDING ALL);");
            ConquerItemInformation.Load();StorageManager.Load();StorageItem.Load();PerfectionManager.AbilityScore.Read(out PerfectionManager.AbilitiesScore);
            Kernel.GamePool.Clear();Kernel.Maps.Clear();
            var town=TournamentMap(runtime,1002);TournamentMap(runtime,10360);
            Call("PlaceNpc");Call("PlaceNpc");Check(town.Floor[275,140,MapObjectType.Player] && town.Npcs.Count(n=>n.Key==Tournament.NpcId)==1,"tournament clerk placement is idempotent on real terrain");
            var a=TournamentPlayer(town);var b=TournamentPlayer(town);
            var original=new ConquerItem(true){UID=ConquerItem.ItemUID.Next,ID=187405,Position=9,Durability=1000,MaximDurability=1000};
            WorldValue("INSERT INTO items(uid,id,entityid,position,warehouse) VALUES(@item,187405,@uid,9,0)","item",(long)original.UID,"uid",(long)a.Entity.UID);
            a.Equipment.Add(original,Enums.ItemUse.None);
            Server.GamePool=new[]{a,b};Tournament.Start();var round=(Guid)typeof(Tournament).GetField("roundId",Hidden).GetValue(null);
            Npcs.GetDialog(new NpcRequest(true){NpcID=Tournament.NpcId,OptionID=1},a);
            Npcs.GetDialog(new NpcRequest(true){NpcID=Tournament.NpcId,OptionID=1},b);
            Check(a.Entity.MapID==10360 && b.Entity.MapID==10360 && Tournament.White.Count==1 && Tournament.Black.Count==1,"ordinary NPC admissions reach opposing teams in the real arena");
            var winner=Tournament.White.Values.Single();var loser=Tournament.Black.Values.Single();
            Check(winner.Account.State==AccountTable.AccountState.Player && loser.Account.State==AccountTable.AccountState.Player,"tournament entrants are ordinary players");
            for(int hit=0;hit<100;hit++)if(!Tournament.HandleQuarantine(winner.Entity,loser.Entity,new SpellInformation{ID=1045}))throw new InvalidOperationException("world hit rejected at "+hit+" hp="+winner.Entity.Hitpoints+","+loser.Entity.Hitpoints+" action="+winner.Action+","+loser.Action+" map="+winner.Entity.MapID+","+loser.Entity.MapID+" uniforms="+winner.Equipment.Objects[8].ID+","+loser.Equipment.Objects[8].ID);
            Check(!Tournament.Started && winner.Entity.MapID==1002 && loser.Entity.MapID==1002,"hundredth accepted hit settles and returns both players");
            Check(winner.Entity.ConquerPoints==300000000 && winner.Entity.RevoraPoint==50000 && winner.Entity.Money==100000000000 && loser.Entity.ConquerPoints==0,"only the winning ordinary player receives the currency purse");
            Check(winner.Inventory.Objects.Count(i=>i.Bound && ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7)==1,"winner receives one bound catalog P7 soul");
            Check(winner.Entity.PKMode==Enums.PKMode.Capture && loser.Entity.PKMode==Enums.PKMode.Capture && ReferenceEquals(a.Equipment.Objects[8],original) && b.Equipment.Objects[8]==null,"actual return restores PK mode and original garment while removing both uniforms");
            Check(Convert.ToInt64(WorldValue("SELECT count(*) FROM revora_quarantine_hits WHERE round_id=@round", "round",round))==100 && Convert.ToInt64(WorldValue("SELECT count(*) FROM revora_quarantine_admissions WHERE round_id=@round AND entered AND ended AND returned","round",round))==2,"all accepted world hits and both completed returns persist");
            var soul=winner.Inventory.Objects.Single(i=>i.Bound && ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7);
            Check(Convert.ToInt64(WorldValue("SELECT count(*) FROM revora_quarantine_purses p JOIN items i ON i.uid=p.soul_uid JOIN entities e ON e.uid=p.uid WHERE p.round_id=@round AND p.uid=@uid AND p.claimed_at IS NOT NULL AND i.uid=@item AND i.entityid=e.uid AND i.bound=1 AND e.conquerpoints=300000000 AND e.revorapoint=50000 AND e.money=100000000000","round",round,"uid",(long)winner.Entity.UID,"item",(long)soul.UID))==1,"world winner receipt, bound item and all saved balances agree");
            Check(Convert.ToInt64(WorldValue("SELECT count(*) FROM items WHERE uid=@item AND entityid=@uid AND position=9","item",(long)original.UID,"uid",(long)a.Entity.UID))==1 && Convert.ToInt64(WorldValue("SELECT count(*) FROM items i JOIN revora_quarantine_admissions a ON a.uniform_uid=i.uid WHERE a.round_id=@round","round",round))==0,"original garment remains saved and no issued uniform survives");
            Sql("CREATE TABLE IF NOT EXISTS itemadding (LIKE public.itemadding INCLUDING ALL)");
            foreach(var item in winner.Inventory.Objects)winner.Inventory.Remove(item.UID,Enums.ItemUse.None,false);
            winner.Equipment=new Revora.Game.ConquerStructures.Equipment(winner);
            ConquerItemTable.LoadItems(winner);
            Check(winner.Inventory.Objects.Count(i=>i.UID==soul.UID && i.ID==soul.ID && i.Bound && i.Days==0)==1,"production inventory loader restores the exact earned permanent bound soul");
            foreach(string table in new[]{"teamarena","championarena","quests","reward","rewardclameid","merchant","dialyquests","activeness","house","pkexploits","achievement"}) {
                // Fixed, source-controlled fixture table names; no player input.
                Sql("CREATE TABLE IF NOT EXISTS "+table+" AS TABLE public."+table+" WITH NO DATA");
            }
            Sql("CREATE TABLE IF NOT EXISTS prizes(uid bigint PRIMARY KEY,name text,prizes bytea)");
            winner.Entity.DailySignDays=new byte[31];
            Check(EntityTable.SaveEntity(winner),"earned winner profile saves before reconnect");
            var fresh=WorldPlayer(town,false,327,303,15);uint placeholder=fresh.Entity.UID;
            foreach(var f in typeof(Screen).GetFields(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance))
                if(f.GetValue(fresh.Screen)==null && f.FieldType.IsGenericType && f.FieldType.GetConstructor(Type.EmptyTypes)!=null)f.SetValue(fresh.Screen,Activator.CreateInstance(f.FieldType));
            fresh.Trade=new Revora.Game.ConquerStructures.Trade();
            fresh.Account.EntityID=winner.Entity.UID;fresh.Account.State=AccountTable.AccountState.Player;fresh.Account.Username="quarantine_world_fixture";fresh.Quests=new Quests(fresh);
            Check((bool)typeof(EntityTable).GetMethod("LoadEntityState",Hidden).Invoke(null,new object[]{fresh}),"production entity state loader restores the winning character");
            Kernel.GamePool.Remove(placeholder);Kernel.GamePool[winner.Entity.UID]=fresh;fresh.Action=2;fresh.Entity.FullyLoaded=true;
            fresh.InnerPower=new InnerPower(fresh.Entity.Name,fresh.Entity.UID);fresh.RunesItem=new SafeDictionary<uint,ConquerItem>();fresh.ChiData=new ChiTable.ChiData{Powers=fresh.ChiPowers};
            ConquerItemTable.LoadItems(fresh);
            Check(fresh.Entity.ConquerPoints==300000000 && fresh.Entity.RevoraPoint==50000 && fresh.Entity.Money==100000000000 && fresh.Inventory.Objects.Count(i=>i.UID==soul.UID && i.ID==soul.ID && i.Bound && i.Days==0)==1,"fresh character and inventory loaders retain earned balances and bound soul");
            winner=fresh;
            Claim(winner);Tournament.End();
            Check(winner.Inventory.Objects.Count(i=>i.UID==soul.UID)==1 && winner.Entity.ConquerPoints==300000000 && Convert.ToInt64(WorldValue("SELECT count(*) FROM revora_quarantine_purses WHERE round_id=@round","round",round))==1,"world completion and reward replay cannot duplicate the purse");
            var fillers=new List<ConquerItem>();
            while(winner.Inventory.Count<40){
                var item=new ConquerItem(true){UID=ConquerItem.ItemUID.Next,ID=1088001,StackSize=1};
                WorldValue("INSERT INTO items(uid,id,entityid,position,warehouse,stacksize) VALUES(@item,1088001,@uid,0,0,1)","item",(long)item.UID,"uid",(long)winner.Entity.UID);
                winner.Inventory.Add(item,Enums.ItemUse.None);fillers.Add(item);
            }
            a.Entity.Teleport(1002,275,140);winner.Entity.Teleport(1002,275,140);a.ActiveNpc=winner.ActiveNpc=Tournament.NpcId;
            Server.GamePool=new[]{a,winner};Tournament.Start();var fullRound=(Guid)typeof(Tournament).GetField("roundId",Hidden).GetValue(null);
            Npcs.GetDialog(new NpcRequest(true){NpcID=Tournament.NpcId,OptionID=1},a);
            Npcs.GetDialog(new NpcRequest(true){NpcID=Tournament.NpcId,OptionID=1},winner);
            Check(a.Entity.MapID==10360 && winner.Entity.MapID==10360 && winner.Inventory.Count==40,"full bag without an original garment can enter without item loss");
            for(int hit=0;hit<100;hit++)if(!Tournament.HandleQuarantine(winner.Entity,a.Entity,new SpellInformation{ID=1045}))throw new InvalidOperationException("full-bag world hit rejected at "+hit);
            Check(winner.Entity.MapID==1002 && winner.Inventory.Count==40 && winner.Entity.ConquerPoints==300000000 && Convert.ToInt64(WorldValue("SELECT count(*) FROM revora_quarantine_purses WHERE round_id=@round AND claimed_at IS NULL","round",fullRound))==1,"earned full-bag winner returns safely with the entire second purse pending");
            var removed=fillers[0];WorldValue("DELETE FROM items WHERE uid=@item AND entityid=@uid","item",(long)removed.UID,"uid",(long)winner.Entity.UID);winner.Inventory.Remove(removed.UID,Enums.ItemUse.None,false);
            Claim(winner);Claim(winner);
            Check(winner.Inventory.Count==40 && winner.Entity.ConquerPoints==600000000 && winner.Entity.RevoraPoint==100000 && winner.Entity.Money==200000000000 && winner.Inventory.Objects.Count(i=>i.Bound && ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7)==2,"one released bag slot delivers the saved second purse exactly once");

            var rejected=TournamentPlayer(town);
            var retained=new ConquerItem(true){UID=ConquerItem.ItemUID.Next,ID=187405,Position=9,Durability=1000,MaximDurability=1000};
            WorldValue("INSERT INTO items(uid,id,entityid,position,warehouse) VALUES(@item,187405,@uid,9,0)","item",(long)retained.UID,"uid",(long)rejected.Entity.UID);
            rejected.Equipment.Add(retained,Enums.ItemUse.None);
            Server.GamePool=new[]{rejected};Tournament.Start();var unavailableRound=(Guid)typeof(Tournament).GetField("roundId",Hidden).GetValue(null);
            var arena=Kernel.Maps[10360];var arenaInfo=MapsTable.MapInformations[10360];
            Kernel.Maps.Remove(10360);MapsTable.MapInformations.Remove(10360);
            try {Npcs.GetDialog(new NpcRequest(true){NpcID=Tournament.NpcId,OptionID=1},rejected);}
            finally {Kernel.Maps[10360]=arena;MapsTable.MapInformations[10360]=arenaInfo;}
            Check(rejected.Entity.MapID==1002 && rejected.Entity.X==327 && rejected.Entity.Y==303 && Tournament.White.Count==0 && Tournament.Black.Count==0,"unavailable arena cancels entry without leaving a team participant in town");
            Check(rejected.Entity.PKMode==Enums.PKMode.Capture && ReferenceEquals(rejected.Equipment.Objects[8],retained),"failed arena travel immediately restores original garment and PK mode");
            Check(Convert.ToInt64(WorldValue("SELECT count(*) FROM revora_quarantine_admissions a WHERE round_id=@round AND uid=@uid AND NOT entered AND ended AND returned AND NOT EXISTS(SELECT 1 FROM items i WHERE i.uid=a.uniform_uid)","round",unavailableRound,"uid",(long)rejected.Entity.UID))==1,"redirected admission persists its completed return without issuing a uniform");
            rejected.Entity.Teleport(1002,275,140);rejected.ActiveNpc=Tournament.NpcId;
            Npcs.GetDialog(new NpcRequest(true){NpcID=Tournament.NpcId,OptionID=1},rejected);
            Check(rejected.Entity.MapID==1002 && Tournament.White.Count==0 && Tournament.Black.Count==0 && Convert.ToInt64(WorldValue("SELECT count(*) FROM revora_quarantine_admissions WHERE round_id=@round AND uid=@uid","round",unavailableRound,"uid",(long)rejected.Entity.UID))==1,"repeated cancelled admission remains one receipt without a duplicate-key failure");
            Tournament.End();
            Check(Convert.ToInt64(WorldValue("SELECT count(*) FROM revora_quarantine_purses WHERE round_id=@round","round",unavailableRound))==0,"cancelled arena entry cannot receive a winner purse");

        } finally {Environment.CurrentDirectory=prior;}
    }
}
