using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Services.Items;

namespace KeysInLootExtended;

/// <summary>
/// Registers static loot transformers that inject keys and keycards into target containers.
/// </summary>
[Injectable(InjectionType.Singleton)]
public class LootInjectionService
{
    private readonly ISptLogger<LootInjectionService> _logger;
    private readonly TemplateTable _templateTable;
    private readonly LocationTable _locationTable;
    private readonly KeysInLootConfigLoader _configLoader;
    private readonly InjectedKeysService _injectedKeysService;
    private readonly ItemFilterService _itemFilterService;

    public LootInjectionService(
        ISptLogger<LootInjectionService> logger,
        TemplateTable templateTable,
        LocationTable locationTable,
        KeysInLootConfigLoader configLoader,
        InjectedKeysService injectedKeysService,
        ItemFilterService itemFilterService)
    {
        _logger = logger;
        _templateTable = templateTable;
        _locationTable = locationTable;
        _configLoader = configLoader;
        _injectedKeysService = injectedKeysService;
        _itemFilterService = itemFilterService;
    }

    public void InjectKeysIntoLocations()
    {
        var config = _configLoader.Config;
        if (!config.Enabled)
        {
            _logger.Warning("[KeysInLootExtended] Mod is disabled. Skipping loot injection.");
            return;
        }
        var resolved = config.Resolved;

        var allItems = _templateTable.Items.Values;
        
        // Native EFT parent template IDs for Key and Keycard categories.
        const string KEY_BASECLASS = "543be5e94bdc2df1348b4568";
        const string KEYCARD_BASECLASS = "5c164d2286f774194c5e69fa";

        var keys = new List<(TemplateItem Item, MongoId Id, string ResolvedRarity)>();
        var keycards = new List<(TemplateItem Item, MongoId Id, string ResolvedRarity)>();

        // Only include items in the Handbook priced above 1 RUB; this skips unfinished placeholder
        // keys (missing from Handbook) and 1-RUB in-raid puzzle items (e.g. Labyrinth valve/Ariadne items).
        var validHandbookItemIds = new HashSet<MongoId>();
        var handbookItems = _templateTable.Handbook?.Items;
        bool enforceHandbookFilter = handbookItems is { Count: > 0 };
        if (enforceHandbookFilter)
        {
            foreach (var hb in handbookItems!)
            {
                if (hb.Price is > 1.0)
                {
                    validHandbookItemIds.Add(hb.Id);
                }
            }
        }
        
        bool IsOfBaseclass(string itemId, string targetBaseclass)
        {
            if (itemId == targetBaseclass) return false;
            string currentId = itemId;
            var visited = new HashSet<string>();

            while (!string.IsNullOrEmpty(currentId))
            {
                if (!visited.Add(currentId)) break; // Guard against circular parent chains in custom templates
                
                if (currentId == targetBaseclass) return true;
                try
                {
                    if (_templateTable.Items.TryGetValue(currentId, out var itemTemplate))
                    {
                        currentId = itemTemplate.Parent;
                    }
                    else
                    {
                        break;
                    }
                }
                catch (Exception ex) when (ex is FormatException or ArgumentException)
                {
                    break;
                }
            }
            return false;
        }

        foreach (var item in allItems)
        {
            // Skip quest-only keys and category nodes so they never leak into containers or Fence.
            if (item.Properties?.QuestItem == true) continue;
            if (string.Equals(item.Type, "Node", StringComparison.OrdinalIgnoreCase)) continue;
            
            MongoId id;
            try
            {
                id = new MongoId(item.Id);
                if (_itemFilterService.IsItemBlacklisted(id) || _itemFilterService.IsLootableItemBlacklisted(id))
                    continue;
                if (enforceHandbookFilter && !validHandbookItemIds.Contains(id))
                    continue;
            }
            catch (Exception ex) when (ex is FormatException or ArgumentException)
            {
                _logger.Warning($"[KeysInLootExtended] Skipping item {item.Id} due to invalid MongoId format: {ex.Message}");
                continue;
            }

            if (IsOfBaseclass(item.Id, KEYCARD_BASECLASS))
            {
                string rarity = item.Properties?.RarityPvE?.ToString() ?? "Not_exist";
                if (ExplicitRarityMap.TryGetValue(item.Id, out var explicitRarity)) rarity = explicitRarity;
                keycards.Add((item, id, rarity));
                _injectedKeysService.InjectedKeyIds.Add(id); 
            }
            else if (IsOfBaseclass(item.Id, KEY_BASECLASS))
            {
                string rarity = item.Properties?.RarityPvE?.ToString() ?? "Not_exist";
                if (ExplicitRarityMap.TryGetValue(item.Id, out var explicitRarity)) rarity = explicitRarity;
                keys.Add((item, id, rarity));
                _injectedKeysService.InjectedKeyIds.Add(id); 
            }
        }

        _logger.Success($"[KeysInLootExtended] Found {keys.Count} Keys and {keycards.Count} Keycards in the database.");

        // Map SPT location IDs to locations/*.jsonc filenames ("Sandbox" is Ground Zero).
        var locationIdToEnum = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            {"bigmap", "customs"},
            {"factory4_day", "factory_day"},
            {"factory4_night", "factory_night"},
            {"Interchange", "interchange"},
            {"laboratory", "laboratory"},
            {"Lighthouse", "lighthouse"},
            {"RezervBase", "reserve"},
            {"Sandbox", "ground_zero"},
            {"Sandbox_high", "ground_zero_high"},
            {"Shoreline", "shoreline"},
            {"TarkovStreets", "streets_of_tarkov"},
            {"Woods", "woods"},
            {"Labyrinth", "labyrinth"}
        };

