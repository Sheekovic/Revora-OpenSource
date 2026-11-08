using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;
internal static partial class CheckCandidateArenaLifecycle
{
    static bool pillFixture;
    const uint PillOwner=4294912501;
    sealed class PillLow:Random {public override int Next(int low,int high){return low;}public override int Next(int high){return 0;}}
    static GameState PillPlayer(uint uid=PillOwner){nextUid=uid;var p=Player(Kernel.Maps[1002],true);SubClassTable.Load(p.Entity);ChiTable.Load(p);WayofHeroes.Load(p);Call(typeof(Revora.Database.JiangHu),"LoadClient",p);p.LoadItemStats();Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));return p;}
    static long PillCount(bool pending){return (long)Sql("SELECT count(*) FROM revora_jiang_pill_uses WHERE uid=@uid AND (NOT @pending OR consumed_at IS NULL)","uid",(long)PillOwner,"pending",pending);}
    static string PillSnapshot(GameState p){return Convert.ToString(Sql(@"SELECT json_build_object('base',@base::json,'pills',
(SELECT json_agg(x ORDER BY x.armed_at,x.id) FROM revora_jiang_pill_uses x WHERE uid=@uid))::text","base",JiangClaimSnapshot(p),"uid",(long)p.Entity.UID));}
    static void PillUse(GameState p,ConquerItem item){var packet=new ItemUsage(true){UID=item.UID,Action=ItemUsage.Mode.EquipItem}.ToArray();Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,packet.Length-8,packet);ItemUsage.Process(packet,p);}
    static void PillTrain(GameState p,byte action=1,byte star=1,byte stage=1){var b=new byte[28];Revora.Network.Writer.WriteUInt16(20,0,b);Revora.Network.Writer.WriteUInt16(2702,2,b);b[9]=action;b[10]=star;b[11]=stage;Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,20,b);Revora.Network.PacketHandler.HandlePacket(b,p);}
    static void PillRestore(GameState p){var b=new byte[15];Revora.Network.Writer.WriteUInt16(7,0,b);Revora.Network.Writer.WriteUInt16(2700,2,b);b[4]=10;Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,7,b);Revora.Network.PacketHandler.HandlePacket(b,p);}
    static void PillProcess(int phase){
        pillFixture=true;uint[] stages={702,804,905,1003};
        if((bool)Sql("SELECT to_regclass('revora_pill_probe') IS NULL")){
            Require(phase==0 && (long)Sql("SELECT count(*) FROM entities WHERE uid=@uid","uid",(long)PillOwner)==0);
            nextUid=PillOwner;Player(Kernel.Maps[1002]);Sql("UPDATE entities SET level=130,reborn=2,conquerpoints=50000 WHERE uid=@uid","uid",(long)PillOwner);
            Sql("CREATE TABLE revora_pill_probe(step integer NOT NULL,snapshot text);INSERT INTO revora_pill_probe VALUES(0,NULL)");
        }
        var p=PillPlayer();int step=Convert.ToInt32(Sql("SELECT step FROM revora_pill_probe"));
        var training=Feature("JiangTraining");var roll=training.GetField("PillRoll",Hidden);object oldRoll=roll.GetValue(null);
        try {
            if(phase==0 && step==0){
                Require(p.ChiPowers.Count==0 && p.Inventory.Count==0);
                foreach(uint stage in stages)NobilityClaimPacket(p,stage);Require(p.Inventory.Count==0);
                ChiClaimRequest(p,Chi.BuyStrength,1);
                var next=typeof(GeneralData).GetField("ChiValueNext",Hidden);var rate=typeof(GeneralData).GetField("ChiValueRate",Hidden);object oldNext=next.GetValue(null),oldRate=rate.GetValue(null);
                try {next.SetValue(null,(Func<int,int,int>)((lo,hi)=>hi-1));rate.SetValue(null,(Func<int,int,bool>)((a,b)=>true));
                    for(byte power=1;power<=4;power++){
                        ChiClaimRequest(p,Chi.Unlock,power);
                        foreach(uint stage in stages)NobilityClaimPacket(p,stage);Require(p.Inventory.Count==0);
                        ChiClaimRequest(p,Chi.Study,power);Require(p.ChiPowers.Sum(x=>x.Points)==400*power);
                        if(power==3){p.ChiPowers[0].Points=1600;foreach(uint stage in stages)NobilityClaimPacket(p,stage);p.ChiPowers[0].CalculatePoints();Require(p.Inventory.Count==0);}
                    }
                }finally{next.SetValue(null,oldNext);rate.SetValue(null,oldRate);}
                foreach(uint stage in stages)System.Threading.Tasks.Parallel.For(0,8,_=>NobilityClaimPacket(p,stage));
                Require(p.Inventory.Count==19 && p.Inventory.Objects.All(x=>x.ID==3003126 && x.Bound && x.StackSize==1 && x.Plus==0));
                Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND NOT legacy","uid",(long)PillOwner)==4);
                // The reward cannot be spent before Jiang Hu exists.
                PillUse(p,p.Inventory.Objects.First());Require(p.Inventory.Count==19 && PillCount(false)==0);
                var create=new byte[19];Revora.Network.Writer.WriteUInt16(11,0,create);Revora.Network.Writer.WriteUInt16(2700,2,create);create[4]=1;create[5]=1;create[6]=4;Revora.Network.Writer.WriteString("Path",7,create);Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,11,create);Revora.Network.PacketHandler.HandlePacket(create,p);
                p.Entity.MyJiang.Random=new PillLow();PillTrain(p);Require(p.Entity.MyJiang.Stagers[0].Stars[0].Level==1 && PillCount(false)==0);
                var pill=p.Inventory.Objects.First();pill.Lock=1;PillUse(p,pill);pill.Lock=0;Require(p.Inventory.Count==19 && PillCount(false)==0);
                System.Threading.Tasks.Parallel.For(0,8,_=>PillUse(p,pill));Require(p.Inventory.Count==18 && PillCount(true)==1 && PillCount(false)==1);
                var another=p.Inventory.Objects.First();PillUse(p,another);Require(p.Inventory.Count==18 && PillCount(false)==1);
                string unchanged=PillSnapshot(p);PillTrain(p,3);PillTrain(p,1,1,9);Require(unchanged==PillSnapshot(p));
                Require(EntityTable.SaveEntity(p));Revora.Database.JiangHu.SaveJiangHu(p);Sql("UPDATE revora_pill_probe SET step=1,snapshot=@snapshot","snapshot",PillSnapshot(p));step=1;
            }
            if(phase==1 && step==1){
                Require(PillCount(true)==1 && p.Inventory.Count==18 && PillSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_pill_probe")));
                // Force a training rollback after the boost/result updates, then recover the ordinary session.
                string before=PillSnapshot(p);var hook=training.GetField("BeforeCommit",Hidden);object previous=hook.GetValue(null);
                try {hook.SetValue(null,(Action<GameState>)(_=>{throw new InvalidOperationException("Pill rollback fixture");}));p.Entity.MyJiang.Random=new PillLow();roll.SetValue(null,(Func<int,int>)(_=>9));PillTrain(p);}
                finally{hook.SetValue(null,previous);}
                Require(PirateFenced(p));p=PillPlayer();Require(before==PillSnapshot(p) && PillCount(true)==1);
                p.Entity.MyJiang.Random=new PillLow();roll.SetValue(null,(Func<int,int>)(_=>9));PillTrain(p);Require(!PirateFenced(p) && PillCount(true)==0 && p.Entity.MyJiang.Stagers[0].Stars[0].Level==6);
                // Restore must not consume the second armed pill.
                PillUse(p,p.Inventory.Objects.First());long used=PillCount(false);PillRestore(p);Require(!PirateFenced(p) && PillCount(true)==1 && PillCount(false)==used && p.Entity.MyJiang.Stagers[0].Stars[0].Level==1);
                p.Entity.MyJiang.Random=new PillLow();roll.SetValue(null,(Func<int,int>)(_=>10));PillTrain(p,0);Require(!PirateFenced(p) && PillCount(true)==0 && p.Entity.MyJiang.Stagers[0].Stars[0].Level==1);
                // Remaining earned pills: preserve naturally highest rolls; exercise both rescue boundaries.
                int index=0;while(p.Inventory.Count>0){PillUse(p,p.Inventory.Objects.First());Require(PillCount(true)==1);
                    bool maximum=index%3==0;int draw=index%3==1?9:10;
                    p.Entity.MyJiang.Random=maximum?(Random)new JiangClaimMaximum():new PillLow();
                    roll.SetValue(null,(Func<int,int>)(_=>{if(maximum)throw new InvalidOperationException("Natural maximum was rerolled");return draw;}));
                    PillTrain(p);Require(!PirateFenced(p) && PillCount(true)==0 && p.Entity.MyJiang.Stagers[0].Stars[0].Level==(maximum || draw<10?6:1));index++;
                }
                Require(PillCount(false)==19);Require(EntityTable.SaveEntity(p));Revora.Database.JiangHu.SaveJiangHu(p);Sql("UPDATE revora_pill_probe SET step=2,snapshot=@snapshot","snapshot",PillSnapshot(p));step=2;
            }
            Require(step==(phase==0?1:2));Require(PillSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_pill_probe")));
            string exact=PillSnapshot(p);foreach(uint stage in stages)NobilityClaimPacket(p,stage);Require(!PirateFenced(p) && exact==PillSnapshot(p));
        }finally{roll.SetValue(null,oldRoll);}
        System.Console.WriteLine("[career-process] career-pill-"+(phase==0?"earn":phase==1?"resume":"reload")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Pill process did not close.");
    }
    static void PillEdges(bool reload){
        pillFixture=true;const uint uid=PillOwner+1;
        if((bool)Sql("SELECT to_regclass('revora_pill_edges_probe') IS NULL")){
            Require(!reload && (long)Sql("SELECT count(*) FROM entities WHERE uid=@uid","uid",(long)uid)==0);nextUid=uid;Player(Kernel.Maps[1002]);
            Sql("UPDATE entities SET level=130,reborn=2,conquerpoints=0 WHERE uid=@uid","uid",(long)uid);Sql("CREATE TABLE revora_pill_edges_probe(snapshot text);INSERT INTO revora_pill_edges_probe VALUES(NULL)");
        }
        var p=PillPlayer(uid);var training=Feature("JiangTraining");object snapshot=Sql("SELECT snapshot FROM revora_pill_edges_probe");
        if(snapshot==DBNull.Value){Require(!reload);
            if(p.Entity.MyJiang==null){
                var create=new byte[19];Revora.Network.Writer.WriteUInt16(11,0,create);Revora.Network.Writer.WriteUInt16(2700,2,create);create[4]=1;create[5]=1;create[6]=4;Revora.Network.Writer.WriteString("Path",7,create);Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,11,create);Revora.Network.PacketHandler.HandlePacket(create,p);
                var supplied=new ConquerItem(true){ID=3003126,Bound=true,StackSize=2,Durability=1,MaximDurability=1};Require(p.Inventory.Add(supplied,Enums.ItemUse.CreateAndAdd));
            }
            long prior=(long)Sql("SELECT count(*) FROM revora_jiang_pill_uses WHERE uid=@uid","uid",(long)uid);
            if(prior==0){
            Require(p.Entity.ConquerPoints==0 && p.Inventory.Objects.Single().StackSize==2);
            var beforeCommit=training.GetField("BeforeCommit",Hidden);var beforeInstall=training.GetField("BeforeInstall",Hidden);
            object commit=beforeCommit.GetValue(null),install=beforeInstall.GetValue(null);string before=PillSnapshot(p);
            try {beforeCommit.SetValue(null,(Action<GameState>)(_=>{throw new InvalidOperationException("Arm rollback fixture");}));PillUse(p,p.Inventory.Objects.Single());}
            finally{beforeCommit.SetValue(null,commit);}
            Require(PirateFenced(p));p=PillPlayer(uid);Require(before==PillSnapshot(p));
            try {beforeInstall.SetValue(null,(Action<GameState>)(_=>{throw new InvalidOperationException("Arm install interruption fixture");}));PillUse(p,p.Inventory.Objects.Single());}
            finally{beforeInstall.SetValue(null,install);}
            Require(PirateFenced(p));p=PillPlayer(uid);Require(p.Inventory.Objects.Single().StackSize==1 && (long)Sql("SELECT count(*) FROM revora_jiang_pill_uses WHERE uid=@uid AND consumed_at IS NULL","uid",(long)uid)==1);
            before=PillSnapshot(p);PillTrain(p);PillUse(p,p.Inventory.Objects.Single());Require(!PirateFenced(p) && before==PillSnapshot(p));
            var roll=training.GetField("PillRoll",Hidden);object old=roll.GetValue(null);
            try {roll.SetValue(null,(Func<int,int>)(_=>10));p.Entity.MyJiang.Random=new PillLow();PillTrain(p,0);}finally{roll.SetValue(null,old);}
            }
            Require(!PirateFenced(p) && p.Entity.MyJiang.FreeCourse==0 && (long)Sql("SELECT count(*) FROM revora_jiang_pill_uses WHERE uid=@uid AND consumed_at IS NOT NULL","uid",(long)uid)==1);
            PillUse(p,p.Inventory.Objects.Single());Require(p.Inventory.Count==0);string exhausted=PillSnapshot(p);PillTrain(p,0);PillTrain(p,1);Require(exhausted==PillSnapshot(p));
            Require(EntityTable.SaveEntity(p));Revora.Database.JiangHu.SaveJiangHu(p);Sql("UPDATE revora_pill_edges_probe SET snapshot=@snapshot","snapshot",PillSnapshot(p));
        }
        Require(!PirateFenced(p) && p.Entity.ConquerPoints==0 && p.Inventory.Count==0 && PillSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_pill_edges_probe")));
        Require((long)Sql("SELECT count(*) FROM revora_jiang_pill_uses WHERE uid=@uid","uid",(long)uid)==2 && (long)Sql("SELECT count(*) FROM revora_jiang_pill_uses WHERE uid=@uid AND consumed_at IS NULL","uid",(long)uid)==1);
        var service=Feature("WayOfHeroesClaims");foreach(int threshold in new[]{1300,1400,1450,1580})foreach(int delta in new[]{-1,0,1}){
            var blob=new byte[53];blob[0]=4;int remaining=threshold+delta;
            for(int power=0;power<4;power++){int start=1+13*power;blob[start]=(byte)(power+1);for(int star=0;star<4;star++){
                int points=Math.Min(100,remaining);remaining-=points;blob[start+1+3*star]=1;Revora.Network.Writer.WriteUInt16((ushort)(2*points),start+2+3*star,blob);
            }}Require((int)Call(service,"ChiScore",blob)==threshold+delta);
        }
        System.Console.WriteLine("[career-process] career-pill-edges"+(reload?"-reload":"")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Pill edges did not close.");
    }
}
