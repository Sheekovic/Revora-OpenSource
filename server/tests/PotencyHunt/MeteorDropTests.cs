using System;
using System.Linq;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
internal static partial class PotencyHuntTests {
    static void VerifyMeteorDrops(string runtime,Map town) {
        Query("DROP TABLE entities;CREATE TABLE entities AS TABLE public.entities WITH NO DATA;CREATE TABLE titles (LIKE public.titles INCLUDING ALL)");
        foreach(string table in new[]{"teamarena","championarena","quests","status","reward","rewardclameid","merchant","dialyquests","activeness","house","pkexploits","achievement","itemadding"})
            Query("CREATE TABLE IF NOT EXISTS "+table+" AS TABLE public."+table+" WITH NO DATA");
        Query("CREATE TABLE prizes(uid bigint PRIMARY KEY,name text,prizes bytea)");
        StorageManager.Load();StorageItem.Load();PerfectionManager.AbilityScore.Read(out PerfectionManager.AbilitiesScore);
        RunesSystem.LoadRunesEXP();SpellTable.Load();Call(EvidenceType,null,"Initialize");
        foreach(ushort id in new ushort[]{1011,1020,1000,1004})World(runtime,id);
        ConquerItem.ItemUID=new Counter(7900000);
        Server.GamePool=new GameState[0];
        var type=typeof(Entity).Assembly.GetType("Revora.Game.Features.MeteorDrops");Call(type,null,"Initialize");
        var roll=type.GetField("Roll",Hidden);var oldRoll=roll.GetValue(null);int oldChance=Config.MeteorDropBasisPoints;
        Config.MeteorDropBasisPoints=100;roll.SetValue(null,new Func<int,int>(max=>0));
        Func<GameState> player=()=>{var q=FullRunePlayer(town,15);q.Entity.DailySignDays=new byte[0];EntityTable.SaveEntity(q);return q;};
        Func<GameState,FloorItem> drop=p=>{var before=town.FloorItems.Keys.ToArray();var m=LootMonster(p);Call(type,null,"TryDrop",m,p.Entity);Call(type,null,"TryDrop",m,p.Entity);
            return town.FloorItems.Values.Single(f=>!before.Contains(f.UID));};
        try {
            var p=player();var floor=drop(p);uint uid=floor.Item.UID;
            Check(!HasAchievement(p,10524) && floor.Item.ID==1088001,"creating one monster-origin Meteor per life does not award pickup credit");
            p.Entity.X=(ushort)(floor.X+10);p.Entity.Y=floor.Y;FloorItem.Process(LootPacket(floor),p);
            Check(!p.Inventory.ContainsUID(uid) && town.FloorItems.ContainsKey(floor.UID),"remote Meteor packet cannot collect the drop");
            p.Entity.X=floor.X;p.Entity.Y=floor.Y;Parallel.For(0,8,n=>FloorItem.Process(LootPacket(floor),p));
            Check(!Fenced(p) && p.Inventory.ContainsUID(uid) && HasAchievement(p,10524)
                && Scalar("SELECT count(*) FROM revora_meteor_pickups WHERE collector=@uid",p.Entity.UID)==1,"concurrent native-layout pickup grants one Meteor and one achievement receipt");
            p=ReloadBall(p);p.Entity.MyAchievement.Send();Check(p.Inventory.ContainsUID(uid) && HasAchievement(p,10524),"Meteor item and monster-loot achievement survive full login");
            var bought=player();Pack(bought,1088001,1);bought.Entity.MyAchievement.Send();Check(!HasAchievement(bought,10524),"inventory ownership of a Meteor cannot forge monster-loot credit");
            var owner=player();var reserved=drop(owner);var thief=player();Loot(thief,reserved);
            Check(!thief.Inventory.ContainsUID(reserved.Item.UID) && !HasAchievement(thief,10524) && town.FloorItems.ContainsKey(reserved.UID),
                "another player cannot bypass the Meteor ownership window");
            var full=player();var waiting=drop(full);while(full.Inventory.Count<40)Pack(full,1088001,1);Loot(full,waiting);
            Check(!full.Inventory.ContainsUID(waiting.Item.UID) && !HasAchievement(full,10524) && town.FloorItems.ContainsKey(waiting.UID),
                "full inventory preserves the floor Meteor without achievement credit");
            var late=player();var expired=drop(late);expired.OnFloor=Time32.Now.AddSeconds(-Constants.FloorItemSeconds-1);Loot(late,expired);
            Check(!late.Inventory.ContainsUID(expired.Item.UID) && !HasAchievement(late,10524),"expired Meteor cannot award an item or achievement");
            foreach(string fault in new[]{"item","receipt","achievement"}) {
                p=player();floor=drop(p);uid=floor.Item.UID;string table=fault=="item"?"items":fault=="receipt"?"revora_meteor_pickups":"revora_achievement_evidence";
                Query("CREATE FUNCTION suppress_meteor() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END'");
                Query("CREATE TRIGGER suppress_meteor BEFORE INSERT ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_meteor()");
                try{Loot(p,floor);}finally{Query("DROP TRIGGER suppress_meteor ON "+table);Query("DROP FUNCTION suppress_meteor()");}
                Check(Fenced(p) && town.FloorItems.ContainsKey(floor.UID) && !p.Inventory.ContainsUID(uid)
                    && Scalar("SELECT count(*) FROM revora_meteor_pickups WHERE collector=@uid",p.Entity.UID)==0
                    && Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10524",p.Entity.UID)==0,
                    "suppressed "+fault+" insert rolls back Meteor item, receipt and achievement");
                p=ReloadBall(p);Loot(p,floor);Check(!Fenced(p) && p.Inventory.ContainsUID(uid) && HasAchievement(p,10524),"reloaded collector retries unchanged Meteor once after "+fault+" failure");
            }
            p=player();int beforeCount=town.FloorItems.Count;roll.SetValue(null,new Func<int,int>(max=>100));Call(type,null,"TryDrop",LootMonster(p),p.Entity);
            Check(town.FloorItems.Count==beforeCount,"one-percent Meteor roll rejects the first value outside 0 through 99");
            roll.SetValue(null,new Func<int,int>(max=>0));var guard=LootMonster(p);guard.MonsterInfo.Guard=true;Call(type,null,"TryDrop",guard,p.Entity);
            Check(town.FloorItems.Count==beforeCount,"guard cannot generate ordinary wilderness Meteor loot");
            int oldEquipmentChance=Config.MonsterPlusEquipmentBasisPoints;Config.MonsterPlusEquipmentBasisPoints=0;
            try {
                var before=town.FloorItems.Keys.ToArray();var real=LootMonster(p);real.MaxHitpoints=10;real.Hitpoints=10;
                uint damage=10;Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,real,new Attack(true){Attacker=p.Entity.UID,Attacked=real.UID,AttackType=Attack.Melee},ref damage,null);
                var earned=town.FloorItems.Values.Single(f=>!before.Contains(f.UID) && (bool)Call(type,null,"Managed",f));Loot(p,earned);
                Check(!Fenced(p) && real.Dead && p.Inventory.ContainsUID(earned.Item.UID) && HasAchievement(p,10524),
                    "ordinary lethal combat dispatches monster Meteor drop and packet pickup earns the achievement");
            }finally{Config.MonsterPlusEquipmentBasisPoints=oldEquipmentChance;}

        }finally{roll.SetValue(null,oldRoll);Config.MeteorDropBasisPoints=oldChance;}
    }
}
