using System;
using Revora;
using Revora.Client;
using Revora.Database;
internal static partial class CheckCandidateArenaLifecycle
{
    static bool enlightBalanceFixture;
    const uint EnlightBalanceUid=4294912901;
    static GameState EnlightBalancePlayer(){nextUid=EnlightBalanceUid;var p=Player(Kernel.Maps[1002],true);p.Entity.EnlightmentStamp=Time32.Now;p.LoadItemStats();Require(!p.Fake && p.Account.State==AccountTable.AccountState.Player && !PirateFenced(p));return p;}
    static string EnlightBalanceSnapshot(GameState p){return Convert.ToString(Sql(@"SELECT json_build_object('base',@base::json,
'enlightenment',(SELECT json_build_array(enlightenpoints,enlightsreceived,enlightmentwait) FROM entities WHERE uid=@uid))::text","base",NobilityClaimSnapshot(p),"uid",(long)p.Entity.UID));}
    static void EnlightBalanceProcess(bool reload){
        enlightBalanceFixture=true;const uint uid=EnlightBalanceUid;
        // This helper fixture deliberately mutates balances without award rows.
        // Keep the interaction/timer service out of this isolated balance check.
        Feature("EnlightenmentProgress").GetField("enabled",Hidden).SetValue(null,false);
        if((bool)Sql("SELECT to_regclass('revora_enlight_balance_probe') IS NULL")){
            Require(!reload && (long)Sql("SELECT count(*) FROM entities WHERE uid=@uid","uid",(long)uid)==0);nextUid=uid;Player(Kernel.Maps[1002]);
            Sql("UPDATE entities SET level=130,reborn=2,enlightenpoints=NULL,enlightsreceived=NULL,enlightmentwait=NULL WHERE uid=@uid","uid",(long)uid);
            Sql("CREATE TABLE revora_enlight_balance_probe(snapshot text);INSERT INTO revora_enlight_balance_probe VALUES(NULL)");
        }
        var p=EnlightBalancePlayer();var balance=Feature("EnlightenmentBalance");
        if(Sql("SELECT snapshot FROM revora_enlight_balance_probe")==DBNull.Value){Require(!reload);
            // NULL preimages and legitimate unflushed reset/earning values.
            p.Entity.EnlightenPoints=300;p.Entity.ReceivedEnlightenPoints=0;p.Entity.EnlightmentTime=0;Require(EntityTable.SaveEntity(p));
            lock(p.ItemSyncRoot){using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Call(balance,"Persist",c,t,p,(ushort)200,(byte)1,(ushort)20);t.Commit();
            }}Call(balance,"ApplyCommitted",p,(ushort)200,(byte)1,(ushort)20);}
            Require(EntityTable.SaveEntity(p));string before=EnlightBalanceSnapshot(p);
            lock(p.ItemSyncRoot){using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Call(balance,"Persist",c,t,p,(ushort)100,(byte)2,(ushort)40);t.Rollback();
            }}}
            Require(before==EnlightBalanceSnapshot(p) && p.Entity.EnlightenPoints==200 && p.Entity.ReceivedEnlightenPoints==1 && p.Entity.EnlightmentTime==20);
            // Commit without runtime installation; an old full-character save must not restore spent points.
            lock(p.ItemSyncRoot){using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Call(balance,"Persist",c,t,p,(ushort)100,(byte)2,(ushort)40);t.Commit();
            }}}
            string committed=EnlightBalanceSnapshot(p);Require(!EntityTable.SaveEntity(p) && PirateFenced(p));Require(committed==EnlightBalanceSnapshot(p));
            p=EnlightBalancePlayer();Require(p.Entity.EnlightenPoints==100 && p.Entity.ReceivedEnlightenPoints==2 && p.Entity.EnlightmentTime==40);Require(EntityTable.SaveEntity(p));
            // A countdown change after save capture remains pending instead of becoming the saved preimage.
            lock(p.ItemSyncRoot)using(var command=new MySqlCommand(MySqlCommandType.UPDATE).Update("entities").Set("EnlightmentWait",999).Where("uid",uid)){
                var captured=(long?[])Call(balance,"PrepareSave",p,command);p.Entity.EnlightmentTime=39;
                Require(command.Execute()==1);Call(balance,"Committed",p,captured);
                Require(Convert.ToInt32(Sql("SELECT enlightmentwait FROM entities WHERE uid=@uid","uid",(long)uid))==40);
            }
            Require(EntityTable.SaveEntity(p));Require(Convert.ToInt32(Sql("SELECT enlightmentwait FROM entities WHERE uid=@uid","uid",(long)uid))==39);
            // Verify bootstrap refuses pending mutations without a loaded durable image.
            Call(balance,"Forget",p);p.Entity.EnlightenPoints=101;string saved=EnlightBalanceSnapshot(p);
            Require(!EntityTable.SaveEntity(p) && PirateFenced(p));Require(saved==EnlightBalanceSnapshot(p));p=EnlightBalancePlayer();
            Require(p.Entity.EnlightenPoints==100 && p.Entity.ReceivedEnlightenPoints==2 && p.Entity.EnlightmentTime==39);
            // Login now preserves the exact saved minute count.
            Require(EntityTable.SaveEntity(p));Sql("UPDATE revora_enlight_balance_probe SET snapshot=@snapshot","snapshot",EnlightBalanceSnapshot(p));
        }
        Require(p.Entity.EnlightenPoints==100 && p.Entity.ReceivedEnlightenPoints==2 && p.Entity.EnlightmentTime==Convert.ToUInt16(Sql("SELECT enlightmentwait FROM entities WHERE uid=@uid","uid",(long)uid)) && !PirateFenced(p));
        Require(EnlightBalanceSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_enlight_balance_probe")));Require(EntityTable.SaveEntity(p));
        Require(EnlightBalanceSnapshot(p)==Convert.ToString(Sql("SELECT snapshot FROM revora_enlight_balance_probe")));
        System.Console.WriteLine("[career-process] career-enlight-balance-"+(reload?"reload":"earn")+"-passed");Server.CommandsAI("@close");throw new InvalidOperationException("Enlight balance did not close.");
    }
}
