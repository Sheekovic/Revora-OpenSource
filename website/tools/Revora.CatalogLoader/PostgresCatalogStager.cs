using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace Revora.CatalogLoader;

internal static class PostgresCatalogStager
{
    public static async Task ApplyAsync(ReviewedCatalog catalog, ReviewedGameplayReference reference, DatabaseSecret secret)
    {
        await using var dataSource = NpgsqlDataSource.Create(secret.ConnectionString);
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync();
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        try
        {
            await RequireMigrationsAsync(connection, transaction);
            await CreateStageAsync(connection, transaction);
            await CopyStageAsync(connection, catalog);
            await CopyReferenceStageAsync(connection, reference);
            await StageMetadataAsync(connection, transaction, catalog, reference, secret.RealmId);
            await StageReferencesAsync(connection, transaction, catalog, reference);
            await StageCatalogsAsync(connection, transaction, catalog, reference);
            await ValidateAppliedCountsAsync(connection, transaction, catalog, reference);
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private static async Task RequireMigrationsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        var expected = new Dictionary<int, string>
        {
            [11] = "50752779e622a259f411ae12bfab8f4b01eceea6832ff300d4b3de7edb06ab62",
            [12] = "a221928dfcbcbecd479086ab573f6f3fca91f73a5b23a46b26b72c0fee1b265e",
            [13] = "2b1090335ab7afe16c63f88b0e06a13880773c3ba4079f082d2d5d96c2a50900",
            [14] = "7506f2a1d05cae05420699886d24792ecb00bb01422adf38104bb1fbcaaf187a",
            [15] = "bf7d671981f172cebc2701ffabdc4e39c28f132c469bef8b0a70f623f3a1483e",
            [16] = "4590387aee90d5118f936cffe591f914ff740e1ac8ad3a208125f578272e7af6",
            [17] = "32269d22c6fb31a7452228b45130bbd67b886a80637b62b1f867f440496bf6da",
            [18] = "f57b85019aecfd566564d6e0a9344208a53217c3fb54bc7cc1a9393f1955a987",
            [19] = "91f47fcef20426b5d972183afabcf70c7b1854c5fb7b4bf7e7c7f8c4cf6abad5",
            [20] = "24764bdf854f9294b44ceacb8adbae1c704ed2a4d811193843115a5082502dc0",
            [21] = "32bfe554143a1c33c46b3cb4689935509e80f6d8ca215d8f3b3bb68ecd46d9ac",
            [22] = "2264540c0fd06be21edc83f2c04268f1139d21556aad667afc5c6015badec595",
            [37] = "7646e4b16e0dd1a4167d370c0a7fe58328ef7191507610ff7951610e7adac3c8",
            [38] = "d7388f31fce9a917c3bb16f3440dec07e4121293224fb763c55d0c6e90f1c0e1",
            [39] = "20607476730524eb17b4ea5408f301d438ac09eb6f713b3aecde9668238b4e9f",
        };
        await using NpgsqlCommand command = Command(connection, transaction,
            "SELECT version,checksum_sha256 FROM operations.schema_migrations WHERE version BETWEEN 11 AND 22 OR version IN (37,38,39,40) ORDER BY version");
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
        var actual = new Dictionary<int, string>();
        while (await reader.ReadAsync()) actual.Add(reader.GetInt32(0), reader.GetString(1));
        if (actual.Count != expected.Count || expected.Any(row => !actual.TryGetValue(row.Key, out string? hash) ||
            !System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(Convert.FromHexString(row.Value), Convert.FromHexString(hash))))
            throw new InvalidDataException("Database migration ledger V011 through V022 and V037-V040 is incomplete or has an unexpected checksum.");
    }

    private static async Task CreateStageAsync(NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        await using NpgsqlCommand command = Command(connection, transaction,
            """
            CREATE TEMP TABLE reviewed_catalog_stage(category text NOT NULL, ordinal integer NOT NULL, record jsonb NOT NULL, record_sha256 text NOT NULL, PRIMARY KEY(category,ordinal)) ON COMMIT DROP;
            CREATE TEMP TABLE reviewed_reference_stage(category text NOT NULL, ordinal integer NOT NULL, record jsonb NOT NULL, record_sha256 text NOT NULL, PRIMARY KEY(category,ordinal)) ON COMMIT DROP;
            """);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task CopyReferenceStageAsync(NpgsqlConnection connection, ReviewedGameplayReference reference)
    {
        await using NpgsqlBinaryImporter writer = await connection.BeginBinaryImportAsync(
            "COPY reviewed_reference_stage(category,ordinal,record,record_sha256) FROM STDIN (FORMAT BINARY)");
        foreach (ReviewedCategory category in reference.Categories.Values)
        {
            for (int ordinal = 0; ordinal < category.Rows.Count; ordinal++)
            {
                JsonElement row = category.Rows[ordinal];
                await writer.StartRowAsync();
                await writer.WriteAsync(category.Name, NpgsqlDbType.Text);
                await writer.WriteAsync(ordinal, NpgsqlDbType.Integer);
                await writer.WriteAsync(row.GetRawText(), NpgsqlDbType.Jsonb);
                await writer.WriteAsync(ReviewedCatalog.RecordHash(row), NpgsqlDbType.Text);
            }
        }
        await writer.CompleteAsync();
    }

    private static async Task CopyStageAsync(NpgsqlConnection connection, ReviewedCatalog catalog)
    {
        await using NpgsqlBinaryImporter writer = await connection.BeginBinaryImportAsync(
            "COPY reviewed_catalog_stage(category,ordinal,record,record_sha256) FROM STDIN (FORMAT BINARY)");
        foreach (ReviewedCategory category in catalog.Categories.Values)
        {
            for (int ordinal = 0; ordinal < category.Rows.Count; ordinal++)
            {
                JsonElement row = category.Rows[ordinal];
                await writer.StartRowAsync();
                await writer.WriteAsync(category.Name, NpgsqlDbType.Text);
                await writer.WriteAsync(ordinal, NpgsqlDbType.Integer);
                await writer.WriteAsync(row.GetRawText(), NpgsqlDbType.Jsonb);
                await writer.WriteAsync(ReviewedCatalog.RecordHash(row), NpgsqlDbType.Text);
            }
        }
        await writer.CompleteAsync();
    }

    private static async Task StageMetadataAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        ReviewedCatalog catalog, ReviewedGameplayReference reference, Guid realmId)
    {
        await ExecuteAsync("""
            INSERT INTO gameplay_reference.import_sources(source_id,archive_sha256,content_sha256,classification)
            VALUES($1,$2,$3,'reviewed-reference-data') ON CONFLICT DO NOTHING
            """, catalog.SourceId, catalog.SourceSha256, catalog.ManifestSha256);
        await ExecuteAsync("""
            INSERT INTO gameplay_reference.source_artifacts(artifact_sha256,safe_filename,byte_length,media_type,format_classification,parser_name,parser_version,review_state)
            VALUES($1,'ReVora-Essentials.zip',$2,'application/zip','owner-archive','Revora.OwnerEssentialsImport',1,'reviewed') ON CONFLICT DO NOTHING
            """, catalog.SourceSha256, catalog.SourceBytes);
        await ExecuteAsync("""
            INSERT INTO gameplay_reference.import_batches(batch_id,source_artifact_sha256,canonical_manifest_sha256,parser_name,parser_version,validation_state,completed_at)
            VALUES($1,$2,$3,'Revora.OwnerEssentialsImport',1,'validated',clock_timestamp()) ON CONFLICT DO NOTHING
            """, catalog.BatchId, catalog.SourceSha256, catalog.ManifestSha256);
        await ExecuteAsync("""
            INSERT INTO gameplay_reference.catalog_releases(release_id,realm_id,manifest_sha256,release_state)
            VALUES($1,$2,$3,'validated') ON CONFLICT DO NOTHING
            """, catalog.ReleaseId, realmId, catalog.ManifestSha256);
        await ExecuteAsync("""
            INSERT INTO gameplay_reference.import_sources(source_id,archive_sha256,content_sha256,classification)
            VALUES($1,$2,$3,'reviewed-reference-data') ON CONFLICT DO NOTHING
            """, reference.SourceId, reference.ArchiveSha256, reference.SourceSha256);
        await ExecuteAsync("""
            INSERT INTO gameplay_reference.source_artifacts(artifact_sha256,safe_filename,byte_length,media_type,format_classification,parser_name,parser_version,review_state)
            VALUES($1,'revora.zip',$2,'application/zip','owner-archive','Revora.OwnerGameplayImport',1,'reviewed') ON CONFLICT DO NOTHING
            """, reference.ArchiveSha256, reference.ArchiveBytes);
        await ExecuteAsync("""
            INSERT INTO gameplay_reference.source_artifacts(artifact_sha256,safe_filename,byte_length,media_type,format_classification,parent_artifact_sha256,parser_name,parser_version,review_state)
            VALUES($1,'revora.sql',$2,'application/sql','owner-database',$3,'Revora.OwnerGameplayImport',1,'reviewed') ON CONFLICT DO NOTHING
            """, reference.SourceSha256, reference.SourceBytes, reference.ArchiveSha256);
        await ExecuteAsync("""
            INSERT INTO gameplay_reference.import_batches(batch_id,source_artifact_sha256,canonical_manifest_sha256,parser_name,parser_version,validation_state,completed_at)
            VALUES($1,$2,$3,'Revora.OwnerGameplayImport',1,'validated',clock_timestamp()) ON CONFLICT DO NOTHING
            """, reference.BatchId, reference.SourceSha256, reference.ManifestSha256);

        string[] fullyStagedCategories =
        [
            "items", "item-upgrades", "monsters", "drops", "equipment-drops", "magic-types", "experience", "character-stats",
        ];
        foreach (string name in fullyStagedCategories)
        {
            ReviewedCategory category = catalog.Categories[name];
            await using NpgsqlCommand categoryCommand = Command(connection, transaction, """
                INSERT INTO gameplay_reference.catalog_release_categories
                    (release_id,category,batch_id,expected_row_count,actual_row_count,content_sha256)
                VALUES($1,$2,$3,$4,$4,$5) ON CONFLICT DO NOTHING
                """);
            categoryCommand.Parameters.AddWithValue(catalog.ReleaseId);
            categoryCommand.Parameters.AddWithValue(category.Name);
            categoryCommand.Parameters.AddWithValue(catalog.BatchId);
            categoryCommand.Parameters.AddWithValue(category.Rows.Count);
            categoryCommand.Parameters.AddWithValue(category.ContentSha256);
            await categoryCommand.ExecuteNonQueryAsync();
        }
        foreach (string name in new[] { "customshop", "itemadd" })
        {
            ReviewedCategory category = reference.Categories[name];
            await using NpgsqlCommand categoryCommand = Command(connection, transaction, """
                INSERT INTO gameplay_reference.catalog_release_categories
                    (release_id,category,batch_id,expected_row_count,actual_row_count,content_sha256)
                VALUES($1,$2,$3,$4,$4,$5) ON CONFLICT DO NOTHING
                """);
            categoryCommand.Parameters.AddWithValue(catalog.ReleaseId);
            categoryCommand.Parameters.AddWithValue("owner-sql." + category.Name);
            categoryCommand.Parameters.AddWithValue(reference.BatchId);
            categoryCommand.Parameters.AddWithValue(category.Rows.Count);
            categoryCommand.Parameters.AddWithValue(category.ContentSha256);
            await categoryCommand.ExecuteNonQueryAsync();
        }

        async Task ExecuteAsync(string sql, params object[] values)
        {
            await using NpgsqlCommand command = Command(connection, transaction, sql);
            foreach (object value in values) command.Parameters.AddWithValue(value);
            await command.ExecuteNonQueryAsync();
        }
    }

    private static async Task StageReferencesAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        ReviewedCatalog catalog, ReviewedGameplayReference reference)
    {
        string[] statements =
        [
            """
            INSERT INTO gameplay_reference.npcs(source_id,legacy_uid,npc_type,flags,face,map_id,x,y,active_month,active_begin,active_end,destination_map,destination_x,destination_y,duration)
            SELECT $1,(r.record->>'legacyUid')::bigint,(r.record->>'npcType')::bigint,(r.record->>'flags')::integer,
                (r.record->>'face')::integer,(r.record->>'mapId')::integer,(r.record->>'x')::integer,(r.record->>'y')::integer,
                (r.record->>'activeMonth')::integer,(r.record->>'activeBegin')::integer,(r.record->>'activeEnd')::integer,
                (r.record->>'destinationMap')::integer,(r.record->>'destinationX')::integer,(r.record->>'destinationY')::integer,
                (r.record->>'duration')::integer
            FROM reviewed_reference_stage r JOIN reviewed_catalog_stage m
              ON m.category='maps' AND (m.record->>'Id')::integer=(r.record->>'mapId')::integer
            WHERE r.category='npcs' ON CONFLICT DO NOTHING
            """,
            """
            INSERT INTO gameplay_reference.portals(source_id,legacy_id,source_map,source_x,source_y,destination_map,destination_x,destination_y)
            SELECT $1,(r.record->>'legacyId')::bigint,(r.record->>'sourceMap')::integer,(r.record->>'sourceX')::integer,
                (r.record->>'sourceY')::integer,(r.record->>'destinationMap')::integer,(r.record->>'destinationX')::integer,
                (r.record->>'destinationY')::integer
            FROM reviewed_reference_stage r
            JOIN reviewed_catalog_stage sm ON sm.category='maps' AND (sm.record->>'Id')::integer=(r.record->>'sourceMap')::integer
            JOIN reviewed_catalog_stage dm ON dm.category='maps' AND (dm.record->>'Id')::integer=(r.record->>'destinationMap')::integer
            WHERE r.category='portals' ON CONFLICT DO NOTHING
            """,
            """
            INSERT INTO gameplay_reference.shop_entries(source_id,legacy_id,item_type,price,currency_type)
            SELECT $1,(record->>'legacyId')::bigint,(record->>'itemType')::bigint,(record->>'price')::bigint,(record->>'currencyType')::bigint
            FROM reviewed_reference_stage WHERE category='customshop' ON CONFLICT DO NOTHING
            """,
            """
            INSERT INTO gameplay_reference.item_upgrade_rules(source_id,legacy_unique_id,base_item_type,plus,health,minimum_damage,maximum_damage,
                defense,magic_damage,magic_defense,accuracy,dodge)
            SELECT $1,(record->>'legacyUniqueId')::bigint,(record->>'baseItemType')::bigint,(record->>'plus')::smallint,
                (record->>'health')::integer,(record->>'minimumDamage')::integer,(record->>'maximumDamage')::integer,
                (record->>'defense')::integer,(record->>'magicDamage')::integer,(record->>'magicDefense')::integer,
                (record->>'accuracy')::integer,(record->>'dodge')::integer
            FROM reviewed_reference_stage WHERE category='itemadd' ON CONFLICT DO NOTHING
            """,
        ];
        foreach (string sql in statements)
        {
            await using NpgsqlCommand command = Command(connection, transaction, sql);
            command.Parameters.AddWithValue(reference.SourceId);
            await command.ExecuteNonQueryAsync();
        }
    }

