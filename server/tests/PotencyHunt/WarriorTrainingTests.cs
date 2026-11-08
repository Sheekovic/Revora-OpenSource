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
    static readonly Type WarriorTrainer=typeof(Entity).Assembly.GetType("Revora.Game.Features.WarriorTraining");
    static long WarriorReceipts(GameState p){return Scalar("SELECT count(*) FROM revora_warrior_training WHERE uid=@uid",p.Entity.UID);}
    static void VerifyWarriorTraining(string runtime){
        VerifyPromotions(runtime);var map=Kernel.Maps[1004];Call(WarriorTrainer,null,"Initialize");Call(WarriorTrainer,null,"Initialize");
        int[][] rows={new[]{6,12770,60,20},new[]{9,10470,40,20},new[]{10,11160,70,23},new[]{11,11200,40,22},new[]{20,12660,15,20},new[]{21,12670,15,20},new[]{22,12680,60,20},new[]{23,12690,15,20},new[]{24,12700,15,20}};
        foreach(var row in rows){
            byte option=(byte)row[0],rank=(byte)row[3];ushort id=(ushort)row[1];var below=PromotionPlayer(map,rank,(byte)(row[2]-1),10001);SoulWire(below,option,10001);
            Check(!Fenced(below) && !below.Spells.ContainsKey(id) && WarriorReceipts(below)==0,"Warrior lesson rejects below its version-reviewed minimum: "+id);
            var p=PromotionPlayer(map,rank,(byte)row[2],10001);uint cp=p.Entity.ConquerPoints;ulong silver=p.Entity.Money;
            Parallel.For(0,12,_=>SoulWire(p,option,10001));
            Check(!Fenced(p) && p.Account.State==AccountTable.AccountState.Player && !p.Fake && p.Spells.ContainsKey(id) && p.Spells[id].Available && p.Spells[id].Level==0 && WarriorReceipts(p)==1 && p.Entity.ConquerPoints==cp && p.Entity.Money==silver,"ordinary Warrior learns exactly one free art at its actual boundary: "+id);
            byte trained=(byte)(id==11160?1:id==12660 || id==12700?0:2),previous=(byte)(trained==0?0:trained==1?1:3);
            var learned=p.Spells[id];learned.Level=trained;learned.PreviousLevel=previous;learned.Experience=71;learned.Souls=(Spell.Soul_Level)1;SkillTable.SaveSpells(p);
            SoulWire(p,option,10001);p=ReloadBall(p);p.ActiveNpc=10001;SoulWire(p,option,10001);
            Check(!Fenced(p) && WarriorReceipts(p)==1 && p.Spells[id].Level==trained && p.Spells[id].PreviousLevel==previous && p.Spells[id].Experience==71 && (int)p.Spells[id].Souls==1,"replayed and reconnected Warrior learning preserves trained metadata: "+id);
        }
        foreach(byte rank in new byte[]{20,21,22,23,24,25}){
            var p=PromotionPlayer(map,rank,100,10001);foreach(var row in rows)SoulWire(p,(byte)row[0],10001);
            Check(!Fenced(p) && WarriorReceipts(p)==(rank<22?7:rank<23?8:9) && p.Spells.ContainsKey(11200)==(rank>=22) && p.Spells.ContainsKey(11160)==(rank>=23),"all Warrior ranks learn eligible arts with Magic Defender promotion enforced: "+rank);
        }
        foreach(byte level in new byte[]{2,3,14,15,39,40}){
            var p=PromotionPlayer(map,20,level,10001);Parallel.For(0,12,_=>SoulWire(p,5,10001));
            ushort[] expected=level<3?new ushort[0]:level<15?new ushort[]{1025}:level<40?new ushort[]{1025,1015,1020}:new ushort[]{1025,1015,1020,1040};
            Check(!Fenced(p) && p.Spells.Keys.OrderBy(i=>i).SequenceEqual(expected.OrderBy(i=>i)) && WarriorReceipts(p)==expected.Length,"XP lesson atomically teaches only eligible current-catalog arts at level "+level);
        }
        var menu=PromotionPlayer(map,25,140,10001);AbyssOpen(menu,10001);SoulWire(menu,2,10001);SoulWire(menu,12,10001);
        Check(!Fenced(menu) && menu.Spells.Count==0 && WarriorReceipts(menu)==0,"opening Warrior menus at 140 grants no unselected arts");
        SoulWire(menu,6,10001);Check(menu.Spells.ContainsKey(12770) && !menu.Spells.ContainsKey(1051),"old Dash option now teaches the installed Pounce identity without inventing missing 1051");
        foreach(string mode in new[]{"class","far","trade","dead","fake","unloaded","missing-owner","echo","short","long"}){
            var p=PromotionPlayer(map,25,100,10001);if(mode=="class")p.Entity.Class=55;if(mode=="far")p.Entity.X+=20;if(mode=="trade")p.Trade.InTrade=true;
            if(mode=="dead")p.Entity.Hitpoints=0;if(mode=="fake")p.Fake=true;if(mode=="unloaded")p.Entity.FullyLoaded=false;if(mode=="missing-owner")Kernel.GamePool.Remove(p.Entity.UID);
            SoulWire(p,22,mode=="echo"?1u:10001u,mode=="short"?18:mode=="long"?121:20);
            Check(!p.Spells.ContainsKey(12680) && WarriorReceipts(p)==0,"invalid Warrior instructor request grants nothing: "+mode);
        }
        foreach(string table in new[]{"entities","skills","revora_warrior_training"})foreach(byte option in new byte[]{5,10,22}){
            var p=PromotionPlayer(map,25,100,10001);
            Query("CREATE FUNCTION suppress_warrior_training() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END'");
            Query("CREATE TRIGGER suppress_warrior_training BEFORE INSERT OR UPDATE OR DELETE ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_warrior_training()");
            try{SoulWire(p,option,10001);}finally{Query("DROP TRIGGER suppress_warrior_training ON "+table+";DROP FUNCTION suppress_warrior_training()");}
            Check(Fenced(p) && p.Spells.Count==0 && WarriorReceipts(p)==0 && Scalar("SELECT count(*) FROM skills WHERE entityid=@uid",p.Entity.UID)==0,"failed single/group lesson rolls back skills and receipts: "+table+" option="+option);
            p=ReloadBall(p);p.ActiveNpc=10001;SoulWire(p,option,10001);Check(!Fenced(p) && WarriorReceipts(p)==(option==5?4:1),"reconnected Warrior can retry an uncommitted lesson");
        }
        var late=PromotionPlayer(map,25,100,10001);
        Query("CREATE FUNCTION suppress_warrior_late() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END'");
        Query("CREATE TRIGGER suppress_warrior_late BEFORE INSERT ON skills FOR EACH ROW WHEN (NEW.id=1040) EXECUTE FUNCTION suppress_warrior_late()");
        try{SoulWire(late,5,10001);}finally{Query("DROP TRIGGER suppress_warrior_late ON skills;DROP FUNCTION suppress_warrior_late()");}
        late=ReloadBall(late);Check(late.Spells.Count==0 && WarriorReceipts(late)==0,"failure of the last XP art rolls back all three earlier skill and receipt writes across reload");
        foreach(string conflict in new[]{"class","mapid","skill"}){
            var p=PromotionPlayer(map,25,100,10001);
            if(conflict=="skill")Query("INSERT INTO skills(entityid,id,level,previouslevel,experience,levelhu,levelhu2) VALUES(@uid,12680,0,0,0,0,0)","uid",(long)p.Entity.UID);
            else Query("UPDATE entities SET "+conflict+"=@value WHERE uid=@uid","value",conflict=="class"?15:1002,"uid",(long)p.Entity.UID);
            SoulWire(p,22,10001);Check(Fenced(p) && !p.Spells.ContainsKey(12680) && WarriorReceipts(p)==0,"saved Warrior ownership conflict cannot overwrite another writer: "+conflict);
        }
    }
}
