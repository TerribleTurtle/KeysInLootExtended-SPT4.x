using System.Reflection;
using System.Text.Json;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Services;

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
    private readonly ItemFilterService _itemFilterService;

    /// <summary>
    /// Initializes the LootInjectionService.
    /// </summary>
    /// <param name="logger">The SPT logger instance.</param>
    /// <param name="databaseServer">The primary SPT database server instance.</param>
    /// <param name="configLoader">The global configuration loader service.</param>
    /// <param name="itemHelper">Helper for checking item baseclasses.</param>
    /// <param name="injectedKeysService">Shared service that stores the MongoIds of all valid keys and keycards discovered during initialization.</param>
    /// <param name="itemFilterService">SPT service for checking global item blacklists.</param>
    public LootInjectionService(
        ISptLogger<LootInjectionService> logger,
        DatabaseServer databaseServer,
        KeysInLootConfigLoader configLoader,
        ItemHelper itemHelper,
        InjectedKeysService injectedKeysService,
        ItemFilterService itemFilterService)
    {
        _logger = logger;
        _databaseServer = databaseServer;
        _configLoader = configLoader;
        _itemHelper = itemHelper;
        _injectedKeysService = injectedKeysService;
        _itemFilterService = itemFilterService;
    }

    /// <summary>
    /// Executes the primary loot injection routine.
    /// Safely exits early if the profile is "Disabled".
    /// </summary>
    public void InjectKeysIntoLocations()
    {
        var config = _configLoader.Config;
        if (string.Equals(config.ActiveProfile, "disabled", StringComparison.OrdinalIgnoreCase))
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
        
        bool IsOfBaseclass(string itemId, string targetBaseclass)
        {
            string currentId = itemId;
            while (!string.IsNullOrEmpty(currentId))
            {
                if (currentId == targetBaseclass) return true;
                if (db.Templates.Items.TryGetValue(currentId, out var itemTemplate))
                {
                    currentId = itemTemplate.Parent;
                }
                else
                {
                    break;
                }
            }
            return false;
        }

        foreach (var item in allItems)
        {
            // Filter out developer keys, test items, and quest-specific keys to prevent them from leaking into the game economy (e.g. Fence assort)
            if (item.Properties?.QuestItem == true) continue;
            
            try
            {
                var id = new MongoId(item.Id);
                if (_itemFilterService.IsItemBlacklisted(id) || _itemFilterService.IsLootableItemBlacklisted(id))
                    continue;
            }
            catch (FormatException)
            {
                // Let the baseclass check handle the format exception logging below
            }

            if (IsOfBaseclass(item.Id, KEYCARD_BASECLASS))
            {
                try 
                { 
                    var id = new MongoId(item.Id);
                    keycards.Add((item, id));
                    _injectedKeysService.InjectedKeyIds.Add(id); 
                } 
                catch (FormatException ex) { _logger.Warning($"[KeysInLootExtended] Skipping keycard {item.Id} due to invalid MongoId format from another mod: {ex.Message}"); }
            }
            else if (IsOfBaseclass(item.Id, KEY_BASECLASS))
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

            if (location.StaticLoot == null)
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

            // -----------------------------------------------------------------------------------------
            // SPT 4.0 ARCHITECTURE NOTE: The LazyLoad<T> Caching Paradigm
            // -----------------------------------------------------------------------------------------
            // In previous SPT versions, reading `.Value` generated a cached dictionary in memory,
            // which mods could mutate directly. In SPT 4.0, LazyLoad<T> was rewritten to remove caching.
            // Accessing `.Value` now parses the JSON file from disk every single time.
            // If we mutate the result of `.Value` directly, our changes are immediately discarded, 
            // and the server will load a pristine Vanilla copy when a raid starts.
            //
            // To fix this, we must use `.AddTransformer()`. This registers our closure into the 
            // deserialization pipeline. Whenever the server requests the static loot, our transformer 
            // natively intercepts and injects our custom keys and loot distributions on the fly.
            // -----------------------------------------------------------------------------------------
            Func<Dictionary<MongoId, StaticLootDetails>?, Dictionary<MongoId, StaticLootDetails>?> transformer = dict => 
            {
                if (dict == null) return dict;
                foreach (var target in targets)
                {
                    if (dict.ContainsKey(target.Id))
                    {
                        var container = dict[target.Id];
                        ModifyContainer(container, keys, target.KeyWeight, keycards, target.KeycardWeight);
                        
                        // We must instantiate a new array of ItemCountDistribution here rather than assigning
                        // the raw precomputed array, as the SPT engine or other mods may unexpectedly hold
                        // references that we don't want to inadvertently cross-contaminate.
                        if (target.Counts != null) 
                            container.ItemCountDistribution = target.Counts.Select(x => new ItemCountDistribution { Count = x.Count, RelativeProbability = x.RelativeProbability }).ToArray();
                    }
                }
                return dict;
            };

            location.StaticLoot.AddTransformer(transformer);
            modifiedContainers += 3;
        }

        _logger.Success($"[KeysInLootExtended] Successfully injected keys into {modifiedContainers} static containers across valid maps.");

    }

    private static readonly Dictionary<string, string> ExplicitRarityMap = new Dictionary<string, string>
    {
        // Common: Early game keys, quest keys, or very low-value loot rooms
        { "5671446a4bdc2d97058b4569", "Common" }, // Pistol case key
        { "57518f7724597720a31c09ab", "Common" }, // Key 3
        { "57518fd424597720c85dbaaa", "Common" }, // Key 5
        { "5751916f24597720a27126df", "Common" }, // Key 2
        { "57a349b2245977762b199ec7", "Common" }, // Pumping station front door key
        { "590de4a286f77423d9312a32", "Common" }, // Folding car key
        { "590de52486f774226a0c24c2", "Common" }, // Machinery key
        { "593858c486f774253a24cb52", "Common" }, // Pumping station back door key
        { "593962ca86f774068014d9af", "Common" }, // Unknown key
        { "6391fcf5744e45201147080f", "Common" }, // Primorsky Ave apartment key
        { "6398fd8ad3de3849057f5128", "Common" }, // Backup hideout key
        { "658199a0490414548c0fa83b", "Common" }, // Horse restaurant toilet key

        // Rare: Mid-to-high value safe keys, good loot rooms, and standard Streets/Labyrinth keys
        { "61a6446f4b5f8b70f451b166", "Rare" }, // Cold storage room key
        { "63a397d3af870e651d58e65b", "Rare" }, // Car dealership closed section key
        { "63a39ddda3a2b32b5f6e007a", "Rare" }, // Apartment locked room safe key
        { "63a39e0f64283b5e9c56b282", "Rare" }, // ?ity key
        { "63a39e5b234195315d4020bf", "Rare" }, // Housing office second floor safe key
        { "63a39e6acd6db0635c1975fe", "Rare" }, // Housing office first floor safe key
        { "63a71f1a0aa9fb29da61c537", "Rare" }, // ?ity key
        { "63a71f3b0aa9fb29da61c539", "Rare" }, // ?ity key
        { "64ce572331dd890873175115", "Rare" }, // Aspect company office key
        { "6582dc63cafcd9485374dbc5", "Rare" }, // Unity Credit Bank archive room key
        { "66265d7be65f224b2e17c6aa", "Rare" }, // USEC cottage room key
        { "679baace4e9ca6b3d80586b2", "Rare" }, // Observation room key
        { "679baae891966fe40408f14c", "Rare" }, // Torture room key
        { "679bac1d61f588ae2b062a26", "Rare" }, // Labyrinth key

        // Superrare: Extremely high value boss stashes, high tier access keycards (Labrys, colored cards), Arena boss keys
        { "5751961824597720a31c09ac", "Superrare" }, // (off)Black Keycard
        { "5d08d21286f774736e7c94c3", "Superrare" }, // Shturman's stash key
        { "5efde6b4f5448336730dbd61", "Superrare" }, // Keycard with a blue marking
        { "664d3db6db5dea2bad286955", "Superrare" }, // Shatun's hideout key
        { "664d3dd590294949fe2d81b7", "Superrare" }, // Grumpy's hideout key
        { "664d3ddfdda2e85aca370d75", "Superrare" }, // Voron's hideout key
        { "664d3de85f2355673b09aed5", "Superrare" }, // Leon's hideout key
        { "66acd6702b17692df20144c0", "Superrare" }, // TerraGroup storage room keycard
        { "679b9819a2f2dd4da9023512", "Superrare" }  // Labrys access keycard
    };

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

                if (ExplicitRarityMap.TryGetValue(itemMongoId.ToString(), out var explicitRarity))
                {
                    rarity = explicitRarity;
                }

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
                        var newEntry = new ItemDistribution { Tpl = entry.Tpl, RelativeProbability = targetWeight };
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
