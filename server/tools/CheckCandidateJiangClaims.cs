using System;
using System.Linq;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;

internal static partial class CheckCandidateArenaLifecycle
{
    static bool jiangClaimFixture;
    sealed class JiangClaimMaximum:Random{
        public override int Next(int low,int high){return high>low?high-1:low;}
        public override int Next(int high){return 0;}
    }
    static string JiangClaimSnapshot(GameState p){return Convert.ToString(Sql(@"SELECT json_build_object(
'base',@base::json,'study',(SELECT studypoints FROM entities WHERE uid=@uid),
'jiang',(SELECT (string_to_array(data,'#'))[1:10]||(string_to_array(data,'#'))[12:184] FROM jianghu WHERE uid=@uid))::text",
        "base",NobilityClaimSnapshot(p),"uid",(long)p.Entity.UID));}
    static void JiangClaimProcess(bool reload){
        const uint uid=4294909501;jiangClaimFixture=true;
        bool absent=(bool)Sql("SELECT to_regclass('revora_jiang_claim_probe') IS NULL");
        if(absent){Require(!reload && (long)Sql("SELECT count(*) FROM entities WHERE uid=@uid","uid",(long)uid)==0);
            nextUid=uid;Player(Kernel.Maps[1002]);Sql("UPDATE entities SET level=140,reborn=2,conquerpoints=50000 WHERE uid=@uid","uid",(long)uid);
            Sql("CREATE TABLE revora_jiang_claim_probe(snapshot text);INSERT INTO revora_jiang_claim_probe VALUES(NULL)");
        }
        nextUid=uid;var p=Player(Kernel.Maps[1002],true);SubClassTable.Load(p.Entity);ChiTable.Load(p);WayofHeroes.Load(p);
        Call(typeof(Revora.Database.JiangHu),"LoadClient",p);
        Require(p.Account.State==AccountTable.AccountState.Player && !p.Fake && !PirateFenced(p));
        uint[] stages={703,805,906,1004},limits={10000,20000,30000,50000},prizes={3005126,3005127,3005128,3005129},chi={500,1000,2000,3000};
        object snapshot=Sql("SELECT snapshot FROM revora_jiang_claim_probe");
        if(snapshot==DBNull.Value){Require(!reload);
            if(p.Entity.MyJiang==null){Require(p.ChiPoints==0);
            foreach(uint stage in stages)NobilityClaimPacket(p,stage);Require(p.Inventory.Count==0 && !PirateFenced(p));
            var create=new byte[19];Revora.Network.Writer.WriteUInt16(11,0,create);Revora.Network.Writer.WriteUInt16(2700,2,create);
            create[4]=1;create[5]=1;create[6]=4;Revora.Network.Writer.WriteString("Path",7,create);Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,11,create);
            Revora.Network.PacketHandler.HandlePacket(create,p);Require(p.Entity.MyJiang!=null && p.Entity.SubClasses.StudyPoints==100);
            }
            int trained=p.Entity.MyJiang.Stagers.Sum(stage=>stage.Stars.Count(star=>star.Activate));
            int next=Convert.ToInt32(Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid", "uid",(long)uid));
            Require(trained<=54 && next<=4 && p.Inventory.Count==0);
            uint totalCost=0,totalChi=0;for(int i=0;i<trained;i++)totalCost+=Revora.Game.JiangHu.GetTrainingCost((ushort)Math.Min(i,49));
            for(int i=0;i<next;i++){Require((long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=@stage AND NOT legacy","uid",(long)uid,"stage",(int)stages[i])==1);totalChi+=chi[i];}
            Require(p.Entity.ConquerPoints==50000-totalCost && p.ChiPoints==totalChi && p.Entity.SubClasses.StudyPoints==100+20*trained);
            for(int roll=trained;roll<54;roll++){
                uint beforeScore=p.Entity.MyJiang.Inner_Strength;
                if(next<4){p.Entity.MyJiang.Inner_Strength=81000;NobilityClaimPacket(p,stages[next]);p.Entity.MyJiang.Inner_Strength=beforeScore;
                    Require(!PirateFenced(p) && (long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=@stage","uid",(long)uid,"stage",(int)stages[next])==0);}
                totalCost+=Revora.Game.JiangHu.GetTrainingCost((ushort)roll);p.Entity.MyJiang.Random=new JiangClaimMaximum();
                var train=new byte[28];Revora.Network.Writer.WriteUInt16(20,0,train);Revora.Network.Writer.WriteUInt16(2702,2,train);train[9]=1;train[10]=(byte)(roll%9+1);train[11]=(byte)(roll/9+1);Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,20,train);
                Revora.Network.PacketHandler.HandlePacket(train,p);
                Require(!PirateFenced(p) && p.Entity.ConquerPoints==50000-totalCost && p.Entity.MyJiang.RoundBuyPoints==Math.Min(49,roll+1) && p.Entity.SubClasses.StudyPoints==100+20*(roll+1));
                if(next<4 && p.Entity.MyJiang.Inner_Strength>=limits[next]){
                    uint stage=stages[next];System.Threading.Tasks.Parallel.For(0,8,_=>NobilityClaimPacket(p,stage));
                    Require(!PirateFenced(p) && (long)Sql("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid=@stage AND NOT legacy","uid",(long)uid,"stage",(int)stage)==1);
                    var reward=p.Inventory.Objects.Single();Require(reward.ID==prizes[next] && reward.Bound && reward.Plus==0 && reward.StackSize==1);
                    var use=new ItemUsage(true){UID=reward.UID,Action=ItemUsage.Mode.EquipItem}.ToArray();Revora.Network.Writer.WriteUInt64(Revora.Network.PacketHandler.ClientSeal,use.Length-8,use);
                    ItemUsage.Process(use,p);ItemUsage.Process(use,p);totalChi+=chi[next];Require(p.Inventory.Count==0 && p.ChiPoints==totalChi && !PirateFenced(p));next++;
                }
            }
            Require(next==4 && p.Entity.MyJiang.Inner_Strength==54000);Require(EntityTable.SaveEntity(p));Revora.Database.JiangHu.SaveJiangHu(p);
            Sql("UPDATE revora_jiang_claim_probe SET snapshot=@snapshot","snapshot",JiangClaimSnapshot(p));
        }
        Require(p.ChiPoints==6500 && p.Entity.MyJiang.Inner_Strength==54000 && p.Entity.MyJiang.RoundBuyPoints==49);
        string savedJiang=Convert.ToString(Sql("SELECT data FROM jianghu WHERE uid=@uid","uid",(long)uid));
        var service=Feature("WayOfHeroesClaims");Require((uint)Call(service,"JiangScore",savedJiang,uid)==54000);
        Action<string> invalid=data=>{bool failed=false;try{Call(service,"JiangScore",data,uid);}catch(System.Reflection.TargetInvocationException ex){failed=ex.InnerException is InvalidOperationException;}Require(failed);};
        invalid(savedJiang+"0#");invalid(savedJiang.Substring(0,savedJiang.Length-1));
        foreach(int field in new[]{0,12,13,14}){var parts=savedJiang.Split('#');parts[field]=field==0?"1":field==14?"65535":"2";invalid(string.Join("#",parts));}
        string exact=JiangClaimSnapshot(p);Require(exact==Convert.ToString(Sql("SELECT snapshot FROM revora_jiang_claim_probe")));
        foreach(uint stage in stages)NobilityClaimPacket(p,stage);Require(exact==JiangClaimSnapshot(p) && !PirateFenced(p));
        System.Console.WriteLine("[career-process] career-jiang-claims-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Jiang claim did not close.");
    }
}
