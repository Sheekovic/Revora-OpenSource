using Npgsql;
using Revora.Domain.Accounts;
using Revora.Gameplay.Contracts;
using Revora.Gameplay.Models;
using Revora.Gameplay.Services;
using Revora.Persistence.PostgreSql;

if (args.Length != 1) throw new ArgumentException("Usage: Revora.GameplayPostgreSqlTests <temporary-database>");
string database = args[0];
string username = Environment.GetEnvironmentVariable("REVORA_TEST_DB_USER") ?? "revora-test";
var builder = new NpgsqlConnectionStringBuilder
{
    Host = Environment.GetEnvironmentVariable("REVORA_TEST_DB_SOCKET") ?? "/var/run/postgresql",
    Port = int.TryParse(Environment.GetEnvironmentVariable("REVORA_TEST_DB_PORT"), out int port) ? port : 5432,
    Database = database,
    Username = username,
    Pooling = false,
    Timeout = 5,
    CommandTimeout = 10,
};

Guid accountOne = Guid.NewGuid(), accountTwo = Guid.NewGuid();
Guid characterOne = Guid.NewGuid(), characterTwo = Guid.NewGuid();
Guid source = Guid.Parse("51650009-1002-1004-8000-000000000001");
Guid warehouse = Guid.NewGuid();
var actorOne = new GameplayActor(new AccountId(accountOne), new CharacterUid(8_100_001));
var actorTwo = new GameplayActor(new AccountId(accountTwo), new CharacterUid(8_100_002));
var swordId = new ItemDefinitionId(410_003);
var shopId = new ShopId(10_001);

await using (var connection = new NpgsqlConnection(builder.ConnectionString))
{
    await connection.OpenAsync();
    await using (var ninjaStats = new NpgsqlCommand(
        "SELECT count(*),count(*) FILTER (WHERE (level,strength,agility,vitality,spirit) IN " +
        "((1,1,8,1,0),(15,7,43,6,0),(40,19,106,17,0),(70,33,181,29,0)," +
        "(100,48,256,41,0),(120,57,306,49,0))) FROM gameplay_reference.base_character_stats " +
        "WHERE source_id='51650034-0000-4e56-8000-000000000001' AND profession_family='ninja'",
        connection))
    {
        await using NpgsqlDataReader reader = await ninjaStats.ExecuteReaderAsync();
        Equal(true, await reader.ReadAsync());
        Equal(120L, reader.GetInt64(0));
        Equal(6L, reader.GetInt64(1));
    }
    await using (var ninjaCatalog = new NpgsqlCommand(
        "SELECT count(*),count(*) FILTER (WHERE item_type BETWEEN 601000 AND 601339)," +
        "count(*) FILTER (WHERE item_type BETWEEN 112000 AND 112109)," +
        "count(*) FILTER (WHERE item_type BETWEEN 123000 AND 123109)," +
        "count(*) FILTER (WHERE item_type BETWEEN 135000 AND 135109) " +
        "FROM gameplay_reference.item_types WHERE source_id='51650035-0000-4e56-8000-000000000001'", connection))
    {
        await using NpgsqlDataReader reader = await ninjaCatalog.ExecuteReaderAsync();
        Equal(true, await reader.ReadAsync());
        Equal(548L, reader.GetInt64(0)); Equal(273L, reader.GetInt64(1));
        Equal(88L, reader.GetInt64(2)); Equal(88L, reader.GetInt64(3)); Equal(99L, reader.GetInt64(4));
    }
    Equal(1L, Convert.ToInt64(await ScalarAsync(connection,
        "SELECT count(*) FROM gameplay_reference.live_monster_spawn_points WHERE " +
        "source_id='51650035-0000-4e56-8000-000000000001' AND legacy_uid=400000035 " +
        "AND map_id=1002 AND x=454 AND y=455 AND enabled=false", null)));
    Equal(0L, Convert.ToInt64(await ScalarAsync(connection,
        "SELECT count(*) FROM gameplay.feature_gate_dependencies WHERE feature_key='ninja.native' " +
        "AND depends_on_feature_key='progression.rebirth'", null)));
    await ExecuteAsync(connection, "SET ROLE revora_migrator");
    await ExecuteParameterizedAsync(connection,
        "INSERT INTO gameplay_reference.item_types(source_id,item_type,safe_name,item_class,equipment_slot,required_profession,required_level,minimum_attack,maximum_attack,maximum_durability,silver_price,canonical_record_sha256) " +
        "VALUES($1,$2,'PostgreSqlTrainingSword',1,4,10,5,5,10,100,100,repeat('a',64))",
        source, (long)swordId.Value);
    await ExecuteParameterizedAsync(connection,
        "INSERT INTO gameplay_reference.shop_catalogs(source_id,legacy_shop_id,npc_source_id,npc_legacy_uid,safe_name,currency_type,canonical_record_sha256) " +
        "VALUES($1,$2,$1,10000,'PostgreSqlTestShop','silver',repeat('b',64))",
        source, (long)shopId.Value);
    await ExecuteParameterizedAsync(connection,
        "INSERT INTO gameplay_reference.shop_catalog_items(source_id,legacy_shop_id,position,item_source_id,item_type,purchase_price,sale_price) " +
        "VALUES($1,$2,0,$1,$3,100,25)", source, (long)shopId.Value, (long)swordId.Value);
    await ExecuteParameterizedAsync(connection,
        "INSERT INTO gameplay_reference.warehouse_definitions(source_id,legacy_warehouse_id,npc_source_id,npc_legacy_uid,slot_capacity,canonical_record_sha256) " +
        "VALUES($1,1,$1,10000,40,repeat('c',64))", source);
    await ExecuteAsync(connection, "RESET ROLE");
    await ExecuteParameterizedAsync(connection,
        "INSERT INTO identity.game_accounts(account_id,username,normalized_username,password_hash,password_algorithm,password_version) " +
        "VALUES($1,'pgplayone','pgplayone',repeat('x',32),'test',1),($2,'pgplaytwo','pgplaytwo',repeat('x',32),'test',1)",
        accountOne, accountTwo);
    await ExecuteParameterizedAsync(connection,
        "INSERT INTO game.characters(character_id,account_id,realm_id,name,map_id,x,y,legacy_uid,mesh,profession,level) " +
        "VALUES($1,$2,'4d1e7aac-e046-48bd-88b9-642f7eafef83','PgPlayOne',1002,430,378,$3,1003,10,20)," +
        "($4,$5,'4d1e7aac-e046-48bd-88b9-642f7eafef83','PgPlayTwo',1002,430,378,$6,1003,10,20)",
        characterOne, accountOne, checked((int)actorOne.CharacterUid.Value),
        characterTwo, accountTwo, checked((int)actorTwo.CharacterUid.Value));
    await ExecuteParameterizedAsync(connection,
        "INSERT INTO game.inventory_profiles(character_id,slot_capacity) VALUES($1,4),($2,4)",
        characterOne, characterTwo);
    await ExecuteParameterizedAsync(connection,
        "INSERT INTO game.character_attributes(character_id,strength,agility) VALUES($1,50,50),($2,50,50)",
        characterOne, characterTwo);
    await ExecuteParameterizedAsync(connection,
        "INSERT INTO game.warehouse_vaults(vault_id,owner_character_id,warehouse_source_id,legacy_warehouse_id,slot_capacity) VALUES($1,$2,$3,1,40)",
        warehouse, characterOne, source);
}

