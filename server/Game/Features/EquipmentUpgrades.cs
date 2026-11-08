using System;
using System.Collections.Generic;
using System.Linq;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Revora.Network;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    // Direct and statically verified Compose requests share one owned mutation.
    internal static class EquipmentUpgrades
    {
        internal static void LowerLevel(GameState p,uint uid) {
            if(!Ready(p))return;
            lock(p.ItemSyncRoot)try {
                ConquerItem item;ConquerItemBaseInformation before;
                if(!Ready(p) || !p.Inventory.TryGetItem(uid,out item) || !Usable(item) || item.Position!=0 || item.StackSize!=1
                    || ItemHandler.IsArrow(item.ID) || !ConquerItemInformation.BaseInformations.TryGetValue(item.ID,out before) || before.Level<=15)return;
                int slot=(int)ItemHandler.GetPositionFromID(item.ID);if(!(slot>=1 && slot<=6 || slot==8))return;
                var after=ConquerItemInformation.BaseInformations.Values.Where(i=>i.ID/1000==item.ID/1000 && i.ID%10==item.ID%10 && i.Level>=15)
                    .OrderBy(i=>i.Level).ThenBy(i=>i.ID).FirstOrDefault();
                if(after==null || after.Level>=before.Level || after.ID==item.ID)return;
                uint cp=p.Entity.ConquerPoints,type=item.ID;if(cp<54){p.Send("You do not have 54 CPs.");return;}
                uint progress=checked(item.SocketProgress+5);byte one=(byte)item.SocketOne,two=(byte)item.SocketTwo;
                uint need=one==0 && two==0?Constants.SocketOneProgress:one!=0 && two==0?Constants.SocketTwoProgress:0u;
                if(need!=0 && progress>=need){progress-=need;if(one==0)one=(byte)Enums.Gem.EmptySocket;else two=(byte)Enums.Gem.EmptySocket;}
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    SkillTable.LockOwner(c,t,p);ArtifactStabilization.Own(c,t,p,item);
                    if(Q.Execute(c,t,"UPDATE entities SET conquerpoints=@after WHERE uid=@uid AND conquerpoints=@before",
                        "after",(long)(cp-54),"uid",(long)p.Entity.UID,"before",(long)cp)!=1)throw new InvalidOperationException("Level reduction purse changed.");
                    if(Q.Execute(c,t,"UPDATE items SET id=@type,durability=@dur,maximdurability=@dur,socketone=@one,sockettwo=@two,socketprogress=@progress WHERE uid=@item AND entityid=@uid AND id=@before",
                        "one",(int)one,"two",(int)two,"progress",(long)progress,"type",(int)after.ID,"dur",(int)after.Durability,"item",(long)uid,"uid",(long)p.Entity.UID,"before",(int)type)!=1)
                        throw new InvalidOperationException("Level reduction item changed.");
                    if(!Ready(p) || item.ID!=type || !Usable(item) || item.Position!=0 || p.Entity.ConquerPoints!=cp
                        || !ReferenceEquals(Find(p,uid),item))throw new InvalidOperationException("Level reduction session changed.");
                    t.Commit();
                }}
                p.Entity.ApplyCommittedConquerPoints(cp-54);item.ID=after.ID;item.Durability=item.MaximDurability=after.Durability;
                item.SocketOne=(Enums.Gem)one;item.SocketTwo=(Enums.Gem)two;item.SocketProgress=progress;
                item.Mode=Enums.ItemMode.Update;item.Send(p);item.Mode=Enums.ItemMode.Default;Q.Refresh(p);
            }catch(Exception ex){Q.Fail(p,"equipment-level-reduction",ex);}
        }
        internal enum Operation { MeteorLevel=1, DragonLevel=2, DragonQuality=3 }
        internal enum Interface { Direct=1, Compose=2 }
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        internal static Func<int,int> Roll=max=>Kernel.Random.Next(max);
        internal static Action BeforeProjection;
        internal static Action BeforeRepairProjection;
        internal static Action BeforePlusProjection;
        internal static Action BeforeBlessProjection;
        internal static void InitializePlus() {
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_plus_compositions(
id uuid PRIMARY KEY,uid bigint NOT NULL,target_uid bigint NOT NULL,materials bigint[] NOT NULL,
before_plus integer NOT NULL,after_plus integer NOT NULL CHECK(after_plus BETWEEN 0 AND 12),
before_progress bigint NOT NULL,after_progress bigint NOT NULL CHECK(after_progress>=0),created_at timestamptz NOT NULL);
CREATE TABLE IF NOT EXISTS revora_plus_composition_targets(
receipt uuid PRIMARY KEY REFERENCES revora_plus_compositions(id),item_type integer NOT NULL CHECK(item_type>0));
CREATE INDEX IF NOT EXISTS revora_plus_recent ON revora_plus_compositions(uid,target_uid,created_at DESC);");}
        }
        private static void InitializeBless() {
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_equipment_blessings(
id uuid PRIMARY KEY,uid bigint NOT NULL,target_uid bigint NOT NULL,item_type integer NOT NULL,
materials bigint[] NOT NULL,stacks integer[] NOT NULL,bound boolean NOT NULL,
before_bless integer NOT NULL CHECK(before_bless IN(0,1,3,5)),after_bless integer NOT NULL,
created_at timestamptz NOT NULL,UNIQUE(uid,target_uid,before_bless),UNIQUE(uid,target_uid,materials),
CHECK(cardinality(materials)=cardinality(stacks)),
CHECK((before_bless=0 AND after_bless=1 AND cardinality(materials)=5)
 OR(before_bless=1 AND after_bless=3 AND cardinality(materials)=1)
 OR(before_bless=3 AND after_bless=5 AND cardinality(materials)=3)
 OR(before_bless=5 AND after_bless=7 AND cardinality(materials)=5)));");}
        }
        internal static bool ParseBless(byte[] frame,out uint target,out uint[] selected) {
            target=0;selected=null;
            if(frame==null || frame.Length<103 || frame.Length>123 || BitConverter.ToUInt16(frame,2)!=1009
                || BitConverter.ToUInt16(frame,20)!=40)return false;
            uint count=BitConverter.ToUInt32(frame,26);if(count!=1 && count!=3 && count!=5)return false;
            int length=91+4*(int)count;
            if(frame.Length!=length+8 && frame.Length!=length+12)return false;
            if(BitConverter.ToUInt16(frame,0)!=frame.Length-8 || BitConverter.ToUInt64(frame,frame.Length-8)!=PacketHandler.ClientSeal)return false;
            target=BitConverter.ToUInt32(frame,8);if(target==0)return false;
            selected=new uint[count];
            for(int n=0;n<selected.Length;n++) {
                uint id=BitConverter.ToUInt32(frame,91+4*n);
                if(id==0 || id==target || selected.Take(n).Contains(id))return false;selected[n]=id;
            }
            Array.Sort(selected);return true;
        }
        private static bool BlessTarget(GameState p,ConquerItem item) {
            if(!Usable(item) || item.StackSize!=1 || ItemHandler.IsArrow(item.ID)
                || !ConquerItemInformation.BaseInformations.ContainsKey(item.ID))return false;
            int slot=(int)ItemHandler.GetPositionFromID(item.ID);
            if(!(slot>=1 && slot<=6 || slot==8))return false;
            if(item.Position==0)return p.Inventory.ContainsUID(item.UID);
            int actual=item.Position>=21 && item.Position<=28?item.Position-20:item.Position;
            return actual==slot || actual==5 && slot==4 && !ItemHandler.IsTwoHand(item.ID) && item.ID/1000!=421;
        }
        internal static void Bless(GameState p,byte[] frame) {
            uint uid;uint[] selected;if(!Ready(p) || !ParseBless(frame,out uid,out selected))return;
            lock(p.ItemSyncRoot)try {
                var item=Find(p,uid);if(!Ready(p) || !BlessTarget(p,item))return;
                byte before=item.Bless,after,position=item.Position;int needed;
                switch(before){case 0:needed=5;after=1;break;case 1:needed=1;after=3;break;
                    case 3:needed=3;after=5;break;case 5:needed=5;after=7;break;default:return;}
                if(selected.Length!=needed)return;
                bool bound=item.Bound;uint type=item.ID;var materials=new ConquerItem[needed];
                for(int n=0;n<needed;n++)if(!p.Inventory.TryGetItem(selected[n],out materials[n]) || !Usable(materials[n])
                    || materials[n].Position!=0 || materials[n].ID!=700073 || materials[n].Bound && !bound)return;
                var stacks=materials.Select(m=>(int)m.StackSize).ToArray();var bindings=materials.Select(m=>m.Bound).ToArray();
                var identities=selected.Select(id=>(long)id).ToArray();DateTime now=Clock();
                if(now.Kind!=DateTimeKind.Utc)throw new InvalidOperationException("Blessing clock must be UTC.");
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    SkillTable.LockOwner(c,t,p);
                    using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_equipment_blessings WHERE uid=@uid AND target_uid=@item AND materials=@materials)",
                        "uid",(long)p.Entity.UID,"item",(long)uid,"materials",identities))if(Equals(q.ExecuteScalar(),true))return;
                    foreach(var owned in materials.Concat(new[]{item}).OrderBy(i=>i.UID))ArtifactStabilization.Own(c,t,p,owned);
                    if(Q.Execute(c,t,"UPDATE items SET bless=@after WHERE uid=@item AND entityid=@uid AND bless=@before",
                        "after",(int)after,"item",(long)uid,"uid",(long)p.Entity.UID,"before",(int)before)!=1)
                        throw new InvalidOperationException("Blessing preimage changed.");
                    for(int n=0;n<needed;n++) {
                        string sql=stacks[n]==1?"DELETE FROM items WHERE uid=@item AND entityid=@uid AND stacksize=@stack"
                            :"UPDATE items SET stacksize=stacksize-1 WHERE uid=@item AND entityid=@uid AND stacksize=@stack";
                        if(Q.Execute(c,t,sql,"item",(long)selected[n],"uid",(long)p.Entity.UID,"stack",stacks[n])!=1)
                            throw new InvalidOperationException("Blessing payment was not consumed.");
                    }
                    if(Q.Execute(c,t,@"INSERT INTO revora_equipment_blessings(id,uid,target_uid,item_type,materials,stacks,bound,before_bless,after_bless,created_at)
VALUES(@id,@uid,@item,@type,@materials,@stacks,@bound,@before,@after,@now)","id",Guid.NewGuid(),"uid",(long)p.Entity.UID,
                        "item",(long)uid,"type",(int)type,"materials",identities,"stacks",stacks,"bound",bound,"before",(int)before,"after",(int)after,"now",now)!=1)
                        throw new InvalidOperationException("Blessing receipt was not saved.");
                    EquipmentAchievements.Record(c,t,p.Entity.UID,now);
                    if(!Ready(p) || !BlessTarget(p,item) || !ReferenceEquals(Find(p,uid),item) || item.ID!=type
                        || item.Position!=position || item.Bless!=before || item.Bound!=bound)throw new InvalidOperationException("Blessing context changed.");
                    for(int n=0;n<needed;n++)if(!Usable(materials[n]) || materials[n].ID!=700073 || materials[n].Position!=0
                        || materials[n].StackSize!=stacks[n] || materials[n].Bound!=bindings[n] || !ReferenceEquals(Find(p,selected[n]),materials[n]))
                        throw new InvalidOperationException("Blessing material context changed.");
                    t.Commit();
                }}
                if(BeforeBlessProjection!=null)BeforeBlessProjection();
                Q.Install(p,materials.Where((m,n)=>stacks[n]==1).Select(m=>m.UID),new ConquerItem[0]);
                for(int n=0;n<needed;n++)if(stacks[n]>1){materials[n].StackSize--;materials[n].Mode=Enums.ItemMode.Update;materials[n].Send(p);}
                item.Bless=after;item.Mode=Enums.ItemMode.Update;item.Send(p);Q.Refresh(p);
                if(position!=0){p.LoadItemStats();p.Equipment.UpdateEntityPacket();}
                var response=(byte[])frame.Clone();Writer.WriteUInt32(1,12,response);p.Send(response);
            }catch(Exception ex){Q.Fail(p,"equipment-blessing",ex);}
        }
        private static bool PlusItem(ConquerItem i) {
            if(!Usable(i) || !ConquerItemInformation.BaseInformations.ContainsKey(i.ID))return false;
            int slot=(int)ItemHandler.GetPositionFromID(i.ID);
            return !ItemHandler.IsArrow(i.ID) && (slot>=1 && slot<=6 || slot==8 || slot==10 || slot==11 || slot==18 || slot==19
                || i.ID>=730001 && i.ID<=730008);
        }
        private static void PlusSaved(NpgsqlConnection c,NpgsqlTransaction t,GameState p,ConquerItem i) {
            ArtifactStabilization.Own(c,t,p,i);
            using(var q=Q.Command(c,t,"SELECT plus,plusprogress FROM items WHERE uid=@uid","uid",(long)i.UID))
            using(var r=q.ExecuteReader())if(!r.Read() || Convert.ToInt32(r[0])!=i.Plus || Convert.ToUInt32(r[1])!=i.PlusProgress)
                throw new InvalidOperationException("Composition progress preimage changed.");
        }
        private static bool HandlePlus(GameState p,byte[] frame) {
            if(frame.Length<24 || frame.Length>180 || !Ready(p))return true;
            int count=frame[5],length=8+4*count;
            if(count<2 || count>41 || frame.Length!=length+8 || BitConverter.ToUInt16(frame,0)!=length
                || BitConverter.ToUInt16(frame,2)!=2036 || BitConverter.ToUInt64(frame,length)!=PacketHandler.ClientSeal)return true;
            var ids=Enumerable.Range(0,count).Select(n=>BitConverter.ToUInt32(frame,8+4*n)).ToArray();
            if(ids.Any(id=>id==0) || ids.Distinct().Count()!=count)return true;
            lock(p.ItemSyncRoot)try{
                if(!Ready(p))return true;var target=Find(p,ids[0]);
                if(!PlusItem(target) || target.StackSize!=1 || target.Plus>=12 || target.PlusProgress>=DataHolder.ComposePlusPoints(target.Plus))return true;
                var materials=new List<ConquerItem>();byte plus=target.Plus;uint progress=target.PlusProgress;
                foreach(uint id in ids.Skip(1)){
                    // Preserve unused materials once the main reaches its cap.
                    if(plus==12)break;
                    ConquerItem m;if(!p.Inventory.TryGetItem(id,out m) || !PlusItem(m) || m.Position!=0 || m.Plus>12
                        || m.Bound && !target.Bound)return true;
                    materials.Add(m);progress=checked(progress+DataHolder.StonePlusPoints(m.Plus));
                    while(plus<12 && progress>=DataHolder.ComposePlusPoints(plus)){progress-=DataHolder.ComposePlusPoints(plus);plus++;}
                    if(plus==12)progress=0;
                }
                var stacks=materials.Select(m=>m.StackSize).ToArray();byte before=target.Plus;uint oldProgress=target.PlusProgress;
                DateTime now=Clock();if(now.Kind!=DateTimeKind.Utc)throw new InvalidOperationException("Composition clock must be UTC.");
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    SkillTable.LockOwner(c,t,p);
                    using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_plus_compositions WHERE uid=@uid AND target_uid=@target AND materials=@materials AND created_at>@now-interval '1 second')",
                        "uid",(long)p.Entity.UID,"target",(long)target.UID,"materials",ids.Skip(1).Select(id=>(long)id).ToArray(),"now",now))
                        if(Equals(q.ExecuteScalar(),true))return true;
                    foreach(var item in materials.Concat(new[]{target}).OrderBy(i=>i.UID))PlusSaved(c,t,p,item);
                    if(Q.Execute(c,t,"UPDATE items SET plus=@plus,plusprogress=@progress WHERE uid=@uid AND entityid=@owner AND plus=@before AND plusprogress=@old",
                        "plus",(int)plus,"progress",(long)progress,"uid",(long)target.UID,"owner",(long)p.Entity.UID,"before",(int)before,"old",(long)oldProgress)!=1)
                        throw new InvalidOperationException("Composition result was not saved.");
                    for(int n=0;n<materials.Count;n++){
                        string sql=stacks[n]==1?"DELETE FROM items":"UPDATE items SET stacksize=stacksize-1";
                        if(Q.Execute(c,t,sql+" WHERE uid=@uid AND entityid=@owner AND stacksize=@stack","uid",(long)materials[n].UID,"owner",(long)p.Entity.UID,"stack",(int)stacks[n])!=1)
                            throw new InvalidOperationException("Composition material was not consumed.");
                    }
                    Guid receipt=Guid.NewGuid();
                    if(Q.Execute(c,t,@"INSERT INTO revora_plus_compositions(id,uid,target_uid,materials,before_plus,after_plus,before_progress,after_progress,created_at)
VALUES(@id,@uid,@target,@materials,@before,@after,@old,@progress,@now)","id",receipt,"uid",(long)p.Entity.UID,"target",(long)target.UID,
                        "materials",ids.Skip(1).Select(id=>(long)id).ToArray(),"before",(int)before,"after",(int)plus,"old",(long)oldProgress,"progress",(long)progress,"now",now)!=1)
                        throw new InvalidOperationException("Composition receipt was not saved.");
                    if(Q.Execute(c,t,"INSERT INTO revora_plus_composition_targets(receipt,item_type) VALUES(@receipt,@type)",
                        "receipt",receipt,"type",(int)target.ID)!=1)throw new InvalidOperationException("Composition target evidence was not saved.");
                    if(!Ready(p) || !ReferenceEquals(Find(p,target.UID),target))throw new InvalidOperationException("Composition session changed.");
                    t.Commit();
                }}
                if(BeforePlusProjection!=null)BeforePlusProjection();
                Q.Install(p,materials.Where((m,n)=>stacks[n]==1).Select(m=>m.UID),new ConquerItem[0]);
                for(int n=0;n<materials.Count;n++)if(stacks[n]>1){materials[n].StackSize--;materials[n].Mode=Enums.ItemMode.Update;materials[n].Send(p);}
                target.Plus=plus;target.PlusProgress=progress;target.Mode=Enums.ItemMode.Update;target.Send(p);Q.Refresh(p);
                if(target.Position!=0){p.LoadItemStats();p.Equipment.UpdateEntityPacket();}
            }catch(Exception ex){Q.Fail(p,"plus-composition",ex);}
            return true;
        }
        internal static void Initialize() {
            InitializeBless();
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_equipment_upgrades(
id uuid PRIMARY KEY,uid bigint NOT NULL,target_uid bigint NOT NULL,operation integer NOT NULL CHECK(operation BETWEEN 1 AND 3),
interface integer NOT NULL CHECK(interface BETWEEN 1 AND 2),materials bigint[] NOT NULL,
before_type integer NOT NULL,after_type integer NOT NULL,success boolean NOT NULL,
before_durability integer NOT NULL,after_durability integer NOT NULL,before_max integer NOT NULL,after_max integer NOT NULL,
before_socket_one integer NOT NULL,after_socket_one integer NOT NULL,before_socket_two integer NOT NULL,after_socket_two integer NOT NULL,
before_progress bigint NOT NULL,after_progress bigint NOT NULL,created_at timestamptz NOT NULL);
CREATE INDEX IF NOT EXISTS revora_upgrade_recent ON revora_equipment_upgrades(uid,target_uid,created_at DESC);
CREATE TABLE IF NOT EXISTS revora_equipment_upgrade_materials(
receipt uuid NOT NULL REFERENCES revora_equipment_upgrades(id),item_uid bigint NOT NULL,item_type integer NOT NULL,
before_stack integer NOT NULL CHECK(before_stack>0),after_stack integer NOT NULL CHECK(after_stack=before_stack-1),
PRIMARY KEY(receipt,item_uid));
CREATE TABLE IF NOT EXISTS revora_equipment_repairs(
id uuid PRIMARY KEY,uid bigint NOT NULL,target_uid bigint NOT NULL,item_type integer NOT NULL,
before_durability integer NOT NULL CHECK(before_durability>=0),after_durability integer NOT NULL CHECK(after_durability>before_durability),
silver_before numeric(20,0) NOT NULL,silver_after numeric(20,0) NOT NULL CHECK(silver_after>=0),cost numeric(20,0) NOT NULL CHECK(cost>=0),
materials bigint[] NOT NULL,stacks integer[] NOT NULL,consumed integer[] NOT NULL,created_at timestamptz NOT NULL,
CHECK(silver_before-silver_after=cost),CHECK(cardinality(materials)=cardinality(stacks)),CHECK(cardinality(materials)=cardinality(consumed)));");}
        }
        internal static void Repair(GameState p,uint uid) {
            if(!Ready(p))return;
            lock(p.ItemSyncRoot)try {
                ConquerItem item;if(!Ready(p) || !p.Inventory.TryGetItem(uid,out item) || !Usable(item) || item.Position!=0 || item.StackSize!=1
                    || item.MaximDurability==0 || item.Durability>=item.MaximDurability || ItemHandler.IsArrow(item.ID)
                    || !ConquerItemInformation.BaseInformations.ContainsKey(item.ID))return;
                int slot=(int)ItemHandler.GetPositionFromID(item.ID);if(!(slot>=1 && slot<=6 || slot==8 || slot==10 || slot==11 || slot==18))return;
                var before=new State(item);bool bound=item.Bound;ulong money=p.Entity.Money,cost=0;
                var materials=new List<ConquerItem>();var counts=new List<int>();
                if(before.Durability==0) {
                    int need=5;
                    foreach(var m in p.Inventory.Objects.OrderBy(i=>i.UID)) {
                        if(!Usable(m) || m.Position!=0 || m.ID!=1088001 || m.Bound && !bound)continue;
                        int use=Math.Min(need,(int)m.StackSize);materials.Add(m);counts.Add(use);need-=use;if(need==0)break;
                    }
                    if(need!=0)return;
                }else {
                    uint quality=item.ID%10;decimal multiplier=quality==9?1.125m:quality==8?0.975m:quality==7?0.9m:quality==6?0.825m:0.75m;
                    decimal proportional=decimal.Floor((decimal)ConquerItemInformation.BaseInformations[item.ID].GoldWorth*(before.Maximum-before.Durability)/before.Maximum);
                    cost=checked((ulong)Math.Max(1m,decimal.Ceiling(proportional*multiplier)));if(money<cost)return;
                }
                var stacks=materials.Select(m=>(int)m.StackSize).ToArray();ulong after=money-cost;DateTime now=Clock();
                if(now.Kind!=DateTimeKind.Utc)throw new InvalidOperationException("Repair clock must be UTC.");
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    SkillTable.LockOwner(c,t,p);
                    foreach(var owned in materials.Concat(new[]{item}).OrderBy(i=>i.UID))ArtifactStabilization.Own(c,t,p,owned);
                    Saved(c,t,p,item,before);
                    using(var q=Q.Command(c,t,"SELECT money FROM entities WHERE uid=@uid","uid",(long)p.Entity.UID))
                        if(Convert.ToDecimal(q.ExecuteScalar())!=(decimal)money)throw new InvalidOperationException("Repair purse preimage changed.");
                    if(cost>0 && Q.Execute(c,t,"UPDATE entities SET money=@after WHERE uid=@uid AND money=@before","after",(decimal)after,"uid",(long)p.Entity.UID,"before",(decimal)money)!=1)
                        throw new InvalidOperationException("Repair charge was not saved.");
                    if(Q.Execute(c,t,"UPDATE items SET durability=@after WHERE uid=@item AND entityid=@uid AND durability=@before AND maximdurability=@after",
                        "after",(int)before.Maximum,"item",(long)uid,"uid",(long)p.Entity.UID,"before",(int)before.Durability)!=1)throw new InvalidOperationException("Repair result was not saved.");
                    for(int n=0;n<materials.Count;n++) {
                        string consume=stacks[n]==counts[n]?"DELETE FROM items":"UPDATE items SET stacksize=stacksize-@used";
                        if(Q.Execute(c,t,consume+" WHERE uid=@item AND entityid=@uid AND stacksize=@before","used",counts[n],"item",(long)materials[n].UID,
                            "uid",(long)p.Entity.UID,"before",stacks[n])!=1)throw new InvalidOperationException("Broken repair material was not consumed.");
                    }
                    if(Q.Execute(c,t,@"INSERT INTO revora_equipment_repairs(id,uid,target_uid,item_type,before_durability,after_durability,silver_before,silver_after,cost,materials,stacks,consumed,created_at)
VALUES(@id,@uid,@item,@type,@before,@after,@money,@balance,@cost,@materials,@stacks,@consumed,@now)","id",Guid.NewGuid(),"uid",(long)p.Entity.UID,"item",(long)uid,
                        "type",(int)item.ID,"before",(int)before.Durability,"after",(int)before.Maximum,"money",(decimal)money,"balance",(decimal)after,"cost",(decimal)cost,
                        "materials",materials.Select(m=>(long)m.UID).ToArray(),"stacks",stacks,"consumed",counts.ToArray(),"now",now)!=1)throw new InvalidOperationException("Repair receipt was not saved.");
                    if(!Ready(p) || !Usable(item) || !before.Matches(item) || item.Position!=0 || item.Bound!=bound || !ReferenceEquals(Find(p,uid),item) || p.Entity.Money!=money)
                        throw new InvalidOperationException("Repair context changed.");
                    for(int n=0;n<materials.Count;n++)if(!Usable(materials[n]) || materials[n].Position!=0 || materials[n].ID!=1088001 || materials[n].Bound && !bound
                        || materials[n].StackSize!=stacks[n] || !ReferenceEquals(Find(p,materials[n].UID),materials[n]))throw new InvalidOperationException("Repair payment context changed.");
                    t.Commit();
                }}
                if(BeforeRepairProjection!=null)BeforeRepairProjection();
                SilverBalance.ApplyCommitted(p,after);
                Q.Install(p,materials.Where((m,n)=>stacks[n]==counts[n]).Select(m=>m.UID),new ConquerItem[0]);
                for(int n=0;n<materials.Count;n++)if(stacks[n]>counts[n]){materials[n].StackSize=(ushort)(stacks[n]-counts[n]);materials[n].Mode=Enums.ItemMode.Update;materials[n].Send(p);}
                item.Durability=before.Maximum;item.Mode=Enums.ItemMode.Update;item.Send(p);
            }catch(Exception ex){Q.Fail(p,"equipment-repair",ex);}
        }
        internal sealed class ComposeRequest {
            internal byte Mode;internal uint Target,Value;internal uint[] Materials;
        }
        internal static bool TryComposeRequest(byte[] frame,ulong seal,out ComposeRequest request) {
            request=null;
            if(frame==null || frame.Length<28 || frame.Length>184 || (frame[4]!=6 && frame[4]!=7))return false;
            int count=frame[5]-2, length=16+4*count;
            if(count<1 || count>40 || frame.Length!=length+8 || BitConverter.ToUInt16(frame,0)!=length
                || BitConverter.ToUInt16(frame,2)!=2036 || BitConverter.ToUInt64(frame,length)!=seal)return false;
            uint target=BitConverter.ToUInt32(frame,8),value=BitConverter.ToUInt32(frame,12);
            if(target==0 || value<1 || value>400)return false;
            var materials=new uint[count];
            for(int n=0;n<count;n++){uint uid=BitConverter.ToUInt32(frame,16+n*4);if(uid==0 || uid==target || materials.Take(n).Contains(uid))return false;materials[n]=uid;}
            request=new ComposeRequest{Mode=frame[4],Target=target,Value=value,Materials=materials};return true;
        }
        private static int MaterialValue(uint type,Operation op,Interface route) {
            if(op==Operation.MeteorLevel)return type==1088001?1:route==Interface.Compose?(type==720027?10:type==723268?5:0):0;
            return type==1088000?1:route==Interface.Compose && type==720028?10:0;
        }
        // Intercept both upgrade modes even on invalid input; never fall back to legacy mutation.
        internal static bool HandleCompose(GameState p,byte[] frame) {
            if(frame==null || frame.Length<5)return true;
            if(frame[4]==0)return HandlePlus(p,frame);
            if(frame[4]!=6 && frame[4]!=7)return false;
            if(!Ready(p))return true;
            lock(p.ItemSyncRoot)try {
                ComposeRequest request;if(!Ready(p) || !TryComposeRequest(frame,PacketHandler.ClientSeal,out request))return true;
                ConquerItem first;if(!p.Inventory.TryGetItem(request.Materials[0],out first))return true;
                Operation op=request.Mode==7?Operation.DragonQuality:first.ID==1088000 || first.ID==720028?Operation.DragonLevel:Operation.MeteorLevel;
                int total=0;foreach(uint uid in request.Materials){ConquerItem m;if(!p.Inventory.TryGetItem(uid,out m))return true;int units=MaterialValue(m.ID,op,Interface.Compose);if(units==0)return true;total+=units;}
                if(total!=request.Value)return true;
                var target=Find(p,request.Target);if(target==null)return true;uint before=target.ID;
                if(Apply(p,request.Target,request.Materials,op,Interface.Compose))p.Send(new ItemUsage(true){UID=target.UID,dwParam=target.ID!=before?1u:2u,
                    Action=request.Mode==6?ItemUsage.Mode.MeteorUpgrade:ItemUsage.Mode.DragonBallUpgrade});
            }catch(Exception ex){Q.Fail(p,"compose-upgrade",ex);}
            return true;
        }
        private static bool Ready(GameState p){return Q.Player(p) && p.Trade!=null && !p.Trade.InTrade && p.Booth==null && p.Entity.PokerTable==0;}
        private static bool Usable(ConquerItem i){return ArtifactStabilization.Usable(i) && i.Lock==0 && i.TimeLeftInMinutes==0;}
        private static ConquerItem Find(GameState p,uint uid){ConquerItem i;return p.Inventory.TryGetItem(uid,out i)?i:p.Equipment==null?null:p.Equipment.TryGetItem(uid);}
        private sealed class State {
            internal uint Type,Progress;internal ushort Durability,Maximum;internal byte One,Two;
            internal State(ConquerItem i){Type=i.ID;Progress=i.SocketProgress;Durability=i.Durability;Maximum=i.MaximDurability;One=(byte)i.SocketOne;Two=(byte)i.SocketTwo;}
            internal bool Matches(ConquerItem i){return Type==i.ID && Progress==i.SocketProgress && Durability==i.Durability && Maximum==i.MaximDurability && One==(byte)i.SocketOne && Two==(byte)i.SocketTwo;}
            internal void Install(ConquerItem i){i.ID=Type;i.SocketProgress=Progress;i.Durability=Durability;i.MaximDurability=Maximum;i.SocketOne=(Enums.Gem)One;i.SocketTwo=(Enums.Gem)Two;}
        }
        private static bool Socket(byte value){return value==0 || Enum.IsDefined(typeof(Enums.Gem),value);}
        private static bool Target(GameState p,ConquerItem i,Interface route) {
            if(!Usable(i) || i.StackSize!=1 || !ConquerItemInformation.BaseInformations.ContainsKey(i.ID) || ItemHandler.IsArrow(i.ID)
                || i.MaximDurability==0 || i.Durability!=i.MaximDurability || !Socket((byte)i.SocketOne) || !Socket((byte)i.SocketTwo)
                || i.SocketOne==Enums.Gem.NoSocket && i.SocketTwo!=Enums.Gem.NoSocket)return false;
            int slot=(int)ItemHandler.GetPositionFromID(i.ID);
            if(!(slot>=1 && slot<=6 || slot==8 || slot==10 || slot==11 || slot==18))return false;
            if(i.Position==0)return p.Inventory.ContainsUID(i.UID);
            if(route!=Interface.Compose)return false;
            int actual=i.Position>=21 && i.Position<=38?i.Position-20:i.Position;
            return actual==slot || actual==5 && slot==4 && !ItemHandler.IsTwoHand(i.ID) && i.ID/1000!=421;
        }
        private static int MeteorCost(ConquerItemBaseInformation info) {
            uint quality=info.ID%10;
            return quality<8?2:info.Level<70?3:quality==8?5:info.Level/4;
        }
        private static bool Draw(int chance,int range){int draw=Roll(range);if(draw<0 || draw>=range)throw new InvalidOperationException("Upgrade roll outside its range.");return draw<chance;}
        private static bool Plan(ConquerItem item,Operation op,Interface route,int materials,out State after,out bool success) {
            after=new State(item);success=false;var info=ConquerItemInformation.BaseInformations[item.ID];
            uint next;int chance;
            if(op==Operation.DragonQuality) {
                uint quality=item.ID%10;if(quality>=9)return false;
                next=quality<6?item.ID-quality+6:item.ID+1;
                if(!ConquerItemInformation.BaseInformations.ContainsKey(next))return false;
                if(route==Interface.Compose)success=materials>=info.Level/30+(int)quality/2 || Draw(44,99);
                else {chance=70-(info.Level-(info.Level>100?30:0))/(10-(int)quality);success=Draw(Math.Max(0,Math.Min(100,chance)),100);}
                if(success)after.Type=next;else if(route==Interface.Direct)after.Durability=(ushort)(after.Durability/2);
                if(route==Interface.Direct) {
                    after.Progress=checked(after.Progress+10);
                    uint need=after.One==0 && after.Two==0?Constants.SocketOneProgress:after.One!=0 && after.Two==0?Constants.SocketTwoProgress:0u;
                    if(need>0 && after.Progress>=need){after.Progress-=need;if(after.One==0)after.One=(byte)Enums.Gem.EmptySocket;else after.Two=(byte)Enums.Gem.EmptySocket;}
                }
            } else {
                int maximum=ItemHandler.ItemMaxLevel(ItemHandler.ItemPosition(item.ID));
                if(maximum==0 || info.Level>=maximum)return false;
                next=new ConquerItemInformation(item.ID,item.Plus).CalculateUplevel();
                if(next==0 || next==item.ID || !ConquerItemInformation.BaseInformations.ContainsKey(next))return false;
                var output=ConquerItemInformation.BaseInformations[next];
                if(output.Level<=info.Level || output.Level>maximum || next/1000!=item.ID/1000 || next%10!=item.ID%10
                   )return false;
                if(op==Operation.DragonLevel)success=true;
                else if(route==Interface.Compose)success=materials>=MeteorCost(info) || Draw(39,99);
                else {chance=70-info.Level/10*3-((int)(item.ID%10)+1)*3;success=Draw(Math.Max(0,Math.Min(100,chance)),100);}
                if(success){after.Type=next;if(route==Interface.Direct)after.Durability=after.Maximum=output.Durability;}
                else if(route==Interface.Direct)after.Durability=(ushort)(after.Durability/2);
            }
            return true;
        }
        private static void Saved(NpgsqlConnection c,NpgsqlTransaction t,GameState p,ConquerItem i,State state) {
            ArtifactStabilization.Own(c,t,p,i);
            using(var q=Q.Command(c,t,@"SELECT uid FROM items WHERE uid=@item AND entityid=@uid AND durability=@dur AND maximdurability=@max
AND socketone=@one AND sockettwo=@two AND socketprogress=@progress", "item",(long)i.UID,"uid",(long)p.Entity.UID,
                "dur",(int)state.Durability,"max",(int)state.Maximum,"one",(int)state.One,"two",(int)state.Two,"progress",(long)state.Progress))
                if(q.ExecuteScalar()==null)throw new InvalidOperationException("Upgrade target preimage changed.");
        }
        internal static bool Apply(GameState p,uint target,uint[] selected,Operation op,Interface route) {
            if(selected!=null)selected=selected.ToArray();
            if(!Ready(p) || selected==null || selected.Length<1 || selected.Length>40 || selected.Any(x=>x==0 || x==target)
                || selected.Distinct().Count()!=selected.Length || !Enum.IsDefined(typeof(Operation),op) || !Enum.IsDefined(typeof(Interface),route)
                || route==Interface.Direct && (selected.Length!=1 || op==Operation.DragonLevel))return false;
            lock(p.ItemSyncRoot)try {
                var item=Find(p,target);if(!Ready(p) || !Target(p,item,route))return false;
                int units=0;
                var materials=new List<ConquerItem>();
                foreach(uint id in selected){ConquerItem m;if(!p.Inventory.TryGetItem(id,out m) || !Usable(m) || m.Position!=0 || MaterialValue(m.ID,op,route)==0 || m.Bound && !item.Bound)return false;materials.Add(m);units+=MaterialValue(m.ID,op,route);}
                var before=new State(item);var stacks=materials.Select(m=>m.StackSize).ToArray();var types=materials.Select(m=>m.ID).ToArray();byte position=item.Position;bool bound=item.Bound;
                var now=Clock();if(now.Kind!=DateTimeKind.Utc)throw new InvalidOperationException("Upgrade clock must be UTC.");
                State after;bool success;Guid receipt=Guid.NewGuid();
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    SkillTable.LockOwner(c,t,p);
                    using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_equipment_upgrades WHERE uid=@uid AND target_uid=@target AND created_at>@now-interval '1 second')",
                        "uid",(long)p.Entity.UID,"target",(long)target,"now",now))if(Equals(q.ExecuteScalar(),true))return false;
                    foreach(var owned in materials.Concat(new[]{item}).OrderBy(i=>i.UID))ArtifactStabilization.Own(c,t,p,owned);
                    Saved(c,t,p,item,before);
                    if(!Plan(item,op,route,units,out after,out success))return false;
                    if(position!=0 && ConquerItemInformation.BaseInformations[after.Type].Level>p.Entity.Level)return false;
                    if(Q.Execute(c,t,@"UPDATE items SET id=@type,durability=@dur,maximdurability=@max,socketone=@one,sockettwo=@two,socketprogress=@progress
WHERE uid=@item AND entityid=@uid","type",(int)after.Type,"dur",(int)after.Durability,"max",(int)after.Maximum,"one",(int)after.One,"two",(int)after.Two,
                        "progress",(long)after.Progress,"item",(long)target,"uid",(long)p.Entity.UID)!=1)throw new InvalidOperationException("Upgrade result was not saved.");
                    for(int n=0;n<materials.Count;n++) {
                        string sql=stacks[n]==1?"DELETE FROM items":"UPDATE items SET stacksize=stacksize-1";
                        if(Q.Execute(c,t,sql+" WHERE uid=@item AND entityid=@uid AND stacksize=@stack","item",(long)materials[n].UID,"uid",(long)p.Entity.UID,"stack",(int)stacks[n])!=1)
                            throw new InvalidOperationException("Upgrade material was not consumed.");
                    }
                    if(Q.Execute(c,t,@"INSERT INTO revora_equipment_upgrades(id,uid,target_uid,operation,interface,materials,before_type,after_type,success,
before_durability,after_durability,before_max,after_max,before_socket_one,after_socket_one,before_socket_two,after_socket_two,before_progress,after_progress,created_at)
VALUES(@id,@uid,@target,@op,@route,@materials,@bt,@at,@success,@bd,@ad,@bm,@am,@bo,@ao,@bs,@aso,@bp,@ap,@now)",
                        "id",receipt,"uid",(long)p.Entity.UID,"target",(long)target,"op",(int)op,"route",(int)route,"materials",selected.Select(x=>(long)x).OrderBy(x=>x).ToArray(),
                        "bt",(int)before.Type,"at",(int)after.Type,"success",success,"bd",(int)before.Durability,"ad",(int)after.Durability,"bm",(int)before.Maximum,"am",(int)after.Maximum,
                        "bo",(int)before.One,"ao",(int)after.One,"bs",(int)before.Two,"aso",(int)after.Two,"bp",(long)before.Progress,"ap",(long)after.Progress,"now",now)!=1)
                        throw new InvalidOperationException("Upgrade receipt was not saved.");
                    for(int n=0;n<materials.Count;n++)if(Q.Execute(c,t,@"INSERT INTO revora_equipment_upgrade_materials(receipt,item_uid,item_type,before_stack,after_stack)
VALUES(@receipt,@item,@type,@before,@after)","receipt",receipt,"item",(long)materials[n].UID,"type",(int)types[n],"before",(int)stacks[n],"after",(int)stacks[n]-1)!=1)
                        throw new InvalidOperationException("Upgrade payment receipt was not saved.");
                    {
                        string key=receipt.ToString("N");
                        if(op==Operation.MeteorLevel) {
                            if(success)ProgressionEvidence.Complete(c,t,p.Entity.UID,10420,"equipment-upgrade",key,1);
                            else ProgressionEvidence.Complete(c,t,p.Entity.UID,10421,"equipment-upgrade",key,1);
                        } else if(op==Operation.DragonQuality) {
                            if(success)ProgressionEvidence.Complete(c,t,p.Entity.UID,10423,"equipment-upgrade",key,1);
                            else ProgressionEvidence.Complete(c,t,p.Entity.UID,10424,"equipment-upgrade",key,1);
                        }
                        if(op==Operation.DragonLevel && success)ProgressionEvidence.Complete(c,t,p.Entity.UID,10422,"equipment-upgrade",key,1);
                        if(before.One==0 && after.One!=0)ProgressionEvidence.Complete(c,t,p.Entity.UID,10425,"equipment-upgrade",key,1);
                        if(before.Two==0 && after.Two!=0)ProgressionEvidence.Complete(c,t,p.Entity.UID,10426,"equipment-upgrade",key,1);
                    }
                    if(!Ready(p) || !Target(p,item,route) || !before.Matches(item) || item.Position!=position || item.Bound!=bound || !ReferenceEquals(Find(p,target),item))throw new InvalidOperationException("Upgrade context changed.");
                    for(int n=0;n<materials.Count;n++)if(materials[n].StackSize!=stacks[n] || !Usable(materials[n]) || materials[n].Position!=0 || materials[n].ID!=types[n] || materials[n].Bound && !bound || !ReferenceEquals(Find(p,materials[n].UID),materials[n]))throw new InvalidOperationException("Upgrade material context changed.");
                    t.Commit();
                }}
                if(BeforeProjection!=null)BeforeProjection();
                Q.Install(p,materials.Where((m,n)=>stacks[n]==1).Select(m=>m.UID),new ConquerItem[0]);
                for(int n=0;n<materials.Count;n++)if(stacks[n]>1){materials[n].StackSize--;materials[n].Mode=Enums.ItemMode.Update;materials[n].Send(p);}
                after.Install(item);item.Mode=Enums.ItemMode.Update;item.Send(p);
                Q.Refresh(p);
                if(position!=0){p.LoadItemStats();p.Equipment.UpdateEntityPacket();}
                return true;
            }catch(Exception ex){Q.Fail(p,"equipment-upgrade",ex);return false;}
        }
    }
}
