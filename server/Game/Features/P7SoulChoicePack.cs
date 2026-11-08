#nullable enable
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class P7SoulChoicePack
    {
        internal const uint ItemId = 3006233;
        private sealed class Offer
        {
            internal ConquerItem Item = null!;
            internal uint[] Choices = new uint[0];
            internal int Page;
            internal DateTime Until;
        }
        private static readonly ConditionalWeakTable<GameState, Offer> Offers = new ConditionalWeakTable<GameState, Offer>();
        internal static Action? BeforeCommit = null;
        internal static Action? AfterCommit = null;
        private static bool enabled;
        internal static void Initialize()
        {
            using (var c = DataHolder.MySqlConnection)
            {
                c.Open();
                Q.Execute(c, null, @"CREATE TABLE IF NOT EXISTS revora_p7_choice_uses(
pack_uid bigint PRIMARY KEY, uid bigint NOT NULL, result_uid bigint NOT NULL UNIQUE,
result_type integer NOT NULL, used_at timestamptz NOT NULL DEFAULT now());");
            }
            enabled = true;
        }
        private static bool Ready(GameState p) => enabled && Q.Player(p) && p.Booth == null && p.Trade != null && !p.Trade.InTrade;
        private static bool Owns(GameState p, ConquerItem item)
        {
            ConquerItem current;
            return item.ID == ItemId && item.Bound && item.StackSize == 1 && item.Position == 0 && item.Warehouse == 0
                && !item.InWardrobe && item.Lock == 0 && !item.Suspicious && item.Days == 0 && item.TimeLeftInMinutes == 0
                && p.Inventory.TryGetItem(item.UID, out current) && ReferenceEquals(current, item);
        }
        internal static bool TryHandle(GameState p, byte[] packet)
        {
            ConquerItem item;
            if (p == null || p.Inventory == null || packet == null || packet.Length < 12
                || !p.Inventory.TryGetItem(BitConverter.ToUInt32(packet, 8), out item) || item.ID != ItemId) return false;
            if (packet.Length < 32 || packet.Length > 128 || BitConverter.ToUInt16(packet, 0) + 8 != packet.Length
                || BitConverter.ToUInt16(packet, 2) != 1009 || BitConverter.ToUInt16(packet, 20) != 4
                || BitConverter.ToUInt64(packet, packet.Length - 8) != Revora.Network.PacketHandler.ClientSeal) return true;
            return TryUse(item, p);
        }
        internal static bool TryUse(ConquerItem item, GameState p)
        {
            if (item == null || item.ID != ItemId) return false;
            if (!Ready(p)) return true;
            lock (p.ItemSyncRoot)
            {
                if (!Ready(p) || !Owns(p, item)) return true;
                var choices = ConquerItemInformation.BaseInformations.Where(x => x.Value.PurificationLevel == 7).Select(x => x.Key).OrderBy(x => x).ToArray();
                if (choices.Length == 0) return true;
                Offers.Remove(p);
                var offer = new Offer { Item = item, Choices = choices, Until = DateTime.UtcNow.AddMinutes(2) };
                Offers.Add(p, offer); p.ActiveNpc = ItemId; Show(p, offer);
            }
            return true;
        }
        private static void Show(GameState p, Offer offer)
        {
            var d = new Npcs(p);
            d.Text("Choose one bound P7 Dragon Soul. Page " + (offer.Page + 1) + "/" + ((offer.Choices.Length + 5) / 6) + ".");
            for (int i = 0; i < 6 && offer.Page * 6 + i < offer.Choices.Length; i++)
                d.Option(ConquerItemInformation.BaseInformations[offer.Choices[offer.Page * 6 + i]].Name, (byte)(i + 1));
            if (offer.Page > 0) d.Option("Previous page", 250);
            if ((offer.Page + 1) * 6 < offer.Choices.Length) d.Option("Next page", 251);
            d.Option("Cancel", 255); d.Send();
        }
        internal static bool HandleNpc(GameState p, NpcRequest request)
        {
            if (p == null || request == null) return false;
            uint npc = request.NpcID == 0 ? p.ActiveNpc : request.NpcID;
            if (npc != ItemId) { Offers.Remove(p); return false; }
            if (!Ready(p) || !SessionInstall.EnterCurrent(p.Entity.UID, p)) return true;
            try
            {
                lock (p.ItemSyncRoot)
                {
                    Offer offer;
                    if (!Offers.TryGetValue(p, out offer)) return true;
                    if (!Ready(p) || p.ActiveNpc != ItemId || DateTime.UtcNow > offer.Until || !Owns(p, offer.Item) || request.OptionID == 255)
                    { Offers.Remove(p); return true; }
                    if (request.OptionID == 250 || request.OptionID == 251)
                    {
                        int page = offer.Page + (request.OptionID == 250 ? -1 : 1);
                        if (page >= 0 && page * 6 < offer.Choices.Length) { offer.Page = page; Show(p, offer); }
                        return true;
                    }
                    if (request.OptionID < 1 || request.OptionID > 6) return true;
                    int index = offer.Page * 6 + request.OptionID - 1;
                    if (index >= offer.Choices.Length) return true;
                    Offers.Remove(p);
                    var reward = Q.New(offer.Choices[index]);
                    using (var c = DataHolder.MySqlConnection)
                    {
                        c.Open(); using (var t = c.BeginTransaction())
                        {
                            SkillTable.LockOwner(c, t, p);
                            if (Q.Execute(c, t, @"DELETE FROM items WHERE uid=@item AND entityid=@uid AND id=@type
AND bound=1 AND stacksize=1 AND position=0 AND warehouse=0 AND locked=0
AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0 AND COALESCE(NULLIF(days,''),'0')='0'",
                                "item", (long)offer.Item.UID, "uid", (long)p.Entity.UID, "type", (int)ItemId) != 1)
                                throw new InvalidOperationException("Soul choice pack ownership changed.");
                            Q.Insert(c, t, p.Entity.UID, reward);
                            Q.Execute(c, t, @"INSERT INTO revora_p7_choice_uses(pack_uid,uid,result_uid,result_type) VALUES(@pack,@uid,@result,@type)",
                                "pack", (long)offer.Item.UID, "uid", (long)p.Entity.UID, "result", (long)reward.UID, "type", (int)reward.ID);
                            BeforeCommit?.Invoke();
                            if (!Ready(p) || !SessionInstall.IsCurrent(p.Entity.UID, p)) throw new InvalidOperationException("Soul choice session changed.");
                            t.Commit(); AfterCommit?.Invoke();
                        }
                    }
                    Q.Install(p, new[] { offer.Item.UID }, new[] { reward });
                    p.Send("Your selected bound P7 Dragon Soul has arrived.");
                }
            }
            catch (Exception ex) { Q.Fail(p, "p7-soul-choice", ex); }
            finally { SessionInstall.Exit(p.Entity.UID); }
            return true;
        }
    }
}
