using System;
using System.Threading;
using Revora.Client;
using Revora.Database;
using Q=Revora.Game.Features.QuestInventory;
using R=Revora.Game.Features.PirateEpicRules;

namespace Revora.Game.Features
{
    internal static partial class PirateEpicStore
    {
        internal sealed class MapOffer {
            internal Guid Id;internal uint Owner,BeforeCp;
            internal string Cache,Rows;internal DateTime Until;
        }
        internal static MapOffer PrepareMap(GameState p,out string error) {
            if(p==null || !Monitor.IsEntered(p.ItemSyncRoot))throw new InvalidOperationException("Pirate purchase requires owner lock.");
            error=null;
            if(!Eligible(p)){error="These charts await second-reborn Pirates of level 70.";return null;}
            if(p.Entity.ConquerPoints<R.PaidMapCp){error="A chart costs 27 CPs.";return null;}
            if(p.Inventory.Count>=40){error="Make room for the chart first.";return null;}
            if(!ConquerItemInformation.BaseInformations.ContainsKey(R.FirstMap))throw new InvalidOperationException("Pirate chart catalog missing.");
            var o=new MapOffer{Id=Guid.NewGuid(),Owner=p.Entity.UID,BeforeCp=p.Entity.ConquerPoints,
                Cache=FirstRebirth.Cache(p),Until=Clock().AddMinutes(2)};
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                SkillTable.LockOwner(c,t,p);Bag(c,t,p);o.Rows=FirstRebirth.Rows(c,t,p,"items");t.Commit();
            }}return o;
        }
        private static bool SameMap(GameState p,MapOffer o,Func<bool> context) {
            return o!=null && context!=null && Eligible(p) && p.Entity.UID==o.Owner
                && Clock()<o.Until && context() && p.Inventory.Count<40
                && p.Entity.ConquerPoints==o.BeforeCp && o.BeforeCp>=R.PaidMapCp && o.Cache==FirstRebirth.Cache(p);
        }
        internal static void CommitMap(GameState p,MapOffer o,Func<bool> context) {
            if(p==null || !Monitor.IsEntered(p.ItemSyncRoot) || !SameMap(p,o,context))throw new InvalidOperationException("Pirate chart offer changed.");
            uint after=o.BeforeCp-R.PaidMapCp;
            var profile=CharacterProgression.Capture(p);var item=Q.New(R.FirstMap,false);
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                SkillTable.LockOwner(c,t,p);Bag(c,t,p);
                if(o.Rows!=FirstRebirth.Rows(c,t,p,"items"))throw new InvalidOperationException("Saved Pirate purchase items changed.");
                CharacterProgression.Persist(c,t,p,profile);
                if(Q.Execute(c,t,"UPDATE entities SET conquerpoints=@after WHERE uid=@uid AND conquerpoints=@before",
                    "after",(long)after,"uid",(long)o.Owner,"before",(long)o.BeforeCp)!=1)throw new InvalidOperationException("Pirate purchase balance changed.");
                Q.Insert(c,t,o.Owner,item);
                if(Q.Execute(c,t,@"INSERT INTO revora_pirate_map_purchases(id,uid,item_uid,balance_before,balance_after,created_at)
VALUES(@id,@uid,@item,@before,@after,@now)","id",o.Id,"uid",(long)o.Owner,"item",(long)item.UID,
                    "before",(long)o.BeforeCp,"after",(long)after,"now",Clock())!=1)throw new InvalidOperationException("Pirate chart receipt missing.");
                if(!SameMap(p,o,context))throw new InvalidOperationException("Pirate chart session changed before commit.");t.Commit();
            }}
            CharacterProgression.Committed(p,profile);p.Entity.ApplyCommittedConquerPoints(after);
            Q.Install(p,new uint[0],new[]{item});Q.Refresh(p);PirateEpicJournal.Refresh(p);
        }
    }
}
