using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Q = Revora.Game.Features.QuestInventory;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;

namespace Revora.Game.Features
{
    public static partial class RevoraMasterSystem
    {
        public const uint NpcId = 900000001;
        public const byte PeakLevel = 140;
        public static uint PeakPrice { get { return Revora.Config.RevoraMasterPeakPrice; } }
        public static uint VestmentsPrice { get { return Revora.Config.RevoraMasterVestmentsPrice; } }
        public static uint MountKitPrice { get { return Revora.Config.RevoraMasterMountKitPrice; } }
        public static uint RenamePrice { get { return Revora.Config.RevoraMasterRenamePrice; } }
        public static uint VipMaxPrice { get { return Revora.Config.RevoraMasterVipPrice; } }
        public static byte MaxVipLevel { get { return Revora.Config.RevoraMasterVipLevel; } }

        private static readonly Dictionary<uint, string> PendingNames = new Dictionary<uint, string>();
        private static readonly Dictionary<uint, byte> PendingClass = new Dictionary<uint, byte>();
        private static readonly Dictionary<uint, byte> PendingService = new Dictionary<uint, byte>();
        private static readonly object Gate = new object();

        private struct KitPiece
        {
            public byte Slot;
            public uint Id;
            public bool Arrow;
            public bool FillEmptyOnly;

            public KitPiece(byte slot, uint id, bool arrow, bool fillEmptyOnly)
            {
                Slot = slot;
                Id = id;
                Arrow = arrow;
                FillEmptyOnly = fillEmptyOnly;
            }
        }

        private sealed class VestmentOffer { internal uint Price; internal byte OwnerClass, KitClass; internal DateTime Until; internal Guid Id=Guid.NewGuid(); }
        private static readonly ConditionalWeakTable<GameState,VestmentOffer> VestmentOffers=new ConditionalWeakTable<GameState,VestmentOffer>();
        internal static Action<GameState> BeforeVestmentInstall;
        private sealed class VipOffer { internal uint Price; internal byte Level; internal DateTime Until; internal Guid Id=Guid.NewGuid(); }
        private static readonly ConditionalWeakTable<GameState,VipOffer> VipOffers=new ConditionalWeakTable<GameState,VipOffer>();
        private sealed class MountOffer { internal uint Price; internal DateTime Until; internal Guid Id=Guid.NewGuid(); }
        private static readonly ConditionalWeakTable<GameState,MountOffer> MountOffers=new ConditionalWeakTable<GameState,MountOffer>();
        internal static Func<DateTime> MountClock=()=>DateTime.UtcNow;
        internal static Action<GameState> BeforeMountInstall;
        internal static void InitializeMountKits() {
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_mount_kit_purchases(
 id uuid PRIMARY KEY,uid bigint NOT NULL,price bigint NOT NULL CHECK(price>=0),balance_before bigint NOT NULL,
 balance_after bigint NOT NULL CHECK(balance_after>=0),item_uids bigint[] NOT NULL CHECK(cardinality(item_uids)=5),
 purchased_at timestamptz NOT NULL,CHECK(balance_before-balance_after=price));
CREATE TABLE IF NOT EXISTS revora_vestment_purchases(id uuid PRIMARY KEY,uid bigint NOT NULL,price bigint NOT NULL CHECK(price>=0),
kit_class smallint NOT NULL,balance_before bigint NOT NULL,balance_after bigint NOT NULL CHECK(balance_after>=0),
item_uids bigint[] NOT NULL CHECK(cardinality(item_uids)>0),purchased_at timestamptz NOT NULL,CHECK(balance_before-balance_after=price));
CREATE TABLE IF NOT EXISTS revora_vip_purchases(id uuid PRIMARY KEY,uid bigint NOT NULL,price bigint NOT NULL CHECK(price>=0),
level smallint NOT NULL CHECK(level BETWEEN 1 AND 7),balance_before bigint NOT NULL,balance_after bigint NOT NULL CHECK(balance_after>=0),
purchased_at timestamptz NOT NULL,CHECK(balance_before-balance_after=price));");}
        }
        private static bool MountReady(GameState p) { return Q.Near(p,NpcId,1002); }
        internal static void ObserveMountDialogue(GameState p,NpcRequest r) {
            if(p==null || p.ItemSyncRoot==null || r==null)return;
            lock(p.ItemSyncRoot){
                if(r.OptionID!=12 || !MountReady(p) || r.NpcID!=0 && r.NpcID!=NpcId)VestmentOffers.Remove(p);
                if(r.OptionID!=14 || !MountReady(p) || r.NpcID!=0 && r.NpcID!=NpcId)MountOffers.Remove(p);
                if(r.OptionID!=11 || !MountReady(p) || r.NpcID!=0 && r.NpcID!=NpcId)PeakOffers.Remove(p);
                if(r.OptionID!=15 || !MountReady(p) || r.NpcID!=0 && r.NpcID!=NpcId)VipOffers.Remove(p);
            }
        }
        public static void Create()
        {
            InitializeMountKits();InitializePeak();
            CityGuardSystem.SpawnTalker(NpcId, 1002, 410, 354, 46030, "RevoraMaster", (byte)Enums.ConquerAngle.South);
            if (Kernel.Maps.ContainsKey(1002) && Kernel.Maps[1002].Npcs.ContainsKey(NpcId))
                Console.WriteLine("RevoraMaster at Twin City 410,354");
            else
                Console.WriteLine("RevoraMaster spawn skipped: map 1002 not ready");
        }

