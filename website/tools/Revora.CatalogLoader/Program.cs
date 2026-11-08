using Revora.CatalogLoader;

if (args is ["--self-test"])
    return await CatalogLoaderSelfTest.RunAsync();

try
{
    LoaderOptions options = LoaderOptions.Parse(args);
    ReviewedCatalog catalog = await ReviewedCatalog.LoadAsync(options.ReviewedDirectory);
    ReviewedGameplayReference? gameplayReference = options.GameplayReferenceDirectory is null
        ? null
        : await ReviewedGameplayReference.LoadAsync(options.GameplayReferenceDirectory);
    Console.WriteLine($"Validated reviewed catalog {catalog.ManifestSha256}: {catalog.TotalRows} rows in {catalog.Categories.Count} categories.");
    if (gameplayReference is not null)
    {
        Console.WriteLine($"Validated reviewed gameplay references {gameplayReference.ManifestSha256}: " +
            $"{gameplayReference.TotalRows} rows in {gameplayReference.Categories.Count} categories.");
        GameplayReferenceClosure closure = gameplayReference.EvaluateAgainst(catalog);
        Console.WriteLine($"Reference closure: {closure.ValidNpcs.Count} NPCs and {closure.ValidPortals.Count} portals releasable; " +
            $"{closure.ResolvedShopRows} shop rows bound to reviewed NPCs.");
        Console.WriteLine($"Quarantined reference gaps: NPC maps [{string.Join(',', closure.MissingNpcMaps)}]; " +
            $"portal maps [{string.Join(',', closure.MissingPortalMaps)}]; shop NPCs [{string.Join(',', closure.UnresolvedShopNpcIds)}]; " +
            $"{closure.UnresolvedCustomShopItems} custom-shop item references; {closure.ConflictingItemAddRows} SQL item-add conflicts.");
    }
    Console.WriteLine($"Quarantined closure gaps: {catalog.DeclaredMissingMapAssets.Count} maps without DMaps; " +
        $"{catalog.DeclaredUnresolvedSpawnMaps.Count} unresolved spawn-map identifiers. These rows are not released.");
    if (!options.Apply)
    {
        Console.WriteLine("Dry run complete. No database connection was opened and no state was changed.");
        return 0;
    }

    DatabaseSecret secret = await DatabaseSecret.LoadProtectedAsync(options.SecretFile!);
    await PostgresCatalogStager.ApplyAsync(catalog, gameplayReference!, secret);
    Console.WriteLine($"Applied staged catalog release {catalog.ReleaseId} transactionally.");
    return 0;
}
catch (Exception error) when (error is ArgumentException or InvalidDataException or IOException or UnauthorizedAccessException or Npgsql.NpgsqlException)
{
    Console.Error.WriteLine($"Catalog load blocked safely: {error.Message}");
    return 1;
}
