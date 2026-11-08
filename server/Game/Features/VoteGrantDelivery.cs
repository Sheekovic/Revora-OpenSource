using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    // Voted rows stay pending until CP, Revora Points, silver and the delivered
    // marker commit together. Login calls ApplyFor only when the session is
    // already an ordinary current player; otherwise Pulse delivers later.
    public static class VoteGrantDelivery
    {
        internal static string Relation="identity.xtremetop_votes";
        internal static Action<GameState> BeforeInstall;
        internal static Action<string> Spoken;
        private static DateTime lastErrorUtc=DateTime.MinValue;

        public static void Pulse()
        {
            try
            {
                foreach(var pair in Kernel.GamePool)
                {
                    GameState client=pair.Value;
                    if(Ready(client))DeliverPending(client,4);
                }
            }
            catch(Exception exception){LogError("[vote] grant pulse ",exception);}
        }

        public static void ApplyFor(GameState client)
        {
            if(!Ready(client))return;
            try{DeliverPending(client,8);}
            catch(Exception exception){LogError("[vote] grant login ",exception);}
        }

        private static bool Ready(GameState client)
        {
            GameState current;
            return client!=null && !client.Fake && client.Account!=null && !string.IsNullOrEmpty(client.Account.Username)
                && client.Entity!=null && client.Entity.FullyLoaded && !client.Entity.PersistenceRequiresReload
                && client.ItemSyncRoot!=null && client.Action==2
                && Kernel.GamePool.TryGetValue(client.Entity.UID,out current) && ReferenceEquals(current,client);
        }

        private static void LogError(string prefix,Exception exception)
        {
            DateTime now=DateTime.UtcNow;
            if((now-lastErrorUtc).TotalMinutes<5)return;
            lastErrorUtc=now;
            Console.WriteLine(prefix+exception.GetType().Name);
        }

        private static string Table()
        {
            string name=Relation??"";
            for(int i=0;i<name.Length;i++)
            {
                char c=name[i];
                bool ok=(c>='A'&&c<='Z')||(c>='a'&&c<='z')||(c>='0'&&c<='9')||c=='_'||c=='.';
                if(!ok)throw new InvalidOperationException("Vote grant relation is not a bare identifier.");
            }
            if(name.Length==0||name[0]=='.'||name[name.Length-1]=='.'||name.IndexOf('.')!=name.LastIndexOf('.'))
                throw new InvalidOperationException("Vote grant relation is not a bare identifier.");
            return name;
        }

        private static void DeliverPending(GameState client,int limit)
        {
            var ids=new List<Guid>();
            using(var conn=DataHolder.MySqlConnection)
            {
                conn.Open();
                using(var cmd=conn.CreateCommand())
                {
                    cmd.CommandText="SELECT vote_id FROM "+Table()+" WHERE status='voted' AND delivered_at IS NULL AND entity_uid=@uid ORDER BY voted_at ASC NULLS LAST, vote_id ASC LIMIT @limit";
                    DataHolder.AddParameter(cmd,"@uid",(long)client.Entity.UID);
                    DataHolder.AddParameter(cmd,"@limit",limit);
                    using(var reader=cmd.ExecuteReader())while(reader.Read())ids.Add(reader.GetGuid(0));
                }
            }
            for(int i=0;i<ids.Count;i++)Deliver(ids[i],client);
        }

        private static void Deliver(Guid voteId,GameState client)
        {
            if(!Ready(client))return;
            lock(client.ItemSyncRoot)try
            {
                if(!Ready(client))return;
                uint cp;uint rvp;ulong gold;uint paidCp;uint paidRvp;ulong paidGold;
                using(var conn=DataHolder.MySqlConnection)
                {
                    conn.Open();
                    using(var t=conn.BeginTransaction())
                    {
                        long? storedCp;long? storedRvp;long storedGold;
                        using(var q=Q.Command(conn,t,"SELECT conquerpoints,revorapoint,COALESCE(money,0) FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)client.Entity.UID))
                        using(var reader=q.ExecuteReader())
                        {
                            if(!reader.Read())throw new InvalidOperationException("Vote grant owner missing.");
                            storedCp=reader.IsDBNull(0)?(long?)null:Convert.ToInt64(reader.GetValue(0));
                            storedRvp=reader.IsDBNull(1)?(long?)null:Convert.ToInt64(reader.GetValue(1));
                            storedGold=Convert.ToInt64(reader.GetValue(2));
                        }
                        if(storedCp<0||storedRvp<0||storedGold<0||storedCp>uint.MaxValue||storedRvp>uint.MaxValue||(ulong)storedGold>Entity.MaximumSilver)
                            throw new InvalidOperationException("Vote grant purse is outside its durable range.");
                        uint seenCp=client.Entity.ConquerPoints;uint seenRvp=client.Entity.RevoraPoint;
                        PurseBalance.AssertMatches(client,storedCp,storedRvp);
                        SilverBalance.AssertSaved(conn,t,client);
                        ulong beforeGold=client.Entity.Money;
                        long entityUid;string username;string status;bool delivered;long grantCp;long grantRvp;long grantGold;
                        using(var q=Q.Command(conn,t,"SELECT entity_uid,username,status,delivered_at IS NOT NULL,cp_amount,rvp_amount,gold_amount FROM "+Table()+" WHERE vote_id=@id FOR UPDATE","id",voteId))
                        using(var reader=q.ExecuteReader())
                        {
                            if(!reader.Read())return;
                            entityUid=Convert.ToInt64(reader.GetValue(0));
                            username=Convert.ToString(reader.GetValue(1));
                            status=Convert.ToString(reader.GetValue(2));
                            delivered=Convert.ToBoolean(reader.GetValue(3));
                            grantCp=Convert.ToInt64(reader.GetValue(4));
                            grantRvp=Convert.ToInt64(reader.GetValue(5));
                            grantGold=Convert.ToInt64(reader.GetValue(6));
                        }
                        if(delivered||status!="voted")return;
                        if(entityUid!=(long)client.Entity.UID||!string.Equals(username,client.Account.Username,StringComparison.OrdinalIgnoreCase))return;
                        if(grantCp<0||grantRvp<0||grantGold<0||grantCp>uint.MaxValue||grantRvp>uint.MaxValue||(ulong)grantGold>Entity.MaximumSilver)return;
                        int owners=0;long ownerUid=-1;
                        using(var q=Q.Command(conn,t,"SELECT entityid FROM accounts WHERE lower(username)=lower(@name) LIMIT 2 FOR SHARE","name",username))
                        using(var reader=q.ExecuteReader())while(reader.Read()){owners++;ownerUid=Convert.ToInt64(reader.GetValue(0));}
                        if(owners!=1||ownerUid!=entityUid)return;
                        if((ulong)grantCp>(ulong)(uint.MaxValue-seenCp)||(ulong)grantRvp>(ulong)(uint.MaxValue-seenRvp)||(ulong)grantGold>Entity.MaximumSilver-beforeGold)
                            return;
                        paidCp=(uint)grantCp;paidRvp=(uint)grantRvp;paidGold=(ulong)grantGold;
                        cp=seenCp+paidCp;rvp=seenRvp+paidRvp;gold=beforeGold+paidGold;
                        if(Q.Execute(conn,t,"UPDATE entities SET conquerpoints=@cp,revorapoint=@rvp,money=@gold WHERE uid=@uid AND (CASE WHEN @cpnull THEN conquerpoints IS NULL ELSE conquerpoints=@oldcp END) AND (CASE WHEN @rvpnull THEN revorapoint IS NULL ELSE revorapoint=@oldrvp END) AND COALESCE(money,0)=@oldgold",
                            "cp",(long)cp,"rvp",(long)rvp,"gold",checked((long)gold),"uid",(long)client.Entity.UID,"cpnull",!storedCp.HasValue,"oldcp",storedCp.GetValueOrDefault(),"rvpnull",!storedRvp.HasValue,"oldrvp",storedRvp.GetValueOrDefault(),"oldgold",storedGold)!=1)
                            throw new InvalidOperationException("Vote grant purse preimage changed.");
                        if(Q.Execute(conn,t,"UPDATE "+Table()+" SET status='delivered', delivered_at=clock_timestamp() WHERE vote_id=@id AND status='voted' AND delivered_at IS NULL AND entity_uid=@uid AND lower(username)=lower(@name) AND cp_amount=@cp AND rvp_amount=@rvp AND gold_amount=@gold",
                            "id",voteId,"uid",entityUid,"name",username,"cp",(int)grantCp,"rvp",(int)grantRvp,"gold",grantGold)!=1)
                            throw new InvalidOperationException("Vote grant claim changed.");
                        if(!Ready(client) || client.Entity.ConquerPoints!=seenCp || client.Entity.RevoraPoint!=seenRvp || client.Entity.Money!=beforeGold)
                            throw new InvalidOperationException("Vote grant session changed.");
                        t.Commit();
                    }
                }
                var hook=BeforeInstall;if(hook!=null)hook(client);
                client.Entity.ApplyCommittedConquerPoints(cp);
                client.Entity.ProjectRevoraPoints(rvp);
                PurseBalance.Committed(client,cp,rvp);
                SilverBalance.ApplyCommitted(client,gold);
                string text="Thanks for voting. You received "+paidCp.ToString(CultureInfo.InvariantCulture)+" CPs, "+paidRvp.ToString(CultureInfo.InvariantCulture)+" Revora Points, and "+paidGold.ToString(CultureInfo.InvariantCulture)+" gold.";
                client.Send(new Message(text,Color.Gold,Message.Talk));
                var spoken=Spoken;if(spoken!=null)spoken(text);
            }
            catch(Exception ex){Q.Fail(client,"vote-grant",ex);}
        }
    }
}