await using (var featureStore = new PostgreSqlGameDataStore(builder.Host, builder.Username, database))
{
    Equal(false, await featureStore.IsCharacterFeatureEnabledAsync(actorOne.AccountId,
        checked((int)actorOne.CharacterUid.Value), "world.player.equipment.visibility", default));
    await using var connection = new NpgsqlConnection(builder.ConnectionString);
    await connection.OpenAsync();
    await ExecuteAsync(connection, "SET ROLE revora_migrator");
    await ExecuteParameterizedAsync(connection,
        "INSERT INTO gameplay.feature_canaries(feature_key,account_id,character_id,legacy_character_uid,enabled_by) " +
        "VALUES('world.player.equipment.visibility',$1,$2,$3,'postgresql-test')",
        accountOne, characterOne, checked((int)actorOne.CharacterUid.Value));
    await ExecuteAsync(connection, "RESET ROLE");
    Equal(true, await featureStore.IsCharacterFeatureEnabledAsync(actorOne.AccountId,
        checked((int)actorOne.CharacterUid.Value), "world.player.equipment.visibility", default));
    Equal(false, await featureStore.IsCharacterFeatureEnabledAsync(actorTwo.AccountId,
        checked((int)actorTwo.CharacterUid.Value), "world.player.equipment.visibility", default));
}

await using var store = new PostgreSqlGameplayStore(builder.ConnectionString);
var definitions = new ItemCatalog(new ItemDefinition(swordId, "PostgreSqlTrainingSword", 100,
    EquipmentSlot.RightHand, new ItemRequirements(5, null, 10), new ItemStatBlock(5, 10)));
var shops = new ShopCatalog(new ShopDefinition(shopId,
    new Dictionary<ItemDefinitionId, ShopEntry> { [swordId] = new(swordId, CurrencyKind.Silver, 100, 25) }));
var ids = new SequentialIds();
var currency = new CurrencyService(store);
var shop = new ShopService(store, definitions, shops, ids);
var equipment = new EquipmentService(store, definitions, new DefaultEquipmentRequirementPolicy());
var warehouseService = new WarehouseService(store);

GameplayCommandId grant = Command();
Equal(GameplayResultCode.Success, (await currency.AdjustAsync(grant, actorOne, CurrencyKind.Silver, 1_000, default)).Code);
Equal(true, (await currency.AdjustAsync(grant, actorOne, CurrencyKind.Silver, 1_000, default)).Replayed);

GameplayCommandId buy = Command();
GameplayCommandResult purchase = await shop.BuyAsync(buy, actorOne, shopId, swordId, 2, default);
Equal(GameplayResultCode.Success, purchase.Code);
Equal(true, (await shop.BuyAsync(buy, actorOne, shopId, swordId, 2, default)).Replayed);

IReadOnlyList<ItemInstance> items = await store.ExecuteAsync(tx => tx.GetItems(actorOne.CharacterUid), default);
Equal(2, items.Count);
Equal(GameplayResultCode.Success, (await equipment.EquipAsync(Command(), actorOne, items[0].Id, default)).Code);
Equal(GameplayResultCode.Success, (await warehouseService.DepositAsync(Command(), actorOne, items[1].Id, default)).Code);

ItemInstance warehoused = await store.ExecuteAsync(tx => tx.FindItem(items[1].Id)!, default);
Equal(ItemContainer.Warehouse, warehoused.Location.Container);
Equal(GameplayResultCode.ForeignItem,
    (await warehouseService.WithdrawAsync(Command(), actorTwo, items[1].Id, default)).Code);

GameplayCommandId concurrent = Command();
GameplayCommandResult[] concurrentResults = await Task.WhenAll(
    currency.AdjustAsync(concurrent, actorOne, CurrencyKind.Silver, 10, default).AsTask(),
    currency.AdjustAsync(concurrent, actorOne, CurrencyKind.Silver, 10, default).AsTask());
Equal(1, concurrentResults.Count(result => result.Replayed));
Equal(1, concurrentResults.Count(result => !result.Replayed && result.Succeeded));

var rolledBackId = new ItemInstanceId(Guid.NewGuid());
await ThrowsAsync<InjectedRollbackException>(() => store.ExecuteAsync<int>(tx =>
{
    tx.AddItem(new ItemInstance(rolledBackId, new LegacyItemUid(90_000_001), swordId,
        actorOne.AccountId, actorOne.CharacterUid, ItemLocation.Inventory(), 100));
    throw new InjectedRollbackException();
}, default).AsTask());
Equal<ItemInstance?>(null, await store.ExecuteAsync(tx => tx.FindItem(rolledBackId), default));

await using (var verify = new NpgsqlConnection(builder.ConnectionString))
{
    await verify.OpenAsync();
    Equal(3L, Convert.ToInt64(await ScalarAsync(verify,
        "SELECT count(*) FROM game.currency_ledger WHERE character_id=$1", characterOne)));
    if (Convert.ToInt64(await ScalarAsync(verify,
        "SELECT count(*) FROM game.gameplay_command_receipts", null)) < 6)
        throw new InvalidOperationException("The expected replay receipts were not persisted.");
    Equal(1L, Convert.ToInt64(await ScalarAsync(verify,
        "SELECT count(*) FROM game.warehouse_items WHERE owner_character_id=$1", characterOne)));
    Equal(0L, Convert.ToInt64(await ScalarAsync(verify,
        "SELECT count(*) FROM game.item_instances WHERE item_instance_id=$1", rolledBackId.Value)));
}

// Production-shaped progression/service boundary: immutable owner, canary gate,
// atomic CP charge, replay, rebirth ordering, loadout creation and GM caps.
await using (var setup = new NpgsqlConnection(builder.ConnectionString))
{
    await setup.OpenAsync();
    await ExecuteParameterizedAsync(setup,
        "UPDATE game.inventory_profiles SET slot_capacity=40 WHERE character_id=$1", characterOne);
    await ExecuteParameterizedAsync(setup,
        "INSERT INTO game.character_vitals(character_id,current_health,current_mana) VALUES($1,1,0) ON CONFLICT DO NOTHING",
        characterOne);
    await ExecuteParameterizedAsync(setup,
        "INSERT INTO game.character_progression(character_id) VALUES($1) ON CONFLICT DO NOTHING", characterOne);
    await ExecuteAsync(setup,
        "INSERT INTO gameplay_reference.catalog_releases(release_id,realm_id,manifest_sha256,release_state,activated_at) " +
        "VALUES('51650034-0000-4e56-8000-000000000099','4d1e7aac-e046-48bd-88b9-642f7eafef83',repeat('d',64),'active',clock_timestamp()) " +
        "ON CONFLICT DO NOTHING");
    await ExecuteParameterizedAsync(setup,
        "INSERT INTO game.character_wallets(character_id,currency_type,balance) VALUES($1,'conquer-points',5000000) " +
        "ON CONFLICT(character_id,currency_type) DO UPDATE SET balance=excluded.balance", characterOne);
    await ExecuteAsync(setup,
        "UPDATE gameplay.feature_gates SET gate_state='enabled',required_release_id=(SELECT release_id FROM gameplay_reference.catalog_releases WHERE release_state='active') " +
        "WHERE feature_key='progression.services'");
    await ExecuteParameterizedAsync(setup,
        "INSERT INTO gameplay.feature_canaries(feature_key,account_id,character_id,legacy_character_uid,enabled_by) " +
        "VALUES('progression.services',$2,$1,$3,'progression-test') ON CONFLICT DO NOTHING",
        characterOne, accountOne, checked((int)actorOne.CharacterUid.Value));
    await ExecuteParameterizedAsync(setup,
        "INSERT INTO game.gameplay_role_assignments(account_id,character_id,role,granted_by) " +
        "VALUES($2,$1,'gm','progression-test') ON CONFLICT DO NOTHING", characterOne, accountOne);
}

