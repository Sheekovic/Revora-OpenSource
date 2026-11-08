using System;
using System.Collections.Generic;
using Revora.Client;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static partial class HeartCampaign
    {
        internal static Action<GameState> BeforePrizeInstall;
        internal static bool Claim(GameState p,string weekend)
        {
            if(!Ready(p) || string.IsNullOrEmpty(weekend))return false;
            lock(p.ItemSyncRoot)try{
                if(!Ready(p))return false;
                uint beforeCp=p.Entity.ConquerPoints,beforeRvp=p.Entity.RevoraPoint,cp,rvp;
                using(var c=Database.DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    long? savedCp,savedRvp;
                    using(var q=Q.Command(c,t,"SELECT conquerpoints,revorapoint FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID))
                    using(var r=q.ExecuteReader()){
                        if(!r.Read())throw new InvalidOperationException("Heart prize owner missing.");
                        savedCp=r.IsDBNull(0)?(long?)null:Convert.ToInt64(r.GetValue(0));
                        savedRvp=r.IsDBNull(1)?(long?)null:Convert.ToInt64(r.GetValue(1));
                    }
                    PurseBalance.AssertMatches(p,savedCp,savedRvp);
                    int paidCp,paidRvp;DateTime earned,expires,end;
                    using(var q=Q.Command(c,t,@"SELECT r.cp,r.rvp,r.earned_at,r.expires_at,w.ends_at
FROM revora_heart_results r JOIN revora_heart_weekends w USING(weekend)
WHERE r.weekend=@weekend AND r.uid=@uid AND NOT r.award_recorded AND w.closed AND w.ends_at<=@now FOR UPDATE OF r",
                        "weekend",weekend,"uid",(long)p.Entity.UID,"now",Clock()))
                    using(var r=q.ExecuteReader()){
                        if(!r.Read())return false;
                        paidCp=r.GetInt32(0);paidRvp=r.GetInt32(1);earned=r.GetFieldValue<DateTimeOffset>(2).UtcDateTime;
                        expires=r.GetFieldValue<DateTimeOffset>(3).UtcDateTime;end=r.GetFieldValue<DateTimeOffset>(4).UtcDateTime;
                    }
                    if(earned!=end || expires!=end.AddDays(30) || !((paidCp==0&&paidRvp==0)||(paidCp==5000&&paidRvp==1000)))
                        throw new InvalidOperationException("Heart prize contract changed.");
                    if((ulong)beforeCp+(uint)paidCp>uint.MaxValue || (ulong)beforeRvp+(uint)paidRvp>uint.MaxValue)return false;
                    cp=beforeCp+(uint)paidCp;rvp=beforeRvp+(uint)paidRvp;
                    if(!StorageManager.Known(6018,6018) || !ProgressionEvidence.Grant(c,t,p.Entity.UID,"heart-revora-v1",weekend,60186018,earned))
                        throw new InvalidOperationException("Heart wing receipt was not created.");
                    if(Q.Execute(c,t,"UPDATE entities SET conquerpoints=@cp,revorapoint=@rvp WHERE uid=@uid","cp",(long)cp,"rvp",(long)rvp,"uid",(long)p.Entity.UID)!=1)
                        throw new InvalidOperationException("Heart prize balances not saved.");
                    if(Q.Execute(c,t,"UPDATE revora_heart_results SET award_recorded=true WHERE weekend=@weekend AND uid=@uid AND NOT award_recorded",
                        "weekend",weekend,"uid",(long)p.Entity.UID)!=1)throw new InvalidOperationException("Heart prize acknowledgement not saved.");
                    if(!Ready(p)||p.Entity.ConquerPoints!=beforeCp||p.Entity.RevoraPoint!=beforeRvp)throw new InvalidOperationException("Heart prize session changed.");
                    t.Commit();
                }}
                var hook=BeforePrizeInstall;if(hook!=null)hook(p);
                p.Entity.ApplyCommittedConquerPoints(cp);p.Entity.ProjectRevoraPoints(rvp);PurseBalance.Committed(p,cp,rvp);
                // A player who opened the wardrobe before claiming may still have
                // a cached award list. Refresh the newly committed wing immediately.
                if(p.Entity.MyAchievement!=null)p.Entity.MyAchievement.Invalidate();
                TitleRequirements.Reconcile(p,false);
                p.Send("Your weekend campaign prizes have arrived. Check your wardrobe.");
                return true;
            }catch(Exception ex){Q.Fail(p,"heart-prize",ex);return false;}
        }
        internal static void RecoverPrizes(GameState p)
        {
            if(!resultsReady || !Ready(p))return;
            try{
                var weekends=new List<string>();
                using(var c=Database.DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,@"SELECT r.weekend FROM revora_heart_results r
JOIN revora_heart_weekends w USING(weekend) WHERE r.uid=@uid AND NOT r.award_recorded AND w.closed AND w.ends_at<=@now ORDER BY w.ends_at LIMIT 12",
                    "uid",(long)p.Entity.UID,"now",Clock()))using(var r=q.ExecuteReader())while(r.Read())weekends.Add(r.GetString(0));}
                foreach(string weekend in weekends){if(!Ready(p))break;Claim(p,weekend);}
            }catch(Exception ex){Console.WriteLine("[heart] prize_recovery_failure="+ex.GetType().Name);}
        }
    }
}
