using Revora.Domain.Accounts;
using Revora.Gameplay.Models;
using Revora.NativeLogin.Core;
using Revora.Persistence.PostgreSql;

namespace Revora.Host.Gameplay;

public sealed class ProgressionServiceSession5165
{
    public const uint NpcUid = 900_000_001;
    private enum MenuState : byte
    {
        Closed,
        Main,
        FirstClass,
        SecondClass,
        LoadoutClass,
        ConfirmMaximum,
        ConfirmFirst,
        ConfirmSecond,
        ConfirmLoadout,
        Completed,
    }

    private readonly ItemEconomyRuntime5165 economy;
    private readonly PostgreSqlProgressionRuntime persistence;
    private readonly AccountId accountId;
    private readonly CharacterUid characterUid;
    private readonly BoundedEventRate5165 serviceRate = new(8, TimeSpan.FromSeconds(10));
    private readonly BoundedEventRate5165 gmCommandRate = new(4, TimeSpan.FromSeconds(10));
    private MenuState state;
    private ushort selectedProfession;
    private Guid pendingOperation;
    // The genuine 5165 client closes the previous dialog page with an
    // option-255/action-101 frame after selecting an option that caused the
    // server to render the next page.  That frame acknowledges the replaced
    // page; it is not a cancellation of the newly rendered page.
    private bool pendingPageCloseAcknowledgement;

    internal ProgressionServiceSession5165(ItemEconomyRuntime5165 economy,
        PostgreSqlProgressionRuntime persistence, AccountId accountId, CharacterUid characterUid, bool enabled)
    {
        this.economy = economy;
        this.persistence = persistence;
        this.accountId = accountId;
        this.characterUid = characterUid;
        Enabled = enabled;
    }

    public bool Enabled { get; }

    public Task<ProgressionCharacterSnapshot?> ReadAsync(CancellationToken token) =>
        persistence.ReadAsync(accountId, characterUid, token);

    public async Task OpenAsync(GameplaySession5165 session, NpcReference5165 npc, CancellationToken token)
    {
        session.BeginNpcInteraction(NpcInteractionKind5165.ProgressionService, npc.LegacyUid);
        selectedProfession = 0;
        pendingOperation = Guid.Empty;
        pendingPageCloseAcknowledgement = false;
        state = MenuState.Main;
        if (!Enabled)
        {
            await RenderAsync(session, npc.Face, "These services are not available for this character yet.",
                [("Goodbye", byte.MaxValue)], token);
            return;
        }
        await RenderMainAsync(session, npc.Face, token);
    }

    public async Task<ProgressionMutationResult?> HandleAsync(GameplaySession5165 session,
        ItemEconomySession5165? itemSession, NpcDialogAnswer5165 answer, ushort face, CancellationToken token)
    {
        if (!session.HasActiveNpcInteraction(NpcInteractionKind5165.ProgressionService, NpcUid))
            throw new InvalidDataException("The RevoraMaster interaction is not active.");
        if (answer.Action == 102)
        {
            state = MenuState.Closed;
            session.CloseNpcDialog(answer);
            return null;
        }
        if (answer.Action != 101) throw new InvalidDataException("The service dialog action is invalid.");
        if (answer.Option == byte.MaxValue)
        {
            if (pendingPageCloseAcknowledgement)
            {
                pendingPageCloseAcknowledgement = false;
                return null;
            }
            state = MenuState.Closed;
            session.CloseNpcDialog(answer);
            return null;
        }
        pendingPageCloseAcknowledgement = false;
        if (!Enabled)
        {
            await RenderAsync(session, face, "That service is not available.", [("Close", byte.MaxValue)], token);
            return null;
        }

        switch (state)
        {
            case MenuState.Main:
                return await SelectMainAsync(session, answer.Option, face, token);
            case MenuState.FirstClass:
            case MenuState.SecondClass:
            case MenuState.LoadoutClass:
                await SelectClassAsync(session, answer.Option, face, token);
                return null;
            case MenuState.ConfirmMaximum:
            case MenuState.ConfirmFirst:
            case MenuState.ConfirmSecond:
            case MenuState.ConfirmLoadout:
                if (answer.Option != 100) throw new InvalidDataException("The service confirmation is invalid.");
                return await ExecuteAsync(session, itemSession, face, token);
            case MenuState.Completed:
                await RenderAsync(session, face, "This confirmed request has already been processed.",
                    [("Close", byte.MaxValue)], token);
                return null;
            default:
                throw new InvalidDataException("The service dialog state is invalid.");
        }
    }