// Disabled-by-default live combat persistence: atomic HP/receipt, exact replay,
// conflict rejection, restart persistence and immutable authenticated ownership.
await using (var combatSetup = new NpgsqlConnection(builder.ConnectionString))
{
    await combatSetup.OpenAsync();
    await ExecuteParameterizedAsync(combatSetup,
        "INSERT INTO game.character_vitals(character_id,current_health,current_mana) VALUES($1,100,0),($2,100,0) " +
        "ON CONFLICT(character_id) DO UPDATE SET current_health=100,row_version=1", characterOne, characterTwo);
    await ExecuteParameterizedAsync(combatSetup,
        "INSERT INTO game.character_stat_snapshots(character_id,character_row_version,inventory_revision," +
        "maximum_health,maximum_mana,minimum_attack,maximum_attack,defense,magic_attack,magic_defense,accuracy,dodge) " +
        "SELECT character_id,row_version,0,100,0,1,10,0,0,0,1,0 FROM game.characters WHERE character_id IN ($1,$2) " +
        "ON CONFLICT(character_id) DO UPDATE SET maximum_health=100", characterOne, characterTwo);
    await ExecuteAsync(combatSetup,
        "UPDATE gameplay.feature_gates SET gate_state='enabled',required_release_id=(SELECT release_id FROM gameplay_reference.catalog_releases WHERE release_state='active') " +
        "WHERE feature_key IN ('world.catalog.complete','items.definitions','inventory','equipment.stats','monsters','combat.pk','combat.runtime'); " +
        "UPDATE gameplay.feature_rollouts SET rollout_scope='global' WHERE feature_key IN " +
        "('world.catalog.complete','items.definitions','inventory','equipment.stats','monsters','combat.pk'); " +
        "UPDATE gameplay.feature_rollouts SET rollout_scope='canary' WHERE feature_key='combat.runtime'");
    await ExecuteParameterizedAsync(combatSetup,
        "INSERT INTO gameplay.feature_canaries(feature_key,account_id,character_id,legacy_character_uid,enabled_by) " +
        "VALUES('combat.runtime',$1,$2,$3,'combat-test') ON CONFLICT DO NOTHING",
        accountOne, characterOne, checked((int)actorOne.CharacterUid.Value));
}

LiveCombatEntitySnapshot targetBefore;
await using (var combat = new PostgreSqlCombatRuntime(builder.ConnectionString))
{
    Equal(true, await combat.IsEnabledAsync(actorOne.AccountId, actorOne.CharacterUid, "combat.runtime", default));
    Equal(false, await combat.IsEnabledAsync(actorTwo.AccountId, actorTwo.CharacterUid, "combat.runtime", default));
    await using (var accountCanary = new NpgsqlConnection(builder.ConnectionString))
    {
        await accountCanary.OpenAsync();
        await ExecuteParameterizedAsync(accountCanary,
            "DELETE FROM gameplay.feature_canaries WHERE feature_key='combat.runtime' AND account_id=$1", accountOne);
        await ExecuteParameterizedAsync(accountCanary,
            "INSERT INTO gameplay.account_feature_canaries(feature_key,account_id,enabled_by) " +
            "VALUES('combat.runtime',$1,'account-canary-test')", accountOne);
    }
    Equal(true, await combat.IsEnabledAsync(actorOne.AccountId, actorOne.CharacterUid, "combat.runtime", default));
    Equal(false, await combat.IsEnabledAsync(actorTwo.AccountId, actorTwo.CharacterUid, "combat.runtime", default));
    Guid recoveryItem = Guid.NewGuid();
    const uint recoveryItemUid = 8_199_001;
    await using (var recoverySetup = new NpgsqlConnection(builder.ConnectionString))
    {
        await recoverySetup.OpenAsync();
        await ExecuteParameterizedAsync(recoverySetup,
            "UPDATE game.character_vitals SET current_health=30,current_mana=0 WHERE character_id=$1",
            characterOne);
        await ExecuteParameterizedAsync(recoverySetup,
            "INSERT INTO game.item_instances(item_instance_id,legacy_item_uid,owner_character_id,item_source_id,item_type," +
            "durability,maximum_durability) SELECT $1,$2,$3,source_id,item_type,1,1 FROM gameplay_reference.item_types " +
            "WHERE item_type=1000000 ORDER BY source_id LIMIT 1",
            recoveryItem, checked((int)recoveryItemUid), characterOne);
        await ExecuteParameterizedAsync(recoverySetup,
            "INSERT INTO game.item_locations(item_instance_id,owner_character_id,location_kind,slot) " +
            "VALUES($1,$2,'inventory',10)", recoveryItem, characterOne);
    }
    LiveCombatEntitySnapshot recoveryBefore = await combat.LoadPlayerAsync(actorOne, 0, 1, default)
        ?? throw new InvalidOperationException("Recovery-item player state was not loaded.");
    Guid recoveryOperation = Guid.NewGuid();
    var recoveryMutation = new RecoveryItemMutation(recoveryOperation, actorOne, recoveryItemUid,
        1_000_000, 70, 0, recoveryBefore.Version, recoveryBefore.PersistenceVersion,
        recoveryBefore.CurrentHealth);
    RecoveryItemPersistenceResult recoveryApplied = await combat.ConsumeRecoveryItemAsync(
        recoveryMutation, default);
    Equal(RecoveryItemPersistenceCode.Applied, recoveryApplied.Code);
    Equal(100L, recoveryApplied.CurrentHealth);
    Equal(recoveryBefore.PersistenceVersion + 1, recoveryApplied.PersistenceVersion);
    Equal(RecoveryItemPersistenceCode.Replay,
        (await combat.ConsumeRecoveryItemAsync(recoveryMutation, default)).Code);
    await using (var recoveryVerify = new NpgsqlConnection(builder.ConnectionString))
    {
        await recoveryVerify.OpenAsync();
        Equal(1L, Convert.ToInt64(await ScalarAsync(recoveryVerify,
            "SELECT count(*) FROM game.item_instances WHERE item_instance_id=$1", recoveryItem)));
        Equal(0L, Convert.ToInt64(await ScalarAsync(recoveryVerify,
            "SELECT count(*) FROM game.item_locations WHERE item_instance_id=$1", recoveryItem)));
        Equal(1L, Convert.ToInt64(await ScalarAsync(recoveryVerify,
            "SELECT count(*) FROM game.consumed_item_instances WHERE item_instance_id=$1", recoveryItem)));
        Equal(1L, Convert.ToInt64(await ScalarAsync(recoveryVerify,
            "SELECT count(*) FROM game.gameplay_command_receipts WHERE command_id=$1", recoveryOperation)));
    }
    await using (var dependency = new NpgsqlConnection(builder.ConnectionString))
    {
        await dependency.OpenAsync();
        await ExecuteAsync(dependency, "UPDATE gameplay.feature_gates SET gate_state='disabled' WHERE feature_key='monsters'");
        Equal(false, await combat.IsEnabledAsync(actorOne.AccountId, actorOne.CharacterUid, "combat.runtime", default));
        await ExecuteAsync(dependency, "UPDATE gameplay.feature_gates SET gate_state='enabled' WHERE feature_key='monsters'");
    }
    await using (var sitting = new NpgsqlConnection(builder.ConnectionString))
    {
        await sitting.OpenAsync();
        await ExecuteParameterizedAsync(sitting,
            "UPDATE game.character_attributes SET stamina=100 WHERE character_id=$1", characterTwo);
        await ExecuteParameterizedAsync(sitting,
            "UPDATE game.character_attributes SET stamina=70 WHERE character_id=$1", characterOne);
    }
    targetBefore = await combat.LoadPlayerAsync(actorTwo, 0, 1, default)
        ?? throw new InvalidOperationException("Combat target was not loaded.");
    Guid operation = Guid.NewGuid();
    var mutation = new CombatMutation(operation, actorOne, LiveEntityKind.Player, actorTwo.CharacterUid.Value,
        targetBefore.Map, targetBefore.X, targetBefore.Y, targetBefore.Version,
        targetBefore.CurrentHealth, 7, targetBefore.CurrentHealth - 7, false, "normal-attack",
        null, 100, 70, true);
    CombatPersistenceResult sittingApplied = await combat.CommitAsync(mutation, default);
    Equal(CombatPersistenceCode.Applied, sittingApplied.Code);
    Equal((ushort)70, sittingApplied.TargetStaminaAfter!.Value);
    Equal(true, sittingApplied.SittingInterrupted);
    CombatPersistenceResult sittingReplay = await combat.CommitAsync(mutation, default);
    Equal(CombatPersistenceCode.Replay, sittingReplay.Code);
    Equal((ushort)70, sittingReplay.TargetStaminaAfter!.Value);
    Equal(CombatPersistenceCode.Conflict, (await combat.CommitAsync(mutation with
    {
        OperationId = Guid.NewGuid(), ExpectedTargetVersion = targetBefore.Version,
        PreviousHealth = targetBefore.CurrentHealth
    }, default)).Code);
    Equal(true, await combat.TryUpdateStaminaAsync(actorOne, 70, 73, default));
    Equal((ushort)73, (await combat.LoadStaminaAsync(actorOne, default))!.Value);
    Equal(false, await combat.TryUpdateStaminaAsync(actorOne, 70, 76, default));
}
await using (var restartedCombat = new PostgreSqlCombatRuntime(builder.ConnectionString))
{
    LiveCombatEntitySnapshot restarted = await restartedCombat.LoadPlayerAsync(actorTwo, 0, 2, default)
        ?? throw new InvalidOperationException("Combat target was not restored after runtime restart.");
    Equal(targetBefore.CurrentHealth - 7, restarted.CurrentHealth);
    Equal(targetBefore.Version + 1, restarted.Version);

    // A movement update advances only the live entity version. PostgreSQL
    // character_vitals.row_version remains the durable combat version and must
    // still authorize the next monster hit.
    var movedRegistry = new LiveCombatEntityRegistry();
    var movedPlayer = new LiveCombatEntity(restarted);
    Equal(true, movedRegistry.TryRegister(movedPlayer));
    Equal(true, movedRegistry.TryRegister(new LiveCombatEntity(new LiveCombatEntitySnapshot(
        450_001, LiveEntityKind.Monster, null, restarted.Map,
        checked((ushort)(restarted.X + 1)), restarted.Y, 20, 20, true, 1, 0))));
    Equal(true, await movedRegistry.RelocateAsync(actorTwo.CharacterUid.Value, restarted.Map,
        checked((ushort)(restarted.X + 2)), restarted.Y, 3_000, default));
    Equal(restarted.Version + 1, movedPlayer.Snapshot().Version);
    Equal(restarted.PersistenceVersion, movedPlayer.Snapshot().PersistenceVersion);

    var movedPipeline = new LiveCombatPipeline(movedRegistry,
        new ConservativeNormalDamageFormula(1), new DenyByDefaultPvpPolicy(), restartedCombat);
    var pheasant = new OwnerMonsterTemplate(1, "Pheasant", OwnerMonsterBehavior.HuntPlayers,
        104, 1, 33, 0, 0, 0, 5, 6, 1, 1, OwnerMonsterAttackType.Melee,
        0, 0, true, 3, 10, 20, 800, 10_000, true).Validate();
    var playerStats = new PlayerCombatStatistics(1, 5, 2, 0, 4, 7, 1, 0, 0, 2, 0).Validate();
    LiveCombatResult movedHit = await movedPipeline.ExecuteMonsterAttackAsync(
        new MonsterCombatRequest(Guid.NewGuid(), 450_001, actorTwo, pheasant,
            playerStats, 3_500, 70, false), default);
    Equal(LiveCombatResultCode.Applied, movedHit.Code);
    Equal(restarted.PersistenceVersion + 1, movedPlayer.Snapshot().PersistenceVersion);
    Equal(restarted.Version + 2, movedPlayer.Snapshot().Version);

    await using (var deadPlayer = new NpgsqlConnection(builder.ConnectionString))
    {
        await deadPlayer.OpenAsync();
        await ExecuteParameterizedAsync(deadPlayer,
            "UPDATE game.character_vitals SET current_health=0 WHERE character_id=$1", characterOne);
        await ExecuteParameterizedAsync(deadPlayer,
            "UPDATE game.character_attributes SET stamina=0 WHERE character_id=$1", characterOne);
    }
    (int MaximumHealth, ushort Stamina, long PersistenceVersion)? revived =
        await restartedCombat.RevivePlayerAsync(actorOne, default);
    Equal(true, revived.HasValue);
    Equal(true, revived!.Value.MaximumHealth > 0);
    Equal((ushort)100, revived.Value.Stamina);
    Equal(null, await restartedCombat.RevivePlayerAsync(actorOne, default));
}

