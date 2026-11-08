using System;
using System.Collections.Generic;
using System.Linq;
using Npgsql;
using Revora.Client;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class SummerMaterials
    {
        internal sealed class Change {internal ConquerItem Item;internal ushort Before,After;internal bool Created;}
        internal static List<Change> Take(GameState p,uint type,int count) {
            if(!SummerCrafting.Material(type) || count<1 || count>10000)throw new ArgumentException("Invalid summer material debit.");
            var changes=new List<Change>();
            foreach(var item in p.Inventory.Objects.Where(i=>i.ID==type && i.Position==0 && i.Bound && i.Lock==0 && i.StackSize>0).OrderBy(i=>i.UID)) {
                int take=Math.Min(count,item.StackSize);changes.Add(new Change{Item=item,Before=item.StackSize,After=(ushort)(item.StackSize-take)});
                count-=take;if(count==0)return changes;
            }
            return null;
        }
        internal static List<Change> Give(GameState p,params int[] counts) {
            if(counts.Length!=4 || counts.Any(n=>n<0 || n>10000))throw new ArgumentException("Invalid summer material credit.");
            var changes=new List<Change>();
            for(int i=0;i<4;i++) {
                if(counts[i]==0)continue;uint type=(uint)(3303704+i);
                var item=p.Inventory.Objects.Where(a=>a.ID==type && a.Position==0 && a.Bound && a.Lock==0 && a.StackSize>0 && a.StackSize+counts[i]<=10000).OrderBy(a=>a.UID).FirstOrDefault();
                if(item==null){item=Q.New(type);item.StackSize=(ushort)counts[i];changes.Add(new Change{Item=item,Created=true,After=item.StackSize});}
                else changes.Add(new Change{Item=item,Before=item.StackSize,After=(ushort)(item.StackSize+counts[i])});
            }
            return p.Inventory.Count+changes.Count(a=>a.Created)>40?null:changes;
        }
        internal static void Write(NpgsqlConnection c,NpgsqlTransaction t,GameState p,IEnumerable<Change> changes) {
            if(t==null)throw new ArgumentException("Summer materials require a transaction.");
            foreach(var a in changes.OrderBy(a=>a.Item.UID)) {
                var item=a.Item;
                if(a.Created) {
                    Q.Insert(c,t,p.Entity.UID,item);
                    if(a.After!=1)Q.Execute(c,t,"UPDATE items SET stacksize=@count WHERE uid=@item","count",(int)a.After,"item",(long)item.UID);
                } else {
                    ConquerItem owned;
                    if(!p.Inventory.TryGetItem(item.UID,out owned) || !ReferenceEquals(owned,item) || item.StackSize!=a.Before || !item.Bound || item.Lock!=0)
                        throw new InvalidOperationException("Summer material cache ownership changed.");
                    string sql=a.After==0?
                        "DELETE FROM items WHERE uid=@item AND entityid=@uid AND id=@type AND position=0 AND warehouse=0 AND stacksize=@before AND bound=1":
                        "UPDATE items SET stacksize=@after WHERE uid=@item AND entityid=@uid AND id=@type AND position=0 AND warehouse=0 AND stacksize=@before AND bound=1";
                    if(Q.Execute(c,t,sql,"after",(int)a.After,"item",(long)item.UID,"uid",(long)p.Entity.UID,"type",(int)item.ID,"before",(int)a.Before)!=1)
                        throw new InvalidOperationException("Summer material persisted ownership changed.");
                }
            }
        }
        internal static void Install(GameState p,IEnumerable<Change> changes) {
            var all=changes.ToArray();Q.Install(p,all.Where(a=>!a.Created && a.After==0).Select(a=>a.Item.UID),all.Where(a=>a.Created).Select(a=>a.Item));
            foreach(var a in all.Where(a=>!a.Created && a.After>0)){a.Item.StackSize=a.After;a.Item.Mode=Enums.ItemMode.Update;a.Item.Send(p);a.Item.Mode=Enums.ItemMode.Default;}
        }
    }
}