    public async Task<ProgressionMutationResult> GiveMeAllAsync(CancellationToken token)
    {
        if (!gmCommandRate.TryAcquire())
            return new(false, false, "rate-limit",
                await ReadAsync(token) ?? throw new InvalidDataException("The authenticated character is missing."), [], 0);
        return await persistence.SetGmCapsAsync(Guid.NewGuid(), accountId, characterUid, token);
    }

    public async Task<GmInventoryClearResult> ClearInventoryAsync(CancellationToken token)
    {
        if (!gmCommandRate.TryAcquire()) return new(false, false, "rate-limit", []);
        return await persistence.ClearGmInventoryAsync(Guid.NewGuid(), accountId, characterUid, token);
    }

    public static IReadOnlyList<byte[]> MutationPackets(ProgressionMutationResult result)
    {
        ProgressionCharacterSnapshot c = result.Character;
        var packets = new List<byte[]>
        {
            ItemEconomyPackets5165.EncodeStatus(c.LegacyUid, 29, checked((ulong)c.ConquerPoints)),
        };
        if (result.Succeeded && result.CreatedItemUids.Count == 0)
        {
            packets.Add(ItemEconomyPackets5165.EncodeStatus(c.LegacyUid, 12, c.Level));
            packets.Add(ItemEconomyPackets5165.EncodeStatus(c.LegacyUid, 5, checked((ulong)c.Experience)));
            packets.Add(ItemEconomyPackets5165.EncodeStatus(c.LegacyUid, 7, c.Profession));
            packets.Add(ItemEconomyPackets5165.EncodeStatus(c.LegacyUid, 22, c.RebirthCount));
            packets.Add(ItemEconomyPackets5165.EncodeStatus(c.LegacyUid, 10,
                checked((ulong)c.AvailableAttributePoints)));
            packets.Add(ItemEconomyPackets5165.EncodeStatus(c.LegacyUid, 15, c.Strength));
            packets.Add(ItemEconomyPackets5165.EncodeStatus(c.LegacyUid, 16, c.Agility));
            packets.Add(ItemEconomyPackets5165.EncodeStatus(c.LegacyUid, 14, c.Vitality));
            packets.Add(ItemEconomyPackets5165.EncodeStatus(c.LegacyUid, 13, c.Spirit));
            packets.Add(ItemEconomyPackets5165.EncodeStatus(c.LegacyUid, 1, checked((ulong)c.MaximumHealth)));
            packets.Add(ItemEconomyPackets5165.EncodeStatus(c.LegacyUid, 3, checked((ulong)c.MaximumMana)));
        }
        else if (result.Succeeded)
        {
            packets.Add(ItemEconomyPackets5165.EncodeStatus(c.LegacyUid, 7, c.Profession));
        }
        return packets;
    }

    private async Task<ProgressionMutationResult?> SelectMainAsync(GameplaySession5165 session, byte option,
        ushort face, CancellationToken token)
    {
        switch (option)
        {
            case 1:
                state = MenuState.ConfirmMaximum;
                pendingOperation = Guid.NewGuid();
                await ConfirmAsync(session, face, "Reach maximum level", "Set level to 130 and recalculate character stats.", token);
                pendingPageCloseAcknowledgement = true;
                return null;
            case 2:
                state = MenuState.FirstClass;
                await RenderClassesAsync(session, face, "Choose your first-rebirth class.", false, token);
                pendingPageCloseAcknowledgement = true;
                return null;
            case 3:
                state = MenuState.SecondClass;
                await RenderClassesAsync(session, face, "Choose your second-rebirth class.", false, token);
                pendingPageCloseAcknowledgement = true;
                return null;
            case 4:
                state = MenuState.LoadoutClass;
                await RenderClassesAsync(session, face, "Choose the class equipment package.", true, token);
                pendingPageCloseAcknowledgement = true;
                return null;
            default:
                throw new InvalidDataException("The RevoraMaster menu option is invalid.");
        }
    }

