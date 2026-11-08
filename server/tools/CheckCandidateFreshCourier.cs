using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Game.Features;
using Revora.Network.GamePackets;
using C=Revora.Game.Features.CourierLoopSystem;
using Console=System.Console;
internal static partial class CheckCandidateArenaLifecycle {
    static string FreshCourierSnapshot(GameState p){return Convert.ToString(Sql(@"SELECT json_build_object('career',@career::json,
'vip',(SELECT json_build_array(viplevel,vipdays,vipleveldays) FROM entities WHERE uid=@uid),
'claims',(SELECT json_agg(r ORDER BY life) FROM revora_courier_claims r WHERE uid=@uid))::text","career",FreshSoulSnapshot(p),"uid",(long)p.Entity.UID));}
    static void FreshCourierSave(GameState p,int kills,int hits,string stage){Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_fresh_courier_probe SET kills=@kills,hits=@hits,stage=@stage,snapshot=@snapshot","kills",kills,"hits",hits,"stage",stage,"snapshot",FreshCourierSnapshot(p));}
    static void FreshCourierProcess(string phase){
        var p=FreshLoadSaved();uint uid=p.Entity.UID;EntityVariableTable.Load(p);SubClassTable.Load(p.Entity);ChiTable.Load(p);p.Quests.Load();
        Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && p.Entity.Reborn==2 && p.Entity.Class==25 && !PirateFenced(p));
        if((bool)Sql("SELECT to_regclass('revora_fresh_courier_probe') IS NULL")){
            Require(phase=="career-fresh-courier-hunt" && FreshSoulSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_soul_probe")) && (long)Sql("SELECT count(*) FROM revora_courier_claims WHERE uid=@uid","uid",(long)uid)==0);
            Sql("CREATE TABLE revora_fresh_courier_probe(uid bigint PRIMARY KEY,kills integer,hits integer,stage text,snapshot text);INSERT INTO revora_fresh_courier_probe VALUES(@uid,0,0,'new',@snapshot)","uid",(long)uid,"snapshot",FreshCourierSnapshot(p));
        }
        Require(FreshCourierSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_courier_probe")));
        int kills=Convert.ToInt32(Sql("SELECT kills FROM revora_fresh_courier_probe")),hits=Convert.ToInt32(Sql("SELECT hits FROM revora_fresh_courier_probe"));string stage=Convert.ToString(Sql("SELECT stage FROM revora_fresh_courier_probe"));
        if(phase.EndsWith("hunt")){
            if(stage=="new"){
                p.Entity.Teleport(1002,C.ClaimX,C.ClaimY);HeartTalk(p,C.NpcId,1);Require((uint)p[C.StageKey]==C.StageAccept);FreshCourierSave(p,kills,hits,"hunt");
            }
            if((uint)p[C.StageKey]==C.StageAccept){
                p.Entity.Teleport(1002,C.ClaimX,C.ClaimY);HeartTalk(p,C.NpcId,3);HeartTalk(p,CityGuardSystem.CommanderNpcId,C.CommanderOption);Require((uint)p[C.StageKey]==C.StageHunt);FreshCourierSave(p,kills,hits,"hunt");
            }
            if((uint)p[C.StageKey]==C.StageHunt){
                p.Entity.Teleport(1002,C.ClaimX,C.ClaimY);HeartTalk(p,C.NpcId,4);Require(p.Entity.MapID==1011);p.Attackable=true;p.CantAttack=Time32.Now.AddSeconds(-1);
                while((uint)p[C.BanditKey]<Config.CourierLoopBanditKills){
                    var body=p.Map.Entities.Values.Where(e=>e.MonsterInfo!=null && !e.Dead && e.MonsterInfo.Name.StartsWith("Bandit",StringComparison.OrdinalIgnoreCase) && !e.MonsterInfo.Boss && !e.MonsterInfo.Guard).OrderBy(e=>e.UID).FirstOrDefault();Require(body!=null);
                    p.Entity.X=(ushort)(body.X+1);p.Entity.Y=body.Y;p.Screen.Add(body);uint before=(uint)p[C.BanditKey],hp=body.Hitpoints;int strikes=0;
                    while(!body.Dead && strikes<500){p.Entity.AttackStamp=Time32.Now.AddSeconds(-2);Attack.HandleAttack(new Attack(true){Attacker=uid,Attacked=body.UID,X=body.X,Y=body.Y,AttackType=Attack.Melee},p);strikes++;hits++;if(strikes==1)Require(body.Hitpoints<hp);}
                    Require(body.Dead && !p.Entity.Dead && !PirateFenced(p) && (uint)p[C.BanditKey]==before+1);kills++;FreshCourierSave(p,kills,hits,"hunt");
                }
            }
            Require((uint)p[C.StageKey]==C.StageClerk);p.Entity.Teleport(1002,C.ClaimX,C.ClaimY);HeartTalk(p,C.NpcId,5);HeartTalk(p,CpShopClerkSystem.NpcId,C.ClerkOption);Require((uint)p[C.StageKey]==C.StageTurnIn);
            p.Entity.Teleport(1002,C.ClaimX,C.ClaimY);HeartTalk(p,C.NpcId,0);FreshCourierSave(p,kills,hits,"ready");
        }else if(phase.EndsWith("claim")){
            Require(stage=="ready" && p.Entity.Level<Config.CourierLoopPuff);uint cps=p.Entity.ConquerPoints,bound=p.Entity.BoundCps,rvp=p.Entity.RevoraPoint;ulong silver=p.Entity.Money;byte vip=p.Entity.VIPLevel,days=p.Entity.VIPDays;
            HeartTalk(p,C.NpcId,2);Require(!PirateFenced(p) && p.Entity.Level==Config.CourierLoopPuff && p.Entity.ConquerPoints==cps+Config.CourierLoopCp && p.Entity.BoundCps==bound && p.Entity.RevoraPoint==rvp+Config.CourierLoopRevoraPoints && p.Entity.Money==silver+Config.CourierLoopGold);
            if(vip>=Config.CourierLoopVipLevel && days==0)Require(p.Entity.VIPLevel==vip && p.Entity.VIPDays==days);
            else Require(p.Entity.VIPLevel>=Config.CourierLoopVipLevel);
            Require((long)Sql("SELECT count(*) FROM revora_courier_claims WHERE uid=@uid AND life=2","uid",(long)uid)==1);
            uint soul=Convert.ToUInt32(Sql("SELECT soul_uid FROM revora_courier_claims WHERE uid=@uid AND life=2","uid",(long)uid));var item=p.Inventory.Objects.Single(i=>i.UID==soul);Require(item.Bound && ConquerItemInformation.BaseInformations[item.ID].PurificationLevel==7);
            FreshCourierSave(p,kills,hits,"paid");string once=FreshCourierSnapshot(p);HeartTalk(p,C.NpcId,2);Require(once==FreshCourierSnapshot(p));
        }else{
            Require(phase.EndsWith("reload") && stage=="paid" && p.Entity.Level==Config.CourierLoopPuff);string once=FreshCourierSnapshot(p);HeartTalk(p,C.NpcId,1);HeartTalk(p,C.NpcId,2);Require(once==FreshCourierSnapshot(p));
        }
        Require(FreshCourierSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_courier_probe")) && !PirateFenced(p));
        Console.WriteLine("[career-process] "+phase+"-passed uid="+uid+" level="+p.Entity.Level+" kills="+kills+" hits="+hits+" cps="+p.Entity.ConquerPoints+" rvp="+p.Entity.RevoraPoint+" silver="+p.Entity.Money+" vip="+p.Entity.VIPLevel+" days="+p.Entity.VIPDays);
        Server.CommandsAI("@close");throw new InvalidOperationException("Fresh Courier did not close.");
    }
}
