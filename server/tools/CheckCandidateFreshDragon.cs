using System;
using System.Linq;
using System.Reflection;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
using Console=System.Console;
internal static partial class CheckCandidateArenaLifecycle {
    static string FreshDragonSnapshot(GameState p){return Convert.ToString(Sql(@"SELECT json_build_object('prior',@prior::json,'hunts',(SELECT json_agg(r ORDER BY branch) FROM revora_dragon_hunts r WHERE uid=1096322),'kills',(SELECT json_agg(k ORDER BY k.run,k.slot) FROM revora_dragon_hunt_kills k JOIN revora_dragon_hunts h ON h.id=k.run WHERE h.uid=1096322),'crafts',(SELECT json_agg(r ORDER BY id) FROM revora_dragon_pill_crafts r WHERE uid=1096322),'materials',(SELECT json_agg(m ORDER BY m.craft,m.item_uid) FROM revora_dragon_pill_materials m JOIN revora_dragon_pill_crafts c ON c.id=m.craft WHERE c.uid=1096322),'keys',(SELECT json_agg(r ORDER BY id) FROM revora_grotto_key_pickups r WHERE collector=1096322),'passages',(SELECT json_agg(r ORDER BY id) FROM revora_grotto_passages r WHERE uid=1096322))::text","prior",FreshDemonSnapshot(p)));}
    static void FreshDragonHunts(string phase){
        Require(phase=="career-fresh-dragon-hunts" || phase=="career-fresh-dragon-pill");var p=FreshLoadSaved(true,true,true);EntityVariableTable.Load(p);SubClassTable.Load(p.Entity);ChiTable.Load(p);p.Quests.Load();Revora.Database.KnownPersons.LoadMentor(p);WayofHeroes.Load(p);Server.GamePool=new[]{p};Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        if((bool)Sql("SELECT to_regclass('revora_fresh_dragon_hunts_probe') IS NULL")){
            Require(phase=="career-fresh-dragon-hunts" && FreshDemonSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_demon_probe")));
            FreshClassicTalk(p,1020,10028,0);var bank=p.Warehouses[Revora.Game.ConquerStructures.Warehouse.WarehouseID.ApeCity];int store=Math.Max(0,p.Inventory.Count-32);Require(bank!=null && bank.Count+store<=60);foreach(var item in p.Inventory.Objects.Where(i=>i.Days==0).Take(store).ToArray()){var frame=new byte[172];Revora.Network.Writer.WriteUInt16(164,0,frame);Revora.Network.Writer.WriteUInt16(1102,2,frame);Revora.Network.Writer.WriteUInt32(10028,4,frame);frame[12]=1;frame[13]=10;Revora.Network.Writer.WriteUInt32(item.UID,18,frame);Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,164,frame);Warehouse.Process(frame,p);Require(!p.Inventory.ContainsUID(item.UID) && bank.ContainsUID(item.UID));}Require(p.Inventory.Count<=32);FreshClassicTalk(p,1002,500010,0);
            Sql("CREATE TABLE revora_fresh_dragon_hunts_probe(done boolean NOT NULL,snapshot text);INSERT INTO revora_fresh_dragon_hunts_probe VALUES(false,@snapshot)","snapshot",FreshDragonSnapshot(p));
        }
        if(phase=="career-fresh-dragon-hunts" && !(bool)Sql("SELECT done FROM revora_fresh_dragon_hunts_probe")){
            Action<Entity> kill=mob=>{p.Entity.X=mob.X;p.Entity.Y=(ushort)(mob.Y+1);p.Screen.Add(mob);p.Attackable=true;p.CantAttack=Time32.Now.AddSeconds(-1);int hits=0;while(!mob.Dead && hits++<1000){p.Entity.AttackStamp=Time32.Now.AddSeconds(-2);Attack.HandleAttack(new Attack(true){Attacker=p.Entity.UID,Attacked=mob.UID,X=mob.X,Y=mob.Y,AttackType=Attack.Melee},p);}Require(mob.Dead && p.Action==2 && !p.Entity.Dead && !PirateFenced(p));};
            if(p.Entity.MapID==1002)CareerPortal(p,1000);
            if(p.Entity.MapID==1000)HeartTalk(p,27837,2);
            if(p.Entity.MapID==1927)CareerPortal(p,1999);
            if(p.Entity.MapID==1999){
                int kills=0;while(!p.Inventory.Objects.Any(x=>x.ID==721347) && kills<1000){
                    var mob=p.Map.Entities.Values.FirstOrDefault(e=>e.MonsterInfo!=null && !e.MonsterInfo.Boss && !e.MonsterInfo.Guard && !e.MonsterInfo.Reviver && !e.Dead && !e.Companion);
                    if(mob==null){Call(typeof(Map),"_timerCallBack",p.Map,Time32.Now.AddSeconds(600).TotalMilliseconds);mob=p.Map.Entities.Values.FirstOrDefault(e=>e.MonsterInfo!=null && !e.MonsterInfo.Boss && !e.MonsterInfo.Guard && !e.MonsterInfo.Reviver && !e.Dead && !e.Companion);}Require(mob!=null);
                    kill(mob);kills++;var drop=p.Map.FloorItems.Values.FirstOrDefault(d=>d.ItemID==721347 && !d.PickedUpAlready);
                    if(drop!=null){p.Entity.X=drop.X;p.Entity.Y=drop.Y;var packet=new FloorItem(true){UID=drop.UID,Type=FloorItem.Remove}.ToArray();Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,packet.Length-8,packet);FloorItem.Process(packet,p);}
                }
                Require(p.Inventory.Objects.Any(x=>x.ID==721347));Console.WriteLine("[career-dragon] key-hunt-kills="+kills);CareerPortal(p,2054);
            }
            if(p.Entity.MapID==2054)HeartTalk(p,7764,3);
            if(p.Entity.MapID==2055)CareerPortal(p,2056);
            Require(p.Entity.MapID==2056);
            var world=Feature("DragonSlayerWorld");var instance=BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Instance;
            Func<Entity[]> actors=()=>((System.Collections.IDictionary)world.GetField("Actors",Hidden).GetValue(null)).Values.Cast<object>().Where(a=>{var hunt=a.GetType().GetField("Hunt",instance).GetValue(a);return ReferenceEquals(hunt.GetType().GetField("Player",instance).GetValue(hunt),p);}).Select(a=>(Entity)a.GetType().GetField("Body",instance).GetValue(a)).ToArray();
            for(int branch=0;branch<7;branch++){
                if((long)Sql("SELECT count(*) FROM revora_dragon_hunts WHERE uid=@uid AND branch=@branch AND phase='complete'","uid",(long)p.Entity.UID,"branch",branch)>0)continue;
                HeartTalk(p,7767,(byte)(30+branch));var grass=p.Inventory.Objects.FirstOrDefault(x=>x.ID==711189+branch);
                if(grass!=null){ItemHandler.UseItem(grass,p);foreach(var mob in actors())kill(mob);HeartTalk(p,7767,40);}
                var pellet=p.Inventory.Objects.FirstOrDefault(x=>x.ID==720591+branch);HeartTalk(p,(uint)(900000120+branch),0);
                if(pellet!=null)ItemHandler.UseItem(pellet,p);else HeartTalk(p,(uint)(900000120+branch),1);
                foreach(var mob in actors())kill(mob);HeartTalk(p,7767,40);
                Require((long)Sql("SELECT count(*) FROM revora_dragon_hunts WHERE uid=@uid AND branch=@branch AND phase='complete'","uid",(long)p.Entity.UID,"branch",branch)==1 && p.Inventory.Objects.Any(x=>x.ID==711181+branch && x.Bound));
                Require(EntityTable.SaveEntity(p));Console.WriteLine("[career-dragon] branch="+branch+" earned");
            }
            HeartTalk(p,7767,6);HeartTalk(p,7767,60);Require(p.Inventory.Objects.Count(x=>x.ID==720598 && x.Bound)==1 && !p.Inventory.Objects.Any(x=>x.ID>=711181 && x.ID<=711187));
            Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_fresh_dragon_hunts_probe SET done=true,snapshot=@snapshot","snapshot",FreshDragonSnapshot(p));
            Console.WriteLine("[career-process] "+phase+"-passed");Console.Out.Flush();Environment.Exit(0);return;
        }
        Require((bool)Sql("SELECT done FROM revora_fresh_dragon_hunts_probe") && p.Inventory.Objects.Count(x=>x.ID==720598 && x.Bound)==1);
        Require((long)Sql("SELECT count(*) FROM revora_dragon_hunt_kills k JOIN revora_dragon_hunts h ON h.id=k.run WHERE h.uid=@uid","uid",(long)p.Entity.UID)==70);
        Require(FreshDragonSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_dragon_hunts_probe")));
        Console.WriteLine("[career-process] "+phase+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Dragon hunts process did not close.");
    }
}
