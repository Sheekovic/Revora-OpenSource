using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
internal static partial class CheckCandidateArenaLifecycle {
    static string FreshClassicSnapshot(GameState p) {return Convert.ToString(Sql(@"SELECT json_build_object('prior',@prior::json,'connector',(SELECT json_agg(r ORDER BY life) FROM revora_connector r WHERE uid=@uid),'spun',(SELECT json_agg(r ORDER BY life) FROM revora_spun_gold r WHERE uid=@uid))::text","prior",FreshWarriorEpicSnapshot(p),"uid",(long)p.Entity.UID));}
    static void FreshClassicSave(GameState p,string stage,int kills,int hits) {Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_fresh_classic_probe SET stage=@stage,kills=@kills,hits=@hits,snapshot=@snapshot","stage",stage,"kills",kills,"hits",hits,"snapshot",FreshClassicSnapshot(p));}
    static void FreshClassicTalk(GameState p,ushort map,uint npc,byte option) {var n=Kernel.Maps[map].Npcs[npc];if(p.Entity.MapID!=map)p.Entity.Teleport(map,n.X,(ushort)(n.Y+2));HeartTalk(p,npc,option);Require(!PirateFenced(p));}
    static void FreshClassicKill(GameState p,ushort map,uint family,ref int kills,ref int hits) {
        if(p.Entity.MapID!=map)p.Entity.Teleport(map,100,100);var body=p.Map.Entities.Values.FirstOrDefault(e=>e.MonsterInfo!=null && e.MonsterInfo.ID==family && !e.Dead);Require(body!=null);p.Entity.X=body.X;p.Entity.Y=(ushort)(body.Y+1);p.Screen.Add(body);p.Attackable=true;p.CantAttack=Time32.Now.AddSeconds(-1);int attacks=0;uint hp=body.Hitpoints;
        while(!body.Dead && attacks<1000){p.Entity.AttackStamp=Time32.Now.AddSeconds(-2);Attack.HandleAttack(new Attack(true){Attacker=p.Entity.UID,Attacked=body.UID,X=body.X,Y=body.Y,AttackType=Attack.Melee},p);attacks++;hits++;if(attacks==1 && body.Hitpoints==hp)System.Console.WriteLine("[fresh-classic] first swing caused no HP loss family="+family);}Require(body.Dead && body.Hitpoints<hp && !p.Entity.Dead && !PirateFenced(p));kills++;
    }
    static void FreshClassicProcess(string phase) {
        var p=FreshLoadSaved();EntityVariableTable.Load(p);SubClassTable.Load(p.Entity);ChiTable.Load(p);p.Quests.Load();Revora.Database.KnownPersons.LoadMentor(p);WayofHeroes.Load(p);
        Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));
        if((bool)Sql("SELECT to_regclass('revora_fresh_classic_probe') IS NULL")) {
            Require(phase.EndsWith("earn") && FreshWarriorEpicSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_warrior_epic_probe")) && (long)Sql("SELECT (SELECT count(*) FROM revora_connector WHERE uid=@uid)+(SELECT count(*) FROM revora_spun_gold WHERE uid=@uid)","uid",(long)p.Entity.UID)==0);
            Sql("CREATE TABLE revora_fresh_classic_probe(stage text,kills integer,hits integer,snapshot text);INSERT INTO revora_fresh_classic_probe VALUES('new',0,0,@snapshot)","snapshot",FreshClassicSnapshot(p));
        }
        Require(FreshClassicSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_classic_probe")));string stage=Convert.ToString(Sql("SELECT stage FROM revora_fresh_classic_probe"));int kills=Convert.ToInt32(Sql("SELECT kills FROM revora_fresh_classic_probe")),hits=Convert.ToInt32(Sql("SELECT hits FROM revora_fresh_classic_probe"));
        if(phase.EndsWith("earn")) {
            Require(stage=="new");FreshClassicTalk(p,1002,8,0);var bank=p.Warehouses.First(w=>(uint)w.Key==8).Value;int store=Math.Min(60-bank.Count,Math.Max(0,p.Inventory.Count-25));
            foreach(var item in p.Inventory.Objects.Where(i=>i.Days==0).Take(store).ToArray()){var frame=new byte[172];Revora.Network.Writer.WriteUInt16(164,0,frame);Revora.Network.Writer.WriteUInt16(1102,2,frame);Revora.Network.Writer.WriteUInt32(8,4,frame);frame[12]=1;frame[13]=10;Revora.Network.Writer.WriteUInt32(item.UID,18,frame);Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,164,frame);Warehouse.Process(frame,p);Require(!p.Inventory.ContainsUID(item.UID) && bank.Objects.Any(i=>i.UID==item.UID));}Require(p.Inventory.Count<=30);
            FreshClassicTalk(p,1002,900071727,51);FreshClassicTalk(p,1002,900071718,1);FreshClassicTalk(p,1011,4437,1);for(int i=0;i<5;i++)FreshClassicKill(p,1011,2140,ref kills,ref hits);FreshClassicTalk(p,1011,4437,1);FreshClassicTalk(p,1002,900071718,1);
            Require((long)Sql("SELECT count(*) FROM revora_connector WHERE uid=@uid AND life=2 AND stage=5 AND kills=5 AND cardinality(rewards)=0","uid",(long)p.Entity.UID)==1);
            FreshClassicTalk(p,1002,900071727,61);FreshClassicTalk(p,1011,4464,1);for(int i=0;!p.Inventory.Contains(729971,1) && i<200;i++)FreshClassicKill(p,1011,7,ref kills,ref hits);Require(p.Inventory.Contains(729971,1));FreshClassicTalk(p,1011,4464,1);FreshClassicTalk(p,1011,4465,1);
            p.Entity.X=100;p.Entity.Y=430;ItemHandler.UseItem(p.Inventory.Objects.Single(i=>i.ID==729972),p);FreshClassicKill(p,1011,9921723,ref kills,ref hits);Call(Feature("SpunGoldQuest"),"Tick");ItemHandler.UseItem(p.Inventory.Objects.Single(i=>i.ID==729973),p);FreshClassicTalk(p,1011,4464,1);FreshClassicTalk(p,1002,900071727,0);
            Require((long)Sql("SELECT count(*) FROM revora_spun_gold WHERE uid=@uid AND life=2 AND stage=7 AND cardinality(rewards)=0","uid",(long)p.Entity.UID)==1);FreshClassicSave(p,"ready",kills,hits);
        } else if(phase.EndsWith("claim")) {
            Require(stage=="ready");var prior=p.Inventory.Objects.Select(i=>i.UID).ToArray();FreshClassicTalk(p,1002,900071727,51);FreshClassicTalk(p,1002,900071727,61);var added=p.Inventory.Objects.Where(i=>!prior.Contains(i.UID)).ToArray();
            Require(added.Length==5 && added.All(i=>i.Bound) && added.Count(i=>ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7)==2);
            Require((long)Sql("SELECT count(*) FROM revora_connector WHERE uid=@uid AND life=2 AND stage=6 AND cardinality(rewards)=3","uid",(long)p.Entity.UID)==1 && (long)Sql("SELECT count(*) FROM revora_spun_gold WHERE uid=@uid AND life=2 AND stage=8 AND cardinality(rewards)=2","uid",(long)p.Entity.UID)==1);FreshClassicSave(p,"paid",kills,hits);
        } else Require(phase.EndsWith("reload") && stage=="paid");
        if(!phase.EndsWith("earn")){
            var rewardIds=((long[])Sql("SELECT rewards FROM revora_connector WHERE uid=@uid AND life=2","uid",(long)p.Entity.UID)).Concat((long[])Sql("SELECT rewards FROM revora_spun_gold WHERE uid=@uid AND life=2","uid",(long)p.Entity.UID)).ToArray();Require(rewardIds.Length==5 && rewardIds.Distinct().Count()==5);
            var rewards=p.Inventory.Objects.Where(i=>rewardIds.Contains((long)i.UID)).ToArray();Require(rewards.Length==5 && rewards.All(i=>i.Bound) && rewards.Count(i=>i.ID==728236)==1 && rewards.Count(i=>i.ID==723911)==1 && rewards.Count(i=>i.ID==728237)==1 && rewards.Count(i=>ConquerItemInformation.BaseInformations[i.ID].PurificationLevel==7)==2);
        }
        if(!phase.EndsWith("earn")){string before=FreshClassicSnapshot(p);FreshClassicTalk(p,1002,900071727,51);FreshClassicTalk(p,1002,900071727,61);Require(before==FreshClassicSnapshot(p));}
        Require(FreshClassicSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_classic_probe")));System.Console.WriteLine("[career-process] "+phase+"-passed uid="+p.Entity.UID+" kills="+kills+" hits="+hits+" bag="+p.Inventory.Count);Server.CommandsAI("@close");throw new InvalidOperationException("Fresh classic quests did not close.");
    }
}
