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
        internal static bool StackMaterial(uint type){return type==R.Water || R.MapStars(type)>0;}
        internal static bool Split(GameState p,uint uid,uint amount){
            if(p==null || !Monitor.IsEntered(p.ItemSyncRoot))throw new InvalidOperationException("Pirate split requires owner lock.");
            ConquerItem item;
            if(!Q.Player(p) || !CharacterProgression.Managed(p) || p.Trade==null || p.Trade.InTrade || p.Booth!=null || p.Entity.PokerTable!=0
                || p.Inventory.Count>=40 || !p.Inventory.TryGetItem(uid,out item) || !StackMaterial(item.ID) || !FlowerUsable(item)
                || amount==0 || amount>=item.StackSize)return false;
            ushort before=item.StackSize,after=(ushort)(before-amount);string cache=FirstRebirth.Cache(p);
            var split=Q.New(item.ID,item.Bound);split.StackSize=(ushort)amount;split.Durability=item.Durability;split.MaximDurability=item.MaximDurability;
            split.Plus=item.Plus;split.Bless=item.Bless;split.Color=item.Color;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                SkillTable.LockOwner(c,t,p);Bag(c,t,p);GearImage(c,t,p,item);
                if(Q.Execute(c,t,"UPDATE items SET stacksize=@after WHERE uid=@item AND entityid=@owner AND position=0 AND warehouse=0 AND stacksize=@before",
                    "after",(int)after,"item",(long)uid,"owner",(long)p.Entity.UID,"before",(int)before)!=1)throw new InvalidOperationException("Pirate split preimage changed.");
                Q.Insert(c,t,p.Entity.UID,split);ConquerItem owned;
                if(!Q.Player(p) || !CharacterProgression.Managed(p) || p.Trade.InTrade || p.Booth!=null || p.Entity.PokerTable!=0 || p.Inventory.Count>=40
                    || cache!=FirstRebirth.Cache(p) || !p.Inventory.TryGetItem(uid,out owned) || !ReferenceEquals(owned,item) || !FlowerUsable(item))throw new InvalidOperationException("Pirate split session changed.");
                t.Commit();
            }}
            item.StackSize=after;item.Mode=Enums.ItemMode.Update;item.Send(p);item.Mode=Enums.ItemMode.Default;
            Q.Install(p,new uint[0],new[]{split});Q.Refresh(p);return true;
        }
        internal static bool Merge(GameState p,uint first,uint second){
            if(p==null || !Monitor.IsEntered(p.ItemSyncRoot))throw new InvalidOperationException("Pirate merge requires owner lock.");
            ConquerItem a,b;
            if(!Q.Player(p) || !CharacterProgression.Managed(p) || p.Trade==null || p.Trade.InTrade || p.Booth!=null || p.Entity.PokerTable!=0
                || first==second || !p.Inventory.TryGetItem(first,out a) || !p.Inventory.TryGetItem(second,out b)
                || a.ID!=b.ID || a.Bound!=b.Bound || !StackMaterial(a.ID) || !FlowerUsable(a) || !FlowerUsable(b))return false;
            int combined=Math.Max(1,(int)a.StackSize)+Math.Max(1,(int)b.StackSize);
            if(combined>Math.Max(1,(int)ConquerItemInformation.BaseInformations[a.ID].StackSize) || combined>ushort.MaxValue)return false;
            ushort originalA=a.StackSize,originalB=b.StackSize;string cache=FirstRebirth.Cache(p);
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                SkillTable.LockOwner(c,t,p);Bag(c,t,p);GearImage(c,t,p,a);GearImage(c,t,p,b);
                if(Q.Execute(c,t,"UPDATE items SET stacksize=@after WHERE uid=@item AND entityid=@owner AND position=0 AND warehouse=0 AND stacksize=@before",
                    "after",combined,"item",(long)first,"owner",(long)p.Entity.UID,"before",(int)originalA)!=1
                    || Q.Execute(c,t,"DELETE FROM items WHERE uid=@item AND entityid=@owner AND position=0 AND warehouse=0 AND stacksize=@before",
                    "item",(long)second,"owner",(long)p.Entity.UID,"before",(int)originalB)!=1)throw new InvalidOperationException("Pirate merge preimage changed.");
                ConquerItem x,y;
                if(!Q.Player(p) || p.Trade.InTrade || p.Booth!=null || p.Entity.PokerTable!=0 || cache!=FirstRebirth.Cache(p)
                    || !p.Inventory.TryGetItem(first,out x) || !ReferenceEquals(x,a) || !p.Inventory.TryGetItem(second,out y) || !ReferenceEquals(y,b)
                    || !FlowerUsable(a) || !FlowerUsable(b))throw new InvalidOperationException("Pirate merge session changed.");
                t.Commit();
            }}
            Q.Install(p,new[]{second},new ConquerItem[0]);a.StackSize=(ushort)combined;a.Mode=Enums.ItemMode.Update;a.Send(p);a.Mode=Enums.ItemMode.Default;Q.Refresh(p);return true;
        }
        internal sealed class SearchOffer {
            internal Guid Id;internal uint Owner,InputUid,InputType;internal ushort Stack;
            internal bool Bound;internal ConquerItem Input;internal string Cache,Rows;internal DateTime Until;
            internal int After {get{return Math.Max(1,(int)Stack)-1;}}
        }
        private static readonly System.Security.Cryptography.RandomNumberGenerator searchRandom=System.Security.Cryptography.RandomNumberGenerator.Create();
        private static int DrawSearch(int maximum) {
            if(maximum<1 || maximum>100)throw new ArgumentOutOfRangeException("maximum");
            int limit=256-256%maximum;var bytes=new byte[1];
            lock(searchRandom){do{searchRandom.GetBytes(bytes);}while(bytes[0]>=limit);}return bytes[0]%maximum;
        }
        internal static Func<int,int> SearchDraw=DrawSearch;
        internal static bool SearchInput(ConquerItem item){return FlowerUsable(item) && (item.ID==R.Bottle || R.MapStars(item.ID)>0);}
        internal static SearchOffer PrepareSearch(GameState p,uint uid,out string error) {
            if(p==null || !Monitor.IsEntered(p.ItemSyncRoot))throw new InvalidOperationException("Pirate search requires owner lock.");
            error=null;ConquerItem input;
            if(!Eligible(p)){error="This voyage awaits second-reborn Pirates of level 70.";return null;}
            if(!p.Inventory.TryGetItem(uid,out input) || !SearchInput(input)){error="Bring your bottle or treasure chart.";return null;}
            var o=new SearchOffer{Id=Guid.NewGuid(),Owner=p.Entity.UID,InputUid=uid,InputType=input.ID,Input=input,Stack=input.StackSize,Bound=input.Bound,Until=Clock().AddMinutes(2),Cache=FirstRebirth.Cache(p)};
            int room=input.ID==R.LastMap?2:1;
            if(p.Inventory.Count-(o.After==0?1:0)+room>40){error="Make room for every treasure first.";return null;}
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                SkillTable.LockOwner(c,t,p);Bag(c,t,p);GearImage(c,t,p,input);o.Rows=FirstRebirth.Rows(c,t,p,"items");t.Commit();
            }}return o;
        }
        private static bool SameSearch(GameState p,SearchOffer o,Func<bool> context) {
            ConquerItem item;
            return o!=null && context!=null && Eligible(p) && p.Entity.UID==o.Owner && Clock()<o.Until && context()
                && o.Cache==FirstRebirth.Cache(p) && p.Inventory.TryGetItem(o.InputUid,out item) && ReferenceEquals(item,o.Input) && FlowerUsable(item);
        }
        internal static void CommitSearch(GameState p,SearchOffer o,Func<bool> context) {
            if(p==null || !Monitor.IsEntered(p.ItemSyncRoot) || !SameSearch(p,o,context))throw new InvalidOperationException("Pirate search offer changed.");
            var profile=CharacterProgression.Capture(p);ConquerItem[] rewards;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                SkillTable.LockOwner(c,t,p);Bag(c,t,p);
                if(o.Rows!=FirstRebirth.Rows(c,t,p,"items"))throw new InvalidOperationException("Saved Pirate search items changed.");
                CharacterProgression.Persist(c,t,p,profile);
                rewards=R.SearchRewards(o.InputType,SearchDraw).Select(id=>Q.New(id,id==R.Water || o.Bound)).ToArray();
                string debit=o.After==0?"DELETE FROM items":"UPDATE items SET stacksize=@after";
                if(Q.Execute(c,t,debit+@" WHERE uid=@item AND entityid=@uid AND id=@type AND position=0 AND warehouse=0
AND stacksize=@stack AND bound=@bound AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')='0'",
                    "after",o.After,"item",(long)o.InputUid,"uid",(long)o.Owner,"type",(int)o.InputType,"stack",(int)o.Stack,"bound",o.Bound?1:0)!=1)
                    throw new InvalidOperationException("Pirate chart changed.");
                foreach(var item in rewards)Q.Insert(c,t,o.Owner,item);
                if(Q.Execute(c,t,@"INSERT INTO revora_pirate_searches(id,uid,input_uid,input_type,stack_before,reward_uids,reward_types,created_at)
VALUES(@id,@uid,@input,@type,@stack,@rewards,@types,@now)","id",o.Id,"uid",(long)o.Owner,"input",(long)o.InputUid,"type",(int)o.InputType,"stack",(int)o.Stack,
                    "rewards",rewards.Select(i=>(long)i.UID).ToArray(),"types",rewards.Select(i=>(int)i.ID).ToArray(),"now",Clock())!=1)
                    throw new InvalidOperationException("Pirate search receipt missing.");
                if(!SameSearch(p,o,context))throw new InvalidOperationException("Pirate search session changed before commit.");t.Commit();
            }}
            CharacterProgression.Committed(p,profile);Q.Install(p,o.After==0?new[]{o.InputUid}:new uint[0],rewards);
            if(o.After>0){o.Input.StackSize=checked((ushort)o.After);o.Input.Mode=Enums.ItemMode.Update;o.Input.Send(p);o.Input.Mode=Enums.ItemMode.Default;}Q.Refresh(p);PirateEpicJournal.Refresh(p);
        }
    }
}