    private async Task SelectClassAsync(GameplaySession5165 session, byte option, ushort face,
        CancellationToken token)
    {
        selectedProfession = option switch
        {
            11 => 11,
            12 => 21,
            13 => 41,
            14 => 132,
            15 => 142,
            16 when state == MenuState.LoadoutClass => 55,
            _ => throw new InvalidDataException("The selected class is not supported by patch 5165."),
        };
        pendingOperation = Guid.NewGuid();
        string name = ClassName(selectedProfession);
        switch (state)
        {
            case MenuState.FirstClass:
                state = MenuState.ConfirmFirst;
                await ConfirmAsync(session, face, "First Rebirth - " + name,
                    "Set canonical first-rebirth lineage, class and level 15.", token);
                pendingPageCloseAcknowledgement = true;
                break;
            case MenuState.SecondClass:
                state = MenuState.ConfirmSecond;
                await ConfirmAsync(session, face, "Second Rebirth - " + name,
                    "Set canonical second-rebirth lineage, class and level 15.", token);
                pendingPageCloseAcknowledgement = true;
                break;
            case MenuState.LoadoutClass:
                state = MenuState.ConfirmLoadout;
                await ConfirmAsync(session, face, "Full " + name + " equipment",
                    "Create one bound maximum-quality set, promote to the highest class rank, and master its weapon proficiencies.", token);
                pendingPageCloseAcknowledgement = true;
                break;
            default:
                throw new InvalidDataException("The class selection state is invalid.");
        }
    }

    private async Task<ProgressionMutationResult> ExecuteAsync(GameplaySession5165 session,
        ItemEconomySession5165? itemSession, ushort face, CancellationToken token)
    {
        if (!serviceRate.TryAcquire())
        {
            await RenderAsync(session, face, "Please wait before using another service.",
                [("Close", byte.MaxValue)], token);
            return new(false, false, "rate-limit",
                await ReadAsync(token) ?? throw new InvalidDataException("The authenticated character is missing."), [], 0);
        }
        ProgressionPurchaseKind kind = state switch
        {
            MenuState.ConfirmMaximum => ProgressionPurchaseKind.MaximumLevel,
            MenuState.ConfirmFirst => ProgressionPurchaseKind.FirstRebirth,
            MenuState.ConfirmSecond => ProgressionPurchaseKind.SecondRebirth,
            MenuState.ConfirmLoadout => ProgressionPurchaseKind.ClassLoadout,
            _ => throw new InvalidDataException("The service confirmation state is invalid."),
        };
        ushort? profession = kind == ProgressionPurchaseKind.MaximumLevel ? null : selectedProfession;
        ProgressionMutationResult result = await persistence.PurchaseAsync(pendingOperation, accountId,
            characterUid, kind, profession, token);
        state = MenuState.Completed;
        if (result.Succeeded)
        {
            session.Character = session.Character with
            {
                Profession = result.Character.Profession,
                Level = result.Character.Level,
                Version = result.Character.CharacterVersion,
            };
            foreach (byte[] packet in MutationPackets(result)) await session.SendAsync(packet, token);
            if (kind == ProgressionPurchaseKind.MaximumLevel &&
                PostgreSqlProgressionRuntime.MaximumRankTarget(result.Character.Profession) is ushort rank)
                foreach (ushort proficiencyType in PostgreSqlProgressionRuntime.LoadoutWeaponProficiencies(rank))
                    await session.SendAsync(Revora.Protocol5165.CombatPackets5165.EncodeProficiency(
                        proficiencyType, 20, 0), token);
            if (result.CreatedItemUids.Count > 0)
            {
                if (itemSession is null) throw new InvalidDataException("The item runtime is unavailable.");
                foreach (byte[] itemPacket in await itemSession.CurrentItemPacketsAsync(result.CreatedItemUids, token))
                    await session.SendAsync(itemPacket, token);
                foreach (ushort proficiencyType in PostgreSqlProgressionRuntime.LoadoutWeaponProficiencies(
                    selectedProfession))
                    await session.SendAsync(Revora.Protocol5165.CombatPackets5165.EncodeProficiency(
                        proficiencyType, 20, 0), token);
            }
        }
        await RenderAsync(session, face, ResultText(result), [("Close", byte.MaxValue)], token);
        return result;
    }

