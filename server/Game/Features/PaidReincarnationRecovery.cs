using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Game.Features.Reincarnation;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class PaidReincarnationRecovery
    {
        private sealed class Loaded { internal Entity Entity; internal uint Uid; internal ReincarnateInfo Info; }
        private static readonly ConditionalWeakTable<GameState,Loaded> loaded = new ConditionalWeakTable<GameState,Loaded>();
        internal static void Invalidate(GameState p) { loaded.Remove(p); }
        internal static ReincarnateInfo Snapshot(GameState p) {
            if(!Monitor.IsEntered(p.ItemSyncRoot ?? p))throw new InvalidOperationException("Reincarnation recovery requires its owner lock.");
            if(p.Fake || p.Entity.Reborn!=2)return null;
            Loaded state;
            if(!loaded.TryGetValue(p,out state) || !ReferenceEquals(state.Entity,p.Entity) || state.Uid!=p.Entity.UID) {
                state=new Loaded{Entity=p.Entity,Uid=p.Entity.UID};
                try {
                    using(var c=DataHolder.MySqlConnection){c.Open();
                        using(var q=Q.Command(c,null,@"SELECT id,old_class,old_level,old_experience,new_class,first_class,first_level,recovered_at
FROM revora_reincarnations WHERE uid=@uid ORDER BY sequence DESC LIMIT 1","uid",(long)p.Entity.UID))
                        using(var r=q.ExecuteReader())if(r.Read()) {
                            byte level=checked((byte)Convert.ToInt32(r[2])),next=checked((byte)Convert.ToInt32(r[4]));
                            if(!ReincarnationRules.Eligible(checked((byte)Convert.ToInt32(r[1])),level,2) || Array.IndexOf(RebirthRules.Classes,next)<0
                                || r.IsDBNull(7) && (Convert.ToInt32(r[1])!=p.Entity.SecondRebornClass || level!=p.Entity.SecondRebornLevel
                                    || next/10!=p.Entity.Class/10 || Convert.ToInt32(r[5])!=p.Entity.FirstRebornClass || Convert.ToInt32(r[6])!=p.Entity.FirstRebornLevel))
                                throw new InvalidOperationException("Reincarnation receipt differs from its character.");
                            state.Info=new ReincarnateInfo{UID=p.Entity.UID,Level=level,Experience=checked((ulong)Convert.ToInt64(r[3])),
                                PaidReincarnation=true,PaidReceipt=r.GetGuid(0),RebirthClass=next,Recovered=!r.IsDBNull(7)};
                            ReincarnationRecovery.Validate(state.Info);
                        }
                        if(state.Info!=null && !state.Info.Recovered) {
                            using(var q=Q.Command(c,null,"SELECT level,experience FROM reincarnation WHERE uid=@uid","uid",(long)p.Entity.UID))
                            using(var r=q.ExecuteReader()) {
                                if(!r.Read() || Convert.ToInt32(r[0])!=state.Info.Level || Convert.ToInt64(r[1])!=checked((long)state.Info.Experience) || r.Read())
                                    throw new InvalidOperationException("Reincarnation recovery snapshot differs from its receipt.");
                            }
                        }
                    }
                    loaded.Remove(p);loaded.Add(p,state);
                }catch(Exception ex){Q.Fail(p,"reincarnation-load",ex);throw;}
            }
            var i=state.Info;
            return i==null?null:new ReincarnateInfo{UID=i.UID,Level=i.Level,Experience=i.Experience,PaidReincarnation=true,
                PaidReceipt=i.PaidReceipt,RebirthClass=i.RebirthClass,Recovered=i.Recovered};
        }
        internal static void Consume(NpgsqlConnection c,NpgsqlTransaction t,GameState p,ReincarnateInfo info) {
            if(!info.PaidReincarnation || info.Recovered || info.Level!=p.Entity.SecondRebornLevel || info.RebirthClass/10!=p.Entity.Class/10
                || Q.Execute(c,t,@"UPDATE revora_reincarnations SET recovered_at=CURRENT_TIMESTAMP
WHERE id=@id AND uid=@uid AND old_class=@old AND old_level=@level AND old_experience=@exp AND new_class=@next
AND first_class=@first AND first_level=@firstlevel AND recovered_at IS NULL",
                    "id",info.PaidReceipt,"uid",(long)p.Entity.UID,"old",(int)p.Entity.SecondRebornClass,"level",(int)info.Level,
                    "exp",checked((long)info.Experience),"next",(int)info.RebirthClass,"first",(int)p.Entity.FirstRebornClass,"firstlevel",(int)p.Entity.FirstRebornLevel)!=1)
                throw new InvalidOperationException("Reincarnation receipt changed.");
            if(Q.Execute(c,t,"DELETE FROM reincarnation WHERE uid=@uid AND level=@level AND experience=@exp",
                "uid",(long)p.Entity.UID,"level",(int)info.Level,"exp",checked((long)info.Experience))!=1)
                throw new InvalidOperationException("Reincarnation snapshot changed.");
        }
        internal static void Committed(GameState p,ReincarnateInfo info) {
            Loaded state;
            if(!loaded.TryGetValue(p,out state) || state.Info==null || state.Info.PaidReceipt!=info.PaidReceipt)
                throw new InvalidOperationException("Committed reincarnation recovery requires reload.");
            state.Info.Recovered=true;
            ReincarnateInfo removed;Kernel.ReincarnatedCharacters.TryRemove(p.Entity.UID,out removed);
        }
    }
}
