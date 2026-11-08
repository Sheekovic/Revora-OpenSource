using Revora.Domain.Accounts;

namespace Revora.Gameplay.Models;

public readonly record struct CharacterUid
{
    public CharacterUid(uint value)
    {
        if (value == 0) throw new ArgumentOutOfRangeException(nameof(value));
        Value = value;
    }

    public uint Value { get; }
}

public readonly record struct ItemDefinitionId
{
    public ItemDefinitionId(uint value)
    {
        if (value == 0) throw new ArgumentOutOfRangeException(nameof(value));
        Value = value;
    }

    public uint Value { get; }
}

public readonly record struct ItemInstanceId
{
    public ItemInstanceId(Guid value)
    {
        if (value == Guid.Empty) throw new ArgumentException("Item instance identifier cannot be empty.", nameof(value));
        Value = value;
    }

    public Guid Value { get; }
}

public readonly record struct LegacyItemUid
{
    public LegacyItemUid(uint value)
    {
        if (value == 0) throw new ArgumentOutOfRangeException(nameof(value));
        Value = value;
    }

    public uint Value { get; }
}

public readonly record struct ShopId
{
    public ShopId(uint value)
    {
        if (value == 0) throw new ArgumentOutOfRangeException(nameof(value));
        Value = value;
    }

    public uint Value { get; }
}

public readonly record struct GameplayCommandId
{
    public GameplayCommandId(Guid value)
    {
        if (value == Guid.Empty) throw new ArgumentException("Command identifier cannot be empty.", nameof(value));
        Value = value;
    }

    public Guid Value { get; }
}

public readonly record struct GameplayActor(AccountId AccountId, CharacterUid CharacterUid);

public enum ItemContainer : byte
{
    Inventory = 1,
    Equipment = 2,
    Warehouse = 3
}

public enum EquipmentSlot : byte
{
    Headwear = 1,
    Necklace = 2,
    Armor = 3,
    RightHand = 4,
    LeftHand = 5,
    Ring = 6,
    Boots = 8,
    Garment = 9,
    Fan = 10,
    Tower = 11,
    Steed = 12
}

public readonly record struct ItemLocation
{
    private ItemLocation(ItemContainer container, ushort slot)
    {
        Container = container;
        Slot = slot;
    }

    public ItemContainer Container { get; }
    public ushort Slot { get; }

    public static ItemLocation Inventory(ushort slot = 0) => new(ItemContainer.Inventory, slot);
    public static ItemLocation Warehouse(ushort slot = 0) => new(ItemContainer.Warehouse, slot);
    public static ItemLocation Equipped(EquipmentSlot slot) => new(ItemContainer.Equipment, (ushort)slot);
}

public enum CurrencyKind : byte
{
    Silver = 1,
    ConquerPoints = 2,
    BoundConquerPoints = 3
}

public readonly record struct ItemStatBlock(
    int MinimumAttack = 0,
    int MaximumAttack = 0,
    int Defense = 0,
    int MagicAttack = 0,
    int MagicDefense = 0,
    int HitPoints = 0,
    int Mana = 0,
    int Dexterity = 0,
    int Dodge = 0)
{
    public static ItemStatBlock operator +(ItemStatBlock left, ItemStatBlock right) => new(
        checked(left.MinimumAttack + right.MinimumAttack),
        checked(left.MaximumAttack + right.MaximumAttack),
        checked(left.Defense + right.Defense),
        checked(left.MagicAttack + right.MagicAttack),
        checked(left.MagicDefense + right.MagicDefense),
        checked(left.HitPoints + right.HitPoints),
        checked(left.Mana + right.Mana),
        checked(left.Dexterity + right.Dexterity),
        checked(left.Dodge + right.Dodge));
}

public sealed record ItemRequirements(byte MinimumLevel = 1, byte? Profession = null, ushort Strength = 0, ushort Agility = 0);

public sealed record ItemDefinition(
    ItemDefinitionId Id,
    string Name,
    ushort MaximumDurability,
    EquipmentSlot? EquipmentSlot,
    ItemRequirements Requirements,
    ItemStatBlock BaseStats)
{
    public ItemDefinition Validate()
    {
        if (string.IsNullOrWhiteSpace(Name) || Name.Length > 64) throw new ArgumentException("Invalid item name.", nameof(Name));
        return this;
    }
}

public sealed record ItemInstance(
    ItemInstanceId Id,
    LegacyItemUid LegacyUid,
    ItemDefinitionId DefinitionId,
    AccountId AccountId,
    CharacterUid CharacterUid,
    ItemLocation Location,
    ushort Durability,
    byte Plus = 0,
    byte Bless = 0,
    byte Enchant = 0,
    byte SocketOne = 0,
    byte SocketTwo = 0,
    ushort StackCount = 1,
    byte Color = 0,
    bool Bound = false)
{
    public ItemInstance Validate()
    {
        if (StackCount == 0) throw new ArgumentOutOfRangeException(nameof(StackCount));
        return this;
    }
}

