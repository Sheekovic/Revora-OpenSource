using System;
using System.Linq;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static readonly Type Anniversary=typeof(Entity).Assembly.GetType("Revora.Game.Features.AnniversaryQuest");
    static DateTime annivNow=new DateTime(2026,9,26,19,0,0,DateTimeKind.Utc);
    static long AnnivRows(GameState p,string condition="true"){return Scalar("SELECT count(*) FROM revora_anniversary_runs WHERE uid=@uid AND "+condition,p.Entity.UID);}
    static long AnnivSpirits(GameState p){return Scalar("SELECT COALESCE(sum(spirits),0) FROM revora_anniversary_runs WHERE uid=@uid",p.Entity.UID);}
    static ConquerItem AnnivBead(GameState p){return p.Inventory.Objects.Single(i=>i.ID==3001172);}
    static GameState AnnivPlayer(Map town){var p=SoulBallPlayer(town);p.Entity.Level=140;p.Entity.Reborn=2;p.Entity.VIPLevel=6;p.LastResetTime=annivNow.ToLocalTime();EntityTable.SaveEntity(p);return p;}
    static GameState FullAnniv(Map town,uint spirits=15000){var p=AnnivPlayer(town);Pack(p,3001172,1);DialyQuestTable.Load(p);p.Entity.spiritBeadsCount=spirits;DialyQuestTable.Save(p);return p;}
    static void AnnivNpc(GameState p,byte option,uint identity=0){p.ActiveNpc=900028;var frame=new byte[28];Writer.WriteUInt16(20,0,frame);Writer.WriteUInt16(2032,2,frame);Writer.WriteUInt32(identity,8,frame);frame[14]=option;Writer.WriteUInt64(PacketHandler.ClientSeal,20,frame);PacketHandler.HandlePacket(frame,p);}
    static void VerifyAnniversaryCalendar(Map town) {
        var saved=annivNow;
        try {
            var boundaries=new[]{
                new DateTime(2026,9,26,7,0,0,DateTimeKind.Utc),new DateTime(2026,9,28,7,0,0,DateTimeKind.Utc),
                new DateTime(2026,3,7,8,0,0,DateTimeKind.Utc),new DateTime(2026,3,9,7,0,0,DateTimeKind.Utc),
                new DateTime(2026,10,31,7,0,0,DateTimeKind.Utc),new DateTime(2026,11,2,8,0,0,DateTimeKind.Utc)};
            for(int n=0;n<boundaries.Length;n++) {
                bool opens=n%2==0;
                Check((bool)Call(Anniversary,null,"Weekend",boundaries[n])==opens && (bool)Call(Anniversary,null,"Weekend",boundaries[n].AddTicks(-10))!=opens,
                    "Pacific weekend boundary is exact across standard time and DST: "+boundaries[n].ToString("s"));
            }
            bool rejected=false;try{Call(Anniversary,null,"Weekend",DateTime.SpecifyKind(saved,DateTimeKind.Unspecified));}catch(ArgumentException){rejected=true;}
            Check(rejected,"Anniversary calendar rejects ambiguous non-UTC timestamps");
            annivNow=boundaries[0].AddSeconds(-1);var early=AnnivPlayer(town);AnnivNpc(early,8);AnnivNpc(early,35);
            Check(!Fenced(early) && AnnivRows(early)==0 && early.Inventory.Count==0,"Friday direct requests cannot issue a new or replacement bead");
            annivNow=boundaries[0];var active=AnnivPlayer(town);AnnivNpc(active,8);
            Check(!Fenced(active) && AnnivRows(active)==1 && AnnivSpirits(active)==3,"Saturday midnight opens ordinary bead admission");
            var bead=AnnivBead(active);active.Inventory.Remove(bead,Enums.ItemUse.Remove);annivNow=boundaries[1];AnnivNpc(active,35);
            Check(!Fenced(active) && AnnivBead(active).UID!=bead.UID && AnnivSpirits(active)==3,"weekday replacement preserves an already entrusted weekend commission");
            Call(Anniversary,null,"OnDeath",HuntMonster(active,"Pheasant"),active.Entity);
            Check(AnnivSpirits(active)==4,"closing the weekend does not strand an existing hunt");
            var late=AnnivPlayer(town);AnnivNpc(late,8);Check(AnnivRows(late)==0 && late.Inventory.Count==0,"Monday midnight closes new bead admission");
            var earned=FullAnniv(town);ItemHandler.UseItem(AnnivBead(earned),earned);
            Check(!Fenced(earned) && AnnivRows(earned,"completed_day<>''")==1 && earned.Entity.RevoraPoint==50000,"an existing full bead remains redeemable after weekend closing");
            annivNow=boundaries[0];var crossing=AnnivPlayer(town);int reads=0;
            Anniversary.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>++reads==1?boundaries[1].AddSeconds(-1):boundaries[1]));
            try{AnnivNpc(crossing,8);}finally{Anniversary.GetField("Clock",Hidden).SetValue(null,new Func<DateTime>(()=>annivNow));}
            Check(!Fenced(crossing) && crossing.Inventory.Count==0 && AnnivRows(crossing)==0,"admission crossing Monday midnight rolls back bead and receipt without fencing the player");
            var metadata=MapsTable.MapInformations[1002];uint owner=metadata.Owner;metadata.Owner=42;MapsTable.MapInformations[1002]=metadata;
            var foreign=AnnivPlayer(town);try{AnnivNpc(foreign,8);}finally{metadata.Owner=owner;MapsTable.MapInformations[1002]=metadata;}
            Check(AnnivRows(foreign)==0,"nonpublic Twin City metadata cannot authorize Anniversary admission");
        }finally{annivNow=saved;}
    }
    static void VerifyAnniversaryWorlds(string runtime,Map town){
        ushort[] ids={1207,1208,1212,1214,1215,1217,1762,1927,1999,2054,2055,2056};
        string[] names={"CateranSoldier","ChiefCateran","WaterDevil","HugeApe","HugeSnake","AlienSerpent","IcySerpent","IcyBladeDevil","DarkLady","GrottoLady","SnakeHubbub","FrostBladeDevil"};
        for(int n=0;n<ids.Length;n++){
            var map=ids[n]==1217?TravelAlias(runtime,1217,1075):World(runtime,ids[n]);
            var p=FullAnniv(town,14999);p.BindWorldMap(map.ID);EntityTable.SaveEntity(p);var mob=HuntMonster(p,names[n]);
            Parallel.For(0,8,i=>Call(Anniversary,null,"OnDeath",mob,p.Entity));
            Check(!Fenced(p) && AnnivSpirits(p)==15000,"existing Anniversary bead reaches its cap once in public world "+map.ID);
            p=ReloadBall(p);Call(Anniversary,null,"OnDeath",mob,p.Entity);ItemHandler.UseItem(AnnivBead(p),p);p=ReloadBall(p);
            Check(!Fenced(p) && AnnivRows(p,"completed_day<>''")==1 && p.Entity.RevoraPoint==50000 && p.Inventory.Objects.Count(i=>i.ID==3006048)==20,
                "world "+map.ID+" Anniversary progress and full prize survive ordinary reload");
        }
        var copy=TravelAlias(runtime,62010,1927);var denied=FullAnniv(town,14999);denied.BindWorldMap(copy.ID);
        Call(Anniversary,null,"OnDeath",HuntMonster(denied,"IcyBladeDevil"),denied.Entity);
        Check(!Fenced(denied) && AnnivRows(denied)==0,"private Grotto copy cannot import or credit an Anniversary bead");
    }
    static void VerifyAnniversary(string runtime,Map town){
        VerifyFirstRebirth(runtime,town);MiningNpc(town,79882);Call(Anniversary,null,"Initialize");Call(Anniversary,null,"Initialize");
        Anniversary.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>annivNow));SoulBall.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>annivNow));
        VerifyAnniversaryWorlds(runtime,town);
        var noNpc=AnnivPlayer(town);AnnivNpc(noNpc,8);Check(AnnivRows(noNpc)==0 && noNpc.Inventory.Count==0,"absent installed Anniversary NPC cannot issue a bead");
        var jail=World(runtime,6000);
        var retired=new NpcSpawn{UID=900028,MapID=6000,X=102,Y=78,Mesh=34240,Name="UnrelatedClerk",Type=Enums.NpcType.Talker};
        jail.AddNpc(retired);Map.SecoundNpc.Add(retired.UID,retired);
        bool collision=false;try{Call(Anniversary,null,"PlaceNpc");}catch(InvalidOperationException){collision=true;}
        Check(collision && jail.Npcs.ContainsKey(900028) && !town.Npcs.ContainsKey(900028),"unrelated profile remains protected during Anniversary migration");
        retired.Name="AdmAniversario";
        var blocker=new NpcSpawn{UID=990012,MapID=1002,X=323,Y=307,Mesh=19936,Name="FixtureBlocker",Type=Enums.NpcType.Talker};town.AddNpc(blocker);
        collision=false;try{Call(Anniversary,null,"PlaceNpc");}catch(InvalidOperationException){collision=true;}
        Check(collision && jail.Npcs.ContainsKey(900028),"occupied Anniversary destination preserves retired actor");town.RemoveNpc(blocker);
        bool parked=false;Revora.Game.Features.TwinCityPlaza.Apply((id,map,x,y)=>{if(id==900028)parked=true;});
        Check(!parked && !Revora.Game.Features.TwinCityPlaza.IsClosed(900028),"plaza leaves restored Anniversary identity available");
        Call(Anniversary,null,"PlaceNpc");Call(Anniversary,null,"PlaceNpc");
        Check(!jail.Npcs.ContainsKey(900028) && ReferenceEquals(Map.SecoundNpc[900028],town.Npcs[900028]) && !ReferenceEquals(Map.SecoundNpc[900028],retired),"retired Anniversary actor replaced in both registries");
        town.RemoveNpc(town.Npcs[900028]);Map.SecoundNpc.Remove(900028);retired.X=90;jail.AddNpc(retired);Map.SecoundNpc.Add(900028,retired);
        Call(Anniversary,null,"PlaceNpc");Call(Anniversary,null,"PlaceNpc");
        Check(!jail.Npcs.ContainsKey(900028) && ReferenceEquals(Map.SecoundNpc[900028],town.Npcs[900028]),"previous plaza-parked Anniversary profile migrates idempotently");
        Check(town.Npcs[900028].X==323 && town.Npcs[900028].Y==307,"production Anniversary placement installs its public envoy idempotently");
        VerifyAnniversaryCalendar(town);
        var p=AnnivPlayer(town);Parallel.For(0,8,i=>AnnivNpc(p,8));
        Check(!Fenced(p) && AnnivRows(p)==1 && AnnivSpirits(p)==3 && AnnivBead(p).Bound==false && p.Inventory.Count==1 && p.Account.State==AccountTable.AccountState.Player,"eligible ordinary public admission issues one unbound bead under concurrent requests");
        var bead=AnnivBead(p);ItemHandler.UseItem(bead,p);Check(AnnivRows(p,"completed_day<>''")==0,"incomplete bead displays durable progress without paying");
        p=ReloadBall(p);Call(Anniversary,null,"Refresh",p);Check(!Fenced(p) && p.Entity.spiritBeadsCount==3 && AnnivBead(p).UID==bead.UID,"admission survives production reload");
        var dead=HuntMonster(p,"Pheasant");Parallel.For(0,8,i=>Call(Anniversary,null,"OnDeath",dead,p.Entity));dead.DeathStamp=dead.DeathStamp.AddSeconds(5);Call(Anniversary,null,"OnDeath",dead,p.Entity);
        Check(AnnivSpirits(p)==4,"concurrent corpse callbacks and altered timestamps credit once");
        dead.Hitpoints=100;dead.ClearFlagsMobs();Call(Anniversary,null,"Revived",dead);dead.Hitpoints=0;Call(Anniversary,null,"OnDeath",dead,p.Entity);Check(AnnivSpirits(p)==5,"genuine monster revival earns a new spirit");
        foreach(bool bound in new[]{false,true}){
            p=FullAnniv(town);bead=AnnivBead(p);bead.Bound=bound;Query("UPDATE items SET bound=@bound WHERE uid=@uid","bound",bound?1:0,"uid",(long)bead.UID);
            ulong money=p.Entity.Money,xp=p.Entity.Experience;uint cp=p.Entity.ConquerPoints,bless=p.Entity.HeavenBlessing;
            Parallel.For(0,12,i=>ItemHandler.UseItem(bead,p));
            Check(!Fenced(p) && AnnivRows(p,"completed_day<>'' AND cardinality(reward_uids)=20")==1 && p.Inventory.Count==20 && p.Inventory.Objects.All(i=>i.ID==3006048 && i.Bound==bound)
                && p.Entity.Money==money+50000000 && p.Entity.RevoraPoint==50000 && p.Entity.SubClasses.StudyPoints==100 && p.Entity.HeavenBlessing==bless+172800 && p.Entity.ConquerPoints==cp && p.Entity.Experience==xp,"full Anniversary prize commits once, preserves binding and grants no unadvertised CPs/EXP: bound="+bound);
            p=ReloadBall(p);Call(Anniversary,null,"Refresh",p);Check(!Fenced(p) && p.Inventory.Count==20 && p.Entity.SpiritBeads && p.Entity.spiritBeadsCount==0 && p.Entity.Money==money+50000000 && p.Entity.RevoraPoint==50000 && p.Entity.HeavenBlessing==bless+172800,"all Anniversary components survive production reload");
            PriceStand(p);SoulNpc(p,7);SoulNpc(p,31);AnnivNpc(p,8);Check(AnnivRows(p)==1 && SoulDone(p)==0,"Anniversary completion closes both bead reward routes for the day");
            annivNow=annivNow.AddDays(1);AnnivNpc(p,8);Check(!Fenced(p) && AnnivRows(p)==2 && AnnivBead(p).ID==3001172,"next Pacific day permits one new Anniversary commission");
            annivNow=annivNow.AddDays(-2);AnnivNpc(p,8);ItemHandler.UseItem(AnnivBead(p),p);Check(AnnivRows(p)==2,"clock rollback cannot start or claim a future Anniversary commission");annivNow=new DateTime(2026,9,26,19,0,0,DateTimeKind.Utc);
        }
        var valuable=AnnivPlayer(town);AnnivNpc(valuable,8);
        Query("UPDATE revora_anniversary_runs SET spirits=15000 WHERE uid=@uid","uid",(long)valuable.Entity.UID);
        while(valuable.Inventory.Count<21)Pack(valuable,1088001,1);ItemHandler.UseItem(AnnivBead(valuable),valuable);
        Check(!Fenced(valuable) && AnnivRows(valuable,"completed_day<>''")==0,"new Anniversary contract requires one additional soul slot");
        DisRemove(valuable,valuable.Inventory.Objects.First(i=>i.ID==1088001));
        Query("CREATE FUNCTION suppress_anniv_soul() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN IF NEW.id BETWEEN 800000 AND 899999 THEN RETURN NULL; END IF; RETURN NEW; END';CREATE TRIGGER suppress_anniv_soul BEFORE INSERT ON items FOR EACH ROW EXECUTE FUNCTION suppress_anniv_soul()");
        try{ItemHandler.UseItem(AnnivBead(valuable),valuable);}finally{Query("DROP TRIGGER suppress_anniv_soul ON items;DROP FUNCTION suppress_anniv_soul()");}
        Check(Fenced(valuable) && AnnivRows(valuable,"completed_day<>''")==0 && valuable.Entity.RevoraPoint==0
            && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND (id=3006048 OR id BETWEEN 800000 AND 899999)",valuable.Entity.UID)==0,"suppressed Anniversary soul rolls back cards, currency and completion");
        valuable=ReloadBall(valuable);var original=AnnivBead(valuable);Parallel.For(0,12,i=>ItemHandler.UseItem(original,valuable));
        valuable=ReloadBall(valuable);var soul=valuable.Inventory.Objects.Single(i=>i.ID>=800000 && i.ID<900000);
        Check(!Fenced(valuable) && valuable.Inventory.Count==40 && soul.Bound && ConquerItemInformation.BaseInformations[soul.ID].PurificationLevel==7
            && valuable.Inventory.Objects.Count(i=>i.ID==3006048)==20 && AnnivRows(valuable,"completed_day<>'' AND cardinality(reward_uids)=21")==1,
            "new Anniversary reward commits one bound P7 soul and twenty cards once and survives production reload");
        Call(Anniversary,null,"Initialize");
        foreach(string fault in new[]{"evidence","wallet","silver","blessing","bead","first-card","late-card","receipt"}){
            p=FullAnniv(town);bead=AnnivBead(p);ulong money=p.Entity.Money;
            string table=fault=="evidence"?"revora_achievement_evidence":fault=="receipt"?"revora_anniversary_runs":fault=="bead" || fault.EndsWith("card")?"items":"entities";
            string verb=fault=="evidence"?"INSERT":fault=="silver"?"UPDATE OF money":fault=="blessing"?"UPDATE OF heavenblessingtime":fault=="bead"?"DELETE":fault.EndsWith("card")?"INSERT":"UPDATE";
            string body=fault=="late-card"?"IF NEW.id=3006048 AND (SELECT count(*) FROM items WHERE entityid=NEW.entityid AND id=3006048)>=19 THEN RETURN NULL; END IF; RETURN NEW;":"RETURN NULL;";
            Query("CREATE FUNCTION suppress_anniv() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN "+body+" END';CREATE TRIGGER suppress_anniv BEFORE "+verb+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_anniv()");
            try{ItemHandler.UseItem(bead,p);}finally{Query("DROP TRIGGER suppress_anniv ON "+table+";DROP FUNCTION suppress_anniv()");}
            Check(Fenced(p) && AnnivRows(p,"completed_day<>''")==0 && p.Inventory.ContainsUID(bead.UID) && p.Entity.Money==money && p.Entity.RevoraPoint==0 && p.Entity.HeavenBlessing==0
                && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND id=3006048",p.Entity.UID)==0,"failed "+fault+" retains the whole Anniversary prize");
            if(fault=="evidence")VerifyStudyRollback(p);
            p=ReloadBall(p);ItemHandler.UseItem(AnnivBead(p),p);Check(!Fenced(p) && AnnivRows(p,"completed_day<>''")==1 && p.Entity.Money==money+50000000,"production reload retries the complete Anniversary prize: "+fault);
            if(fault=="evidence")VerifyStudyAward(p,"anniversary");
        }
        foreach(string fault in new[]{"items","revora_anniversary_runs"}){
            p=AnnivPlayer(town);Query("CREATE FUNCTION suppress_anniv() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_anniv BEFORE INSERT ON "+fault+" FOR EACH ROW EXECUTE FUNCTION suppress_anniv()");
            try{AnnivNpc(p,8);}finally{Query("DROP TRIGGER suppress_anniv ON "+fault+";DROP FUNCTION suppress_anniv()");}
            Check(Fenced(p) && AnnivRows(p)==0 && p.Inventory.Count==0,"failed "+fault+" rolls back fresh admission");p=ReloadBall(p);PriceStand(p);AnnivNpc(p,8);Check(!Fenced(p) && AnnivRows(p)==1,"fresh admission recovers after reload: "+fault);
        }
        p=AnnivPlayer(town);AnnivNpc(p,8);dead=HuntMonster(p,"Pheasant");
        Query("CREATE FUNCTION suppress_anniv() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_anniv BEFORE UPDATE ON revora_anniversary_runs FOR EACH ROW EXECUTE FUNCTION suppress_anniv()");
        try{Call(Anniversary,null,"OnDeath",dead,p.Entity);}finally{Query("DROP TRIGGER suppress_anniv ON revora_anniversary_runs;DROP FUNCTION suppress_anniv()");}
        Check(Fenced(p) && AnnivSpirits(p)==3,"failed hunt write retains already committed spirits");p=ReloadBall(p);Call(Anniversary,null,"OnDeath",dead,p.Entity);Check(!Fenced(p) && AnnivSpirits(p)==4,"uncredited corpse can finish once after reload");
        foreach(string state in new[]{"remote","trade","dead","fake","unloaded","level","reborn","vip","replaced","wrong-npc"}){
            p=AnnivPlayer(town);if(state=="remote")p.Entity.X=1;if(state=="trade")p.Trade.InTrade=true;if(state=="dead")p.Entity.Hitpoints=0;if(state=="fake")p.Fake=true;if(state=="unloaded")p.Entity.FullyLoaded=false;
            if(state=="level")p.Entity.Level=139;if(state=="reborn")p.Entity.Reborn=1;if(state=="vip")p.Entity.VIPLevel=5;if(state=="replaced")Kernel.GamePool[p.Entity.UID]=new GameState(null);
            AnnivNpc(p,8,state=="wrong-npc"?12333u:0);AnnivNpc(p,9);AnnivNpc(p,31);
            Check(!Fenced(p) && AnnivRows(p)==0 && p.Inventory.Count==0 && p.Entity.ConquerPoints==20000,"direct or hidden admission cannot bypass "+state);
        }
        foreach(string field in new[]{"level","reborn","viplevel"}){
            p=AnnivPlayer(town);Query("UPDATE entities SET "+field+"=0 WHERE uid=@uid","uid",(long)p.Entity.UID);AnnivNpc(p,8);Check(Fenced(p) && AnnivRows(p)==0,"durable eligibility is enforced: "+field);
        }
        foreach(string field in new[]{"revorapoint","studypoints","money","heavenblessingtime"}){
            p=FullAnniv(town);bead=AnnivBead(p);Query("UPDATE entities SET "+field+"=COALESCE("+field+",0)+1 WHERE uid=@uid","uid",(long)p.Entity.UID);ItemHandler.UseItem(bead,p);
            Check(Fenced(p) && AnnivRows(p,"completed_day<>''")==0 && p.Inventory.ContainsUID(bead.UID),"stale balance cannot overwrite a newer value: "+field);
        }
        foreach(string change in new[]{"owner","stack","bound","locked","type"}){
            p=FullAnniv(town);bead=AnnivBead(p);string value=change=="owner"?"entityid=1":change=="stack"?"stacksize=2":change=="bound"?"bound=1":change=="locked"?"locked=1":"id=id+1";
            Query("UPDATE items SET "+value+" WHERE uid=@uid","uid",(long)bead.UID);ItemHandler.UseItem(bead,p);Check(Fenced(p) && AnnivRows(p,"completed_day<>''")==0,"stale bead "+change+" is fenced");
        }
        foreach(string state in new[]{"locked","suspicious","expired","stack","rvp","silver","study","blessing"}){
            p=FullAnniv(town);bead=AnnivBead(p);if(state=="locked")bead.Lock=1;if(state=="suspicious")bead.Suspicious=true;if(state=="expired"){bead.Days=1;bead.DayStamp=DateTime.Now.AddDays(-2);}if(state=="stack")bead.StackSize=2;
            if(state=="rvp")p.Entity.RevoraPoint=uint.MaxValue;if(state=="silver")p.Entity.Money=(ulong)long.MaxValue;if(state=="study")p.Entity.SubClasses.StudyPoints=ushort.MaxValue;if(state=="blessing")Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.BlessingBalance"),null,"Set",p,uint.MaxValue);
            EntityTable.SaveEntity(p);ItemHandler.UseItem(bead,p);Check(!Fenced(p) && AnnivRows(p,"completed_day<>''")==0 && p.Inventory.ContainsUID(bead.UID),"unclaimable "+state+" leaves the full reward intact");
        }
        p=FullAnniv(town);while(p.Inventory.Count<22)Pack(p,1088001,1);ItemHandler.UseItem(AnnivBead(p),p);Check(!Fenced(p) && AnnivRows(p,"completed_day<>''")==0,"22-slot bag cannot consume bead and lose part of twenty cards");
        var extra=p.Inventory.Objects.First(i=>i.ID==1088001);p.Inventory.Remove(extra,Enums.ItemUse.Remove);ItemHandler.UseItem(AnnivBead(p),p);Check(!Fenced(p) && AnnivRows(p,"completed_day<>''")==1 && p.Inventory.Count==40,"exact 21-slot bag consumes bead and receives all twenty cards");
        p=FullAnniv(town);bead=AnnivBead(p);ItemHandler.UseItem(new ConquerItem(true){UID=bead.UID,ID=bead.ID},p);Check(AnnivRows(p,"completed_day<>''")==0,"forged matching item object cannot redeem");
        p=FullAnniv(town);p.Entity.SpiritBeads=true;DialyQuestTable.Save(p);ItemHandler.UseItem(AnnivBead(p),p);Check(!Fenced(p) && AnnivRows(p,"completed_day<>'' AND legacy")==1 && p.Entity.RevoraPoint==0,"old same-day completion imports closed without another prize");
        p=FullAnniv(town);Pack(p,729703,1);ItemHandler.UseItem(AnnivBead(p),p);SoulNpc(p,7);ItemHandler.UseItem(SoulBead(p),p);Check(AnnivRows(p)==0 && SoulRuns(p)==0,"mixed legacy beads do not assign shared progress to either service");
        p=AnnivPlayer(town);SoulNpc(p,7);SoulNpc(p,31);AnnivNpc(p,8);Check(SoulDone(p)==1 && p.Inventory.Objects.All(i=>i.ID!=3001172),"daily Soul Ball completion closes Anniversary admission");
        p=AnnivPlayer(town);SoulNpc(p,7);SoulNpc(p,8);AnnivNpc(p,8);Check(AnnivRows(p)==0,"active Soul Ball blocks Anniversary admission");
        p=AnnivPlayer(town);AnnivNpc(p,8);bead=AnnivBead(p);bead.Position=0;bead.Warehouse=1002;Query("UPDATE items SET warehouse=1002 WHERE uid=@uid","uid",(long)bead.UID);p.Inventory.Remove(bead.UID,Enums.ItemUse.None,true);
        SoulNpc(p,7);SoulNpc(p,31);PriceStand(p);AnnivNpc(p,35);Check(AnnivRows(p)==1 && SoulDone(p)==0 && p.Inventory.Count==0,"stored Anniversary bead cannot be replaced or bypassed by a paid daily finish");
        Query("DELETE FROM items WHERE uid=@uid","uid",(long)bead.UID);AnnivNpc(p,35);Check(!Fenced(p) && AnnivBead(p).UID!=bead.UID && AnnivSpirits(p)==3,"lost-bead replacement preserves the original commission and progress");
        p=FullAnniv(town,14999);ItemHandler.UseItem(AnnivBead(p),p);annivNow=annivNow.AddDays(1);Call(Anniversary,null,"OnDeath",HuntMonster(p,"Pheasant"),p.Entity);ItemHandler.UseItem(AnnivBead(p),p);AnnivNpc(p,8);Check(!Fenced(p) && AnnivRows(p)==1 && p.Entity.RevoraPoint==50000,"overnight earned claim consumes its completion day");annivNow=annivNow.AddDays(-1);
        p=FullAnniv(town,12);bead=AnnivBead(p);bead.Bound=true;Query("UPDATE items SET bound=1 WHERE uid=@uid","uid",(long)bead.UID);ItemHandler.UseItem(bead,p);p.Inventory.Remove(bead,Enums.ItemUse.Remove);AnnivNpc(p,35);
        Check(!Fenced(p) && AnnivBead(p).Bound && AnnivSpirits(p)==12,"replacement retains bound legacy bead status and progress");
        p=FullAnniv(town,12);bead=AnnivBead(p);bead.StackSize=0;Query("UPDATE items SET stacksize=0 WHERE uid=@uid","uid",(long)bead.UID);ItemHandler.UseItem(bead,p);Check(!Fenced(p) && AnnivSpirits(p)==12,"legacy zero-stack singleton imports once");
        foreach(string invalid in new[]{"guard","reviver","boss","companion","unregistered","template","alive","other-map","fake-owner"}){
            p=AnnivPlayer(town);AnnivNpc(p,8);dead=HuntMonster(p,"Pheasant");
            if(invalid=="guard")dead.MonsterInfo.Guard=true;if(invalid=="reviver")dead.MonsterInfo.Reviver=true;if(invalid=="boss")dead.MonsterInfo.Boss=true;
            if(invalid=="companion")dead.Companion=true;if(invalid=="unregistered")town.Entities.Remove(dead.UID);if(invalid=="template")dead.MonsterInfo.Name="Counterfeit";
            if(invalid=="alive"){dead.Hitpoints=100;dead.ClearFlagsMobs();}if(invalid=="other-map")dead.MapID=1015;if(invalid=="fake-owner")p.Fake=true;
            Call(Anniversary,null,"OnDeath",dead,p.Entity);Check(!Fenced(p) && AnnivSpirits(p)==3,"non-earning monster or owner cannot add spirits: "+invalid);
        }
        p=AnnivPlayer(town);p.ActiveNpc=900028;
        foreach(int length in new[]{4,12,19,121}){var frame=new byte[length+8];Writer.WriteUInt16((ushort)length,0,frame);Writer.WriteUInt16(2032,2,frame);Writer.WriteUInt64(PacketHandler.ClientSeal,length,frame);if(length>14)frame[14]=8;PacketHandler.HandlePacket(frame,p);}
        var malformed=new byte[28];Writer.WriteUInt16(20,0,malformed);Writer.WriteUInt16(2032,2,malformed);malformed[14]=8;malformed[17]=255;Writer.WriteUInt64(PacketHandler.ClientSeal,20,malformed);PacketHandler.HandlePacket(malformed,p);
        Check(!Fenced(p) && AnnivRows(p)==0 && p.Inventory.Count==0,"bounded Anniversary continuations reject malformed lengths and strings before admission");
        p=FullAnniv(town);ItemHandler.UseItem(AnnivBead(p),p);DialyQuestTable.Load(p);p.Entity.SpiritBeads=false;DialyQuestTable.Save(p);PriceStand(p);SoulNpc(p,7);SoulNpc(p,31);AnnivNpc(p,8);
        Check(!Fenced(p) && AnnivRows(p)==1 && SoulDone(p)==0 && p.Entity.ConquerPoints==20000,"legacy daily reset cannot reopen the committed Anniversary or Soul Ball reward");
        p=FullAnniv(town,14999);ItemHandler.UseItem(AnnivBead(p),p);var pendingClaim=p;bead=AnnivBead(p);Call(Anniversary,null,"OnDeath",HuntMonster(p,"Pheasant"),p.Entity);
        Parallel.Invoke(()=>ItemHandler.UseItem(bead,pendingClaim),()=>{PriceStand(pendingClaim);SoulNpc(pendingClaim,7);SoulNpc(pendingClaim,31);});
        Check(!Fenced(p) && AnnivRows(p,"completed_day<>''")==1 && SoulDone(p)==0 && p.Entity.ConquerPoints==20000,"concurrent Anniversary claim and paid Soul Ball finish share one allowance and cannot charge twice");
        p=AnnivPlayer(town);SoulNpc(p,7);SoulNpc(p,31);annivNow=annivNow.AddDays(1);DialyQuestTable.Load(p);p.Entity.SpiritBeads=false;DialyQuestTable.Save(p);
        Pack(p,3001172,1);p.Entity.spiritBeadsCount=15000;DialyQuestTable.Save(p);ItemHandler.UseItem(AnnivBead(p),p);Call(SoulBall,null,"Refresh",p);
        Check(!Fenced(p) && AnnivRows(p,"completed_day<>''")==1 && (bool)Call(SoulBall,null,"CompletedToday",p),"older daily Soul Ball history cannot hide today's Anniversary completion in the shared menu");annivNow=annivNow.AddDays(-1);
        // Full configured hunt, without preloading spirit progress.
        p=AnnivPlayer(town);AnnivNpc(p,8);World(runtime,1015);p.BindWorldMap(1015);var xy=FlowerTile(p.Map,396,528);p.Entity.X=xy.Item1;p.Entity.Y=xy.Item2;
        string source=MonsterInformation.MonsterInformations.Values.First(m=>m.Level>=120 && !m.Guard && !m.Reviver && !m.Boss).Name;
        Call(MonsterLoot,null,"Initialize");MonsterLoot.GetField("Roll",Hidden).SetValue(null,(Func<int,int>)(max=>max-1));
        bool oldMoney=Config.MoneyDropEnabled,oldCp=Config.KillConquerPointsEnabled;Config.MoneyDropEnabled=false;Config.KillConquerPointsEnabled=false;
        try{var first=HuntMonster(p,source);first.Hitpoints=100;first.ClearFlagsMobs();first.Die(p.Entity);Check(!Fenced(p) && AnnivSpirits(p)==7,"production monster death dispatch credits the Anniversary ledger once");}
        finally{Config.MoneyDropEnabled=oldMoney;Config.KillConquerPointsEnabled=oldCp;}
        for(int n=1;n<3750;n++)Call(Anniversary,null,"OnDeath",HuntMonster(p,source),p.Entity);
        Check(!Fenced(p) && AnnivSpirits(p)==15000,"fresh issued bead fills its complete 15,000-spirit requirement through 3,750 registered wilderness deaths");
        p=ReloadBall(p);ItemHandler.UseItem(AnnivBead(p),p);Check(!Fenced(p) && p.Inventory.Count==21 && p.Inventory.Objects.Count(i=>i.ID>=800000 && i.ID<900000 && i.Bound && ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7)==1 && p.Entity.RevoraPoint==50000 && p.Entity.SubClasses.StudyPoints==100,"full earned route reloads and redeems the entire prize");
            VerifyStudyAward(p,"anniversary");
        town.Npcs.Remove(900028);
    }
}
