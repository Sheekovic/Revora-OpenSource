using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
using Console=System.Console;
internal static partial class CheckCandidateArenaLifecycle {
    static string FreshFurnitureSnapshot(GameState p){return Convert.ToString(Sql(@"SELECT json_build_object('career',@career::json,
'purchases',(SELECT json_agg(r ORDER BY item_uid) FROM revora_furniture_purchases r WHERE uid=@uid),
'placements',(SELECT json_agg(r ORDER BY actor) FROM revora_house_furniture r WHERE uid=@uid),
'actors',(SELECT json_agg(r ORDER BY id) FROM furniture r WHERE mapid=(SELECT map_id FROM revora_house_maps WHERE uid=@uid)))::text","career",FreshHouseSnapshot(p),"uid",(long)p.Entity.UID));}
    static void FreshFurnitureJourney(bool reload){
        var p=FreshLoadSaved(true,true,true);EntityVariableTable.Load(p);SubClassTable.Load(p.Entity);ChiTable.Load(p);p.Quests.Load();Revora.Database.KnownPersons.LoadMentor(p);WayofHeroes.Load(p);Server.GamePool=new[]{p};Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        if((bool)Sql("SELECT to_regclass('revora_fresh_furniture_probe') IS NULL")){
            Require(!reload && FreshHouseSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_house_probe")));
            Sql("CREATE TABLE revora_fresh_furniture_probe(snapshot text);INSERT INTO revora_fresh_furniture_probe VALUES(@snapshot)","snapshot",FreshFurnitureSnapshot(p));
        }
        Require(FreshFurnitureSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_furniture_probe")));
        Map home=null;int trips=0;
        do{
        Require(++trips<=2);
        if(!reload){
            for(byte choice=10;choice<22;choice++){
                uint type=(uint)(721177+choice-10);
                if((long)Sql("SELECT count(*) FROM revora_furniture_purchases WHERE uid=@uid AND item_type=@type","uid",(long)p.Entity.UID,"type",(int)type)>0)continue;
                if(p.Inventory.Count>=40)break;
                uint cp=p.Entity.ConquerPoints;var before=p.Inventory.Objects.Select(v=>v.UID).ToArray();HeartTalk(p,30161,choice);HeartTalk(p,30161,40);var item=p.Inventory.Objects.Single(v=>!before.Contains(v.UID));Require(item.ID==type && p.Entity.ConquerPoints==cp-2500);
                string once=FreshFurnitureSnapshot(p);HeartTalk(p,30161,40);Require(once==FreshFurnitureSnapshot(p));
                Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_fresh_furniture_probe SET snapshot=@snapshot","snapshot",FreshFurnitureSnapshot(p));
            }
        }
        home=(Map)Call(Feature("Housing"),"Load",p);HeartTalk(p,30156,2);Require(p.Entity.MapID==home.ID);
        if(!reload)foreach(var item in p.Inventory.Objects.Where(v=>v.ID>=721177 && v.ID<=721188).OrderBy(v=>v.ID).ToArray()){
            Require((long)Sql("SELECT count(*) FROM revora_furniture_purchases WHERE uid=@uid AND item_uid=@item","uid",(long)p.Entity.UID,"item",(long)item.UID)==1);
            ItemHandler.UseItem(item,p);var frame=CareerFurniturePosition(p,(ushort)((item.ID-721176)*10));Revora.Network.PacketHandler.HandlePacket(frame,p);Require(p.Action==2 && !p.Inventory.ContainsUID(item.UID));
            string once=FreshFurnitureSnapshot(p);Revora.Network.PacketHandler.HandlePacket(frame,p);Require(once==FreshFurnitureSnapshot(p));
            Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_fresh_furniture_probe SET snapshot=@snapshot","snapshot",FreshFurnitureSnapshot(p));
        }
        var portal=home.Portals.Single();p.Entity.X=portal.CurrentX;p.Entity.Y=portal.CurrentY;GeneralData.UsePortal(new Data(true){ID=Data.UsePortal,UID=p.Entity.UID},p);Require(p.Entity.MapID==portal.DestinationMapID);FreshClassicTalk(p,1002,30156,0);
        }while(!reload && (long)Sql("SELECT count(*) FROM revora_furniture_purchases WHERE uid=@uid","uid",(long)p.Entity.UID)<12);
        Require((long)Sql("SELECT count(*) FROM revora_furniture_purchases WHERE uid=@uid AND cps_paid=2500 AND silver_paid=0","uid",(long)p.Entity.UID)==12);
        Require((long)Sql("SELECT count(*) FROM revora_house_furniture WHERE uid=@uid AND removed_at IS NULL","uid",(long)p.Entity.UID)==12);
        Require(home.Npcs.Values.Count(v=>v.Type==Enums.NpcType.Furniture)==12);
        Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10101 AND source='house-furniture-v1'","uid",(long)p.Entity.UID)==1);
        Require(EntityTable.SaveEntity(p));if(!reload)Sql("UPDATE revora_fresh_furniture_probe SET snapshot=@snapshot","snapshot",FreshFurnitureSnapshot(p));
        Require(FreshFurnitureSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_furniture_probe")));
        Console.WriteLine("[career-process] career-fresh-furniture-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Career furniture did not close.");
    }
}
