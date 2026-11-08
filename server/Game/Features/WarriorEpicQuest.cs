using System;
using Revora.Client;
using Revora.Network.GamePackets;
using Q = Revora.Game.Features.QuestInventory;
using S = Revora.Game.Features.WarriorEpicStore;
using W = Revora.Game.Features.WarriorEpicScenes;

namespace Revora.Game.Features
{
    internal static class WarriorEpicQuest
    {
        internal static bool Enabled { get; private set; }
        internal static void Initialize()
        {
            W.ValidateTerrain();
            Map home; Revora.Interfaces.INpc instructor;
            if (!Kernel.Maps.TryGetValue(1004, out home) || home.BaseID != 1004 || !home.Npcs.TryGetValue(10001, out instructor)
                || instructor.Mesh != 50006 || instructor.X != 42 || instructor.Y != 74 || !home.Floor[44, 74, MapObjectType.Player])
                throw new InvalidOperationException("Warrior instructor or return terrain changed.");
            S.Initialize(); WarriorEpicJournal.Initialize(); Enabled = true;
        }
        internal static bool Handles(uint npc) { return Enabled && (npc == 19252 || npc == 19253 || npc == 19254 || npc == 19259); }
        private static bool NearMaster(GameState owner) { return S.Eligible(owner) && Q.Near(owner, 10001, 1004) && owner.Map.BaseID == 1004; }
        private static bool Near(W.Scene scene, GameState owner, uint npc)
        {
            return W.Owns(scene, owner) && W.Clock() < scene.Until && Q.Near(owner, npc, scene.Map.ID);
        }
        private static W.Scene Travel(GameState owner, S.Stage stage)
        {
            var scene = W.Find(owner);
            if (scene != null)
            {
                if (W.Owns(scene, owner) && scene.Map.BaseID == W.BaseFor(stage)) return scene;
                W.Exit(scene, owner);
            }
            scene = W.Create(owner, stage);
            W.Enter(scene, owner);
            if (stage == S.Stage.MemoryWitnessed || stage == S.Stage.EvilReleased) WarriorEpicCombat.Spawn(scene, owner);
            return scene;
        }
        internal static void Victory(GameState owner)
        {
            if (!Enabled || !S.Eligible(owner)) return;
            // Only called after a registered combat victory has committed.
            if (S.Load(owner).Stage == S.Stage.SteleBroken)
            {
                Travel(owner, S.Stage.SteleBroken);
                owner.Send("The broken seal reveals a hidden cave. Search the chest.");
            }
            else owner.Send("The evil is vanquished. Speak with Miracle's Spirit.");
        }
        internal static void Tick() { if (Enabled) W.Tick(); }
        internal static bool HandleNpc(GameState owner, NpcRequest request)
        {
            if (!Enabled || owner == null || request == null) return false;
            uint npc = owner.ActiveNpc; byte option = request.OptionID;
            if (!Handles(npc) && !(npc == 10001 && option >= 86 && option <= 89)) return false;
            if (owner.ItemSyncRoot == null || request.NpcID != 0 && request.NpcID != npc) return true;
            lock (owner.ItemSyncRoot) try
            {
                var scene = W.Find(owner);
                if (npc == 10001 && Q.Near(owner, 10001, 1004) && !S.Eligible(owner))
                {
                    var refusal = new Npcs(owner); refusal.Text("These memories await Warriors of level 40. Return when you are ready.");
                    refusal.Option("Farewell.",255); refusal.Send(); return true;
                }
                if (npc == 10001 ? !NearMaster(owner) : scene == null || !Near(scene, owner, npc)) return true;
                if (option == 255) return true;
                if (option == 254 && scene != null) { W.Exit(scene, owner); return true; }
                var progress = S.Load(owner); var dialog = new Npcs(owner);
                if (npc == 10001)
                {
                    dialog.Avatar(8);
                    if (option == 87 && progress.Stage == S.Stage.None)
                    {
                        if (S.Begin(owner, () => NearMaster(owner))) dialog.Text("Take these jade slips. Use them here to enter the ancient memory.");
                        else dialog.Text("Make room in your bag for the jade slips.");
                    }
                    else if (option == 89 && progress.Stage == S.Stage.JadeIssued)
                    {
                        S.RecoverJade(owner, () => NearMaster(owner));
                        dialog.Text("Keep your jade slips close. Use them here when you are ready.");
                    }
                    else if (option == 88 && progress.Stage >= S.Stage.MemoryEntered && progress.Stage < S.Stage.Claimed)
                    {
                        Travel(owner, progress.Stage); WarriorEpicJournal.Refresh(owner); return true;
                    }
                    else if (progress.Stage == S.Stage.None)
                    {
                        dialog.Text("This ancient book speaks of fists sealed away by a warrior's own darkness. Follow his memory and earn unbound level-140 fists: +12, -7, two sockets.");
                        dialog.Option("Let me read the book.", 87);
                    }
                    else if (progress.Stage == S.Stage.JadeIssued)
                    {
                        dialog.Text("Use the jade slips here. They will show you where the fists were sealed.");
                        dialog.Option("I lost my jade slips.", 89);
                    }
                    else if (progress.Stage == S.Stage.Claimed) dialog.Text("You have mastered the darkness within. Wield those fists with honor.");
                    else { dialog.Text("Your trial is not yet over. Shall I guide you back?"); dialog.Option("Return me to my trial.", 88); }
                }
                else if (npc == 19253)
                {
                    if (option == 1 && progress.Stage == S.Stage.MemoryEntered && S.Advance(owner, S.Stage.MemoryEntered, () => Near(scene, owner, npc)))
                    { Travel(owner, S.Stage.MemoryWitnessed); WarriorEpicJournal.Refresh(owner); owner.Send("The memory reveals a sealing stele. Break it to reach the fists."); return true; }
                    dialog.Text("A pair of fists lies beyond the seal. The memory draws you toward an ancient formation.");
                    if (progress.Stage == S.Stage.MemoryEntered) dialog.Option("Follow the memory.", 1);
                }
                else if (npc == 19254)
                {
                    if (option == 1 && progress.Stage == S.Stage.SteleBroken && S.Advance(owner, S.Stage.SteleBroken, () => Near(scene, owner, npc)))
                    { WarriorEpicCombat.Spawn(scene, owner); dialog.Text("A dark presence rises from the chest. Defeat it!"); }
                    else { dialog.Text("A restless darkness guards what lies within."); if (progress.Stage == S.Stage.SteleBroken) dialog.Option("Open the chest.", 1); }
                }
                else if (npc == 19252) dialog.Text("The seal hums with ancient power. You may return to your master before facing it.");
                else
                {
                    if (option == 1 && progress.Stage == S.Stage.EvilDefeated)
                        dialog.Text(S.Claim(owner, () => Near(scene, owner, npc)) ? "The fists are yours. Let courage guide your hands." : "Leave two spaces in your bag. The fists must remain a pair.");
                    else { dialog.Text(progress.Stage == S.Stage.Claimed ? "Carry this strength with honor." : (progress.Valuable ? "Defeat the darkness. Claim your unbound level-140 fists: +12, -7, two sockets." : "Defeat the darkness, and these fists shall be yours.")); if (progress.Stage == S.Stage.EvilDefeated) dialog.Option("Accept the paired fists.", 1); }
                }
                if (scene != null) dialog.Option("Return to Warrior God.", 254);
                dialog.Option("Farewell.", 255); dialog.Send(); WarriorEpicJournal.Refresh(owner);
            }
            catch (Exception ex) { Q.Fail(owner, "warrior-epic-quest", ex); }
            return true;
        }
        internal static bool TryUse(ConquerItem item, GameState owner)
        {
            if (item == null || item.ID != S.Jade) return false;
            if (!Enabled || owner == null || owner.ItemSyncRoot == null) return true;
            lock (owner.ItemSyncRoot) try
            {
                ConquerItem owned;
                if (!S.Eligible(owner) || !owner.Inventory.TryGetItem(item.UID, out owned) || !ReferenceEquals(item, owned)) return true;
                if (!item.Bound || item.StackSize != 1 || item.Position != 0 || item.Warehouse != 0 || item.Lock != 0
                    || item.Suspicious || item.InWardrobe || item.Days != 0 || item.TimeLeftInMinutes != 0) return true;
                var progress = S.Load(owner);
                if (progress.Stage != S.Stage.JadeIssued || progress.JadeUid != item.UID) return true;
                if (owner.Entity.MapID != 1004 || owner.Map.BaseID != 1004) { owner.Send("Use the jade slips in the Job Center."); return true; }
                var scene = Travel(owner, S.Stage.JadeIssued);
                if (!S.Advance(owner, S.Stage.JadeIssued, () => W.Owns(scene, owner) && W.Clock() < scene.Until))
                { W.Exit(scene, owner); return true; }
                WarriorEpicJournal.Refresh(owner); owner.Send("The memory takes shape. Examine the chest.");
            }
            catch (Exception ex) { Q.Fail(owner, "warrior-epic-jade", ex); }
            return true;
        }
    }
}
