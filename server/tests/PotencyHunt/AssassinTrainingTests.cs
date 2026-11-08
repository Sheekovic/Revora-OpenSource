using System;
using System.Linq;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static readonly Type AssassinTrainer=typeof(Entity).Assembly.GetType("Revora.Game.Features.AssassinTraining");
    static readonly ushort[] assassinArts={11590,11600,11620,11610,11650,11660,11670};
    static long TrainingCount(GameState p){return Scalar("SELECT count(*) FROM revora_assassin_training WHERE uid=@uid",p.Entity.UID);}
    static void VerifyAssassinTraining(string runtime) {
        VerifyPromotions(runtime);var map=Kernel.Maps[1004];Call(AssassinTrainer,null,"Initialize");
        byte[] minimum={50,100,40,40,70,40,90};
        for(int i=0;i<assassinArts.Length;i++) {
            var p=PromotionPlayer(map,42,(byte)(minimum[i]-1),400);byte option=(byte)(12+i);uint cp=p.Entity.ConquerPoints;
            AbyssOpen(p,400);SoulWire(p,49,400);SoulWire(p,option,400);
            Check(!p.Spells.ContainsKey(assassinArts[i]) && TrainingCount(p)==0 && !Fenced(p),"Assassin art rejects just below its learning level: "+assassinArts[i]);
            p.Entity.Level=minimum[i];SoulWire(p,option,400);
            Check(!Fenced(p) && p.Spells.ContainsKey(assassinArts[i]) && p.Spells[assassinArts[i]].Available
                && p.Spells[assassinArts[i]].Level==0 && TrainingCount(p)==1 && p.Entity.ConquerPoints==cp,
                "ordinary native dialogue learns and persists the correct free art at its boundary: "+assassinArts[i]);
            if(i==2)Check(!p.Spells.ContainsKey(11600),"Path of Shadow does not teach Dagger Storm");
            var skill=p.Spells[assassinArts[i]];skill.Level=2;skill.PreviousLevel=3;skill.Experience=91;skill.Souls=(Spell.Soul_Level)1;SkillTable.SaveSpells(p);
            byte level=skill.Level;Parallel.For(0,12,_=>SoulWire(p,option,400));
            Check(TrainingCount(p)==1 && skill.Level==level && skill.PreviousLevel==3 && skill.Experience==91 && (int)skill.Souls==1,
                "concurrent learning replays preserve trained art and soul metadata: "+assassinArts[i]);
            p=ReloadBall(p);
            Check(!Fenced(p) && p.Spells[assassinArts[i]].Level==level && p.Spells[assassinArts[i]].PreviousLevel==3
                && p.Spells[assassinArts[i]].Experience==91 && (int)p.Spells[assassinArts[i]].Souls==1,"full reload retains learned art metadata: "+assassinArts[i]);
        }
        foreach(string mode in new[]{"unpromoted","wrong-class","remote","trade","dead","wrong-npc","short","long","missing-owner"}) {
            var p=PromotionPlayer(map,42,100,400);
            if(mode=="unpromoted")p.Entity.Class=41;if(mode=="wrong-class")p.Entity.Class=15;
            if(mode=="remote")p.Entity.X+=20;if(mode=="trade")p.Trade.InTrade=true;if(mode=="dead")p.Entity.Hitpoints=0;
            if(mode=="missing-owner")Kernel.GamePool.Remove(p.Entity.UID);
            SoulWire(p,14,mode=="wrong-npc"?1u:400u,mode=="short"?18:mode=="long"?121:20);
            Check(!p.Spells.ContainsKey(11620) && TrainingCount(p)==0,"training rejects invalid ordinary interaction: "+mode);
        }
        foreach(string table in new[]{"entities","skills","revora_assassin_training"}) {
            var p=PromotionPlayer(map,42,100,400);
            Query("CREATE FUNCTION suppress_training() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END'");
            Query("CREATE TRIGGER suppress_training BEFORE INSERT OR UPDATE ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_training()");
            try{SoulWire(p,14,400);}finally{Query("DROP TRIGGER suppress_training ON "+table+";DROP FUNCTION suppress_training()");}
            Check(Fenced(p) && !p.Spells.ContainsKey(11620) && TrainingCount(p)==0
                && Scalar("SELECT count(*) FROM skills WHERE entityid=@uid AND id=11620",p.Entity.UID)==0,"suppressed training write rolls back the entire grant: "+table);
        }
        foreach(string conflict in new[]{"class","mapid","skill"}) {
            var p=PromotionPlayer(map,42,100,400);
            if(conflict=="skill")Query("INSERT INTO skills(entityid,id,level,previouslevel,experience,levelhu,levelhu2) VALUES(@uid,11620,0,0,0,0,0)","uid",(long)p.Entity.UID);
            else Query("UPDATE entities SET "+conflict+"=@value WHERE uid=@uid","value",conflict=="class"?15:1002,"uid",(long)p.Entity.UID);
            SoulWire(p,14,400);
            Check(Fenced(p) && !p.Spells.ContainsKey(11620) && TrainingCount(p)==0,"training preserves an external saved preimage: "+conflict);
        }
    }
    static void AssassinModes(GameState p) {
        foreach(var f in new[]{FlagsUpdate.Flags.PathOfShadow,FlagsUpdate.Flags.BladeFlurry,FlagsUpdate.Flags.KineticSpark})
            p.Entity.AddFlag((ulong)f,StatusFlagsBigVector32.PermanentFlag,false);
        p.Entity.BladeFlurryTime=20;p.Entity.SpiritFocus=true;p.Entity.SpiritFocusPercent=2;
    }
    static void VerifyAssassinRebirth(string runtime,Map town) {
        var map=World(runtime,1004);
        using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new Npgsql.NpgsqlCommand("SELECT cellx,celly,lookface FROM public.npcs WHERE id=400 AND mapid=1004",c))
        using(var r=q.ExecuteReader()){if(!r.Read())throw new Exception("Archer Master missing.");
            map.AddNpc(new NpcSpawn{UID=400,MapID=1004,X=Convert.ToUInt16(r[0]),Y=Convert.ToUInt16(r[1]),Mesh=Convert.ToUInt16(r[2]),Type=Enums.NpcType.Talker});}}
        foreach(int rite in new[]{0,1,2})foreach(bool keep in new[]{false,true})foreach(bool fail in new[]{false,true}) {
            var p=rite==0?RebirthPlayer(town,45):rite==1?SecondPlayer(town,45,45):PaidPlayer(town,41);
            if(rite==2)p.Entity.Class=45;
            p.Entity.MapID=1004;p.Entity.X=map.Npcs[400].X;p.Entity.Y=map.Npcs[400].Y;p.ActiveNpc=400;EntityTable.SaveEntity(p);
            for(byte option=12;option<=18;option++)SoulWire(p,option,400);
            Check(!Fenced(p) && assassinArts.All(id=>p.Spells.ContainsKey(id)) && TrainingCount(p)==7,"ordinary Archer learns all seven arts before rite "+rite);
            foreach(var id in assassinArts){p.Spells[id].Level=2;p.Spells[id].Experience=35;}
            SkillTable.SaveSpells(p);var previous=assassinArts.ToDictionary(id=>id,id=>p.Spells[id].Level);
            p.Entity.MapID=1002;p.Entity.X=town.Npcs[59558].X;p.Entity.Y=town.Npcs[59558].Y;p.ActiveNpc=59558;EntityTable.SaveEntity(p);AssassinModes(p);
            byte next=keep?(byte)41:(byte)11;
            if(rite==0)RebirthOffer(p,next);else if(rite==1)SecondOffer(p,next);else PaidOffer(p);
            if(fail)Query("CREATE FUNCTION reject_assassin_rite() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER reject_assassin_rite BEFORE UPDATE ON entities FOR EACH ROW EXECUTE FUNCTION reject_assassin_rite()");
            try{if(rite==0)StoneNpc(p,214);else if(rite==1)StoneNpc(p,180);else PaidRequest(p,next);}
            finally{if(fail)Query("DROP TRIGGER reject_assassin_rite ON entities;DROP FUNCTION reject_assassin_rite()");}
            if(fail) {
                Check(Fenced(p) && assassinArts.All(id=>p.Spells[id].Level==previous[id]) && p.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.KineticSpark)
                    && p.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.PathOfShadow) && p.Entity.BladeFlurryTime==20 && p.Entity.SpiritFocusPercent==2,
                    "failed rite preserves active Assassin modes and skills: "+rite);
            } else {
                Check(!Fenced(p) && p.Entity.Class==next && p.Entity.Level==15
                    && !p.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.KineticSpark) && !p.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.PathOfShadow)
                    && !p.Entity.ContainsFlag((ulong)FlagsUpdate.Flags.BladeFlurry) && p.Entity.BladeFlurryTime==0 && p.Entity.SpiritFocusPercent==0,
                    "committed rite clears all active Assassin modes: "+rite+" keep="+keep);
                p=ReloadBall(p);
                Check(assassinArts.All(id=>keep?p.Spells.ContainsKey(id) && p.Spells[id].Level==0 && p.Spells[id].PreviousLevel==previous[id]:!p.Spells.ContainsKey(id)),
                    "rite persists only destination-eligible Assassin arts with previous training: "+rite+" keep="+keep);
            }
        }
    }
}
