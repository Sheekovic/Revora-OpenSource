using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class SummerCrafting
    {
        internal static Func<int,int> Roll=max=>Kernel.Random.Next(max);
        private static bool enabled;
        private sealed class Offer {internal GameState Player;internal Guid Id;internal int Kind,Color;internal uint Item,Before;internal DateTime End;}
        private sealed class Selection {internal GameState Player;internal uint[] Items;internal uint Item;internal DateTime End;}
        private sealed class Receipt {internal Guid Id;internal int Kind;}
        private static readonly ConcurrentDictionary<uint,Offer> offers=new ConcurrentDictionary<uint,Offer>();
        private static readonly ConcurrentDictionary<uint,Selection> selections=new ConcurrentDictionary<uint,Selection>();
        internal static bool Identity(uint id) {return id==3303764 || id==3303765 || id>=3303772 && id<=3303776 || id>=3303797 && id<=3303801;}
        internal static bool Material(uint id) {return id>=3303704 && id<=3303707;}
        private static bool BoundType(uint id) {return id<3303797;}
        private static int Color(uint id) {return id>=3303797?(int)(id-3303797):(int)(id-3303772);}
        private static string Name(uint id) {return ConquerItemInformation.BaseInformations[id].Name;}
        private static int Key(int color) {return 60136013+color*10001;}
        private static string Source(int color) {return color==4?"summer-siren-2017":"summer-glow-2017";}
        internal static void Initialize() {
            foreach(uint id in new uint[]{3303704,3303705,3303706,3303707,3303764,3303765,3303772,3303773,3303774,3303775,3303776,3303797,3303798,3303799,3303800,3303801}) {
                if(!ConquerItemInformation.BaseInformations.ContainsKey(id))throw new InvalidOperationException("Summer item catalog incomplete.");
                if(Identity(id))ConquerItemInformation.BaseInformations[id].StackSize=1;
            }
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_summer_crafts(
id uuid PRIMARY KEY,maker bigint NOT NULL CHECK(maker>0),kind integer NOT NULL CHECK(kind BETWEEN 1 AND 4),
item_uid bigint UNIQUE NOT NULL CHECK(item_uid>0),original_type integer NOT NULL,current_type integer NOT NULL,
roll integer NOT NULL CHECK(roll BETWEEN 0 AND 3),state varchar(12) NOT NULL DEFAULT 'issued' CHECK(state IN ('issued','consumed')),
created_at timestamptz NOT NULL DEFAULT now());
CREATE TABLE IF NOT EXISTS revora_summer_operations(
id uuid PRIMARY KEY,craft uuid NOT NULL REFERENCES revora_summer_crafts(id),uid bigint NOT NULL CHECK(uid>0),
action varchar(12) NOT NULL CHECK(action IN ('dye','unbind','activate')),before_type integer NOT NULL,after_type integer NOT NULL,
tool_uid bigint NOT NULL DEFAULT 0,roll integer NOT NULL CHECK(roll BETWEEN 0 AND 5),created_at timestamptz NOT NULL DEFAULT now());");}
            offers.Clear();selections.Clear();enabled=true;
        }
        private static Receipt Owned(NpgsqlConnection c,NpgsqlTransaction t,GameState p,ConquerItem item,int kind=0) {
            if(item==null || !Identity(item.ID) || item.Lock!=0 || item.StackSize!=1 || item.Bound!=BoundType(item.ID) || !Q.Owns(c,t,p,item.UID,item.ID,item.Bound))return null;
            using(var q=Q.Command(c,t,"SELECT id,kind,current_type,state FROM revora_summer_crafts WHERE item_uid=@item FOR UPDATE","item",(long)item.UID))
            using(var r=q.ExecuteReader()) {
                if(!r.Read() || r.GetInt32(2)!=item.ID || r.GetString(3)!="issued" || kind!=0 && r.GetInt32(1)!=kind)return null;
                return new Receipt{Id=r.GetGuid(0),Kind=r.GetInt32(1)};
            }
        }
        internal static bool Transferable(NpgsqlConnection c,NpgsqlTransaction t,GameState p,ConquerItem item) {
            if(item==null || item.Bound)return false;var receipt=Owned(c,t,p,item);return receipt!=null && receipt.Kind<=2;
        }
        private static void Consumed(NpgsqlConnection c,NpgsqlTransaction t,Guid id) {
            if(Q.Execute(c,t,"UPDATE revora_summer_crafts SET state='consumed' WHERE id=@id AND state='issued'","id",id)!=1)throw new InvalidOperationException("Summer item receipt changed.");
        }
        private static string Craft(GameState p,Offer offer) {
            int[] costs={30,99,10,5};uint material=(uint)(3303703+offer.Kind);
            var takes=SummerMaterials.Take(p,material,costs[offer.Kind-1]);if(takes==null)return "You need "+costs[offer.Kind-1]+" bound "+Name(material)+" in your bag.";
            if(p.Inventory.Count-takes.Count(a=>a.After==0)>=40)return "Make one space for the finished work.";
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                int roll=offer.Kind==1?Roll(4):0;if(roll<0 || roll>=4)throw new InvalidOperationException("Summer color roll invalid.");
                uint type=offer.Kind==1?(uint)(3303772+roll):offer.Kind==2?3303776u:offer.Kind==3?3303764u:3303765u;
                var item=Q.New(type);SummerMaterials.Write(c,t,p,takes);Q.Insert(c,t,p.Entity.UID,item);
                Q.Execute(c,t,"INSERT INTO revora_summer_crafts(id,maker,kind,item_uid,original_type,current_type,roll) VALUES(@id,@uid,@kind,@item,@type,@type,@roll)",
                    "id",offer.Id,"uid",(long)p.Entity.UID,"kind",offer.Kind,"item",(long)item.UID,"type",(int)type,"roll",roll);
                t.Commit();SummerMaterials.Install(p,takes);Q.Install(p,new uint[0],new[]{item});return Name(type)+" is ready. Activate wings to add them to your wardrobe.";
            }}
        }
        private static ConquerItem Tool(GameState p,uint type) {return p.Inventory.Objects.Where(i=>i.ID==type && i.Position==0 && i.Bound && i.Lock==0 && i.StackSize==1).OrderBy(i=>i.UID).FirstOrDefault();}
        private static string Change(GameState p,Offer offer) {
            ConquerItem item;if(!p.Inventory.TryGetItem(offer.Item,out item) || !Identity(item.ID) || item.ID!=offer.Before || item.ID==3303764 || item.ID==3303765)return "Bring the same wing package back to me.";
            bool dye=offer.Kind==5;int color=Color(item.ID);uint toolType=dye?3303764u:3303765u;
            if(dye && (color==4 || offer.Color<0 || offer.Color>3 || offer.Color==color))return "Choose a different Glow color.";
            if(!dye && !item.Bound)return "Those wings are already unbound.";
            var tool=Tool(p,toolType);if(tool==null)return "Bring one "+Name(toolType)+" in your bag.";
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                // One player's inventory lock owns both items; receipt rows also
                // verify that this work was actually issued by the artisan.
                var origin=Owned(c,t,p,item);var toolOrigin=Owned(c,t,p,tool,dye?3:4);
                if(origin==null || origin.Kind>2 || toolOrigin==null)return "I cannot alter these items without their original artisan's mark.";
                int roll=!dye && color==4?Roll(6):0;if(roll<0 || roll>=6)throw new InvalidOperationException("Summer unbinding roll invalid.");
                bool success=dye || color!=4 || roll==0;uint before=item.ID;
                uint after=!success?before:dye?(uint)((item.Bound?3303772:3303797)+offer.Color):(uint)(3303797+color);
                Q.Consume(c,t,p,tool.UID,tool.ID,true);Consumed(c,t,toolOrigin.Id);
                if(success) {
                    if(Q.Execute(c,t,"UPDATE items SET id=@after,bound=@bound WHERE uid=@item AND entityid=@uid AND id=@before AND position=0 AND warehouse=0 AND stacksize=1 AND bound=@oldbound",
                        "after",(int)after,"bound",BoundType(after)?1:0,"item",(long)item.UID,"uid",(long)p.Entity.UID,"before",(int)before,"oldbound",item.Bound?1:0)!=1)throw new InvalidOperationException("Summer wing ownership changed.");
                    Q.Execute(c,t,"UPDATE revora_summer_crafts SET current_type=@type WHERE id=@id","type",(int)after,"id",origin.Id);
                }
                Q.Execute(c,t,"INSERT INTO revora_summer_operations(id,craft,uid,action,before_type,after_type,tool_uid,roll) VALUES(@id,@craft,@uid,@action,@before,@after,@tool,@roll)",
                    "id",offer.Id,"craft",origin.Id,"uid",(long)p.Entity.UID,"action",dye?"dye":"unbind","before",(int)before,"after",(int)after,"tool",(long)tool.UID,"roll",roll);
                t.Commit();Q.Install(p,new[]{tool.UID},new ConquerItem[0]);
                if(success){item.ID=after;item.Bound=BoundType(after);item.Mode=Enums.ItemMode.Update;item.Send(p);item.Mode=Enums.ItemMode.Default;}
                return success?"Your "+Name(after)+" is ready.":"The solution was spent, but the Siren's binding held. Your wings are safe.";
            }}
        }
        private static string Activate(GameState p,ConquerItem item) {
            if(item==null || !Identity(item.ID) || item.ID==3303764 || item.ID==3303765)return "Bring a finished wing package.";
            int color=Color(item.ID),key=Key(color);
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                var receipt=Owned(c,t,p,item);if(receipt==null || receipt.Kind>2)return "These wings need a genuine artisan's mark before they can awaken.";
                using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_event_awards WHERE uid=@uid AND title_key=@key AND source=@source AND NOT revoked AND expires_at IS NULL)","uid",(long)p.Entity.UID,"key",key,"source",Source(color)))
                    if(Equals(q.ExecuteScalar(),true))return "You already own this design. Keep this package for dyeing or unbinding.";
                Q.Consume(c,t,p,item.UID,item.ID,item.Bound);
                if(!ProgressionEvidence.Grant(c,t,p.Entity.UID,Source(color),receipt.Id.ToString("N"),key,SummerHunt.Clock()))throw new InvalidOperationException("Summer activation receipt already used.");
                Consumed(c,t,receipt.Id);
                Q.Execute(c,t,"INSERT INTO revora_summer_operations(id,craft,uid,action,before_type,after_type,roll) VALUES(@id,@craft,@uid,'activate',@type,@type,0)",
                    "id",Guid.NewGuid(),"craft",receipt.Id,"uid",(long)p.Entity.UID,"type",(int)item.ID);
                t.Commit();Q.Install(p,new[]{item.UID},new ConquerItem[0]);Q.Refresh(p);return "Your new wings are waiting in the wardrobe.";
            }}
        }
        internal static bool TryUse(ConquerItem item,GameState p) {
            if(!enabled || item==null || !Material(item.ID) && !Identity(item.ID))return false;
            if(p==null || p.ItemSyncRoot==null)return true;
            lock(p.ItemSyncRoot) {
                if(!Q.Player(p) || p.Booth!=null || p.Trade==null || p.Trade.InTrade)return true;
                try{p.Send(Material(item.ID) || item.ID==3303764 || item.ID==3303765?"Bring these materials to Liora in Twin City near (460,281).":Activate(p,item));}
                catch(Exception ex){Q.Fail(p,"summer-activation",ex);}
            }
            return true;
        }
        internal static void Cleanup() {
            var now=SummerHunt.Clock();foreach(var a in offers.ToArray())if(a.Value.End<=now)((ICollection<KeyValuePair<uint,Offer>>)offers).Remove(a);
            foreach(var a in selections.ToArray())if(a.Value.End<=now || !Q.Player(a.Value.Player))((ICollection<KeyValuePair<uint,Selection>>)selections).Remove(a);
        }
        internal static bool Menu(GameState p,byte option,Npcs d) {
            var now=SummerHunt.Clock();
            if(option==10) {
                d.Text("Bring bound fragments and I will fashion wings or tools for you.");d.Option("Glow Wings: 30 fragments.",11);d.Option("Siren Song: 99 fragments.",12);
                d.Option("Rainbow Wand: 10 fragments.",13);d.Option("Unbind Solution: 5 ingredients.",14);return true;
            }
            if(option>=11 && option<=14) {
                int kind=option-10;offers[p.Entity.UID]=new Offer{Player=p,Id=Guid.NewGuid(),Kind=kind,End=now.AddMinutes(2)};
                d.Text(kind==1?"Thirty Glow fragments become one bound wing package. Each of the four colors has an equal chance; you may receive a duplicate.":kind==2?"Ninety-nine Siren fragments become one permanent bound Siren Song package.":kind==3?"Ten Rainbow fragments become one wand. It changes an unopened Glow package to a different color you choose.":"Five unbind ingredients become one solution. It always frees Glow Wings; Siren Song has a one-in-six chance, and every attempt spends the solution.");
                d.Option("Make it for me.",220);return true;
            }
            if(option>=20 && option<=24) {
                var items=p.Inventory.Objects.Where(i=>Identity(i.ID) && i.ID!=3303764 && i.ID!=3303765).OrderBy(i=>i.UID).Take(40).Select(i=>i.UID).ToArray();
                selections[p.Entity.UID]=new Selection{Player=p,Items=items,End=now.AddMinutes(2)};int page=option-20;
                d.Text("Choose an unopened wing package from your bag.");for(int i=page*8;i<Math.Min(items.Length,page*8+8);i++){ConquerItem item;p.Inventory.TryGetItem(items[i],out item);d.Option(Name(item.ID)+" #"+item.UID,(byte)(40+i));}
                if(page>0)d.Option("Previous packages.",(byte)(option-1));if(items.Length>(page+1)*8)d.Option("More packages.",(byte)(option+1));return true;
            }
            Selection select;
            if(option>=40 && option<=79) {
                if(!selections.TryGetValue(p.Entity.UID,out select) || select.Player!=p || select.End<=now || option-40>=select.Items.Length){d.Text("Show me your packages again.");return true;}
                select.Item=select.Items[option-40];ConquerItem item;if(!p.Inventory.TryGetItem(select.Item,out item)){d.Text("That package has left your bag.");return true;}
                d.Text(Name(item.ID)+" is still unopened. What shall we do with it?");
                if(Color(item.ID)<4)for(int i=0;i<4;i++)if(i!=Color(item.ID))d.Option("Dye "+Name((uint)(3303772+i))+".",(byte)(80+i));
                if(item.Bound)d.Option("Unbind these wings.",85);d.Option("Awaken this design.",86);return true;
            }
            if(option>=80 && option<=86) {
                if(!selections.TryGetValue(p.Entity.UID,out select) || select.Player!=p || select.End<=now || select.Item==0){d.Text("Choose your wing package first.");return true;}
                ConquerItem item;if(!p.Inventory.TryGetItem(select.Item,out item)){d.Text("Bring the selected package back.");return true;}
                if(option==86){d.Text(Activate(p,item));return true;}
                if(option==84){d.Text("Choose one of the listed colors.");return true;}
                bool dye=option<84;offers[p.Entity.UID]=new Offer{Player=p,Id=Guid.NewGuid(),Kind=dye?5:6,Color=option-80,Item=select.Item,Before=item.ID,End=now.AddMinutes(2)};
                d.Text(dye?"One Rainbow Wand will change this unopened Glow package to "+Name((uint)(3303772+option-80))+".":Color(item.ID)==4?"One solution gives a one-in-six chance to unbind Siren Song. The solution is spent even if the binding holds.":"One solution will make this Glow package unbound. It can then be traded before activation.");
                d.Option("I accept. Continue.",220);return true;
            }
            if(option==220) {
                Offer offer;if(!offers.TryRemove(p.Entity.UID,out offer) || offer.Player!=p || offer.End<=now){d.Text("Choose your craft again when you are ready.");return true;}
                d.Text(offer.Kind<=4?Craft(p,offer):Change(p,offer));return true;
            }
            return false;
        }
    }
}
