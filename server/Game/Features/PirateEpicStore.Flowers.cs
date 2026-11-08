using System;
using System.Linq;
using System.Threading;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
using R=Revora.Game.Features.PirateEpicRules;

namespace Revora.Game.Features
{
    internal static partial class PirateEpicStore
    {
        internal sealed class FlowerPart {
            internal ConquerItem Item;internal uint Uid,Type;internal ushort Stack;
            internal int Count;internal bool Bound;
            internal int After {get{return Math.Max(1,(int)Stack)-Count;}}
        }
        internal sealed class FlowerOffer {
            internal Guid Id;internal uint Owner;internal string Day,Cache,Rows;
            internal DateTime Until;internal FlowerPart[] Parts;
        }
        private static string FlowerDay(){return Clock().ToString("yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture);}
        private static bool FlowerUsable(ConquerItem i) {
            ConquerItemBaseInformation info;
            return i!=null && i.Position==0 && i.Warehouse==0 && !i.InWardrobe && i.Lock==0 && !i.Suspicious
                && i.Days==0 && i.TimeLeftInMinutes==0 && ConquerItemInformation.BaseInformations.TryGetValue(i.ID,out info)
                && Math.Max(1,(int)i.StackSize)<=Math.Max(1,(int)info.StackSize);
        }
        private static FlowerPart[] Flowers(GameState p,uint type,int count=R.FlowersPerColor) {
            var selected=new System.Collections.Generic.List<FlowerPart>();
            foreach(var item in p.Inventory.Objects.Where(i=>i.ID==type && FlowerUsable(i)).OrderByDescending(i=>i.Bound).ThenBy(i=>i.UID)) {
                int take=Math.Min(count,Math.Max(1,(int)item.StackSize));
                selected.Add(new FlowerPart{Item=item,Uid=item.UID,Type=item.ID,Stack=item.StackSize,Bound=item.Bound,Count=take});
                count-=take;if(count==0)return selected.ToArray();
            }return null;
        }
        internal static FlowerOffer PrepareFlowers(GameState p,out string error) {
            if(p==null || !Monitor.IsEntered(p.ItemSyncRoot))throw new InvalidOperationException("Pirate offering requires owner lock.");
            error=null;if(!Eligible(p)){error="This voyage awaits second-reborn Pirates of level 70.";return null;}
            var red=Flowers(p,722732);var white=Flowers(p,722736);
            if(red==null || white==null){error="Bring three red and three white flowers.";return null;}
            var o=new FlowerOffer{Id=Guid.NewGuid(),Owner=p.Entity.UID,Day=FlowerDay(),Until=Clock().AddMinutes(2),Parts=red.Concat(white).ToArray(),Cache=FirstRebirth.Cache(p)};
            if(p.Inventory.Count-o.Parts.Count(a=>a.After==0)+1>40){error="Make room for the bottle.";return null;}
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                SkillTable.LockOwner(c,t,p);Bag(c,t,p);
                using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_pirate_flowers WHERE uid=@uid AND day=CAST(@day AS date))","uid",(long)o.Owner,"day",o.Day))
                    if(Equals(q.ExecuteScalar(),true)){error="Return tomorrow with fresh flowers.";return null;}
                foreach(var part in o.Parts)GearImage(c,t,p,part.Item);
                o.Rows=FirstRebirth.Rows(c,t,p,"items");t.Commit();
            }}return o;
        }
        private static bool SameFlowers(GameState p,FlowerOffer o,Func<bool> context) {
            return o!=null && context!=null && Eligible(p) && p.Entity.UID==o.Owner && Clock()<o.Until
                && FlowerDay()==o.Day && context() && o.Cache==FirstRebirth.Cache(p)
                && o.Parts.All(a=>{ConquerItem item;return p.Inventory.TryGetItem(a.Uid,out item) && ReferenceEquals(item,a.Item) && FlowerUsable(item);});
        }
        internal static void CommitFlowers(GameState p,FlowerOffer o,Func<bool> context) {
            if(p==null || !Monitor.IsEntered(p.ItemSyncRoot) || !SameFlowers(p,o,context))throw new InvalidOperationException("Pirate flower offering changed.");
            var profile=CharacterProgression.Capture(p);var bottle=Q.New(R.Bottle,o.Parts.Any(a=>a.Bound));
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                SkillTable.LockOwner(c,t,p);Bag(c,t,p);
                if(FirstRebirth.Rows(c,t,p,"items")!=o.Rows)throw new InvalidOperationException("Saved Pirate flowers changed.");
                CharacterProgression.Persist(c,t,p,profile);
                if(Q.Execute(c,t,@"INSERT INTO revora_pirate_flowers(id,uid,day,bottle_uid,bound,material_uids,quantities,created_at)
VALUES(@id,@uid,CAST(@day AS date),@bottle,@bound,@materials,@counts,@now) ON CONFLICT(uid,day) DO NOTHING",
                    "id",o.Id,"uid",(long)o.Owner,"day",o.Day,"bottle",(long)bottle.UID,"bound",bottle.Bound,
                    "materials",o.Parts.Select(a=>(long)a.Uid).ToArray(),"counts",o.Parts.Select(a=>a.Count).ToArray(),"now",Clock())!=1)
                    throw new InvalidOperationException("Today's Pirate offering is already claimed.");
                foreach(var a in o.Parts) {
                    string debit=a.After==0?"DELETE FROM items":"UPDATE items SET stacksize=@after";
                    if(Q.Execute(c,t,debit+@" WHERE uid=@item AND entityid=@uid AND id=@type AND position=0 AND warehouse=0
AND stacksize=@stack AND bound=@bound AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')='0'",
                        "after",a.After,"item",(long)a.Uid,"uid",(long)o.Owner,"type",(int)a.Type,"stack",(int)a.Stack,"bound",a.Bound?1:0)!=1)
                        throw new InvalidOperationException("Pirate flower material changed.");
                }
                Q.Insert(c,t,o.Owner,bottle);
                if(!SameFlowers(p,o,context))throw new InvalidOperationException("Pirate offering session changed before commit.");t.Commit();
            }}
            CharacterProgression.Committed(p,profile);Q.Install(p,o.Parts.Where(a=>a.After==0).Select(a=>a.Uid),new[]{bottle});
            foreach(var a in o.Parts.Where(a=>a.After>0)){a.Item.StackSize=checked((ushort)a.After);a.Item.Mode=Enums.ItemMode.Update;a.Item.Send(p);a.Item.Mode=Enums.ItemMode.Default;}
            Q.Refresh(p);PirateEpicJournal.Refresh(p);
        }
    }
}
