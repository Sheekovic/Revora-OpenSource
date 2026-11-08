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
    static readonly Type NinjaTrainer=typeof(Entity).Assembly.GetType("Revora.Game.Features.NinjaTraining");
    static long NinjaReceipts(GameState p){return Scalar("SELECT count(*) FROM revora_ninja_training WHERE uid=@uid",p.Entity.UID);}
    static void VerifyNinjaTraining(string runtime) {
        VerifyAssassinTraining(runtime);var map=Kernel.Maps[1004];Call(NinjaTrainer,null,"Initialize");
        // Expectations are the reviewed instructor options, not the service's private table.
        int[][] rows={new[]{60,11170,40,52,0},new[]{61,11180,20,51,0},new[]{69,11230,40,50,0},
            new[]{80,12070,40,50,3004460},new[]{81,12090,50,50,3004459},new[]{82,12080,40,50,0},new[]{83,12110,15,50,3004461}};
        foreach(var row in rows)foreach(bool bound in row[4]==0?new[]{false}:new[]{false,true}) {
            byte option=(byte)row[0];ushort spell=(ushort)row[1];var p=PromotionPlayer(map,(byte)row[3],(byte)(row[2]-1),4720);
            var scroll=row[4]==0?null:NestedPack(p,(uint)row[4],3,bound);uint cp=p.Entity.ConquerPoints;
            AbyssOpen(p,4720);SoulWire(p,option,4720);
            Check(!p.Spells.ContainsKey(spell) && NinjaReceipts(p)==0 && (scroll==null || SavedRune(scroll,"stacksize")==3),"Wind Sage rejects below learning level without spending: "+spell);
            p.Entity.Level=(byte)row[2];SoulWire(p,option,4720);
            Check(!Fenced(p) && p.Spells.ContainsKey(spell) && p.Spells[spell].Available && p.Spells[spell].Level==0
                && NinjaReceipts(p)==1 && p.Entity.ConquerPoints==cp && (scroll==null || scroll.StackSize==2 && SavedRune(scroll,"stacksize")==2),
                "ordinary packet learns and persists exactly one art for its exact cost: "+spell+" bound="+bound);
            if(scroll!=null)Check(Scalar("SELECT count(*) FROM revora_ninja_training WHERE uid=@uid AND item_uid="+scroll.UID+" AND item_type="+row[4]+" AND stack_before=3 AND bound="+(bound?"true":"false"),p.Entity.UID)==1,"receipt preserves exact scroll ownership and binding");
            var skill=p.Spells[spell];skill.Level=2;skill.PreviousLevel=3;skill.Experience=71;skill.Souls=(Spell.Soul_Level)1;SkillTable.SaveSpells(p);byte level=skill.Level;
            Parallel.For(0,16,_=>SoulWire(p,option,4720));
            Check(!Fenced(p) && NinjaReceipts(p)==1 && skill.Level==level && skill.Experience==71 && (scroll==null || scroll.StackSize==2),"concurrent training replays preserve scrolls and prior practice: "+spell);
            p=ReloadBall(p);p.ActiveNpc=4720;SoulWire(p,option,4720);
            Check(!Fenced(p) && NinjaReceipts(p)==1 && p.Spells[spell].Level==level && p.Spells[spell].PreviousLevel==3
                && p.Spells[spell].Experience==71 && (int)p.Spells[spell].Souls==1 && (scroll==null || SavedRune(scroll,"stacksize")==2),"full reload and retry preserve skill metadata and payment: "+spell);
        }
        foreach(ushort stack in new ushort[]{0,1,2}) {
            var p=PromotionPlayer(map,55,100,4720);var scroll=NestedPack(p,3004460,stack,true);SoulWire(p,80,4720);
            Check(!Fenced(p) && p.Spells.ContainsKey(12070) && NinjaReceipts(p)==1 && (stack<=1?!p.Inventory.ContainsUID(scroll.UID)
                && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND uid="+scroll.UID,p.Entity.UID)==0:scroll.StackSize==stack-1),"exact scroll debit handles legacy singleton and stacked rows: "+stack);
        }
        var preference=PromotionPlayer(map,55,100,4720);var unbound=NestedPack(preference,3004460,1,false);var owned=NestedPack(preference,3004460,1,true);SoulWire(preference,80,4720);
        Check(preference.Spells.ContainsKey(12070) && preference.Inventory.ContainsUID(unbound.UID) && !preference.Inventory.ContainsUID(owned.UID),"Wind Sage spends bound scroll before a tradable copy");
        foreach(byte option in new byte[]{60,61,69,80,81,82,83}) {
            var p=PromotionPlayer(map,15,130,4720);var scroll=NestedPack(p,3004460,1,true);SoulWire(p,option,4720);
            Check(!Fenced(p) && p.Spells.Count==0 && NinjaReceipts(p)==0 && SavedRune(scroll,"id")==3004460,"forged cross-class selection grants no Ninja art: "+option);
        }
        foreach(byte option in new byte[]{60,61}) {
            var p=PromotionPlayer(map,option==60?(byte)51:(byte)50,100,4720);SoulWire(p,option,4720);
            Check(!Fenced(p) && NinjaReceipts(p)==0 && p.Spells.Count==0,"scythe art requires its reviewed promotion: "+option);
        }
        foreach(string mode in new[]{"missing","locked","suspicious","wardrobe","expired","time-left","warehouse","equipped","remote","trade","dead","fake","unloaded","missing-owner","wrong-npc","short","long"}) {
            var p=PromotionPlayer(map,55,100,4720);var scroll=NestedPack(p,3004460,1,true);
            if(mode=="missing")DisRemove(p,scroll);if(mode=="locked")scroll.Lock=1;if(mode=="suspicious")scroll.Suspicious=true;
            if(mode=="wardrobe")scroll.InWardrobe=true;if(mode=="expired")scroll.Days=1;if(mode=="time-left")scroll.TimeLeftInMinutes=1;
            if(mode=="warehouse")scroll.Warehouse=1;if(mode=="equipped")scroll.Position=1;if(mode=="remote")p.Entity.X+=20;
            if(mode=="trade")p.Trade.InTrade=true;if(mode=="dead")p.Entity.Hitpoints=0;if(mode=="fake")p.Fake=true;
            if(mode=="unloaded")p.Entity.FullyLoaded=false;if(mode=="missing-owner")Kernel.GamePool.Remove(p.Entity.UID);
            SoulWire(p,80,mode=="wrong-npc"?1u:4720u,mode=="short"?18:mode=="long"?121:20);
            Check(!p.Spells.ContainsKey(12070) && NinjaReceipts(p)==0 && (mode=="missing" || SavedRune(scroll,"id")==3004460),"invalid learning interaction preserves scroll: "+mode);
        }
        foreach(ushort stack in new ushort[]{1,2})foreach(string table in new[]{"entities","items","skills","revora_ninja_training"}) {
            var p=PromotionPlayer(map,55,100,4720);var scroll=NestedPack(p,3004460,stack,true);
            Query("CREATE FUNCTION suppress_ninja_training() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END'");
            Query("CREATE TRIGGER suppress_ninja_training BEFORE INSERT OR UPDATE OR DELETE ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_ninja_training()");
            try{SoulWire(p,80,4720);}finally{Query("DROP TRIGGER suppress_ninja_training ON "+table+";DROP FUNCTION suppress_ninja_training()");}
            Check(Fenced(p) && !p.Spells.ContainsKey(12070) && NinjaReceipts(p)==0 && scroll.StackSize==stack && SavedRune(scroll,"stacksize")==stack
                && Scalar("SELECT count(*) FROM skills WHERE entityid=@uid AND id=12070",p.Entity.UID)==0,"failed write rolls back scroll, skill and receipt: "+table+" stack="+stack);
            p=ReloadBall(p);Check(!p.Spells.ContainsKey(12070) && p.Inventory.ContainsUID(scroll.UID),"reload exposes the rolled-back scroll with no unearned skill");
        }
        foreach(string conflict in new[]{"class","mapid","skill","item-owner","item-stack","item-bound"}) {
            var p=PromotionPlayer(map,55,100,4720);var scroll=NestedPack(p,3004460,2,true);
            if(conflict=="skill")Query("INSERT INTO skills(entityid,id,level,previouslevel,experience,levelhu,levelhu2) VALUES(@uid,12070,0,0,0,0,0)","uid",(long)p.Entity.UID);
            else if(conflict.StartsWith("item-"))Query("UPDATE items SET "+(conflict=="item-owner"?"entityid=1":conflict=="item-stack"?"stacksize=3":"bound=0")+" WHERE uid=@uid","uid",(long)scroll.UID);
            else Query("UPDATE entities SET "+conflict+"=@value WHERE uid=@uid","value",conflict=="class"?15:1002,"uid",(long)p.Entity.UID);
            SoulWire(p,80,4720);
            Check(Fenced(p) && !p.Spells.ContainsKey(12070) && NinjaReceipts(p)==0 && scroll.StackSize==2,"saved ownership conflict grants no skill and spends nothing: "+conflict);
        }
    }
}
