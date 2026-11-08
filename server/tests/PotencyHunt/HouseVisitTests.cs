using System;
using System.Linq;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network;
using Revora.Network.GamePackets;
using Store=Revora.Game.ConquerStructures.Warehouse;

internal static partial class PotencyHuntTests
{
    static readonly Type HomeVisits=typeof(Entity).Assembly.GetType("Revora.Game.Features.HouseVisits");
    static DateTime visitNow=new DateTime(2035,1,1,19,0,0,DateTimeKind.Utc);
    static byte[] VisitFrame(byte option,uint echo=30156)
    {
        var frame=new byte[28];Writer.WriteUInt16(20,0,frame);Writer.WriteUInt16(2032,2,frame);
        Writer.WriteUInt32(echo,8,frame);frame[14]=option;Writer.WriteUInt64(PacketHandler.ClientSeal,20,frame);return frame;
    }
    static void VisitNpc(GameState p,byte option,uint echo=30156){p.ActiveNpc=30156;PacketHandler.HandlePacket(VisitFrame(option,echo),p);}
    static GameState ReloadHouseVisitor(GameState p)
    {
        p=ReloadBall(p);Revora.Database.KnownPersons.LoaderFriends(p);return p;
    }
    static long HomeArrivals(GameState p){return Scalar("SELECT count(*) FROM revora_house_visits WHERE guest=@uid AND state=1",p.Entity.UID);}
    static GameState[] HomePair(Map town)
    {
        visitNow=visitNow.AddSeconds(3);var host=HousePlayer(town);BuyHouseCert(host,1);HouseNpc(host,1);HouseNpc(host,40);
        var guest=HousePlayer(town);Revora.Database.KnownPersons.LoaderFriends(host);Revora.Database.KnownPersons.LoaderFriends(guest);
        FriendSend(host,10,guest);FriendSend(guest,11,host);
        Check(!Fenced(host) && !Fenced(guest) && host.Friends.ContainsKey(guest.Entity.UID) && guest.Friends.ContainsKey(host.Entity.UID)
            && host.Account.State==AccountTable.AccountState.Player && guest.Account.State==AccountTable.AccountState.Player,
            "ordinary house host and guest establish mutual friendship through the native request path");
        HouseNpc(host,0);HouseNpc(guest,0);return new[]{host,guest};
    }
    static void InviteHome(GameState host,GameState guest){VisitNpc(host,16);VisitNpc(host,60);VisitNpc(guest,13);}
    static void VerifyHouseVisits(string runtime,Map town)
    {
        Query("CREATE TABLE IF NOT EXISTS friends AS TABLE public.friends WITH NO DATA");Call(FriendService,null,"Initialize");
        HomeVisits.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>visitNow));
        FriendService.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>visitNow));
        try {
            var pair=HomePair(town);var host=pair[0];var guest=pair[1];ulong gold=guest.Entity.Money;uint cp=guest.Entity.ConquerPoints;
            VisitNpc(guest,70);VisitNpc(host,60);
            Check(HomeArrivals(guest)==0 && !HasAchievement(host,10131) && !HasAchievement(guest,10132),"unoffered visit and forged friend selection grant neither admission nor achievement");
            InviteHome(host,guest);
            Check(HomeArrivals(guest)==0 && !HasAchievement(host,10131),"invitation and review alone never count as a completed visit");
            Parallel.For(0,12,i=>VisitNpc(guest,70));ushort home=guest.Entity.MapID;
            Check(!Fenced(host) && !Fenced(guest) && home>=60000 && guest.Map.BaseID==1098 && HomeArrivals(guest)==1
                && HasAchievement(host,10131) && HasAchievement(guest,10132) && !HasAchievement(host,10132) && !HasAchievement(guest,10131)
                && guest.Entity.Money==gold && guest.Entity.ConquerPoints==cp,"one consented native visit saves arrival and distinct host/guest achievements without a fee");
            guest.Entity.Teleport(guest.Map.BaseID,home,guest.Entity.X,guest.Entity.Y);
            Check(guest.Entity.MapID==home && HomeArrivals(guest)==1,"same-house correction retains a visit without issuing another receipt");
            var portal=guest.Map.Portals.Single();AdventureWalk(guest,portal.CurrentX,portal.CurrentY);
            GeneralData.UsePortal(new Data(true){ID=Data.UsePortal,UID=guest.Entity.UID},guest);
            Check(guest.Entity.MapID==portal.DestinationMapID,"ordinary guest uses the installed house exit");
            guest.Entity.Teleport(1098,home,32,31);
            Check(guest.Entity.MapID!=home && HomeArrivals(guest)==1,"a consumed invitation cannot authorize re-entry after leaving");
            if(guest.Entity.MapID!=1002)guest.Entity.Teleport(1002,345,529);
            guest=ReloadHouseVisitor(guest);host=ReloadHouseVisitor(host);new TitleStorage().Login(guest);new TitleStorage().Login(host);
            Check(!Fenced(host) && !Fenced(guest) && HasAchievement(host,10131) && HasAchievement(guest,10132),"both earned visit roles survive production character/item/title reload");
            AdventureNear(host,30156);AdventureNear(guest,30156);HouseNpc(host,0);HouseNpc(guest,0);visitNow=visitNow.AddSeconds(3);
            InviteHome(host,guest);VisitNpc(guest,70);guest=ReloadHouseVisitor(guest);new TitleStorage().Login(guest);
            Check(!Fenced(guest) && guest.Entity.MapID==1002 && HomeArrivals(guest)==2 && HasAchievement(guest,10132),
                "reconnecting from a friend's home returns to Twin City while preserving visit evidence");
            foreach(string malformed in new[]{"short","length","extra","input","seal"}){
                pair=HomePair(town);host=pair[0];guest=pair[1];InviteHome(host,guest);var frame=VisitFrame(70);
                if(malformed=="short")Array.Resize(ref frame,27);if(malformed=="length")Writer.WriteUInt16(19,0,frame);
                if(malformed=="extra")Array.Resize(ref frame,29);if(malformed=="input")frame[17]=255;if(malformed=="seal")frame[27]^=1;
                Check(!(bool)Call(HomeVisits,null,"Frame",frame),"bounded visit decoder rejects malformed frame: "+malformed);
                // Transport-invalid seals/lengths disconnect in the shared dispatcher; this isolated actor has no transport timers.
                if(malformed=="input")PacketHandler.HandlePacket(frame,guest);
                Check(guest.Entity.MapID==1002 && HomeArrivals(guest)==0 && !Fenced(guest),"input validation and decoder probes preserve pending consent: "+malformed);
                VisitNpc(guest,70);Check(guest.Entity.MapID>=60000 && HomeArrivals(guest)==1,"valid native reply still completes once after rejection: "+malformed);
            }
            foreach(int level in new[]{2,3,4,5}){
                pair=HomePair(town);host=pair[0];guest=pair[1];
                for(int stage=2;stage<=level;stage++){BuyHouseCert(host,stage);HouseNpc(host,3);HouseNpc(host,40);}
                if(level==3){BuyHouseCert(guest,1);HouseNpc(guest,1);HouseNpc(guest,40);}
                InviteHome(host,guest);VisitNpc(guest,70);
                Check(!Fenced(guest) && guest.Entity.MapID>=60000 && guest.Map.BaseID==new ushort[]{0,1098,1099,2080,1765,3024}[level]
                    && HomeArrivals(guest)==1 && HasAchievement(guest,10132),"ordinary friend admission uses the owned terrain for house level "+level);
                if(level==2){BuyHouseCert(host,3);ulong before=host.Entity.Money;HouseNpc(host,3);HouseNpc(host,40);
                    Check(host.Entity.Money==before && Scalar("SELECT houselevel FROM house WHERE id=@uid",host.Entity.UID)==2 && host.Inventory.Contains(711198,1),
                        "a visiting friend blocks renovation without consuming the certificate or fee");}
                guest=ReloadHouseVisitor(guest);new TitleStorage().Login(guest);
                Check(!Fenced(guest) && guest.Entity.MapID==1002 && HasAchievement(guest,10132),"guest reconnect returns safely even when owning a separate home: level "+level);
            }
            foreach(string mode in new[]{"no-review","expired","cancelled","declined","removed-friend","missing-direction","guest-reload","host-reload","host-away","guest-trade","host-trade","guest-dead","wrong-npc"}) {
                pair=HomePair(town);host=pair[0];guest=pair[1];InviteHome(host,guest);
                if(mode=="no-review"){VisitNpc(guest,71);visitNow=visitNow.AddSeconds(3);VisitNpc(host,16);VisitNpc(host,60);}
                if(mode=="expired")visitNow=visitNow.AddMinutes(2);
                if(mode=="cancelled")VisitNpc(host,14);if(mode=="declined")VisitNpc(guest,71);
                if(mode=="removed-friend")FriendSend(host,14,guest);
                if(mode=="missing-direction")Query("DELETE FROM friends WHERE entityid=@host AND friendid=@guest","host",(long)host.Entity.UID,"guest",(long)guest.Entity.UID);
                if(mode=="guest-reload")guest=ReloadHouseVisitor(guest);if(mode=="host-reload")host=ReloadHouseVisitor(host);
                if(mode=="host-away")host.Entity.X+=20;if(mode=="guest-trade")guest.Trade.InTrade=true;
                if(mode=="host-trade")host.Trade.InTrade=true;if(mode=="guest-dead")guest.Entity.Hitpoints=0;
                VisitNpc(guest,70,mode=="wrong-npc"?30157u:30156u);
                Check(guest.Entity.MapID==1002 && HomeArrivals(guest)==0 && !HasAchievement(guest,10132) && !HasAchievement(host,10131),
                    "invalid house invitation cannot admit or award either player: "+mode);
            }
            foreach(string failure in new[]{"consent","arrival","map","evidence"}) {
                pair=HomePair(town);host=pair[0];guest=pair[1];InviteHome(host,guest);
                string table=failure=="consent" || failure=="arrival"?"revora_house_visits":failure=="map"?"entities":"revora_achievement_evidence";
                string operation=failure=="consent" || failure=="evidence"?"INSERT":"UPDATE";
                string filter=failure=="map"?"WHEN (NEW.mapid>=60000 AND NEW.uid="+guest.Entity.UID+")":failure=="evidence"?"WHEN (NEW.achievement_id IN(10131,10132))":"";
                Query("CREATE FUNCTION suppress_home_visit() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$;CREATE TRIGGER suppress_home_visit BEFORE "+operation+" ON "+table+" FOR EACH ROW "+filter+" EXECUTE FUNCTION suppress_home_visit()");
                try{VisitNpc(guest,70);}finally{Query("DROP TRIGGER suppress_home_visit ON "+table+";DROP FUNCTION suppress_home_visit()");}
                Check(Fenced(guest) || Fenced(host),"failed house visit participant fences its affected session: "+failure);
                Check(HomeArrivals(guest)==(failure=="evidence"?1:0) && !HasAchievement(host,10131) && !HasAchievement(guest,10132),
                    "failed writes cannot manufacture arrival or visible achievements: "+failure);
                guest=ReloadHouseVisitor(guest);host=ReloadHouseVisitor(host);new TitleStorage().Login(guest);new TitleStorage().Login(host);
                if(failure=="evidence")Check(HasAchievement(host,10131) && HasAchievement(guest,10132) && guest.Entity.MapID==1002,
                    "durable arrival restores both entitlements after a failed projection without re-entering");
                else {
                    AdventureNear(host,30156);AdventureNear(guest,30156);HouseNpc(host,0);HouseNpc(guest,0);visitNow=visitNow.AddSeconds(3);
                    InviteHome(host,guest);VisitNpc(guest,70);
                    Check(!Fenced(host) && !Fenced(guest) && HomeArrivals(guest)==1 && HasAchievement(host,10131) && HasAchievement(guest,10132),
                        "fresh consent retries once after failed visit and production reload: "+failure);
                }
            }
            pair=HomePair(town);host=pair[0];guest=ReloadHouseVisitor(pair[1]);HouseNpc(guest,0);host=ReloadHouseVisitor(host);AdventureNear(host,30156);HouseNpc(host,2);
            uint box=PlaceBox(host,Pack(host,721189,1));var owned=Pack(host,1088001,1);BoxOpen(host,box);BoxMove(host,1,owned.UID,box);
            host.Entity.Teleport(1002,345,529);AdventureNear(host,30156);HouseNpc(host,0);InviteHome(host,guest);VisitNpc(guest,70);
            BoxOpen(guest,box);RemoveFurniture(guest,box);
            Check(guest.Map.Npcs.ContainsKey(box) && !guest.Warehouses.ContainsKey((Store.WarehouseID)box),"invited friend cannot open or destroy the owner's item box");
            guest.Warehouses[(Store.WarehouseID)box]=new Store(guest,(Store.WarehouseID)box,60,true);guest.ActiveNpc=box;
            var carried=Pack(guest,1088001,1);BoxMove(guest,1,carried.UID,box);BoxMove(guest,2,owned.UID,box);
            var decoration=Pack(guest,721177,1);ItemHandler.UseItem(decoration,guest);PacketHandler.HandlePacket(FurniturePosition(guest,decoration.ID),guest);
            Check(!Fenced(guest) && guest.Inventory.ContainsUID(carried.UID) && guest.Inventory.ContainsUID(decoration.UID)
                && !guest.Inventory.ContainsUID(owned.UID) && Scalar("SELECT warehouse FROM items WHERE uid=@uid",owned.UID)==box
                && guest.Map.Npcs.Count(n=>n.Value.Type==Enums.NpcType.Furniture)==0,
                "even a foreign container registration grants no guest deposit, withdrawal or furnishing authority");
        }finally{
            HomeVisits.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>DateTime.UtcNow));
            FriendService.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>DateTime.UtcNow));
        }
    }
}
