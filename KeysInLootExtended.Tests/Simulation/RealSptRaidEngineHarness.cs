using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using SPTarkov.Common.Models.Logging;
using SPTarkov.Server.Core.Generators.Loot;
using SPTarkov.Server.Core.Helpers.InRaid;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Services.Items;
using SPTarkov.Server.Core.Services.Locales;
using SPTarkov.Server.Core.Services.Server;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Utils.Cloners;
using SPTarkov.Server.Core.Utils.Json;

namespace KeysInLootExtended.Tests.Simulation;

public sealed record RealRaidContainerStats(
    int RaidsSimulated,
    int TotalContainersSpawned,
    double AvgContainersPerRaid,
    int TotalItemsSpawned,
    int RandomKeysSpawned,
    int ForcedKeysSpawned,
    double RandomKeysPer100SpawnedContainers,
    double TotalKeysPer100IncludingForced,
    double AvgTotalKeysPerRaid);

/// <summary>
/// Boots SPT 4.1.6's actual compiled <see cref="LocationLootGenerator"/> against a live read-only
/// SPT_Data folder (with real staticContainers.json, statics.json, staticAmmo.json, globals.json
/// weapon presets, seasonal event filters, and per-raid LootMaxSpawnLimits) to verify 1:1 raid parity.
/// </summary>
public sealed class RealSptRaidEngineHarness
{
    private readonly LocationLootGenerator _generator;
    private readonly LocationTable _locationTable;
    private readonly LocationConfig _locationConfig;
    private readonly CounterTrackerHelper _counterTracker;
    private readonly ICloner _cloner;
    private readonly Dictionary<string, Dictionary<string, IEnumerable<StaticAmmoDetails>>> _staticAmmoByMap;
    public IReadOnlySet<MongoId> InjectedKeyIds { get; }

    private RealSptRaidEngineHarness(
        LocationLootGenerator generator,
        LocationTable locationTable,
        LocationConfig locationConfig,
        CounterTrackerHelper counterTracker,
        ICloner cloner,
        Dictionary<string, Dictionary<string, IEnumerable<StaticAmmoDetails>>> staticAmmoByMap,
        IReadOnlySet<MongoId> injectedKeyIds)
    {
        _generator = generator;
        _locationTable = locationTable;
        _locationConfig = locationConfig;
        _counterTracker = counterTracker;
        _cloner = cloner;
        _staticAmmoByMap = staticAmmoByMap;
        InjectedKeyIds = injectedKeyIds;
    }

    public static async Task<RealSptRaidEngineHarness> CreateAsync(string sptDataPath, string rawProfile)
    {
        string root = System.IO.Path.GetFullPath(sptDataPath);
        var asm = typeof(JsonUtil).Assembly;
        var regs = asm.GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface && typeof(IJsonConverterRegistrator).IsAssignableFrom(t)
                        && t.GetConstructors().Any(c => c.GetParameters().Length == 0))
            .Select(t => (IJsonConverterRegistrator)Activator.CreateInstance(t)!)
            .ToList();
        var jsonUtil = new JsonUtil(regs);

        var items = jsonUtil.DeserializeFromFile<Dictionary<MongoId, TemplateItem>>(
            System.IO.Path.Combine(root, "database", "templates", "items.json"))!;
        var handbook = jsonUtil.DeserializeFromFile<HandbookBase>(
            System.IO.Path.Combine(root, "database", "templates", "handbook.json"))!;
        var prices = jsonUtil.DeserializeFromFile<Dictionary<MongoId, double>>(
            System.IO.Path.Combine(root, "database", "templates", "prices.json"))!;
        var defaultEqPresets = jsonUtil.DeserializeFromFile<List<DefaultEquipmentPreset>>(
            System.IO.Path.Combine(root, "database", "templates", "defaultEquipmentPresets.json"))!;
        var globalTable = jsonUtil.DeserializeFromFile<GlobalTable>(
            System.IO.Path.Combine(root, "database", "globals.json"))!;
        var itemCfg = jsonUtil.DeserializeFromFile<ItemConfig>(
            System.IO.Path.Combine(root, "configs", "item.json"))!;
        var traderCfg = jsonUtil.DeserializeFromFile<TraderConfig>(
            System.IO.Path.Combine(root, "configs", "trader.json"))!;
        var locCfg = jsonUtil.DeserializeFromFile<LocationConfig>(
            System.IO.Path.Combine(root, "configs", "location.json"))!;
        var seasonalCfg = jsonUtil.DeserializeFromFile<SeasonalEventConfig>(
            System.IO.Path.Combine(root, "configs", "seasonalevents.json"))!;
        var localeCfg = jsonUtil.DeserializeFromFile<LocaleConfig>(
            System.IO.Path.Combine(root, "configs", "locale.json"))!;

