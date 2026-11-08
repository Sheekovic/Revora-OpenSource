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
    static bool snowExpiryFixture;
    static string SnowExpirySnapshot(GameState p) {
        return Convert.ToString(Sql(@"SELECT json_build_object(
'items',(SELECT json_agg(r ORDER BY uid) FROM items r WHERE entityid=@uid),
'loot',(SELECT json_agg(r ORDER BY slot) FROM revora_snow_banshee_loot r WHERE run=(SELECT run FROM revora_snow_expiry_probe)),
'study',(SELECT json_agg(r ORDER BY run) FROM revora_snow_banshee_study r WHERE uid=@uid),
'purse',(SELECT json_build_array(conquerpoints,money,revorapoint,studypoints) FROM entities WHERE uid=@uid))::text","uid",(long)p.Entity.UID));
    }
    static void SnowExpiryProcess(bool reload) {
        snowExpiryFixture=true;const uint uid=4294918601;
        var now=new DateTime(2041,1,6,5,0,0,DateTimeKind.Utc);
        var store=Feature("SnowBansheeStore");var encounter=Feature("SnowBansheeEncounter");var rewards=Feature("SnowBansheeRewards");
        store.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));
        if((bool)Sql("SELECT to_regclass('revora_snow_expiry_probe') IS NULL")) {
            Require(!reload && (long)Sql("SELECT count(*) FROM entities WHERE uid=@uid","uid",(long)uid)==0);
            nextUid=uid;Player(Kernel.Maps[1002]);
            Sql("CREATE TABLE revora_snow_expiry_probe(run uuid,snapshot text);INSERT INTO revora_snow_expiry_probe VALUES(NULL,NULL)");
        }
        nextUid=uid;var p=Player(Kernel.Maps[1002],true);SubClassTable.Load(p.Entity);p.LoadItemStats();Server.GamePool=new[]{p};
        Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        p.Entity.Teleport(1927,370,366);
        if(Sql("SELECT snapshot FROM revora_snow_expiry_probe")==DBNull.Value) {
            Require(!reload && p.Inventory.Count==0);Call(encounter,"Tick");var actor=encounter.GetField("Active",Hidden).GetValue(null);Require(actor!=null);
            var boss=(Entity)actor.GetType().GetField("Body",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).GetValue(actor);
            p.Entity.X=boss.X;p.Entity.Y=(ushort)(boss.Y+1);
            Sql("UPDATE revora_snow_expiry_probe SET run=(SELECT id FROM revora_snow_banshee_runs WHERE state='active')");
            rewards.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(max=>max==100?70:0));
            // Supplied lethal callback isolates reward lifetime; this is not earned combat evidence.
            Require((bool)Call(encounter,"TryDie",boss,p.Entity));Call(rewards,"Tick",true);
            Require((long)Sql("SELECT count(*) FROM revora_snow_banshee_loot WHERE run=(SELECT run FROM revora_snow_expiry_probe) AND owner=@uid AND collector IS NULL","uid",(long)uid)==3);
            var drops=p.Map.FloorItems.Values.Where(d=>d.OwnerUID==uid && (bool)Call(rewards,"Managed",d)).ToArray();Require(drops.Length==3);
            now=now.AddMinutes(2).AddTicks(-1);
            Require(drops.All(d=>!(bool)Call(rewards,"Expired",d)));
            now=now.AddTicks(1);Require(drops.All(d=>(bool)Call(rewards,"Expired",d)));
            string before=SnowExpirySnapshot(p);
            foreach(var drop in drops) {
                p.Entity.X=drop.X;p.Entity.Y=drop.Y;
                var frame=new FloorItem(true){UID=drop.UID,Type=FloorItem.Remove}.ToArray();
                Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,frame.Length-8,frame);
                FloorItem.Process(frame,p);Require((bool)Call(rewards,"Pickup",p,drop));
            }
            Require(before==SnowExpirySnapshot(p) && !PirateFenced(p));Call(rewards,"Tick",true);
            Require(drops.All(d=>!p.Map.FloorItems.ContainsKey(d.UID)));
            foreach(var drop in drops)Require((bool)Call(rewards,"Pickup",p,drop));
            Require(before==SnowExpirySnapshot(p));Require(EntityTable.SaveEntity(p));
            Sql("UPDATE revora_snow_expiry_probe SET snapshot=@snapshot","snapshot",SnowExpirySnapshot(p));
        } else {
            Require(reload);now=now.AddMinutes(2);
            // Startup uses the real host clock. Clear that projection, then reconstruct at the saved deadline.
            Call(rewards,"ClearGround");string before=SnowExpirySnapshot(p);
            Call(rewards,"Tick",true);Call(rewards,"Tick",true);
            Require(before==SnowExpirySnapshot(p));
        }
        Require(!p.Map.FloorItems.Values.Any(d=>d.OwnerUID==uid && (bool)Call(rewards,"Managed",d)));
        Require(p.Inventory.Count==0 && p.Entity.SubClasses.StudyPoints==100 && !PirateFenced(p));
        Require((long)Sql("SELECT count(*) FROM revora_snow_banshee_loot WHERE run=(SELECT run FROM revora_snow_expiry_probe) AND collector IS NULL AND item_uid IS NULL")==3);
        Require(SnowExpirySnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_snow_expiry_probe")));
        Console.WriteLine("[career-process] career-snow-expiry-"+(reload?"reload":"earn")+"-passed");
        Server.CommandsAI("@close");throw new InvalidOperationException("Snow expiry fixture did not close.");
    }
}
