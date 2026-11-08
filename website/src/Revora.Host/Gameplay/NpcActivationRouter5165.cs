using Revora.NativeLogin.Core;

namespace Revora.Host.Gameplay;

public readonly record struct NpcActivationOutcome5165(
    bool Accepted, uint NpcUid, NpcInteractionKind5165 Kind, string Category, int MapId, int NpcX, int NpcY,
    int PlayerX, int PlayerY, int PacketCount);

public sealed class NpcActivationRouter5165
{
    public const uint GuideNpcUid = 10000;
    private readonly GameplayWorld5165 world;
    private readonly IReadOnlySet<uint> reviewedDeclarativeNpcs;
    private readonly uint guideNpcUid;

    public NpcActivationRouter5165(GameplayWorld5165 world, IReadOnlySet<uint>? reviewedDeclarativeNpcs = null,
        uint guideNpcUid = GuideNpcUid)
    {
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.reviewedDeclarativeNpcs = reviewedDeclarativeNpcs ?? new HashSet<uint>();
        this.guideNpcUid = guideNpcUid == 0 ? throw new ArgumentOutOfRangeException(nameof(guideNpcUid)) : guideNpcUid;
    }

    public async Task<NpcActivationOutcome5165> ActivateAsync(GameplaySession5165 session,
        ItemEconomySession5165? economy, ProgressionServiceSession5165? progression,
        NpcInteraction5165 interaction, CancellationToken token)
    {
        session.ResetNpcDialog();
        if (!world.TryValidateNpcActivation(session, interaction, out NpcReference5165 npc, out string category))
            return new(false, interaction.Id, NpcInteractionKind5165.None, category, session.Character.MapId,
                0, 0, session.Character.X, session.Character.Y, 0);

        NpcInteractionKind5165 kind;
        int packetCount;
        if (npc.LegacyUid == ProgressionServiceSession5165.NpcUid)
        {
            kind = NpcInteractionKind5165.ProgressionService;
            if (progression is null)
                await world.OpenUnavailableDialogAsync(session, npc, token);
            else
                await progression.OpenAsync(session, npc, token);
            packetCount = progression?.Enabled == true ? 8 : 4;
            category = progression?.Enabled == true ? "progression-service" : "safe-unavailable";
        }
        else if (reviewedDeclarativeNpcs.Contains(npc.LegacyUid))
        {
            kind = NpcInteractionKind5165.DeclarativeDialog;
            await world.OpenUnavailableDialogAsync(session, npc, token);
            packetCount = 4;
        }
        else if (economy?.HasWarehouseForNpc(npc.LegacyUid) == true)
        {
            kind = NpcInteractionKind5165.Warehouse;
            session.BeginNpcInteraction(kind, npc.LegacyUid);
            await session.SendAsync(GamePacketsAfterAuthentication5165.EncodeNpcWindowAction(
                checked((uint)session.Character.LegacyUid), 4,
                GamePacketsAfterAuthentication5165.OpenWarehouseAction), token);
            packetCount = 1;
        }
        else if (economy?.HasShopForNpc(npc.LegacyUid) == true)
        {
            kind = NpcInteractionKind5165.Shop;
            session.BeginNpcInteraction(kind, npc.LegacyUid);
            await session.SendAsync(GamePacketsAfterAuthentication5165.EncodeNpcWindowAction(
                checked((uint)session.Character.LegacyUid), npc.LegacyUid,
                GamePacketsAfterAuthentication5165.OpenShopAction), token);
            packetCount = 1;
        }
        else if (npc.LegacyUid == guideNpcUid)
        {
            kind = NpcInteractionKind5165.GuideDialog;
            await world.OpenGuideDialogAsync(session, npc, token);
            packetCount = 5;
        }
        else
        {
            kind = NpcInteractionKind5165.DeclarativeDialog;
            await world.OpenUnavailableDialogAsync(session, npc, token);
            packetCount = 4;
            category = "safe-unavailable";
        }

        return new(true, npc.LegacyUid, kind, category, npc.MapId, npc.X, npc.Y,
            session.Character.X, session.Character.Y, packetCount);
    }

    public Task<NpcActivationOutcome5165> ActivateAsync(GameplaySession5165 session,
        ItemEconomySession5165? economy, NpcInteraction5165 interaction, CancellationToken token) =>
        ActivateAsync(session, economy, null, interaction, token);
}