// Native Ninja creation initializes every required row in the same serializable
// transaction and retry cannot create a duplicate character or UID.
Guid ninjaAccount = Guid.NewGuid();
Guid blockedNinjaAccount = Guid.NewGuid();
CharacterState? ninjaCharacter = null;
long ninjaUidSequenceBefore;
await using (var ninjaSetup = new NpgsqlConnection(builder.ConnectionString))
{
    await ninjaSetup.OpenAsync();
    ninjaUidSequenceBefore = Convert.ToInt64(await ScalarAsync(ninjaSetup,
        "SELECT last_value FROM game.characters_legacy_uid_seq", null));
    await ExecuteParameterizedAsync(ninjaSetup,
        "INSERT INTO identity.game_accounts(account_id,username,normalized_username,password_hash,password_algorithm,password_version) " +
        "VALUES($1,'pgninja','pgninja',repeat('x',32),'test',1)," +
        "($2,'pgnotninja','pgnotninja',repeat('x',32),'test',1)", ninjaAccount, blockedNinjaAccount);
    Equal(1L, Convert.ToInt64(await ScalarAsync(ninjaSetup,
        "SELECT count(*) FROM identity.game_accounts WHERE normalized_username='pgninja' AND account_id=$1", ninjaAccount)));
    Equal(0L, Convert.ToInt64(await ScalarAsync(ninjaSetup,
        "SELECT count(*) FROM game.characters WHERE account_id=$1", ninjaAccount)));
    await ExecuteParameterizedAsync(ninjaSetup,
        "INSERT INTO gameplay.account_feature_canaries(feature_key,account_id,enabled_by) " +
        "VALUES('ninja.native',$1,'postgresql-test')", ninjaAccount);
    await ExecuteAsync(ninjaSetup,
        "UPDATE gameplay.feature_gates SET gate_state='enabled',required_release_id=" +
        "(SELECT release_id FROM gameplay_reference.catalog_releases WHERE release_state='active') " +
        "WHERE feature_key='ninja.native'; " +
        "UPDATE gameplay.feature_rollouts SET rollout_scope='canary' WHERE feature_key='ninja.native'");
}
await using (var characterStore = new PostgreSqlGameDataStore(
    Environment.GetEnvironmentVariable("REVORA_TEST_DB_SOCKET") ?? "/var/run/postgresql", username, database))
{
    Equal(null, await characterStore.TryCreateCharacterAsync(blockedNinjaAccount,
        "BlockedNinja", 1003, 50, default));
    Task<CharacterState?> firstNinja = characterStore.TryCreateCharacterAsync(ninjaAccount, "PgNinja", 1003, 50, default);
    Task<CharacterState?> duplicateNinja = characterStore.TryCreateCharacterAsync(ninjaAccount, "PgNinjaTwo", 1004, 50, default);
    CharacterState?[] attempts = await Task.WhenAll(firstNinja, duplicateNinja);
    Equal(1, attempts.Count(value => value is not null));
    ninjaCharacter = attempts.Single(value => value is not null)!;
    Equal((ushort)50, ninjaCharacter.Profession);
    Equal(1002, ninjaCharacter.MapId);
}

