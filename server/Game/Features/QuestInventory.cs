using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Npgsql;
using NpgsqlTypes;
using Revora.Client;
using Revora.Database;
using Revora.Interfaces;
using Revora.Network.GamePackets;

namespace Revora.Game.Features
{
    // Shared transaction plumbing; callers own eligibility, progression and reward rules.
    internal static class QuestInventory
    {
        internal static bool Player(GameState p,bool alive=true) {
            GameState current;
            return p!=null && !p.Fake && p.Entity!=null && p.Action==2 && p.Entity.FullyLoaded
                && (!alive || !p.Entity.Dead) && !p.Entity.PersistenceRequiresReload && p.ItemSyncRoot!=null && p.Inventory!=null
                && Kernel.GamePool.TryGetValue(p.Entity.UID,out current) && ReferenceEquals(current,p);
        }
        internal static bool Near(GameState p,uint npc,ushort map) {
            INpc actor;
            return Player(p) && p.ActiveNpc==npc && p.Entity.MapID==map && p.Booth==null && p.Trade!=null && !p.Trade.InTrade
                && p.Map.Npcs.TryGetValue(npc,out actor) && Kernel.GetDistance(p.Entity.X,p.Entity.Y,actor.X,actor.Y)<=6;
        }
        internal static NpgsqlCommand Command(NpgsqlConnection c,NpgsqlTransaction t,string sql,params object[] values) {
            var q=new NpgsqlCommand(sql,c,t);
            for(int i=0;i<values.Length;i+=2) {
                if(values[i+1] is DateTime) {
                    var utc=(DateTime)values[i+1];if(utc.Kind!=DateTimeKind.Utc)throw new ArgumentException("Quest timestamps require UTC.");
                    q.Parameters.Add((string)values[i],NpgsqlDbType.TimestampTz).Value=utc;
                } else q.Parameters.AddWithValue((string)values[i],values[i+1]);
            }
            return q;
        }
        internal static int Execute(NpgsqlConnection c,NpgsqlTransaction t,string sql,params object[] values) {
            using(var q=Command(c,t,sql,values))return q.ExecuteNonQuery();
        }
        internal static ConquerItem New(uint id,bool bound=true) {
            var info=ConquerItemInformation.BaseInformations[id];
            return new ConquerItem(true){ID=id,UID=ConquerItem.ItemUID.Next,Bound=bound,StackSize=1,Durability=info.Durability,MaximDurability=info.Durability};
        }
        internal static void Insert(NpgsqlConnection c,NpgsqlTransaction t,uint uid,ConquerItem item) {
            if(t==null)throw new ArgumentException("Quest item insert requires a transaction.");
            if(Execute(c,t,@"INSERT INTO items(uid,id,entityid,position,warehouse,stacksize,bound,durability,maximdurability)
VALUES(@id,@type,@uid,0,0,1,@bound,@dur,@max)","id",(long)item.UID,"type",(int)item.ID,"uid",(long)uid,
                "bound",item.Bound?1:0,"dur",(int)item.Durability,"max",(int)item.MaximDurability)!=1)
                throw new InvalidOperationException("Quest reward item was not saved.");
        }
        internal static bool Owns(NpgsqlConnection c,NpgsqlTransaction t,GameState p,uint itemUid,uint type,bool bound=true) {
            ConquerItem item;
            if(!p.Inventory.TryGetItem(itemUid,out item) || item.ID!=type || item.Position!=0 || item.Bound!=bound)return false;
            using(var q=Command(c,t,@"SELECT EXISTS(SELECT 1 FROM items WHERE uid=@item AND id=@type AND entityid=@uid
AND position=0 AND warehouse=0 AND bound=@bound AND stacksize=1)","item",(long)itemUid,"type",(int)type,"uid",(long)p.Entity.UID,"bound",bound?1:0))return Equals(q.ExecuteScalar(),true);
        }
        internal static void Consume(NpgsqlConnection c,NpgsqlTransaction t,GameState p,uint itemUid,uint type,bool bound=true) {
            if(!Owns(c,t,p,itemUid,type,bound) || Execute(c,t,@"DELETE FROM items WHERE uid=@item AND id=@type AND entityid=@uid
AND position=0 AND warehouse=0 AND bound=@bound AND stacksize=1","item",(long)itemUid,"type",(int)type,"uid",(long)p.Entity.UID,"bound",bound?1:0)!=1)
                throw new InvalidOperationException("Quest material ownership changed.");
        }
        internal static void Install(GameState p,IEnumerable<uint> removed,IEnumerable<ConquerItem> added) {
            if(!Monitor.IsEntered(p.ItemSyncRoot))throw new InvalidOperationException("Quest inventory requires its owner lock.");
            foreach(uint id in removed)if(!p.Inventory.Remove(id,Enums.ItemUse.None,true))throw new InvalidOperationException("Committed quest removal requires reload.");
            foreach(var item in added) {if(!p.Inventory.Add(item,Enums.ItemUse.None))throw new InvalidOperationException("Committed quest reward requires reload.");item.Send(p);}
        }
        internal static void Refresh(GameState p) {
            if(p.Entity.MyAchievement!=null)p.Entity.MyAchievement.Invalidate();TitleRequirements.Reconcile(p,false);
        }
        internal static void Fail(GameState p,string source,Exception ex) {
            Console.WriteLine("[quest] source="+source+" uid="+p.Entity.UID+" error="+ex.GetType().Name
                +" inner="+(ex.InnerException==null?"none":ex.InnerException.GetType().Name));
            p.Entity.PersistenceRequiresReload=true;p.Action=0;p.Send("Please reconnect before continuing your quest.");
        }
    }
}
