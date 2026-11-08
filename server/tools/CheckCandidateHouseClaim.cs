using System;
using System.Linq;
using Revora;
using Revora.Game;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
internal static partial class CheckCandidateArenaLifecycle
{
    static bool houseClaimFixture;
    static string HouseClaimSnapshot(GameState p){return Convert.ToString(Sql(@"SELECT json_build_object(
'base',@snapshot::json,'house',(SELECT row_to_json(h) FROM house h WHERE id=@uid),
'builds',(SELECT json_agg(b ORDER BY level) FROM revora_house_builds b WHERE uid=@uid),
'uses',(SELECT json_agg(u ORDER BY id) FROM revora_fine_furniture_pack_uses u WHERE uid=@uid),
'placements',(SELECT json_agg(f ORDER BY actor) FROM revora_house_furniture f WHERE uid=@uid),
'actors',(SELECT json_agg(f ORDER BY id) FROM furniture f WHERE mapid=(SELECT map_id FROM revora_house_maps WHERE uid=@uid)))::text",
        "snapshot",NobilityClaimSnapshot(p),"uid",(long)p.Entity.UID));}
    static void HouseClaimProcess(bool reload){
        houseClaimFixture=true;const uint uid=4294911801;
        if((bool)Sql("SELECT to_regclass('revora_house_claim_probe') IS NULL")){
            Require(!reload && (long)Sql("SELECT count(*) FROM entities WHERE uid=@uid","uid",(long)uid)==0);
            nextUid=uid;Player(Kernel.Maps[1002]);Sql("UPDATE entities SET level=130,reborn=2,money=1000000,conquerpoints=100000 WHERE uid=@uid","uid",(long)uid);
            Sql("CREATE TABLE revora_house_claim_probe(snapshot text);INSERT INTO revora_house_claim_probe VALUES(NULL)");
        }
        nextUid=uid;var p=Player(Kernel.Maps[1002],true);ChiTable.Load(p);WayofHeroes.Load(p);
        Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        if(Sql("SELECT snapshot FROM revora_house_claim_probe")==DBNull.Value){
            Require(!reload);p.Entity.Teleport(1002,400,350);
            if((long)Sql("SELECT count(*) FROM house WHERE id=@uid","uid",(long)uid)==0){
                string before=HouseClaimSnapshot(p);NobilityClaimPacket(p,704);Require(before==HouseClaimSnapshot(p));
                uint cp=p.Entity.ConquerPoints,price=(uint)Call(Feature("Housing"),"Price",1);ulong money=p.Entity.Money;
                HeartTalk(p,30156,5);HeartTalk(p,30156,40);Require(p.Inventory.Objects.Single().ID==721170 && p.Entity.ConquerPoints==cp-price);
                string bought=HouseClaimSnapshot(p);NobilityClaimPacket(p,704);Require(bought==HouseClaimSnapshot(p));
                HeartTalk(p,30156,1);HeartTalk(p,30156,40);Require(p.Inventory.Count==0 && p.Entity.Money==money-300000);
            }
            System.Threading.Tasks.Parallel.For(0,8,_=>NobilityClaimPacket(p,704));
            if(Convert.ToInt32(Sql("SELECT houselevel FROM house WHERE id=@uid","uid",(long)uid))==1){
                HeartTalk(p,30156,6);HeartTalk(p,30156,40);HeartTalk(p,30156,3);HeartTalk(p,30156,40);
                Require(Convert.ToInt32(Sql("SELECT houselevel FROM house WHERE id=@uid","uid",(long)uid))==2);
            }
            var feature=Feature("GemDragonSoulPacks");var roll=feature.GetField("Roll",Hidden);object old=roll.GetValue(null);
            Require(((uint[])Call(feature,"Pool",3005131u)).SequenceEqual(Enumerable.Range(721177,12).Select(i=>(uint)i)));
            try{
                for(int pick=0;pick<12;pick++){
                    uint type=721177u+(uint)pick;
                    if((long)Sql("SELECT count(*) FROM revora_house_furniture WHERE uid=@uid AND item_type=@type","uid",(long)uid,"type",(int)type)>0)continue;
                    var furniture=p.Inventory.Objects.SingleOrDefault(i=>i.ID==type);
                    if(furniture==null){
                    var pack=p.Inventory.Objects.SingleOrDefault(i=>i.ID==3005131)??RefineryClaimGive(p,3005131);
                    if(pick==0){Require(pack.Bound);string before=HouseClaimSnapshot(p);roll.SetValue(null,(Func<int,int,int>)((min,max)=>max));
                        RetainedUpgradeUsage(p,pack.UID,0,ItemUsage.Mode.EquipItem);Require(before==HouseClaimSnapshot(p) && !PirateFenced(p));}
                    int selected=pick;roll.SetValue(null,(Func<int,int,int>)((min,max)=>selected));
                    uint packUid=pack.UID;System.Threading.Tasks.Parallel.For(0,8,_=>RetainedUpgradeUsage(p,packUid,0,ItemUsage.Mode.EquipItem));
                    furniture=p.Inventory.Objects.Single();Require(furniture.ID==type && furniture.Bound && furniture.Days==0 && furniture.StackSize==1);
                    string used=HouseClaimSnapshot(p);RetainedUpgradeUsage(p,packUid,0,ItemUsage.Mode.EquipItem);Require(used==HouseClaimSnapshot(p));
                    }
                    if(p.Entity.MapID==1002)HeartTalk(p,30156,2);
                    RetainedUpgradeUsage(p,furniture.UID,0,ItemUsage.Mode.EquipItem);var frame=CareerFurniturePosition(p,(ushort)((type-721176)*10));
                    Revora.Network.PacketHandler.HandlePacket(frame,p);Require(!p.Inventory.ContainsUID(furniture.UID) && !PirateFenced(p));
                    string placed=HouseClaimSnapshot(p);Revora.Network.PacketHandler.HandlePacket(frame,p);Require(placed==HouseClaimSnapshot(p));
                    if(pick<11){
                        uint actor=Convert.ToUInt32(Sql("SELECT actor FROM revora_house_furniture WHERE uid=@uid AND item_type=@type AND removed_at IS NULL","uid",(long)uid,"type",(int)type));
                        var body=new System.Collections.Generic.List<byte>{8,6,16};uint v=actor;
                        while(v>=128){body.Add((byte)(v|128));v>>=7;}body.Add((byte)v);body.AddRange(new byte[]{24,0,32,0});
                        var removal=new byte[body.Count+12];body.CopyTo(removal,4);Revora.Network.Writer.WriteUInt16((ushort)(removal.Length-8),0,removal);Revora.Network.Writer.WriteUInt16(2031,2,removal);Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,removal.Length-8,removal);
                        Revora.Network.PacketHandler.HandlePacket(removal,p);Require(!p.Map.Npcs.ContainsKey(actor));
                        string removed=HouseClaimSnapshot(p);Revora.Network.PacketHandler.HandlePacket(removal,p);Require(removed==HouseClaimSnapshot(p));
                    }
                }
            }finally{roll.SetValue(null,old);}
            p.Entity.Teleport(1002,400,350);Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_house_claim_probe SET snapshot=@snapshot","snapshot",HouseClaimSnapshot(p));
        }
        Require(p.Inventory.Count==0 && !PirateFenced(p));
        Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=704 AND NOT legacy","uid",(long)uid)==1);
        Require((long)Sql("SELECT count(*) FROM revora_fine_furniture_pack_uses WHERE uid=@uid","uid",(long)uid)==12);
        Require((long)Sql("SELECT count(*) FROM revora_house_furniture WHERE uid=@uid AND bound","uid",(long)uid)==12);
        var home=(Map)Call(Feature("Housing"),"Load",p);Require(home!=null && home.Npcs.Values.Count(n=>n.Type==Enums.NpcType.Furniture)==1);
        string exact=HouseClaimSnapshot(p);Require(exact==Convert.ToString(Sql("SELECT snapshot FROM revora_house_claim_probe")));
        NobilityClaimPacket(p,704);Require(exact==HouseClaimSnapshot(p));
        System.Console.WriteLine("[career-process] career-house-claim-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("House claim did not close.");
    }
}