await using (var ninjaCombatSetup = new NpgsqlConnection(builder.ConnectionString))
{
    await ninjaCombatSetup.OpenAsync();
    await ExecuteParameterizedAsync(ninjaCombatSetup,
        "INSERT INTO gameplay_reference.item_types(source_id,item_type,safe_name,item_class,equipment_slot,required_profession,required_level,minimum_attack,maximum_attack,maximum_durability,silver_price,canonical_record_sha256) " +
        "VALUES('51650034-0000-4e56-8000-000000000001',601000,'MetalKatana',601,4,50,5,7,20,1599,102,repeat('f',64)) ON CONFLICT DO NOTHING");
    await ExecuteParameterizedAsync(ninjaCombatSetup,
        "UPDATE game.characters SET level=120 WHERE character_id=$1", ninjaCharacter!.Id);
    await ExecuteParameterizedAsync(ninjaCombatSetup,
        "INSERT INTO game.character_skills(character_id,magic_source_id,magic_type,level) " +
        "SELECT $1,source_id,magic_type,level FROM gameplay_reference.magic_types WHERE magic_type=6000 AND level=0 " +
        "ORDER BY source_id LIMIT 1 ON CONFLICT DO NOTHING", ninjaCharacter!.Id);
    for (short slot = 4; slot <= 5; slot++)
    {
        Guid item = Guid.NewGuid();
        await ExecuteParameterizedAsync(ninjaCombatSetup,
            "INSERT INTO game.item_instances(item_instance_id,owner_character_id,item_source_id,item_type,durability,maximum_durability) " +
            "VALUES($1,$2,'51650034-0000-4e56-8000-000000000001',601000,1599,1599)",
            item, ninjaCharacter.Id);
        await ExecuteParameterizedAsync(ninjaCombatSetup,
            "INSERT INTO game.item_locations(item_instance_id,owner_character_id,location_kind,slot) VALUES($1,$2,'equipment',$3)",
            item, ninjaCharacter.Id, slot);
    }
}
var ninjaActor = new GameplayActor(new AccountId(ninjaAccount),
    new CharacterUid(checked((uint)ninjaCharacter!.LegacyUid)));
await using (var ninjaCombat = new PostgreSqlCombatRuntime(builder.ConnectionString))
{
    NinjaCombatant loaded = await ninjaCombat.LoadNinjaCombatantAsync(ninjaActor, default)
        ?? throw new InvalidOperationException("The persisted Ninja combatant was not loaded.");
    Equal((byte)50, loaded.Profession); Equal((byte)120, loaded.Level);
    Equal((uint)601000, loaded.RightHandItemType!.Value);
    Equal((uint)601000, loaded.LeftHandItemType!.Value);
    Guid operation = Guid.NewGuid();
    var skillMutation = new NinjaCombatMutation(operation, ninjaActor, 6000, 0, 100, 70,
        LiveEntityKind.Monster, 450_001, new CombatMapKey(1002, 0),
        checked((ushort)ninjaCharacter.X), checked((ushort)ninjaCharacter.Y), 431, 378,
        1, 33, 1, 32, false, 1);
    Equal(CombatPersistenceCode.Applied, (await ninjaCombat.CommitSkillAsync(skillMutation, default)).Code);
    Equal(CombatPersistenceCode.Replay, (await ninjaCombat.CommitSkillAsync(skillMutation, default)).Code);
    PersistedSkillState skill = await ninjaCombat.ReadSkillAsync(ninjaActor, 6000, default)
        ?? throw new InvalidOperationException("The persisted Ninja skill was not restored.");
    Equal(1u, skill.Experience);
    NinjaCombatant reloaded = await ninjaCombat.LoadNinjaCombatantAsync(ninjaActor, default)
        ?? throw new InvalidOperationException("The persisted Ninja combatant was not restored.");
    Equal((ushort)70, reloaded.Stamina);
    await using (var advance = new NpgsqlConnection(builder.ConnectionString))
    {
        await advance.OpenAsync();
        await ExecuteParameterizedAsync(advance,
            "UPDATE game.character_attributes SET stamina=100 WHERE character_id=$1", ninjaCharacter.Id);
        await ExecuteParameterizedAsync(advance,
            "UPDATE game.character_skills SET experience=299999 WHERE character_id=$1 AND magic_type=6000",
            ninjaCharacter.Id);
    }
    Equal(CombatPersistenceCode.Applied, (await ninjaCombat.CommitSkillAsync(skillMutation with
    {
        OperationId = Guid.NewGuid(), PreviousStamina = 100, RemainingStamina = 70
    }, default)).Code);
    PersistedSkillState advancedSkill = await ninjaCombat.ReadSkillAsync(ninjaActor, 6000, default)
        ?? throw new InvalidOperationException("The advanced Ninja skill was not restored.");
    Equal((ushort)1, advancedSkill.Level); Equal(0u, advancedSkill.Experience);
    Equal(NinjaPromotionResultCode.InvalidTransition,
        (await ninjaCombat.PromoteNinjaAsync(Guid.NewGuid(), ninjaActor, 52, default)).Code);
    for (byte profession = 51; profession <= 55; profession++)
    {
        Guid promotion = Guid.NewGuid();
        NinjaPromotionResult promoted = await ninjaCombat.PromoteNinjaAsync(promotion, ninjaActor,
            profession, default);
        Equal(NinjaPromotionResultCode.Applied, promoted.Code);
        Equal(NinjaPromotionResultCode.Replay,
            (await ninjaCombat.PromoteNinjaAsync(promotion, ninjaActor, profession, default)).Code);
    }
}
await using (var ninjaVerify = new NpgsqlConnection(builder.ConnectionString))
{
    await ninjaVerify.OpenAsync();
    Equal(1L, Convert.ToInt64(await ScalarAsync(ninjaVerify,
        "SELECT count(*) FROM game.characters WHERE account_id=$1", ninjaAccount)));
    Equal(1L, Convert.ToInt64(await ScalarAsync(ninjaVerify,
        "SELECT count(*) FROM game.ninja_character_initialization WHERE account_id=$1", ninjaAccount)));
    Equal(1L, Convert.ToInt64(await ScalarAsync(ninjaVerify,
        "SELECT count(*) FROM game.ninja_canary_test_grants WHERE account_id=$1 AND cardinality(legacy_item_uids)=5 " +
        "AND conquer_points_granted=1000000", ninjaAccount)));
    Equal(5L, Convert.ToInt64(await ScalarAsync(ninjaVerify,
        "SELECT count(*) FROM game.item_instances i JOIN game.item_locations l USING(item_instance_id) " +
        "WHERE i.owner_character_id=$1 AND l.location_kind='inventory' AND i.bound", ninjaCharacter.Id)));
    Equal(7L, Convert.ToInt64(await ScalarAsync(ninjaVerify,
        "SELECT count(*) FROM game.character_skills WHERE character_id=$1 AND magic_type IN(6000,6001,6002,6003,6004,6010,6011)",
        ninjaCharacter.Id)));
    Equal(1000000L, Convert.ToInt64(await ScalarAsync(ninjaVerify,
        "SELECT balance FROM game.character_wallets WHERE character_id=$1 AND currency_type='conquer-points'",
        ninjaCharacter.Id)));
    Equal(ninjaUidSequenceBefore + 1, Convert.ToInt64(await ScalarAsync(ninjaVerify,
        "SELECT last_value FROM game.characters_legacy_uid_seq", null)));
    Equal(3L, Convert.ToInt64(await ScalarAsync(ninjaVerify,
        "SELECT count(*) FROM game.character_wallets w JOIN game.characters c USING(character_id) WHERE c.account_id=$1", ninjaAccount)));
    Equal(1L, Convert.ToInt64(await ScalarAsync(ninjaVerify,
        "SELECT count(*) FROM game.combat_operation_receipts WHERE attacker_character_id=$1", characterOne)));
    Equal(5L, Convert.ToInt64(await ScalarAsync(ninjaVerify,
        "SELECT count(*) FROM game.ninja_promotion_receipts WHERE character_id=$1", ninjaCharacter.Id)));
    Equal(55L, Convert.ToInt64(await ScalarAsync(ninjaVerify,
        "SELECT profession FROM game.characters WHERE character_id=$1", ninjaCharacter.Id)));
}

