using System.Reflection;
using System.Text.Json;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Models.Common;

namespace KeysInLootExtended;

/// <summary>
/// Service responsible for injecting Keys and Keycards into specific static loot containers.
/// Parses database templates and mutates container probabilities.
/// </summary>
[Injectable(InjectionType.Singleton)]
public class LootInjectionService
{
    private readonly ISptLogger<LootInjectionService> _logger;
    private readonly DatabaseServer _databaseServer;
    private readonly KeysInLootConfigLoader _configLoader;
    private readonly ItemHelper _itemHelper;
    private readonly InjectedKeysService _injectedKeysService;

    /// <summary>
    /// Initializes the LootInjectionService.
    /// </summary>
    /// <param name="logger">The SPT logger instance.</param>
    /// <param name="databaseServer">The primary SPT database server instance.</param>
    /// <param name="configLoader">The global configuration loader service.</param>
    /// <param name="itemHelper">Helper for checking item baseclasses.</param>
    /// <param name="injectedKeysService">Shared service that stores the MongoIds of all valid keys and keycards discovered during initialization.</param>
    public LootInjectionService(
        ISptLogger<LootInjectionService> logger,
        DatabaseServer databaseServer,
        KeysInLootConfigLoader configLoader,
        ItemHelper itemHelper,
        InjectedKeysService injectedKeysService)
    {
        _logger = logger;
        _databaseServer = databaseServer;
        _configLoader = configLoader;
        _itemHelper = itemHelper;
        _injectedKeysService = injectedKeysService;
    }

    /// <summary>
    /// Executes the primary loot injection routine.
    /// Safely exits early if the profile is "Disabled".
    /// </summary>
    public void InjectKeysIntoLocations()
    {
        var config = _configLoader.Config;
        if (config.ActiveProfile == "Disabled")
        {
            _logger.Warning("[KeysInLootExtended] Mod is Disabled. Skipping loot injection.");
            return;
        }

        var db = _databaseServer.GetTables();
        var allItems = db.Templates.Items.Values;
        
        // Find keys and keycards
        // Note: These baseclass IDs are hardcoded native EFT IDs for "Key" and "Keycard" categories.
        const string KEY_BASECLASS = "543be5e94bdc2df1348b4568";
        const string KEYCARD_BASECLASS = "5c164d2286f774194c5e69fa";

        var keys = new List<(TemplateItem Item, MongoId Id)>();
        var keycards = new List<(TemplateItem Item, MongoId Id)>();
        
        foreach (var item in allItems)
        {
            if (_itemHelper.IsOfBaseclass(item.Id, KEYCARD_BASECLASS))
            {
                try 
                { 
                    var id = new MongoId(item.Id);
                    keycards.Add((item, id));
                    _injectedKeysService.InjectedKeyIds.Add(id); 
                } 
                catch (FormatException ex) { _logger.Warning($"[KeysInLootExtended] Skipping keycard {item.Id} due to invalid MongoId format from another mod: {ex.Message}"); }
            }
            else if (_itemHelper.IsOfBaseclass(item.Id, KEY_BASECLASS))
            {
                try 
                { 
                    var id = new MongoId(item.Id);
                    keys.Add((item, id));
                    _injectedKeysService.InjectedKeyIds.Add(id); 
                } 
                catch (FormatException ex) { _logger.Warning($"[KeysInLootExtended] Skipping key {item.Id} due to invalid MongoId format from another mod: {ex.Message}"); }
            }
        }

        _logger.Success($"[KeysInLootExtended] Found {keys.Count} Keys and {keycards.Count} Keycards in the database.");



        // Internal dictionary to map raw location IDs from the database to cleaner enum-style names
        // used by our custom JSON configuration files. "Sandbox" is internally "Ground Zero".
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
            {"Woods", "woods"}
        };

