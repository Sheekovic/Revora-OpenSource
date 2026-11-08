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
    static readonly Type MonkTrainer=typeof(Entity).Assembly.GetType("Revora.Game.Features.MonkTraining");
    static long MonkReceipts(GameState p){return Scalar("SELECT count(*) FROM revora_monk_training WHERE uid=@uid",p.Entity.UID);}
    static void VerifyMonkTraining(string runtime) {
        VerifyPromotions(runtime);var map=Kernel.Maps[1004];Call(MonkTrainer,null,"Initialize");
        // Expectations are the reviewed instructor options, not the service's private table.
        int[][] rows={new[]{23,12550,40,60,3007568},new[]{24,12560,50,60,3007566},new[]{25,12570,15,60,3007567}};
        foreach(var row in rows)foreach(bool bound in row[4]==0?new[]{false}:new[]{false,true}) {
            byte option=(byte)row[0];ushort spell=(ushort)row[1];var p=PromotionPlayer(map,(byte)row[3],(byte)(row[2]-1),4271);
            var scroll=row[4]==0?null:NestedPack(p,(uint)row[4],3,bound);uint cp=p.Entity.ConquerPoints;
            AbyssOpen(p,4271);SoulWire(p,option,4271);
            Check(!p.Spells.ContainsKey(spell) && MonkReceipts(p)==0 && (scroll==null || SavedRune(scroll,"stacksize")==3),"Head Abbot rejects below learning level without spending: "+spell);
            p.Entity.Level=(byte)row[2];SoulWire(p,option,4271);
            Check(!Fenced(p) && p.Spells.ContainsKey(spell) && p.Spells[spell].Available && p.Spells[spell].Level==0
                && MonkReceipts(p)==1 && p.Entity.ConquerPoints==cp && (scroll==null || scroll.StackSize==2 && SavedRune(scroll,"stacksize")==2),
                "ordinary packet learns and persists exactly one art for its exact cost: "+spell+" bound="+bound);
            if(scroll!=null)Check(Scalar("SELECT count(*) FROM revora_monk_training WHERE uid=@uid AND item_uid="+scroll.UID+" AND item_type="+row[4]+" AND stack_before=3 AND bound="+(bound?"true":"false"),p.Entity.UID)==1,"receipt preserves exact scroll ownership and binding");
            var skill=p.Spells[spell];skill.Level=2;skill.PreviousLevel=3;skill.Experience=71;skill.Souls=(Spell.Soul_Level)1;SkillTable.SaveSpells(p);byte level=skill.Level;
            Parallel.For(0,16,_=>SoulWire(p,option,4271));
            Check(!Fenced(p) && MonkReceipts(p)==1 && skill.Level==level && skill.Experience==71 && (scroll==null || scroll.StackSize==2),"concurrent training replays preserve scrolls and prior practice: "+spell);
            p=ReloadBall(p);p.ActiveNpc=4271;SoulWire(p,option,4271);
            Check(!Fenced(p) && MonkReceipts(p)==1 && p.Spells[spell].Level==level && p.Spells[spell].PreviousLevel==3
                && p.Spells[spell].Experience==71 && (int)p.Spells[spell].Souls==1 && (scroll==null || SavedRune(scroll,"stacksize")==2),"full reload and retry preserve skill metadata and payment: "+spell);
        }
        foreach(byte school in new byte[]{60,61,62,63,64,65})foreach(var row in rows) {
            var p=PromotionPlayer(map,school,100,4271);var scroll=NestedPack(p,(uint)row[4],5,true);
            Parallel.For(0,16,_=>SoulWire(p,(byte)row[0],4271));
            Check(!Fenced(p) && p.Spells.ContainsKey((ushort)row[1]) && MonkReceipts(p)==1 && scroll.StackSize==4
                && SavedRune(scroll,"stacksize")==4,"simultaneous first requests spend one scroll at Monk rank "+school+" art "+row[1]);
        }
        foreach(var row in rows)foreach(uint wrong in new uint[]{3007564,3007565,3004460,(uint)(row[4]==3007568?3007566:3007568)}) {
            var p=PromotionPlayer(map,65,100,4271);var scroll=NestedPack(p,wrong,1,true);SoulWire(p,(byte)row[0],4271);
            Check(!Fenced(p) && p.Spells.Count==0 && MonkReceipts(p)==0 && SavedRune(scroll,"id")==wrong,
                "different quest materials and scrolls cannot buy art "+row[1]+" with "+wrong);
        }
        foreach(ushort stack in new ushort[]{0,1,2}) {
            var p=PromotionPlayer(map,65,100,4271);var scroll=NestedPack(p,3007568,stack,true);SoulWire(p,23,4271);
            Check(!Fenced(p) && p.Spells.ContainsKey(12550) && MonkReceipts(p)==1 && (stack<=1?!p.Inventory.ContainsUID(scroll.UID)
                && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND uid="+scroll.UID,p.Entity.UID)==0:scroll.StackSize==stack-1),"exact scroll debit handles legacy singleton and stacked rows: "+stack);
        }
        var preference=PromotionPlayer(map,65,100,4271);var unbound=NestedPack(preference,3007568,1,false);var owned=NestedPack(preference,3007568,1,true);SoulWire(preference,23,4271);
        Check(preference.Spells.ContainsKey(12550) && preference.Inventory.ContainsUID(unbound.UID) && !preference.Inventory.ContainsUID(owned.UID),"Head Abbot spends bound scroll before a tradable copy");
        foreach(byte option in new byte[]{23,24,25}) {
            var p=PromotionPlayer(map,15,130,4271);var scroll=NestedPack(p,3007568,1,true);SoulWire(p,option,4271);
            Check(!Fenced(p) && p.Spells.Count==0 && MonkReceipts(p)==0 && SavedRune(scroll,"id")==3007568,"forged cross-class selection grants no Monk art: "+option);
        }
        foreach(string mode in new[]{"missing","locked","suspicious","wardrobe","expired","time-left","warehouse","equipped","remote","trade","dead","fake","unloaded","missing-owner","wrong-npc","short","long"}) {
            var p=PromotionPlayer(map,65,100,4271);var scroll=NestedPack(p,3007568,1,true);
            if(mode=="missing")DisRemove(p,scroll);if(mode=="locked")scroll.Lock=1;if(mode=="suspicious")scroll.Suspicious=true;
            if(mode=="wardrobe")scroll.InWardrobe=true;if(mode=="expired")scroll.Days=1;if(mode=="time-left")scroll.TimeLeftInMinutes=1;
            if(mode=="warehouse")scroll.Warehouse=1;if(mode=="equipped")scroll.Position=1;if(mode=="remote")p.Entity.X+=20;
            if(mode=="trade")p.Trade.InTrade=true;if(mode=="dead")p.Entity.Hitpoints=0;if(mode=="fake")p.Fake=true;
            if(mode=="unloaded")p.Entity.FullyLoaded=false;if(mode=="missing-owner")Kernel.GamePool.Remove(p.Entity.UID);
            SoulWire(p,23,mode=="wrong-npc"?1u:4271u,mode=="short"?18:mode=="long"?121:20);
            Check(!p.Spells.ContainsKey(12550) && MonkReceipts(p)==0 && (mode=="missing" || SavedRune(scroll,"id")==3007568),"invalid learning interaction preserves scroll: "+mode);
        }
        foreach(ushort stack in new ushort[]{1,2})foreach(string table in new[]{"entities","items","skills","revora_monk_training"}) {
            var p=PromotionPlayer(map,65,100,4271);var scroll=NestedPack(p,3007568,stack,true);
            Query("CREATE FUNCTION suppress_monk_training() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END'");
            Query("CREATE TRIGGER suppress_monk_training BEFORE INSERT OR UPDATE OR DELETE ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_monk_training()");
            try{SoulWire(p,23,4271);}finally{Query("DROP TRIGGER suppress_monk_training ON "+table+";DROP FUNCTION suppress_monk_training()");}
            Check(Fenced(p) && !p.Spells.ContainsKey(12550) && MonkReceipts(p)==0 && scroll.StackSize==stack && SavedRune(scroll,"stacksize")==stack
                && Scalar("SELECT count(*) FROM skills WHERE entityid=@uid AND id=12550",p.Entity.UID)==0,"failed write rolls back scroll, skill and receipt: "+table+" stack="+stack);
            p=ReloadBall(p);Check(!p.Spells.ContainsKey(12550) && p.Inventory.ContainsUID(scroll.UID),"reload exposes the rolled-back scroll with no unearned skill");
        }
        foreach(string conflict in new[]{"class","mapid","skill","item-owner","item-stack","item-bound"}) {
            var p=PromotionPlayer(map,65,100,4271);var scroll=NestedPack(p,3007568,2,true);
            if(conflict=="skill")Query("INSERT INTO skills(entityid,id,level,previouslevel,experience,levelhu,levelhu2) VALUES(@uid,12550,0,0,0,0,0)","uid",(long)p.Entity.UID);
            else if(conflict.StartsWith("item-"))Query("UPDATE items SET "+(conflict=="item-owner"?"entityid=1":conflict=="item-stack"?"stacksize=3":"bound=0")+" WHERE uid=@uid","uid",(long)scroll.UID);
            else Query("UPDATE entities SET "+conflict+"=@value WHERE uid=@uid","value",conflict=="class"?15:1002,"uid",(long)p.Entity.UID);
            SoulWire(p,23,4271);
            Check(Fenced(p) && !p.Spells.ContainsKey(12550) && MonkReceipts(p)==0 && scroll.StackSize==2,"saved ownership conflict grants no skill and spends nothing: "+conflict);
        }
    }
}
