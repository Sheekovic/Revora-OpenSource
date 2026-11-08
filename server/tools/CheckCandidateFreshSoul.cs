using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Game.Features;
using Revora.Network.GamePackets;
using Console=System.Console;
internal static partial class CheckCandidateArenaLifecycle {
    static string FreshSoulSnapshot(GameState p) {return Convert.ToString(Sql(@"SELECT json_build_object('career',@career::json,
'variables',(SELECT encode(data,'hex') FROM variablevault WHERE entityid=@uid),
'purse',(SELECT json_build_array(revorapoint,money,conquerpoints,boundcps) FROM entities WHERE uid=@uid),
'claims',(SELECT json_agg(r ORDER BY life) FROM revora_soul_trial_claims r WHERE uid=@uid))::text","career",FreshWarriorSnapshot(p),"uid",(long)p.Entity.UID));}
    static void FreshSoulSave(GameState p,int kills,int hits,string stage) {Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_fresh_soul_probe SET kills=@kills,hits=@hits,stage=@stage,snapshot=@snapshot","kills",kills,"hits",hits,"stage",stage,"snapshot",FreshSoulSnapshot(p));}
    static void FreshSoulProcess(string phase) {
        bool reload=phase.EndsWith("reload"),claim=phase.EndsWith("claim");var p=FreshLoadSaved(true);uint uid=p.Entity.UID;
        EntityVariableTable.Load(p);SubClassTable.Load(p.Entity);ChiTable.Load(p);p.Quests.Load();
        Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && p.Entity.Reborn==2 && p.Entity.Class==25 && !PirateFenced(p));
        if((bool)Sql("SELECT to_regclass('revora_fresh_soul_probe') IS NULL")) {
            Require(phase=="career-fresh-soul-hunt" && FreshWarriorSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_warrior_promotion_probe")));
            Require((long)Sql("SELECT count(*) FROM revora_soul_trial_claims WHERE uid=@uid","uid",(long)uid)==0);
            Sql("CREATE TABLE revora_fresh_soul_probe(uid bigint PRIMARY KEY,kills integer,hits integer,stage text,snapshot text);INSERT INTO revora_fresh_soul_probe VALUES(@uid,0,0,'new',@snapshot)","uid",(long)uid,"snapshot",FreshSoulSnapshot(p));
        }
        Require(FreshSoulSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_soul_probe")));
        Require(string.Join(",",p.Warehouses[Revora.Game.ConquerStructures.Warehouse.WarehouseID.TwinCity].Objects.OrderBy(i=>i.UID).Select(i=>i.UID.ToString(System.Globalization.CultureInfo.InvariantCulture)))
            ==Convert.ToString(Sql("SELECT coalesce(string_agg(uid::text,',' ORDER BY uid),'') FROM items WHERE entityid=@uid AND warehouse=8","uid",(long)uid)));

        int kills=Convert.ToInt32(Sql("SELECT kills FROM revora_fresh_soul_probe")),hits=Convert.ToInt32(Sql("SELECT hits FROM revora_fresh_soul_probe"));string stage=Convert.ToString(Sql("SELECT stage FROM revora_fresh_soul_probe"));
        if(phase=="career-fresh-soul-hunt") {
            if(stage=="new") {
                // Ordinary public warehouse packets preserve all previous bag items.
                CombinedStash(p);p.Entity.Teleport(1002,319,295);HeartTalk(p,SoulTrialSystem.NpcId,1);
                Require((uint)p[SoulTrialSystem.LifeKey]==3 && !PirateFenced(p));FreshSoulSave(p,kills,hits,"hunt");
            }
            p.Attackable=true;p.CantAttack=Time32.Now.AddSeconds(-1);
            string[] keys={SoulTrialSystem.BirdKey,SoulTrialSystem.ApeKey,SoulTrialSystem.SandKey},names={"birdman","macaque","sandmonster"};
            ushort[] maps={1015,1020,1000};uint[] needs={Config.SoulTrialBirdKills,Config.SoulTrialApeKills,Config.SoulTrialSandKills};
            for(int index=0;index<3;index++) {
                if((uint)p[keys[index]]>=needs[index])continue;
                p.Entity.Teleport(1002,319,295);HeartTalk(p,SoulTrialSystem.NpcId,(byte)(3+index));Require(p.Entity.MapID==maps[index]);
                while((uint)p[keys[index]]<needs[index]) {
                    var body=p.Map.Entities.Values.Where(e=>e.MonsterInfo!=null && !e.Dead && e.MonsterInfo.Name.StartsWith(names[index],StringComparison.OrdinalIgnoreCase) && !e.MonsterInfo.Boss && !e.MonsterInfo.Guard).OrderBy(e=>e.UID).FirstOrDefault();Require(body!=null);
                    p.Entity.X=(ushort)(body.X+1);p.Entity.Y=body.Y;p.Screen.Add(body);uint before=(uint)p[keys[index]],hp=body.Hitpoints;int strikes=0;
                    while(!body.Dead && strikes<500) {p.Entity.AttackStamp=Time32.Now.AddSeconds(-2);Attack.HandleAttack(new Attack(true){Attacker=uid,Attacked=body.UID,X=body.X,Y=body.Y,AttackType=Attack.Melee},p);strikes++;hits++;if(strikes==1)Require(body.Hitpoints<hp);}
                    Require(body.Dead && !p.Entity.Dead && !PirateFenced(p) && (uint)p[keys[index]]==before+1);kills++;FreshSoulSave(p,kills,hits,"hunt");
                }
                Console.WriteLine("[fresh-soul] map="+maps[index]+" earned="+(uint)p[keys[index]]);
            }
            p.Entity.Teleport(1002,319,295);HeartTalk(p,SoulTrialSystem.NpcId,0);FreshSoulSave(p,kills,hits,"ready");
        } else if(claim) {
            Require(stage=="ready");uint cps=p.Entity.ConquerPoints,bound=p.Entity.BoundCps,rvp=p.Entity.RevoraPoint;ulong silver=p.Entity.Money;byte level=p.Entity.Level;
            HeartTalk(p,SoulTrialSystem.NpcId,2);Require(!PirateFenced(p) && p.Entity.Level==level && p.Entity.ConquerPoints==cps && p.Entity.BoundCps==bound && p.Entity.RevoraPoint==rvp+Config.SoulTrialRevoraPoints && p.Entity.Money==silver+Config.SoulTrialGold);
            Require((long)Sql("SELECT count(*) FROM revora_soul_trial_claims WHERE uid=@uid AND life=2","uid",(long)uid)==1);
            var ids=(long[])Sql("SELECT rewards FROM revora_soul_trial_claims WHERE uid=@uid AND life=2","uid",(long)uid);var rewards=p.Inventory.Objects.Where(i=>ids.Contains((long)i.UID)).ToArray();
            Require(rewards.Length==1+Config.SoulTrialExpPotions+Config.SoulTrialPuffPotions && rewards.Count(i=>i.Bound && ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7)==1 && rewards.Count(i=>i.ID==Config.SoulTrialExpPotionId && !i.Bound)==Config.SoulTrialExpPotions+Config.SoulTrialPuffPotions);
            FreshSoulSave(p,kills,hits,"paid");string once=FreshSoulSnapshot(p);HeartTalk(p,SoulTrialSystem.NpcId,2);Require(once==FreshSoulSnapshot(p));
        } else if(reload) {
            Require(stage=="paid" && (long)Sql("SELECT count(*) FROM revora_soul_trial_claims WHERE uid=@uid AND life=2","uid",(long)uid)==1);
            string once=FreshSoulSnapshot(p);HeartTalk(p,SoulTrialSystem.NpcId,1);HeartTalk(p,SoulTrialSystem.NpcId,2);Require(once==FreshSoulSnapshot(p));
        }
        Require(FreshSoulSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_soul_probe")) && !PirateFenced(p));
        Console.WriteLine("[career-process] "+phase+"-passed uid="+uid+" kills="+kills+" hits="+hits+" bag="+p.Inventory.Count+" stored="+p.Warehouses.Values.Sum(w=>w.Count)+" rvp="+p.Entity.RevoraPoint+" silver="+p.Entity.Money);
        Server.CommandsAI("@close");throw new InvalidOperationException("Fresh Soul Trial did not close.");
    }
}