        // Precompute count distribution arrays to prevent repeated allocations inside the loop
        ItemCountDistribution[]? jacketCounts = null;
        ItemCountDistribution[]? duffleCounts = null;
        ItemCountDistribution[]? deadScavCounts = null;
        
        if (config.OverrideLootDistribution)
        {
            jacketCounts = config.OverrideLootDistributionJackets?.Select(x => new ItemCountDistribution { Count = x.Count, RelativeProbability = x.RelativeProbability }).ToArray();
            duffleCounts = config.OverrideLootDistributionDuffleBags?.Select(x => new ItemCountDistribution { Count = x.Count, RelativeProbability = x.RelativeProbability }).ToArray();
            deadScavCounts = config.OverrideLootDistributionDeadScavs?.Select(x => new ItemCountDistribution { Count = x.Count, RelativeProbability = x.RelativeProbability }).ToArray();
        }

        int modifiedContainers = 0;

        // Extract all properties from Locations to get all maps including custom ones if they were added via properties.
        // This reflection-based approach is intentionally used to bypass hardcoded map lists and dynamically discover custom modded maps.
        var validLocations = db.Locations.GetType().GetProperties()
            .Select(p => p.GetValue(db.Locations))
            .Where(l => l != null)
            .Cast<dynamic>()
            .ToList();

        var jacketContainerId = new MongoId("578f8778245977358849a9b5");
        var duffleContainerId = new MongoId("578f87a3245977356274f2cb");
        var deadScavContainerId = new MongoId("5909e4b686f7747f5b744fa4");

        foreach (var location in validLocations)
        {
            if (location == null) continue;
            
            object? baseObj = null;
            // The db.Locations object is highly dynamic in SPT. Custom maps might be missing a .Base property,
            // which throws a RuntimeBinderException. We safely swallow this to ignore invalid map objects.
            try { baseObj = location.Base; } catch (Microsoft.CSharp.RuntimeBinder.RuntimeBinderException) { continue; }
            if (baseObj == null) continue;

            var staticLootDict = location.StaticLoot?.Value;
            if (staticLootDict == null)
                continue;

            KeysInLootRarityConfig jacketKeyWeight = config.KeyWeight;
            KeysInLootRarityConfig jacketKeycardWeight = config.KeycardWeight;
            KeysInLootRarityConfig duffleKeyWeight = config.KeyWeight;
            KeysInLootRarityConfig duffleKeycardWeight = config.KeycardWeight;
            KeysInLootRarityConfig deadScavKeyWeight = config.KeyWeight;
            KeysInLootRarityConfig deadScavKeycardWeight = config.KeycardWeight;

            if (config.EnableLocationsConfig)
            {
                string baseId = location.Base.Id;
                string enumName = locationIdToEnum.TryGetValue(baseId, out var mappedName) 
                    ? mappedName 
                    : baseId.ToLowerInvariant();

                var locConfig = _configLoader.LoadLocationConfig(enumName);
                if (locConfig != null)
                {
                    jacketKeyWeight = locConfig.JacketContainer?.Key ?? config.KeyWeight;
                    jacketKeycardWeight = locConfig.JacketContainer?.Keycard ?? config.KeycardWeight;
                    duffleKeyWeight = locConfig.DuffleBagContainer?.Key ?? config.KeyWeight;
                    duffleKeycardWeight = locConfig.DuffleBagContainer?.Keycard ?? config.KeycardWeight;
                    deadScavKeyWeight = locConfig.DeadScavContainer?.Key ?? config.KeyWeight;
                    deadScavKeycardWeight = locConfig.DeadScavContainer?.Keycard ?? config.KeycardWeight;
                }
            }

            var targets = new (MongoId Id, KeysInLootRarityConfig KeyWeight, KeysInLootRarityConfig KeycardWeight, ItemCountDistribution[]? Counts)[]
            {
                (jacketContainerId, jacketKeyWeight, jacketKeycardWeight, jacketCounts),
                (duffleContainerId, duffleKeyWeight, duffleKeycardWeight, duffleCounts),
                (deadScavContainerId, deadScavKeyWeight, deadScavKeycardWeight, deadScavCounts)
            };

            foreach (var target in targets)
            {
                if (staticLootDict.ContainsKey(target.Id))
                {
                    var container = staticLootDict[target.Id];
                    ModifyContainer(container, keys, target.KeyWeight, keycards, target.KeycardWeight);
                    if (target.Counts != null) container.ItemCountDistribution = target.Counts.Select(x => new ItemCountDistribution { Count = x.Count, RelativeProbability = x.RelativeProbability }).ToArray();
                    modifiedContainers++;
                }
            }
        }

