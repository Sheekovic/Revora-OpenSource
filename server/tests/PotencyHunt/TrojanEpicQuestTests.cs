using System;
using System.Linq;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static readonly Type TrojanQuest = typeof(Entity).Assembly.GetType("Revora.Game.Features.TrojanEpicQuest");
    static void TrojanTalk(GameState p, uint npc, byte option) {
        var actor = p.Map.Npcs[npc]; var tile = FlowerTile(p.Map, actor.X, actor.Y);
        AdventureWalk(p, tile.Item1, tile.Item2); p.ActiveNpc = npc;
        if (option == 0) Revora.Npcs.GetDialog(new NpcRequest(true) { NpcID = npc, OptionID = 0 }, p);
        else SoulWire(p, option, npc);
    }
    static void VerifyTrojanPrivateNpcJourney(Map home, string runtime) {
        var town = World(runtime, 1002);
        // Read the real nearby catalog actors as terrain/placement evidence; never write public NPC rows.
        using (var c = DataHolder.MySqlConnection) { c.Open(); using (var q = new Npgsql.NpgsqlCommand(
            "SELECT id,lookface,cellx,celly FROM public.npcs WHERE mapid=1002 AND ((cellx BETWEEN 415 AND 435 AND celly BETWEEN 288 AND 308) OR (cellx BETWEEN 456 AND 476 AND celly BETWEEN 390 AND 410))", c))
        using (var r = q.ExecuteReader()) while (r.Read()) town.AddNpc(new NpcSpawn { UID = Convert.ToUInt32(r[0]), Mesh = Convert.ToUInt16(r[1]),
            X = Convert.ToUInt16(r[2]), Y = Convert.ToUInt16(r[3]), MapID = 1002, Type = Enums.NpcType.Talker }); }
        Call(TrojanQuest, null, "Initialize"); Call(TrojanQuest, null, "Initialize");
        var p = TrojanPlayer(home);
        p.Entity.Teleport(1002, 425, 302); EntityTable.SaveEntity(p);
        Check(p.Account.State == AccountTable.AccountState.Player, "Trojan public NPC route admits an ordinary non-GM fixture");
        uint cp = p.Entity.ConquerPoints; ulong silver = p.Entity.Money;
        TrojanTalk(p, 10579, 1);
        Check(TrojanValue(p, "Stage") == 0, "public entrance rejects an unoffered start request");
        TrojanTalk(p, 10579, 0);
        var actual = town.Npcs[10579]; town.Npcs[10579] = new NpcSpawn { UID = 10579, MapID = 1002, X = 425, Y = 298, Mesh = 29196, Type = Enums.NpcType.Talker };
        try { SoulWire(p, 1, 10579); }
        finally { town.Npcs[10579] = actual; }
        Check(TrojanValue(p, "Stage") == 0, "replaced public NPC cannot authorize a Trojan quest action");
        var clockField = TrojanScenes.GetField("Clock", Hidden); var clock = (Func<DateTime>)clockField.GetValue(null);
        clockField.SetValue(null, new Func<DateTime>(() => clock().AddMinutes(3)));
        try { SoulWire(p, 1, 10579); }
        finally { clockField.SetValue(null, clock); }
        Check(TrojanValue(p, "Stage") == 0, "expired public dialogue offer cannot start the story");
        var low = PromotionPlayer(home, 10, 119, 10001); low.Entity.Teleport(1002, 425, 302); EntityTable.SaveEntity(low);
        TrojanTalk(low, 10579, 0); TrojanTalk(low, 10579, 1);
        Check(TrojanValue(low, "Stage") == 0 && !Fenced(low), "public entrance refuses an ineligible ordinary Trojan without changing progress");
        TrojanTalk(p, 10579, 0); TrojanTalk(p, 10579, 1);
        Check(TrojanValue(p, "Stage") == 1, "Pak's Ghost begins the story through ordinary dialogue");
        int earlyDays = 0;
        while (TrojanValue(p, "Stage") < 3) {
            int revision = TrojanValue(p, "Revision"); uint npc = TrojanValue(p, "Stage") == 1 ? 10580u : 10579u;
            TrojanTalk(p, npc, 0); TrojanTalk(p, npc, 1);
            Check(!Fenced(p), "public scrap or forging action preserves the saved character");
            if (TrojanValue(p, "Revision") == revision) {
                if (++earlyDays > 26) throw new Exception("Public scrap journey failed to progress.");
                trojanNow = trojanNow.AddDays(1);
                SoulWire(p, 1, npc);
                Check(TrojanValue(p, "Revision") == revision, "old scrap request cannot spend the next day's energy");
            }
        }
        Check(earlyDays == 26 && TrojanValue(p, "Energy") == 27, "Super Mok's daily work and Pak's reforging earn the first twenty-seven energies");
        TrojanTalk(p, 10579, 0); TrojanTalk(p, 10579, 1);
        Check(p.Map.BaseID == 3825 && TrojanValue(p, "Stage") == 3, "Pak's Ghost opens the first owned memory without free story progress");
        int beforeReload = TrojanValue(p, "Revision");
        p = ReloadBall(p);
        Check(p.Entity.MapID == 1002, "interrupted Trojan memory returns through ordinary login fallback");
        TrojanTalk(p, 10579, 0); TrojanTalk(p, 10579, 1);
        Check(!Fenced(p) && p.Entity.MapID == 1002 && TrojanValue(p, "Revision") == beforeReload,
            "resume before old-room cleanup waits without fencing or charging the character");
        Call(TrojanQuest, null, "Tick");
        TrojanTalk(p, 10579, 0); TrojanTalk(p, 10579, 1);
        Check(p.Map.BaseID == 3825 && TrojanValue(p, "Revision") == beforeReload, "public entrance resumes cold-reloaded progress without another energy charge");
        try {
            int initial = TrojanValue(p, "Revision");
            TrojanTalk(p, 10581, 1);
            Check(TrojanValue(p, "Revision") == initial, "unoffered Trojan dialogue action cannot advance saved progress");
            TrojanTalk(p, 10581, 0); SoulWire(p, 1, 10582);
            Check(TrojanValue(p, "Revision") == initial, "wrong NPC echo cannot consume a Trojan story action");
            SoulWire(p, 1, 10581, 18);
            Check(TrojanValue(p, "Revision") == initial, "short Trojan dialogue frame is rejected before mutation");
            TrojanTalk(p, 10581, 1);
            Check(TrojanValue(p, "Stage") == 4, "General Pak's offered arrival dialogue advances through the packet handler");
            SoulWire(p, 1, 10581);
            Check(TrojanValue(p, "Revision") == initial + 1, "replayed arrival choice cannot advance the next stage");
            int victories = 0, renewals = 0;
            for (int guard = 0; TrojanValue(p, "Stage") < 14 && guard < 200; guard++) {
                int stage = TrojanValue(p, "Stage"), revision = TrojanValue(p, "Revision");
                uint npc = stage == 4 || stage == 8 ? 10583u : stage == 6 ? 10582u : stage == 9 ? 10600u
                    : stage == 10 ? 10602u : stage >= 11 ? 10603u : 10581u;
                if (!p.Map.Npcs.ContainsKey(npc)) {
                    uint general = p.Map.Npcs.Keys.Single(id => id == 10581 || id == 10600 || id == 10603);
                    TrojanTalk(p, general, 0); TrojanTalk(p, general, 3);
                    Check(!Fenced(p) && p.Map.Npcs.ContainsKey(npc) && TrojanValue(p, "Revision") == revision,
                        "General Pak transfers to the next owned story room without free progress");
                }
                TrojanTalk(p, npc, 0); TrojanTalk(p, npc, 1);
                if (stage == 6 || stage == 8 || stage == 12) {
                    var body = p.Map.Entities.Values.SingleOrDefault(e => (bool)Call(TrojanCombat, null, "IsEncounter", e));
                    if (body != null) { AdventureWalk(p, 45, 42); SpookKill(p, body); victories++; }
                }
                Check(!Fenced(p), "Trojan NPC action remains consistent with saved character state: " + stage);
                int after = TrojanValue(p, "Revision");
                if (after == revision) {
                    Check(stage == 4 || stage == 6 || stage == 9, "only daily energy can defer this prepared NPC journey");
                    trojanNow = trojanNow.AddDays(1); renewals++;
                    SoulWire(p, 1, npc);
                    Check(TrojanValue(p, "Revision") == revision && p.Map.Entities.Count == 0,
                        "consumed dialogue choice cannot be replayed after daily renewal");
                } else Check(after == revision + 1, "one offered Trojan action or victory commits exactly one revision");
            }
            Check(TrojanValue(p, "Stage") == 14 && TrojanValue(p, "Energy") == 90 && victories == 20 && renewals == 63,
                "complete NPC and combat route earns all stages with ninety total energy receipts");
            Check(p.Entity.ConquerPoints == cp && p.Entity.Money == silver, "full Trojan journey charges no hidden CP or silver shortcut");
            var reward = p.Inventory.Objects.Single(i => i.ID == 614439);
            Check(!reward.Bound && reward.Plus == 12 && reward.Bless == 7 && reward.SocketOne == Enums.Gem.EmptySocket
                && reward.SocketTwo == Enums.Gem.EmptySocket && !p.Inventory.Objects.Any(i => i.ID == 3003340),
                "General Pak exchanges the issued blade for one unbound maximum enhanced saber");
            SoulWire(p, 1, 10603);
            Check(p.Inventory.Objects.Count(i => i.ID == 614439) == 1, "replayed NPC claim cannot duplicate the earned saber");
            TrojanTalk(p, 10603, 254);
            Check(p.Entity.MapID == 1004 && Call(TrojanScenes, null, "Find", p) == null, "completed Trojan story offers a working exit");
            p = ReloadBall(p);
            Check(TrojanValue(p, "Completed") == 1 && p.Inventory.Objects.Any(i => i.UID == reward.UID && !i.Bound && i.Plus == 12 && i.Bless == 7),
                "NPC-earned Trojan completion and enhanced reward survive fresh reload");
            VerifyTrojanTraining(p, home);
        } finally {
            var active = Call(TrojanScenes, null, "Find", p);
            if (active != null) TrojanTravel(p, active, "Exit");
        }
    }
}