    private async Task RenderMainAsync(GameplaySession5165 session, ushort face, CancellationToken token) =>
        await RenderAsync(session, face, "RevoraMaster offers reviewed progression services. Each purchase costs 1,000,000 CPs.",
        [
            ("Reach maximum level - 1,000,000 CPs", (byte)1),
            ("First Rebirth - 1,000,000 CPs", (byte)2),
            ("Second Rebirth - 1,000,000 CPs", (byte)3),
            ("Get full class equipment - 1,000,000 CPs", (byte)4),
            ("Goodbye", byte.MaxValue),
        ], token);

    private static async Task RenderClassesAsync(GameplaySession5165 session, ushort face, string text,
        bool includeNinja, CancellationToken token)
    {
        var options = new List<(string Text, byte Option)>
        {
            ("Trojan", 11), ("Warrior", 12), ("Archer", 13),
            ("Water Taoist", 14), ("Fire Taoist", 15),
        };
        if (includeNinja) options.Add(("Ninja", 16));
        options.Add(("Cancel", byte.MaxValue));
        await RenderAsync(session, face, text, options, token);
    }

    private static async Task ConfirmAsync(GameplaySession5165 session, ushort face, string service,
        string change, CancellationToken token) => await RenderAsync(session, face,
        $"{service}. Cost: 1,000,000 CPs. {change}",
        [("Confirm purchase", (byte)100), ("Cancel", byte.MaxValue)], token);

    private static async Task RenderAsync(GameplaySession5165 session, ushort face, string text,
        IReadOnlyList<(string Text, byte Option)> options, CancellationToken token)
    {
        await session.SendAsync(GamePacketsAfterAuthentication5165.EncodeNpcDialogText(text), token);
        foreach ((string optionText, byte option) in options)
            await session.SendAsync(GamePacketsAfterAuthentication5165.EncodeNpcDialogOption(optionText, option), token);
        await session.SendAsync(GamePacketsAfterAuthentication5165.EncodeNpcDialogFace(face), token);
        await session.SendAsync(GamePacketsAfterAuthentication5165.EncodeNpcDialogFinish(), token);
    }

    private static string ResultText(ProgressionMutationResult result) => result.Category switch
    {
        "committed" => result.CreatedItemUids.Count > 0
            ? $"Purchase complete. {result.CreatedItemUids.Count} bound items were placed in your inventory."
            : "Purchase complete. Your authoritative character state was updated.",
        "ninja-promotion-reconciled" => "Your paid maximum-level service is complete. You are now a Ninja Master.",
        "maximum-rank-reconciled" => "Your maximum class rank and weapon proficiencies were restored. No CPs were charged.",
        "already-maximum-level" => "You are already at the maximum level. No CPs were charged.",
        "rebirth-level-required" => "You do not meet the level requirement. No CPs were charged.",
        "invalid-rebirth-order" => "That rebirth is not available for your current state. No CPs were charged.",
        "inventory-full" => "Your inventory does not have enough space. No CPs were charged.",
        "insufficient-conquer-points" => "You do not have enough CPs. Nothing was changed.",
        _ => "The service could not be completed. Nothing was charged.",
    };

    private static string ClassName(ushort profession) => profession switch
    {
        11 => "Trojan", 21 => "Warrior", 41 => "Archer", 55 => "Ninja", 132 => "Water Taoist", 142 => "Fire Taoist",
        _ => throw new ArgumentOutOfRangeException(nameof(profession)),
    };
}
