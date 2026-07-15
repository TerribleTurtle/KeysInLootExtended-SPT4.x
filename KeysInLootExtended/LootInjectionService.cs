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

[Injectable(InjectionType.Singleton)]
public class LootInjectionService
{
    private readonly ISptLogger<LootInjectionService> _logger;
    private readonly DatabaseServer _databaseServer;
    private readonly KeysInLootConfigLoader _configLoader;
    private readonly ItemHelper _itemHelper;
    private readonly InjectedKeysService _injectedKeysService;

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
        const string KEY_BASECLASS = "543be5e94bdc2df1348b4568";
        const string KEYCARD_BASECLASS = "5c164d2286f774194c5e69fa";

        var keys = new List<TemplateItem>();
        var keycards = new List<TemplateItem>();
        
        foreach (var item in allItems)
        {
            if (_itemHelper.IsOfBaseclass(item.Id, KEYCARD_BASECLASS))
            {
                keycards.Add(item);
                try { _injectedKeysService.InjectedKeyIds.Add(new MongoId(item.Id)); } 
                catch (FormatException ex) { _logger.Warning($"[KeysInLootExtended] Skipping keycard {item.Id} due to invalid MongoId format from another mod: {ex.Message}"); }
            }
            else if (_itemHelper.IsOfBaseclass(item.Id, KEY_BASECLASS))
            {
                keys.Add(item);
                try { _injectedKeysService.InjectedKeyIds.Add(new MongoId(item.Id)); } 
                catch (FormatException ex) { _logger.Warning($"[KeysInLootExtended] Skipping key {item.Id} due to invalid MongoId format from another mod: {ex.Message}"); }
            }
        }

        _logger.Success($"[KeysInLootExtended] Found {keys.Count} Keys and {keycards.Count} Keycards in the database.");



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

        // Extract all properties from Locations to get all maps including custom ones if they were added via properties
        var validLocations = db.Locations.GetType().GetProperties()
            .Select(p => p.GetValue(db.Locations))
            .Where(l => l != null)
            .Cast<dynamic>()
            .ToList();

        foreach (var location in validLocations)
        {
            if (location == null) continue;
            
            object? baseObj = null;
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

            // Jacket
            var jacketId = new MongoId("578f8778245977358849a9b5");
            if (staticLootDict.ContainsKey(jacketId))
            {
                var jacket = staticLootDict[jacketId];
                ModifyContainer(jacket, keys, jacketKeyWeight, keycards, jacketKeycardWeight);
                if (jacketCounts != null) jacket.ItemCountDistribution = jacketCounts.Select(x => new ItemCountDistribution { Count = x.Count, RelativeProbability = x.RelativeProbability }).ToArray();
                modifiedContainers++;
            }

            // Duffle Bag
            var duffleId = new MongoId("578f87a3245977356274f2cb");
            if (staticLootDict.ContainsKey(duffleId))
            {
                var duffle = staticLootDict[duffleId];
                ModifyContainer(duffle, keys, duffleKeyWeight, keycards, duffleKeycardWeight);
                if (duffleCounts != null) duffle.ItemCountDistribution = duffleCounts.Select(x => new ItemCountDistribution { Count = x.Count, RelativeProbability = x.RelativeProbability }).ToArray();
                modifiedContainers++;
            }

            // Dead Scav
            var deadScavId = new MongoId("5909e4b686f7747f5b744fa4");
            if (staticLootDict.ContainsKey(deadScavId))
            {
                var deadScav = staticLootDict[deadScavId];
                ModifyContainer(deadScav, keys, deadScavKeyWeight, keycards, deadScavKeycardWeight);
                if (deadScavCounts != null) deadScav.ItemCountDistribution = deadScavCounts.Select(x => new ItemCountDistribution { Count = x.Count, RelativeProbability = x.RelativeProbability }).ToArray();
                modifiedContainers++;
            }
        }

        _logger.Success($"[KeysInLootExtended] Successfully injected keys into {modifiedContainers} static containers across valid maps.");

    }

    private void ModifyContainer(StaticLootDetails container, List<TemplateItem> keys, KeysInLootRarityConfig keyWeights, List<TemplateItem> keycards, KeysInLootRarityConfig keycardWeights)
    {
        var existingItems = container.ItemDistribution?.ToList() ?? new List<ItemDistribution>();
        var distDict = new Dictionary<MongoId, List<ItemDistribution>>();

        foreach (var entry in existingItems)
        {
            if (!distDict.ContainsKey(entry.Tpl)) distDict[entry.Tpl] = new List<ItemDistribution>();
            distDict[entry.Tpl].Add(entry);
        }

        void ProcessItems(List<TemplateItem> items, KeysInLootRarityConfig weights)
        {
            foreach (var item in items)
            {
                int targetWeight = 0;
                string rarity = item.Properties?.RarityPvE?.ToString() ?? "Not_exist";

                switch (rarity)
                {
                    case "Not_exist": targetWeight = weights.NotExist; break;
                    case "Common": targetWeight = weights.Common; break;
                    case "Rare": targetWeight = weights.Rare; break;
                    case "Superrare": targetWeight = weights.SuperRare; break;
                }

                MongoId itemMongoId;
                try { itemMongoId = new MongoId(item.Id); } 
                catch (FormatException ex) 
                { 
                    _logger.Warning($"[KeysInLootExtended] Skipping item {item.Id} in container loop due to invalid MongoId format: {ex.Message}");
                    continue; 
                }

                if (targetWeight <= 0) 
                {
                    distDict.Remove(itemMongoId);
                    continue;
                }

                if (distDict.TryGetValue(itemMongoId, out var existingEntries))
                {
                    var updatedList = new List<ItemDistribution>();
                    foreach (var entry in existingEntries)
                    {
                        var newEntry = new ItemDistribution { Tpl = entry.Tpl, RelativeProbability = entry.RelativeProbability };
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
        long totalWeight = distDict.Values.SelectMany(x => x).Sum(x => (long)x.RelativeProbability);
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
                    var newEntry = new ItemDistribution { Tpl = entry.Tpl, RelativeProbability = Math.Max(1, (int)(entry.RelativeProbability * scale)) };
                    updatedList.Add(newEntry);
                }
                distDict[key] = updatedList;
            }
        }

        container.ItemDistribution = distDict.Values.SelectMany(x => x).ToArray();
    }
}
