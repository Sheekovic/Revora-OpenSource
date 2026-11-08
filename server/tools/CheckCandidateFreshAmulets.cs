using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
internal static partial class CheckCandidateArenaLifecycle {
    static string FreshAmuletsSnapshot(GameState p){return Convert.ToString(Sql(@"SELECT json_build_object('prior',@prior::json,'trials',(SELECT json_agg(r ORDER BY id) FROM revora_ancient_trials r WHERE uid IN(SELECT uid FROM revora_fresh_contributors)),'lives',(SELECT json_agg(l ORDER BY l.id) FROM revora_ancient_lives l JOIN revora_ancient_trials r ON r.id=l.trial WHERE r.uid IN(SELECT uid FROM revora_fresh_contributors)),'collection_items',(SELECT json_agg(i ORDER BY item_uid) FROM revora_ancient_collection_items i JOIN revora_ancient_collections c ON c.id=i.collection WHERE c.uid=@uid))::text","prior",FreshContributorsSnapshot(p),"uid",(long)p.Entity.UID));}
    static void FreshAmuletsSave(GameState p,GameState[] peers,string stage,int kills,int hits){foreach(var who in peers.Concat(new[]{p}))Require(EntityTable.SaveEntity(who));Sql("UPDATE revora_fresh_amulets_probe SET stage=@stage,kills=@kills,hits=@hits,snapshot=@snapshot","stage",stage,"kills",kills,"hits",hits,"snapshot",FreshAmuletsSnapshot(p));}
    static void FreshAmuletsProcess(string phase){
        var p=FreshLoadSaved(true,true);EntityVariableTable.Load(p);SubClassTable.Load(p.Entity);ChiTable.Load(p);p.Quests.Load();Revora.Database.KnownPersons.LoadMentor(p);WayofHeroes.Load(p);Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));var peers=Enumerable.Range(0,8).Select(i=>FreshContributor(Convert.ToUInt32(Sql("SELECT uid FROM revora_fresh_contributors WHERE slot=@slot","slot",i)),Convert.ToString(Sql("SELECT name FROM revora_fresh_contributors WHERE slot=@slot","slot",i)))).ToArray();Server.GamePool=peers.Concat(new[]{p}).ToArray();uint[] amulets={710012,710015,710635,711336,711600,3004947,3301712,710014};
        if((bool)Sql("SELECT to_regclass('revora_fresh_amulets_probe') IS NULL")){
            Require(phase.EndsWith("qualify") && FreshContributorsSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_contributors_probe")) && peers.All(w=>w.Entity.Level>=50));Sql("CREATE TABLE revora_fresh_amulets_probe(stage text,kills integer,hits integer,snapshot text);INSERT INTO revora_fresh_amulets_probe VALUES('new',0,0,@snapshot)","snapshot",FreshAmuletsSnapshot(p));
        }
        Require(FreshAmuletsSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_amulets_probe")));string stage=Convert.ToString(Sql("SELECT stage FROM revora_fresh_amulets_probe"));int kills=Convert.ToInt32(Sql("SELECT kills FROM revora_fresh_amulets_probe")),hits=Convert.ToInt32(Sql("SELECT hits FROM revora_fresh_amulets_probe"));
        if(phase.EndsWith("qualify")){
            Require(stage=="new");uint[] trainers={10022,400,4720,4271,4272,5061,51580,10000};byte[] classes={11,41,51,61,71,81,161,132};
            for(int n=0;n<peers.Length;n++){
                var who=peers[n];FreshClassicTalk(who,1004,trainers[n],1);HeartTalk(who,trainers[n],n==7?(byte)254:n==6?(byte)2:(byte)3);if(n==7){Require(who.Entity.Class==101);HeartTalk(who,10000,1);HeartTalk(who,10000,253);}Require(who.Entity.Class==classes[n]);
                foreach(var item in who.Inventory.Objects.Where(i=>i.ID!=727990).ToArray()){
                    ushort slot=ItemHandler.ItemPosition(item.ID);if(slot==0 || slot>8 || slot==7)continue;if(slot==4 && who.Equipment.TryGetItem(4)!=null)continue;ItemHandler.EquipItem(new ItemUsage(true){UID=item.UID,dwParam=slot,Action=ItemUsage.Mode.EquipItem},who);
                }
                Require(who.Equipment.GetItems().Count(i=>i.Position<20)>=6 && who.Equipment.TryGetItem(4)!=null && !PirateFenced(who));
            }
            FreshAmuletsSave(p,peers,"qualified",kills,hits);
        }else if(phase.EndsWith("earn")){
            Require(stage=="qualified");for(int n=0;n<peers.Length;n++){
                var who=peers[n];uint guard=n==0?300012u:n==1?300015u:n==7?300014u:300011u;FreshClassicTalk(who,1015,300010,0);HeartTalk(who,300010,1);Require(who.Entity.MapID==1052);HeartTalk(who,guard,0);HeartTalk(who,guard,1);Require(who.Entity.MapID>=11000);var mob=who.Map.Entities.Values.Single(e=>e.MonsterInfo!=null);who.Entity.X=mob.X;who.Entity.Y=(ushort)(mob.Y+1);who.Screen.Add(mob);who.Attackable=true;who.CantAttack=Time32.Now.AddSeconds(-1);uint hp=mob.Hitpoints;int attempts=0;
                while(!mob.Dead && attempts++<2000){who.Entity.AttackStamp=Time32.Now.AddSeconds(-2);Attack.HandleAttack(new Attack(true){Attacker=who.Entity.UID,Attacked=mob.UID,X=mob.X,Y=mob.Y,AttackType=n==1?Attack.Ranged:Attack.Melee},who);hits++;}Require(mob.Dead && mob.Hitpoints<hp && !who.Entity.Dead && !PirateFenced(who));kills++;Call(Feature("AncientDevilQuest"),"Tick");Require(who.Entity.MapID==1052);var prior=who.Inventory.Objects.Select(i=>i.UID).ToArray();HeartTalk(who,guard,0);HeartTalk(who,guard,2);var certificate=who.Inventory.Objects.Single(i=>!prior.Contains(i.UID));Require(certificate.Bound);HeartTalk(who,guard,3);var added=who.Inventory.Objects.Where(i=>!prior.Contains(i.UID)).ToArray();Require(added.Length==1 && added[0].ID==amulets[n] && !added[0].Bound && !who.Inventory.ContainsUID(certificate.UID) && prior.All(uid=>who.Inventory.ContainsUID(uid)));System.Console.WriteLine("[fresh-amulets] slot="+n+" attacks="+attempts+" amulet="+added[0].ID);
            }
            Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=11020","uid",(long)p.Entity.UID)==0);FreshAmuletsSave(p,peers,"earned",kills,hits);
        }else if(phase.EndsWith("trade")){
            Require(stage=="earned");FreshClassicTalk(p,1011,10012,0);var bank=p.Warehouses.First(w=>(uint)w.Key==10012).Value;int store=Math.Max(0,p.Inventory.Count-30);Require(bank.Count+store<=60);foreach(var item in p.Inventory.Objects.Where(i=>i.Days==0 && i.ID!=710011).Take(store).ToArray()){
                var frame=new byte[172];Revora.Network.Writer.WriteUInt16(164,0,frame);Revora.Network.Writer.WriteUInt16(1102,2,frame);Revora.Network.Writer.WriteUInt32(10012,4,frame);frame[12]=1;frame[13]=10;Revora.Network.Writer.WriteUInt32(item.UID,18,frame);Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,164,frame);Warehouse.Process(frame,p);Require(!p.Inventory.ContainsUID(item.UID) && bank.ContainsUID(item.UID));
            }
            Require(p.Inventory.Count<=30);FreshClassicTalk(p,1015,300010,0);HeartTalk(p,300010,1);Require(p.Entity.MapID==1052);for(int n=0;n<peers.Length;n++){
                var who=peers[n];HeartTalk(p,300017,0);HeartTalk(who,300017,0);uint uid=who.Inventory.Objects.Single(i=>i.ID==amulets[n]).UID;CareerTradePacket(who,Trade.Request,p.Entity.UID);CareerTradePacket(p,Trade.Request,who.Entity.UID);Require(p.Trade.InTrade && who.Trade.InTrade);CareerTradePacket(who,Trade.AddItem,uid);Require(who.Trade.Items.Count==1);CareerTradePacket(who,Trade.Accept,0);CareerTradePacket(p,Trade.Accept,0);Require(p.Inventory.ContainsUID(uid) && !who.Inventory.ContainsUID(uid) && !p.Trade.InTrade && !who.Trade.InTrade);
                Require((long)Sql("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=11020","uid",(long)p.Entity.UID)==(n==7?1:0));
            }
            new TitleStorage().Login(p);FreshAmuletsSave(p,peers,"collected",kills,hits);
        }else Require(phase.EndsWith("reload") && stage=="collected");
        if(phase.EndsWith("trade") || phase.EndsWith("reload"))Require((long)Sql("SELECT count(*) FROM revora_ancient_collection_items r JOIN revora_ancient_collections c ON c.id=r.collection JOIN items i ON i.uid=r.item_uid WHERE c.uid=@uid AND i.entityid=c.uid AND i.id=r.item_type AND i.bound=0","uid",(long)p.Entity.UID)==9 && p.Inventory.Objects.Count(i=>new uint[]{710011,710012,710015,710635,711336,711600,3004947,3301712,710014}.Contains(i.ID))==9);
        Require(FreshAmuletsSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_fresh_amulets_probe")) && !PirateFenced(p) && peers.All(w=>!PirateFenced(w)));System.Console.WriteLine("[career-process] "+phase+"-passed kills="+kills+" hits="+hits+" bag="+p.Inventory.Count);Server.CommandsAI("@close");throw new InvalidOperationException("Fresh amulets did not close.");
    }
}
