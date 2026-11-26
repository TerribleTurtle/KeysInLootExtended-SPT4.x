using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Utils.Json;

namespace KeysInLootExtended.Tests.Simulation;

public sealed record ItemMetadata(
    MongoId Id,
    MongoId ParentId,
    string Type,
    bool QuestItem,
    string? RarityPvE,
    int Width,
    int Height,
    double? HandbookPrice,
    double? FleaPrice,
    int? CellsH,
    int? CellsV,
    int? MaxCount);

public sealed record RawLocationSnapshot(
    string FolderName,
    string? BaseId,
    bool HasStaticLoot,
    IReadOnlyDictionary<MongoId, RawContainerPool> Containers);

public sealed record RawContainerPool(
    IReadOnlyList<(int Count, double RelativeProbability)> ItemCountDistribution,
    IReadOnlyList<(MongoId Tpl, double RelativeProbability)> ItemDistribution);

public sealed class SptDatabaseSnapshot
{
    public required string SourceLabel { get; init; }
    public required IReadOnlyList<ItemMetadata> Items { get; init; }
    public required IReadOnlySet<MongoId> Blacklist { get; init; }
    public required IReadOnlySet<MongoId> LootableItemBlacklist { get; init; }
    public required IReadOnlySet<MongoId> FenceBlacklist { get; init; }
    public required IReadOnlyDictionary<string, RawLocationSnapshot> Locations { get; init; }
    public required IReadOnlyDictionary<MongoId, (int Width, int Height)> ItemDimensions { get; init; }

    /// <summary>
    /// Creates a completely isolated in-memory SPT table environment so each profile simulation
    /// can mutate TemplateTable, Prices, Handbook, Grids, Fence.Blacklist, and LazyLoad transformers cleanly.
    /// </summary>
    public InMemorySptEnvironment CreateFreshEnvironment()
    {
        var itemsDict = new Dictionary<MongoId, TemplateItem>(Items.Count);
        var pricesDict = new Dictionary<MongoId, double>();
        var handbookList = new List<HandbookItem>(Items.Count);

        foreach (var meta in Items)
        {
            var tpl = CreateTemplateItem(meta);
            itemsDict[meta.Id] = tpl;

            if (meta.FleaPrice.HasValue)
            {
                pricesDict[meta.Id] = meta.FleaPrice.Value;
            }

            if (meta.HandbookPrice.HasValue)
            {
                handbookList.Add(CreateHandbookItem(meta.Id, meta.HandbookPrice.Value));
            }
        }

        var handbook = (HandbookBase)RuntimeHelpers.GetUninitializedObject(typeof(HandbookBase));
        typeof(HandbookBase).GetProperty("Items")!.SetValue(handbook, handbookList);

        var templateTable = (TemplateTable)RuntimeHelpers.GetUninitializedObject(typeof(TemplateTable));
        typeof(TemplateTable).GetProperty("Items")!.SetValue(templateTable, itemsDict);
        typeof(TemplateTable).GetProperty("Prices")!.SetValue(templateTable, pricesDict);
        typeof(TemplateTable).GetProperty("Handbook")!.SetValue(templateTable, handbook);

        var locationTable = (LocationTable)RuntimeHelpers.GetUninitializedObject(typeof(LocationTable));
        PopulateLocationTable(locationTable, Locations);

        var itemConfig = (ItemConfig)RuntimeHelpers.GetUninitializedObject(typeof(ItemConfig));
        typeof(ItemConfig).GetProperty("Blacklist")!.SetValue(itemConfig, new HashSet<MongoId>(Blacklist));
        typeof(ItemConfig).GetProperty("LootableItemBlacklist")!.SetValue(itemConfig, new HashSet<MongoId>(LootableItemBlacklist));

        var fenceConfig = (FenceConfig)RuntimeHelpers.GetUninitializedObject(typeof(FenceConfig));
        typeof(FenceConfig).GetProperty("Blacklist")!.SetValue(fenceConfig, new HashSet<MongoId>(FenceBlacklist));

        var traderConfig = (TraderConfig)RuntimeHelpers.GetUninitializedObject(typeof(TraderConfig));
        typeof(TraderConfig).GetProperty("Fence")!.SetValue(traderConfig, fenceConfig);

        return new InMemorySptEnvironment(
            SourceLabel,
            templateTable,
            locationTable,
            itemConfig,
            traderConfig,
            ItemDimensions);
    }

