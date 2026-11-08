using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class HousingMaterials
    {
        internal static Func<DateTime> Clock = () => DateTime.UtcNow;
        private static bool enabled;
        private sealed class Recipe {
            internal uint Npc, Input, Output; internal int Count; internal string Words;
            internal Recipe(uint npc,uint input,int count,uint output,string words){Npc=npc;Input=input;Count=count;Output=output;Words=words;}
        }
        private static readonly Recipe[] recipes = {
            new Recipe(30158,0,5,721171,"Five ordinary ores for a piece of timber."),
            new Recipe(30159,721171,10,721172,"Ten pieces of timber for a Rosewood Voucher."),
            new Recipe(30159,721172,10,721173,"Ten Rosewood Vouchers for a Timber Voucher."),
            new Recipe(30157,721173,5,721170,"Five Timber Vouchers for a House Permit."),
            new Recipe(30160,1,12,721175,"Twelve copper ores for a Copper Ore Voucher."),
            new Recipe(30160,721175,10,721176,"Ten Copper Ore Vouchers for an Ore Voucher."),
            new Recipe(30157,721176,10,721174,"Ten Ore Vouchers for an Upgrade Certificate.")
        };
        private sealed class Take {
            internal ConquerItem Item; internal uint Uid,Type; internal ushort Stack; internal int Count; internal bool Bound;
            internal int After { get { return Math.Max(1,(int)Stack)-Count; } }
        }
        private sealed class Offer {internal Guid Id;internal int Recipe;internal DateTime Until;internal List<Take> Materials;}
        private static readonly ConditionalWeakTable<GameState,Offer> offers=new ConditionalWeakTable<GameState,Offer>();
        private static bool Copper(uint id){return id>=1072020 && id<=1072029;}
        // Explicit ordinary-ore whitelist. Event ores and unrelated catalog families are excluded.
        private static bool Ore(uint id){return Copper(id) || id>=1072010 && id<=1072019 || id==1072031 || id>=1072040 && id<=1072059;}
        private static bool Matches(Recipe recipe,uint id){return recipe.Input==0?Ore(id):recipe.Input==1?Copper(id):id==recipe.Input;}
        private static bool Usable(ConquerItem i){return i.Position==0 && i.Warehouse==0 && i.Lock==0 && !i.Suspicious && !i.InWardrobe && i.Days==0 && i.TimeLeftInMinutes==0;}
        private static ushort Location(uint npc){return npc==30157?(ushort)1002:(ushort)1036;}
        private static bool Near(GameState p,uint npc){return Q.Near(p,npc,Location(npc)) && p.Map.BaseID==Location(npc) && p.Entity.PokerTable==0;}
        internal static void Initialize()
        {
            foreach(uint id in recipes.Select(r=>r.Output).Concat(Enumerable.Range(1072010,50).Select(i=>(uint)i).Where(Ore)))
                if(!ConquerItemInformation.BaseInformations.ContainsKey(id))throw new InvalidOperationException("Housing material catalog missing.");
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_house_exchanges(
id uuid PRIMARY KEY,uid bigint NOT NULL,recipe integer NOT NULL CHECK(recipe BETWEEN 0 AND 6),
item_uid bigint NOT NULL UNIQUE,bound boolean NOT NULL,created_at timestamptz NOT NULL DEFAULT now());
CREATE TABLE IF NOT EXISTS revora_house_materials(
exchange uuid NOT NULL REFERENCES revora_house_exchanges(id),item_uid bigint NOT NULL,item_type integer NOT NULL,
stack_before integer NOT NULL CHECK(stack_before BETWEEN 0 AND 65535),amount integer NOT NULL CHECK(amount BETWEEN 1 AND 12),
bound boolean NOT NULL,PRIMARY KEY(exchange,item_uid),CHECK(amount<=greatest(1,stack_before)));");}
            enabled=true;
        }
        // Startup overlay: preserve the installed NPC row; no live database relocation.
        internal static void PlaceAgent()
        {
            var town=Kernel.Maps[1002]; const ushort x=342,y=518; ushort mesh;
            if(town.BaseID!=1002 || town.Npcs.Values.Any(n=>n.UID!=30157 && Math.Abs(n.X-x)<=1 && Math.Abs(n.Y-y)<=1))
                throw new InvalidOperationException("House Agent placement conflicts with another actor.");
            Revora.Interfaces.INpc existing;
            if(town.Npcs.TryGetValue(30157,out existing) && existing.X==x && existing.Y==y)return;
            if(!town.Floor[x,y,MapObjectType.Player])throw new InvalidOperationException("House Agent terrain is blocked.");
            using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,"SELECT lookface FROM npcs WHERE id=@id","id",30157L)) {
                object value=q.ExecuteScalar();if(value==null)throw new InvalidOperationException("House Agent catalog missing.");mesh=Convert.ToUInt16(value);
            }}
            foreach(var map in Kernel.Maps.Values.ToArray())if(map.Npcs.TryGetValue(30157,out existing))map.RemoveNpc(existing);
            Map.SecoundNpc.Remove(30157);
            town.AddNpc(new NpcSpawn{UID=30157,MapID=1002,X=x,Y=y,Mesh=mesh,Type=Enums.NpcType.Talker,Name="HouseAgent"});
        }
        private static void Prepare(GameState p,int index,Npcs d)
        {
            offers.Remove(p);var r=recipes[index];var parts=new List<Take>();int needed=r.Count;
            foreach(var i in p.Inventory.Objects.Where(i=>Matches(r,i.ID) && Usable(i)).OrderByDescending(i=>i.Bound).ThenBy(i=>i.UID)) {
                int count=Math.Min(needed,Math.Max(1,(int)i.StackSize));
                parts.Add(new Take{Item=i,Uid=i.UID,Type=i.ID,Stack=i.StackSize,Count=count,Bound=i.Bound});needed-=count;if(needed==0)break;
            }
            if(needed!=0){d.Text(r.Words+" Bring the materials when you are ready.");return;}
            if(p.Inventory.Count-parts.Count(m=>m.After==0)>=40){d.Text("Make room in your bag first.");return;}
            offers.Add(p,new Offer{Id=Guid.NewGuid(),Recipe=index,Until=Clock().AddMinutes(2),Materials=parts});
            d.Text(r.Words);if(parts.Any(m=>m.Bound))d.Text("The finished goods will be bound.");d.Option("Agreed.",40);
        }
        private static bool Same(GameState p,Take m){ConquerItem i;return p.Inventory.TryGetItem(m.Uid,out i) && ReferenceEquals(i,m.Item) && Usable(i) && i.ID==m.Type && i.StackSize==m.Stack && i.Bound==m.Bound;}
        private static void Confirm(GameState p,Npcs d)
        {
            Offer offer;if(!offers.TryGetValue(p,out offer)){d.Text("Tell me what you wish to exchange.");return;}offers.Remove(p);
            var r=recipes[offer.Recipe];
            if(!Near(p,r.Npc) || Clock()>=offer.Until || !offer.Materials.All(m=>Same(p,m))){d.Text("Show me your materials again.");return;}
            if(p.Inventory.Count-offer.Materials.Count(m=>m.After==0)>=40){d.Text("Make room in your bag first.");return;}
            var result=Q.New(r.Output,offer.Materials.Any(m=>m.Bound));
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                SkillTable.LockOwner(c,t,p);
                using(var q=Q.Command(c,t,"SELECT mapid FROM entities WHERE uid=@uid","uid",(long)p.Entity.UID))
                    if(Convert.ToUInt16(q.ExecuteScalar())!=Location(r.Npc))throw new InvalidOperationException("Housing exchange location changed.");
                var saved=new List<uint>();
                using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid","uid",(long)p.Entity.UID))
                using(var reader=q.ExecuteReader())while(reader.Read())saved.Add(checked((uint)Convert.ToInt64(reader[0])));
                if(!saved.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException("Housing exchange inventory changed.");
                foreach(var m in offer.Materials) {
                    string debit=m.After==0?"DELETE FROM items":"UPDATE items SET stacksize=@after";
                    if(Q.Execute(c,t,debit+@" WHERE uid=@item AND entityid=@uid AND id=@type AND stacksize=@stack AND bound=@bound
AND position=0 AND warehouse=0 AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0
AND COALESCE(NULLIF(days,''),'0')='0'","after",m.After,"item",(long)m.Uid,"uid",(long)p.Entity.UID,"type",(int)m.Type,"stack",(int)m.Stack,"bound",m.Bound?1:0)!=1)
                        throw new InvalidOperationException("Housing material debit was not saved.");
                }
                Q.Insert(c,t,p.Entity.UID,result);
                if(Q.Execute(c,t,"INSERT INTO revora_house_exchanges(id,uid,recipe,item_uid,bound) VALUES(@id,@uid,@recipe,@item,@bound)","id",offer.Id,"uid",(long)p.Entity.UID,"recipe",offer.Recipe,"item",(long)result.UID,"bound",result.Bound)!=1)
                    throw new InvalidOperationException("Housing exchange was not recorded.");
                foreach(var m in offer.Materials)
                    if(Q.Execute(c,t,"INSERT INTO revora_house_materials(exchange,item_uid,item_type,stack_before,amount,bound) VALUES(@id,@item,@type,@stack,@count,@bound)","id",offer.Id,"item",(long)m.Uid,"type",(int)m.Type,"stack",(int)m.Stack,"count",m.Count,"bound",m.Bound)!=1)
                        throw new InvalidOperationException("Housing material receipt was not saved.");
                if(!Near(p,r.Npc) || Clock()>=offer.Until || !offer.Materials.All(m=>Same(p,m)))throw new InvalidOperationException("Housing customer changed.");
                t.Commit();
            }}
            Q.Install(p,offer.Materials.Where(m=>m.After==0).Select(m=>m.Uid),new ConquerItem[0]);
            foreach(var m in offer.Materials.Where(m=>m.After>0)){m.Item.StackSize=(ushort)m.After;m.Item.Mode=Enums.ItemMode.Update;m.Item.Send(p);}
            Q.Install(p,new uint[0],new[]{result});d.Text("Here you are. May your home bring you happiness.");
        }
        internal static bool HandleNpc(GameState p,NpcRequest request)
        {
            if(!enabled || p==null || request==null || !recipes.Any(r=>r.Npc==p.ActiveNpc))return false;
            if(p.ItemSyncRoot==null)return true;
            lock(p.ItemSyncRoot)try {
                if(request.NpcID!=0 && request.NpcID!=p.ActiveNpc || !Near(p,p.ActiveNpc))return true;
                var d=new Npcs(p);int index=request.OptionID-1;
                if(request.OptionID==0){offers.Remove(p);d.Text("Good materials make a good home.");for(int i=0;i<recipes.Length;i++)if(recipes[i].Npc==p.ActiveNpc)d.Option(recipes[i].Words,(byte)(i+1));}
                else if(index>=0 && index<recipes.Length && recipes[index].Npc==p.ActiveNpc)Prepare(p,index,d);
                else if(request.OptionID==40)Confirm(p,d);
                else {offers.Remove(p);return true;}
                d.Option("Farewell.",255);d.Send();
            }catch(Exception ex){Q.Fail(p,"housing-materials",ex);}return true;
        }
    }
}