    private static async Task StageCatalogsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        ReviewedCatalog catalog, ReviewedGameplayReference reference)
    {
        foreach (string sql in Statements)
        {
            await using NpgsqlCommand command = Command(connection, transaction, sql);
            command.Parameters.AddWithValue(catalog.SourceId);
            command.Parameters.AddWithValue(catalog.ReleaseId);
            command.Parameters.AddWithValue(reference.SourceId);
            await command.ExecuteNonQueryAsync();
        }
    }

    private static readonly string[] Statements =
    [
        """
            INSERT INTO gameplay_reference.maps(source_id,legacy_map_id,safe_name,map_flags,canonical_record_sha256)
            SELECT $1,(record->>'Id')::integer,left(regexp_replace(record->>'Path','^.*/','','g'),128),(record->>'Flags')::bigint,record_sha256
            FROM reviewed_catalog_stage WHERE category='maps' ON CONFLICT DO NOTHING
        """,
        """
            INSERT INTO gameplay_reference.map_release_entries(release_id,legacy_map_id,source_id)
            SELECT $2,(m.record->>'Id')::integer,$1 FROM reviewed_catalog_stage m
            JOIN reviewed_catalog_stage d ON d.category='dmaps' AND lower(d.record->>'Path')=lower(m.record->>'Path')
            WHERE m.category='maps' ON CONFLICT DO NOTHING
        """,
        """
            INSERT INTO gameplay_reference.source_artifacts(artifact_sha256,safe_filename,byte_length,media_type,format_classification,
            parent_artifact_sha256,parser_name,parser_version,review_state)
            SELECT record->>'Sha256',left(regexp_replace(record->>'Path','^.*/','','g'),160),(record->>'Length')::bigint,
            'application/octet-stream','dmap',NULL,'Revora.OwnerEssentialsImport',1,'reviewed'
            FROM reviewed_catalog_stage WHERE category='dmaps' ON CONFLICT DO NOTHING
        """,
        """
            INSERT INTO gameplay_reference.map_documents(source_id,legacy_map_id,document_id,dmap_artifact_sha256,dmap_byte_length,width,height,format_version)
            SELECT $1,(m.record->>'Id')::integer,(m.record->>'Id')::integer,d.record->>'Sha256',(d.record->>'Length')::bigint,
            (d.record->>'Width')::integer,(d.record->>'Height')::integer,
            CASE d.record->>'Format' WHEN 'numeric-1003' THEN '1003' WHEN 'numeric-1004' THEN '1004'
            WHEN 'dmap-100' THEN 'DMAP100' WHEN 'dmap-101' THEN 'DMAP101' ELSE NULL END
            FROM reviewed_catalog_stage m JOIN reviewed_catalog_stage d ON d.category='dmaps' AND lower(d.record->>'Path')=lower(m.record->>'Path')
            WHERE m.category='maps' ON CONFLICT DO NOTHING
        """,
        """
            INSERT INTO gameplay_reference.item_types(source_id,item_type,safe_name,item_class,equipment_slot,required_profession,required_level,required_gender,required_strength,required_agility,
            minimum_attack,maximum_attack,defense,magic_attack,magic_defense,accuracy,dodge,health_bonus,mana_bonus,maximum_durability,silver_price,conquer_point_price,canonical_record_sha256)
            SELECT $1,(record->>'Id')::bigint,record->>'Name',(record->>'Class')::integer,
            CASE
              WHEN (record->>'Id')::bigint/10000=11 THEN 1 WHEN (record->>'Id')::bigint/10000=12 THEN 2
              WHEN (record->>'Id')::bigint/10000=13 THEN 3
              WHEN (record->>'Id')::bigint/1000 BETWEEN 410 AND 699 THEN 4
              WHEN (record->>'Id')::bigint/1000 IN (900,1050,1051) THEN 5
              WHEN (record->>'Id')::bigint/10000=15 THEN 6 WHEN (record->>'Id')::bigint/10000=16 THEN 8
              WHEN (record->>'Id')::bigint/10000 IN (18,19) THEN 9
              WHEN (record->>'Id')::bigint/1000=201 THEN 10 WHEN (record->>'Id')::bigint/1000=202 THEN 11
              ELSE 0 END,
            (record->>'Profession')::smallint,(record->>'Level')::smallint,
            (record->>'Gender')::smallint,(record->>'Strength')::integer,(record->>'Agility')::integer,
            (record->>'MinimumAttack')::integer,(record->>'MaximumAttack')::integer,(record->>'Defense')::integer,
            (record->>'MagicAttack')::integer,(record->>'MagicDefense')::integer,(record->>'Accuracy')::integer,(record->>'Dodge')::integer,
            (record->>'Health')::integer,(record->>'Mana')::integer,(record->>'Durability')::integer,(record->>'SilverPrice')::bigint,
            (record->>'ConquerPointPrice')::bigint,record_sha256 FROM reviewed_catalog_stage WHERE category='items' ON CONFLICT DO NOTHING
        """,
        """
            INSERT INTO gameplay_reference.item_type_release_entries(release_id,item_type,source_id)
            SELECT $2,(record->>'Id')::bigint,$1 FROM reviewed_catalog_stage WHERE category='items' ON CONFLICT DO NOTHING
        """,
        """
            INSERT INTO gameplay_reference.shop_catalogs(source_id,legacy_shop_id,npc_source_id,npc_legacy_uid,safe_name,currency_type,canonical_record_sha256)
            SELECT $1,(s.record->>'ShopId')::bigint,n.source_id,(s.record->>'ShopId')::bigint,'Shop '||(s.record->>'ShopId'),
            CASE (s.record->>'CurrencyType')::integer WHEN 0 THEN 'silver' WHEN 1 THEN 'conquer-points' WHEN 2 THEN 'bound-conquer-points' END,
            min(s.record_sha256) FROM reviewed_catalog_stage s
            JOIN gameplay_reference.npcs n ON n.source_id=$3 AND n.legacy_uid=(s.record->>'ShopId')::bigint WHERE s.category='shops'
            GROUP BY (s.record->>'ShopId')::bigint,n.source_id,(s.record->>'CurrencyType')::integer,s.record->>'ShopId' ON CONFLICT DO NOTHING
        """,
        """
            INSERT INTO gameplay_reference.shop_catalog_items(source_id,legacy_shop_id,position,item_source_id,item_type,purchase_price,sale_price)
            SELECT $1,(s.record->>'ShopId')::bigint,(s.record->>'Ordinal')::smallint,$1,(s.record->>'ItemType')::bigint,
            CASE (s.record->>'CurrencyType')::integer WHEN 0 THEN i.silver_price ELSE i.conquer_point_price END,
            CASE (s.record->>'CurrencyType')::integer WHEN 0 THEN i.silver_price/3 ELSE 0 END
            FROM reviewed_catalog_stage s JOIN gameplay_reference.item_types i ON i.source_id=$1 AND i.item_type=(s.record->>'ItemType')::bigint
            JOIN gameplay_reference.npcs n ON n.source_id=$3 AND n.legacy_uid=(s.record->>'ShopId')::bigint
            WHERE s.category='shops' ON CONFLICT DO NOTHING
        """,
        """
            INSERT INTO gameplay_reference.shop_release_entries(release_id,legacy_shop_id,source_id)
            SELECT DISTINCT $2,(s.record->>'ShopId')::bigint,$1 FROM reviewed_catalog_stage s
            JOIN gameplay_reference.npcs n ON n.source_id=$3 AND n.legacy_uid=(s.record->>'ShopId')::bigint
            WHERE s.category='shops' ON CONFLICT DO NOTHING
        """,
        """
            INSERT INTO gameplay_reference.warehouse_definitions(source_id,legacy_warehouse_id,npc_source_id,npc_legacy_uid,slot_capacity,canonical_record_sha256)
            SELECT $1,n.legacy_uid,n.source_id,n.legacy_uid,40,
                encode(sha256(convert_to('warehouse:'||n.legacy_uid::text||':'||n.map_id::text||':'||n.x::text||':'||n.y::text,'UTF8')),'hex')
            FROM gameplay_reference.npcs n
            WHERE n.source_id=$3 AND n.legacy_uid IN (8,44,46,4101,10011,10012,10027,10028)
            ON CONFLICT DO NOTHING
        """,
        """
            INSERT INTO gameplay_reference.warehouse_release_entries(release_id,legacy_warehouse_id,source_id)
            SELECT $2,w.legacy_warehouse_id,w.source_id
            FROM gameplay_reference.warehouse_definitions w WHERE w.source_id=$1
            ON CONFLICT DO NOTHING
        """,
        """
            INSERT INTO gameplay_reference.item_enhancement_rules(source_id,item_family,plus,health_bonus,minimum_attack_bonus,maximum_attack_bonus,
            defense_bonus,magic_attack_bonus,magic_defense_bonus,accuracy_bonus,dodge_bonus,canonical_record_sha256)
            SELECT $1,(record->>'ItemType')::bigint,(record->>'Plus')::smallint,(record->>'Health')::integer,(record->>'MinimumAttack')::integer,
            (record->>'MaximumAttack')::integer,(record->>'Defense')::integer,(record->>'MagicAttack')::integer,(record->>'MagicDefense')::integer,
            (record->>'Accuracy')::integer,(record->>'Dodge')::integer,record_sha256 FROM reviewed_catalog_stage WHERE category='item-upgrades' ON CONFLICT DO NOTHING
        """,
        """
            INSERT INTO gameplay_reference.item_enhancement_release_entries(release_id,source_id,item_family,plus)
            SELECT $2,$1,(record->>'ItemType')::bigint,(record->>'Plus')::smallint FROM reviewed_catalog_stage WHERE category='item-upgrades' ON CONFLICT DO NOTHING
        """,
        """
            INSERT INTO gameplay_reference.monster_types(source_id,monster_type,safe_name,behavior,mesh,level,maximum_health,defense,magic_defense,
            magic_attack,minimum_attack,maximum_attack,dodge,attack_type,attack_distance,move_delay_ms,respawn_delay_ms,canonical_record_sha256,
            damage_reduction,magic_skill,magic_level,gives_rewards,minimum_silver,maximum_silver,use_level_difference_damage,classification)
            SELECT $1,(record->>'Id')::integer,record->>'Name',(record->>'Behavior')::smallint,(record->>'Mesh')::integer,(record->>'Level')::smallint,
            (record->>'MaximumHealth')::bigint,(record->>'Defense')::integer,(record->>'MagicDefense')::integer,(record->>'MagicAttack')::integer,
            (record->>'MinimumAttack')::integer,(record->>'MaximumAttack')::integer,(record->>'Dodge')::smallint,(record->>'AttackType')::smallint,
            (record->>'AttackDistance')::smallint,(record->>'MoveDelay')::integer,(record->>'SpawnDelay')::bigint,record_sha256,
            greatest(1,(record->>'DamageReduction')::smallint),(record->>'MagicSkill')::integer,(record->>'MagicLevel')::smallint,
            (record->>'GivesRewards')::boolean,(record->>'MinimumSilver')::integer,(record->>'MaximumSilver')::integer,
            (record->>'LevelDifferenceDamage')::boolean,
            CASE WHEN (record->>'Behavior')::smallint IN (4,6) THEN 'guard'
                 WHEN (record->>'Id')::integer BETWEEN 3000 AND 4500 THEN 'boss'
                 WHEN (record->>'Id')::integer=ANY(ARRAY[150,244,247,300,409,500,501,700,701,702,703,704,8423,8424]) THEN 'event-special'
                 WHEN NOT (record->>'GivesRewards')::boolean THEN 'passive' ELSE 'normal' END
            FROM reviewed_catalog_stage WHERE category='monsters' ON CONFLICT (source_id,monster_type) DO UPDATE SET
            safe_name=excluded.safe_name,behavior=excluded.behavior,mesh=excluded.mesh,level=excluded.level,
            maximum_health=excluded.maximum_health,defense=excluded.defense,magic_defense=excluded.magic_defense,
            magic_attack=excluded.magic_attack,minimum_attack=excluded.minimum_attack,maximum_attack=excluded.maximum_attack,
            dodge=excluded.dodge,attack_type=excluded.attack_type,attack_distance=excluded.attack_distance,
            move_delay_ms=excluded.move_delay_ms,respawn_delay_ms=excluded.respawn_delay_ms,
            canonical_record_sha256=excluded.canonical_record_sha256,damage_reduction=excluded.damage_reduction,
            magic_skill=excluded.magic_skill,magic_level=excluded.magic_level,gives_rewards=excluded.gives_rewards,
            minimum_silver=excluded.minimum_silver,maximum_silver=excluded.maximum_silver,
            use_level_difference_damage=excluded.use_level_difference_damage,classification=excluded.classification
        """,
        """
            INSERT INTO gameplay_reference.monster_release_entries(release_id,source_id,monster_type)
            SELECT $2,$1,(record->>'Id')::integer FROM reviewed_catalog_stage WHERE category='monsters' ON CONFLICT DO NOTHING
        """,
        """
            INSERT INTO gameplay_reference.monster_spawn_regions(source_id,spawn_region_id,monster_type,map_source_id,map_id,requested_population,
            minimum_x,minimum_y,maximum_x,maximum_y,enabled,canonical_record_sha256,activation_category)
            SELECT $1,ordinal+1,(record->>'MonsterId')::integer,$1,(record->>'MapId')::integer,(record->>'Count')::integer,
            (record->>'MinimumX')::integer,(record->>'MinimumY')::integer,(record->>'MaximumX')::integer,(record->>'MaximumY')::integer,
            NOT ((record->>'MonsterId')::integer=ANY(ARRAY[150,244,247,300,409,500,501,700,701,702,703,704,3821,3822,3823,4152,4170,4171,4172,8423,8424])
                OR (record->>'MapId')::integer=ANY(ARRAY[2020,8000,8001,8002,8003,8004,8005,8006])),record_sha256,
            CASE WHEN (record->>'MonsterId')::integer=ANY(ARRAY[150,244,247,300,409,500,501,700,701,702,703,704,3821,3822,3823,4152,4170,4171,4172,8423,8424])
                OR (record->>'MapId')::integer=ANY(ARRAY[2020,8000,8001,8002,8003,8004,8005,8006]) THEN 'event-gated' ELSE 'normal' END
            FROM reviewed_catalog_stage s JOIN gameplay_reference.maps m ON m.source_id=$1 AND m.legacy_map_id=(s.record->>'MapId')::integer
            WHERE s.category='spawns' ON CONFLICT (source_id,spawn_region_id) DO UPDATE SET
            monster_type=excluded.monster_type,map_source_id=excluded.map_source_id,map_id=excluded.map_id,
            requested_population=excluded.requested_population,minimum_x=excluded.minimum_x,minimum_y=excluded.minimum_y,
            maximum_x=excluded.maximum_x,maximum_y=excluded.maximum_y,enabled=excluded.enabled,
            canonical_record_sha256=excluded.canonical_record_sha256,activation_category=excluded.activation_category
        """,
        """
            INSERT INTO gameplay_reference.monster_spawn_dependency_gates(source_id,spawn_region_id,monster_type,map_id,
            requested_population,minimum_x,minimum_y,maximum_x,maximum_y,dependency_reason,activation_category,canonical_record_sha256)
            SELECT $1,s.ordinal+1,(s.record->>'MonsterId')::integer,(s.record->>'MapId')::integer,
            (s.record->>'Count')::integer,(s.record->>'MinimumX')::integer,(s.record->>'MinimumY')::integer,
            (s.record->>'MaximumX')::integer,(s.record->>'MaximumY')::integer,'missing-map-dmap','incompatible',s.record_sha256
            FROM reviewed_catalog_stage s LEFT JOIN gameplay_reference.maps m
            ON m.source_id=$1 AND m.legacy_map_id=(s.record->>'MapId')::integer
            WHERE s.category='spawns' AND m.legacy_map_id IS NULL
            ON CONFLICT (source_id,spawn_region_id) DO UPDATE SET monster_type=excluded.monster_type,map_id=excluded.map_id,
            requested_population=excluded.requested_population,minimum_x=excluded.minimum_x,minimum_y=excluded.minimum_y,
            maximum_x=excluded.maximum_x,maximum_y=excluded.maximum_y,dependency_reason=excluded.dependency_reason,
            activation_category=excluded.activation_category,canonical_record_sha256=excluded.canonical_record_sha256
        """,
        """
            INSERT INTO gameplay_reference.spawn_release_entries(release_id,source_id,spawn_region_id)
            SELECT $2,$1,s.ordinal+1 FROM reviewed_catalog_stage s JOIN gameplay_reference.maps m
            ON m.source_id=$1 AND m.legacy_map_id=(s.record->>'MapId')::integer WHERE s.category='spawns' ON CONFLICT DO NOTHING
        """,
        """
            INSERT INTO gameplay_reference.monster_drop_rules(source_id,monster_type,ordinal,item_source_id,item_type,chance_basis_points,plus,sockets,bless,canonical_record_sha256)
            SELECT $1,(record->>'MonsterId')::integer,row_number() over(partition by record->>'MonsterId' order by ordinal)-1,$1,(record->>'ItemType')::bigint,
            round((record->>'ChancePercent')::numeric*10000)::integer,(record->>'Plus')::smallint,(record->>'Sockets')::smallint,
            (record->>'Bless')::smallint,record_sha256 FROM reviewed_catalog_stage WHERE category='drops' ON CONFLICT DO NOTHING
        """,
        """
            INSERT INTO gameplay_reference.equipment_drop_candidates(source_id,monster_level,item_source_id,item_type,ordinal)
            SELECT $1,(record->>'Level')::smallint,$1,(record->>'ItemType')::bigint,
            row_number() over(partition by record->>'Level' order by ordinal)-1 FROM reviewed_catalog_stage WHERE category='equipment-drops' ON CONFLICT DO NOTHING
        """,
        """
            INSERT INTO gameplay_reference.magic_types(source_id,magic_type,level,sort,safe_name,mana_cost,base_damage,activation_chance,required_level,required_experience,canonical_record_sha256)
            SELECT $1,(record->>'Id')::integer,(record->>'Level')::smallint,(record->>'Sort')::smallint,record->>'Name',(record->>'Mana')::integer,
            (record->>'Damage')::bigint,(record->>'ActivationChance')::smallint,(record->>'RequiredLevel')::smallint,
            (record->>'RequiredExperience')::bigint,record_sha256 FROM reviewed_catalog_stage WHERE category='magic-types' ON CONFLICT DO NOTHING
        """,
        """
            INSERT INTO gameplay_reference.character_level_requirements(source_id,level,required_experience)
            SELECT $1,(record->>'Level')::smallint,(record->>'RequiredExperience')::bigint FROM reviewed_catalog_stage WHERE category='experience' ON CONFLICT DO NOTHING
        """,
        """
            INSERT INTO gameplay_reference.base_character_stats(source_id,profession_family,level,strength,agility,vitality,spirit)
            SELECT $1,lower(record->>'ClassName'),(record->>'Level')::smallint,(record->>'Strength')::integer,(record->>'Agility')::integer,
            (record->>'Vitality')::integer,(record->>'Spirit')::integer FROM reviewed_catalog_stage WHERE category='character-stats' ON CONFLICT DO NOTHING
        """,
    ];

    private static async Task ValidateAppliedCountsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        ReviewedCatalog catalog, ReviewedGameplayReference reference)
    {
        GameplayReferenceClosure closure = reference.EvaluateAgainst(catalog);
        var expectations = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["maps"] = "SELECT count(*) FROM gameplay_reference.maps WHERE source_id=$1",
            ["map-release"] = "SELECT count(*) FROM gameplay_reference.map_release_entries WHERE source_id=$1",
            ["items"] = "SELECT count(*) FROM gameplay_reference.item_types WHERE source_id=$1",
            ["shops"] = "SELECT count(*) FROM gameplay_reference.shop_catalog_items WHERE source_id=$1",
            ["shop-catalogs"] = "SELECT count(*) FROM gameplay_reference.shop_catalogs WHERE source_id=$1",
            ["item-upgrades"] = "SELECT count(*) FROM gameplay_reference.item_enhancement_rules WHERE source_id=$1",
            ["monsters"] = "SELECT count(*) FROM gameplay_reference.monster_types WHERE source_id=$1",
            ["spawns"] = "SELECT count(*) FROM gameplay_reference.monster_spawn_regions WHERE source_id=$1",
            ["spawn-dependency-gates"] = "SELECT count(*) FROM gameplay_reference.monster_spawn_dependency_gates WHERE source_id=$1",
            ["drops"] = "SELECT count(*) FROM gameplay_reference.monster_drop_rules WHERE source_id=$1",
            ["equipment-drops"] = "SELECT count(*) FROM gameplay_reference.equipment_drop_candidates WHERE source_id=$1",
            ["magic-types"] = "SELECT count(*) FROM gameplay_reference.magic_types WHERE source_id=$1",
            ["experience"] = "SELECT count(*) FROM gameplay_reference.character_level_requirements WHERE source_id=$1",
            ["character-stats"] = "SELECT count(*) FROM gameplay_reference.base_character_stats WHERE source_id=$1",
        };
        foreach ((string category, string sql) in expectations)
        {
            await using NpgsqlCommand command = Command(connection, transaction, sql);
            command.Parameters.AddWithValue(catalog.SourceId);
            long actual = Convert.ToInt64(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
            long expected = category switch
            {
                "map-release" => catalog.Categories["maps"].Rows.Count(row => !catalog.DeclaredMissingMapAssets.Contains(ReviewedCatalog.Text(row, "Path"))),
                "spawns" => catalog.Categories[category].Rows.Count(row => !catalog.DeclaredUnresolvedSpawnMaps.Contains(ReviewedCatalog.Integer(row, "MapId", 1, uint.MaxValue))),
                "spawn-dependency-gates" => catalog.Categories["spawns"].Rows.Count(row =>
                    catalog.DeclaredUnresolvedSpawnMaps.Contains(ReviewedCatalog.Integer(row, "MapId", 1, uint.MaxValue))),
                "shops" => closure.ResolvedShopRows,
                "shop-catalogs" => catalog.Categories["shops"].Rows
                    .Select(row => ReviewedCatalog.Integer(row, "ShopId", 1, uint.MaxValue))
                    .Distinct().Count(id => !closure.UnresolvedShopNpcIds.Contains(id)),
                _ => catalog.Categories[category].Rows.Count,
            };
            if (actual != expected)
                throw new InvalidDataException($"Applied category {category} did not match its reviewed count.");
        }
        var referenceExpectations = new Dictionary<string, (string Sql, long Expected)>(StringComparer.Ordinal)
        {
            ["npcs"] = ("SELECT count(*) FROM gameplay_reference.npcs WHERE source_id=$1", closure.ValidNpcs.Count),
            ["portals"] = ("SELECT count(*) FROM gameplay_reference.portals WHERE source_id=$1", closure.ValidPortals.Count),
            ["customshop"] = ("SELECT count(*) FROM gameplay_reference.shop_entries WHERE source_id=$1", reference.Categories["customshop"].Rows.Count),
            ["itemadd"] = ("SELECT count(*) FROM gameplay_reference.item_upgrade_rules WHERE source_id=$1", reference.Categories["itemadd"].Rows.Count),
        };
        foreach ((string category, (string sql, long expected)) in referenceExpectations)
        {
            await using NpgsqlCommand command = Command(connection, transaction, sql);
            command.Parameters.AddWithValue(reference.SourceId);
            long actual = Convert.ToInt64(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
            if (actual != expected)
                throw new InvalidDataException($"Applied gameplay-reference category {category} did not match its reviewed releasable count.");
        }
    }

    private static NpgsqlCommand Command(NpgsqlConnection connection, NpgsqlTransaction transaction, string text) =>
        new(text, connection, transaction) { CommandTimeout = 120 };
}