        _logger.Success($"[KeysInLootExtended] Successfully injected keys into {modifiedContainers} static containers across valid maps.");

    }

    /// <summary>
    /// Internal routine to apply keys and keycards to a single container's loot distribution.
    /// </summary>
    /// <param name="container">The specific StaticLoot container to modify.</param>
    /// <param name="keys">The list of generic key items to inject.</param>
    /// <param name="keyWeights">The targeted spawn weights for standard keys.</param>
    /// <param name="keycards">The list of keycard items to inject.</param>
    /// <param name="keycardWeights">The targeted spawn weights for keycards.</param>
    private void ModifyContainer(StaticLootDetails container, List<(TemplateItem Item, MongoId Id)> keys, KeysInLootRarityConfig keyWeights, List<(TemplateItem Item, MongoId Id)> keycards, KeysInLootRarityConfig keycardWeights)
    {
        var existingItems = container.ItemDistribution?.ToList() ?? new List<ItemDistribution>();
        var distDict = new Dictionary<MongoId, List<ItemDistribution>>();

        foreach (var entry in existingItems)
        {
            if (!distDict.ContainsKey(entry.Tpl)) distDict[entry.Tpl] = new List<ItemDistribution>();
            distDict[entry.Tpl].Add(entry);
        }

        void ProcessItems(List<(TemplateItem Item, MongoId Id)> items, KeysInLootRarityConfig weights)
        {
            foreach (var tuple in items)
            {
                var item = tuple.Item;
                var itemMongoId = tuple.Id;

                int targetWeight = 0;
                // In SPT, a null rarity typically maps to the "Very Common" tier, internally referred to as "Not_exist"
                string rarity = item.Properties?.RarityPvE?.ToString() ?? "Not_exist";

                switch (rarity)
                {
                    case "Not_exist": targetWeight = weights.NotExist; break;
                    case "Common": targetWeight = weights.Common; break;
                    case "Rare": targetWeight = weights.Rare; break;
                    case "Superrare": targetWeight = weights.SuperRare; break;
                }

                if (targetWeight <= 0) 
                {
                    continue;
                }

                if (distDict.TryGetValue(itemMongoId, out var existingEntries))
                {
                    var updatedList = new List<ItemDistribution>();
                    foreach (var entry in existingEntries)
                    {
                        var newEntry = new ItemDistribution { Tpl = entry.Tpl, RelativeProbability = entry.RelativeProbability };
                        // We use the maximum between the existing weight and the new target weight.
                        // This prevents accidentally nerfing keys that already have a naturally high vanilla spawn rate.
                        if (newEntry.RelativeProbability < targetWeight)
                        {
                            newEntry.RelativeProbability = targetWeight;
                        }
                        updatedList.Add(newEntry);
                    }
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

        // Clamp total weight to prevent SPT map load crashes and leave ecosystem headroom
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
                    var newEntry = new ItemDistribution { Tpl = entry.Tpl, RelativeProbability = Math.Max(1, (int)((entry.RelativeProbability ?? 0) * scale)) };
                    updatedList.Add(newEntry);
                }
                distDict[key] = updatedList;
            }
        }

        container.ItemDistribution = distDict.Values.SelectMany(x => x).ToArray();
    }
}
