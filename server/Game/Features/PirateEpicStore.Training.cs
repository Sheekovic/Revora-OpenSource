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
        internal sealed class ExchangeOffer {
            internal Guid Id;internal uint Owner,Product;internal FlowerPart[] Parts;
            internal string Cache,Rows;internal DateTime Until;
        }
        internal static ExchangeOffer PrepareExchange(GameState p,byte choice,out string error) {
            if(p==null || !Monitor.IsEntered(p.ItemSyncRoot))throw new InvalidOperationException("Pirate exchange requires owner lock.");
            error=null;if(!Eligible(p)){error="This trade awaits second-reborn Pirates of level 70.";return null;}
            uint product=R.Scroll(choice);var parts=Flowers(p,R.Water,R.WaterPerScroll);
            if(parts==null){error="Bring ten Spring Water for a scroll.";return null;}
            if(p.Inventory.Count-parts.Count(a=>a.After==0)+1>40){error="Make room for the scroll.";return null;}
            if(!ConquerItemInformation.BaseInformations.ContainsKey(product) || SpellTable.GetSpell(R.ScrollSpell(product),0)==null)throw new InvalidOperationException("Pirate scroll catalog missing.");
            var o=new ExchangeOffer{Id=Guid.NewGuid(),Owner=p.Entity.UID,Product=product,Parts=parts,Cache=FirstRebirth.Cache(p),Until=Clock().AddMinutes(2)};
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                SkillTable.LockOwner(c,t,p);Bag(c,t,p);foreach(var a in parts)GearImage(c,t,p,a.Item);o.Rows=FirstRebirth.Rows(c,t,p,"items");t.Commit();
            }}return o;
        }
        private static bool SameExchange(GameState p,ExchangeOffer o,Func<bool> context) {
            return o!=null && context!=null && Eligible(p) && p.Entity.UID==o.Owner && Clock()<o.Until && context() && o.Cache==FirstRebirth.Cache(p)
                && o.Parts.All(a=>{ConquerItem item;return p.Inventory.TryGetItem(a.Uid,out item) && ReferenceEquals(item,a.Item) && FlowerUsable(item);});
        }
        internal static void CommitExchange(GameState p,ExchangeOffer o,Func<bool> context) {
            if(p==null || !Monitor.IsEntered(p.ItemSyncRoot) || !SameExchange(p,o,context))throw new InvalidOperationException("Pirate exchange offer changed.");
            var profile=CharacterProgression.Capture(p);var scroll=Q.New(o.Product,o.Parts.Any(a=>a.Bound));
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                SkillTable.LockOwner(c,t,p);Bag(c,t,p);
                if(o.Rows!=FirstRebirth.Rows(c,t,p,"items"))throw new InvalidOperationException("Saved Pirate exchange items changed.");
                CharacterProgression.Persist(c,t,p,profile);
                foreach(var a in o.Parts) {
                    string debit=a.After==0?"DELETE FROM items":"UPDATE items SET stacksize=@after";
                    if(Q.Execute(c,t,debit+@" WHERE uid=@item AND entityid=@uid AND id=@type AND position=0 AND warehouse=0
AND stacksize=@stack AND bound=@bound AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')='0'",
                        "after",a.After,"item",(long)a.Uid,"uid",(long)o.Owner,"type",(int)R.Water,"stack",(int)a.Stack,"bound",a.Bound?1:0)!=1)throw new InvalidOperationException("Pirate Water changed.");
                }
                Q.Insert(c,t,o.Owner,scroll);
                if(Q.Execute(c,t,"INSERT INTO revora_pirate_scroll_exchanges(id,uid,scroll_uid,scroll_type,water_count,bound,created_at) VALUES(@id,@uid,@scroll,@type,10,@bound,@now)",
                    "id",o.Id,"uid",(long)o.Owner,"scroll",(long)scroll.UID,"type",(int)scroll.ID,"bound",scroll.Bound,"now",Clock())!=1)throw new InvalidOperationException("Pirate exchange receipt missing.");
                if(!SameExchange(p,o,context))throw new InvalidOperationException("Pirate exchange session changed.");t.Commit();
            }}
            CharacterProgression.Committed(p,profile);Q.Install(p,o.Parts.Where(a=>a.After==0).Select(a=>a.Uid),new[]{scroll});
            foreach(var a in o.Parts.Where(a=>a.After>0)){a.Item.StackSize=checked((ushort)a.After);a.Item.Mode=Enums.ItemMode.Update;a.Item.Send(p);a.Item.Mode=Enums.ItemMode.Default;}Q.Refresh(p);PirateEpicJournal.Refresh(p);
        }
        private static bool CanTrain(GameState p,byte minimum=40) {
            return Q.Player(p) && p.Entity.Class>=70 && p.Entity.Class<=75 && p.Entity.Level>=minimum && p.Trade!=null && !p.Trade.InTrade
                && p.Booth==null && p.Entity.PokerTable==0 && CharacterProgression.Managed(p);
        }
        internal static bool LearnWindstorm(GameState p,Func<bool> context) {
            if(p==null || !Monitor.IsEntered(p.ItemSyncRoot))throw new InvalidOperationException("Pirate training requires owner lock.");
            if(context==null || !CanTrain(p,15) || !context() || p.Spells.ContainsKey(11140))return false;
            if(SpellTable.GetSpell(11140,0)==null)throw new InvalidOperationException("Windstorm catalog missing.");
            var profile=CharacterProgression.Capture(p);var learned=new Spell(true){ID=11140,Level=0};
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                SkillTable.LockOwner(c,t,p);CharacterProgression.Persist(c,t,p,profile);
                using(var q=Q.Command(c,t,"SELECT mapid FROM entities WHERE uid=@uid","uid",(long)p.Entity.UID))
                    if(Convert.ToInt32(q.ExecuteScalar())!=1004)throw new InvalidOperationException("Pirate trainer location changed.");
                SkillTable.Persist(c,t,p,learned);
                if(Q.Execute(c,t,"INSERT INTO revora_pirate_training(id,uid,scroll_uid,scroll_type,spell,created_at) VALUES(@id,@uid,0,0,11140,@now)",
                    "id",Guid.NewGuid(),"uid",(long)p.Entity.UID,"now",Clock())!=1)throw new InvalidOperationException("Windstorm training receipt missing.");
                if(!CanTrain(p,15) || !context() || p.Entity.Class!=profile.Values[7] || p.Entity.Level!=profile.Values[0] || p.Spells.ContainsKey(11140))
                    throw new InvalidOperationException("Pirate training session changed.");
                t.Commit();
            }}
            CharacterProgression.Committed(p,profile);learned.Available=true;p.Spells.Add(11140,learned);learned.Send(p);Q.Refresh(p);return true;
        }
        internal static bool LearnScroll(GameState p,uint uid,Func<bool> context) {
            if(p==null || !Monitor.IsEntered(p.ItemSyncRoot))throw new InvalidOperationException("Pirate training requires owner lock.");
            ConquerItem item;
            if(context==null || !CanTrain(p) || !context() || !p.Inventory.TryGetItem(uid,out item) || !FlowerUsable(item) || item.StackSize>1)return false;
            ushort spell=R.ScrollSpell(item.ID);if(spell==0 || p.Spells.ContainsKey(spell))return false;
            if(SpellTable.GetSpell(spell,0)==null)throw new InvalidOperationException("Pirate spell catalog missing.");
            string cache=FirstRebirth.Cache(p);var profile=CharacterProgression.Capture(p);var learned=new Spell(true){ID=spell,Level=0};
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                SkillTable.LockOwner(c,t,p);Bag(c,t,p);GearImage(c,t,p,item);
                using(var q=Q.Command(c,t,"SELECT id FROM skills WHERE entityid=@uid AND id=@spell FOR UPDATE","uid",(long)p.Entity.UID,"spell",(int)spell))
                    if(q.ExecuteScalar()!=null)return false;
                CharacterProgression.Persist(c,t,p,profile);
                if(Q.Execute(c,t,@"DELETE FROM items WHERE uid=@item AND entityid=@uid AND id=@type AND position=0 AND warehouse=0
AND stacksize=@stack AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')='0'",
                    "item",(long)item.UID,"uid",(long)p.Entity.UID,"type",(int)item.ID,"stack",(int)item.StackSize)!=1)throw new InvalidOperationException("Pirate scroll changed.");
                SkillTable.Persist(c,t,p,learned);
                if(Q.Execute(c,t,"INSERT INTO revora_pirate_training(id,uid,scroll_uid,scroll_type,spell,created_at) VALUES(@id,@uid,@scroll,@type,@spell,@now)",
                    "id",Guid.NewGuid(),"uid",(long)p.Entity.UID,"scroll",(long)item.UID,"type",(int)item.ID,"spell",(int)spell,"now",Clock())!=1)throw new InvalidOperationException("Pirate training receipt missing.");
                if(!CanTrain(p) || !context() || cache!=FirstRebirth.Cache(p) || p.Spells.ContainsKey(spell))throw new InvalidOperationException("Pirate training session changed.");t.Commit();
            }}
            CharacterProgression.Committed(p,profile);Q.Install(p,new[]{item.UID},new ConquerItem[0]);
            learned.Available=true;p.Spells.Add(spell,learned);learned.Send(p);Q.Refresh(p);PirateEpicJournal.Refresh(p);return true;
        }
    }
}
