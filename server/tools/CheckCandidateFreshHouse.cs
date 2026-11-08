using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
using Console=System.Console;
internal static partial class CheckCandidateArenaLifecycle {
    static string FreshHouseSnapshot(GameState p){return Convert.ToString(Sql(@"SELECT json_build_object('career',@career::json,
'house',(SELECT row_to_json(h) FROM house h WHERE id=@uid),
'map',(SELECT row_to_json(m) FROM revora_house_maps m WHERE uid=@uid),
'builds',(SELECT json_agg(b ORDER BY level) FROM revora_house_builds b WHERE uid=@uid),
'materials',(SELECT json_agg(m ORDER BY level,item_uid) FROM revora_house_build_materials m WHERE uid=@uid))::text","career",FreshSnowSnapshot(p),"uid",(long)p.Entity.UID));}
    static void FreshHouseJourney(bool reload){
        var p=FreshLoadSaved(true,true,true);EntityVariableTable.Load(p);SubClassTable.Load(p.Entity);ChiTable.Load(p);p.Quests.Load();Revora.Database.KnownPersons.LoadMentor(p);WayofHeroes.Load(p);Server.GamePool=new[]{p};Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        if((bool)Sql("SELECT to_regclass('revora_fresh_house_probe') IS NULL")){
            Require(!reload && FreshSnowSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_snow_probe")));
            Require((long)Sql("SELECT count(*) FROM house WHERE id=@uid","uid",(long)p.Entity.UID)==0);
            Sql("CREATE TABLE revora_fresh_house_probe(snapshot text);INSERT INTO revora_fresh_house_probe VALUES(@snapshot)","snapshot",FreshHouseSnapshot(p));
        }
        Require(FreshHouseSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_house_probe")));
        if(!reload){
            FreshClassicTalk(p,1002,30156,0);Require(p.Inventory.Count<40);
            uint[] permits={721170,721174,711198,711200,711553};ulong[] silver={300000,500000,2000000,5000000,0};
            int level=Convert.ToInt32(Sql("SELECT COALESCE((SELECT houselevel FROM house WHERE id=@uid),0)","uid",(long)p.Entity.UID));
            for(int next=level+1;next<=5;next++){
                uint cp=p.Entity.ConquerPoints,price=(uint)Call(Feature("Housing"),"Price",next);ulong money=p.Entity.Money;
                var before=p.Inventory.Objects.Select(i=>i.UID).ToArray();HeartTalk(p,30156,(byte)(next+4));HeartTalk(p,30156,40);
                var cert=p.Inventory.Objects.Single(i=>!before.Contains(i.UID));Require(cert.ID==permits[next-1] && p.Entity.ConquerPoints==cp-price);
                string bought=FreshHouseSnapshot(p);HeartTalk(p,30156,40);Require(bought==FreshHouseSnapshot(p));
                HeartTalk(p,30156,next==1?(byte)1:(byte)3);HeartTalk(p,30156,40);
                Require(p.Action==2 && !p.Inventory.ContainsUID(cert.UID) && p.Entity.Money==money-silver[next-1] && Convert.ToInt32(Sql("SELECT houselevel FROM house WHERE id=@uid","uid",(long)p.Entity.UID))==next);
                string built=FreshHouseSnapshot(p);HeartTalk(p,30156,40);Require(built==FreshHouseSnapshot(p));
                Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_fresh_house_probe SET snapshot=@snapshot","snapshot",FreshHouseSnapshot(p));
                Console.WriteLine("[fresh-house] level="+next+" cp="+price+" silver="+silver[next-1]);
            }
        }
        Require((long)Sql("SELECT count(*) FROM revora_house_builds WHERE uid=@uid","uid",(long)p.Entity.UID)==5);
        foreach(int id in new[]{10102,10103,10104,10105,10122})Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=@id AND source='house-construction-v1'","uid",(long)p.Entity.UID,"id",id)==1);
        var home=(Map)Call(Feature("Housing"),"Load",p);Require(home!=null && home.BaseID==3024 && MapsTable.MapInformations[home.ID].Owner==p.Entity.UID);
        HeartTalk(p,30156,2);Require(p.Action==2 && p.Entity.MapID==home.ID && home.Floor[p.Entity.X,p.Entity.Y,MapObjectType.Player]);
        var portal=home.Portals.Single();p.Entity.X=portal.CurrentX;p.Entity.Y=portal.CurrentY;
        GeneralData.UsePortal(new Data(true){ID=Data.UsePortal,UID=p.Entity.UID},p);Require(p.Entity.MapID==portal.DestinationMapID);
        FreshClassicTalk(p,1002,30156,0);Require(EntityTable.SaveEntity(p));
        if(!reload)Sql("UPDATE revora_fresh_house_probe SET snapshot=@snapshot","snapshot",FreshHouseSnapshot(p));
        Require(FreshHouseSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_house_probe")));
        Console.WriteLine("[career-process] career-fresh-house-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Career housing did not close.");
    }
}