int clearedInventoryCount = 0;
await using (var progression = new PostgreSqlProgressionRuntime(builder.ConnectionString))
{
    Equal(true, await progression.IsEnabledAsync(actorOne.AccountId, actorOne.CharacterUid, default));
    await using (var deadState = new NpgsqlConnection(builder.ConnectionString))
    {
        await deadState.OpenAsync();
        await ExecuteParameterizedAsync(deadState,
            "UPDATE game.character_vitals SET current_health=0 WHERE character_id=$1", characterOne);
    }
    ProgressionCharacterSnapshot deadSnapshot = await progression.ReadAsync(actorOne.AccountId,
        actorOne.CharacterUid, default) ?? throw new InvalidOperationException("The dead character snapshot was not loaded.");
    Equal(0, deadSnapshot.CurrentHealth);
    Equal(true, deadSnapshot.MaximumHealth > 0);

    GameplayCommandId maximum = Command();
    ProgressionMutationResult maxResult = await progression.PurchaseAsync(maximum.Value, actorOne.AccountId,
        actorOne.CharacterUid, ProgressionPurchaseKind.MaximumLevel, null, default);
    Equal(true, maxResult.Succeeded); Equal((byte)130, maxResult.Character.Level);
    Equal(4_000_000L, maxResult.Character.ConquerPoints);
    ProgressionMutationResult replay = await progression.PurchaseAsync(maximum.Value, actorOne.AccountId,
        actorOne.CharacterUid, ProgressionPurchaseKind.MaximumLevel, null, default);
    Equal(true, replay.Replayed); Equal(4_000_000L, replay.Character.ConquerPoints);
    ProgressionMutationResult alreadyMaximum = await progression.PurchaseAsync(Guid.NewGuid(), actorOne.AccountId,
        actorOne.CharacterUid, ProgressionPurchaseKind.MaximumLevel, null, default);
    Equal(false, alreadyMaximum.Succeeded); Equal("already-maximum-level", alreadyMaximum.Category);
    Equal(4_000_000L, alreadyMaximum.Character.ConquerPoints);

    await using (var legacyMaximum = new NpgsqlConnection(builder.ConnectionString))
    {
        await legacyMaximum.OpenAsync();
        await ExecuteParameterizedAsync(legacyMaximum,
            "UPDATE game.characters SET profession=14 WHERE character_id=$1", characterOne);
        await ExecuteParameterizedAsync(legacyMaximum,
            "DELETE FROM game.character_proficiencies WHERE character_id=$1 AND proficiency_type=420", characterOne);
    }
    ProgressionMutationResult reconciledMaximum = await progression.PurchaseAsync(Guid.NewGuid(), actorOne.AccountId,
        actorOne.CharacterUid, ProgressionPurchaseKind.MaximumLevel, null, default);
    Equal(true, reconciledMaximum.Succeeded); Equal("maximum-rank-reconciled", reconciledMaximum.Category);
    Equal(0L, reconciledMaximum.ChargedConquerPoints); Equal(4_000_000L, reconciledMaximum.Character.ConquerPoints);
    Equal((ushort)15, reconciledMaximum.Character.Profession);
    await using (var reconciledVerify = new NpgsqlConnection(builder.ConnectionString))
    {
        await reconciledVerify.OpenAsync();
        Equal(2L, Convert.ToInt64(await ScalarAsync(reconciledVerify,
            "SELECT count(*) FROM game.character_proficiencies WHERE character_id=$1 " +
            "AND proficiency_type IN (410,420) AND level=20 AND experience=0", characterOne)));
    }

    ProgressionMutationResult first = await progression.PurchaseAsync(Guid.NewGuid(), actorOne.AccountId,
        actorOne.CharacterUid, ProgressionPurchaseKind.FirstRebirth, 11, default);
    Equal(true, first.Succeeded); Equal((byte)1, first.Character.RebirthCount);
    Equal((byte)15, first.Character.Level); Equal((ushort)11, first.Character.Profession);
    ProgressionMutationResult invalidSecond = await progression.PurchaseAsync(Guid.NewGuid(), actorOne.AccountId,
        actorOne.CharacterUid, ProgressionPurchaseKind.SecondRebirth, 21, default);
    Equal(false, invalidSecond.Succeeded); Equal(3_000_000L, invalidSecond.Character.ConquerPoints);

    ProgressionMutationResult gm = await progression.SetGmCapsAsync(Guid.NewGuid(), actorOne.AccountId,
        actorOne.CharacterUid, default);
    Equal(true, gm.Succeeded); Equal(PostgreSqlProgressionRuntime.MaximumSafeSilver, gm.Character.Silver);
    Equal(PostgreSqlProgressionRuntime.MaximumSafeConquerPoints, gm.Character.ConquerPoints);
    ProgressionMutationResult repeatedGm = await progression.SetGmCapsAsync(Guid.NewGuid(), actorOne.AccountId,
        actorOne.CharacterUid, default);
    Equal(true, repeatedGm.Succeeded); Equal(PostgreSqlProgressionRuntime.MaximumSafeSilver, repeatedGm.Character.Silver);
    Equal(PostgreSqlProgressionRuntime.MaximumSafeConquerPoints, repeatedGm.Character.ConquerPoints);

    ProgressionMutationResult loadout = await progression.PurchaseAsync(Guid.NewGuid(), actorOne.AccountId,
        actorOne.CharacterUid, ProgressionPurchaseKind.ClassLoadout, 11, default);
    Equal(true, loadout.Succeeded); Equal(7, loadout.CreatedItemUids.Count);
    Equal(7, loadout.CreatedItemUids.Distinct().Count());
    Equal((ushort)15, loadout.Character.Profession);
    await using (var loadoutVerify = new NpgsqlConnection(builder.ConnectionString))
    {
        await loadoutVerify.OpenAsync();
        Equal(2L, Convert.ToInt64(await ScalarAsync(loadoutVerify,
            "SELECT count(*) FROM game.character_proficiencies WHERE character_id=$1 " +
            "AND proficiency_type IN (410,420) AND level=20 AND experience=0", characterOne)));
    }

    Guid concurrentLoadout = Guid.NewGuid();
    ProgressionMutationResult[] concurrentLoadouts = await Task.WhenAll(
        progression.PurchaseAsync(concurrentLoadout, actorOne.AccountId, actorOne.CharacterUid,
            ProgressionPurchaseKind.ClassLoadout, 21, default),
        progression.PurchaseAsync(concurrentLoadout, actorOne.AccountId, actorOne.CharacterUid,
            ProgressionPurchaseKind.ClassLoadout, 21, default));
    Equal(1, concurrentLoadouts.Count(value => value.Replayed));
    Equal(7, concurrentLoadouts.Single(value => !value.Replayed).CreatedItemUids.Count);

    ProgressionMutationResult maxAgain = await progression.PurchaseAsync(Guid.NewGuid(), actorOne.AccountId,
        actorOne.CharacterUid, ProgressionPurchaseKind.MaximumLevel, null, default);
    Equal(true, maxAgain.Succeeded); Equal((byte)130, maxAgain.Character.Level);
    ProgressionMutationResult second = await progression.PurchaseAsync(Guid.NewGuid(), actorOne.AccountId,
        actorOne.CharacterUid, ProgressionPurchaseKind.SecondRebirth, 21, default);
    Equal(true, second.Succeeded); Equal((byte)2, second.Character.RebirthCount);
    Equal((ushort)21, second.Character.Profession); Equal((byte)15, second.Character.Level);

    ProgressionMutationResult notGm = await progression.SetGmCapsAsync(Guid.NewGuid(), actorTwo.AccountId,
        actorTwo.CharacterUid, default);
    Equal(false, notGm.Succeeded); Equal("not-authorized", notGm.Category);

    Guid clearOperation = Guid.NewGuid();
    GmInventoryClearResult cleared = await progression.ClearGmInventoryAsync(clearOperation,
        actorOne.AccountId, actorOne.CharacterUid, default);
    Equal(true, cleared.Succeeded); Equal(false, cleared.Replayed);
    Equal(true, cleared.RemovedItemUids.Count >= 14);
    clearedInventoryCount = cleared.RemovedItemUids.Count;
    Equal(cleared.RemovedItemUids.Count, cleared.RemovedItemUids.Distinct().Count());
    GmInventoryClearResult clearReplay = await progression.ClearGmInventoryAsync(clearOperation,
        actorOne.AccountId, actorOne.CharacterUid, default);
    Equal(true, clearReplay.Succeeded); Equal(true, clearReplay.Replayed);
    Equal(true, cleared.RemovedItemUids.SequenceEqual(clearReplay.RemovedItemUids));
    GmInventoryClearResult clearEmpty = await progression.ClearGmInventoryAsync(Guid.NewGuid(),
        actorOne.AccountId, actorOne.CharacterUid, default);
    Equal(true, clearEmpty.Succeeded); Equal("empty-inventory", clearEmpty.Category);
    GmInventoryClearResult clearNotGm = await progression.ClearGmInventoryAsync(Guid.NewGuid(),
        actorTwo.AccountId, actorTwo.CharacterUid, default);
    Equal(false, clearNotGm.Succeeded); Equal("not-authorized", clearNotGm.Category);
}

