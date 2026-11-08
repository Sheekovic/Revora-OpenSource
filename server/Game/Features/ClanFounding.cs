using System;
using System.Linq;
using Revora.Client;
using Revora.Database;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features {
    internal static class ClanFounding {
        private static readonly object gate=new object();
        private static bool Ready(GameState p){return Q.Near(p,9450,1002) && p.Entity.ClanId==0 && p.Entity.Level>=Config.ClanCreateMinLevel
            && p.Map.BaseID==1002 && p.Entity.PokerTable==0 && CharacterProgression.Managed(p);}
        internal static void ReserveIds(Npgsql.NpgsqlConnection c,Npgsql.NpgsqlTransaction t,long minimum){
            Q.Execute(c,t,"SELECT pg_advisory_xact_lock(6711,10304)");
            Q.Execute(c,t,"CREATE TABLE IF NOT EXISTS revora_clan_ids(id integer PRIMARY KEY CHECK(id=1),last_id bigint NOT NULL CHECK(last_id BETWEEN 599 AND 4294967295))");
            Q.Execute(c,t,"INSERT INTO revora_clan_ids(id,last_id) VALUES(1,@minimum) ON CONFLICT(id) DO UPDATE SET last_id=GREATEST(revora_clan_ids.last_id,EXCLUDED.last_id)","minimum",Math.Max(599,minimum));
        }
        internal static bool Create(GameState p,string name){
            if(p==null || p.ItemSyncRoot==null || name==null)return false;name=name.Trim();
            if(name.Length<2 || name.Length>16 || name.Any(char.IsControl) || System.Text.Encoding.Default.GetByteCount(name)>16 || System.Text.Encoding.Default.GetString(System.Text.Encoding.Default.GetBytes(name))!=name)return false;
            lock(p.ItemSyncRoot)lock(gate)try{
                if(!Ready(p) || p.Entity.Money<Config.ClanCreateGold)return false;
                uint cost=Config.ClanCreateGold,fund=Config.ClanCreateFund;ulong next=p.Entity.Money-cost;
                var profile=CharacterProgression.Capture(p);uint id;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    SkillTable.LockOwner(c,t,p);CharacterProgression.Persist(c,t,p,profile);
                    long floor;
                    using(var q=Q.Command(c,t,"SELECT COALESCE(MAX(identifier),599) FROM clans"))floor=Convert.ToInt64(q.ExecuteScalar());
                    ReserveIds(c,t,Math.Max(floor,Kernel.Clans.Keys.DefaultIfEmpty(599u).Max()));
                    using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM clans WHERE lower(name)=lower(@name))","name",name))
                        if(Equals(q.ExecuteScalar(),true))return false;
                    using(var q=Q.Command(c,t,"UPDATE revora_clan_ids SET last_id=last_id+1 WHERE id=1 RETURNING last_id"))id=checked(Convert.ToUInt32(q.ExecuteScalar()));
                    if(Q.Execute(c,t,@"INSERT INTO clans(identifier,leaderid,name,leadername,fund,level,bptower,announcement,claimed)
VALUES(@id,@uid,@name,@leader,@fund,1,0,'No Notice',0)","id",(long)id,"uid",(long)p.Entity.UID,"name",name,"leader",p.Entity.Name,"fund",(long)fund)!=1)
                        throw new InvalidOperationException("Clan was not saved.");
                    if(Q.Execute(c,t,"UPDATE entities SET clanid=@clan,clanrank=100,clandonation=0 WHERE uid=@uid AND COALESCE(clanid,0)=0",
                        "clan",(long)id,"uid",(long)p.Entity.UID)!=1)throw new InvalidOperationException("Founder membership changed.");
                    SilverBalance.Persist(c,t,p,next);
                    ProgressionEvidence.Complete(c,t,p.Entity.UID,10304,"clan-founder-v1",id.ToString(System.Globalization.CultureInfo.InvariantCulture),1);
                    if(!Ready(p) || p.Entity.Level!=profile.Values[0] || p.Entity.Money!=next+cost)throw new InvalidOperationException("Founder context changed.");
                    t.Commit();
                }}
                CharacterProgression.Committed(p,profile);SilverBalance.ApplyCommitted(p,next);
                var clan=new Clan(p.Entity.UID,id,name,p.Entity.Name){LeaderName=p.Entity.Name,Fund=fund,Level=1,BPTower=0,Announcement="No Notice"};
                clan.Members.TryAdd(p.Entity.UID,new ClanMember{UID=p.Entity.UID,Identifier=p.Entity.UID,Name=p.Entity.Name,Level=p.Entity.Level,Class=p.Entity.Class,Rank=Clan.Ranks.ClanLeader});
                Kernel.Clans.Add(id,clan);Clan registered;
                if(!Kernel.Clans.TryGetValue(id,out registered) || !ReferenceEquals(registered,clan))throw new InvalidOperationException("Clan registry requires reload.");
                ClanMembership.Invalidate(p);p.Entity.ClanId=id;p.Entity.ClanRank=Clan.Ranks.ClanLeader;p.Entity.ClanName=name;
                clan.Build(p,Clan.Types.Info);p.Send(clan);p.SendScreenSpawn(p.Entity,true);Q.Refresh(p);
                Kernel.SendWorldMessage(new Revora.Network.GamePackets.Message(p.Entity.Name+" has set up a new clan, "+name+".",System.Drawing.Color.Yellow,Revora.Network.GamePackets.Message.World),Server.GamePool);return true;
            }catch(Exception ex){Q.Fail(p,"clan-founder",ex);return false;}
        }
    }
}