        var templateTable = (TemplateTable)RuntimeHelpers.GetUninitializedObject(typeof(TemplateTable));
        typeof(TemplateTable).GetProperty("Items")!.SetValue(templateTable, items);
        typeof(TemplateTable).GetProperty("Handbook")!.SetValue(templateTable, handbook);
        typeof(TemplateTable).GetProperty("Prices")!.SetValue(templateTable, prices);
        typeof(TemplateTable).GetProperty("DefaultEquipmentPresets")!.SetValue(templateTable, defaultEqPresets);

        var locationTable = (LocationTable)RuntimeHelpers.GetUninitializedObject(typeof(LocationTable));
        var staticAmmoByMap = new Dictionary<string, Dictionary<string, IEnumerable<StaticAmmoDetails>>>(StringComparer.OrdinalIgnoreCase);

        var mapProps = new (string Folder, string PropName)[]
        {
            ("bigmap", nameof(LocationTable.Bigmap)),
            ("factory4_day", nameof(LocationTable.Factory4Day)),
            ("factory4_night", nameof(LocationTable.Factory4Night)),
            ("interchange", nameof(LocationTable.Interchange)),
            ("laboratory", nameof(LocationTable.Laboratory)),
            ("lighthouse", nameof(LocationTable.Lighthouse)),
            ("rezervbase", nameof(LocationTable.RezervBase)),
            ("sandbox", nameof(LocationTable.Sandbox)),
            ("sandbox_high", nameof(LocationTable.SandboxHigh)),
            ("shoreline", nameof(LocationTable.Shoreline)),
            ("tarkovstreets", nameof(LocationTable.TarkovStreets)),
            ("woods", nameof(LocationTable.Woods)),
            ("labyrinth", nameof(LocationTable.Labyrinth)),
        };

        foreach (var (folder, propName) in mapProps)
        {
            string mapDir = System.IO.Path.Combine(root, "database", "locations", folder);
            var loc = (Location)RuntimeHelpers.GetUninitializedObject(typeof(Location));
            var locBase = jsonUtil.DeserializeFromFile<LocationBase>(System.IO.Path.Combine(mapDir, "base.json"))!;
            var ammo = jsonUtil.DeserializeFromFile<Dictionary<string, IEnumerable<StaticAmmoDetails>>>(
                System.IO.Path.Combine(mapDir, "staticAmmo.json")) ?? new();
            staticAmmoByMap[folder] = ammo;

            string staticsFile = System.IO.Path.Combine(mapDir, "statics.json");
            if (File.Exists(staticsFile))
            {
                var statics = jsonUtil.DeserializeFromFile<StaticContainer>(staticsFile);
                typeof(Location).GetProperty("Statics")!.SetValue(loc, statics);
            }

            string staticLootFile = System.IO.Path.Combine(mapDir, "staticLoot.json");
            string staticContFile = System.IO.Path.Combine(mapDir, "staticContainers.json");

            typeof(Location).GetProperty("Base")!.SetValue(loc, locBase);
            typeof(Location).GetProperty("StaticAmmo")!.SetValue(loc, ammo);
            typeof(Location).GetProperty("StaticLoot")!.SetValue(loc,
                new LazyLoad<Dictionary<MongoId, StaticLootDetails>>(
                    () => jsonUtil.DeserializeFromFile<Dictionary<MongoId, StaticLootDetails>>(staticLootFile)!, false));
            typeof(Location).GetProperty("StaticContainers")!.SetValue(loc,
                new LazyLoad<StaticContainerDetails>(
                    () => jsonUtil.DeserializeFromFile<StaticContainerDetails>(staticContFile)!, false));

            typeof(LocationTable).GetProperty(propName)!.SetValue(locationTable, loc);
        }

