using System;
using System.Linq;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Game.Features;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static long TrialClaims(GameState p) { return Scalar("SELECT count(*) FROM revora_soul_trial_claims WHERE uid=@uid",p.Entity.UID); }
    static void TrialNpc(GameState p,byte option) { p.ActiveNpc=SoulTrialSystem.NpcId;Npcs.GetDialog(new NpcRequest(true){NpcID=SoulTrialSystem.NpcId,OptionID=option},p); }
    static void TrialReturn(GameState p) {
        p.Entity.Teleport(1002,320,295);var xy=FlowerTile(p.Map,320,295);p.Entity.X=xy.Item1;p.Entity.Y=xy.Item2;
        TrialNpc(p,0);
    }
    static GameState TrialPlayer(Map town,byte life=0,byte level=70) {
        var p=BallPlayer(town);p.Entity.Reborn=life;p.Entity.Level=level;p.Entity.Vitality=100;p.Entity.Experience=1234;
        p.Entity.X=320;p.Entity.Y=295;p.Variables=new VariableVault();
        Query("INSERT INTO variablevault(entityid,data) VALUES(@uid,@data)","uid",(long)p.Entity.UID,"data",p.Variables.Serialize());
        if(!EntityTable.SaveEntity(p))throw new Exception("Trial fixture profile save failed.");
        TrialNpc(p,1);Check(!Fenced(p) && p.Account.State==AccountTable.AccountState.Player && !p.Fake && (uint)p[SoulTrialSystem.LifeKey]==life+1,"ordinary player accepts Soul Trial for life "+life);
        return p;
    }
    static GameState ReloadTrial(GameState p) {p=ReloadBall(p);EntityVariableTable.Load(p);return p;}
    static void ReadyTrial(GameState p) {
        p[SoulTrialSystem.BirdKey]=Config.SoulTrialBirdKills;p[SoulTrialSystem.ApeKey]=Config.SoulTrialApeKills;p[SoulTrialSystem.SandKey]=Config.SoulTrialSandKills;
        EntityVariableTable.Save(p);
    }
    static GameState EarnTrial(GameState p) {
        string[] names={"Birdman","Macaque","SandMonster"};
        string[] keys={SoulTrialSystem.BirdKey,SoulTrialSystem.ApeKey,SoulTrialSystem.SandKey};
        uint[] need={Config.SoulTrialBirdKills,Config.SoulTrialApeKills,Config.SoulTrialSandKills};
        ushort[] maps={1015,1020,1000};
        for(int stage=0;stage<3;stage++) {
            TrialReturn(p);TrialNpc(p,(byte)(3+stage));
            Check(!Fenced(p) && p.Entity.MapID==maps[stage],"Soul Trial NPC travel reaches hunting map "+maps[stage]);
            string family=MonsterInformation.MonsterInformations.Values.First(m=>m.Name.Equals(names[stage],StringComparison.OrdinalIgnoreCase)).Name;
            uint familyId=MonsterInformation.MonsterInformations.Values.First(m=>m.Name==family).ID;
            using(var c=DataHolder.MySqlConnection) {c.Open();using(var q=new Npgsql.NpgsqlCommand("SELECT count(*) FROM public.monsterspawns WHERE npctype=@family AND mapid=@map",c)) {
                q.Parameters.AddWithValue("family",(long)familyId);q.Parameters.AddWithValue("map",(int)maps[stage]);
                Check(Convert.ToInt64(q.ExecuteScalar())>0,"installed spawn source supports Soul Trial family "+family+" on "+maps[stage]);
            }}
            for(int n=0;n<need[stage];n++) {
                var mob=HuntMonster(p,family);mob.Hitpoints=mob.MaxHitpoints=100;mob.IsDropped=true;mob.ClearFlagsMobs();uint damage=mob.Hitpoints;Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,mob,new Attack(true){AttackType=Attack.Melee,Attacker=p.Entity.UID,Attacked=mob.UID},ref damage,null);
                if(Fenced(p))throw new Exception("Soul Trial death fenced its owner.");
                SoulTrialSystem.OnMonsterKill(p,mob);
                if(n==24) {
                    EntityTable.SaveEntity(p);p=ReloadTrial(p);p.BindWorldMap(maps[stage]);
                    var xy=FlowerTile(p.Map,(ushort)(stage==0?717:stage==1?565:500),(ushort)(stage==0?571:stage==1?562:650));p.Entity.X=xy.Item1;p.Entity.Y=xy.Item2;
                    Check((uint)p[keys[stage]]==25,"earned hunt progress survives fresh variable and character reload: "+family);
                }
            }
            Check((uint)p[keys[stage]]==need[stage],"actual registered deaths earn exact quota without corpse replay: "+family);
        }
        TrialReturn(p);return p;
    }
    static void VerifySoulTrial(string runtime,Map town) {
        VerifyFirstRebirth(runtime,town);
        Query("CREATE TABLE IF NOT EXISTS variablevault(entityid bigint PRIMARY KEY,data bytea NOT NULL)");
        foreach(ushort id in new ushort[]{1015,1020,1000})World(runtime,id);
        SoulTrialSystem.Create();Call(typeof(SoulTrialSystem),null,"InitializeRewards");
        foreach(byte life in new byte[]{0,1,2}) {
            var p=EarnTrial(TrialPlayer(town,life));
            if(!EntityTable.SaveEntity(p))throw new Exception("Trial preclaim profile save failed.");
            ulong gold=p.Entity.Money;uint rvp=p.Entity.RevoraPoint;
            byte target=life==0?Config.SoulTrialPuff0:life==1?Config.SoulTrialPuff1:Config.SoulTrialPuff2;
            ushort attributes=p.Entity.Atributes,strength=p.Entity.Strength,agility=p.Entity.Agility,vitality=p.Entity.Vitality,spirit=p.Entity.Spirit;
            byte beforeLevel=p.Entity.Level;
            Parallel.For(0,8,i=>TrialNpc(p,2));
            var souls=p.Inventory.Objects.Where(i=>i.ID>=800000 && i.ID<900000).ToArray();
            Check(!Fenced(p) && TrialClaims(p)==1 && souls.Length==1 && souls[0].Bound && ConquerItemInformation.BaseInformations[souls[0].ID].PurificationLevel==7
                && p.Inventory.Objects.Count(i=>i.ID==Config.SoulTrialExpPotionId)==Config.SoulTrialExpPotions
                && p.Entity.Level==target && p.Entity.Experience==0 && p.Entity.Money==gold+Config.SoulTrialGold && p.Entity.RevoraPoint==rvp+Config.SoulTrialRevoraPoints,
                "earned life "+life+" commits exactly one bound soul, potions, puff and purse under concurrent claims");
            if(life!=0)Check(p.Entity.Atributes==Math.Min(ushort.MaxValue,attributes+(target-beforeLevel)*Config.AttributePointsPerLevel)
                && p.Entity.Strength==strength && p.Entity.Agility==agility && p.Entity.Vitality==vitality && p.Entity.Spirit==spirit,
                "reborn puff adds only configured free attributes and preserves allocated stats: life "+life);
            else Check(p.Entity.Atributes==attributes,"first-life puff preserves free attributes while applying installed class stats");
            ushort[] stats={p.Entity.Strength,p.Entity.Agility,p.Entity.Vitality,p.Entity.Spirit,p.Entity.Atributes};
            uint[] items=p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i).ToArray();uint soul= souls[0].UID;
            // No entity save here: reload must read the claim transaction itself.
            p=ReloadTrial(p);TrialReturn(p);TrialNpc(p,1);TrialNpc(p,2);
            Check(!Fenced(p) && TrialClaims(p)==1 && p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i).SequenceEqual(items)
                && p.Inventory.Objects.Single(i=>i.UID==soul).Bound && p.Entity.Level==target && p.Entity.Experience==0
                && p.Entity.Money==gold+Config.SoulTrialGold && p.Entity.RevoraPoint==rvp+Config.SoulTrialRevoraPoints
                && new ushort[]{p.Entity.Strength,p.Entity.Agility,p.Entity.Vitality,p.Entity.Spirit,p.Entity.Atributes}.SequenceEqual(stats)
                && ((uint)p[SoulTrialSystem.DoneKey]&(1u<<life))!=0,"fresh character/item/variable reload retains exact earned reward and refuses replay: life "+life);
        }
        foreach(string fault in new[]{"purse","progression","soul","potion","variables","receipt"}) {
            var p=TrialPlayer(town);ReadyTrial(p);ulong gold=p.Entity.Money;uint rvp=p.Entity.RevoraPoint;byte level=p.Entity.Level;
            string table=fault=="variables"?"variablevault":fault=="receipt"?"revora_soul_trial_claims":fault=="soul" || fault=="potion"?"items":"entities";
            string verb=table=="items" || fault=="receipt"?"INSERT":"UPDATE";
            string when=fault=="soul"?" WHEN(NEW.id BETWEEN 800000 AND 899999)":fault=="potion"?" WHEN(NEW.id="+Config.SoulTrialExpPotionId+")":fault=="progression"?" WHEN(NEW.level<>OLD.level)":"";
            Query("CREATE FUNCTION suppress_trial() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_trial BEFORE "+verb+" ON "+table+" FOR EACH ROW"+when+" EXECUTE FUNCTION suppress_trial()");
            try {TrialNpc(p,2);}finally {Query("DROP TRIGGER suppress_trial ON "+table+";DROP FUNCTION suppress_trial()");}
            Check(Fenced(p) && TrialClaims(p)==0 && p.Inventory.Count==0 && p.Entity.Level==level && p.Entity.Money==gold && p.Entity.RevoraPoint==rvp,
                "suppressed "+fault+" rolls back all Soul Trial rewards and fences uncertain session");
            p=ReloadTrial(p);TrialReturn(p);
            Check(!Fenced(p) && p.Entity.Level==level && p.Entity.Money==gold && p.Entity.RevoraPoint==rvp && (uint)p[SoulTrialSystem.DoneKey]==0
                && (uint)p[SoulTrialSystem.BirdKey]==Config.SoulTrialBirdKills && p.Inventory.Count==0,"fresh reload preserves unpaid completion and balances after "+fault);
            TrialNpc(p,2);Check(!Fenced(p) && TrialClaims(p)==1 && p.Inventory.Count==1+Config.SoulTrialExpPotions,"retry succeeds once after "+fault);
        }
        var high=TrialPlayer(town,0,135);ReadyTrial(high);for(int n=0;n<25;n++)Pack(high,1088001,1);
        TrialNpc(high,2);Check(!Fenced(high) && TrialClaims(high)==0 && high.Inventory.Count==25,"full reward capacity refusal preserves unpaid quest");
        DisRemove(high,high.Inventory.Objects.First());TrialNpc(high,2);
        Check(!Fenced(high) && TrialClaims(high)==1 && high.Inventory.Count==40 && high.Entity.Level==135
            && high.Inventory.Objects.Count(i=>i.ID==Config.SoulTrialExpPotionId)==Config.SoulTrialExpPotions+Config.SoulTrialPuffPotions,
            "already-high-level reward retains extra potions at exact bag capacity");
        var old=TrialPlayer(town);ReadyTrial(old);old[SoulTrialSystem.DoneKey]=1u;EntityVariableTable.Save(old);TrialNpc(old,2);
        Check(!Fenced(old) && TrialClaims(old)==0 && old.Inventory.Count==0,"legacy paid life remains paid without retroactive reward");
        var remote=TrialPlayer(town);ReadyTrial(remote);remote.Entity.X=10;remote.Entity.Y=10;TrialNpc(remote,2);
        Check(!Fenced(remote) && TrialClaims(remote)==0 && remote.Inventory.Count==0,"remote NPC continuation cannot claim Soul Trial");
        foreach(string field in new[]{"revorapoint","money","experience"}) {
            var p=TrialPlayer(town);ReadyTrial(p);Query("UPDATE entities SET "+field+"="+field+"+1 WHERE uid=@uid","uid",(long)p.Entity.UID);
            TrialNpc(p,2);Check(Fenced(p) && TrialClaims(p)==0 && p.Inventory.Count==0,"stale "+field+" cannot overwrite an external durable change");
            p=ReloadTrial(p);TrialReturn(p);TrialNpc(p,2);Check(!Fenced(p) && TrialClaims(p)==1,"reloaded "+field+" preimage permits one valid claim");
        }
        var paid=TrialPlayer(town);ReadyTrial(paid);TrialNpc(paid,2);uint paidRvp=paid.Entity.RevoraPoint;ulong paidGold=paid.Entity.Money;
        paid[SoulTrialSystem.DoneKey]=0u;paid[SoulTrialSystem.LifeKey]=1u;ReadyTrial(paid);TrialNpc(paid,2);
        Check(!Fenced(paid) && (uint)paid[SoulTrialSystem.DoneKey]==1 && TrialClaims(paid)==1 && paid.Entity.RevoraPoint==paidRvp && paid.Entity.Money==paidGold
            && paid.Inventory.Count==1+Config.SoulTrialExpPotions,"durable life receipt repairs a stale completion flag without duplicate reward");
        paid=ReloadTrial(paid);TrialReturn(paid);TrialNpc(paid,2);
        Check(!Fenced(paid) && (uint)paid[SoulTrialSystem.DoneKey]==1 && TrialClaims(paid)==1 && paid.Entity.RevoraPoint==paidRvp
            && paid.Entity.Money==paidGold,"receipt-repaired completion survives fresh reload without another payment");
        foreach(string currency in new[]{"revorapoint","money"}) {
            var p=TrialPlayer(town);ReadyTrial(p);
            Query("UPDATE entities SET "+currency+"=@value WHERE uid=@uid","value",currency=="money"?long.MaxValue:(long)uint.MaxValue,"uid",(long)p.Entity.UID);
            p=ReloadTrial(p);TrialReturn(p);TrialNpc(p,2);
            Check(!Fenced(p) && TrialClaims(p)==0 && p.Inventory.Count==0 && (uint)p[SoulTrialSystem.DoneKey]==0,"Soul Trial refuses overflowing "+currency+" before any reward");
        }
        var hunter=TrialPlayer(town);TrialNpc(hunter,4);
        foreach(string invalid in new[]{"alive","companion","removed","different-map","fake-family","far","wrong-owner","guard"}) {
            var mob=HuntMonster(hunter,"Macaque");
            if(invalid=="alive")mob.Hitpoints=100;if(invalid=="companion")mob.Companion=true;
            if(invalid=="removed")hunter.Map.Entities.Remove(mob.UID);if(invalid=="different-map")mob.MapID=1015;
            if(invalid=="fake-family")mob.MonsterInfo.ID=uint.MaxValue;if(invalid=="far")mob.X=(ushort)(hunter.Entity.X+40);
            if(invalid=="wrong-owner")mob.MonsterInfo.Owner=null;if(invalid=="guard")mob.MonsterInfo.Guard=true;
            SoulTrialSystem.OnMonsterKill(hunter,mob);Check(!Fenced(hunter) && (uint)hunter[SoulTrialSystem.ApeKey]==0,"Soul Trial rejects invalid kill source: "+invalid);
        }
        var revived=HuntMonster(hunter,"Macaque");SoulTrialSystem.OnMonsterKill(hunter,revived);SoulTrialSystem.OnMonsterKill(hunter,revived);
        Check((uint)hunter[SoulTrialSystem.ApeKey]==1,"same registered corpse credits only once");
        revived.Hitpoints=revived.MaxHitpoints=100;revived.ClearFlagsMobs();Call(typeof(SoulTrialSystem),null,"Revived",revived);revived.IsDropped=true;revived.Die(hunter.Entity);
        Check(!Fenced(hunter) && (uint)hunter[SoulTrialSystem.ApeKey]==2,"revival starts a new creditable monster life");
        SoulTrialSystem.OnReborn(hunter);EntityVariableTable.Load(hunter);
        Check((uint)hunter[SoulTrialSystem.LifeKey]==0 && (uint)hunter[SoulTrialSystem.ApeKey]==0,"rebirth reset durably clears unfinished trial progress");
    }
}
