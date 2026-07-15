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

        var keys = allItems.Where(i => _itemHelper.IsOfBaseclass(i.Id, KEY_BASECLASS)).ToList();
        var keycards = allItems.Where(i => _itemHelper.IsOfBaseclass(i.Id, KEYCARD_BASECLASS)).ToList();

        _logger.Success($"[KeysInLootExtended] Found {keys.Count} Keys and {keycards.Count} Keycards in the database.");

        var validLocations = new[] { 
            db.Locations.Bigmap, db.Locations.Factory4Day, db.Locations.Factory4Night, 
            db.Locations.Interchange, db.Locations.Laboratory, db.Locations.Lighthouse, 
            db.Locations.RezervBase, db.Locations.Sandbox, db.Locations.SandboxHigh, 
            db.Locations.Shoreline, db.Locations.TarkovStreets, db.Locations.Woods 
        };

        var locationIdToEnum = new Dictionary<string, string>
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

        int modifiedContainers = 0;

        foreach (var location in validLocations)
        {
            if (location == null || location.Base == null)
                continue;

            var staticLootDict = location.StaticLoot?.Value;
            if (staticLootDict == null)
                continue;

            KeysInLootRarityConfig jacketKeyWeight = config.KeyWeight;
            KeysInLootRarityConfig jacketKeycardWeight = config.KeycardWeight;
            KeysInLootRarityConfig duffleKeyWeight = config.KeyWeight;
            KeysInLootRarityConfig duffleKeycardWeight = config.KeycardWeight;
            KeysInLootRarityConfig deadScavKeyWeight = config.KeyWeight;
            KeysInLootRarityConfig deadScavKeycardWeight = config.KeycardWeight;

            if (config.EnableLocationsConfig && locationIdToEnum.TryGetValue(location.Base.Id, out var enumName))
            {
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
            if (staticLootDict.TryGetValue(jacketId, out var jacket))
            {
                ModifyContainer(jacket, keys, jacketKeyWeight);
                ModifyContainer(jacket, keycards, jacketKeycardWeight);
                if (config.OverrideLootDistribution)
                    jacket.ItemCountDistribution = config.OverrideLootDistributionJackets?.Select(x => new ItemCountDistribution { Count = x.Count, RelativeProbability = x.RelativeProbability }).ToArray() ?? jacket.ItemCountDistribution;
                modifiedContainers++;
            }

            // Duffle Bag
            var duffleId = new MongoId("578f87a3245977356274f2cb");
            if (staticLootDict.TryGetValue(duffleId, out var duffle))
            {
                ModifyContainer(duffle, keys, duffleKeyWeight);
                ModifyContainer(duffle, keycards, duffleKeycardWeight);
                if (config.OverrideLootDistribution)
                    duffle.ItemCountDistribution = config.OverrideLootDistributionDuffleBags?.Select(x => new ItemCountDistribution { Count = x.Count, RelativeProbability = x.RelativeProbability }).ToArray() ?? duffle.ItemCountDistribution;
                modifiedContainers++;
            }

            // Dead Scav
            var deadScavId = new MongoId("5909e4b686f7747f5b744fa4");
            if (staticLootDict.TryGetValue(deadScavId, out var deadScav))
            {
                ModifyContainer(deadScav, keys, deadScavKeyWeight);
                ModifyContainer(deadScav, keycards, deadScavKeycardWeight);
                if (config.OverrideLootDistribution)
                    deadScav.ItemCountDistribution = config.OverrideLootDistributionDeadScavs?.Select(x => new ItemCountDistribution { Count = x.Count, RelativeProbability = x.RelativeProbability }).ToArray() ?? deadScav.ItemCountDistribution;
                modifiedContainers++;
            }
        }

        _logger.Success($"[KeysInLootExtended] Successfully injected keys into {modifiedContainers} static containers across valid maps.");

    }

    private void ModifyContainer(StaticLootDetails container, List<TemplateItem> items, KeysInLootRarityConfig weights)
    {
        var distDict = container.ItemDistribution?.GroupBy(x => x.Tpl).ToDictionary(g => g.Key, g => g.First()) ?? new Dictionary<MongoId, ItemDistribution>();

        foreach (var item in items)
        {
            int targetWeight = 0;
            string rarity = item.Properties.RarityPvE?.ToString() ?? "Not_exist";

            switch (rarity)
            {
                case "Not_exist": targetWeight = weights.NotExist; break;
                case "Common": targetWeight = weights.Common; break;
                case "Rare": targetWeight = weights.Rare; break;
                case "Superrare": targetWeight = weights.SuperRare; break;
            }

            if (targetWeight <= 0) continue;

            var itemMongoId = new MongoId(item.Id);

            if (distDict.TryGetValue(itemMongoId, out var existingItem))
            {
                if (existingItem.RelativeProbability < targetWeight)
                {
                    existingItem.RelativeProbability = targetWeight;
                    distDict[itemMongoId] = existingItem;
                }
            }
            else
            {
                distDict[itemMongoId] = new ItemDistribution
                {
                    Tpl = itemMongoId,
                    RelativeProbability = targetWeight
                };
            }

            // Track this key for Phase 4 price modifications
            _injectedKeysService.InjectedKeyIds.Add(item.Id);
        }

        // Clamp total weight to prevent SPT map load crashes (int.MaxValue blast radius)
        long totalWeight = distDict.Values.Sum(x => (long)x.RelativeProbability);
        if (totalWeight > int.MaxValue)
        {
            _logger.Warning($"[KeysInLootExtended] A container's weight exceeds int.MaxValue! Normalizing weights...");
            double scale = (double)int.MaxValue / totalWeight;
            foreach (var key in distDict.Keys.ToList())
            {
                var entry = distDict[key];
                entry.RelativeProbability = (int)(entry.RelativeProbability * scale);
                distDict[key] = entry;
            }
        }

        container.ItemDistribution = distDict.Values.ToArray();
    }
}
