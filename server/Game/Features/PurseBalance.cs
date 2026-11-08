using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Revora.Client;
using Revora.Database;

namespace Revora.Game.Features
{
    // Durable CP and Revora Point preimages. Live gains may sit ahead of the
    // saved image until a full save or an explicit purse commit. An external
    // durable change does not match the saved image and must reload.
    internal static class PurseBalance
    {
        private sealed class Image { internal Entity Entity; internal long? SavedCp; internal long? SavedRvp; }
        private static readonly ConditionalWeakTable<GameState, Image> images = new ConditionalWeakTable<GameState, Image>();
        private static void Require(GameState p)
        {
            if (p == null || p.Entity == null || !Monitor.IsEntered(p.ItemSyncRoot ?? p) || p.Entity.PersistenceRequiresReload)
                throw new InvalidOperationException("CP and Revora Points require a healthy owner lock.");
        }
        private static Image State(GameState p)
        {
            Image image;
            return p != null && images.TryGetValue(p, out image) && ReferenceEquals(image.Entity, p.Entity) ? image : null;
        }
        internal static bool Managed(GameState p) { return State(p) != null; }
        internal static void Forget(GameState p) { if (p != null) images.Remove(p); }
        internal static void CpCommitted(GameState p, uint cp)
        {
            Image image = State(p);
            if (image == null) return;
            Require(p);
            // A CP-only transaction must not mark pending Revora Points saved.
            image.SavedCp = cp;
        }
        internal static void RvpCommitted(GameState p, uint rvp)
        {
            Image image = State(p);
            if (image == null) return;
            Require(p);
            image.SavedRvp = rvp;
        }
        // The value a purse UPDATE must compare. Pending session gains stay in
        // memory and are included in the new total, not in this preimage.
        internal static long PreimageCp(GameState p)
        {
            Image image = State(p);
            return image == null || p == null || p.Entity == null ? (p == null || p.Entity == null ? 0 : p.Entity.ConquerPoints) : image.SavedCp.GetValueOrDefault();
        }
        internal static long PreimageRvp(GameState p)
        {
            Image image = State(p);
            return image == null || p == null || p.Entity == null ? (p == null || p.Entity == null ? 0 : p.Entity.RevoraPoint) : image.SavedRvp.GetValueOrDefault();
        }
        internal static void CopySaved(GameState p, out long? cp, out long? rvp)
        {
            Image image = State(p);
            if (image == null || p == null || p.Entity == null)
            {
                cp = p == null || p.Entity == null ? (long?)null : p.Entity.ConquerPoints;
                rvp = p == null || p.Entity == null ? (long?)null : p.Entity.RevoraPoint;
                return;
            }
            cp = image.SavedCp;
            rvp = image.SavedRvp;
        }
        internal static Action<GameState, GameState> AfterTransferCommit;
        internal enum TransferResult { Insufficient, Overflow, Transferred, Rejected }
        internal static bool TryPersistCp(GameState p, uint next) { return TryPersist(p, next, true); }
        internal static bool TryPersistRvp(GameState p, uint next) { return TryPersist(p, next, false); }
        internal static void Save(GameState p, bool cp)
        {
            lock (p.ItemSyncRoot ?? p)
            {
                if (p.Entity.PersistenceRequiresReload) return;
                try
                {
                    if (!TryPersist(p, cp ? p.Entity.ConquerPoints : p.Entity.RevoraPoint, cp))
                        throw new InvalidOperationException("Durable purse changed; reload required.");
                }
                catch (Exception ex) { QuestInventory.Fail(p, cp ? "cp-save" : "rvp-save", ex); }
            }
        }
        private static bool TryPersist(GameState p, uint next, bool cp)
        {
            Require(p);
            Image image = State(p);
            if (image == null) return false;
            long? saved = cp ? image.SavedCp : image.SavedRvp;
            string column = cp ? "conquerpoints" : "revorapoint";
            try
            {
                using (var conn = DataHolder.MySqlConnection)
                {
                    conn.Open();
                    using (var t = conn.BeginTransaction())
                    {
                        int n = QuestInventory.Execute(conn, t,
                            "UPDATE entities SET " + column + @"=@next WHERE uid=@uid AND (CASE WHEN @isnull THEN " + column + " IS NULL ELSE " + column + "=@old END)",
                            "next", (long)next, "uid", (long)p.Entity.UID, "isnull", !saved.HasValue, "old", saved.GetValueOrDefault());
                        if (n != 1) return false;
                        t.Commit();
                        if (cp) image.SavedCp = next; else image.SavedRvp = next;
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                // Includes a commit whose result is unknown. Never continue with
                // a usable session and a potentially stale durable preimage.
                QuestInventory.Fail(p, cp ? "cp-persist" : "rvp-persist", ex);
                return false;
            }
        }
        internal static TransferResult TryTransferConquerPoints(GameState payer, GameState payee, uint amount)
        {
            if (payer == null || payee == null || payer.Entity == null || payee.Entity == null || amount == 0
                || ReferenceEquals(payer, payee) || payer.Entity.UID == payee.Entity.UID)
                return TransferResult.Rejected;
            GameState lo = payer.Entity.UID < payee.Entity.UID ? payer : payee;
            GameState hi = ReferenceEquals(lo, payer) ? payee : payer;
            return WithOwnerLocks(lo, hi, delegate { return TransferCore(payer, payee, amount); });
        }
        private static TransferResult WithOwnerLocks(GameState lo, GameState hi, Func<TransferResult> body)
        {
            object a = lo.ItemSyncRoot ?? lo;
            object b = hi.ItemSyncRoot ?? hi;
            bool holdA = Monitor.IsEntered(a);
            bool holdB = Monitor.IsEntered(b);
            if (holdA && holdB) return body();
            if (!holdA && !holdB) { lock (a) lock (b) return body(); }
            // A caller already holding either owner must never block on the
            // other: another caller can hold it while entering this operation.
            object missing = holdA ? b : a;
            if (!Monitor.TryEnter(missing)) return TransferResult.Rejected;
            try { return body(); }
            finally { Monitor.Exit(missing); }
        }
        private static TransferResult TransferCore(GameState payer, GameState payee, uint amount)
        {
            if (payer.Entity.PersistenceRequiresReload || payee.Entity.PersistenceRequiresReload) return TransferResult.Rejected;
            if (payer.Entity.ConquerPoints < amount) return TransferResult.Insufficient;
            if ((ulong)payee.Entity.ConquerPoints + amount > uint.MaxValue) return TransferResult.Overflow;
            uint nextPayer = payer.Entity.ConquerPoints - amount;
            uint nextPayee = payee.Entity.ConquerPoints + amount;
            long oldPayer = PreimageCp(payer);
            long oldPayee = PreimageCp(payee);
            try
            {
                using (var conn = DataHolder.MySqlConnection)
                {
                    conn.Open();
                    using (var t = conn.BeginTransaction())
                    {
                        var first = payer.Entity.UID < payee.Entity.UID ? payer : payee;
                        var second = ReferenceEquals(first, payer) ? payee : payer;
                        foreach (var owner in new[] { first, second })
                        using (var q = QuestInventory.Command(conn, t,
                            "SELECT conquerpoints FROM entities WHERE uid=@uid FOR UPDATE", "uid", (long)owner.Entity.UID))
                        {
                            object value = q.ExecuteScalar();
                            Image image = State(owner);
                            long? expected = image == null ? (long?)owner.Entity.ConquerPoints : image.SavedCp;
                            long? actual = value == DBNull.Value ? (long?)null : value == null ? (long?)null : Convert.ToInt64(value);
                            if (value == null || actual != expected)
                                throw new InvalidOperationException("JiangHu durable balance changed.");
                        }
                        if (QuestInventory.Execute(conn, t, "UPDATE entities SET conquerpoints=@next WHERE uid=@uid AND COALESCE(conquerpoints,0)=@old",
                            "next", (long)nextPayer, "uid", (long)payer.Entity.UID, "old", oldPayer) != 1)
                            throw new InvalidOperationException("JiangHu payer balance changed.");
                        if (QuestInventory.Execute(conn, t, "UPDATE entities SET conquerpoints=@next WHERE uid=@uid AND COALESCE(conquerpoints,0)=@old",
                            "next", (long)nextPayee, "uid", (long)payee.Entity.UID, "old", oldPayee) != 1)
                            throw new InvalidOperationException("JiangHu payee balance changed.");
                        t.Commit();
                    }
                }
                Action<GameState, GameState> hook = AfterTransferCommit;
                if (hook != null) hook(payer, payee);
                payer.Entity.ApplyCommittedConquerPoints(nextPayer);
                payee.Entity.ApplyCommittedConquerPoints(nextPayee);
                return TransferResult.Transferred;
            }
            catch (Exception ex)
            {
                QuestInventory.Fail(payer, "cp-transfer", ex);
                QuestInventory.Fail(payee, "cp-transfer", ex);
                return TransferResult.Rejected;
            }
        }
        internal static TransferResult TrySettleBoothPurchase(GameState buyer, GameState seller, uint itemUid, ulong cost, uint boothUid)
        {
            if (buyer == null || seller == null || buyer.Entity == null || seller.Entity == null
                || ReferenceEquals(buyer, seller) || buyer.Entity.UID == seller.Entity.UID || itemUid == 0)
                return TransferResult.Rejected;
            GameState lo = buyer.Entity.UID < seller.Entity.UID ? buyer : seller;
            GameState hi = ReferenceEquals(lo, buyer) ? seller : buyer;
            return WithOwnerLocks(lo, hi, delegate { return BoothCore(buyer, seller, itemUid, cost, boothUid); });
        }
        private static bool BoothContext(GameState buyer, GameState seller, uint uid)
        {
            var booth=seller.Booth;
            Revora.Game.ConquerStructures.Booth registered;
            return booth!=null && booth.Base!=null && booth.Base.UID==uid
                && ReferenceEquals(booth.Base.Owner,seller)
                && Revora.Game.ConquerStructures.Booth.TryGetValue(uid,out registered) && ReferenceEquals(registered,booth)
                && buyer.Map!=null && ReferenceEquals(buyer.Map,seller.Map)
                && buyer.Entity.MapID==seller.Entity.MapID && booth.Base.MapID==seller.Entity.MapID
                && Kernel.GetDistance(buyer.Entity.X,buyer.Entity.Y,booth.Base.X,booth.Base.Y)<=Constants.nScreenDistance
                && Kernel.GetDistance(seller.Entity.X,seller.Entity.Y,booth.Base.X,booth.Base.Y)<=1;
        }
        private static TransferResult BoothCore(GameState buyer, GameState seller, uint itemUid, ulong cost, uint boothUid)
        {
            if (!QuestInventory.Player(buyer) || !QuestInventory.Player(seller)
                || buyer.Trade == null || seller.Trade == null || buyer.Trade.InTrade || seller.Trade.InTrade)
                return TransferResult.Rejected;
            if (buyer.Inventory == null || seller.Inventory == null || seller.Booth == null || seller.Booth.ItemList == null)
                return TransferResult.Rejected;
            if (!BoothContext(buyer,seller,boothUid) || buyer.Inventory.Count >= 40) return TransferResult.Rejected;
            if (!seller.Booth.ItemList.ContainsKey(itemUid)) return TransferResult.Rejected;
            var listed = seller.Booth.ItemList[itemUid];
            if (listed.Item == null || listed.Item.UID != itemUid || listed.Cost != cost
                || (listed.Cost_Type != Revora.Game.ConquerStructures.BoothItem.CostType.ConquerPoints
                    && listed.Cost_Type != Revora.Game.ConquerStructures.BoothItem.CostType.Silvers))
                return TransferResult.Rejected;
            Revora.Network.GamePackets.ConquerItem owned;
            if (!seller.Inventory.TryGetItem(itemUid, out owned) || !ReferenceEquals(owned, listed.Item))
                return TransferResult.Rejected;
            ConquerItemBaseInformation info;
            if (owned.Bound || owned.Lock != 0 || owned.Suspicious || owned.Position != 0 || owned.Warehouse != 0
                || owned.StackSize == 0 || owned.InWardrobe || SummerCrafting.Identity(owned.ID)
                || !ConquerItemInformation.BaseInformations.TryGetValue(owned.ID, out info)
                || info.Type != ConquerItemBaseInformation.ItemType.Dropable)
                return TransferResult.Rejected;
            bool silver=listed.Cost_Type==Revora.Game.ConquerStructures.BoothItem.CostType.Silvers;
            ulong buyerFunds=silver?buyer.Entity.Money:buyer.Entity.ConquerPoints;
            ulong sellerFunds=silver?seller.Entity.Money:seller.Entity.ConquerPoints;
            ulong maximum=silver?(ulong)long.MaxValue:uint.MaxValue;
            if(buyerFunds>maximum || sellerFunds>maximum || cost>maximum)return TransferResult.Overflow;
            if(buyerFunds<cost)return TransferResult.Insufficient;
            if(sellerFunds>maximum-cost)return TransferResult.Overflow;
            ulong nextBuyer=buyerFunds-cost,nextSeller=sellerFunds+cost;
            try
            {
                using (var conn = DataHolder.MySqlConnection)
                {
                    conn.Open();
                    using (var t = conn.BeginTransaction())
                    {
                        var first = buyer.Entity.UID < seller.Entity.UID ? buyer : seller;
                        var second = ReferenceEquals(first, buyer) ? seller : buyer;
                        if(silver) {
                            SilverBalance.AssertSaved(conn,t,first);SilverBalance.AssertSaved(conn,t,second);
                            SilverBalance.Persist(conn,t,buyer,nextBuyer);SilverBalance.Persist(conn,t,seller,nextSeller);
                        } else {
                        foreach (var owner in new[] { first, second })
                        using (var q = QuestInventory.Command(conn, t,
                            "SELECT conquerpoints FROM entities WHERE uid=@uid FOR UPDATE", "uid", (long)owner.Entity.UID))
                        {
                            object value = q.ExecuteScalar();
                            Image image = State(owner);
                            long? expected = image == null ? (long?)owner.Entity.ConquerPoints : image.SavedCp;
                            long? actual = value == DBNull.Value ? (long?)null : value == null ? (long?)null : Convert.ToInt64(value);
                            if (value == null || actual != expected)
                                throw new InvalidOperationException("Booth purse changed.");
                        }
                        if (QuestInventory.Execute(conn, t, "UPDATE entities SET conquerpoints=@next WHERE uid=@uid AND COALESCE(conquerpoints,0)=@old",
                            "next", (long)nextBuyer, "uid", (long)buyer.Entity.UID, "old", PreimageCp(buyer)) != 1)
                            throw new InvalidOperationException("Booth buyer balance changed.");
                        if (QuestInventory.Execute(conn, t, "UPDATE entities SET conquerpoints=@next WHERE uid=@uid AND COALESCE(conquerpoints,0)=@old",
                            "next", (long)nextSeller, "uid", (long)seller.Entity.UID, "old", PreimageCp(seller)) != 1)
                            throw new InvalidOperationException("Booth seller balance changed.");
                        }
                        if (QuestInventory.Execute(conn, t, @"UPDATE items SET entityid=@to WHERE uid=@item AND entityid=@from AND id=@type
AND COALESCE(position,0)=0 AND COALESCE(warehouse,0)=0 AND COALESCE(bound,0)=0
AND COALESCE(locked,0)=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND stacksize=@stack",
                            "to", (long)buyer.Entity.UID, "item", (long)itemUid, "from", (long)seller.Entity.UID,
                            "type", (int)owned.ID, "stack", (int)owned.StackSize) != 1)
                            throw new InvalidOperationException("Booth item changed.");
                        if(!QuestInventory.Player(buyer) || !QuestInventory.Player(seller) || !BoothContext(buyer,seller,boothUid))
                            throw new InvalidOperationException("Booth context changed.");
                        ProgressionEvidence.Complete(conn,t,buyer.Entity.UID,10128,"player-booth",itemUid.ToString(System.Globalization.CultureInfo.InvariantCulture),1);
                        t.Commit();
                    }
                }
                if(silver) { SilverBalance.ApplyCommitted(buyer,nextBuyer);SilverBalance.ApplyCommitted(seller,nextSeller); }
                else { buyer.Entity.ApplyCommittedConquerPoints((uint)nextBuyer);seller.Entity.ApplyCommittedConquerPoints((uint)nextSeller); }
                if (!buyer.Inventory.Add(owned, Enums.ItemUse.None)
                    || !seller.Inventory.Remove(itemUid, Enums.ItemUse.None, false))
                    throw new InvalidOperationException("Booth item requires reload.");
                seller.Booth.ItemList.Remove(itemUid);
                owned.Send(buyer);QuestInventory.Refresh(buyer);
                return TransferResult.Transferred;
            }
            catch (Exception ex)
            {
                QuestInventory.Fail(buyer, "booth-purchase", ex);
                QuestInventory.Fail(seller, "booth-purchase", ex);
                return TransferResult.Rejected;
            }
        }
        internal static void Loaded(GameState p, Database.MySqlReader reader)
        {
            lock (p.ItemSyncRoot ?? p)
            {
                Forget(p);
                long? cp = reader.ReadNullableInt64Strict("conquerpoints");
                long? rvp = reader.ReadNullableInt64Strict("revorapoint");
                if (cp < 0 || rvp < 0 || cp > uint.MaxValue || rvp > uint.MaxValue)
                    throw new OverflowException("Durable CP or Revora Points are outside the supported balance range.");
                p.Entity.ApplyCommittedConquerPoints((uint)cp.GetValueOrDefault());
                p.Entity.ProjectRevoraPoints((uint)rvp.GetValueOrDefault());
                images.Add(p, new Image { Entity = p.Entity, SavedCp = cp, SavedRvp = rvp });
            }
        }
        internal static void AssertMatches(GameState p, long? dbCp, long? dbRvp)
        {
            Require(p);
            Image image = State(p);
            if (dbCp < 0 || dbRvp < 0 || dbCp > uint.MaxValue || dbRvp > uint.MaxValue)
                throw new InvalidOperationException("Durable CP or Revora Points are outside the supported balance range.");
            if (image == null)
            {
                if (dbCp.GetValueOrDefault() != p.Entity.ConquerPoints || dbRvp.GetValueOrDefault() != p.Entity.RevoraPoint)
                    throw new InvalidOperationException("CP or Revora Points were not loaded.");
                return;
            }
            if (dbCp != image.SavedCp || dbRvp != image.SavedRvp)
                throw new InvalidOperationException("Durable CP or Revora Points changed; reload required.");
        }
        internal static void PrepareSave(GameState p, Database.MySqlCommand command)
        {
            Require(p);
            Image image = State(p);
            if (image != null)
            {
                command.AndInt64Parameter("ConquerPoints", image.SavedCp).AndInt64Parameter("RevoraPoint", image.SavedRvp);
                return;
            }
            // An unloaded session may save only when its memory still equals the
            // durable row. A null column is the same empty balance as zero.
            using (var q = new Database.MySqlCommand(Database.MySqlCommandType.SELECT).Select("entities").Where("uid", p.Entity.UID))
            using (var r = new Database.MySqlReader(q))
            {
                if (!r.Read()) throw new InvalidOperationException("CP or Revora Points owner missing.");
                long? savedCp = r.ReadNullableInt64Strict("conquerpoints");
                long? savedRvp = r.ReadNullableInt64Strict("revorapoint");
                if (savedCp < 0 || savedRvp < 0 || savedCp > uint.MaxValue || savedRvp > uint.MaxValue
                    || savedCp.GetValueOrDefault() != p.Entity.ConquerPoints || savedRvp.GetValueOrDefault() != p.Entity.RevoraPoint)
                    throw new InvalidOperationException("CP or Revora Points were not loaded.");
                command.AndInt64Parameter("ConquerPoints", savedCp).AndInt64Parameter("RevoraPoint", savedRvp);
            }
        }
        internal static void Committed(GameState p, uint cp, uint rvp)
        {
            Require(p);
            Forget(p);
            images.Add(p, new Image { Entity = p.Entity, SavedCp = cp, SavedRvp = rvp });
        }
    }
}