        public static bool HandleNpc(GameState client, NpcRequest request)
        {
            if (client == null || request == null || client.Entity == null || client.ActiveNpc != NpcId)
                return false;
            Npcs dialog = new Npcs(client);
            switch (request.OptionID)
            {
                case 0:
                    dialog.Text("I am RevoraMaster. Peak training costs " + Cp(PeakPrice) + ". Class vestments cost " + Cp(VestmentsPrice) + ". A mount kit costs " + Cp(MountKitPrice) + ". Lifetime VIP " + MaxVipLevel + " costs " + Cp(VipMaxPrice) + ". A name change costs " + Cp(RenamePrice) + ". Goods go into your inventory. I name the price before I take a coin.");
                    dialog.Option("Peak training - " + Cp(PeakPrice), 1);
                    dialog.Option("Class vestments - " + Cp(VestmentsPrice), 2);
                    dialog.Option("A new name - " + Cp(RenamePrice), 3);
                    dialog.Option("Mount kit - " + Cp(MountKitPrice), 4);
                    dialog.Option("Lifetime VIP " + MaxVipLevel + " - " + Cp(VipMaxPrice), 5);
                    dialog.Option("Goodbye", 255);
                    dialog.Send();
                    return true;
                case 1:
                    if (NeedsBranch(client.Entity.Class))
                    {
                        SetPendingService(client, 11);
                        OfferBranch(dialog);
                        return true;
                    }
                    OfferPeakConfirm(client,dialog, HighestClass(client.Entity.Class));
                    return true;
                case 2:
                    if (NeedsBranch(client.Entity.Class))
                    {
                        SetPendingService(client, 12);
                        OfferBranch(dialog);
                        return true;
                    }
                    OfferVestments(client,dialog,client.Entity.Class);
                    return true;
                case 3:
                    dialog.Text("Write a name of 3 to 15 letters or numbers. Only GMs may use [GM] or []. Price: " + Cp(RenamePrice) + ". You will reconnect in Twin City under the new name.");
                    dialog.Input("Your new name", 20, 15);
                    dialog.Option("Cancel", 255);
                    dialog.Send();
                    return true;
                case 4:
                    lock(client.ItemSyncRoot) {
                        MountOffers.Remove(client);
                        if(!MountReady(client))return true;
                        MountOffers.Add(client,new MountOffer{Price=MountKitPrice,Until=MountClock().AddMinutes(2)});
                    }
                    dialog.Text("I will place a +12 Steed, Super Heaven Fan, Super Star Tower, Super Riding Crop, and Super Tempest Wing in your inventory. Fan takes Super Thunder gems, Tower takes Super Glory gems, and the Wing is socketed. The Crop has no sockets. All are plus 12 and minus 7. I will not put them on you. Price: " + Cp(MountKitPrice) + ".");
                    dialog.Option("Give me the kit", 14);
                    dialog.Option("Not now", 255);
                    dialog.Send();
                    return true;
                case 5:
                    if(!MountReady(client) || request.NpcID!=0 && request.NpcID!=NpcId || MaxVipLevel<1 || MaxVipLevel>7)return true;
                    if (client.Entity.VIPLevel > MaxVipLevel || client.Entity.VIPLevel == MaxVipLevel && client.Entity.VIPDays==0)
                    {
                        dialog.Text("Your current VIP needs no upgrade from me. I will not take a coin.");
                        dialog.Option("I see.", 255);
                        dialog.Send();
                        return true;
                    }
                    lock(client.ItemSyncRoot){VipOffers.Remove(client);VipOffers.Add(client,new VipOffer{Price=VipMaxPrice,Level=MaxVipLevel,Until=MountClock().AddMinutes(1)});}
                    dialog.Text("I will raise you to lifetime VIP " + MaxVipLevel + ". It does not expire. Price: " + Cp(VipMaxPrice) + ".");
                    dialog.Option("Raise my VIP", 15);
                    dialog.Option("Not now", 255);
                    dialog.Send();
                    return true;
                case 11:
                    ApplyPeak(client, dialog);
                    return true;
                case 12:
                    ApplyVestments(client, dialog);
                    return true;
                case 14:
                    ApplyMountKit(client, dialog);
                    return true;
                case 15:
                    ApplyMaxVip(client, dialog);
                    return true;
                case 22:
                    SetPendingClass(client, 135);
                    ConfirmAfterBranch(client, dialog);
                    return true;
                case 23:
                    SetPendingClass(client, 145);
                    ConfirmAfterBranch(client, dialog);
                    return true;
                case 20:
                    PromptRename(client, dialog, request.Input);
                    return true;
                case 21:
                    ApplyRename(client, dialog);
                    return true;
            }
            return true;
        }