    private static TemplateItem CreateTemplateItem(ItemMetadata meta)
    {
        var item = (TemplateItem)RuntimeHelpers.GetUninitializedObject(typeof(TemplateItem));
        typeof(TemplateItem).GetProperty("Id")!.SetValue(item, meta.Id);
        typeof(TemplateItem).GetProperty("Parent")!.SetValue(item, meta.ParentId);
        typeof(TemplateItem).GetProperty("Type")!.SetValue(item, meta.Type);

        var props = (TemplateItemProperties)RuntimeHelpers.GetUninitializedObject(typeof(TemplateItemProperties));
        typeof(TemplateItemProperties).GetProperty("RarityPvE")!.SetValue(props, meta.RarityPvE);
        typeof(TemplateItemProperties).GetProperty("QuestItem")!.SetValue(props, meta.QuestItem);

        if (meta.CellsH.HasValue && meta.CellsV.HasValue)
        {
            var grid = (Grid)RuntimeHelpers.GetUninitializedObject(typeof(Grid));
            var gridProps = (GridProperties)RuntimeHelpers.GetUninitializedObject(typeof(GridProperties));
            gridProps.CellsH = meta.CellsH.Value;
            gridProps.CellsV = meta.CellsV.Value;
            gridProps.MaxCount = meta.MaxCount ?? (meta.CellsH.Value * meta.CellsV.Value);
            typeof(Grid).GetProperty("Properties")!.SetValue(grid, gridProps);
            typeof(TemplateItemProperties).GetProperty("Grids")!.SetValue(props, new List<Grid> { grid });
        }

        typeof(TemplateItem).GetProperty("Properties")!.SetValue(item, props);
        return item;
    }

    private static HandbookItem CreateHandbookItem(MongoId id, double price)
    {
        var hb = (HandbookItem)RuntimeHelpers.GetUninitializedObject(typeof(HandbookItem));
        typeof(HandbookItem).GetProperty("Id")!.SetValue(hb, id);
        typeof(HandbookItem).GetProperty("Price")!.SetValue(hb, price);
        return hb;
    }

    private static void PopulateLocationTable(
        LocationTable locationTable,
        IReadOnlyDictionary<string, RawLocationSnapshot> locations)
    {
        var folderToProperty = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["bigmap"] = nameof(LocationTable.Bigmap),
            ["develop"] = nameof(LocationTable.Develop),
            ["factory4_day"] = nameof(LocationTable.Factory4Day),
            ["factory4_night"] = nameof(LocationTable.Factory4Night),
            ["hideout"] = nameof(LocationTable.Hideout),
            ["interchange"] = nameof(LocationTable.Interchange),
            ["laboratory"] = nameof(LocationTable.Laboratory),
            ["lighthouse"] = nameof(LocationTable.Lighthouse),
            ["privatearea"] = nameof(LocationTable.PrivateArea),
            ["rezervbase"] = nameof(LocationTable.RezervBase),
            ["shoreline"] = nameof(LocationTable.Shoreline),
            ["suburbs"] = nameof(LocationTable.Suburbs),
            ["tarkovstreets"] = nameof(LocationTable.TarkovStreets),
            ["labyrinth"] = nameof(LocationTable.Labyrinth),
            ["terminal"] = nameof(LocationTable.Terminal),
            ["town"] = nameof(LocationTable.Town),
            ["woods"] = nameof(LocationTable.Woods),
            ["sandbox"] = nameof(LocationTable.Sandbox),
            ["sandbox_high"] = nameof(LocationTable.SandboxHigh),
        };