        // Execute the real KeysInLootExtended plugin against these live tables BEFORE raid generation!
        var modHelper = (ModHelper)RuntimeHelpers.GetUninitializedObject(typeof(ModHelper));
        var configLoader = new KeysInLootConfigLoader(NoOpLoggerProxy.Create<KeysInLootConfigLoader>(), modHelper);
        ContainerLootSimulator.ApplyProfileToLoader(configLoader, rawProfile);

        var itemFilter = new ItemFilterService(itemCfg);
        var injectedKeysSvc = new InjectedKeysService();

        var plugin = new KeysInLootExtendedPlugin(
            configLoader,
            new LootInjectionService(NoOpLoggerProxy.Create<LootInjectionService>(), templateTable, locationTable, configLoader, injectedKeysSvc, itemFilter),
            new ItemPriceService(NoOpLoggerProxy.Create<ItemPriceService>(), templateTable, configLoader, injectedKeysSvc, traderCfg),
            new ContainerGridService(NoOpLoggerProxy.Create<ContainerGridService>(), templateTable, configLoader),
            NoOpLoggerProxy.Create<KeysInLootExtendedPlugin>());

        await plugin.OnLoadAsync(CancellationToken.None);

        // Wire SPT 4.1.6's actual LocationLootGenerator and its real item/preset/seasonal helpers
        ICloner cloner = new SPTarkov.Server.Core.Utils.Cloners.FastCloner();
        var rand = new RandomUtil(ValueSafeNoOpProxy.Create<ISptLogger<RandomUtil>>(), cloner);

        var localeTable = (LocaleTable)RuntimeHelpers.GetUninitializedObject(typeof(LocaleTable));
        typeof(LocaleTable).GetProperty("Global")!.SetValue(localeTable, new Dictionary<string, LazyLoad<GlobalLocaleDictionary>>
        {
            ["en"] = new(() => new GlobalLocaleDictionary(), true)
        });
        typeof(LocaleTable).GetProperty("Languages")!.SetValue(localeTable, new Dictionary<string, string> { ["en"] = "English" });

        var localeSvc = new LocaleService(ValueSafeNoOpProxy.Create<ISptLogger<LocaleService>>(), localeTable, localeCfg);
        var serverLocSvc = new InMemoryServerLocalisationService(rand, localeSvc, jsonUtil);

        var hbHelperType = typeof(ItemHelper).GetConstructors()[0].GetParameters()[3].ParameterType;
        var hbHelper = RuntimeHelpers.GetUninitializedObject(hbHelperType);

        var baseClassSvc = new ItemBaseClassService(ValueSafeNoOpProxy.Create<ISptLogger<ItemBaseClassService>>(), templateTable, serverLocSvc);
        baseClassSvc.HydrateItemBaseClassCache();

        var itemHelper = (ItemHelper)Activator.CreateInstance(typeof(ItemHelper),
        [
            ValueSafeNoOpProxy.Create<ISptLogger<ItemHelper>>(),
            templateTable,
            rand,
            hbHelper,
            baseClassSvc,
            itemFilter,
            serverLocSvc,
            localeSvc,
            cloner
        ])!;
        var presetHelper = new PresetHelper(globalTable, itemHelper, cloner);
        var seasonalSvc = new SeasonalEventService(
            ValueSafeNoOpProxy.Create<ISptLogger<SeasonalEventService>>(),
            localeTable, globalTable, null!, locationTable, null!, null!, serverLocSvc,
            null!, null!, locCfg, null!, seasonalCfg, null!, rand);
        var counterTracker = new CounterTrackerHelper();

        var generator = new LocationLootGenerator(
            ValueSafeNoOpProxy.Create<ISptLogger<LocationLootGenerator>>(),
            locationTable,
            rand,
            itemHelper,
            presetHelper,
            serverLocSvc,
            seasonalSvc,
            itemFilter,
            locCfg,
            seasonalCfg,
            counterTracker,
            cloner);

