using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class Housing
    {
        internal static bool Enabled;
        private static readonly object worlds = new object();
        private static readonly ushort[] terrains = { 1098, 1099, 2080, 1765, 3024 };
        private static readonly uint[] permits = { 721170, 721174, 711198, 711200, 711553 };
        private static readonly ulong[] silver = { 300000, 500000, 2000000, 5000000, 0 };
        private static readonly int[] gemCounts = { 6, 7, 8, 2, 40 };
        private static readonly ulong[] gemSilver = { 1000000, 2000000, 3000000, 4000000, 80000000 };
        private static readonly uint[] gemFamilies = { 700000,700010,700020,700030,700040,700050,700060,700070,700100,700120 };
        private sealed class Material { internal ConquerItem Item; internal uint Uid, Type; internal ushort Stack; internal bool Bound; }
        private sealed class Offer { internal int Level; internal bool Buy; internal uint Price; internal ulong Silver; internal Material[] Materials; internal DateTime Until; }
        private static Material Snapshot(ConquerItem i) { return new Material{Item=i,Uid=i.UID,Type=i.ID,Stack=i.StackSize,Bound=i.Bound}; }
        private static bool Same(GameState p,Material m) { ConquerItem i;return p.Inventory.TryGetItem(m.Uid,out i) && ReferenceEquals(i,m.Item) && Usable(i) && i.ID==m.Type && i.StackSize==m.Stack && i.Bound==m.Bound; }
        private static readonly ConditionalWeakTable<GameState, Offer> offers = new ConditionalWeakTable<GameState, Offer>();
        internal static void Initialize()
        {
            foreach (uint item in permits) if (!ConquerItemInformation.BaseInformations.ContainsKey(item)) throw new InvalidOperationException("House certificate catalog missing.");
            foreach (ushort terrain in terrains) if (!DMaps.MapPaths.ContainsKey(terrain)) throw new InvalidOperationException("House terrain registration missing.");
            HouseMapRegistry.Initialize();
            using (var c = DataHolder.MySqlConnection) { c.Open(); Q.Execute(c, null, @"CREATE TABLE IF NOT EXISTS revora_house_builds(
uid bigint NOT NULL,level integer NOT NULL CHECK(level BETWEEN 1 AND 5),material_uid bigint NOT NULL,
silver_paid bigint NOT NULL CHECK(silver_paid>=0),built_at timestamptz NOT NULL DEFAULT now(),PRIMARY KEY(uid,level));
CREATE TABLE IF NOT EXISTS revora_house_build_materials(
uid bigint NOT NULL,level integer NOT NULL,item_uid bigint NOT NULL,item_type integer NOT NULL,bound boolean NOT NULL,
PRIMARY KEY(uid,level,item_uid),FOREIGN KEY(uid,level) REFERENCES revora_house_builds(uid,level));"); }
            HouseInstallments.Initialize();
            HouseFurniture.Initialize();
            HouseVisits.Initialize();
            Enabled = true;
        }
        private static bool Near(GameState p) { return Q.Near(p,30156,1002) && p.Map.BaseID==1002 && p.Entity.PokerTable==0; }
        private static bool Usable(ConquerItem i) { return i!=null && i.Position==0 && i.Warehouse==0 && i.StackSize<=1 && i.Lock==0 && !i.Suspicious && !i.InWardrobe && i.Days==0 && i.TimeLeftInMinutes==0; }
        private static uint Price(int level) { return new[]{Config.HousePermitCp,Config.CertImproveCp,Config.CertImprove3Cp,Config.CertImprove4Cp,Config.CertImprove5Cp}[level-1]; }
        private static ushort ReserveMap(NpgsqlConnection c,NpgsqlTransaction t,GameState p,bool legacyPossible)
        {
            bool existing;
            using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_house_maps WHERE uid=@uid)","uid",(long)p.Entity.UID))existing=Equals(q.ExecuteScalar(),true);
            if(legacyPossible && !existing)
                using(var q=Q.Command(c,t,"SELECT count(*) FROM furniture WHERE mapid=@old","old",(long)(ushort)p.Entity.UID))
                    if(Convert.ToInt64(q.ExecuteScalar())!=0)throw new InvalidOperationException("Legacy house furniture requires reconciliation.");
            return HouseMapRegistry.Reserve(c,t,p.Entity.UID);
        }
        internal static int Level(NpgsqlConnection c,NpgsqlTransaction t,uint uid)
        {
            using(var q=Q.Command(c,t,"SELECT owner,houselevel,map FROM house WHERE id=@uid FOR UPDATE","uid",(long)uid))
            using(var r=q.ExecuteReader()) {
                if(!r.Read())return 0;
                int level=Convert.ToInt32(r[1]);
                if(Convert.ToInt64(r[0])!=uid || level<1 || level>5 || Convert.ToInt32(r[2])!=terrains[level-1])
                    throw new InvalidOperationException("Saved house requires ownership reconciliation.");
                if(r.Read())throw new InvalidOperationException("Duplicate saved house.");return level;
            }
        }
        private static void Credit(NpgsqlConnection c,NpgsqlTransaction t,uint uid,int level)
        {
            switch(level) {
                case 1:ProgressionEvidence.Complete(c,t,uid,10102,"house-construction-v1","1",1);break;
                case 2:ProgressionEvidence.Complete(c,t,uid,10103,"house-construction-v1","2",1);break;
                case 3:ProgressionEvidence.Complete(c,t,uid,10104,"house-construction-v1","3",1);break;
                case 4:ProgressionEvidence.Complete(c,t,uid,10105,"house-construction-v1","4",1);break;
                case 5:ProgressionEvidence.Complete(c,t,uid,10122,"house-construction-v1","5",1);break;
                default:throw new InvalidOperationException("Unknown house level.");
            }
        }
        internal static bool CompleteInstallments(NpgsqlConnection c,NpgsqlTransaction t,GameState p,Npcs d,long firstMaterial)
        {
            if(Level(c,t,p.Entity.UID)!=4)throw new InvalidOperationException("Installment house level changed.");
            if(SwordMasterEncounter.BlocksHouseChange(c,t,p.Entity.UID) || PurpleBansheeRewards.BlocksHouseChange(c,t,p.Entity.UID)){d.Text("Finish your house challenge and collect its spoils before building.");return false;}
            ushort id=ReserveMap(c,t,p,true);
            if(Kernel.GamePool.Values.Any(v=>v!=null && v.Entity!=null && v.Entity.MapID==id)){d.Text("Everyone must leave your house before work begins.");return false;}
            using(var q=Q.Command(c,t,"SELECT count(*) FROM furniture WHERE mapid=@id OR nameowner=@name","id",(long)id,"name",p.Entity.Name))
                if(Convert.ToInt64(q.ExecuteScalar())!=0){d.Text("Pack up your furniture before the builders arrive.");return false;}
            if(Q.Execute(c,t,"UPDATE house SET map=3024,houselevel=5 WHERE id=@uid AND owner=@uid AND houselevel=4","uid",(long)p.Entity.UID)!=1
                || Q.Execute(c,t,"INSERT INTO revora_house_builds(uid,level,material_uid,silver_paid) VALUES(@uid,5,@item,0)","uid",(long)p.Entity.UID,"item",firstMaterial)!=1)
                throw new InvalidOperationException("Installment house construction was not saved.");
            Credit(c,t,p.Entity.UID,5);return true;
        }
        internal static Map Load(GameState p)
        {
            if(!Enabled || p==null || p.Entity==null)return null;
            lock(p.ItemSyncRoot) lock(worlds) {
                int level;ushort id;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    level=Level(c,t,p.Entity.UID);
                    if(level==0){if(p.Entity.MapID>=HouseMapRegistry.First && !DMaps.MapPaths.ContainsKey(p.Entity.MapID))HomeLocation(p);return null;}
                    id=ReserveMap(c,t,p,true);
                    t.Commit();
                }}
                MapsTable.MapInformation meta;Map map;
                if(p.Entity.MapID>=HouseMapRegistry.First && p.Entity.MapID!=id && !DMaps.MapPaths.ContainsKey(p.Entity.MapID))HomeLocation(p);
                if(Kernel.Maps.TryGetValue(id,out map)) {
                    if(!MapsTable.MapInformations.TryGetValue(id,out meta) || meta.Owner!=p.Entity.UID || map.ID!=id)
                        throw new InvalidOperationException("House runtime ownership changed.");
                    if(map.BaseID==terrains[level-1] && map.Floor!=null && map.Floor.Bounds.Width>0){ValidatePosition(p,map,level);return map;}
                    if(Kernel.GamePool.Values.Any(v=>v!=null && v.Entity!=null && v.Entity.MapID==id))throw new InvalidOperationException("House still has occupants.");
                    map.Dispose();
                }
                MapsTable.MapInformations[id]=new MapsTable.MapInformation{ID=id,BaseID=terrains[level-1],Owner=p.Entity.UID,HouseLevel=(uint)level,Status=7};
                try {
                    map=new Map(id,terrains[level-1],"maps\\"+terrains[level-1]+".map");
                    if(!ReferenceEquals(Kernel.Maps[id],map) || map.Floor.Bounds.Width==0)throw new InvalidOperationException("House terrain unavailable.");
                    if(p.Entity.MapID>=HouseMapRegistry.First && p.Entity.MapID!=id)HomeLocation(p);
                    ValidatePosition(p,map,level);
                    return map;
                } catch {
                    Map partial;if(Kernel.Maps.TryGetValue(id,out partial) && partial.BaseID==terrains[level-1])partial.Dispose();
                    throw;
                }
            }
        }
        private static void HomeLocation(GameState p){p.Entity.MapID=1002;p.Entity.X=345;p.Entity.Y=529;}
        private static void ValidatePosition(GameState p,Map map,int level)
        {
            if(p.Entity.MapID!=map.ID || map.Floor[p.Entity.X,p.Entity.Y,MapObjectType.Player])return;
            ushort x=level==1?(ushort)32:level==5?(ushort)90:(ushort)50,y=level==1?(ushort)31:level==5?(ushort)70:(ushort)50;
            if(!PotencyGuide.FindLanding(map,ref x,ref y))throw new InvalidOperationException("Saved house landing unavailable.");
            p.Entity.X=x;p.Entity.Y=y;
        }
        internal static bool OwnsLocation(GameState p)
        {
            MapsTable.MapInformation info;
            return Enabled && p!=null && p.Entity!=null && p.Entity.MapID>=HouseMapRegistry.First && p.Entity.MapID<=HouseMapRegistry.Last
                && MapsTable.MapInformations.TryGetValue(p.Entity.MapID,out info) && info.Owner==p.Entity.UID && info.HouseLevel>=1 && info.HouseLevel<=5;
        }
        internal static bool AllowTeleport(GameState p,ushort destination)
        {
            if(!Enabled || destination<HouseMapRegistry.First || DMaps.MapPaths.ContainsKey(destination))return true;
            MapsTable.MapInformation info;
            return p!=null && p.Entity!=null && MapsTable.MapInformations.TryGetValue(destination,out info)
                && (info.Owner==p.Entity.UID || HouseVisits.AllowTeleport(p,destination)) && info.HouseLevel>=1 && info.HouseLevel<=5;
        }
        private static void Prepare(GameState p,Npcs d,int level,bool buy,bool gems=false)
        {
            offers.Remove(p);
            if(level<1 || level>5){d.Text("Your home needs no further building work.");return;}
            var offer=new Offer{Level=level,Buy=buy,Price=Price(level),Silver=gems?gemSilver[level-1]:silver[level-1],Until=DateTime.UtcNow.AddMinutes(2)};
            if(buy){if(offer.Price==0){d.Text("I have no certificates for sale today.");return;}d.Text("One certificate for a level "+level+" home costs "+offer.Price+" CPs.");}
            else {
                int count=gems?gemCounts[level-1]:1;uint quality=level<=3?2u:3u;
                offer.Materials=p.Inventory.Objects.Where(i=>Usable(i) && (gems?gemFamilies.Any(f=>i.ID==f+quality):i.ID==permits[level-1]))
                    .OrderByDescending(i=>i.Bound).ThenBy(i=>i.UID).Take(count).Select(Snapshot).ToArray();
                if(offer.Materials.Length!=count){d.Text(gems?"Bring "+count+" unlocked "+(quality==2?"refined":"super")+" gems.":"Bring an unlocked certificate for a level "+level+" home.");return;}
                d.Text((gems?count+" "+(quality==2?"refined":"super")+" gems":"The certificate")+" and "+offer.Silver+" silver will pay for your level "+level+" home. Empty the house before work begins.");
                if(gems && offer.Materials.Any(m=>m.Bound))d.Text("I will use your bound gems first.");
            }
            offers.Add(p,offer);d.Option("Agreed.",40);
        }
        private static void Confirm(GameState p,Npcs d)
        {
            Offer offer;if(!offers.TryGetValue(p,out offer)){d.Text("Tell me what you need first.");return;}offers.Remove(p);
            if(DateTime.UtcNow>=offer.Until || !Near(p)){d.Text("Let us agree on the work again.");return;}
            ConquerItem result=null;ulong after=p.Entity.Money;uint cps=p.Entity.ConquerPoints;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                SkillTable.LockOwner(c,t,p);
                using(var q=Q.Command(c,t,"SELECT mapid FROM entities WHERE uid=@uid","uid",(long)p.Entity.UID))
                    if(Convert.ToInt32(q.ExecuteScalar())!=1002)throw new InvalidOperationException("House customer location changed.");
                if(offer.Buy) {
                    if(offer.Price!=Price(offer.Level) || cps<offer.Price || p.Inventory.Count>=40){d.Text("Bring enough CPs and leave a space in your bag.");return;}
                    using(var q=Q.Command(c,t,"SELECT count(*) FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0","uid",(long)p.Entity.UID))
                        if(Convert.ToInt64(q.ExecuteScalar())!=p.Inventory.Count)throw new InvalidOperationException("House purchase inventory changed.");
                    result=Q.New(permits[offer.Level-1],false);
                    if(Q.Execute(c,t,"UPDATE entities SET conquerpoints=@after WHERE uid=@uid AND conquerpoints=@before","after",(long)(cps-offer.Price),"uid",(long)p.Entity.UID,"before",(long)cps)!=1)
                        throw new InvalidOperationException("House purchase balance changed.");
                    Q.Insert(c,t,p.Entity.UID,result);
                } else {
                    if(Level(c,t,p.Entity.UID)!=offer.Level-1){d.Text("Your house has changed. Speak to me again.");return;}
                    if(offer.Level==5 && HouseInstallments.HasDeposits(c,t,p.Entity.UID)){d.Text("The Senior House Manager already holds materials for your new home. Finish the work with her.");return;}
                    if(!offer.Materials.All(m=>Same(p,m)))throw new InvalidOperationException("House materials changed.");
                    if(p.Entity.Money<offer.Silver){d.Text("You still need more silver for the building work.");return;}
                    if(SwordMasterEncounter.BlocksHouseChange(c,t,p.Entity.UID) || PurpleBansheeRewards.BlocksHouseChange(c,t,p.Entity.UID)){d.Text("Finish your house challenge and collect its spoils before building.");return;}
                    ushort id=ReserveMap(c,t,p,offer.Level>1);
                    if(Kernel.GamePool.Values.Any(v=>v!=null && v.Entity!=null && v.Entity.MapID==id)){d.Text("Everyone must leave the house before work begins.");return;}
                    using(var q=Q.Command(c,t,"SELECT count(*) FROM furniture WHERE mapid=@id OR nameowner=@name","id",(long)id,"name",p.Entity.Name))
                        if(Convert.ToInt64(q.ExecuteScalar())!=0){d.Text("Pack up your furniture before the builders arrive.");return;}
                    after=p.Entity.Money-offer.Silver;SilverBalance.Persist(c,t,p,after);
                    foreach(var material in offer.Materials)if(Q.Execute(c,t,@"DELETE FROM items WHERE uid=@item AND id=@type AND entityid=@uid AND position=0 AND warehouse=0
AND stacksize=@stack AND bound=@bound AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')='0'",
                        "item",(long)material.Uid,"type",(int)material.Type,"uid",(long)p.Entity.UID,"stack",(int)material.Stack,"bound",material.Bound?1:0)!=1)throw new InvalidOperationException("House material debit failed.");
                    int saved=offer.Level==1?Q.Execute(c,t,"INSERT INTO house(id,owner,map,houselevel,name) VALUES(@uid,@uid,@map,1,@name)","uid",(long)p.Entity.UID,"map",(int)terrains[0],"name",p.Entity.Name)
                        :Q.Execute(c,t,"UPDATE house SET map=@map,houselevel=@level WHERE id=@uid AND owner=@uid AND houselevel=@before","uid",(long)p.Entity.UID,"map",(int)terrains[offer.Level-1],"level",offer.Level,"before",offer.Level-1);
                    if(saved!=1 || Q.Execute(c,t,"INSERT INTO revora_house_builds(uid,level,material_uid,silver_paid) VALUES(@uid,@level,@item,@silver)","uid",(long)p.Entity.UID,"level",offer.Level,"item",(long)offer.Materials[0].Uid,"silver",(long)offer.Silver)!=1)
                        throw new InvalidOperationException("House construction was not saved.");
                    foreach(var material in offer.Materials)
                        if(Q.Execute(c,t,"INSERT INTO revora_house_build_materials(uid,level,item_uid,item_type,bound) VALUES(@uid,@level,@item,@type,@bound)",
                            "uid",(long)p.Entity.UID,"level",offer.Level,"item",(long)material.Uid,"type",(int)material.Type,"bound",material.Bound)!=1)
                            throw new InvalidOperationException("House material receipt was not saved.");
                    if(!offer.Materials.All(m=>Same(p,m)))throw new InvalidOperationException("House materials changed before commit.");
                    Credit(c,t,p.Entity.UID,offer.Level);
                }
                if(!Near(p) || DateTime.UtcNow>=offer.Until)throw new InvalidOperationException("House customer changed.");
                t.Commit();
            }}
            if(offer.Buy){p.Entity.ApplyCommittedConquerPoints(cps-offer.Price);Q.Install(p,new uint[0],new[]{result});d.Text("Your certificate is ready.");}
            else {SilverBalance.ApplyCommitted(p,after);Q.Install(p,offer.Materials.Select(m=>m.Uid),new ConquerItem[0]);Load(p);Q.Refresh(p);d.Text("The work is finished. Your home awaits.");}
        }
        internal static bool HandleNpc(GameState p,NpcRequest request)
        {
            if(p==null || request==null || p.ActiveNpc!=30156)return false;
            if(!Enabled)return false;
            lock(p.ItemSyncRoot)try {
                if(!Near(p))return true;var d=new Npcs(p);
                int level;using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){level=Level(c,t,p.Entity.UID);t.Commit();}}
                switch(request.OptionID) {
                    case 0:offers.Remove(p);d.Text("A home of your own, or a little more room?");d.Option(level==0?"Build a home.":"Improve my home.",level==0?(byte)1:(byte)3);if(level>0)d.Option("Take me home.",2);d.Option("House visits.",12);d.Option("Buy a certificate.",4);if(level<5)d.Option("Build with gems.",10);if(level>=4)d.Option("Visit the display home.",11);break;
                    case 1:case 3:Prepare(p,d,level+1,false);break;
                    case 2:
                        var map=Load(p);if(map==null){d.Text("You do not own a home yet.");break;}
                        ushort x=level==1?(ushort)32:level==5?(ushort)90:(ushort)50,y=level==1?(ushort)31:level==5?(ushort)70:(ushort)50;
                        if(!PotencyGuide.FindLanding(map,ref x,ref y))throw new InvalidOperationException("House landing unavailable.");
                        p.Entity.Teleport(map.BaseID,map.ID,x,y);
                        if(p.Entity.MapID!=map.ID || p.Entity.X!=x || p.Entity.Y!=y)throw new InvalidOperationException("House entry requires retry.");
                        using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,"SELECT mapid FROM entities WHERE uid=@uid","uid",(long)p.Entity.UID))
                            if(Convert.ToInt32(q.ExecuteScalar())!=map.ID)throw new InvalidOperationException("House arrival was not saved.");}
                        return true;
                    case 4:offers.Remove(p);d.Text("Which certificate do you need?");for(int i=1;i<=5;i++)d.Option("Level "+i+": "+Price(i)+" CPs.",(byte)(i+4));break;
                    case 5:case 6:case 7:case 8:case 9:Prepare(p,d,request.OptionID-4,true);break;
                    case 10:Prepare(p,d,level+1,false,true);break;
                    case 11:offers.Remove(p);HouseInstallments.Visit(p);return true;
                    case 40:Confirm(p,d);break;
                    default:offers.Remove(p);return true;
                }
                d.Option("Farewell.",255);d.Send();
            }catch(Exception ex){Q.Fail(p,"housing",ex);}return true;
        }
    }
}
