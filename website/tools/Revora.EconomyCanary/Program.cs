using System.Security.Cryptography;
using System.Text;
using Npgsql;
using Revora.Domain.Accounts;
using Revora.Gameplay.Contracts;
using Revora.Gameplay.Models;
using Revora.Gameplay.Services;
using Revora.Persistence.PostgreSql;

if (args.Length != 7 || args[0] is not ("grant" or "rollback"))
{
    Console.Error.WriteLine("Usage: Revora.EconomyCanary <grant|rollback> <database> <account-id> <character-uid> <item-type> <silver> <operation-id>");
    return 2;
}

string operation = args[0];
string database = args[1];
if (!Guid.TryParse(args[2], out Guid rawAccountId) || rawAccountId == Guid.Empty ||
    !uint.TryParse(args[3], out uint rawCharacterUid) || rawCharacterUid == 0 ||
    !uint.TryParse(args[4], out uint rawItemType) || rawItemType == 0 ||
    !long.TryParse(args[5], out long silver) || silver <= 0 ||
    !Guid.TryParse(args[6], out Guid operationId) || operationId == Guid.Empty)
{
    Console.Error.WriteLine("The bounded canary operation arguments are invalid.");
    return 2;
}

string username = Environment.UserName;
string connectionString = PostgreSqlEconomyRuntime.BuildLocalPeerConnectionString(
    "/var/run/postgresql", username, database);
var actor = new GameplayActor(new AccountId(rawAccountId), new CharacterUid(rawCharacterUid));
await using var runtime = new PostgreSqlEconomyRuntime(connectionString);
EconomyCatalogSnapshot catalog = await runtime.LoadCatalogAsync(default);
var definitions = new ItemCatalog(catalog.Items);
var service = new OwnerCanaryGrantService(runtime.Store, definitions, new RuntimeIds(runtime));
GameplayPlayerSnapshot before = await runtime.ReadPlayerAsync(actor, default) ??
    throw new InvalidOperationException("The immutable AccountId/Character UID owner pair was not found.");
ItemInstance? targetBefore = before.Items.SingleOrDefault(item => item.DefinitionId.Value == rawItemType);

GameplayCommandResult result;
if (operation == "grant")
{
    result = await service.GrantAsync(new GameplayCommandId(operationId), actor,
        new ItemDefinitionId(rawItemType), silver, default);
}
else
{
    if (targetBefore is null) throw new InvalidOperationException("The reviewed grant item is not owned by the canary character.");
    result = await service.RollbackAsync(new GameplayCommandId(RollbackCommand(operationId)), actor,
        targetBefore.Id, silver, default);
}
if (!result.Succeeded) throw new InvalidOperationException($"The bounded canary {operation} failed safely ({result.Code}).");

GameplayPlayerSnapshot after = await runtime.ReadPlayerAsync(actor, default) ??
    throw new InvalidOperationException("The canary owner disappeared after the transaction.");
ItemInstance auditedItem = operation == "grant"
    ? after.Items.Single(item => item.Id == result.ItemInstanceId)
    : targetBefore!;
await RecordAuditAsync(connectionString, operationId, operation, actor, auditedItem,
    before.Character.Balance(CurrencyKind.Silver), after.Character.Balance(CurrencyKind.Silver));
Console.WriteLine($"Canary {operation} complete: characterUid={actor.CharacterUid.Value}; itemUid={auditedItem.LegacyUid.Value}; " +
    $"itemType={auditedItem.DefinitionId.Value}; silverBefore={before.Character.Balance(CurrencyKind.Silver)}; " +
    $"silverAfter={after.Character.Balance(CurrencyKind.Silver)}; replayed={result.Replayed}.");
return 0;

static Guid RollbackCommand(Guid operationId)
{
    byte[] input = [.. operationId.ToByteArray(), .. "revora-owner-canary-rollback"u8.ToArray()];
    byte[] digest = SHA256.HashData(input);
    try { return new Guid(digest.AsSpan(0, 16)); }
    finally { CryptographicOperations.ZeroMemory(input); CryptographicOperations.ZeroMemory(digest); }
}

static async Task RecordAuditAsync(string connectionString, Guid operationId, string eventType,
    GameplayActor actor, ItemInstance item, long before, long after)
{
    await using var connection = new NpgsqlConnection(connectionString);
    await connection.OpenAsync();
    await using NpgsqlCommand command = connection.CreateCommand();
    command.CommandText =
        "INSERT INTO operations.owner_test_grant_events(operation_id,event_type,account_id,character_id," +
        "legacy_character_uid,item_instance_id,legacy_item_uid,item_type,silver_before,silver_after)" +
        " SELECT $1,$2,$3,c.character_id,$4,$5,$6,$7,$8,$9 FROM game.characters c" +
        " WHERE c.account_id=$3 AND c.legacy_uid=$4 ON CONFLICT(operation_id,event_type) DO NOTHING";
    command.Parameters.AddWithValue(operationId);
    command.Parameters.AddWithValue(eventType);
    command.Parameters.AddWithValue(actor.AccountId.Value);
    command.Parameters.AddWithValue(checked((int)actor.CharacterUid.Value));
    command.Parameters.AddWithValue(item.Id.Value);
    command.Parameters.AddWithValue(checked((int)item.LegacyUid.Value));
    command.Parameters.AddWithValue((long)item.DefinitionId.Value);
    command.Parameters.AddWithValue(before);
    command.Parameters.AddWithValue(after);
    int changed = await command.ExecuteNonQueryAsync();
    if (changed == 0)
    {
        command.Parameters.Clear();
        command.CommandText = "SELECT count(*) FROM operations.owner_test_grant_events WHERE operation_id=$1 AND event_type=$2";
        command.Parameters.AddWithValue(operationId);
        command.Parameters.AddWithValue(eventType);
        if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 1)
            throw new InvalidOperationException("The append-only canary audit event was not recorded.");
    }
}

file sealed class RuntimeIds(PostgreSqlEconomyRuntime runtime) : IItemInstanceIdGenerator
{
    public ItemIdentity Next() => runtime.NextItemIdentity();
}

file sealed class ItemCatalog(IReadOnlyDictionary<ItemDefinitionId, ItemDefinition> items) : IItemDefinitionCatalog
{
    public ItemDefinition? Find(ItemDefinitionId id) => items.GetValueOrDefault(id);
}