        private static void OfferVestments(GameState client,Npcs dialog,byte kitClass) {
            lock(client.ItemSyncRoot){
                if(!MountReady(client))return;
                VestmentOffers.Remove(client);VestmentOffers.Add(client,new VestmentOffer{Price=VestmentsPrice,OwnerClass=client.Entity.Class,KitClass=kitClass,Until=MountClock().AddMinutes(1)});
                dialog.Text("I will place your class's bound Super set in your bag: +12, -7, two Super gems and permanent P7 souls. Price: "+Cp(VestmentsPrice)+".");
                dialog.Option("Give me the set",12);dialog.Option("Not now",255);dialog.Send();
            }
        }
        private static void ApplyVestments(GameState client,Npcs dialog) {
            lock(client.ItemSyncRoot)try {
                VestmentOffer offer;if(!VestmentOffers.TryGetValue(client,out offer))return;VestmentOffers.Remove(client);
                if(!MountReady(client) || MountClock()>=offer.Until || VestmentsPrice!=offer.Price || client.Entity.Class!=offer.OwnerClass)return;
                var kit=KitFor(offer.KitClass);if(kit==null || kit.Length==0 || !KitInCatalog(kit))return;
                if(client.Inventory.Count+CountBagPieces(kit)>40 || client.Entity.ConquerPoints<offer.Price){dialog.Text("Bring the quoted CPs and leave room for the whole set.");dialog.Option("I see.",255);dialog.Send();return;}
                var items=BuildBagSet(kit,offer.KitClass);uint before=client.Entity.ConquerPoints,after=before-offer.Price;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    SkillTable.LockOwner(c,t,client);var saved=new List<uint>();
                    using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid FOR UPDATE","uid",(long)client.Entity.UID))
                    using(var r=q.ExecuteReader())while(r.Read())saved.Add(checked((uint)r.GetInt64(0)));
                    if(!saved.SequenceEqual(client.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException("Vestment bag changed.");
                    if(Q.Execute(c,t,"UPDATE entities SET conquerpoints=@after WHERE uid=@uid AND conquerpoints=@before AND class=@class","after",(long)after,"uid",(long)client.Entity.UID,"before",(long)before,"class",(int)offer.OwnerClass)!=1)throw new InvalidOperationException("Vestment purse or class changed.");
                    foreach(var item in items){
                        Q.Insert(c,t,client.Entity.UID,item);
                        if(Q.Execute(c,t,"UPDATE items SET plus=@plus,bless=@bless,color=@color,socketone=@one,sockettwo=@two WHERE uid=@item AND entityid=@uid","plus",(int)item.Plus,"bless",(int)item.Bless,"color",(int)item.Color,"one",(int)item.SocketOne,"two",(int)item.SocketTwo,"item",(long)item.UID,"uid",(long)client.Entity.UID)!=1)throw new InvalidOperationException("Vestment properties failed.");
                        var soul=item.Purification;
                        if(soul.Available && Q.Execute(c,t,"INSERT INTO itemadding(uid,addingtype,addingid,addinglevel,addingpercent,duration,addedon) VALUES(@item,0,@soul,7,0,0,@stamp)","item",(long)item.UID,"soul",(long)soul.PurificationItemID,"stamp",soul.AddedOn.ToBinary())!=1)throw new InvalidOperationException("Vestment soul failed.");
                    }
                    if(Q.Execute(c,t,"INSERT INTO revora_vestment_purchases(id,uid,price,kit_class,balance_before,balance_after,item_uids,purchased_at) VALUES(@id,@uid,@price,@class,@before,@after,@items,@now)","id",offer.Id,"uid",(long)client.Entity.UID,"price",(long)offer.Price,"class",(int)offer.KitClass,"before",(long)before,"after",(long)after,"items",items.Select(i=>(long)i.UID).ToArray(),"now",MountClock())!=1)throw new InvalidOperationException("Vestment receipt failed.");
                    if(!MountReady(client) || MountClock()>=offer.Until || VestmentsPrice!=offer.Price || client.Entity.Class!=offer.OwnerClass || client.Entity.ConquerPoints!=before)throw new InvalidOperationException("Vestment context changed.");t.Commit();
                }}
                var install=BeforeVestmentInstall;if(install!=null)install(client);
                client.Entity.ApplyCommittedConquerPoints(after);Q.Install(client,new uint[0],items);ClearPending(client);
                dialog.Text("Your bound vestments are in your bag. Equip them when you are ready.");dialog.Option("Thank you.",255);dialog.Send();
            }catch(Exception ex){Q.Fail(client,"vestment-purchase",ex);}
        }

        private static readonly uint[] MountKitIds = new uint[] { 300000, 201009, 202009, 203009, 204009 };