await using (var progressionVerify = new NpgsqlConnection(builder.ConnectionString))
{
    await progressionVerify.OpenAsync();
    Equal((long)clearedInventoryCount, Convert.ToInt64(await ScalarAsync(progressionVerify,
        "SELECT count(*) FROM game.item_instances WHERE owner_character_id=$1 AND bound", characterOne)));
    Equal(14L, Convert.ToInt64(await ScalarAsync(progressionVerify,
        "SELECT count(DISTINCT legacy_item_uid) FROM game.item_instances WHERE owner_character_id=$1 AND bound", characterOne)));
    Equal(2L, Convert.ToInt64(await ScalarAsync(progressionVerify,
        "SELECT count(*) FROM operations.progression_service_audit WHERE character_id=$1 AND event_type='gm-cap'", characterOne)));
    Equal(0L, Convert.ToInt64(await ScalarAsync(progressionVerify,
        "SELECT count(*) FROM game.item_locations WHERE owner_character_id=$1 AND location_kind='inventory'", characterOne)));
    Equal(14L, Convert.ToInt64(await ScalarAsync(progressionVerify,
        "SELECT count(*) FROM game.item_locations WHERE owner_character_id=$1 AND location_kind='gm-discarded'", characterOne)));
    Equal(2L, Convert.ToInt64(await ScalarAsync(progressionVerify,
        "SELECT count(*) FROM game.gm_inventory_clear_receipts WHERE character_id=$1", characterOne)));
}

await using (var ground = new PostgreSqlGroundDropRuntime(builder.ConnectionString))
{
    Guid death = Guid.NewGuid();
    var reward = new MonsterRewardPlan(death, 1, actorOne, 450_010, 1,
        new CombatMapKey(1002, 0), 430, 378, 25, 25,
        [
            new PlannedGroundDrop(GroundDropKind.Item, swordId.Value, 0, source, 0, 0, 0, 430, 378),
            new PlannedGroundDrop(GroundDropKind.Silver, 1090020, 250, null, 0, 0, 0, 431, 378),
            new PlannedGroundDrop(GroundDropKind.Item, swordId.Value, 0, source, 0, 0, 0, 432, 378),
        ]);
    MonsterRewardCommitResult committed = await ground.CommitRewardAsync(reward, default);
    Equal(true, committed.Applied); Equal(3, committed.Drops.Count);
    MonsterRewardCommitResult replay = await ground.CommitRewardAsync(reward, default);
    Equal(true, replay.Replayed); Equal(3, replay.Drops.Count);
    GroundDropSnapshot item = committed.Drops.Single(value => value.Kind == GroundDropKind.Item && value.X == 430);
    GroundDropSnapshot activeItem = committed.Drops.Single(value => value.Kind == GroundDropKind.Item && value.X == 432);
    GroundPickupRequest pickup = new(Guid.NewGuid(), actorOne, item.GroundUid,
        new CombatMapKey(1002, 0), 430, 378, item.ProtectedUntil.AddSeconds(1));
    GroundPickupResult picked = await ground.PickupAsync(pickup, default);
    Equal(GroundPickupCode.Applied, picked.Code); Equal(item.LegacyItemUid, picked.LegacyItemUid);
    GroundPickupResult pickedReplay = await ground.PickupAsync(pickup, default);
    Equal(GroundPickupCode.Replay, pickedReplay.Code); Equal(item.LegacyItemUid, pickedReplay.LegacyItemUid);
    await using (var economyRead = new PostgreSqlEconomyRuntime(builder.ConnectionString))
    {
        GameplayPlayerSnapshot snapshot = await economyRead.ReadPlayerAsync(actorOne, default) ??
            throw new InvalidOperationException("Ground-item owner disappeared.");
        Equal(true, snapshot.Items.Any(value => value.LegacyUid.Value == item.LegacyItemUid));
        Equal(false, snapshot.Items.Any(value => value.LegacyUid.Value == activeItem.LegacyItemUid));
    }
    await using (var forceExpiry = new NpgsqlConnection(builder.ConnectionString))
    {
        await forceExpiry.OpenAsync();
        await ExecuteParameterizedAsync(forceExpiry,
            "UPDATE game.world_ground_drops SET protected_until=clock_timestamp()-interval '2 seconds'," +
            "expires_at=clock_timestamp()-interval '1 second' WHERE ground_uid=$1",
            checked((long)activeItem.GroundUid));
    }
    Equal(1, (await ground.ExpireDueAsync(default)).Count(value => value.GroundUid == activeItem.GroundUid));
    await using (var economyRead = new PostgreSqlEconomyRuntime(builder.ConnectionString))
    {
        GameplayPlayerSnapshot snapshot = await economyRead.ReadPlayerAsync(actorOne, default) ??
            throw new InvalidOperationException("Expired-ground-item owner disappeared.");
        Equal(false, snapshot.Items.Any(value => value.LegacyUid.Value == activeItem.LegacyItemUid));
    }
    GroundDropSnapshot silver = committed.Drops.Single(value => value.Kind == GroundDropKind.Silver);
    GroundPickupResult protectedResult = await ground.PickupAsync(new(Guid.NewGuid(), actorTwo,
        silver.GroundUid, new CombatMapKey(1002, 0), 430, 378, silver.ProtectedUntil.AddSeconds(-1)), default);
    Equal(GroundPickupCode.Protected, protectedResult.Code);
    GroundPickupResult silverResult = await ground.PickupAsync(new(Guid.NewGuid(), actorOne,
        silver.GroundUid, new CombatMapKey(1002, 0), 430, 378, silver.ProtectedUntil.AddSeconds(1)), default);
    Equal(GroundPickupCode.WalletFull, silverResult.Code);
    await using (var wallet = new NpgsqlConnection(builder.ConnectionString))
    {
        await wallet.OpenAsync();
        await ExecuteParameterizedAsync(wallet,
            "UPDATE game.character_wallets SET balance=1000 WHERE character_id=$1 AND currency_type='silver'",
            characterOne);
    }
    silverResult = await ground.PickupAsync(new(Guid.NewGuid(), actorOne,
        silver.GroundUid, new CombatMapKey(1002, 0), 430, 378, silver.ProtectedUntil.AddSeconds(1)), default);
    Equal(GroundPickupCode.Applied, silverResult.Code);

    var raceReward = reward with
    {
        DeathId = Guid.NewGuid(), SpawnGeneration = 2, MonsterUid = 450_011,
        Drops = [new PlannedGroundDrop(GroundDropKind.Item, swordId.Value, 0, source, 0, 0, 0, 433, 378)]
    };
    GroundDropSnapshot raceDrop = (await ground.CommitRewardAsync(raceReward, default)).Drops.Single();
    GroundPickupResult[] race = await Task.WhenAll(
        ground.PickupAsync(new(Guid.NewGuid(), actorOne, raceDrop.GroundUid,
            new CombatMapKey(1002, 0), 430, 378, raceDrop.ProtectedUntil.AddSeconds(1)), default),
        ground.PickupAsync(new(Guid.NewGuid(), actorTwo, raceDrop.GroundUid,
            new CombatMapKey(1002, 0), 430, 378, raceDrop.ProtectedUntil.AddSeconds(1)), default));
    Equal(1, race.Count(value => value.Code == GroundPickupCode.Applied));
    Equal(1, race.Count(value => value.Code == GroundPickupCode.Missing));
}

