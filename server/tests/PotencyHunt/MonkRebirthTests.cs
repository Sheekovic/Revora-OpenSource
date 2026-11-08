using System;
using System.Linq;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Game.Attacking;
using Revora.Network.GamePackets;
internal static partial class PotencyHuntTests
{
    static readonly ushort[] monkArts={12550,12560,12570};
    static void MonkInstructorMap(string runtime){
        var map=World(runtime,1004);Call(MonkTrainer,null,"Initialize");
        using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new Npgsql.NpgsqlCommand("SELECT cellx,celly,lookface FROM public.npcs WHERE id=4271 AND mapid=1004",c))
        using(var r=q.ExecuteReader()){if(!r.Read())throw new Exception("Head Abbot missing.");map.AddNpc(new NpcSpawn{UID=4271,MapID=1004,X=Convert.ToUInt16(r[0]),Y=Convert.ToUInt16(r[1]),Mesh=Convert.ToUInt16(r[2]),Type=Enums.NpcType.Talker});}}
    }
    static void LearnMonkArts(GameState p){
        p.Entity.Teleport(1004,78,42);p.ActiveNpc=4271;EntityTable.SaveEntity(p);
        foreach(uint item in new uint[]{3007568,3007566,3007567})NestedPack(p,item,1,true);
        foreach(byte option in new byte[]{23,24,25})SoulWire(p,option,4271);
        if(Fenced(p) || monkArts.Any(id=>!p.Spells.ContainsKey(id)))throw new Exception("Monk rite training failed.");
        foreach(var id in monkArts){p.Spells[id].Level=2;p.Spells[id].PreviousLevel=3;p.Spells[id].Experience=51;p.Spells[id].Souls=(Spell.Soul_Level)1;}SkillTable.SaveSpells(p);
    }
    static FloorItem MonkPillar(GameState p,uint type=FloorItem.InfernalEcho){
        var item=new FloorItem(true){UID=FloorItem.FloorUID.Next,Owner=p,OwnerUID=p.Entity.UID,ItemID=type,Type=FloorItem.Effect,MapID=p.Map.ID,X=p.Entity.X,Y=p.Entity.Y,OnFloor=Time32.Now};
        while(p.Map.FloorItems.ContainsKey(item.UID))item.UID=FloorItem.FloorUID.Next;p.Map.AddFloorItem(item);return item;
    }
    static bool PillarRegistered(FloorItem item){FloorItem current;return Kernel.Maps[item.MapID].FloorItems.TryGetValue(item.UID,out current) && ReferenceEquals(item,current);}
    static void VerifyMonkRebirth(string runtime,Map town){
        VerifyPaidReincarnation(runtime,town);MonkInstructorMap(runtime);
        foreach(int rite in new[]{0,1,2})foreach(bool keep in new[]{false,true})foreach(bool fail in new[]{false,true}){
            var p=rite==0?RebirthPlayer(town,65):rite==1?SecondPlayer(town,65,65):PaidPlayer(town,61);if(rite==2)p.Entity.Class=65;
            LearnMonkArts(p);Check(MonkReceipts(p)==3,"ordinary Monk learns three paid epic arts before rite "+rite);
            p.Entity.Teleport(1002,town.Npcs[59558].X,town.Npcs[59558].Y);p.ActiveNpc=59558;EntityTable.SaveEntity(p);
            var pillars=new[]{MonkPillar(p),MonkPillar(p,FloorItem.ActOfTarget),MonkPillar(p,FloorItem.ActOfTarget2)};
            p.Entity.WrathoftheEmperor=true;p.Entity.WrathoftheEmperorStamp=DateTime.Now;p.Entity.OblivionTime=20;
            p.Entity.AddFlag((ulong)FlagsUpdate.Flags.Oblivion,StatusFlagsBigVector32.PermanentFlag,false);
            p.Entity.Aura_isActive=true;p.Entity.Aura_actType=(ulong)FlagsUpdate.Flags.TyrantAura;p.Entity.Aura_actPower=10;
            p.Entity.AddFlag(p.Entity.Aura_actType,StatusFlagsBigVector32.PermanentFlag,false);p.doAuraBonuses(p.Entity.Aura_actType,10,1);
            byte next=keep?(byte)61:(byte)21;if(rite==0)RebirthOffer(p,next);else if(rite==1)SecondOffer(p,next);else PaidOffer(p);
            if(fail)Query("CREATE FUNCTION reject_monk_rite() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER reject_monk_rite BEFORE UPDATE ON entities FOR EACH ROW EXECUTE FUNCTION reject_monk_rite()");
            try{if(rite==0)StoneNpc(p,214);else if(rite==1)StoneNpc(p,180);else PaidRequest(p,next);}finally{if(fail)Query("DROP TRIGGER reject_monk_rite ON entities;DROP FUNCTION reject_monk_rite()");}
            if(fail){
                Check(Fenced(p) && monkArts.All(id=>p.Spells.ContainsKey(id) && p.Spells[id].Experience==51) && pillars.All(PillarRegistered)
                    && p.Entity.WrathoftheEmperor && p.Entity.Aura_isActive && p.Entity.OblivionTime==20 && p.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Oblivion),"failed Monk rite preserves earned skills, registered effects and modes: "+rite);
            }else{
                Check(!Fenced(p) && p.Entity.Class==next && pillars.All(i=>!PillarRegistered(i) && i.Type==FloorItem.RemoveEffect) && !p.Entity.WrathoftheEmperor
                    && p.Entity.WrathoftheEmperorStamp==DateTime.MinValue && !p.Entity.Aura_isActive && p.Entity.Aura_actType==0 && p.Entity.Aura_actPower==0
                    && !p.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.TyrantAura) && !p.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.Oblivion) && p.Entity.OblivionTime==0,"committed Monk rite clears prior-life effects and active modes: "+rite);
                foreach(var item in pillars)Handle.InfroEcho(item);
                p=ReloadBall(p);Check(monkArts.All(id=>keep?p.Spells.ContainsKey(id) && p.Spells[id].Level==0 && p.Spells[id].Experience==0 && p.Spells[id].PreviousLevel==3 && (int)p.Spells[id].Souls==1:!p.Spells.ContainsKey(id)),"full reload retains/reset or removes earned Monk arts for destination: "+rite+" monk="+keep);
                if(keep){p.Entity.Teleport(1004,78,42);p.ActiveNpc=4271;EntityTable.SaveEntity(p);foreach(uint id in new uint[]{3007568,3007566,3007567})NestedPack(p,id,1,true);
                    foreach(byte option in new byte[]{23,24,25})SoulWire(p,option,4271);
                    Check(!Fenced(p) && MonkReceipts(p)==3 && new uint[]{3007568,3007566,3007567}.All(id=>EpicAmount(p,id)==1),"retained arts do not charge new scrolls after Monk rite "+rite);}
            }
        }
        VerifyMonkPulses(town);VerifyMonkPulseDamage(runtime,town);
        var rules=typeof(Entity).Assembly.GetType("Revora.Game.Features.RebirthSkillRules");var clean=RebirthPlayer(town,65);
        Check(!((Spell[])Call(rules,null,"Plan",clean,(byte)61)).Any(s=>monkArts.Contains(s.ID)),"Monk retention never grants unlearned epic arts");
    }
    static void VerifyMonkPulses(Map town){
        var rules=typeof(Entity).Assembly.GetType("Revora.Game.Features.RebirthSkillRules");
        var p=RebirthPlayer(town,65);RebirthSkill(p,12550);p.Entity.MinAttack=100;p.Entity.MaxAttack=101;
        var target=LootMonster(p);target.Hitpoints=target.MaxHitpoints=100000;p.Screen.Add(target);
        foreach(string bad in new[]{"owner-id","owner-reference","unregistered","map","class","skill","dead","fake","removed","out-of-bounds"}){
            var item=MonkPillar(p);item.X=target.X;item.Y=target.Y;uint hp=target.Hitpoints;
            if(bad=="owner-id")item.OwnerUID++;if(bad=="owner-reference")item.Owner=RebirthPlayer(town,65);if(bad=="unregistered")p.Map.RemoveFloorItem(item);
            if(bad=="map")item.MapID=1004;if(bad=="class")p.Entity.Class=55;if(bad=="skill")p.Spells.Remove(12550);
            if(bad=="dead")p.Entity.Hitpoints=0;if(bad=="fake")p.Fake=true;if(bad=="removed")item.Type=FloorItem.RemoveEffect;if(bad=="out-of-bounds")item.X=65535;
            Handle.InfroEcho(item);Check(target.Hitpoints==hp,"invalid or stale Infernal Echo cannot damage: "+bad);
            p.Entity.Class=65;p.Entity.Hitpoints=100;p.Fake=false;if(bad=="skill")p.Spells.Add(12550,new Spell(true){ID=12550,Level=4,Available=true});
            item.MapID=1002;item.X=target.X;item.Y=target.Y;p.Map.RemoveFloorItem(item);
        }
        var valid=MonkPillar(p);valid.X=target.X;valid.Y=target.Y;Handle.InfroEcho(valid);uint after=target.Hitpoints;
        Check(after<100000 && valid.Type==FloorItem.RemoveEffect,"canonical owned learned pillar still delivers its pulse");
        Parallel.For(0,12,_=>Handle.InfroEcho(valid));Check(target.Hitpoints==after,"consumed effect cannot deal duplicate damage through concurrent screen ticks");
        p.Map.RemoveFloorItem(valid);var fresh=MonkPillar(p);var other=RebirthPlayer(town,65);var foreign=MonkPillar(other);
        lock(p.ItemSyncRoot)Call(rules,null,"ClearMonkModes",p);
        Check(!PillarRegistered(fresh) && PillarRegistered(foreign),"Monk cleanup preserves another character's registered effect");
        Handle.InfroEcho(fresh);Check(target.Hitpoints==after,"retired pillar cannot deliver a delayed pulse even while its owner still knows the art");
        var old=MonkPillar(p);p.Map.RemoveFloorItem(old);var replacement=MonkPillar(other);p.Map.RemoveFloorItem(replacement);replacement.UID=old.UID;p.Map.AddFloorItem(replacement);
        p.Map.RemoveFloorItem(old);Check(PillarRegistered(replacement),"stale effect expiry cannot remove a replacement that reused its floor UID");
    }
    static void VerifyMonkPulseDamage(string runtime,Map town){
        var map=World(runtime,1700);var caster=RebirthPlayer(town,65);var victim=RebirthPlayer(town,15);
        RebirthSkill(caster,12550);caster.Entity.Teleport(1700,603,641);victim.Entity.Teleport(1700,605,641);
        caster.Attackable=victim.Attackable=true;caster.Entity.PKMode=Enums.PKMode.PK;victim.Entity.PKMode=Enums.PKMode.PK;
        caster.Screen.Add(victim.Entity);victim.Entity.Defence=0;
        Func<uint,uint,uint> pulse=(casterAttack,victimAttack)=>{
            caster.Entity.MinAttack=casterAttack;caster.Entity.MaxAttack=casterAttack+1;victim.Entity.MinAttack=victimAttack;victim.Entity.MaxAttack=victimAttack+1;
            victim.Entity.Hitpoints=victim.Entity.MaxHitpoints=1000000;var item=MonkPillar(caster);item.X=victim.Entity.X;item.Y=victim.Entity.Y;
            Handle.InfroEcho(item);map.RemoveFloorItem(item);return 1000000-victim.Entity.Hitpoints;
        };
        uint first=pulse(1000,1000),victimChanged=pulse(1000,100000),casterChanged=pulse(10000,1000);
        Check(first>0 && victimChanged==first && casterChanged>first,"Infernal Echo player damage follows the caster's attack and does not use the victim as attacker");
    }

}
