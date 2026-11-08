using System;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features {
    internal static class SteedRaceClaims {
        internal static Action AfterCommit;
        internal static void Initialize() {
            SteedRaceResults.Initialize();
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_steed_claims(
id uuid PRIMARY KEY,uid bigint NOT NULL,rank integer NOT NULL CHECK(rank BETWEEN 1 AND 3),
cp bigint NOT NULL CHECK(cp>=0),rvp bigint NOT NULL CHECK(rvp>=0),gold bigint NOT NULL CHECK(gold>=0),
claimed_at timestamptz NOT NULL);
ALTER TABLE revora_steed_claims ADD COLUMN IF NOT EXISTS run uuid;
ALTER TABLE revora_steed_claims ADD COLUMN IF NOT EXISTS item_uid bigint NOT NULL DEFAULT 0;");}
        }
        private static bool Ready(GameState p) {return Q.Player(p) && p.Variables!=null && p.Entity.PokerTable==0
            && Q.Near(p,SteedRaceSystem.NpcId,SteedRaceSystem.TwinCityMap);}
        internal static string Claim(GameState p) {
            if(!Ready(p) || !SessionInstall.TryEnter(p.Entity.UID))return "Speak to me here when you are ready to claim.";
            try { return ClaimOwned(p); } finally { SessionInstall.Exit(p.Entity.UID); }
        }
        private static string ClaimOwned(GameState p) {
            if(!Ready(p))return "Speak to me here when you are ready to claim.";
            lock(p.ItemSyncRoot)lock(p.Variables.SyncRoot)try {
                if(!Ready(p))return "Speak to me here when you are ready to claim.";
                Guid run=SteedRaceResults.ClaimRun(p.Variables);
                ulong rank=p.Variables.ReadULong(SteedRaceSystem.ClaimKey);
                if(rank<1 || rank>3)return "You have no Steed Race prize to claim.";
                uint cp=rank==1?Config.SteedRaceFirstCp:rank==2?Config.SteedRaceSecondCp:Config.SteedRaceThirdCp;
                uint rvp=rank==1?Config.SteedRaceFirstRevoraPoints:rank==2?Config.SteedRaceSecondRevoraPoints:Config.SteedRaceThirdRevoraPoints;
                ulong gold=rank==1?Config.SteedRaceFirstGold:rank==2?Config.SteedRaceSecondGold:Config.SteedRaceThirdGold;
                uint beforeCp=p.Entity.ConquerPoints,beforeRvp=p.Entity.RevoraPoint;ulong beforeGold=p.Entity.Money;
                uint nextCp=0,nextRvp=0;ulong nextGold=0;byte[] merged;ConquerItem soul=null;uint soulType=0;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    SkillTable.LockOwner(c,t,p);
                    if(run!=Guid.Empty) {
                        using(var q=Q.Command(c,t,"SELECT cp,rvp,gold,soul_type FROM revora_steed_results WHERE run=@run AND uid=@uid AND rank=@rank AND finished_at IS NOT NULL AND claimed_at IS NULL FOR UPDATE","run",run,"uid",(long)p.Entity.UID,"rank",(int)rank))
                        using(var r=q.ExecuteReader()){if(!r.Read())throw new InvalidOperationException("Steed result is missing or already paid.");cp=checked(Convert.ToUInt32(r[0]));rvp=checked(Convert.ToUInt32(r[1]));gold=checked(Convert.ToUInt64(r[2]));soulType=checked(Convert.ToUInt32(r[3]));}
                    }
                    if(soulType!=0) {
                        if(p.Inventory.Count>=40)return "Make one space in your bag. Your prize will wait.";
                        using(var q=Q.Command(c,t,"SELECT count(*) FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0","uid",(long)p.Entity.UID))
                            if(Convert.ToInt32(q.ExecuteScalar())>=40)return "Make one space in your bag. Your prize will wait.";
                        if(soulType<800000 || soulType>=900000 || !ConquerItemInformation.BaseInformations.ContainsKey(soulType)
                            || ConquerItemInformation.BaseInformations[soulType].PurificationLevel!=7)
                            throw new InvalidOperationException("Steed soul catalog mismatch.");
                        soul=Q.New(soulType,true);Q.Insert(c,t,p.Entity.UID,soul);
                    }
                    if((ulong)beforeCp+cp>uint.MaxValue || (ulong)beforeRvp+rvp>uint.MaxValue || beforeGold>Entity.MaximumSilver || gold>Entity.MaximumSilver-beforeGold)
                        return "Make room in your purse first. Your prize will wait.";
                    nextCp=beforeCp+cp;nextRvp=beforeRvp+rvp;nextGold=beforeGold+gold;
                    byte[] before;using(var q=Q.Command(c,t,"SELECT data FROM variablevault WHERE entityid=@uid FOR UPDATE","uid",(long)p.Entity.UID))before=q.ExecuteScalar() as byte[];
                    if(before==null || before.Length<4)throw new InvalidOperationException("Steed claim is not saved.");
                    var saved=new VariableVault();saved.Deserialize(before);
                    if(saved.ReadULong(SteedRaceSystem.ClaimKey)!=rank || SteedRaceResults.ClaimRun(saved)!=run || !p.Variables.TryMerge(before,new[]{SteedRaceSystem.ClaimKey,SteedRaceResults.RunLowKey,SteedRaceResults.RunHighKey},new DynamicVariable[]{(DynamicVariable)0ul,(DynamicVariable)0ul,(DynamicVariable)0ul},out merged))
                        throw new InvalidOperationException("Steed claim changed.");
                    long? savedCp,savedRvp;PurseBalance.CopySaved(p,out savedCp,out savedRvp);
                    if(Q.Execute(c,t,@"UPDATE entities SET conquerpoints=@cp,revorapoint=@rvp WHERE uid=@uid
AND (CASE WHEN @cpnull THEN conquerpoints IS NULL ELSE conquerpoints=@oldcp END)
AND (CASE WHEN @rvpnull THEN revorapoint IS NULL ELSE revorapoint=@oldrvp END)","cp",(long)nextCp,"rvp",(long)nextRvp,"uid",(long)p.Entity.UID,
                        "cpnull",!savedCp.HasValue,"oldcp",savedCp.GetValueOrDefault(),"rvpnull",!savedRvp.HasValue,"oldrvp",savedRvp.GetValueOrDefault())!=1)
                        throw new InvalidOperationException("Steed purse changed.");
                    SilverBalance.Persist(c,t,p,nextGold);
                    if(Q.Execute(c,t,"UPDATE variablevault SET data=@data WHERE entityid=@uid AND data=@before","data",merged,"uid",(long)p.Entity.UID,"before",before)!=1)
                        throw new InvalidOperationException("Steed claim was not consumed.");
                    if(run!=Guid.Empty && Q.Execute(c,t,"UPDATE revora_steed_results SET claimed_at=@now WHERE run=@run AND uid=@uid AND claimed_at IS NULL","now",DateTime.UtcNow,"run",run,"uid",(long)p.Entity.UID)!=1)
                        throw new InvalidOperationException("Steed result payment was not saved.");
                    if(Q.Execute(c,t,"INSERT INTO revora_steed_claims(id,uid,rank,cp,rvp,gold,claimed_at,run,item_uid) VALUES(@id,@uid,@rank,@cp,@rvp,@gold,@now,@run,@item)",
                        "item",soul==null?0L:(long)soul.UID,"run",run==Guid.Empty?(object)DBNull.Value:run,"id",Guid.NewGuid(),"uid",(long)p.Entity.UID,"rank",(int)rank,"cp",(long)cp,"rvp",(long)rvp,"gold",(long)gold,"now",DateTime.UtcNow)!=1)
                        throw new InvalidOperationException("Steed receipt was not saved.");
                    if(!Ready(p) || SteedRaceResults.ClaimRun(p.Variables)!=run || p.Variables.ReadULong(SteedRaceSystem.ClaimKey)!=rank || p.Entity.ConquerPoints!=beforeCp || p.Entity.RevoraPoint!=beforeRvp || p.Entity.Money!=beforeGold)
                        throw new InvalidOperationException("Steed claimant changed.");
                    t.Commit();
                }}
                if(AfterCommit!=null)AfterCommit();
                p.Variables.ReplaceContents(merged);p.Entity.ApplyCommittedConquerPoints(nextCp);p.Entity.ApplyCommittedRevoraPoints(nextRvp);SilverBalance.ApplyCommitted(p,nextGold);
                if(soul!=null)Q.Install(p,new uint[0],new[]{soul});
                return "Claimed "+(rank==1?"first":rank==2?"second":"third")+": "+cp+" CPs, "+gold+" gold, and "+rvp+" Revora Points."+(soul==null?"":" Your bound P7 soul is in your bag.");
            }catch(Exception ex){Q.Fail(p,"steed-race-claim",ex);return "Your prize could not be confirmed. Reconnect to check your purse.";}
        }
    }
}