        return new RealSptRaidEngineHarness(
            generator, locationTable, locCfg, counterTracker, cloner, staticAmmoByMap, injectedKeysSvc.InjectedKeyIds);
    }

    public Dictionary<MongoId, RealRaidContainerStats> SimulateRealRaids(
        string mapFolder,
        int raidCount,
        IReadOnlyCollection<MongoId> trackedContainerIds)
    {
        var trackedSet = trackedContainerIds.ToHashSet();
        var countsByContainer = trackedSet.ToDictionary(
            id => id,
            _ => (Containers: 0, Items: 0, RandomKeys: 0, ForcedKeys: 0));

        string locId = mapFolder.ToLowerInvariant();
        var baseAmmo = _staticAmmoByMap[mapFolder];
        var forcedPairs = (_locationTable.GetLocation(locId)?.StaticContainers?.Value?.StaticForced ?? [])
            .Where(f => !string.IsNullOrEmpty(f.ContainerId))
            .Select(f => (ContainerId: f.ContainerId!, f.ItemTpl))
            .ToHashSet();

        for (int r = 0; r < raidCount; r++)
        {
            // Mirror LocationLootGenerator.GenerateLocationLoot per-raid spawn limit tracking
            var raidLimits = _cloner.Clone(_locationConfig.LootMaxSpawnLimits.GetValueOrDefault(locId));
            if (raidLimits != null)
            {
                _counterTracker.AddDataToTrack(raidLimits);
            }

            var spawned = _generator.GenerateStaticContainers(locId, _cloner.Clone(baseAmmo)!);
            _counterTracker.Clear();

            foreach (var sp in spawned)
            {
                var itemList = sp.Items?.ToList();
                if (itemList is not { Count: > 0 })
                {
                    continue;
                }

                var rootContainerTpl = itemList[0].Template;
                if (!trackedSet.Contains(rootContainerTpl))
                {
                    continue;
                }

                var rootId = itemList[0].Id;
                int rootLootItems = 0;
                int randomKeyItems = 0;
                int forcedKeyItems = 0;
                var creditedForcedTpls = new HashSet<MongoId>();

                for (int i = 1; i < itemList.Count; i++)
                {
                    var child = itemList[i];
                    // Count direct container items (SlotId == "main", ParentId == rootId) rather than weapon sub-attachments
                    if (!string.IsNullOrEmpty(child.ParentId) && child.ParentId == rootId && child.SlotId == "main")
                    {
                        rootLootItems++;
                        if (InjectedKeyIds.Contains(child.Template))
                        {
                            if (!string.IsNullOrEmpty(sp.Id)
                                && forcedPairs.Contains((sp.Id, child.Template))
                                && creditedForcedTpls.Add(child.Template))
                            {
                                forcedKeyItems++;
                            }
                            else
                            {
                                randomKeyItems++;
                            }
                        }
                    }
                }

                var prev = countsByContainer[rootContainerTpl];
                countsByContainer[rootContainerTpl] = (
                    prev.Containers + 1,
                    prev.Items + rootLootItems,
                    prev.RandomKeys + randomKeyItems,
                    prev.ForcedKeys + forcedKeyItems);
            }
        }

        return countsByContainer.ToDictionary(
            kvp => kvp.Key,
            kvp =>
            {
                var (c, it, rk, fk) = kvp.Value;
                int totalK = rk + fk;
                return new RealRaidContainerStats(
                    RaidsSimulated: raidCount,
                    TotalContainersSpawned: c,
                    AvgContainersPerRaid: (double)c / raidCount,
                    TotalItemsSpawned: it,
                    RandomKeysSpawned: rk,
                    ForcedKeysSpawned: fk,
                    RandomKeysPer100SpawnedContainers: c > 0 ? (100.0 * rk / c) : 0.0,
                    TotalKeysPer100IncludingForced: c > 0 ? (100.0 * totalK / c) : 0.0,
                    AvgTotalKeysPerRaid: (double)totalK / raidCount);
            });
    }

    private sealed class InMemoryServerLocalisationService(RandomUtil rand, LocaleService localeSvc, JsonUtil jsonUtil)
        : ServerLocalisationService(ValueSafeNoOpProxy.Create<ISptLogger<ServerLocalisationService>>(), rand, localeSvc, jsonUtil, null!)
    {
        protected override void LoadLocales()
        {
            LoadedLocales["en"] = new LazyLoad<Dictionary<string, string>>(() => new Dictionary<string, string>(), true);
            LocalesHydrated = true;
        }
    }

    private class ValueSafeNoOpProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            var ret = targetMethod?.ReturnType;
            if (ret == null || ret == typeof(void))
            {
                return null;
            }
            return ret.IsValueType ? Activator.CreateInstance(ret) : null;
        }

        public static T Create<T>() => Create<T, ValueSafeNoOpProxy>();
    }
}