        foreach (var (folder, rawLoc) in locations)
        {
            if (!folderToProperty.TryGetValue(folder, out var propName))
            {
                continue;
            }

            var prop = typeof(LocationTable).GetProperty(propName);
            if (prop == null)
            {
                continue;
            }

            var loc = (Location)RuntimeHelpers.GetUninitializedObject(typeof(Location));
            var locBase = (LocationBase)RuntimeHelpers.GetUninitializedObject(typeof(LocationBase));
            typeof(LocationBase).GetProperty("Id")!.SetValue(locBase, rawLoc.BaseId);
            typeof(Location).GetProperty("Base")!.SetValue(loc, locBase);

            if (rawLoc.HasStaticLoot)
            {
                // Mirrors SPT 4.x uncached LazyLoad<T>: builds a fresh dictionary each time .Value is evaluated
                var lazyLoot = new LazyLoad<Dictionary<MongoId, StaticLootDetails>>(() =>
                {
                    var dict = new Dictionary<MongoId, StaticLootDetails>(rawLoc.Containers.Count);
                    foreach (var (cid, pool) in rawLoc.Containers)
                    {
                        dict[cid] = new StaticLootDetails
                        {
                            ItemCountDistribution = pool.ItemCountDistribution
                                .Select(c => new ItemCountDistribution { Count = c.Count, RelativeProbability = (float)c.RelativeProbability })
                                .ToArray(),
                            ItemDistribution = pool.ItemDistribution
                                .Select(i => new ItemDistribution { Tpl = i.Tpl, RelativeProbability = (float)i.RelativeProbability })
                                .ToArray()
                        };
                    }
                    return dict;
                }, false);

                typeof(Location).GetProperty("StaticLoot")!.SetValue(loc, lazyLoot);
            }

            prop.SetValue(locationTable, loc);
        }
    }
}

public sealed record InMemorySptEnvironment(
    string SourceLabel,
    TemplateTable TemplateTable,
    LocationTable LocationTable,
    ItemConfig ItemConfig,
    TraderConfig TraderConfig,
    IReadOnlyDictionary<MongoId, (int Width, int Height)> ItemDimensions);

public static class SptDatabaseLoader
{
    private static SptDatabaseSnapshot? _cachedGoldenSnapshot;
    private static readonly Dictionary<string, SptDatabaseSnapshot> _cachedLiveSnapshots = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object _sync = new();