        private static void ApplyMaxVip(GameState client, Npcs dialog)
        {
            lock(client.ItemSyncRoot)try {
                VipOffer offer;if(!VipOffers.TryGetValue(client,out offer))return;VipOffers.Remove(client);
                if(!MountReady(client) || MountClock()>=offer.Until || VipMaxPrice!=offer.Price || MaxVipLevel!=offer.Level
                    || client.Entity.VIPLevel>offer.Level || client.Entity.VIPLevel==offer.Level && client.Entity.VIPDays==0)return;
                uint before=client.Entity.ConquerPoints;if(before<offer.Price){dialog.Text("Bring the quoted CPs first.");dialog.Option("I see.",255);dialog.Send();return;}uint after=before-offer.Price;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    SkillTable.LockOwner(c,t,client);
                    if(Q.Execute(c,t,"UPDATE entities SET conquerpoints=@after WHERE uid=@uid AND conquerpoints=@before","after",(long)after,"uid",(long)client.Entity.UID,"before",(long)before)!=1)throw new InvalidOperationException("VIP payment changed.");
                    VipState.Persist(c,t,client,offer.Level,0,0);
                    if(Q.Execute(c,t,"INSERT INTO revora_vip_purchases(id,uid,price,level,balance_before,balance_after,purchased_at) VALUES(@id,@uid,@price,@level,@before,@after,@now)","id",offer.Id,"uid",(long)client.Entity.UID,"price",(long)offer.Price,"level",(int)offer.Level,"before",(long)before,"after",(long)after,"now",MountClock())!=1)throw new InvalidOperationException("VIP receipt was not saved.");
                    if(!MountReady(client) || MountClock()>=offer.Until || VipMaxPrice!=offer.Price || MaxVipLevel!=offer.Level || client.Entity.ConquerPoints!=before)throw new InvalidOperationException("VIP purchase context changed.");t.Commit();
                }}client.Entity.ApplyCommittedConquerPoints(after);VipState.ApplyCommitted(client,offer.Level,0,0);client.Send(new VipStatus().ToArray());
                dialog.Text("It is done. You hold lifetime VIP "+offer.Level+".");dialog.Option("Thank you.",255);dialog.Send();
            }catch(Exception ex){Q.Fail(client,"vip-purchase",ex);}
        }

        private static void ApplyMountKit(GameState client, Npcs dialog)
        {
            lock(client.ItemSyncRoot)try {
                MountOffer offer;
                if(!MountOffers.TryGetValue(client,out offer))return;
                MountOffers.Remove(client);
                if(!MountReady(client) || MountClock()>=offer.Until || MountKitPrice!=offer.Price)return;
                if(MountKitIds.Any(id=>!ConquerItemInformation.BaseInformations.ContainsKey(id)))return;
                if(client.Inventory.Count+5>40 || client.Entity.ConquerPoints<offer.Price) {
                    dialog.Text("Bring the quoted CPs and leave five places in your bag.");dialog.Option("I see.",255);dialog.Send();return;
                }
                uint before=client.Entity.ConquerPoints,after=before-offer.Price;
                var items=MountKitIds.Select(id=>MakeMountItem(id,id==300000)).ToArray();
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    SkillTable.LockOwner(c,t,client);
                    var saved=new List<uint>();
                    using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid FOR UPDATE","uid",(long)client.Entity.UID))
                    using(var r=q.ExecuteReader())while(r.Read())saved.Add(checked((uint)r.GetInt64(0)));
                    if(!saved.SequenceEqual(client.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException("Mount kit bag changed.");
                    if(Q.Execute(c,t,"UPDATE entities SET conquerpoints=@after WHERE uid=@uid AND conquerpoints=@before","after",(long)after,"uid",(long)client.Entity.UID,"before",(long)before)!=1)throw new InvalidOperationException("Mount kit purse changed.");
                    foreach(var item in items) {
                        Q.Insert(c,t,client.Entity.UID,item);
                        if(Q.Execute(c,t,@"UPDATE items SET plus=12,bless=7,enchant=0,color=@color,socketone=@one,sockettwo=@two,effect=@effect,socketprogress=@progress,nextsteedcolor=@nextcolor WHERE uid=@item AND entityid=@uid",
                            "color",(int)item.Color,"one",(int)item.SocketOne,"two",(int)item.SocketTwo,"effect",(int)item.Effect,"progress",(long)item.SocketProgress,"nextcolor",item.ID==300000?item.NextGreen | (item.NextBlue<<8) | (item.NextRed<<16):0,"item",(long)item.UID,"uid",(long)client.Entity.UID)!=1)throw new InvalidOperationException("Mount kit item properties failed.");
                    }
                    if(Q.Execute(c,t,@"INSERT INTO revora_mount_kit_purchases(id,uid,price,balance_before,balance_after,item_uids,purchased_at) VALUES(@id,@uid,@price,@before,@after,@items,@now)",
                        "id",offer.Id,"uid",(long)client.Entity.UID,"price",(long)offer.Price,"before",(long)before,"after",(long)after,"items",items.Select(i=>(long)i.UID).ToArray(),"now",MountClock())!=1)throw new InvalidOperationException("Mount kit receipt failed.");
                    if(!MountReady(client) || MountClock()>=offer.Until || MountKitPrice!=offer.Price || client.Entity.ConquerPoints!=before)throw new InvalidOperationException("Mount kit context changed.");
                    t.Commit();
                }}
                var beforeInstall=BeforeMountInstall;if(beforeInstall!=null)beforeInstall(client);
                client.Entity.ApplyCommittedConquerPoints(after);Q.Install(client,new uint[0],items);
                dialog.Text("Your mount kit is ready. You will find all five pieces in your bag.");dialog.Option("Thank you.",255);dialog.Send();
            } catch(Exception ex){Q.Fail(client,"mount-kit-purchase",ex);}
        }

        private static ConquerItem MakeMountItem(uint id, bool steed)
        {
            ConquerItem item = Q.New(id,true);
            item.ID = id;
            item.Position = 0;
            item.Bound = true;
            item.Color = Enums.Color.Orange;
            item.Plus = 12;
            item.Bless = 7;
            item.Enchant = 0;
            if (steed)
            {
                item.Effect = Enums.ItemEffect.Horse;
                item.SocketProgress = 255u | (200u << 8) | (40u << 16);
            }
            else
            {
                uint type = id / 1000;
                if (type == 201)
                {
                    item.SocketOne = Enums.Gem.SuperThunderGem;
                    item.SocketTwo = Enums.Gem.SuperThunderGem;
                }
                else if (type == 202)
                {
                    item.SocketOne = Enums.Gem.SuperGloryGem;
                    item.SocketTwo = Enums.Gem.SuperGloryGem;
                }
                else if (type == 204)
                {
                    item.SocketOne = Enums.Gem.SuperThunderGem;
                    item.SocketTwo = Enums.Gem.SuperGloryGem;
                }
            }
            ConquerItemBaseInformation info;
            if (ConquerItemInformation.BaseInformations.TryGetValue(id, out info) && info != null && info.Durability > 0)
            {
                item.Durability = info.Durability;
                item.MaximDurability = info.Durability;
            }
            else
            {
                item.Durability = 10000;
                item.MaximDurability = 10000;
            }
            return item;
        }

        private static void PromptRename(GameState client, Npcs dialog, string name)
        {
            if (name != null)
                name = name.Trim();
            string rejection;
            if (!TryValidateName(client, name, out rejection))
            {
                dialog.Text(rejection);
                dialog.Option("Choose another name", 3);
                dialog.Option("Goodbye", 255);
                dialog.Send();
                return;
            }
            lock (Gate)
                PendingNames[client.Entity.UID] = name;
            dialog.Text("I will change your name to " + name + " for " + Cp(RenamePrice) + ". This cannot be undone from this menu. Confirm to pay. You will reconnect in Twin City under the new name.");
            dialog.Option("Confirm purchase", 21);
            dialog.Option("Cancel", 255);
            dialog.Send();
        }

        private static void ApplyRename(GameState client, Npcs dialog)
        {
            if (!CanServe(client, dialog))
                return;
            string name;
            lock (Gate)
            {
                if (!PendingNames.TryGetValue(client.Entity.UID, out name))
                    name = null;
            }
            string rejection;
            if (!TryValidateName(client, name, out rejection))
            {
                dialog.Text(rejection);
                dialog.Option("Choose another name", 3);
                dialog.Option("Goodbye", 255);
                dialog.Send();
                return;
            }
            if (!Charge(client, dialog, RenamePrice))
                return;
            lock (Gate)
                PendingNames.Remove(client.Entity.UID);
            client.NewName = name;
            client.namechanges += 1;
            Server.CambiarNombreRegistro(client.Entity.Name, " changed name to " + name + " Date " + DateTime.Now.ToString());
            client.ChangeName(client);
        }

        private static bool NeedsBranch(byte cls)
        {
            return cls <= 102 && cls >= 100;
        }

        private static byte HighestClass(byte cls)
        {
            if (cls >= 10 && cls <= 15) return 15;
            if (cls >= 20 && cls <= 25) return 25;
            if (cls >= 40 && cls <= 45) return 45;
            if (cls >= 50 && cls <= 55) return 55;
            if (cls >= 60 && cls <= 65) return 65;
            if (cls >= 70 && cls <= 75) return 75;
            if (cls >= 80 && cls <= 85) return 85;
            if (cls >= 132 && cls <= 135) return 135;
            if (cls >= 142 && cls <= 145) return 145;
            if (cls >= 160 && cls <= 165) return 165;
            return cls;
        }

        private static string ClassTitle(byte cls)
        {
            switch (HighestClass(cls))
            {
                case 15: return "Trojan Master";
                case 25: return "Warrior Master";
                case 45: return "Archer Master";
                case 55: return "Ninja Master";
                case 65: return "Monk Master";
                case 75: return "Pirate Master";
                case 85: return "Dragon Warrior";
                case 135: return "Water Saint";
                case 145: return "Fire Saint";
                case 165: return "Windwalker";
                default: return "your class";
            }
        }

        private static void OfferBranch(Npcs dialog)
        {
            dialog.Text("You have not chosen a Taoist path. Follow Water or Fire first; I will not teach class arts or promote you until you choose. I will ask you to confirm before charging any CPs.");
            dialog.Option("Water Taoist", 22);
            dialog.Option("Fire Taoist", 23);
            dialog.Option("Cancel", 255);
            dialog.Send();
        }

        private static void OfferPeakConfirm(GameState client,Npcs dialog, byte target)
        {
            if(!PreparePeak(client,target))return;
            dialog.Text("I will raise you to level " + PeakLevel + ", promote you to " + ClassTitle(target) + ", and teach your class arts. Equipped weapons go to +12. Rebirth is unchanged. Price: " + Cp(PeakPrice) + ".");
            dialog.Option("Begin", 11);
            dialog.Option("Not now", 255);
            dialog.Send();
        }

        private static void ConfirmAfterBranch(GameState client, Npcs dialog)
        {
            byte service = PeekPendingService(client);
            byte target = PeekPendingClass(client);
            if (service == 12)
            {
                OfferVestments(client,dialog,target);
                return;
            }
            OfferPeakConfirm(client,dialog, target);
        }

        private static void SetPendingService(GameState client, byte service)
        {
            lock (Gate)
                PendingService[client.Entity.UID] = service;
        }

        private static void SetPendingClass(GameState client, byte cls)
        {
            lock (Gate)
                PendingClass[client.Entity.UID] = cls;
        }

        private static byte PeekPendingClass(GameState client)
        {
            lock (Gate)
            {
                byte cls;
                if (PendingClass.TryGetValue(client.Entity.UID, out cls))
                    return cls;
            }
            return 0;
        }

        private static byte PeekPendingService(GameState client)
        {
            lock (Gate)
            {
                byte service;
                if (PendingService.TryGetValue(client.Entity.UID, out service))
                    return service;
            }
            return 0;
        }

        private static void ClearPending(GameState client)
        {
            lock (Gate)
            {
                PendingClass.Remove(client.Entity.UID);
                PendingService.Remove(client.Entity.UID);
            }
        }

        private static bool PromoteClass(GameState client, byte target)
        {
            if (target == 0 || client.Entity.Class == target)
                return false;
            client.Entity.Class = target;
            return true;
        }

        private static bool CanServe(GameState client, Npcs dialog)
        {
            if (client.Entity.Dead)
            {
                dialog.Text("Rise first. I do not work on the fallen.");
                dialog.Option("I see.", 255);
                dialog.Send();
                return false;
            }
            if (client.Trade != null && client.Trade.InTrade)
            {
                dialog.Text("Finish your trade. I will not take a coin while your hands are busy.");
                dialog.Option("I see.", 255);
                dialog.Send();
                return false;
            }
            return true;
        }

        private static string Cp(uint price)
        {
            return price.ToString("#,0") + " CPs";
        }

        private static bool Charge(GameState client, Npcs dialog, uint price)
        {
            if (client.Entity.ConquerPoints < price)
            {
                dialog.Text("Return when you have " + Cp(price) + ".");
                dialog.Option("I see.", 255);
                dialog.Send();
                return false;
            }
            return client.Entity.TrySpendConquerPoints(price);
        }

        private static bool RaiseLevel(GameState client)
        {
            byte from = client.Entity.Level;
            if (from >= PeakLevel)
                return false;
            if (client.Entity.Reborn == 0 && from < 120)
                DataHolder.GetStats(client.Entity.Class, 120, client);
            int start = from;
            if (client.Entity.Reborn == 0 && from < 120)
                start = 120;
            if (PeakLevel > start)
                client.Entity.Atributes += (ushort)(3 * (PeakLevel - start));
            client.Entity.Level = PeakLevel;
            client.Entity.Experience = 0;
            return true;
        }

        private static bool TeachClassArts(GameState client)
        {
            bool changed = false;
            if (NeedsBranch(client.Entity.Class))
                return false;
            changed |= GiveSkill(client, 9876);
            switch (client.Entity.Class / 10)
            {
                case 1:
                    changed |= GiveSkills(client, 1110, 1015, 1115, 1270, 1190, 3050, 10315);
                    changed |= GiveProficiency(client, 410);
                    changed |= GiveProficiency(client, 420);
                    break;
                case 2:
                    changed |= GiveSkills(client, 1020, 1025, 1040, 1050, 1051, 1260, 10470, 3060, 10311);
                    changed |= GiveProficiency(client, 480);
                    changed |= GiveProficiency(client, 900);
                    break;
                case 4:
                    changed |= GiveSkills(client, 8000, 8001, 8002, 8003, 8030, 9000, 10313);
                    changed |= GiveProficiency(client, 500);
                    break;
                case 5:
                    changed |= GiveSkills(client, 6000, 6001, 6002, 6003, 6004, 6010, 6011);
                    changed |= GiveProficiency(client, 601);
                    break;
                case 6:
                    changed |= GiveSkills(client, 10490, 10390, 10415, 10381, 10400, 10425, 10430, 10395, 10410, 10420, 10421, 10422, 10423, 10424, 10405);
                    changed |= GiveProficiency(client, 610);
                    break;
                case 7:
                    changed |= GiveSkills(client, 11030, 11040, 11050, 11060, 11070, 11100, 11110, 11120, 11130, 11140, 11170, 11180, 11230, 3050);
                    changed |= GiveProficiency(client, 611);
                    changed |= GiveProficiency(client, 612);
                    break;
                case 8:
                    changed |= GiveSkills(client, 1020, 1025, 1040, 1050, 1051, 1260, 10470, 3060, 10311);
                    changed |= GiveProficiency(client, 624);
                    break;
                case 13:
                    changed |= GiveSkills(client, 1000, 1010, 1050, 1055, 1075, 1085, 1090, 1095, 1100, 1170, 1175, 1195, 4000, 4010, 4020, 4050, 4060, 4070, 10309, 30000, 3090, 5001);
                    changed |= GiveProficiency(client, 421);
                    break;
                case 14:
                    changed |= GiveSkills(client, 1000, 1002, 1005, 1010, 1120, 1125, 1150, 1160, 1165, 1180, 5001, 10309, 10310, 3080);
                    changed |= GiveProficiency(client, 561);
                    break;
                case 16:
                    changed |= GiveProficiency(client, 626);
                    break;
            }
            return changed;
        }

        private static bool GiveSkills(GameState client, params ushort[] ids)
        {
            bool changed = false;
            for (int i = 0; i < ids.Length; i++)
                changed |= GiveSkill(client, ids[i]);
            return changed;
        }

        private static bool GiveSkill(GameState client, ushort id)
        {
            if (id == 0 || client.Spells == null) return false;
            if (SpellTable.SpellInformations == null || !SpellTable.SpellInformations.ContainsKey(id))
                return false;
            byte max = SpellTable.SpellInformations[id].Keys.Max();
            if (client.Spells != null && client.Spells.ContainsKey(id) && client.Spells[id].Level >= max)
                return false;
            Spell spell = new Spell(true);
            spell.ID = id;
            spell.Level = max;
            spell.Experience = 0;
            client.AddSpell(spell);
            return true;
        }

        private static bool GiveProficiency(GameState client, ushort id)
        {
            if (id == 0 || client.Proficiencies == null) return false;
            if (client.Proficiencies.ContainsKey(id) && client.Proficiencies[id].Level >= 20)
                return false;
            Proficiency prof = new Proficiency(true);
            prof.ID = id;
            prof.Level = 20;
            prof.Experience = 0;
            client.AddProficiency(prof);
            return true;
        }

        private static bool ComposeWeapons(GameState client)
        {
            bool changed = false;
            changed |= ComposeWeapon(client, ConquerItem.RightWeapon);
            changed |= ComposeWeapon(client, ConquerItem.LeftWeapon);
            return changed;
        }

        private static bool ComposeWeapon(GameState client, byte slot)
        {
            ConquerItem item = client.Equipment.TryGetItem(slot);
            if (item == null) return false;
            if (ItemHandler.IsArrow(item.ID)) return false;
            if (item.Plus >= 12) return false;
            item.Plus = 12;
            ConquerItemTable.UpdatePlus(item);
            item.Mode = Enums.ItemMode.Update;
            item.Send(client);
            item.Mode = Enums.ItemMode.Default;
            return true;
        }

        private static KitPiece[] KitFor(byte cls)
        {
            List<KitPiece> pieces = new List<KitPiece>();
            switch (cls / 10)
            {
                case 1:
                    AddWear(pieces, ConquerItem.Head, 118309);
                    AddWear(pieces, ConquerItem.Necklace, 120269);
                    AddWear(pieces, ConquerItem.Armor, 130309);
                    AddWear(pieces, ConquerItem.RightWeapon, 410439);
                    AddWear(pieces, ConquerItem.LeftWeapon, 420439);
                    AddWear(pieces, ConquerItem.Ring, 150269);
                    AddWear(pieces, ConquerItem.Boots, 160249);
                    break;
                case 2:
                    AddWear(pieces, ConquerItem.Head, 111309);
                    AddWear(pieces, ConquerItem.Necklace, 120269);
                    AddWear(pieces, ConquerItem.Armor, 131309);
                    AddWear(pieces, ConquerItem.RightWeapon, 480439);
                    AddWear(pieces, ConquerItem.LeftWeapon, 900309);
                    AddWear(pieces, ConquerItem.Ring, 150269);
                    AddWear(pieces, ConquerItem.Boots, 160249);
                    break;
                case 4:
                    AddWear(pieces, ConquerItem.Head, 113309);
                    AddWear(pieces, ConquerItem.Necklace, 120269);
                    AddWear(pieces, ConquerItem.Armor, 133309);
                    AddWear(pieces, ConquerItem.RightWeapon, 500429);
                    pieces.Add(new KitPiece(ConquerItem.LeftWeapon, 1050002, true, false));
                    AddWear(pieces, ConquerItem.Ring, 150269);
                    AddWear(pieces, ConquerItem.Boots, 160249);
                    break;
                case 5:
                    AddWear(pieces, ConquerItem.Head, 123309);
                    AddWear(pieces, ConquerItem.Necklace, 120269);
                    AddWear(pieces, ConquerItem.Armor, 135309);
                    AddWear(pieces, ConquerItem.RightWeapon, 601439);
                    AddWear(pieces, ConquerItem.LeftWeapon, 601439);
                    AddWear(pieces, ConquerItem.Ring, 150269);
                    AddWear(pieces, ConquerItem.Boots, 160249);
                    break;
                case 6:
                    AddWear(pieces, ConquerItem.Head, 143309);
                    AddWear(pieces, ConquerItem.Necklace, 120269);
                    AddWear(pieces, ConquerItem.Armor, 136309);
                    AddWear(pieces, ConquerItem.RightWeapon, 610439);
                    AddWear(pieces, ConquerItem.LeftWeapon, 610439);
                    AddWear(pieces, ConquerItem.Ring, 150269);
                    AddWear(pieces, ConquerItem.Boots, 160249);
                    break;
                case 7:
                    AddWear(pieces, ConquerItem.Head, 144309);
                    AddWear(pieces, ConquerItem.Necklace, 120269);
                    AddWear(pieces, ConquerItem.Armor, 139309);
                    AddWear(pieces, ConquerItem.RightWeapon, 611439);
                    AddWear(pieces, ConquerItem.LeftWeapon, 612439);
                    AddWear(pieces, ConquerItem.Ring, 150269);
                    AddWear(pieces, ConquerItem.Boots, 160249);
                    break;
                case 8:
                    AddWear(pieces, ConquerItem.Head, 148309);
                    AddWear(pieces, ConquerItem.Necklace, 120269);
                    AddWear(pieces, ConquerItem.Armor, 138309);
                    AddWear(pieces, ConquerItem.RightWeapon, 617439);
                    AddWear(pieces, ConquerItem.LeftWeapon, 617439);
                    AddWear(pieces, ConquerItem.Ring, 150269);
                    AddWear(pieces, ConquerItem.Boots, 160249);
                    break;
                case 10:
                case 13:
                    AddWear(pieces, ConquerItem.Head, 114309);
                    AddWear(pieces, ConquerItem.Necklace, 121269);
                    AddWear(pieces, ConquerItem.Armor, 134309);
                    AddWear(pieces, ConquerItem.RightWeapon, 421439);
                    AddWear(pieces, ConquerItem.LeftWeapon, 619439);
                    AddWear(pieces, ConquerItem.Ring, 152279);
                    AddWear(pieces, ConquerItem.Boots, 160249);
                    break;
                case 14:
                    AddWear(pieces, ConquerItem.Head, 114309);
                    AddWear(pieces, ConquerItem.Necklace, 121269);
                    AddWear(pieces, ConquerItem.Armor, 134309);
                    AddWear(pieces, ConquerItem.RightWeapon, 561439);
                    AddWear(pieces, ConquerItem.LeftWeapon, 619439);
                    AddWear(pieces, ConquerItem.Ring, 152279);
                    AddWear(pieces, ConquerItem.Boots, 160249);
                    break;
                case 16:
                    AddWear(pieces, ConquerItem.Head, 170309);
                    AddWear(pieces, ConquerItem.Necklace, 120269);
                    AddWear(pieces, ConquerItem.Armor, 101309);
                    AddWear(pieces, ConquerItem.RightWeapon, 626439);
                    AddWear(pieces, ConquerItem.LeftWeapon, 626439);
                    AddWear(pieces, ConquerItem.Ring, 150269);
                    AddWear(pieces, ConquerItem.Boots, 160249);
                    break;
                default:
                    return null;
            }
            return pieces.ToArray();
        }

        private static void AddWear(List<KitPiece> pieces, byte slot, uint id)
        {
            pieces.Add(new KitPiece(slot, id, false, false));
        }

        private static bool KitInCatalog(KitPiece[] kit)
        {
            for (int i = 0; i < kit.Length; i++)
            {
                if (kit[i].FillEmptyOnly)
                    continue;
                if (!ConquerItemInformation.BaseInformations.ContainsKey(kit[i].Id))
                    return false;
            }
            return true;
        }

        private static int CountBagPieces(KitPiece[] kit)
        {
            int n = 0;
            for (int i = 0; i < kit.Length; i++)
            {
                if (kit[i].FillEmptyOnly)
                    continue;
                if (ConquerItemInformation.BaseInformations.ContainsKey(kit[i].Id))
                    n++;
            }
            return n;
        }

        private static ConquerItem[] BuildBagSet(KitPiece[] kit,byte kitClass)
        {
            var items=new List<ConquerItem>();
            bool taoist = kitClass / 10 == 10 || kitClass / 10 == 13 || kitClass / 10 == 14;
            Enums.Gem gem = taoist ? Enums.Gem.SuperPhoenixGem : Enums.Gem.SuperDragonGem;
            for (int i = 0; i < kit.Length; i++)
            {
                KitPiece piece = kit[i];
                if (piece.FillEmptyOnly)
                    continue;
                if (!ConquerItemInformation.BaseInformations.ContainsKey(piece.Id))
                    continue;
                ConquerItem item = Q.New(piece.Id,true);
                item.ID = piece.Id;
                item.Position = 0;
                item.Bound = true;
                item.Color = Enums.Color.Orange;
                if (piece.Arrow)
                {
                    item.Plus = 0;
                    item.Bless = 0;
                }
                else
                {
                    item.Plus = 12;
                    item.Bless = 7;
                    item.SocketOne = gem;
                    item.SocketTwo = gem;
                }
                ConquerItemBaseInformation info;
                if (ConquerItemInformation.BaseInformations.TryGetValue(piece.Id, out info) && info != null && info.Durability > 0)
                {
                    item.Durability = info.Durability;
                    item.MaximDurability = info.Durability;
                }
                else
                {
                    item.Durability = 10000;
                    item.MaximDurability = 10000;
                }
                uint soul = piece.Arrow ? 0 : SoulFor(piece.Slot,piece.Id,kitClass);
                if(soul!=0){if(!ConquerItemInformation.BaseInformations.ContainsKey(soul))throw new InvalidOperationException("Vestment soul catalog missing.");item.Purification=new ItemAdding.Purification_{Available=true,ItemUID=item.UID,PurificationLevel=7,PurificationDuration=0,PurificationItemID=soul,AddedOn=MountClock().ToLocalTime()};}
                items.Add(item);
            }
            return items.ToArray();
        }

        private static uint SoulFor(byte position, uint itemId, byte cls)
        {
            bool taoist = cls / 10 == 10 || cls / 10 == 13 || cls / 10 == 14;
            switch (position)
            {
                case ConquerItem.Head:
                    return taoist ? 820076u : 820073u;
                case ConquerItem.Armor:
                    return taoist ? 822072u : 822071u;
                case ConquerItem.Necklace:
                    return taoist ? 821034u : 821033u;
                case ConquerItem.Ring:
                    return taoist ? 823060u : 823058u;
                case ConquerItem.Boots:
                    return 824018u;
                case ConquerItem.LeftWeapon:
                    uint left = itemId / 1000;
                    if (left == 900) return 800422u;
                    if (left == 619) return 801104u;
                    if (left == 612) return 800810u;
                    if (left == 601) return 800142u;
                    if (left == 610) return 800725u;
                    if (left == 420) return 800020u;
                    if (left == 617) return 801004u;
                    if (left == 626) return 801308u;
                    return 0;
                case ConquerItem.RightWeapon:
                    uint type = itemId / 1000;
                    if (type == 410 || type == 420) return 800020u;
                    if (type == 421) return 800522u;
                    if (type == 480) return 800111u;
                    if (type == 500) return 800618u;
                    if (type == 560 || type == 561) return 800215u;
                    if (type == 601) return 800142u;
                    if (type == 610) return 800725u;
                    if (type == 611) return 800811u;
                    if (type == 612) return 800810u;
                    if (type == 613) return 800917u;
                    if (type == 617) return 801004u;
                    if (type == 624) return 801216u;
                    if (type == 626) return 801308u;
                    return taoist ? 800522u : 800020u;
                default:
                    return 0;
            }
        }

        private static bool TryValidateName(GameState client, string name, out string rejection)
        {
            rejection = "";
            if (string.IsNullOrEmpty(name) || name.Length < 3 || name.Length > 15)
            {
                rejection = "Names must be 3 to 15 characters.";
                return false;
            }
            bool gm = client.Account != null &&
                (client.Account.State == AccountTable.AccountState.GameMaster ||
                 client.Account.State == AccountTable.AccountState.Administrador);
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (char.IsControl(c) || c == ' ' || c == '<' || c == '>' || c == '#' || c == '!' || c == '@' || c == '/' || c == '|' || c == '\\' || c == '"' || c == '\'')
                {
                    rejection = "That name is not allowed.";
                    return false;
                }
                bool letter = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');
                bool digit = c >= '0' && c <= '9';
                if (!(letter || digit || c == '_' || c == '[' || c == ']'))
                {
                    rejection = "That name is not allowed.";
                    return false;
                }
                if (!gm && Kernel.InvalidCharacters.Contains(c))
                {
                    rejection = "That name is not allowed.";
                    return false;
                }
            }
            if (!gm && (name.ToUpperInvariant().Contains("[GM]") || name.Contains("[]")))
            {
                rejection = "Only GMs may use [GM] or [] in a name.";
                return false;
            }
            if (string.Equals(client.Entity.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                rejection = "That is already your name. I will not take your Conquer Points.";
                return false;
            }
            if (Npcs.InvalidCharacters2(name))
            {
                rejection = "That name is taken.";
                return false;
            }
            foreach (GameState other in Kernel.GamePool.Values)
            {
                if (other == null || other.Fake || other.Entity == null) continue;
                if (other.Entity.UID == client.Entity.UID) continue;
                if (string.Equals(other.Entity.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    rejection = "That name is taken.";
                    return false;
                }
            }
            return true;
        }
    }
}