await using (var proficiencyReset = new NpgsqlConnection(builder.ConnectionString))
{
    await proficiencyReset.OpenAsync();
    await ExecuteParameterizedAsync(proficiencyReset,
        "DELETE FROM game.character_proficiencies WHERE character_id=$1", characterOne);
}

await using (var combatRewards = new PostgreSqlCombatRuntime(builder.ConnectionString))
{
    Guid death = Guid.NewGuid();
    var reward = new MonsterRewardPlan(death, 7, actorOne, 450_017, 1,
        new CombatMapKey(1002, 0), 433, 378, 40, 40,
        [new PlannedGroundDrop(GroundDropKind.Item, swordId.Value, 0, source, 0, 0, 0, 433, 378)]);
    var mutation = new CombatMutation(Guid.NewGuid(), actorOne, LiveEntityKind.Monster,
        450_017, new CombatMapKey(1002, 0), 433, 378, 1, 10, 10, 0, true,
        "normal-attack", reward, Progression: new CombatProgressionPlan(14, 10));
    CombatPersistenceResult applied = await combatRewards.CommitAsync(mutation, default);
    Equal(CombatPersistenceCode.Applied, applied.Code); Equal(true, applied.Reward?.Applied);
    Equal(14L, applied.Progression!.ExperienceAwarded);
    Equal((ushort)410, applied.Progression.Proficiencies.Single().ProficiencyType);
    Equal(0L, applied.Progression.Proficiencies.Single().ExperienceAwarded);
    CombatPersistenceResult replay = await combatRewards.CommitAsync(mutation, default);
    Equal(CombatPersistenceCode.Replay, replay.Code); Equal(true, replay.Reward?.Replayed);
    Equal(0L, replay.Progression!.Proficiencies.Single().ExperienceAwarded);
    IReadOnlyDictionary<uint, long> restartedGenerations =
        await combatRewards.LoadNextMonsterSpawnGenerationsAsync(default);
    Equal(8L, restartedGenerations[450_017]);
    Equal(false, restartedGenerations.ContainsKey(450_099));

    CombatPersistenceResult secondHit = await combatRewards.CommitAsync(mutation with
    {
        OperationId = Guid.NewGuid(), TargetUid = 450_019, TargetX = 435,
        PreviousHealth = 20, Damage = 10, RemainingHealth = 10, Killed = false, Reward = null
    }, default);
    Equal(CombatPersistenceCode.Applied, secondHit.Code);
    Equal((byte)1, secondHit.Progression!.Proficiencies.Single().LevelAfter);
    Equal(35L, secondHit.Progression.Proficiencies.Single().ExperienceAwarded);

    Guid failedOperation = Guid.NewGuid(), failedDeath = Guid.NewGuid();
    var invalidReward = reward with
    {
        DeathId = failedDeath,
        SpawnGeneration = 8,
        MonsterUid = 450_018,
        Drops = [new PlannedGroundDrop(GroundDropKind.Item, swordId.Value, 0, Guid.NewGuid(), 0, 0, 0, 434, 378)]
    };
    var invalidMutation = mutation with { OperationId = failedOperation, TargetUid = 450_018,
        TargetX = 434, Reward = invalidReward };
    bool rolledBack = false;
    try { _ = await combatRewards.CommitAsync(invalidMutation, default); }
    catch (Exception error) when (error is InvalidOperationException or Npgsql.PostgresException)
    { rolledBack = true; }
    Equal(true, rolledBack);
    await using var verify = new NpgsqlConnection(builder.ConnectionString);
    await verify.OpenAsync();
    Equal(0L, Convert.ToInt64(await ScalarAsync(verify,
        "SELECT count(*) FROM game.combat_operation_receipts WHERE operation_id=$1", failedOperation)));
    Equal(0L, Convert.ToInt64(await ScalarAsync(verify,
        "SELECT count(*) FROM game.monster_death_resolutions WHERE death_id=$1", failedDeath)));
    Equal(1L, Convert.ToInt64(await ScalarAsync(verify,
        "SELECT count(*) FROM game.character_proficiencies WHERE character_id=$1 " +
        "AND proficiency_type=410 AND level=1 AND experience=0", characterOne)));
}

Console.WriteLine("Revora.GameplayPostgreSqlTests: ownership, receipts, replay, concurrency, rollback, inventory, wallet, equipment, shop, warehouse, progression, combat restart persistence, atomic Ninja creation, skill persistence, stages 50-55 promotion, loadouts, GM caps, GM inventory clear, atomic monster rewards and ground pickup passed");

static GameplayCommandId Command() => new(Guid.NewGuid());

static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
{
    await using var command = new NpgsqlCommand(sql, connection);
    await command.ExecuteNonQueryAsync();
}

static async Task ExecuteParameterizedAsync(NpgsqlConnection connection, string sql, params object[] values)
{
    await using var command = new NpgsqlCommand(sql, connection);
    foreach (object value in values) command.Parameters.AddWithValue(value);
    await command.ExecuteNonQueryAsync();
}

static async Task<object?> ScalarAsync(NpgsqlConnection connection, string sql, object? value)
{
    await using var command = new NpgsqlCommand(sql, connection);
    if (value is not null) command.Parameters.AddWithValue(value);
    return await command.ExecuteScalarAsync();
}

static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"Expected {expected}; actual {actual}.");
}

static async Task ThrowsAsync<TException>(Func<Task> action) where TException : Exception
{
    try { await action(); }
    catch (TException) { return; }
    throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
}

sealed class ItemCatalog(ItemDefinition value) : IItemDefinitionCatalog
{
    public ItemDefinition? Find(ItemDefinitionId id) => id == value.Id ? value : null;
}

sealed class ShopCatalog(ShopDefinition value) : IShopCatalog
{
    public ShopDefinition? Find(ShopId id) => id == value.Id ? value : null;
}

sealed class SequentialIds : IItemInstanceIdGenerator
{
    private int value = 90_100_000;
    public ItemIdentity Next()
    {
        int next = Interlocked.Increment(ref value);
        return new(new ItemInstanceId(Guid.NewGuid()), new LegacyItemUid(checked((uint)next)));
    }
}

sealed class InjectedRollbackException : Exception;
