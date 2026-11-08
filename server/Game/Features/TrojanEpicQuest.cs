using System;
using System.Runtime.CompilerServices;
using Revora.Client;
using Revora.Network.GamePackets;
using S = Revora.Game.Features.TrojanEpicStore;
using W = Revora.Game.Features.TrojanEpicScenes;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    // Story coordinator initialized and scheduled by the server lifecycle.
    internal static class TrojanEpicQuest
    {
        internal static bool Enabled { get; private set; }
        private sealed class Offer { internal uint Npc; internal int Revision; internal byte Option; internal W.Scene Scene; internal DateTime Until; }
        private static readonly ConditionalWeakTable<GameState, Offer> offers = new ConditionalWeakTable<GameState, Offer>();
        internal static void Initialize()
        {
            W.ValidateTerrain(); TrojanEpicCombat.ValidateCatalog();
            S.Initialize(); TrojanEpicEntrance.Place(); TrojanEpicTraining.Initialize(); TrojanEpicJournal.Initialize(); Enabled = true;
        }
        internal static bool Handles(uint npc)
        {
            return Enabled && (npc == 10579 || npc == 10580 || npc == 10581 || npc == 10582 || npc == 10583 || npc == 10600 || npc == 10602 || npc == 10603);
        }
        internal static void Tick() { if (Enabled) W.Tick(); }
        private static bool Near(GameState p, W.Scene scene, uint npc)
        {
            return W.Owns(scene, p) && W.Clock() < scene.Until && Q.Near(p, npc, scene.Map.ID);
        }
        private static bool General(uint npc) { return npc == 10581 || npc == 10600 || npc == 10603; }
        private static uint Actor(S.Stage stage)
        {
            switch (stage) {
                case S.Stage.Arrival: case S.Stage.ArrayReport: case S.Stage.GhostReport: return 10581;
                case S.Stage.Array: case S.Stage.Traitor: return 10583;
                case S.Stage.Ghosts: return 10582;
                case S.Stage.Essences: return 10600;
                case S.Stage.Infuse: return 10602;
                case S.Stage.BorrowBlade: case S.Stage.Devastator: case S.Stage.Victory: return 10603;
                default: return 0;
            }
        }
        private static byte Action(W.Scene scene, S.Progress progress, uint npc)
        {
            if (progress.Stage < S.Stage.Arrival || progress.Stage > S.Stage.Victory) return 0;
            if (scene.Run != progress.Run) return 0;
            if (scene.Part != W.PartFor(progress.Stage)) return General(npc) ? (byte)3 : (byte)0;
            return Actor(progress.Stage) == npc ? (byte)1 : (byte)0;
        }
        private static void NextRoom(GameState p, W.Scene old, S.Progress progress)
        {
            W.Exit(old, p);
            var next = W.TryCreate(p, progress.Stage);
            if (next == null) { p.Send("The memory is occupied. Speak with Pak's Ghost again shortly."); return; }
            W.Enter(next, p);
            p.Send("General Pak awaits you. Speak with him to continue.");
        }
        private static void Entrance(GameState p, NpcRequest request)
        {
            uint npc = p.ActiveNpc; byte option = request.OptionID;
            if (!TrojanEpicEntrance.Near(p, npc)) return;
            if (!S.Eligible(p)) {
                offers.Remove(p); var refusal = new Npcs(p);
                refusal.Text("This path calls for a Trojan of level 120, or one who has been reborn.");
                refusal.Option("Farewell.", 255); refusal.Send(); return;
            }
            if (option == 255) { offers.Remove(p); return; }
            var progress = S.Load(p);
            bool start = npc == 10579 && (progress.Stage == S.Stage.None || progress.Stage == S.Stage.Claimed);
            bool work = npc == 10580 && progress.Stage == S.Stage.Scraps || npc == 10579 && progress.Stage == S.Stage.Reforge;
            bool travel = npc == 10579 && progress.Stage >= S.Stage.Arrival && progress.Stage <= S.Stage.Victory;
            if (option != 0) {
                Offer offer;
                if (option != 1 || !offers.TryGetValue(p, out offer) || offer.Npc != npc || offer.Option != option
                    || offer.Scene != null || offer.Revision != progress.Revision || W.Clock() >= offer.Until) return;
                offers.Remove(p);
                Func<bool> context = () => TrojanEpicEntrance.Near(p, npc);
                if (start) {
                    if (S.Begin(p, progress.Revision, context)) p.Send("Seek Super Mok. He will help you gather the blade's scraps.");
                } else if (work) {
                    p.Send(S.Advance(p, progress.Revision, null, context)
                        ? npc == 10580 ? "Another scrap is recovered. Keep faith; the blade will be whole again." : "The blade is reforged. Speak with me when you are ready to enter the memory."
                        : "Rest now. Return after the next daily renewal.");
                } else if (travel) {
                    var old = W.Find(p); if (old != null) W.Exit(old, p);
                    var scene = W.TryCreate(p, progress.Stage);
                    if (scene == null) { p.Send("The memory is occupied. Try again shortly."); return; }
                    W.Enter(scene, p);
                    p.Send("Speak with General Pak. Your past deeds are remembered here.");
                }
                TrojanEpicJournal.Refresh(p); return;
            }
            offers.Remove(p); var d = new Npcs(p);
            d.Text(start ? "The Solar Blade lies in pieces. Will you help restore its light?"
                : work ? npc == 10580 ? "A blade lost to time cannot be restored in a day. Lend me your strength."
                    : "You have gathered every scrap. Shall we reforge the Solar Blade?"
                : travel ? "The memory awaits. I can return you to your unfinished task."
                : npc == 10580 ? "Pak's Ghost awaits you. Speak with him about the blade." : "Seek Super Mok and recover the blade's scattered scraps.");
            if (start || work || travel) {
                offers.Add(p, new Offer { Npc = npc, Revision = progress.Revision, Option = 1, Until = W.Clock().AddMinutes(2) });
                d.Option(start ? "Begin the journey." : travel ? "Enter the memory." : npc == 10580 ? "Recover a scrap." : "Reforge the blade.", 1);
            }
            d.Option("Farewell.", 255); d.Send();
        }
        internal static bool HandleNpc(GameState p, NpcRequest request)
        {
            if (!Enabled || p == null || request == null || !Handles(p.ActiveNpc)) return false;
            if (p.ItemSyncRoot == null || request.NpcID != 0 && request.NpcID != p.ActiveNpc) return true;
            lock (p.ItemSyncRoot) try {
                if (p.ActiveNpc == 10579 || p.ActiveNpc == 10580) { Entrance(p, request); return true; }
                uint npc = p.ActiveNpc; var scene = W.Find(p);
                if (scene == null || !Near(p, scene, npc)) return true;
                byte option = request.OptionID;
                if (option == 255) { offers.Remove(p); return true; }
                if (option == 254) { offers.Remove(p); W.Exit(scene, p); return true; }
                var progress = S.Load(p); byte action = Action(scene, progress, npc);
                if (option != 0) {
                    Offer offer;
                    bool recovery = option == 2 && npc == 10603 && (progress.Stage == S.Stage.Devastator || progress.Stage == S.Stage.Victory);
                    if (!offers.TryGetValue(p, out offer) || offer.Npc != npc || offer.Revision != progress.Revision
                        || !ReferenceEquals(offer.Scene, scene) || W.Clock() >= offer.Until
                        || option != offer.Option && !recovery) return true;
                    offers.Remove(p);
                    if (option == 3 && action == 3) { NextRoom(p, scene, progress); return true; }
                    if (recovery) {
                        p.Send(S.RecoverBlade(p, progress.Revision, () => Near(p, scene, npc))
                            ? "Keep the Solar Blade close this time." : "Bring room in your bag. I cannot replace a blade you still possess.");
                    } else if (option == 1 && action == 1) {
                        if (progress.Stage == S.Stage.Ghosts || progress.Stage == S.Stage.Traitor || progress.Stage == S.Stage.Devastator) {
                            p.Send(TrojanEpicCombat.Spawn(scene, p) != null ? "Your foe awaits. Stand firm."
                                : progress.Stage == S.Stage.Devastator ? "Bring the borrowed Solar Blade in your bag." : "Rest now. Return after the next daily renewal.");
                        } else if (progress.Stage == S.Stage.Victory) {
                            p.Send(S.Claim(p, progress.Revision, () => Near(p, scene, npc))
                                ? "The saber is yours. Carry it with honor." : "Bring back the borrowed Solar Blade and make room in your bag.");
                        } else {
                            bool advanced = S.Advance(p, progress.Revision, null, () => Near(p, scene, npc));
                            p.Send(advanced ? "Your work is done. The path grows clearer."
                                : progress.Stage == S.Stage.BorrowBlade ? "Make room for the Solar Blade." : "Rest now. Return after the next daily renewal.");
                        }
                        TrojanEpicJournal.Refresh(p);
                    }
                    // A fresh conversation is required for another mutation, including after daily renewal.
                    return true;
                }
                offers.Remove(p); var d = new Npcs(p);
                string text, choice;
                switch (progress.Stage) {
                    case S.Stage.Arrival: text = "An evil array binds this city. Monk Misery may know its weakness."; choice = "I will investigate."; break;
                    case S.Stage.Array: text = "Lend your strength to the array. Its hold weakens with every effort."; choice = "Break the array."; break;
                    case S.Stage.ArrayReport: text = "The array is broken, but shades still haunt these streets. Seek Mr. Mirror."; choice = "I will hunt them."; break;
                    case S.Stage.Ghosts: text = "Another shade waits beyond the mirror. Are you ready?"; choice = "Face the shade."; break;
                    case S.Stage.GhostReport: text = "The shades reveal our traitor. Confront Monk Misery."; choice = "Show me the traitor."; break;
                    case S.Stage.Traitor: text = "You have uncovered my secret. Now face me!"; choice = "Stand and fight."; break;
                    case S.Stage.Essences: text = "Gather the scattered solar essence. We must restore the blade's light."; choice = "Gather solar essence."; break;
                    case S.Stage.Infuse: text = "The essence is ready. Let its light flow into the blade."; choice = "Infuse the blade."; break;
                    case S.Stage.BorrowBlade: text = "Take the Solar Blade. Return it when the Devastator falls."; choice = "Borrow the blade."; break;
                    case S.Stage.Devastator: text = "The Devastator awaits. Keep the Solar Blade in your bag."; choice = "Face the Devastator."; break;
                    case S.Stage.Victory: text = "Return my blade and claim your unbound level-140 saber: +12, -7, two sockets."; choice = "Exchange the blade."; break;
                    default: text = "Your deeds will be remembered."; choice = "Continue."; break;
                }
                if (action == 3) { text = "Our work here is finished. Follow me to the next chamber."; choice = "Follow General Pak."; }
                else if (action == 0 && progress.Stage != S.Stage.Claimed) text = "Your next task awaits elsewhere. Follow the trail in your journal.";
                d.Text(text);
                if (action != 0) {
                    offers.Add(p, new Offer { Npc = npc, Revision = progress.Revision, Option = action, Scene = scene, Until = W.Clock().AddMinutes(2) });
                    d.Option(choice, action);
                    if (npc == 10603 && (progress.Stage == S.Stage.Devastator || progress.Stage == S.Stage.Victory)) d.Option("I lost the borrowed blade.", 2);
                }
                d.Option("Return to Trojan Star.", 254); d.Option("Farewell.", 255); d.Send();
            } catch (Exception ex) { offers.Remove(p); Q.Fail(p, "trojan-epic-quest", ex); }
            return true;
        }
    }
}