        ItemCountDistribution[]? jacketCounts = null;
        ItemCountDistribution[]? duffleCounts = null;
        ItemCountDistribution[]? deadScavCounts = null;
        
        if (resolved.OverrideLootDistribution)
        {
            jacketCounts = resolved.JacketsItemCounts?.Select(x => new ItemCountDistribution { Count = x.Count, RelativeProbability = x.RelativeProbability }).ToArray();
            duffleCounts = resolved.DuffleBagsItemCounts?.Select(x => new ItemCountDistribution { Count = x.Count, RelativeProbability = x.RelativeProbability }).ToArray();
            deadScavCounts = resolved.DeadScavsItemCounts?.Select(x => new ItemCountDistribution { Count = x.Count, RelativeProbability = x.RelativeProbability }).ToArray();
        }

        int registeredLocations = 0;
        bool isDebugVerbosity = config.DebugLogs;

        var validLocations = _locationTable.GetDictionary().Values;

        foreach (var location in validLocations)
        {
            if (location?.Base == null || location.StaticLoot == null)
                continue;

            string mapBaseId = location.Base.Id ?? "unknown";
            KeysInLootRarityConfig jacketKeyWeight = resolved.KeyWeight;
            KeysInLootRarityConfig jacketKeycardWeight = resolved.KeycardWeight;
            KeysInLootRarityConfig duffleKeyWeight = resolved.KeyWeight;
            KeysInLootRarityConfig duffleKeycardWeight = resolved.KeycardWeight;
            KeysInLootRarityConfig deadScavKeyWeight = resolved.KeyWeight;
            KeysInLootRarityConfig deadScavKeycardWeight = resolved.KeycardWeight;

            if (config.EnableLocationsConfig)
            {
                if (string.IsNullOrEmpty(location.Base.Id)) continue;
                string enumName = locationIdToEnum.TryGetValue(mapBaseId, out var mappedName) 
                    ? mappedName 
                    : mapBaseId.ToLowerInvariant();

                var locConfig = _configLoader.LoadLocationConfig(enumName);
                if (locConfig != null)
                {
                    jacketKeyWeight = locConfig.JacketContainer?.Key ?? resolved.KeyWeight;
                    jacketKeycardWeight = locConfig.JacketContainer?.Keycard ?? resolved.KeycardWeight;
                    duffleKeyWeight = locConfig.DuffleBagContainer?.Key ?? resolved.KeyWeight;
                    duffleKeycardWeight = locConfig.DuffleBagContainer?.Keycard ?? resolved.KeycardWeight;
                    deadScavKeyWeight = locConfig.DeadScavContainer?.Key ?? resolved.KeyWeight;
                    deadScavKeycardWeight = locConfig.DeadScavContainer?.Keycard ?? resolved.KeycardWeight;
                }
            }

            var targets = new (string Label, MongoId[] Ids, KeysInLootRarityConfig KeyWeight, KeysInLootRarityConfig KeycardWeight, ItemCountDistribution[]? Counts)[]
            {
                ("Jacket", ContainerTargets.Jackets, jacketKeyWeight, jacketKeycardWeight, jacketCounts),
                ("DuffleBag", ContainerTargets.DuffleBags, duffleKeyWeight, duffleKeycardWeight, duffleCounts),
                ("DeadScav", ContainerTargets.DeadScavs, deadScavKeyWeight, deadScavKeycardWeight, deadScavCounts)
            };

            // SPT 4's LazyLoad<T> reloads static loot from disk on each access, so mutating .Value directly
            // is discarded; AddTransformer injects our changes whenever a raid loads the map.
            int debugLoggedOnce = 0;
            Func<Dictionary<MongoId, StaticLootDetails>?, Dictionary<MongoId, StaticLootDetails>?> transformer = dict => 
            {
                if (dict == null) return dict;
                try
                {
                    bool logThisPass = isDebugVerbosity && System.Threading.Interlocked.Exchange(ref debugLoggedOnce, 1) == 0;
                    foreach (var target in targets)
                    {
                        foreach (var containerId in target.Ids)
                        {
                            if (dict.TryGetValue(containerId, out var container))
                            {
                                ModifyContainer(container, keys, target.KeyWeight, keycards, target.KeycardWeight);
                                
                                // Clone count entries per call so SPT or other mods cannot mutate our shared template array.
                                if (target.Counts is { Length: > 0 }) 
                                    container.ItemCountDistribution = target.Counts.Select(x => new ItemCountDistribution { Count = x.Count, RelativeProbability = x.RelativeProbability }).ToArray();

                                if (logThisPass && container.ItemDistribution != null)
                                {
                                    double totW = container.ItemDistribution.Sum(x => (double)(x.RelativeProbability ?? 0f));
                                    double keyW = container.ItemDistribution
                                        .Where(x => _injectedKeysService.InjectedKeyIds.Contains(x.Tpl))
                                        .Sum(x => (double)(x.RelativeProbability ?? 0f));
                                    double pct = totW > 0 ? (100.0 * keyW / totW) : 0.0;
                                    _logger.Info($"[KeysInLootExtended] [Debug] {mapBaseId} {target.Label}: key pool share = {pct:F2}% ({keyW:F0}/{totW:F0} weight)");
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"[KeysInLootExtended] Error transforming static loot: {ex}");
                }
                return dict;
            };

            location.StaticLoot.AddTransformer(transformer);
            registeredLocations++;
        }

        _logger.Success($"[KeysInLootExtended] Registered static loot key transformers across {registeredLocations} locations.");
    }

    private static readonly Dictionary<string, string> ExplicitRarityMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        // Common: Early game keys, quest-adjacent unlock keys, or low-value loot rooms
        { "57a349b2245977762b199ec7", "Common" }, // Pumping station front door key
        { "593858c486f774253a24cb52", "Common" }, // Pumping station back door key
        { "593962ca86f774068014d9af", "Common" }, // Unknown key
        { "6398fd8ad3de3849057f5128", "Common" }, // Backup hideout key
        { "658199a0490414548c0fa83b", "Common" }, // Horse restaurant toilet key

        // Rare: Mid-to-high value rooms and standard Streets/Labyrinth keys
        { "61a6446f4b5f8b70f451b166", "Rare" }, // Cold storage room key
        { "64ce572331dd890873175115", "Rare" }, // Aspect company office key
        { "6582dc63cafcd9485374dbc5", "Rare" }, // Unity Credit Bank archive room key
        { "66265d7be65f224b2e17c6aa", "Rare" }, // USEC cottage room key
        { "679baace4e9ca6b3d80586b2", "Rare" }, // Observation room key
        { "679baae891966fe40408f14c", "Rare" }, // Torture room key
        { "679bac1d61f588ae2b062a26", "Rare" }, // Labyrinth key

        // Superrare: Extremely high value boss stashes, high tier access keycards, Arena boss keys, LexOs
        { "63a397d3af870e651d58e65b", "Superrare" }, // Car dealership closed section key
        { "5d08d21286f774736e7c94c3", "Superrare" }, // Shturman's stash key
        { "5efde6b4f5448336730dbd61", "Superrare" }, // Keycard with a blue marking
        { "664d3db6db5dea2bad286955", "Superrare" }, // Shatun's hideout key
        { "664d3dd590294949fe2d81b7", "Superrare" }, // Grumpy's hideout key
        { "664d3ddfdda2e85aca370d75", "Superrare" }, // Voron's hideout key
        { "664d3de85f2355673b09aed5", "Superrare" }, // Leon's hideout key
        { "66acd6702b17692df20144c0", "Superrare" }, // TerraGroup storage room keycard
        { "679b9819a2f2dd4da9023512", "Superrare" }  // Labrys access keycard
    };

    private void ModifyContainer(StaticLootDetails container, List<(TemplateItem Item, MongoId Id, string ResolvedRarity)> keys, KeysInLootRarityConfig keyWeights, List<(TemplateItem Item, MongoId Id, string ResolvedRarity)> keycards, KeysInLootRarityConfig keycardWeights)
    {
        var existingItems = container.ItemDistribution?.ToList() ?? new List<ItemDistribution>();
        var distDict = new Dictionary<MongoId, List<ItemDistribution>>();

        foreach (var entry in existingItems)
        {
            if (!distDict.ContainsKey(entry.Tpl)) distDict[entry.Tpl] = new List<ItemDistribution>();
            distDict[entry.Tpl].Add(entry);
        }

        void ProcessItems(List<(TemplateItem Item, MongoId Id, string ResolvedRarity)> items, KeysInLootRarityConfig weights)
        {
            foreach (var tuple in items)
            {
                var item = tuple.Item;
                var itemMongoId = tuple.Id;

                int targetWeight = 0;

                switch (tuple.ResolvedRarity.ToLowerInvariant())
                {
                    case "not_exist": targetWeight = weights.NotExist; break;
                    case "common": targetWeight = weights.Common; break;
                    case "rare": targetWeight = weights.Rare; break;
                    case "superrare": targetWeight = weights.SuperRare; break;
                    default: targetWeight = weights.NotExist; break;
                }

                if (targetWeight <= 0) 
                {
                    // Remove existing vanilla entries when a tier is set to 0 so vanilla weights do not leak through.
                    if (distDict.ContainsKey(itemMongoId))
                    {
                        distDict.Remove(itemMongoId);
                    }
                    continue;
                }

                if (distDict.TryGetValue(itemMongoId, out var existingEntries))
                {
                    // Collapse duplicate vanilla entries for the same template into a single normalized weight.
                    var updatedList = new List<ItemDistribution>
                    {
                        new ItemDistribution { Tpl = existingEntries.First().Tpl, RelativeProbability = targetWeight }
                    };
                    distDict[itemMongoId] = updatedList;
                }
                else
                {
                    distDict[itemMongoId] = new List<ItemDistribution>
                    {
                        new ItemDistribution
                        {
                            Tpl = itemMongoId,
                            RelativeProbability = targetWeight
                        }
                    };
                }
            }
        }

        ProcessItems(keys, keyWeights);
        ProcessItems(keycards, keycardWeights);

        // Normalize weights above int.MaxValue / 2 to avoid int overflow in SPT's loot roller while leaving headroom for other mods.
        long totalWeight = distDict.Values.SelectMany(x => x).Sum(x => (long)(x.RelativeProbability ?? 0));
        int safeCeiling = int.MaxValue / 2;
        if (totalWeight > safeCeiling)
        {
            _logger.Warning($"[KeysInLootExtended] A container's weight exceeds int.MaxValue/2! Normalizing weights to leave ecosystem headroom...");
            double scale = (double)safeCeiling / totalWeight;
            foreach (var key in distDict.Keys.ToList())
            {
                var updatedList = new List<ItemDistribution>();
                foreach (var entry in distDict[key])
                {
                    var rawWeight = entry.RelativeProbability ?? 0;
                    var newEntry = new ItemDistribution { Tpl = entry.Tpl, RelativeProbability = rawWeight > 0 ? Math.Max(1, (int)(rawWeight * scale)) : 0 };
                    updatedList.Add(newEntry);
                }
                distDict[key] = updatedList;
            }
        }

        container.ItemDistribution = distDict.Values.SelectMany(x => x).ToArray();
    }
}