public sealed class CharacterGameplayState
{
    private readonly IReadOnlyDictionary<CurrencyKind, long> balances;

    public CharacterGameplayState(
        AccountId accountId,
        CharacterUid characterUid,
        int inventoryCapacity,
        int warehouseCapacity,
        byte level = 1,
        byte profession = 0,
        ushort strength = 0,
        ushort agility = 0,
        ushort vitality = 0,
        ushort spirit = 0,
        IReadOnlyDictionary<CurrencyKind, long>? balances = null,
        IReadOnlyDictionary<ushort, byte>? weaponProficiencies = null)
    {
        if (inventoryCapacity is < 1 or > 200) throw new ArgumentOutOfRangeException(nameof(inventoryCapacity));
        if (warehouseCapacity is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(warehouseCapacity));
        AccountId = accountId;
        CharacterUid = characterUid;
        InventoryCapacity = inventoryCapacity;
        WarehouseCapacity = warehouseCapacity;
        Level = level;
        Profession = profession;
        Strength = strength;
        Agility = agility;
        Vitality = vitality;
        Spirit = spirit;
        this.balances = new Dictionary<CurrencyKind, long>(balances ?? new Dictionary<CurrencyKind, long>());
        WeaponProficiencies = new Dictionary<ushort, byte>(weaponProficiencies ?? new Dictionary<ushort, byte>());
        if (this.balances.Values.Any(value => value < 0)) throw new ArgumentOutOfRangeException(nameof(balances));
        if (WeaponProficiencies.Values.Any(value => value > 20))
            throw new ArgumentOutOfRangeException(nameof(weaponProficiencies));
    }

    public AccountId AccountId { get; }
    public CharacterUid CharacterUid { get; }
    public int InventoryCapacity { get; }
    public int WarehouseCapacity { get; }
    public byte Level { get; }
    public byte Profession { get; }
    public ushort Strength { get; }
    public ushort Agility { get; }
    public ushort Vitality { get; }
    public ushort Spirit { get; }
    public IReadOnlyDictionary<CurrencyKind, long> Balances => balances;
    public IReadOnlyDictionary<ushort, byte> WeaponProficiencies { get; }

    public long Balance(CurrencyKind currency) => balances.GetValueOrDefault(currency);

    public CharacterGameplayState WithBalance(CurrencyKind currency, long amount)
    {
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        var next = new Dictionary<CurrencyKind, long>(balances) { [currency] = amount };
        return new CharacterGameplayState(AccountId, CharacterUid, InventoryCapacity, WarehouseCapacity, Level,
            Profession, Strength, Agility, Vitality, Spirit, next, WeaponProficiencies);
    }
}

public sealed record ShopEntry(ItemDefinitionId ItemDefinitionId, CurrencyKind Currency, long BuyPrice, long SellPrice)
{
    public ShopEntry Validate()
    {
        if (BuyPrice < 0 || SellPrice < 0 || SellPrice > BuyPrice) throw new ArgumentOutOfRangeException(nameof(BuyPrice));
        return this;
    }
}

public sealed record ShopDefinition(ShopId Id, IReadOnlyDictionary<ItemDefinitionId, ShopEntry> Entries);

public enum GameplayResultCode
{
    Success,
    CharacterNotFound,
    OwnershipMismatch,
    ItemNotFound,
    ForeignItem,
    InvalidLocation,
    InventoryFull,
    WarehouseFull,
    EquipmentSlotOccupied,
    DefinitionNotFound,
    RequirementsNotMet,
    ShopNotFound,
    ShopItemUnavailable,
    InvalidReferenceData,
    InvalidQuantity,
    InsufficientFunds,
    CurrencyOverflow,
    CommandConflict
}

public sealed record GameplayCommandResult(
    GameplayResultCode Code,
    bool Replayed = false,
    ItemInstanceId? ItemInstanceId = null)
{
    public bool Succeeded => Code == GameplayResultCode.Success;
    public GameplayCommandResult AsReplay() => this with { Replayed = true };
}

public sealed record GameplayCommandReceipt(GameplayCommandId CommandId, string Fingerprint, GameplayCommandResult Result);

public sealed record GameplayPlayerSnapshot(
    CharacterGameplayState Character,
    IReadOnlyList<ItemInstance> Items);

public sealed record CalculatedCharacterStats(
    int MaximumHealth,
    int MaximumMana,
    ItemStatBlock Equipment);

public sealed record WarehouseDefinition(uint LegacyId, uint NpcUid, ushort Capacity);

public sealed record EconomyCatalogSnapshot(
    Guid ReleaseId,
    IReadOnlyDictionary<ItemDefinitionId, ItemDefinition> Items,
    IReadOnlyDictionary<ShopId, ShopDefinition> Shops,
    IReadOnlyDictionary<uint, WarehouseDefinition> Warehouses);

public sealed record EconomyRolloutDecision(bool Enabled, string Scope);