    public static SptDatabaseSnapshot LoadGoldenFixture(string? fixturePath = null)
    {
        lock (_sync)
        {
            if (fixturePath == null && _cachedGoldenSnapshot != null)
            {
                return _cachedGoldenSnapshot;
            }

            string resolvedPath = fixturePath
                ?? System.IO.Path.Combine(AppContext.BaseDirectory, "TestData", "spt416_golden_fixture.json.gz");

            if (!File.Exists(resolvedPath))
            {
                throw new FileNotFoundException($"Golden SPT fixture not found at {resolvedPath}");
            }

            using var fs = File.OpenRead(resolvedPath);
            using var gz = new GZipStream(fs, CompressionMode.Decompress);
            var dto = JsonSerializer.Deserialize<GoldenFixtureDto>(gz)
                ?? throw new InvalidDataException("Failed to deserialize golden SPT fixture.");

            var items = new List<ItemMetadata>(dto.Items.Count);
            var dims = new Dictionary<MongoId, (int Width, int Height)>(dto.Items.Count);
            var idByIndex = new MongoId[dto.Items.Count];
            var rootFallback = new MongoId("54009119af1c881c07000029");

            for (int i = 0; i < dto.Items.Count; i++)
            {
                var entry = dto.Items[i];
                var id = new MongoId(entry.Id);
                var parent = string.IsNullOrWhiteSpace(entry.Parent) ? rootFallback : new MongoId(entry.Parent);
                idByIndex[i] = id;
                dims[id] = (Math.Max(1, entry.W), Math.Max(1, entry.H));

                items.Add(new ItemMetadata(
                    id,
                    parent,
                    entry.Type,
                    entry.Quest,
                    entry.Rarity,
                    Math.Max(1, entry.W),
                    Math.Max(1, entry.H),
                    entry.Hb,
                    entry.Flea,
                    entry.CellsH,
                    entry.CellsV,
                    entry.MaxCount));
            }

            var locations = new Dictionary<string, RawLocationSnapshot>(StringComparer.OrdinalIgnoreCase);
            foreach (var (locKey, locDto) in dto.Locations)
            {
                var containers = new Dictionary<MongoId, RawContainerPool>(locDto.Containers.Count);
                foreach (var (cidStr, cDto) in locDto.Containers)
                {
                    var cid = new MongoId(cidStr);
                    var countDist = cDto.ItemCountDistribution
                        .Select(pair => (pair[0], (double)pair[1]))
                        .ToList();
                    var itemDist = cDto.ItemDistribution
                        .Select(pair => (idByIndex[pair[0]], (double)pair[1]))
                        .ToList();
                    containers[cid] = new RawContainerPool(countDist, itemDist);
                }

                locations[locKey] = new RawLocationSnapshot(locKey, locDto.BaseId, locDto.HasStaticLoot, containers);
            }

            var snapshot = new SptDatabaseSnapshot
            {
                SourceLabel = $"GoldenFixture (SPT {dto.SptVersion})",
                Items = items,
                Blacklist = dto.Blacklist.Select(x => new MongoId(x)).ToHashSet(),
                LootableItemBlacklist = dto.LootableItemBlacklist.Select(x => new MongoId(x)).ToHashSet(),
                FenceBlacklist = dto.FenceBlacklist.Select(x => new MongoId(x)).ToHashSet(),
                Locations = locations,
                ItemDimensions = dims
            };

            if (fixturePath == null)
            {
                _cachedGoldenSnapshot = snapshot;
            }

            return snapshot;
        }
    }

