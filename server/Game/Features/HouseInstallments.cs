using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Interfaces;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class HouseInstallments
    {
        internal const uint Manager=91017;
        internal const ushort Showroom=3024;
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        private static readonly int[] needed={5,20,40,20,30};
        private static readonly uint[] types={1088000,723087,1072031,720027};
        private static readonly string[] names={"Dragon Balls","Lucky Amulets","Euxenite Ores","Meteor Scrolls","Super Gems"};
        private sealed class Part {internal ConquerItem Item;internal uint Uid,Type;internal ushort Stack;internal bool Bound;internal int Count;internal int After{get{return Math.Max(1,(int)Stack)-Count;}}}
        private sealed class Offer {internal Guid Id;internal int Kind;internal DateTime Until;internal Part[] Parts;}
        private static readonly ConditionalWeakTable<GameState,Offer> offers=new ConditionalWeakTable<GameState,Offer>();
        internal static void Initialize()
        {
            foreach(uint id in types.Concat(Enumerable.Range(0,7).Select(i=>700003u+(uint)i*10)))
                if(!ConquerItemInformation.BaseInformations.ContainsKey(id))throw new InvalidOperationException("House installment catalog missing.");
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_house_installments(
uid bigint PRIMARY KEY REFERENCES entities(uid),paid integer[] NOT NULL DEFAULT ARRAY[0,0,0,0,0],completed boolean NOT NULL DEFAULT false,
CHECK(array_length(paid,1)=5 AND array_lower(paid,1)=1 AND array_position(paid,NULL) IS NULL),
CHECK(paid[1] BETWEEN 0 AND 5 AND paid[2] BETWEEN 0 AND 20 AND paid[3] BETWEEN 0 AND 40 AND paid[4] BETWEEN 0 AND 20 AND paid[5] BETWEEN 0 AND 30),
CHECK(NOT completed OR paid=ARRAY[5,20,40,20,30]));
CREATE TABLE IF NOT EXISTS revora_house_installment_items(
id uuid NOT NULL,uid bigint NOT NULL REFERENCES revora_house_installments(uid),kind integer NOT NULL CHECK(kind BETWEEN 0 AND 4),
item_uid bigint NOT NULL,item_type integer NOT NULL,bound boolean NOT NULL,stack_before integer NOT NULL CHECK(stack_before BETWEEN 0 AND 65535),
amount integer NOT NULL CHECK(amount BETWEEN 1 AND 40 AND amount<=greatest(1,stack_before)),
PRIMARY KEY(id,item_uid),deposited_at timestamptz NOT NULL DEFAULT now());");}
        }
        internal static void PlaceManager()
        {
            using(var sha=SHA256.Create())using(var f=File.OpenRead(Path.Combine(Constants.DMapsPath,"maps","3024.map")))
                if(System.BitConverter.ToString(sha.ComputeHash(f)).Replace("-","").ToLowerInvariant()!="bbb2abb9cf4f023af233c4a3df078de59e3e3505dd5b655b061977011172bae8")
                    throw new InvalidOperationException("House showroom terrain changed.");
            using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,"SELECT count(*) FROM npcs WHERE id=@id OR mapid=@map","id",(long)Manager,"map",(int)Showroom))
                if(Convert.ToInt64(q.ExecuteScalar())!=0)throw new InvalidOperationException("House showroom catalog requires reconciliation.");}
            MapsTable.MapInformation info;Map map;
            if(MapsTable.MapInformations.TryGetValue(Showroom,out info) && (info.Owner!=0 || info.HouseLevel!=0 || info.BaseID!=Showroom))
                throw new InvalidOperationException("House showroom identity conflict.");
            if(Kernel.Maps.Values.Any(m=>m.ID!=Showroom && m.Npcs.ContainsKey(Manager)))throw new InvalidOperationException("House manager identity conflict.");
            MapsTable.MapInformations[Showroom]=new MapsTable.MapInformation{ID=Showroom,BaseID=Showroom,Status=7};
            if(!Kernel.Maps.TryGetValue(Showroom,out map))map=new Map(Showroom,Showroom,"maps\\3024.map",false);
            if(map.ID!=Showroom || map.BaseID!=Showroom || !ReferenceEquals(Kernel.Maps[Showroom],map))throw new InvalidOperationException("House showroom publication failed.");
            INpc actor;
            if(map.Npcs.TryGetValue(Manager,out actor)) {
                if(actor.MapID!=Showroom || actor.X!=101 || actor.Y!=102 || actor.Mesh!=12970 || actor.Type!=Enums.NpcType.Talker)throw new InvalidOperationException("House manager was replaced.");
                return;
            }
            if(!map.Floor[90,70,MapObjectType.Player] || !map.Floor[101,102,MapObjectType.Player]
                || map.Npcs.Values.Any(n=>Math.Abs(n.X-101)<=1 && Math.Abs(n.Y-102)<=1))throw new InvalidOperationException("House showroom placement blocked.");
            map.AddNpc(new NpcSpawn{UID=Manager,MapID=Showroom,X=101,Y=102,Mesh=12970,Type=Enums.NpcType.Talker,Name="SeniorHouseManager"});
        }
        internal static void Visit(GameState p)
        {
            if(!Q.Near(p,30156,1002) || p.Entity.PokerTable!=0)return;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                SkillTable.LockOwner(c,t,p);int level=Housing.Level(c,t,p.Entity.UID);
                if(level<4 || level>5){p.Send("Come back when you own a level-four home.");return;}t.Commit();
            }}
            p.Entity.Teleport(Showroom,Showroom,90,70);Arrival(p,Showroom);
        }
        private static void Arrival(GameState p,ushort map)
        {
            using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,"SELECT mapid FROM entities WHERE uid=@uid","uid",(long)p.Entity.UID))
                if(p.Entity.MapID!=map || Convert.ToInt32(q.ExecuteScalar())!=map)throw new InvalidOperationException("House showroom arrival was not saved.");}
        }
        internal static bool HasDeposits(NpgsqlConnection c,NpgsqlTransaction t,uint uid)
        {
            using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_house_installments WHERE uid=@uid AND NOT completed AND paid<>ARRAY[0,0,0,0,0])","uid",(long)uid))return Equals(q.ExecuteScalar(),true);
        }
        private static int[] Read(NpgsqlConnection c,NpgsqlTransaction t,uint uid,out bool completed)
        {
            using(var q=Q.Command(c,t,"SELECT paid,completed FROM revora_house_installments WHERE uid=@uid FOR UPDATE","uid",(long)uid))
            using(var r=q.ExecuteReader()){if(!r.Read()){completed=false;return new int[5];}completed=r.GetBoolean(1);return (int[])r[0];}
        }
        private static bool Near(GameState p){return Q.Near(p,Manager,Showroom) && p.Map.BaseID==Showroom && p.Entity.PokerTable==0;}
        private static bool Matches(uint id,int kind){return kind<4?id==types[kind]:id>=700003 && id<=700063 && id%10==3;}
        private static bool Usable(ConquerItem i){ConquerItemBaseInformation info;return i.Position==0 && i.Warehouse==0 && i.Lock==0 && !i.Suspicious && !i.InWardrobe && i.Days==0 && i.TimeLeftInMinutes==0
            && ConquerItemInformation.BaseInformations.TryGetValue(i.ID,out info) && i.StackSize<=Math.Max(1,(int)info.StackSize);}
        private static bool Same(GameState p,Part a){ConquerItem i;return p.Inventory.TryGetItem(a.Uid,out i) && ReferenceEquals(i,a.Item) && Usable(i) && i.ID==a.Type && i.StackSize==a.Stack && i.Bound==a.Bound;}
        private static void Prepare(GameState p,Npcs d,int kind,int[] paid)
        {
            offers.Remove(p);int left=needed[kind]-paid[kind];var parts=new List<Part>();
            foreach(var i in p.Inventory.Objects.Where(i=>Usable(i) && Matches(i.ID,kind)).OrderByDescending(i=>i.Bound).ThenBy(i=>i.UID)) {
                if(left==0)break;int count=Math.Min(left,Math.Max(1,(int)i.StackSize));
                parts.Add(new Part{Item=i,Uid=i.UID,Type=i.ID,Stack=i.StackSize,Bound=i.Bound,Count=count});left-=count;
            }
            if(parts.Count==0){d.Text("Bring the materials you still owe.");return;}
            offers.Add(p,new Offer{Id=Guid.NewGuid(),Kind=kind,Until=Clock().AddMinutes(2),Parts=parts.ToArray()});
            d.Text("I will put these "+parts.Sum(i=>i.Count)+" "+names[kind]+" toward your new home.");
            if(parts.Any(i=>i.Bound))d.Text("Your bound materials will be used first.");d.Option("Hand them over.",40);
        }
        private static void Confirm(GameState p,Npcs d)
        {
            Offer offer;if(!offers.TryGetValue(p,out offer)){d.Text("Tell me what you wish to deliver.");return;}offers.Remove(p);
            if(!Near(p) || Clock()>=offer.Until)return;bool built=false;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                SkillTable.LockOwner(c,t,p);
                using(var q=Q.Command(c,t,"SELECT mapid FROM entities WHERE uid=@uid","uid",(long)p.Entity.UID))
                    if(Convert.ToInt32(q.ExecuteScalar())!=Showroom)throw new InvalidOperationException("Installment customer location changed.");
                if(Housing.Level(c,t,p.Entity.UID)!=4){d.Text("Your home is no longer awaiting this work. Your recorded deliveries are safe.");return;}
                Q.Execute(c,t,"INSERT INTO revora_house_installments(uid) VALUES(@uid) ON CONFLICT(uid) DO NOTHING","uid",(long)p.Entity.UID);
                bool completed;int[] paid=Read(c,t,p.Entity.UID,out completed);if(completed)return;
                if(offer.Kind==5) {
                    if(!paid.SequenceEqual(needed)){d.Text("Some building materials are still missing.");return;}
                    var recorded=new int[5];
                    using(var q=Q.Command(c,t,"SELECT kind,sum(amount) FROM revora_house_installment_items WHERE uid=@uid GROUP BY kind","uid",(long)p.Entity.UID))
                    using(var r=q.ExecuteReader())while(r.Read())recorded[r.GetInt32(0)]=checked((int)Convert.ToInt64(r[1]));
                    if(!recorded.SequenceEqual(paid))throw new InvalidOperationException("Installment material history requires reconciliation.");
                    long first;
                    using(var q=Q.Command(c,t,"SELECT min(item_uid) FROM revora_house_installment_items WHERE uid=@uid","uid",(long)p.Entity.UID))first=Convert.ToInt64(q.ExecuteScalar());
                    if(!Housing.CompleteInstallments(c,t,p,d,first))return;
                    if(Q.Execute(c,t,"UPDATE revora_house_installments SET completed=true WHERE uid=@uid AND NOT completed AND paid=ARRAY[5,20,40,20,30]","uid",(long)p.Entity.UID)!=1)
                        throw new InvalidOperationException("House installment completion was not saved.");
                    built=true;
                } else {
                    if(!offer.Parts.All(a=>Same(p,a)))throw new InvalidOperationException("Installment materials changed.");
                    int quantity=offer.Parts.Sum(a=>a.Count);if(quantity>needed[offer.Kind]-paid[offer.Kind]){d.Text("Your deliveries have changed. Let us count them again.");return;}
                    var after=(int[])paid.Clone();after[offer.Kind]+=quantity;
                    foreach(var a in offer.Parts) {
                        string sql=a.After==0?"DELETE FROM items":"UPDATE items SET stacksize=@after";
                        if(Q.Execute(c,t,sql+@" WHERE uid=@item AND entityid=@uid AND id=@type AND stacksize=@stack AND bound=@bound
AND position=0 AND warehouse=0 AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')='0'",
                            "after",a.After,"item",(long)a.Uid,"uid",(long)p.Entity.UID,"type",(int)a.Type,"stack",(int)a.Stack,"bound",a.Bound?1:0)!=1)
                            throw new InvalidOperationException("House installment material debit failed.");
                        if(Q.Execute(c,t,@"INSERT INTO revora_house_installment_items(id,uid,kind,item_uid,item_type,bound,stack_before,amount)
VALUES(@id,@uid,@kind,@item,@type,@bound,@stack,@amount)","id",offer.Id,"uid",(long)p.Entity.UID,"kind",offer.Kind,
                            "item",(long)a.Uid,"type",(int)a.Type,"bound",a.Bound,"stack",(int)a.Stack,"amount",a.Count)!=1)
                            throw new InvalidOperationException("House installment material receipt was not saved.");
                    }
                    if(Q.Execute(c,t,"UPDATE revora_house_installments SET paid=@after WHERE uid=@uid AND NOT completed AND paid=@before",
                        "after",after,"uid",(long)p.Entity.UID,"before",paid)!=1)throw new InvalidOperationException("House installment progress was not saved.");
                    if(!offer.Parts.All(a=>Same(p,a)))throw new InvalidOperationException("Installment materials changed before commit.");
                }
                if(!Near(p) || Clock()>=offer.Until)throw new InvalidOperationException("Installment customer changed.");t.Commit();
            }}
            if(built){Housing.Load(p);Q.Refresh(p);d.Text("The work is finished. Your new home awaits.");}
            else {
                Q.Install(p,offer.Parts.Where(a=>a.After==0).Select(a=>a.Uid),new ConquerItem[0]);
                foreach(var a in offer.Parts.Where(a=>a.After>0)){a.Item.StackSize=(ushort)a.After;a.Item.Mode=Enums.ItemMode.Update;a.Item.Send(p);}
                d.Text("Your delivery is recorded. Bring the rest when you are ready.");
            }
        }
        internal static bool HandleNpc(GameState p,NpcRequest request)
        {
            if(p==null || request==null || p.ActiveNpc!=Manager)return false;if(p.ItemSyncRoot==null)return true;
            lock(p.ItemSyncRoot)try {
                if(request.NpcID!=0 && request.NpcID!=Manager || !Near(p))return true;
                if(request.OptionID>7 && request.OptionID!=40){offers.Remove(p);return true;}
                if(request.OptionID==7){offers.Remove(p);p.Entity.Teleport(1002,345,529);Arrival(p,1002);return true;}
                var d=new Npcs(p);
                if(request.OptionID==40)Confirm(p,d);
                else {
                    bool completed;int[] paid;int level;
                    using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){SkillTable.LockOwner(c,t,p);level=Housing.Level(c,t,p.Entity.UID);paid=Read(c,t,p.Entity.UID,out completed);t.Commit();}}
                    if(level!=4 || completed){offers.Remove(p);d.Text(completed?"Your new home is ready.":"A level-four home is needed before we begin.");}
                    else if(request.OptionID==0){offers.Remove(p);d.Text("A spacious home takes good materials. Here is your account.");
                        for(int i=0;i<5;i++){d.Text(names[i]+": "+paid[i]+"/"+needed[i]+".");if(paid[i]<needed[i])d.Option("Deliver "+names[i]+".",(byte)(i+1));}
                        if(paid.SequenceEqual(needed))d.Option("Begin the work.",6);
                    } else if(request.OptionID>=1 && request.OptionID<=5)Prepare(p,d,request.OptionID-1,paid);
                    else if(request.OptionID==6 && paid.SequenceEqual(needed)){offers.Remove(p);offers.Add(p,new Offer{Kind=5,Until=Clock().AddMinutes(2)});d.Text("All materials are here. Empty your house, and my builders can begin.");d.Option("Begin.",40);}
                    else offers.Remove(p);
                }
                d.Option("Return to Twin City.",7);d.Option("Farewell.",255);d.Send();
            }catch(Exception ex){Q.Fail(p,"house-installments",ex);}return true;
        }
    }
}
