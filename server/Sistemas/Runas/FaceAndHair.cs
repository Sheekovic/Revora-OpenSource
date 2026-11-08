using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Revora.Client;
using Revora.Game.Features;
using Revora.Network.GamePackets;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Network
{
    public static class FaceAndHair
    {
        private sealed class Requests { internal long Started; internal int Count; }
        private static readonly ConditionalWeakTable<GameState, Requests> requests = new ConditionalWeakTable<GameState, Requests>();
        private static bool Ready(GameState p)
        { return Q.Player(p) && p.Booth == null && p.Trade != null && !p.Trade.InTrade; }
        private static bool Allow(GameState p)
        {
            var state = requests.GetValue(p, _ => new Requests()); long now = Stopwatch.GetTimestamp();
            if (now - state.Started >= Stopwatch.Frequency) { state.Started = now; state.Count = 0; }
            if (state.Count >= 20) return false;
            state.Count++; return true;
        }
        private static void SendOwned(GameState p)
        {
            foreach (var bytes in HairFaceResponse.Entries(0, CosmeticOwnership.Snapshot(p))) p.Send(bytes);
        }
        private static void SendWindow(GameState p, uint action)
        {
            SendOwned(p);
            // Avatar eligibility reads the local player value, including VIP expiry.
            p.Entity.Update((byte)FlagsUpdate.DataType.VIPLevel, p.Entity.VIPLevel, false);
            p.Send(HairFaceResponse.Rules(action, p.Entity.VIPLevel));
        }
        public static void Handle(GameState p, byte[] packet)
        {
            if (!Ready(p)) return;
            HairFaceRequest request;
            if (!HairFaceRequest.TryRead(packet, out request)) return;
            lock (p.ItemSyncRoot) {
                if (!Ready(p) || !Allow(p)) return;
                try {
                    if (request.Action == 0) { SendOwned(p); return; }
                    if (request.Action == 8 || request.Action == 9) {
                        SendWindow(p, request.Action); return;
                    }
                    var result = CosmeticOwnership.Operate(p, request.Action, request.First, request.Second);
                    if (result == null) { p.Send("That look is unavailable, or you need more silver."); SendOwned(p); return; }
                    if (request.Action == 4) p.Send(HairFaceResponse.Entries(1, new[] { result.Entry })[0]);
                    else p.Send(HairFaceResponse.Pair(request.Action == 7 ? 5u : request.Action, request.First, request.Second));
                    if (result.AchievementChanged) Q.Refresh(p);
                } catch (Exception ex) { Q.Fail(p, "cosmetic-service", ex); }
            }
        }
        internal static void Open(GameState p, uint action)
        {
            if (action != 8 && action != 9 || !Ready(p)) return;
            lock (p.ItemSyncRoot) {
                if (!Ready(p) || !Allow(p)) return;
                try { SendWindow(p, action); }
                catch (Exception ex) { Q.Fail(p, "cosmetic-window", ex); }
            }
        }
    }
}