    public static SptDatabaseSnapshot LoadLiveSptData(string sptDataPath)
    {
        if (!Directory.Exists(sptDataPath))
        {
            throw new DirectoryNotFoundException($"Live SPT_DATA_PATH directory does not exist: {sptDataPath}");
        }

        string fullRoot = System.IO.Path.GetFullPath(sptDataPath);
        lock (_sync)
        {
            if (_cachedLiveSnapshots.TryGetValue(fullRoot, out var cached))
            {
                return cached;
            }

            var jsonUtil = CreateSptJsonUtil();

            var rawItems = jsonUtil.DeserializeFromFile<Dictionary<MongoId, TemplateItem>>(
                System.IO.Path.Combine(fullRoot, "database", "templates", "items.json"))
                ?? throw new InvalidDataException("Failed to deserialize items.json via SPT JsonUtil.");

            var rawPrices = jsonUtil.DeserializeFromFile<Dictionary<MongoId, double>>(
                System.IO.Path.Combine(fullRoot, "database", "templates", "prices.json"))
                ?? new Dictionary<MongoId, double>();

            var rawHandbook = jsonUtil.DeserializeFromFile<HandbookBase>(
                System.IO.Path.Combine(fullRoot, "database", "templates", "handbook.json"))
                ?? throw new InvalidDataException("Failed to deserialize handbook.json via SPT JsonUtil.");

            var rawItemConfig = jsonUtil.DeserializeFromFile<ItemConfig>(
                System.IO.Path.Combine(fullRoot, "configs", "item.json"))
                ?? throw new InvalidDataException("Failed to deserialize configs/item.json via SPT JsonUtil.");

            var rawTraderConfig = jsonUtil.DeserializeFromFile<TraderConfig>(
                System.IO.Path.Combine(fullRoot, "configs", "trader.json"))
                ?? throw new InvalidDataException("Failed to deserialize configs/trader.json via SPT JsonUtil.");

            var hbById = new Dictionary<MongoId, double>();
            if (rawHandbook.Items != null)
            {
                foreach (var h in rawHandbook.Items)
                {
                    if (h.Price.HasValue)
                    {
                        hbById[h.Id] = h.Price.Value;
                    }
                }
            }

            var widthProp = typeof(TemplateItemProperties).GetProperty("Width");
            var heightProp = typeof(TemplateItemProperties).GetProperty("Height");
            var rootFallback = new MongoId("54009119af1c881c07000029");

            var items = new List<ItemMetadata>(rawItems.Count);
            var dims = new Dictionary<MongoId, (int Width, int Height)>(rawItems.Count);

            foreach (var (id, tpl) in rawItems)
            {
                int w = ReadPositiveDimension(widthProp, tpl.Properties);
                int h = ReadPositiveDimension(heightProp, tpl.Properties);
                dims[id] = (w, h);

                int? cellsH = null;
                int? cellsV = null;
                int? maxCount = null;
                var firstGrid = tpl.Properties?.Grids?.FirstOrDefault()?.Properties;
                if (firstGrid != null)
                {
                    cellsH = firstGrid.CellsH;
                    cellsV = firstGrid.CellsV;
                    maxCount = firstGrid.MaxCount.HasValue ? (int)firstGrid.MaxCount.Value : null;
                }

                MongoId parentId = rootFallback;
                try
                {
                    if (!string.IsNullOrWhiteSpace(tpl.Parent))
                    {
                        parentId = new MongoId(tpl.Parent);
                    }
                }
                catch (Exception ex) when (ex is FormatException or ArgumentException)
                {
                    parentId = rootFallback;
                }

                items.Add(new ItemMetadata(
                    id,
                    parentId,
                    tpl.Type ?? "Item",
                    tpl.Properties?.QuestItem == true,
                    tpl.Properties?.RarityPvE?.ToString(),
                    w,
                    h,
                    hbById.TryGetValue(id, out var hbp) ? hbp : null,
                    rawPrices.TryGetValue(id, out var fp) ? fp : null,
                    cellsH,
                    cellsV,
                    maxCount));
            }

            var locations = new Dictionary<string, RawLocationSnapshot>(StringComparer.OrdinalIgnoreCase);
            string locationsDir = System.IO.Path.Combine(fullRoot, "database", "locations");
            foreach (var dir in Directory.GetDirectories(locationsDir))
            {
                string folderName = System.IO.Path.GetFileName(dir);
                string basePath = System.IO.Path.Combine(dir, "base.json");
                string staticLootPath = System.IO.Path.Combine(dir, "staticLoot.json");

                string? baseId = folderName;
                if (File.Exists(basePath))
                {
                    using var doc = JsonDocument.Parse(File.ReadAllBytes(basePath));
                    if (doc.RootElement.TryGetProperty("Id", out var idEl))
                    {
                        baseId = idEl.GetString();
                    }
                }

                if (!File.Exists(staticLootPath))
                {
                    locations[folderName] = new RawLocationSnapshot(folderName, baseId, false, new Dictionary<MongoId, RawContainerPool>());
                    continue;
                }

                var rawStaticLoot = jsonUtil.DeserializeFromFile<Dictionary<MongoId, StaticLootDetails>>(staticLootPath)
                    ?? new Dictionary<MongoId, StaticLootDetails>();

                var containers = new Dictionary<MongoId, RawContainerPool>(rawStaticLoot.Count);
                foreach (var (cid, details) in rawStaticLoot)
                {
                    var countDist = (details.ItemCountDistribution ?? [])
                        .Select(c => ((int)(c.Count ?? 0), (double)(c.RelativeProbability ?? 0f)))
                        .ToList();
                    var itemDist = (details.ItemDistribution ?? [])
                        .Select(i => (i.Tpl, (double)(i.RelativeProbability ?? 0f)))
                        .ToList();
                    containers[cid] = new RawContainerPool(countDist, itemDist);
                }

                locations[folderName] = new RawLocationSnapshot(folderName, baseId, true, containers);
            }

            var snapshot = new SptDatabaseSnapshot
            {
                SourceLabel = $"LiveSPT ({fullRoot})",
                Items = items,
                Blacklist = (rawItemConfig.Blacklist ?? []).ToHashSet(),
                LootableItemBlacklist = (rawItemConfig.LootableItemBlacklist ?? []).ToHashSet(),
                FenceBlacklist = (rawTraderConfig.Fence?.Blacklist ?? []).ToHashSet(),
                Locations = locations,
                ItemDimensions = dims
            };

            _cachedLiveSnapshots[fullRoot] = snapshot;
            return snapshot;
        }
    }

