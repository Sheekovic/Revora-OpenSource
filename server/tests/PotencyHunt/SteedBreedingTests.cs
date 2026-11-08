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
    static readonly Type Breeding=typeof(Entity).Assembly.GetType("Revora.Game.Features.SteedBreeding");
    static byte[] BreedFrame(byte mode,params uint[] ids)
    {
        var b=new byte[16+4*ids.Length];Writer.WriteUInt16((ushort)(b.Length-8),0,b);Writer.WriteUInt16(2036,2,b);
        b[4]=mode;b[5]=(byte)ids.Length;for(int i=0;i<ids.Length;i++)Writer.WriteUInt32(ids[i],8+4*i,b);
        Writer.WriteUInt64(PacketHandler.ClientSeal,b.Length-8,b);return b;
    }
    static ConquerItem BreedMount(GameState p,byte plus=6,uint progress=0,uint color=0xff0000,bool bound=false)
    {
        var i=Pack(p,300000,1);i.Plus=plus;i.PlusProgress=progress;i.SocketProgress=color;i.Bound=bound;
        i.NextRed=(byte)(color>>16);i.NextBlue=(byte)(color>>8);i.NextGreen=(byte)color;
        Query("UPDATE items SET plus=@plus,plusprogress=@progress,socketprogress=@color,nextsteedcolor=@color,bound=@bound WHERE uid=@uid",
            "plus",(int)plus,"progress",(long)progress,"color",(long)color,"bound",bound?1:0,"uid",(long)i.UID);return i;
    }
    static long BreedCount(GameState p){return Scalar("SELECT count(*) FROM revora_steed_breedings WHERE uid=@uid",p.Entity.UID);}
    static void Breed(GameState p,byte mode,params uint[] ids){PacketHandler.HandlePacket(BreedFrame(mode,ids),p);}
    static void VerifySteedBreeding(string runtime,Map town)
    {
        InitializeHousingFixture(runtime,town);Call(Breeding,null,"Initialize");Call(Breeding,null,"Initialize");
        var layout=new ConquerItem(true);layout.PlusProgress=2000000;layout.Suspicious=true;layout.NextGreen=255;
        Check(layout.PlusProgress==2000000&&layout.Suspicious&&System.BitConverter.ToUInt16(layout.ToArray(),38)==1
            &&System.BitConverter.ToUInt32(layout.ToArray(),34)==255&&System.BitConverter.ToUInt32(layout.ToArray(),42)==2000000,
            "native item fields keep suspicion, green channel and lineage independent");
        layout.Suspicious=false;Check(layout.PlusProgress==2000000&&!layout.Suspicious,"clearing suspicion preserves high lineage progress");
        foreach(byte mode in new byte[]{2,3})foreach(bool bound in new[]{false,true})
        {
            var p=HousePlayer(town);var main=BreedMount(p,6,200,0xff0000,bound);var minor=BreedMount(p,6,100,0x00ff00,bound);
            uint id=main.UID;ulong silver=p.Entity.Money;uint cp=p.Entity.ConquerPoints;
            Parallel.For(0,12,n=>Breed(p,mode,id,minor.UID));uint color=mode==2?0xff0000u:0xe51900u;
            Check(!Fenced(p)&&BreedCount(p)==1&&main.Plus==6&&main.PlusProgress==3540&&main.SocketProgress==color
                &&p.Inventory.Count==1&&p.Entity.Money==silver&&p.Entity.ConquerPoints==cp&&HasAchievement(p,10112),"both breeding choices consume owned parents once with retained progress and achievement: "+mode+"/"+bound);
            p=ReloadBall(p);new TitleStorage().Login(p);Breed(p,mode,id,minor.UID);main=p.Inventory.Objects.Single();
            Check(!Fenced(p)&&BreedCount(p)==1&&main.UID==id&&main.Plus==6&&main.PlusProgress==3540&&main.SocketProgress==color&&main.Bound==bound&&HasAchievement(p,10112),"breeding survives production reload and replay");
        }
        uint[] thresholds={20,20,90,240,720,2160,6480,19440,58320,2700,5500,9000};
        for(byte level=0;level<12;level++)
        {
            var p=HousePlayer(town);var main=BreedMount(p,level,thresholds[level]-1);var minor=BreedMount(p,0);
            Breed(p,2,main.UID,minor.UID);Check(!Fenced(p)&&BreedCount(p)==1&&main.Plus==level+1&&main.PlusProgress==0,"exact lineage boundary "+level);
        }
        var higher=HousePlayer(town);var low=BreedMount(higher,0);var high=BreedMount(higher,8,100);
        Breed(higher,2,low.UID,high.UID);Check(!Fenced(higher)&&low.Plus==8&&low.PlusProgress==90,"higher minor and saved progress cross multiple exact thresholds");
        var capped=HousePlayer(town);var max=BreedMount(capped,12,1999999);var one=BreedMount(capped,0);
        Breed(capped,2,max.UID,one.UID);Check(!Fenced(capped)&&max.Plus==12&&max.PlusProgress==2000000&&BreedCount(capped)==1,"explicit +12 retained progress cap");
        var spare=BreedMount(capped,0);Breed(capped,2,max.UID,spare.UID);
        Check(capped.Inventory.ContainsUID(spare.UID)&&BreedCount(capped)==1,"no-effect request does not consume a parent");
        capped=ReloadBall(capped);max=capped.Inventory.Objects.Single(i=>i.UID==max.UID);
        Check(max.PlusProgress==2000000&&!max.Suspicious,"high lineage survives production item deserialization without false suspicion");
        var equipped=ReloadBall(HousePlayer(town));var worn=BreedMount(equipped,6,0,0x00ffff);var part=BreedMount(equipped,6,0,0xff0000);
        ItemHandler.EquipItem(new ItemUsage(true){UID=worn.UID,dwParam=ConquerItem.Steed,Action=ItemUsage.Mode.EquipItem},equipped);
        Check(worn.Position==ConquerItem.Steed,"ordinary equipment path installs the main mount");
        Breed(equipped,3,worn.UID,part.UID);
        Check(!Fenced(equipped)&&BreedCount(equipped)==1&&worn.PlusProgress==3240&&worn.SocketProgress==0x19e5e5,
            "equipped main breeding refreshes lineage and color through the shared service");
        equipped=ReloadBall(equipped);var reloaded=equipped.Equipment.TryGetItem(ConquerItem.Steed);
        Check(reloaded!=null&&reloaded.UID==worn.UID&&reloaded.PlusProgress==3240&&reloaded.NextGreen==229,
            "equipped bred mount and all preview channels survive reload");
        var genes=HousePlayer(town);var parent=BreedMount(genes,6,0,0);var donor=BreedMount(genes,0,0,0xff0000);
        donor.NextRed=0;donor.NextBlue=255;donor.NextGreen=0;
        Query("UPDATE items SET nextsteedcolor=65280 WHERE uid=@uid","uid",(long)donor.UID);
        Breed(genes,3,parent.UID,donor.UID);
        Check(!Fenced(genes)&&parent.SocketProgress==0x001900,"breeding uses the minor preview channels read by the native panel");
        var mixed=HousePlayer(town);var kept=BreedMount(mixed,6,0,0,true);var free=BreedMount(mixed,6);
        Breed(mixed,2,kept.UID,free.UID);
        Check(!Fenced(mixed)&&kept.Bound&&kept.PlusProgress==3240&&mixed.Inventory.Count==1,"unbound parent may improve a bound main without releasing bound value");
        var batch=HousePlayer(town);var root=BreedMount(batch,6,200,0xff0000);var first=BreedMount(batch,6,100,0x00ff00);var last=BreedMount(batch,0,0,0);
        Query("CREATE FUNCTION suppress_last_parent() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN IF OLD.uid="+last.UID+" THEN RETURN NULL; END IF; RETURN OLD; END';CREATE TRIGGER suppress_last_parent BEFORE DELETE ON items FOR EACH ROW EXECUTE FUNCTION suppress_last_parent()");
        Breed(batch,3,root.UID,first.UID,last.UID);
        Check(Fenced(batch)&&BreedCount(batch)==0&&batch.Inventory.Count==3&&Scalar("SELECT count(*) FROM items WHERE entityid=@uid",batch.Entity.UID)==3,
            "failure consuming the final parent restores earlier parents and the entire result");
        Query("DROP TRIGGER suppress_last_parent ON items;DROP FUNCTION suppress_last_parent()");batch=ReloadBall(batch);
        Breed(batch,3,root.UID,first.UID,last.UID);var result=batch.Inventory.Objects.Single();
        Check(!Fenced(batch)&&BreedCount(batch)==1&&result.PlusProgress==3541&&result.SocketProgress==0xce1600
            &&Scalar("SELECT count(*) FROM revora_steed_parents p JOIN revora_steed_breedings b ON b.id=p.breeding WHERE b.uid=@uid",batch.Entity.UID)==2,
            "multiple parents retain request order, exact contributions and individual provenance");
        foreach(string bad in new[]{"duplicate","self","foreign","item-type","bound","locked","warehouse","position","suspicious","expired","stack","minor-nine","bad-progress","trade","dead","fake","stale","legacy-mode","zero"})
        {
            var p=HousePlayer(town);var main=BreedMount(p);var minor=BreedMount(p);uint minorId=minor.UID;
            if(bad=="foreign")minorId=uint.MaxValue;
            if(bad=="item-type")minor.ID=1088001;
            if(bad=="bound")minor.Bound=true;
            if(bad=="locked")minor.Lock=1;
            if(bad=="warehouse")minor.Warehouse=8;
            if(bad=="position")minor.Position=12;
            if(bad=="suspicious")minor.Suspicious=true;
            if(bad=="expired")minor.Days=1;
            if(bad=="stack")minor.StackSize=2;
            if(bad=="minor-nine")minor.Plus=9;
            if(bad=="bad-progress")minor.PlusProgress=uint.MaxValue;
            if(bad=="trade")p.Trade.InTrade=true;
            if(bad=="dead")p.Entity.Hitpoints=0;
            if(bad=="fake")p.Fake=true;
            if(bad=="stale")Query("UPDATE items SET plusprogress=1 WHERE uid=@uid","uid",(long)minor.UID);
            if(bad=="zero")minorId=0;
            if(bad=="self")minorId=main.UID;
            if(bad=="duplicate")Breed(p,3,main.UID,minorId,minorId);else Breed(p,bad=="legacy-mode"?(byte)0:(byte)3,main.UID,minorId);
            Check(BreedCount(p)==0&&main.Plus==6&&main.PlusProgress==0&&main.SocketProgress==0xff0000&&p.Inventory.Count==2
                &&Scalar("SELECT count(*) FROM items WHERE entityid=@uid",p.Entity.UID)==2,"ineligible breeding preserves parents and result: "+bad);
        }
        foreach(string fault in new[]{"receipt","parent","consume","main","achievement"})
        {
            var p=HousePlayer(town);var main=BreedMount(p);var minor=BreedMount(p);
            string table=fault=="receipt"?"revora_steed_breedings":fault=="parent"?"revora_steed_parents":fault=="achievement"?"revora_achievement_evidence":"items";
            string verb=fault=="consume"?"DELETE":fault=="main"?"UPDATE":"INSERT";
            Query("CREATE FUNCTION suppress_breed() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_breed BEFORE "+verb+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_breed()");
            Breed(p,3,main.UID,minor.UID);
            Check(Fenced(p)&&BreedCount(p)==0&&p.Inventory.Count==2&&main.PlusProgress==0&&main.SocketProgress==0xff0000
                &&Scalar("SELECT count(*) FROM items WHERE entityid=@uid",p.Entity.UID)==2,"suppressed "+fault+" rolls back complete breeding");
            Query("DROP TRIGGER suppress_breed ON "+table+";DROP FUNCTION suppress_breed()");
            p=ReloadBall(p);Breed(p,3,main.UID,minor.UID);
            Check(!Fenced(p)&&BreedCount(p)==1&&p.Inventory.Count==1&&HasAchievement(p,10112),"reload retries whole breeding after "+fault);
        }
    }
    static void VerifySteedEarning(string runtime,Map town)
    {
        // Reuse the real Elite acquisition regression, including fault paths.
        VerifyElitePrizes(runtime,town);Query("ALTER TABLE entities ADD PRIMARY KEY(uid)");Call(Breeding,null,"Initialize");
        var winner=BallPlayer(town);winner.Entity.Level=140;winner.Entity.Experience=0;EntityTable.SaveEntity(winner);
        for(int week=0;week<2;week++)
        {
            epkNow=new DateTime(2027,2,6,3,55,0,DateTimeKind.Utc).AddDays(7*week);
            var e=NewEpk(runtime);Call(Epk,e,"Tick");
            var entrants=new System.Collections.Generic.List<GameState>{winner};
            for(int n=0;n<7;n++){var rival=BallPlayer(town);rival.Entity.Level=140;EntityTable.SaveEntity(rival);entrants.Add(rival);}
            foreach(var p in entrants){EliteDesk(p);EpkJoin(p);}
            Check(entrants.All(p=>p.Entity.MapID!=1002),"eight ordinary entrants join breeding-parent tournament "+week);
            EpkTick(e,660);int guard=0;
            while(EpkGroups(e).Any(g=>!(bool)Field(g,"Done")))
            {
                if(++guard>16)throw new Exception("Breeding prize tournament did not settle.");EpkTick(e,11);EpkTick(e,11);
                foreach(var match in EpkMatches(e).Where(m=>(bool)Field(m,"Imported")&&Field(m,"Winner")==null).ToArray())
                {
                    var a=EpkFighter(match,"Left");var b=EpkFighter(match,"Right");
                    if(b.Entity.UID==winner.Entity.UID){var swap=a;a=b;b=swap;}
                    EpkDamage(a,b,100);b.Entity.Die(a.Entity);
                }
                EpkTick(e,1);
            }
            EliteCollect(winner);
            Check(!Fenced(winner)&&EliteClaims(winner)==week+1&&winner.Inventory.Objects.Count(i=>i.ID==720049)==1,
                "real championship grants one owned steed pack "+week+" claims="+EliteClaims(winner)+" bag="+winner.Inventory.Count+" fenced="+Fenced(winner)+" placements="+Scalar("SELECT count(*) FROM revora_epk_placements WHERE uid=@uid AND place=1",winner.Entity.UID));
            ItemHandler.UseItem(winner.Inventory.Objects.Single(i=>i.ID==720049),winner);winner=ReloadBall(winner);winner.CalculateStatBonus();winner.CalculateHPBonus();
            Check(!Fenced(winner)&&winner.Inventory.Objects.Count(i=>i.ID==300000)==week+1,
                "earned parent mount survives pack use and reconnect "+week);
            if(week==0)
            {
                MiningNpc(town,8);AdventureNear(winner,8);winner.ActiveNpc=8;
                var stored=winner.Inventory.Objects.Where(i=>i.ID!=300000).Select(i=>i.UID).ToArray();
                foreach(uint item in stored)StoreRequest(winner,1,item);
                winner=ReloadBall(winner);winner.CalculateStatBonus();winner.CalculateHPBonus();
                Check(!Fenced(winner)&&winner.Inventory.Count==1&&stored.All(id=>TownStorage(winner).GetItem(id)!=null),
                    "ordinary warehouse deposits preserve first-week rewards and free room for the second prize");
            }
        }
        var parents=winner.Inventory.Objects.Where(i=>i.ID==300000).OrderBy(i=>i.UID).ToArray();
        ulong money=winner.Entity.Money;uint cp=winner.Entity.ConquerPoints;int count=winner.Inventory.Count;
        Breed(winner,3,parents[0].UID,parents[1].UID);winner=ReloadBall(winner);new TitleStorage().Login(winner);
        var mount=winner.Inventory.Objects.Single(i=>i.ID==300000);
        Check(!Fenced(winner)&&BreedCount(winner)==1&&mount.UID==parents[0].UID&&mount.Plus==6&&mount.PlusProgress==3240
            &&mount.SocketProgress==0xfe9600&&winner.Inventory.Count==count-1&&winner.Entity.Money==money&&winner.Entity.ConquerPoints==cp&&HasAchievement(winner,10112),
            "two earned championships produce one persistent bred mount and achievement without fixture parents or extra charges");
        Check(Scalar(@"SELECT count(*) FROM revora_epk_nested_openings n JOIN revora_epk_prize_claims c ON c.uid=n.uid AND n.pack_uid=ANY(c.rewards)
JOIN revora_epk_placements p ON p.run=c.run AND p.uid=c.uid
WHERE n.uid=@uid AND n.result_type=300000 AND p.place=1 AND (n.result_uid IN(SELECT main_uid FROM revora_steed_breedings WHERE uid=@uid)
OR n.result_uid IN(SELECT item_uid FROM revora_steed_parents m JOIN revora_steed_breedings b ON b.id=m.breeding WHERE b.uid=@uid))",winner.Entity.UID)==2,
            "both breeding parents join back to owned pack openings and earned champion claims");
    }

}
