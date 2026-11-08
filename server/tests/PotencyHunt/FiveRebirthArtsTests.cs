using System;
using System.Linq;
using Revora;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static void ChainBoltLesson(Revora.Client.GameState p,byte option=61) {
        var npc=Kernel.Maps[1004].Npcs[10000];p.Entity.Teleport(1004,(ushort)(npc.X-2),npc.Y);p.ActiveNpc=10000;
        if(!EntityTable.SaveEntity(p))throw new Exception("Chain Bolt training profile save failed");SoulWire(p,option,10000);
    }
    static void InitializeChainBolt(string runtime) {
        var job=World(runtime,1004);World(runtime,3933);Query("CREATE TABLE npcs AS TABLE public.npcs WITH NO DATA");
        using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new Npgsql.NpgsqlCommand("SELECT mapid,cellx,celly,lookface FROM public.npcs WHERE id=10000",c))
        using(var r=q.ExecuteReader()){if(!r.Read() || Convert.ToUInt16(r[0])!=1004)throw new Exception("Taoist Moon catalog missing");job.AddNpc(new NpcSpawn{UID=10000,MapID=1004,X=Convert.ToUInt16(r[1]),Y=Convert.ToUInt16(r[2]),Mesh=Convert.ToUInt16(r[3]),Type=Enums.NpcType.Talker});}}
        Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.TaoistEpicQuest"),null,"Initialize");
    }
    static void VerifyChainBoltGuards(Map town) {
        foreach(byte option in new byte[]{61,208,210})foreach(byte cls in new byte[]{15,75,101,131,141,132,142}) {
            var p=ReloadBall(WelcomePlayer(town,40,0,cls));ChainBoltLesson(p,option);bool valid=cls==132 || cls==142;
            Check(!Fenced(p) && p.Spells.ContainsKey(10309)==valid && Scalar("SELECT count(*) FROM revora_taoist_training WHERE uid=@uid AND spell=10309",p.Entity.UID)==(valid?1:0),"Chain Bolt option validates both promoted Taoist paths: "+option+"/"+cls);
            if(valid){p.Spells[10309].Level=1;p.Spells[10309].PreviousLevel=2;p.Spells[10309].Experience=90;SkillTable.SaveSpells(p);ChainBoltLesson(p,option);p=ReloadBall(p);
                Check(p.Spells[10309].Level==1 && p.Spells[10309].PreviousLevel==2 && p.Spells[10309].Experience==90 && Scalar("SELECT count(*) FROM revora_taoist_training WHERE uid=@uid AND spell=10309",p.Entity.UID)==1,"trainer replay preserves saved Chain Bolt metadata");}
        }
        foreach(string mode in new[]{"low","remote","dead","receipt"}) {
            var p=ReloadBall(WelcomePlayer(town,mode=="low"?(byte)39:(byte)40,0,132));var npc=Kernel.Maps[1004].Npcs[10000];
            p.Entity.Teleport(1004,(ushort)(npc.X-2),npc.Y);p.ActiveNpc=10000;
            if(mode=="remote")p.Entity.X+=20;if(mode=="dead")p.Entity.Hitpoints=0;EntityTable.SaveEntity(p);
            if(mode=="receipt")Query("CREATE FUNCTION reject_chain_training() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER reject_chain_training BEFORE INSERT ON revora_taoist_training FOR EACH ROW EXECUTE FUNCTION reject_chain_training()");
            try {SoulWire(p,61,10000);}finally{if(mode=="receipt")Query("DROP TRIGGER reject_chain_training ON revora_taoist_training;DROP FUNCTION reject_chain_training()");}
            Check(!p.Spells.ContainsKey(10309) && Scalar("SELECT count(*) FROM skills WHERE entityid=@uid AND id=10309",p.Entity.UID)==0 && Scalar("SELECT count(*) FROM revora_taoist_training WHERE uid=@uid AND spell=10309",p.Entity.UID)==0,"invalid or failed Chain Bolt training leaves no skill/receipt: "+mode);
            if(mode=="receipt"){Check(Fenced(p),"failed training fences the session");p=ReloadBall(p);ChainBoltLesson(p);Check(!Fenced(p) && p.Spells.ContainsKey(10309),"reconnected training retries rejected transaction");}
        }
    }
    static void InitializePirateLessons(string runtime) {
        var job=Kernel.Maps[1004];string privateRuntime=System.IO.Path.GetFullPath("C:/Revora/DisposablePostgres/moon-box-candidate/game");World(privateRuntime,10271);
        using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new Npgsql.NpgsqlCommand("SELECT mapid,cellx,celly,lookface FROM public.npcs WHERE id=4272",c))
        using(var r=q.ExecuteReader()){if(!r.Read() || Convert.ToUInt16(r[0])!=1004)throw new Exception("Pirate instructor catalog missing");job.AddNpc(new NpcSpawn{UID=4272,MapID=1004,X=Convert.ToUInt16(r[1]),Y=Convert.ToUInt16(r[2]),Mesh=Convert.ToUInt16(r[3]),Type=Enums.NpcType.Talker});}}
        // Reuse the existing private terrain, validated by the production initializer.
        // No files are copied into the development or live runtime.
        string before=Environment.CurrentDirectory;
        try {Environment.CurrentDirectory=privateRuntime;Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.PirateEpicWorld"),null,"Initialize");}
        finally {Environment.CurrentDirectory=before;}
    }
    static void WindstormLesson(Revora.Client.GameState p) {
        var npc=Kernel.Maps[1004].Npcs[4272];p.Entity.Teleport(1004,(ushort)(npc.X-2),npc.Y);p.ActiveNpc=4272;
        if(!EntityTable.SaveEntity(p))throw new Exception("Windstorm training profile save failed");SoulWire(p,15,4272);
    }
    static void LearnPirateRiteArts(Revora.Client.GameState p) {
        WindstormLesson(p);Check(p.Spells.ContainsKey(11140),"ordinary Pirate instructor teaches Windstorm before rite");
        foreach(uint id in new uint[]{3307432,3307433,3307434}){
            var scroll=Pack(p,id,1);ItemUsage.Process(PrayerPacket(scroll.UID),p);
            Check(!Fenced(p) && !p.Inventory.ContainsUID(scroll.UID),"ordinary item-use consumes supplied Pirate scroll: "+id);
            var spare=Pack(p,id,1);ItemUsage.Process(PrayerPacket(spare.UID),p);Check(p.Inventory.ContainsUID(spare.UID),"already learned Pirate art preserves spare scroll: "+id);
        }
        Check(new ushort[]{11140,14680,14710,14720}.All(id=>p.Spells.ContainsKey(id)) && Scalar("SELECT count(*) FROM revora_pirate_training WHERE uid=@uid",p.Entity.UID)==4,"all four Pirate arts have actual training receipts");
    }
    static void VerifyWindstormGuards(Map town) {
        foreach(string mode in new[]{"success","wrong-class","low","remote","receipt"}){
            var p=ReloadBall(WelcomePlayer(town,mode=="low"?(byte)14:(byte)15,0,mode=="wrong-class"?(byte)15:(byte)70));
            var npc=Kernel.Maps[1004].Npcs[4272];p.Entity.Teleport(1004,(ushort)(npc.X-2),npc.Y);p.ActiveNpc=4272;if(mode=="remote")p.Entity.X+=20;EntityTable.SaveEntity(p);
            if(mode=="receipt")Query("CREATE FUNCTION reject_wind_training() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER reject_wind_training BEFORE INSERT ON revora_pirate_training FOR EACH ROW EXECUTE FUNCTION reject_wind_training()");
            try{System.Threading.Tasks.Parallel.For(0,8,n=>SoulWire(p,15,4272));}finally{if(mode=="receipt")Query("DROP TRIGGER reject_wind_training ON revora_pirate_training;DROP FUNCTION reject_wind_training()");}
            bool success=mode=="success";Check(p.Spells.ContainsKey(11140)==success && Scalar("SELECT count(*) FROM skills WHERE entityid=@uid AND id=11140",p.Entity.UID)==(success?1:0) && Scalar("SELECT count(*) FROM revora_pirate_training WHERE uid=@uid",p.Entity.UID)==(success?1:0),"Windstorm admission, concurrency and rollback: "+mode);
            if(success){p.Spells[11140].Level=1;p.Spells[11140].PreviousLevel=2;p.Spells[11140].Experience=90;SkillTable.SaveSpells(p);WindstormLesson(p);p=ReloadBall(p);Check(p.Spells[11140].Level==1 && p.Spells[11140].PreviousLevel==2 && p.Spells[11140].Experience==90,"Windstorm replay and reload preserve trained metadata");}
            if(mode=="receipt"){Check(Fenced(p),"failed Windstorm transaction fences session");p=ReloadBall(p);WindstormLesson(p);Check(!Fenced(p) && p.Spells.ContainsKey(11140),"Windstorm reconnect retries failed training");}
        }
    }
    static void PirateForceCast(Revora.Client.GameState p) {
        p.Entity.AttackStamp=default(Time32);
        new Revora.Game.Attacking.Handle(new Attack(true){AttackType=Attack.Magic,Decoded=true,Damage=14710,Attacker=p.Entity.UID,Attacked=p.Entity.UID,X=p.Entity.X,Y=p.Entity.Y},p.Entity,p.Entity);
    }
    static void VerifyPirateRiteConcurrency(Map town) {
        foreach(int rite in new[]{0,1,2}) {
            var p=rite==0?RebirthPlayer(town,75):rite==1?SecondPlayer(town,75,75):PaidPlayer(town,71);
            LearnPirateRiteArts(p);
            p.Entity.Teleport(1002,(ushort)(town.Npcs[59558].X-2),(ushort)(town.Npcs[59558].Y-2));p.ActiveNpc=59558;
            var castInfo=SpellTable.GetSpell(14710,0);
            RebirthGear(p,ConquerItem.RightWeapon,ConquerItemInformation.BaseInformations.Keys.First(id=>castInfo.OnlyWithThisWeaponSubtype.Contains((ushort)(id/1000))));p.Weapons=p.ComputeWeapons();
            p.Entity.Stamina=100;p.Entity.Mana=10000;p.Attackable=true;EntityTable.SaveEntity(p);
            PirateForceCast(p);
            Check(p.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.ImmortalForce),"actual Pirate cast succeeds before rite: "+rite);
            p.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.ImmortalForce);
            var target=LootMonster(p);target.Hitpoints=target.MaxHitpoints=100000;p.Screen.Add(target);
            var own=MonkPillar(p,FloorItem.SeaBurial);own.X=target.X;own.Y=target.Y;own.X2=target.X;own.Y2=(ushort)(target.Y+1);
            if(rite==0)RebirthOffer(p,11);else if(rite==1)SecondOffer(p,11);else PaidOffer(p);
            p.Spells[14710].ColdTime=default(Time32);
            Exception pulseError=null,castError=null;
            var cast=new System.Threading.Thread(()=>{try{PirateForceCast(p);}catch(Exception e){castError=e;}}){IsBackground=true};
            var pulse=new System.Threading.Thread(()=>{try{Revora.Game.Attacking.Handle.SeaBurial(own);}catch(Exception e){pulseError=e;}}){IsBackground=true};
            try {
                lock(p.ItemSyncRoot) {
                    cast.Start();
                    Check(System.Threading.SpinWait.SpinUntil(()=>!cast.IsAlive || (cast.ThreadState & System.Threading.ThreadState.WaitSleepJoin)!=0,5000) && !p.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.ImmortalForce),"contended actual cast cannot activate during rite: "+rite);
                    pulse.Start();
                    Check(System.Threading.SpinWait.SpinUntil(()=>(pulse.ThreadState & System.Threading.ThreadState.WaitSleepJoin)!=0,5000),"delayed Pirate pulse waits on rite ownership: "+rite);
                    if(rite==0)StoneNpc(p,214);else if(rite==1)StoneNpc(p,180);else PaidRequest(p,11);
                    Check(!Fenced(p) && p.Entity.Class==11,"actual rite commits while delayed pulse waits: "+rite);
                }
            }finally{
                if(cast.IsAlive && !cast.Join(5000))throw new Exception("Pirate cast did not release after rite");
                if(pulse.IsAlive && !pulse.Join(5000))throw new Exception("Pirate pulse did not release after rite");
            }
            if(castError!=null)throw new Exception("Concurrent Pirate cast failed",castError);
            if(pulseError!=null)throw new Exception("Concurrent Pirate pulse failed",pulseError);
            Check(target.Hitpoints==100000 && !PillarRegistered(own) && !p.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.ImmortalForce),"queued Pirate pulse cannot damage or restore effects after committed rite: "+rite);
            PirateForceCast(p);
            Check(!p.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.ImmortalForce),"actual post-rite cast cannot restore removed Pirate art: "+rite);
        }
    }
    static void PirateJourneyNpc(Revora.Client.GameState p,uint id,byte option) {
        var npc=Kernel.Maps[10271].Npcs[id];p.Entity.Teleport(10271,(ushort)(npc.X-2),npc.Y);p.ActiveNpc=id;
        if(!EntityTable.SaveEntity(p))throw new Exception("Pirate journey approach save failed");
        SoulWire(p,option,id);
    }
    static void VerifyEarnedPirateScrollRite(Map town) {
        var store=typeof(Entity).Assembly.GetType("Revora.Game.Features.PirateEpicStore");
        var draw=store.GetField("SearchDraw",Hidden);var original=draw.GetValue(null);
        // Exercise the documented consolation branch deterministically, not its odds.
        draw.SetValue(null,(Func<int,int>)(maximum=>maximum-1));
        try {foreach(byte next in new byte[]{71,11}) {
            var p=PaidPlayer(town,71);uint initialCp=p.Entity.ConquerPoints;WindstormLesson(p);
            SoulWire(p,150,4272);Check(!Fenced(p) && p.Entity.MapID==10271,"ordinary eligible Pirate enters scroll quest through instructor");
            foreach(byte choice in new byte[]{1,2,3}) {
                for(int n=0;n<10;n++) {
                    PirateJourneyNpc(p,199590,1);SoulWire(p,20,199590);SoulWire(p,20,199590);
                    Check(!Fenced(p) && p.Inventory.Objects.Count(i=>i.ID==3307443)==1,"one paid chart survives duplicate purchase confirmation");
                    PirateJourneyNpc(p,199592,1);SoulWire(p,20,199592);SoulWire(p,20,199592);
                    Check(!Fenced(p) && !p.Inventory.Objects.Any(i=>i.ID==3307443) && p.Inventory.Objects.Where(i=>i.ID==3307450).Sum(i=>Math.Max(1,(int)i.StackSize))==n+1,"searched chart earns exactly one Water despite duplicate request");
                }
                p=ReloadBall(p);
                Check(p.Inventory.Objects.Where(i=>i.ID==3307450).Sum(i=>Math.Max(1,(int)i.StackSize))==10 && p.Inventory.Objects.Where(i=>i.ID==3307450).All(i=>i.Bound),"ten earned bound Water persist across reconnect");
                PirateJourneyNpc(p,199591,choice);SoulWire(p,20,199591);SoulWire(p,20,199591);
                uint type=3307431u+choice;var scroll=p.Inventory.Objects.Single(i=>i.ID==type);uint scrollUid=scroll.UID;
                Check(!Fenced(p) && scroll.Bound && !p.Inventory.Objects.Any(i=>i.ID==3307450),"Mermaid exchanges earned Water once for a bound scroll");
                p=ReloadBall(p);ItemUsage.Process(PrayerPacket(scrollUid),p);ItemUsage.Process(PrayerPacket(scrollUid),p);
                ushort skill=choice==1?(ushort)14680:choice==2?(ushort)14720:(ushort)14710;
                Check(!Fenced(p) && p.Spells.ContainsKey(skill) && !p.Inventory.ContainsUID(scrollUid)
                    && Scalar("SELECT count(*) FROM revora_pirate_training t JOIN revora_pirate_scroll_exchanges e ON e.uid=t.uid AND e.scroll_uid=t.scroll_uid AND e.scroll_type=t.scroll_type WHERE t.uid=@uid",p.Entity.UID)==choice,"learned art links to its actually exchanged scroll and resists replay");
            }
            Check(p.Entity.ConquerPoints==initialCp-810 && Scalar("SELECT count(*) FROM revora_pirate_map_purchases WHERE uid=@uid",p.Entity.UID)==30
                && Scalar("SELECT count(*) FROM revora_pirate_searches WHERE uid=@uid",p.Entity.UID)==30
                && Scalar("SELECT count(*) FROM revora_pirate_scroll_exchanges WHERE uid=@uid",p.Entity.UID)==3,"earned scroll journey records exact chart costs, searches and exchanges");
            p.Entity.Teleport(1002,(ushort)(town.Npcs[59558].X-2),(ushort)(town.Npcs[59558].Y-2));p.ActiveNpc=59558;
            EntityTable.SaveEntity(p);PaidOffer(p);PaidRequest(p,next);PaidRequest(p,next);
            Check(!Fenced(p) && p.Entity.Class==next && PaidCount(p)==1 && p.Entity.ConquerPoints==initialCp-810-Config.SerethReincarnationCp,"earned-scroll character pays for exactly one actual reincarnation");
            p=ReloadBall(p);
            Check(new ushort[]{11140,14680,14710,14720}.All(id=>p.Spells.ContainsKey(id)==(next==71))
                && p.Entity.ConquerPoints==initialCp-810-Config.SerethReincarnationCp,"reconnect preserves earned art retention or removal and exact CP balance");
        }}finally{draw.SetValue(null,original);}
    }
    static void VerifyFiveRebirthArts(string runtime,Map town) {
        Query("CREATE TABLE IF NOT EXISTS reincarnation(id bigserial PRIMARY KEY,uid bigint NOT NULL,level bigint NOT NULL,experience bigint NOT NULL)");
        Call(StoneCraft,null,"Initialize");Call(WaterLoot,null,"Initialize");
        FirstRite.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>rebirthNow));
        SecondRite.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>secondNow));
        PaidRite.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>paidNow));
        using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new Npgsql.NpgsqlCommand("SELECT mapid,cellx,celly,lookface FROM public.npcs WHERE id=59558",c))
        using(var r=q.ExecuteReader()){if(!r.Read() || Convert.ToUInt16(r[0])!=1002)throw new Exception("Sereth catalog missing");town.AddNpc(new NpcSpawn{UID=59558,MapID=1002,X=Convert.ToUInt16(r[1]),Y=Convert.ToUInt16(r[2]),Mesh=Convert.ToUInt16(r[3]),Type=Enums.NpcType.Talker});}}

        InitializeChainBolt(runtime);VerifyChainBoltGuards(town);InitializePirateLessons(runtime);VerifyWindstormGuards(town);
        var rules=typeof(Entity).Assembly.GetType("Revora.Game.Features.RebirthSkillRules");
        foreach(int rite in new[]{0,1,2})foreach(byte school in new byte[]{7,13,14})
        foreach(byte destination in (school==7?new byte[]{71,132,11}:new byte[]{132,142,11}))foreach(bool fail in new[]{false,true}) {
            byte master=(byte)(school*10+5),rank=rebirthClasses.Single(c=>c/10==school);
            var p=rite==0?RebirthPlayer(town,master):rite==1?SecondPlayer(town,master,master):PaidPlayer(town,rank);
            ushort[] ids=school==7?new ushort[]{11140,14680,14710,14720}:new ushort[]{10309};
            if(school!=7){ChainBoltLesson(p);Check(p.Spells.ContainsKey(10309),"ordinary Taoist Moon lesson precedes actual rite");p.Entity.Teleport(1002,(ushort)(town.Npcs[59558].X-2),(ushort)(town.Npcs[59558].Y-2));p.ActiveNpc=59558;}
            if(school==7){LearnPirateRiteArts(p);p.Entity.Teleport(1002,(ushort)(town.Npcs[59558].X-2),(ushort)(town.Npcs[59558].Y-2));p.ActiveNpc=59558;}
            foreach(var id in ids){p.Spells[id].Level=1;p.Spells[id].PreviousLevel=2;p.Spells[id].Experience=90;p.Spells[id].Souls=(Spell.Soul_Level)1;}
            SkillTable.SaveSpells(p);EntityTable.SaveEntity(p);
            var foreign=RebirthPlayer(town,75);var other=MonkPillar(foreign,FloorItem.SeaBurial);
            var own=MonkPillar(p,FloorItem.SeaBurial);p.Entity.AddFlag((ulong)FlagsUpdate.Flags.ImmortalForce,30,false);
            if(rite==0)RebirthOffer(p,destination);else if(rite==1)SecondOffer(p,destination);else PaidOffer(p);
            if(fail)Query("CREATE FUNCTION reject_five_rite() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER reject_five_rite BEFORE UPDATE ON entities FOR EACH ROW EXECUTE FUNCTION reject_five_rite()");
            try {if(rite==0)StoneNpc(p,214);else if(rite==1)StoneNpc(p,180);else PaidRequest(p,destination);}
            finally {if(fail)Query("DROP TRIGGER reject_five_rite ON entities;DROP FUNCTION reject_five_rite()");}
            string label=rite+"/"+school+"/"+destination;
            if(fail)Check(Fenced(p) && ids.All(id=>p.Spells.ContainsKey(id) && p.Spells[id].Level==1 && p.Spells[id].Experience==90)
                && p.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.ImmortalForce) && PillarRegistered(own),"failed rite retains learned skills and Pirate effects: "+label);
            else {
                Check(!Fenced(p) && p.Entity.Class==destination && !p.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.ImmortalForce) && !PillarRegistered(own),"committed rite clears old Pirate effect ownership: "+label);
                bool keep=school==7?destination==71:destination==132 || destination==142;
                p=ReloadBall(p);
                Check(ids.All(id=>keep?p.Spells.ContainsKey(id) && p.Spells[id].Level==0 && p.Spells[id].Experience==0 && p.Spells[id].PreviousLevel==2 && (int)p.Spells[id].Souls==1:!p.Spells.ContainsKey(id)),"actual rite and character reload apply five-art school rules: "+label);
                Revora.Game.Attacking.Handle.SeaBurial(own);
                Check(!PillarRegistered(own),"retired Sea Burial callback cannot resurrect after rite: "+label);
            }
            Check(PillarRegistered(other),"rite preserves foreign Pirate effects: "+label);
            town.RemoveFloorItem(own);town.RemoveFloorItem(other);
        }
        VerifyEarnedPirateScrollRite(town);
        VerifyPirateRiteConcurrency(town);
        var pulse=RebirthPlayer(town,75);RebirthSkill(pulse,14680);pulse.Spells[14680].Level=0;SkillTable.SaveSpells(pulse);
        pulse.Entity.MinAttack=100;pulse.Entity.MaxAttack=101;
        var target=LootMonster(pulse);target.Hitpoints=target.MaxHitpoints=100000;pulse.Screen.Add(target);
        foreach(string bad in new[]{"owner-id","unregistered","class","skill","dead","fake","removed","out-of-bounds"}){
            var item=MonkPillar(pulse,FloorItem.SeaBurial);item.X=target.X;item.Y=target.Y;item.X2=target.X;item.Y2=(ushort)(target.Y+1);uint hp=target.Hitpoints;
            if(bad=="owner-id")item.OwnerUID++;if(bad=="unregistered")town.RemoveFloorItem(item);if(bad=="class")pulse.Entity.Class=15;
            if(bad=="skill")pulse.Spells.Remove(14680);if(bad=="dead")pulse.Entity.Hitpoints=0;if(bad=="fake")pulse.Fake=true;
            if(bad=="removed")item.Type=FloorItem.RemoveEffect;if(bad=="out-of-bounds")item.X=65535;
            Revora.Game.Attacking.Handle.SeaBurial(item);Check(target.Hitpoints==hp,"invalid Pirate pulse cannot damage: "+bad);
            pulse.Entity.Class=75;pulse.Entity.Hitpoints=100;pulse.Fake=false;if(bad=="skill")pulse.Spells.Add(14680,new Spell(true){ID=14680,Level=0,Available=true});town.RemoveFloorItem(item);
        }
        var valid=MonkPillar(pulse,FloorItem.SeaBurial);valid.X=target.X;valid.Y=target.Y;valid.X2=target.X;valid.Y2=(ushort)(target.Y+1);
        Revora.Game.Attacking.Handle.SeaBurial(valid);uint after=target.Hitpoints;
        Check(after<100000 && !PillarRegistered(valid),"registered Pirate pulse damages and consumes its effect");
        System.Threading.Tasks.Parallel.For(0,12,_=>Revora.Game.Attacking.Handle.SeaBurial(valid));
        Check(target.Hitpoints==after,"concurrent repeated Pirate pulses cannot deal duplicate damage");
        var clean=RebirthPlayer(town,75);
        Check(!((Spell[])Call(rules,null,"Plan",clean,(byte)71)).Any(s=>new ushort[]{11140,14680,14710,14720}.Contains(s.ID)),"Pirate retention never grants unlearned arts");
        var stale=MonkPillar(clean,FloorItem.SeaBurial);var replacement=MonkPillar(clean,FloorItem.SeaBurial);town.RemoveFloorItem(replacement);replacement.UID=stale.UID;town.RemoveFloorItem(stale);town.AddFloorItem(replacement);
        Revora.Game.Attacking.Handle.SeaBurial(stale);Check(PillarRegistered(replacement),"stale Sea Burial callback preserves replacement floor identity");
        clean.Entity.Class=15;Revora.Game.Attacking.Handle.SeaBurial(replacement);
        Check(!PillarRegistered(replacement),"unusable registered Pirate effect expires without stale damage");
    }
}