    private static int ReadPositiveDimension(PropertyInfo? prop, TemplateItemProperties? target)
    {
        if (prop == null || target == null)
        {
            return 1;
        }

        var raw = prop.GetValue(target);
        if (raw == null)
        {
            return 1;
        }

        int converted = Convert.ToInt32(raw);
        return Math.Max(1, converted);
    }

    private static JsonUtil CreateSptJsonUtil()
    {
        var asm = typeof(JsonUtil).Assembly;
        var registrators = asm.GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface && typeof(IJsonConverterRegistrator).IsAssignableFrom(t)
                        && t.GetConstructors().Any(c => c.GetParameters().Length == 0))
            .Select(t => (IJsonConverterRegistrator)Activator.CreateInstance(t)!)
            .ToList();

        return new JsonUtil(registrators);
    }

    private sealed class GoldenFixtureDto
    {
        [JsonPropertyName("sptVersion")]
        public string SptVersion { get; set; } = string.Empty;

        [JsonPropertyName("blacklist")]
        public List<string> Blacklist { get; set; } = [];

        [JsonPropertyName("lootableItemBlacklist")]
        public List<string> LootableItemBlacklist { get; set; } = [];

        [JsonPropertyName("fenceBlacklist")]
        public List<string> FenceBlacklist { get; set; } = [];

        [JsonPropertyName("items")]
        public List<GoldenItemDto> Items { get; set; } = [];

        [JsonPropertyName("locations")]
        public Dictionary<string, GoldenLocationDto> Locations { get; set; } = new();
    }

    private sealed class GoldenItemDto
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("parent")]
        public string Parent { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = "Item";

        [JsonPropertyName("w")]
        public int W { get; set; } = 1;

        [JsonPropertyName("h")]
        public int H { get; set; } = 1;

        [JsonPropertyName("quest")]
        public bool Quest { get; set; }

        [JsonPropertyName("rarity")]
        public string? Rarity { get; set; }

        [JsonPropertyName("hb")]
        public double? Hb { get; set; }

        [JsonPropertyName("flea")]
        public double? Flea { get; set; }

        [JsonPropertyName("cellsH")]
        public int? CellsH { get; set; }

        [JsonPropertyName("cellsV")]
        public int? CellsV { get; set; }

        [JsonPropertyName("maxCount")]
        public int? MaxCount { get; set; }
    }

    private sealed class GoldenLocationDto
    {
        [JsonPropertyName("baseId")]
        public string? BaseId { get; set; }

        [JsonPropertyName("hasStaticLoot")]
        public bool HasStaticLoot { get; set; }

        [JsonPropertyName("containers")]
        public Dictionary<string, GoldenContainerDto> Containers { get; set; } = new();
    }

    private sealed class GoldenContainerDto
    {
        [JsonPropertyName("itemCountDistribution")]
        public List<int[]> ItemCountDistribution { get; set; } = [];

        [JsonPropertyName("itemDistribution")]
        public List<int[]> ItemDistribution { get; set; } = [];
    }
}
