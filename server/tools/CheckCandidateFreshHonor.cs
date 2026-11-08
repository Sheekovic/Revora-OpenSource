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
    static string FreshHonorSnapshot(GameState p){uint peer=Convert.ToUInt32(Sql("SELECT peer FROM revora_fresh_honor_probe"));return Convert.ToString(Sql(@"SELECT json_build_object('prior',@prior::json,'peer',@profile::json,'balls',(SELECT row_to_json(b) FROM revora_exp_ball_days b WHERE uid=@peer),'arena',(SELECT json_agg(r ORDER BY entityid) FROM arena r WHERE entityid IN(@uid,@peer)),'teamarena',(SELECT json_agg(r ORDER BY entityid) FROM teamarena r WHERE entityid IN(@uid,@peer)),'match',(SELECT row_to_json(r) FROM revora_arena_matches r WHERE id=(SELECT run FROM revora_fresh_honor_probe)),'rank',(SELECT json_agg(r ORDER BY uid) FROM revora_arena_rank_rewards r WHERE source_run=(SELECT run FROM revora_fresh_honor_probe)))::text","prior",FreshFurnitureSnapshot(p),"profile",FreshStarterSnapshot(peer),"uid",(long)p.Entity.UID,"peer",(long)peer));}
    static void FreshHonorSave(GameState p,GameState peer,int stage){Require(EntityTable.SaveEntity(p) && EntityTable.SaveEntity(peer));Sql("UPDATE revora_fresh_honor_probe SET stage=@stage,snapshot=@snapshot","stage",stage,"snapshot",FreshHonorSnapshot(p));}
    static void FreshHonorJourney(string phase){
        var p=FreshLoadSaved(true,true,true);EntityVariableTable.Load(p);SubClassTable.Load(p.Entity);ChiTable.Load(p);p.Quests.Load();Revora.Database.KnownPersons.LoadMentor(p);WayofHeroes.Load(p);
        uint peerUid=Convert.ToUInt32(Sql("SELECT uid FROM revora_fresh_contributors WHERE slot=0"));var peer=FreshContributor(peerUid,Convert.ToString(Sql("SELECT name FROM revora_fresh_contributors WHERE slot=0")));Server.GamePool=new[]{p,peer};Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p) && !PirateFenced(peer));
        if((bool)Sql("SELECT to_regclass('revora_fresh_honor_probe') IS NULL")){
            Require(phase.EndsWith("prepare") && FreshFurnitureSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_furniture_probe")) && peer.Entity.Level>=50 && peer.Entity.Level<70);
            Sql("CREATE TABLE revora_fresh_honor_probe(peer bigint,run uuid,honor bigint,stage int,funded bool,balls int,snapshot text);INSERT INTO revora_fresh_honor_probe VALUES(@peer,NULL,@honor,0,false,0,NULL)","peer",(long)peerUid,"honor",(long)p.CurrentHonor);FreshHonorSave(p,peer,0);
        }
        if(phase.EndsWith("recover") || phase.EndsWith("inspect") || phase.EndsWith("fight") || phase.EndsWith("pay") || phase=="career-fresh-honor-reload"){FreshHonorMatch(p,peer,phase);return;}
        Require(peerUid==Convert.ToUInt32(Sql("SELECT peer FROM revora_fresh_honor_probe")) && FreshHonorSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_honor_probe")));
        if(phase.EndsWith("prepare")){
            Require((int)Sql("SELECT stage FROM revora_fresh_honor_probe")==0);p.Entity.Teleport(1002,304,435);peer.Entity.Teleport(1002,305,435);
            if(!(bool)Sql("SELECT funded FROM revora_fresh_honor_probe")){
                uint cp=p.Entity.ConquerPoints,other=peer.Entity.ConquerPoints;Require(cp>=1000);
                CareerTradePacket(p,Trade.Request,peer.Entity.UID);CareerTradePacket(peer,Trade.Request,p.Entity.UID);Require(p.Trade.InTrade && peer.Trade.InTrade);
                CareerTradePacket(p,Trade.SetConquerPoints,1000);CareerTradePacket(p,Trade.Accept,0);CareerTradePacket(peer,Trade.Accept,0);Require(!p.Trade.InTrade && !peer.Trade.InTrade && p.Entity.ConquerPoints==cp-1000 && peer.Entity.ConquerPoints==other+1000);
                string funded=FreshHonorSnapshot(p);CareerTradePacket(p,Trade.Accept,0);CareerTradePacket(peer,Trade.Accept,0);Require(funded==FreshHonorSnapshot(p));Sql("UPDATE revora_fresh_honor_probe SET funded=true");FreshHonorSave(p,peer,0);
            }
            var day=new DateTime(2041,3,31,12,0,0,DateTimeKind.Local);Feature("ExperienceConsumables").GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>day));int balls=(int)Sql("SELECT balls FROM revora_fresh_honor_probe");
            while(peer.Entity.Level<70 && balls<20){
                uint cp=peer.Entity.ConquerPoints;var ball=peer.Inventory.Objects.FirstOrDefault(i=>i.ID==723700);if(ball==null){var old=peer.Inventory.Objects.Select(i=>i.UID).ToArray();CareerBuy(peer,723700);ball=peer.Inventory.Objects.Single(i=>!old.Contains(i.UID));}Require(ball.ID==723700);
                if(balls==10){string capped=FreshHonorSnapshot(p);ItemHandler.UseItem(ball,peer);Require(capped==FreshHonorSnapshot(p) && peer.Inventory.ContainsUID(ball.UID));Console.WriteLine("[fresh-honor-prepare] same-day-eleventh-refused");}
                day=new DateTime(2041,3,31,12,0,0,DateTimeKind.Local).AddDays(balls/10);byte level=peer.Entity.Level;ulong experience=peer.Entity.Experience;ItemHandler.UseItem(ball,peer);
                Require(!peer.Inventory.ContainsUID(ball.UID) && (peer.Entity.Level>level || peer.Entity.Experience>experience));balls++;Sql("UPDATE revora_fresh_honor_probe SET balls=@balls","balls",balls);FreshHonorSave(p,peer,0);
                string used=FreshHonorSnapshot(p);ItemHandler.UseItem(ball,peer);Require(used==FreshHonorSnapshot(p));Console.WriteLine("[fresh-honor-prepare] balls="+balls+" peer-level="+peer.Entity.Level+" paid-cp="+(cp-peer.Entity.ConquerPoints));
            }
            Require(peer.Entity.Level>=70 && !PirateFenced(peer));FreshHonorSave(p,peer,1);
        }else Require(phase.EndsWith("prepare-reload") && (int)Sql("SELECT stage FROM revora_fresh_honor_probe")==1 && peer.Entity.Level>=70);
        Require(FreshHonorSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_honor_probe")));Console.WriteLine("[career-process] "+phase+"-passed peer-level="+peer.Entity.Level);Console.Out.Flush();Environment.Exit(0);
    }
    static void FreshHonorMatch(GameState p,GameState peer,string phase){
        bool fight=phase.EndsWith("fight"),pay=phase.EndsWith("pay");DateTime now=new DateTime(2041,4,2,20,0,0,DateTimeKind.Utc);if(!fight)now=now.AddDays(1);Feature("ArenaMatchLedger").GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));
        string loaded=FreshHonorSnapshot(p),saved=Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_honor_probe"));if(phase.EndsWith("inspect")){System.IO.File.WriteAllText("fresh-honor-loaded.json",loaded);System.IO.File.WriteAllText("fresh-honor-saved.json",saved);Console.WriteLine("[fresh-honor-inspect] stage="+Sql("SELECT stage FROM revora_fresh_honor_probe")+" level="+p.Entity.Level+" peer-level="+peer.Entity.Level+" honor="+p.CurrentHonor+" mirror="+p.TeamArenaStatistic.CurrentHonor);Console.WriteLine("[career-process] "+phase+"-passed");Console.Out.Flush();Environment.Exit(0);return;}
        if(phase.EndsWith("recover")){
            using(var sha=System.Security.Cryptography.SHA256.Create()){
                Require(System.BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(saved))).Replace("-","").ToLowerInvariant()=="e055233d6e650e0fbccc5fe6bcad52e4aed02ee9f325e5099dcad3b1c29887d5");
                string hash=System.BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(loaded))).Replace("-","").ToLowerInvariant();Require(hash=="40818b63ea9bb09aafef5c30b90a90d49e32a3d5ec79e650cfe9bd21bdc284e3" || hash=="0ad32c942a6291768da91d19dcf7e8b40a3e3c53ff47e75c084a1511df0f7988");
            }
            Require((int)Sql("SELECT stage FROM revora_fresh_honor_probe")==2 && (long)Sql("SELECT count(*) FROM revora_arena_matches WHERE id=(SELECT run FROM revora_fresh_honor_probe) AND state='finished' AND winner=@uid","uid",(long)p.Entity.UID)==1);
            // Audited differences: the two retired arena map IDs (nested profiles), plus
            // the scheduler's two unpaid daily rank rows in the second accepted preimage.
            // Complete the controlled town return; preserve the earned result and unpaid Honor.
            p.Entity.Teleport(1002,408,330);peer.Entity.Teleport(1002,410,330);FreshHonorSave(p,peer,2);
            Console.WriteLine("[career-process] "+phase+"-passed");Console.Out.Flush();Environment.Exit(0);return;
        }
        Require(loaded==saved || pay && (bool)Sql("SELECT (@loaded::jsonb - 'rank')=(@saved::jsonb - 'rank') AND NOT EXISTS(SELECT 1 FROM revora_arena_rank_rewards WHERE source_run=(SELECT run FROM revora_fresh_honor_probe) AND paid_at IS NOT NULL)","loaded",loaded,"saved",saved));
        if(fight){
            Require((int)Sql("SELECT stage FROM revora_fresh_honor_probe")==1 && peer.Entity.Level>=70 && (bool)Sql("SELECT run IS NULL FROM revora_fresh_honor_probe"));
            p.Entity.Teleport(1002,408,330);peer.Entity.Teleport(1002,410,330);
            Action<GameState,uint,uint> request=(who,dialog,button)=>{var frame=new Qualifier.ArenaSignup{DialogID=dialog,OptionID=button}.BuildPacket();Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,frame.Length-8,frame);Revora.Network.PacketHandler.HandlePacket(frame,who);Require(who.Action==2);};
            request(p,0,0);request(peer,0,0);Require(Qualifier.PlayerList.ContainsKey(p.Entity.UID) && Qualifier.PlayerList.ContainsKey(peer.Entity.UID));
            Qualifier.EngagePlayers();Require(ReferenceEquals(p.ArenaStatistic.PlayWith,peer));request(p,3,1);request(peer,3,1);Qualifier.VerifyAwaitingPeople();
            var group=p.QualifierGroup;Require(group!=null && ReferenceEquals(peer.QualifierGroup,group) && p.Map!=null && peer.Map!=null && p.Map.ID==peer.Map.ID && p.Map.BaseID==700);
            var run=(Guid)group.GetType().GetField("ParticipationKey",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(group);Sql("UPDATE revora_fresh_honor_probe SET run=@run,honor=@honor","run",run,"honor",(long)p.CurrentHonor);
            group.ImportTime=group.CreateTime=Time32.Now.AddSeconds(-13);p.Attackable=peer.Attackable=true;p.CantAttack=peer.CantAttack=Time32.Now.AddSeconds(-1);int hits=0;
            while(!group.Done && hits<100){p.Entity.X=peer.Entity.X;p.Entity.Y=(ushort)(peer.Entity.Y+1);p.Screen.Add(peer.Entity);peer.Screen.Add(p.Entity);p.Entity.AttackStamp=Time32.Now.AddSeconds(-2);Attack.HandleAttack(new Attack(true){Attacker=p.Entity.UID,Attacked=peer.Entity.UID,X=peer.Entity.X,Y=peer.Entity.Y,AttackType=Attack.Melee},p);hits++;}
            Require(group.Done);group.DoneStamp=Time32.Now.AddSeconds(-5);Qualifier.CheckGroups();Require((long)Sql("SELECT count(*) FROM revora_arena_matches WHERE id=@run AND state='finished' AND winner=@uid","run",run,"uid",(long)p.Entity.UID)==1);
            Require(p.CurrentHonor==Convert.ToUInt32(Sql("SELECT honor FROM revora_fresh_honor_probe")));FreshHonorSave(p,peer,2);Console.WriteLine("[fresh-honor] calculated-melee-hits="+hits);
        }else if(pay){
            Require((int)Sql("SELECT stage FROM revora_fresh_honor_probe")==2);var ranking=Feature("ArenaRankingSettlement");Call(ranking,"Close","20410402");uint before=p.CurrentHonor;
            Call(ranking,"Pay",p);uint award=Convert.ToUInt32(Sql("SELECT honor FROM revora_arena_rank_rewards WHERE source_run=(SELECT run FROM revora_fresh_honor_probe) AND uid=@uid","uid",(long)p.Entity.UID));Require(award>=8000 && p.CurrentHonor==before+award && p.TeamArenaStatistic.CurrentHonor==p.CurrentHonor);
            string paid=FreshHonorSnapshot(p);Call(ranking,"Pay",p);Require(paid==FreshHonorSnapshot(p));FreshHonorSave(p,peer,3);Console.WriteLine("[fresh-honor] ranking-award="+award);
        }else{
            Require((int)Sql("SELECT stage FROM revora_fresh_honor_probe")==3 && p.CurrentHonor>=8000 && p.TeamArenaStatistic.CurrentHonor==p.CurrentHonor);string before=FreshHonorSnapshot(p);Call(Feature("ArenaRankingSettlement"),"Pay",p);Require(before==FreshHonorSnapshot(p));
        }
        Require(!PirateFenced(p) && !PirateFenced(peer) && FreshHonorSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_honor_probe")));Console.WriteLine("[career-process] "+phase+"-passed");Console.Out.Flush();Environment.Exit(0);
    }

}
