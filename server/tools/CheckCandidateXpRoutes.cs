using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
internal static partial class CheckCandidateArenaLifecycle
{
    static bool xpClaimFixture;
    static void XpClaimProcess(bool reload) {
        xpClaimFixture=true;const uint uid=4294916701;
        Sql("CREATE TABLE IF NOT EXISTS revora_xp_claim_probe(uid bigint PRIMARY KEY,snapshot text NOT NULL)");
        for(uint n=uid;n<=uid+1;n++)if((long)Sql("SELECT count(*) FROM entities WHERE uid=@uid","uid",(long)n)==0) {
            Require(!reload);nextUid=n;Player(Kernel.Maps[1002]);Sql("UPDATE entities SET level=120,reborn=1,class=55,experience=0,expballs=0,enlightenpoints=0,strength=2000,agility=2000 WHERE uid=@uid","uid",(long)n);
        }
        nextUid=uid;var p=Player(Kernel.Maps[1002],true);var outsider=Player(Kernel.Maps[1002],true);
        foreach(var actor in new[]{p,outsider}){SkillTable.LoadSpells(actor);ChiTable.Load(actor);WayofHeroes.Load(actor);actor.LoadItemStats();}
        Func<long> count=()=> (long)Sql("SELECT count(*) FROM revora_xp_kills WHERE uid=@uid","uid",(long)uid);
        Action<GameState> denied=actor=>{string prior=ChampionClaimSnapshot(actor);NobilityClaimPacket(actor,103);Require(prior==ChampionClaimSnapshot(actor) && !PirateFenced(actor));};
        denied(outsider);
        if((long)Sql("SELECT count(*) FROM revora_xp_claim_probe WHERE uid=@uid","uid",(long)uid)==0) {
            Require(!reload);
            Func<Entity> monster=()=>{
                var info=new MonsterInformation{ID=1,Name="XPClaim",Level=1,Hitpoints=10};
                var m=new Entity(EntityFlag.Monster,false){UID=p.Map.EntityUIDCounter.Next,MapID=p.Entity.MapID,X=p.Entity.X,Y=p.Entity.Y,MonsterInfo=info,Name="XPClaim",MaxHitpoints=10,MapObjType=MapObjectType.Monster,IsDropped=true};
                info.Owner=m;p.Map.Entities[m.UID]=m;m.Hitpoints=10;p.Screen.Add(m);return m;
            };
            Action<Entity> remove=m=>{p.Screen.Remove(m);p.Map.Entities.Remove(m.UID);};
            p.Spells[1010]=new Spell(true){ID=1010,Level=0,Available=true};p.Spells[6010]=new Spell(true){ID=6010,Level=0,Available=true};
            Action<ushort,Entity> cast=(id,target)=>{
                p.Entity.MagicAttack=100000;p.Entity.MinAttack=p.Entity.MaxAttack=p.Entity.BaseMinAttack=p.Entity.BaseMaxAttack=1000;
                p.Entity.Mana=10000;p.Entity.Stamina=150;p.Entity.AttackStamp=p.Entity.SpellStamp=Time32.Now.AddSeconds(-10);p.Spells[id].ColdTime=Time32.Now.AddSeconds(-10);
                p.Entity.AddFlag((ulong)FlagsUpdate.Flags.XPList,20,true);
                new Revora.Game.Attacking.Handle(new Attack(true){AttackType=Attack.Magic,Decoded=true,Damage=id,Attacker=uid,Attacked=target.UID,X=target.X,Y=target.Y},p.Entity,target);
            };
            if(count()<20) {
                denied(p);while(count()<19){long n=count();var m=monster();cast(1010,m);Require(m.Dead && count()==n+1);remove(m);}
                Require(count()==19);denied(p);
                var def=SpellTable.GetSpell(6010,0);
                if(def.OnlyWithThisWeaponSubtype.Count>0 && p.Equipment.TryGetItem(ConquerItem.RightWeapon)==null) {
                    var type=ConquerItemInformation.BaseInformations.Where(kv=>def.OnlyWithThisWeaponSubtype.Contains((ushort)(kv.Key/1000)) && ItemHandler.EquipPassStatsReq(kv.Value,p) && ItemHandler.EquipPassJobReq(kv.Value,p)).OrderBy(kv=>kv.Key).First().Key;
                    var weapon=p.Inventory.Objects.FirstOrDefault(item=>item.ID==type) ?? BattlePowerGear(p,type,0,0);RetainedUpgradeUsage(p,weapon.UID,ConquerItem.RightWeapon,ItemUsage.Mode.EquipItem);
                }
                p.LoadItemStats();cast(6010,p.Entity);Require((ushort)Call(Feature("XpEffectAttribution"),"Active",p.Entity)==6010);
                var target=monster();var pulse=p.Entity.VortexPacket;Require(pulse!=null);
                new Revora.Game.Attacking.Handle(pulse,p.Entity,null);Require(target.Dead && count()==20);remove(target);
                p.Entity.RemoveFlag((ulong)FlagsUpdate.Flags.ShurikenVortex);target=monster();new Revora.Game.Attacking.Handle(pulse,p.Entity,null);Require(!target.Dead && target.Hitpoints==10);remove(target);
            }
            Require(count()==20 && (long)Sql("SELECT count(*) FROM revora_xp_kills WHERE uid=@uid AND spell=6010","uid",(long)uid)>0);
            if((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=103","uid",(long)uid)==0) {
                p.Trade.InTrade=true;denied(p);p.Trade.InTrade=false;System.Threading.Tasks.Parallel.For(0,8,_=>NobilityClaimPacket(p,103));
            }
            Require(!PirateFenced(p) && p.Way2Heroes[1].SubStages[103].Done && p.Way2Heroes[1].SubStages[103].points==1);
            var reward=p.Inventory.Objects.SingleOrDefault(item=>item.ID==723700);
            if(reward!=null){Require(reward.Bound && reward.StackSize==1);ulong before=p.Entity.Experience,award=p.ExpBall;byte level=p.Entity.Level;SoulClaimUse(p,reward);Require(p.Entity.Level==level && p.Entity.Experience==before+award && p.ExpBalls==1);}
            Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=103 AND prize_type=723700 AND prize_count=1 AND NOT legacy","uid",(long)uid)==1);
            Require(p.Inventory.Objects.All(item=>item.ID!=723700) && EntityTable.SaveEntity(p));
            Sql("INSERT INTO revora_xp_claim_probe VALUES(@uid,@snapshot)","uid",(long)uid,"snapshot",ChampionClaimSnapshot(p));
        }
        Require(count()==20 && p.Way2Heroes[1].SubStages[103].Done && p.Inventory.Objects.All(item=>item.ID!=723700));
        string saved=Convert.ToString(Sql("SELECT snapshot FROM revora_xp_claim_probe WHERE uid=@uid","uid",(long)uid));Require(ChampionClaimSnapshot(p)==saved);NobilityClaimPacket(p,103);Require(ChampionClaimSnapshot(p)==saved);denied(outsider);
        System.Console.WriteLine("[career-process] career-xp-claim-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("XP claim did not close.");
    }
    static bool xpRoutesFixture;
    static void XpRoutesProcess(bool reload) {
        xpRoutesFixture=true;const uint first=4294916601;
        ushort[] ids={1015,1020,8002,10309,10390,11050,11060,11610,11970,12290,12700,12860};
        byte[] classes={15,25,45,145,65,75,75,45,15,85,25,165};
        Sql("CREATE TABLE IF NOT EXISTS revora_xp_routes_probe(uid bigint PRIMARY KEY,spell integer NOT NULL,receipts text NOT NULL)");
        for(int index=0;index<ids.Length;index++) {
            uint uid=first+(uint)index;ushort id=ids[index];
            bool exists=(long)Sql("SELECT count(*) FROM entities WHERE uid=@uid","uid",(long)uid)==1;
            Require(exists || !reload);
            if(!exists){nextUid=uid;Player(Kernel.Maps[1002]);}
            if(!reload && (long)Sql("SELECT count(*) FROM revora_xp_routes_probe WHERE uid=@uid","uid",(long)uid)==0)
                Sql("UPDATE entities SET level=140,reborn=2,class=@class,strength=2000,agility=2000,spirit=2000 WHERE uid=@uid","class",(int)classes[index],"uid",(long)uid);
            nextUid=uid;var p=Player(Kernel.Maps[1002],true);SkillTable.LoadSpells(p);
            Func<long> count=()=> (long)Sql("SELECT count(*) FROM revora_xp_kills WHERE uid=@uid","uid",(long)uid);
            Func<string> snapshot=()=>Convert.ToString(Sql("SELECT coalesce(json_agg(r ORDER BY life)::text,'[]') FROM revora_xp_kills r WHERE uid=@uid","uid",(long)uid));
            Func<ushort> active=()=> (ushort)Call(Feature("XpEffectAttribution"),"Active",p.Entity);
            if((long)Sql("SELECT count(*) FROM revora_xp_routes_probe WHERE uid=@uid","uid",(long)uid)==0) {
                Require(!reload);
                p.Entity.ClearFlags();p.Spells[id]=new Spell(true){ID=id,Level=0,Available=true};
                var definition=SpellTable.GetSpell(id,0);Require(definition!=null && definition.NeedXP==1);
                if(definition.OnlyWithThisWeaponSubtype.Count>0) {
                    var type=ConquerItemInformation.BaseInformations.Where(kv=>definition.OnlyWithThisWeaponSubtype.Contains((ushort)(kv.Key/1000))
                        && (id!=12700 || kv.Key/1000==624) && (id!=12860 || kv.Key/1000==626)
                        && ItemHandler.EquipPassStatsReq(kv.Value,p) && ItemHandler.EquipPassJobReq(kv.Value,p)).OrderBy(kv=>kv.Key).First().Key;
                    if(p.Equipment.TryGetItem(ConquerItem.RightWeapon)==null) {
                        var weapon=p.Inventory.Objects.FirstOrDefault(item=>item.ID==type) ?? BattlePowerGear(p,type,0,0);
                        RetainedUpgradeUsage(p,weapon.UID,ConquerItem.RightWeapon,ItemUsage.Mode.EquipItem);
                    }
                    if(id==12700 && p.Equipment.TryGetItem(ConquerItem.LeftWeapon)==null) {
                        var weapon=p.Inventory.Objects.FirstOrDefault(item=>item.ID==type) ?? BattlePowerGear(p,type,0,0);
                        RetainedUpgradeUsage(p,weapon.UID,ConquerItem.LeftWeapon,ItemUsage.Mode.EquipItem);
                    }
                    Require(p.Equipment.TryGetItem(ConquerItem.RightWeapon)!=null);
                }
                p.LoadItemStats();if(id==11610)p.Entity.AddFlag((ulong)FlagsUpdate.Flags.PathOfShadow,60,true);
                p.Entity.Mana=10000;p.Entity.Stamina=150;p.Entity.AttackStamp=p.Entity.SpellStamp=Time32.Now.AddSeconds(-10);
                p.Spells[id].ColdTime=Time32.Now.AddSeconds(-10);p.Entity.AddFlag((ulong)FlagsUpdate.Flags.XPList,20,true);
                new Revora.Game.Attacking.Handle(new Attack(true){AttackType=Attack.Magic,Decoded=true,Damage=id,Attacker=uid,Attacked=uid,X=p.Entity.X,Y=p.Entity.Y},p.Entity,p.Entity);
                System.Console.WriteLine("[xp-routes] spell="+id+" source="+active());Require(active()==id);
                long before=count();Require(before<20);
                var info=new MonsterInformation{ID=1,Name="XPRoute",Level=1,Hitpoints=10};
                var m=new Entity(EntityFlag.Monster,false){UID=p.Map.EntityUIDCounter.Next,MapID=p.Entity.MapID,X=p.Entity.X,Y=p.Entity.Y,MonsterInfo=info,Name="XPRoute",MaxHitpoints=10,MapObjType=MapObjectType.Monster,IsDropped=true};
                info.Owner=m;p.Map.Entities[m.UID]=m;m.Hitpoints=10;p.Screen.Add(m);
                for(int attempt=0;attempt<10 && !m.Dead;attempt++) {
                    p.Entity.MinAttack=p.Entity.MaxAttack=p.Entity.BaseMinAttack=p.Entity.BaseMaxAttack=1000;
                    var attack=new Attack(true){AttackType=Attack.Melee,Attacker=uid,Attacked=m.UID};uint damage=Revora.Game.Attacking.Calculate.Melee(p.Entity,m,ref attack);Require(damage>=10);
                    Revora.Game.Attacking.Handle.ReceiveAttack(p.Entity,m,attack,ref damage,null);
                    if(!m.Dead){Require(damage==0);System.Threading.Thread.Sleep(10);}
                }
                Require(m.Dead && !PirateFenced(p) && count()==before+1);
                Require((long)Sql("SELECT count(*) FROM revora_xp_kills WHERE uid=@uid AND spell=@spell","uid",(long)uid,"spell",(int)id)>0);
                p.Screen.Remove(m);p.Map.Entities.Remove(m.UID);p.Entity.ClearFlags();Require(active()==0 && EntityTable.SaveEntity(p));
                Sql("INSERT INTO revora_xp_routes_probe VALUES(@uid,@spell,@receipts)","uid",(long)uid,"spell",(int)id,"receipts",snapshot());
            }
            Require(active()==0 && snapshot()==Convert.ToString(Sql("SELECT receipts FROM revora_xp_routes_probe WHERE uid=@uid AND spell=@spell","uid",(long)uid,"spell",(int)id)));
        }
        System.Console.WriteLine("[career-process] career-xp-routes-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("XP routes did not close.");
    }
}
