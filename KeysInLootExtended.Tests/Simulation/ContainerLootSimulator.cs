using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Services.Items;
using SPTarkov.Server.Core.Services.Server;

namespace KeysInLootExtended.Tests.Simulation;

public sealed record AnalyticalContainerMetrics(
    string MapName,
    string ContainerKind,
    MongoId ContainerId,
    bool ExistsOnMap,
    int GridCellsH,
    int GridCellsV,
    int GridMaxCount,
    int UniquePoolItems,
    double TotalPoolWeight,
    double TotalKeyAndKeycardWeight,
    double ExpectedTotalItemsPerContainer,
    double ExpectedKeysPer100Containers,
    double ExpectedCommonPer100,
    double ExpectedRarePer100,
    double ExpectedSuperRarePer100,
    double ExpectedNotExistPer100);

public sealed record MonteCarloContainerMetrics(
    int Trials,
    double SimulatedItemsPerContainer,
    double SimulatedKeysPer100Containers,
    double ContainersWithAtLeastOneKeyPercent,
    int DroppedByNoDuplicates,
    int DroppedByMaxCount,
    int DroppedByGridFit);

public sealed record ProfileSimulationRun(
    string NormalizedProfile,
    InMemorySptEnvironment Environment,
    KeysInLootConfigLoader ConfigLoader,
    InjectedKeysService InjectedKeysService,
    IReadOnlySet<MongoId> AllValidKeysAndKeycardsInDatabase,
    IReadOnlyDictionary<MongoId, string> ResolvedKeyRarities);

public static class ContainerLootSimulator
{
    public static readonly MongoId StandardJacketId = new("578f8778245977358849a9b5");
    public static readonly MongoId LabyrinthJacketId = new("67adf4a95247ac91530fcec7");
    public static readonly MongoId StandardDuffleId = new("578f87a3245977356274f2cb");
    public static readonly MongoId StandardDeadScavId = new("5909e4b686f7747f5b744fa4");
    public static readonly MongoId LabyrinthDeadScavId = new("67adf5752fc5ee84020a9940");

    /// <summary>
    /// Exact inactive Christmas & Halloween item TPLs excluded by SPT 4.1.6's
    /// <see cref="SeasonalEventService.GetInactiveSeasonalEventItems"/> outside active holiday events.
    /// </summary>
    private static readonly HashSet<MongoId> InactiveSeasonalItems = BuildInactiveSeasonalSet();

    public static readonly (string FolderName, string ConfigName)[] PlayableMaps =
    [
        ("bigmap", "customs"),
        ("factory4_day", "factory_day"),
        ("factory4_night", "factory_night"),
        ("interchange", "interchange"),
        ("laboratory", "laboratory"),
        ("lighthouse", "lighthouse"),
        ("rezervbase", "reserve"),
        ("sandbox", "ground_zero"),
        ("sandbox_high", "ground_zero_high"),
        ("shoreline", "shoreline"),
        ("tarkovstreets", "streets_of_tarkov"),
        ("woods", "woods"),
        ("labyrinth", "labyrinth")
    ];

    private static HashSet<MongoId> BuildInactiveSeasonalSet()
    {
        var svc = new SeasonalEventService(
            null!, null!, null!, null!, null!, null!, null!, null!,
            null!, null!, null!, null!, null!, null!, null!);
        return svc.GetInactiveSeasonalEventItems();
    }

    public static async Task<ProfileSimulationRun> ExecuteModInMemoryAsync(
        SptDatabaseSnapshot snapshot,
        string rawProfile,
        Action<KeysInLootCoreConfig>? configureCustom = null)
    {
        var env = snapshot.CreateFreshEnvironment();
        var modHelper = (ModHelper)RuntimeHelpers.GetUninitializedObject(typeof(ModHelper));
        var configLoader = new KeysInLootConfigLoader(NoOpLoggerProxy.Create<KeysInLootConfigLoader>(), modHelper);

        ApplyProfileToLoader(configLoader, rawProfile, configureCustom);

        var itemFilterService = new ItemFilterService(env.ItemConfig);
        var injectedKeysService = new InjectedKeysService();

        var lootService = new LootInjectionService(
            NoOpLoggerProxy.Create<LootInjectionService>(),
            env.TemplateTable,
            env.LocationTable,
            configLoader,
            injectedKeysService,
            itemFilterService);

        var priceService = new ItemPriceService(
            NoOpLoggerProxy.Create<ItemPriceService>(),
            env.TemplateTable,
            configLoader,
            injectedKeysService,
            env.TraderConfig);

        var gridService = new ContainerGridService(
            NoOpLoggerProxy.Create<ContainerGridService>(),
            env.TemplateTable,
            configLoader);

        var plugin = new KeysInLootExtendedPlugin(
            configLoader,
            lootService,
            priceService,
            gridService,
            NoOpLoggerProxy.Create<KeysInLootExtendedPlugin>());

        await plugin.OnLoadAsync(CancellationToken.None);

        var (allDbKeys, rarityMap) = BuildKeyReferenceCatalog(env, itemFilterService);

        return new ProfileSimulationRun(
            configLoader.Config.Enabled ? configLoader.Config.Preset : "disabled",
            env,
            configLoader,
            injectedKeysService,
            allDbKeys,
            rarityMap);
    }

    public static AnalyticalContainerMetrics ComputeAnalyticalMetrics(
        ProfileSimulationRun run,
        string mapFolder,
        string mapConfigName,
        string containerKind,
        MongoId preferredContainerId)
    {
        var locDict = run.Environment.LocationTable.GetDictionary();
        var location = locDict.Values.FirstOrDefault(l =>
            string.Equals(l?.Base?.Id, mapFolder, StringComparison.OrdinalIgnoreCase));

        var resolvedLoot = location?.StaticLoot?.Value;
        var gridProps = run.Environment.TemplateTable.Items.TryGetValue(preferredContainerId, out var cTpl)
            ? cTpl.Properties?.Grids?.FirstOrDefault()?.Properties
            : null;
        int cellsH = gridProps?.CellsH ?? 0;
        int cellsV = gridProps?.CellsV ?? 0;
        int maxCount = (int)(gridProps?.MaxCount ?? 0);

        if (resolvedLoot == null || !resolvedLoot.TryGetValue(preferredContainerId, out var container))
        {
            return new AnalyticalContainerMetrics(
                mapConfigName, containerKind, preferredContainerId,
                ExistsOnMap: false, cellsH, cellsV, maxCount,
                UniquePoolItems: 0, TotalPoolWeight: 0, TotalKeyAndKeycardWeight: 0,
                ExpectedTotalItemsPerContainer: 0, ExpectedKeysPer100Containers: 0,
                ExpectedCommonPer100: 0, ExpectedRarePer100: 0, ExpectedSuperRarePer100: 0, ExpectedNotExistPer100: 0);
        }

        var itemFilterService = new ItemFilterService(run.Environment.ItemConfig);

        // Apply exact SPT LocationLootGenerator.GetPossibleLootItemsForContainer seasonal & lootable blacklist filters
        var itemDist = (container.ItemDistribution ?? [])
            .Where(i => !InactiveSeasonalItems.Contains(i.Tpl) && !itemFilterService.IsLootableItemBlacklisted(i.Tpl))
            .ToArray();
        var countDist = (container.ItemCountDistribution ?? []).ToArray();

        double totalCountProb = countDist.Sum(c => (double)(c.RelativeProbability ?? 0f));
        double expectedItems = totalCountProb > 0
            ? countDist.Sum(c => (c.Count ?? 0.0) * (c.RelativeProbability ?? 0f)) / totalCountProb
            : 0.0;

        double totalWeight = 0.0;
        double commonW = 0.0;
        double rareW = 0.0;
        double superRareW = 0.0;
        double notExistW = 0.0;

        var keySet = run.InjectedKeysService.InjectedKeyIds.Count > 0
            ? (IReadOnlySet<MongoId>)run.InjectedKeysService.InjectedKeyIds
            : run.AllValidKeysAndKeycardsInDatabase;

        foreach (var entry in itemDist)
        {
            double w = entry.RelativeProbability ?? 0.0;
            totalWeight += w;
            if (w > 0 && keySet.Contains(entry.Tpl))
            {
                string r = run.ResolvedKeyRarities.TryGetValue(entry.Tpl, out var rVal) ? rVal : "not_exist";
                switch (r)
                {
                    case "common": commonW += w; break;
                    case "rare": rareW += w; break;
                    case "superrare": superRareW += w; break;
                    default: notExistW += w; break;
                }
            }
        }

        double totalKeyW = commonW + rareW + superRareW + notExistW;
        double scale100 = totalWeight > 0 ? (100.0 * expectedItems / totalWeight) : 0.0;

        return new AnalyticalContainerMetrics(
            mapConfigName,
            containerKind,
            preferredContainerId,
            ExistsOnMap: true,
            cellsH,
            cellsV,
            maxCount,
            UniquePoolItems: itemDist.Length,
            TotalPoolWeight: totalWeight,
            TotalKeyAndKeycardWeight: totalKeyW,
            ExpectedTotalItemsPerContainer: expectedItems,
            ExpectedKeysPer100Containers: totalKeyW * scale100,
            ExpectedCommonPer100: commonW * scale100,
            ExpectedRarePer100: rareW * scale100,
            ExpectedSuperRarePer100: superRareW * scale100,
            ExpectedNotExistPer100: notExistW * scale100);
    }

    /// <summary>
    /// Runs a deterministic seeded Monte Carlo simulation of N physical container openings,
    /// matching SPT 4.1.6's LocationLootGenerator rules:
    /// - Seasonal & lootable-blacklist item filtering
    /// - Configurable duplicate toggle (SPT 4.1.6 defaults allowDuplicates = true in configs/location.json)
    /// - SPT's exact <see cref="ContainerExtensions.FindSlotForItem"/> and <see cref="ContainerExtensions.TryFillContainerMapWithItem"/>
    /// - SPT's FitLootIntoContainerAttempts = 3 break threshold + EFT client GridProperties.MaxCount cap
    /// </summary>
    public static MonteCarloContainerMetrics RunMonteCarlo(
        ProfileSimulationRun run,
        string mapFolder,
        MongoId containerId,
        int trials = 10_000,
        int seed = 1337,
        bool allowDuplicates = true,
        int fitLootIntoContainerAttempts = 3)
    {
        var locDict = run.Environment.LocationTable.GetDictionary();
        var location = locDict.Values.FirstOrDefault(l =>
            string.Equals(l?.Base?.Id, mapFolder, StringComparison.OrdinalIgnoreCase));

        var resolvedLoot = location?.StaticLoot?.Value;
        if (resolvedLoot == null || !resolvedLoot.TryGetValue(containerId, out var container))
        {
            return new MonteCarloContainerMetrics(trials, 0, 0, 0, 0, 0, 0);
        }

        var gridProps = run.Environment.TemplateTable.Items[containerId].Properties!.Grids!.First().Properties!;
        int cellsH = Math.Max(1, gridProps.CellsH ?? 1);
        int cellsV = Math.Max(1, gridProps.CellsV ?? 1);
        int maxCount = Math.Max(0, (int)(gridProps.MaxCount ?? (cellsH * cellsV)));

        var countEntries = (container.ItemCountDistribution ?? [])
            .Where(c => (c.RelativeProbability ?? 0f) > 0)
            .Select(c => (Count: (int)(c.Count ?? 0), Prob: (double)(c.RelativeProbability ?? 0f)))
            .ToArray();
        double totalCountProb = countEntries.Sum(c => c.Prob);

        var itemFilterService = new ItemFilterService(run.Environment.ItemConfig);
        var basePool = (container.ItemDistribution ?? [])
            .Where(i => (i.RelativeProbability ?? 0f) > 0
                        && !InactiveSeasonalItems.Contains(i.Tpl)
                        && !itemFilterService.IsLootableItemBlacklisted(i.Tpl))
            .GroupBy(i => i.Tpl)
            .Select(g => (Tpl: g.Key, Weight: g.Sum(x => (double)(x.RelativeProbability ?? 0f))))
            .ToArray();
        double baseTotalWeight = basePool.Sum(x => x.Weight);

        if (totalCountProb <= 0 || basePool.Length == 0 || baseTotalWeight <= 0)
        {
            return new MonteCarloContainerMetrics(trials, 0, 0, 0, 0, 0, 0);
        }

        var keySet = run.InjectedKeysService.InjectedKeyIds.Count > 0
            ? (IReadOnlySet<MongoId>)run.InjectedKeysService.InjectedKeyIds
            : run.AllValidKeysAndKeycardsInDatabase;

        var rng = new Random(seed);
        var drawnIndices = new HashSet<int>();
        var containerMap = new int[cellsV, cellsH];

        long totalPlacedItems = 0;
        long totalPlacedKeys = 0;
        int containersWithKey = 0;
        int droppedNoDups = 0;
        int droppedMaxCount = 0;
        int droppedGridFit = 0;

        var cumCounts = new double[countEntries.Length];
        double runningC = 0;
        for (int i = 0; i < countEntries.Length; i++)
        {
            runningC += countEntries[i].Prob;
            cumCounts[i] = runningC;
        }

        var cumWeights = new double[basePool.Length];
        double runningW = 0;
        for (int i = 0; i < basePool.Length; i++)
        {
            runningW += basePool[i].Weight;
            cumWeights[i] = runningW;
        }

        for (int t = 0; t < trials; t++)
        {
            double countRoll = rng.NextDouble() * totalCountProb;
            int targetCount = 0;
            for (int i = 0; i < cumCounts.Length; i++)
            {
                if (countRoll < cumCounts[i])
                {
                    targetCount = countEntries[i].Count;
                    break;
                }
            }

            if (targetCount <= 0)
            {
                continue;
            }

            Array.Clear(containerMap);
            drawnIndices.Clear();
            double remainingWeight = baseTotalWeight;
            int placedInContainer = 0;
            int failedFitAttempts = 0;
            bool foundKeyInContainer = false;

            for (int slot = 0; slot < targetCount; slot++)
            {
                int chosenIdx;
                if (allowDuplicates)
                {
                    double r = rng.NextDouble() * baseTotalWeight;
                    int idx = Array.BinarySearch(cumWeights, r);
                    if (idx < 0) idx = ~idx;
                    chosenIdx = Math.Clamp(idx, 0, basePool.Length - 1);
                }
                else
                {
                    if (drawnIndices.Count >= basePool.Length || remainingWeight <= 0)
                    {
                        droppedNoDups += (targetCount - slot);
                        break;
                    }
                    chosenIdx = DrawWithoutReplacement(rng, basePool, cumWeights, drawnIndices, remainingWeight);
                    drawnIndices.Add(chosenIdx);
                    remainingWeight -= basePool[chosenIdx].Weight;
                }

                if (placedInContainer >= maxCount)
                {
                    droppedMaxCount++;
                    continue;
                }

                var tpl = basePool[chosenIdx].Tpl;
                var (itemW, itemH) = run.Environment.ItemDimensions.TryGetValue(tpl, out var dim) ? dim : (1, 1);

                // Invoke SPT 4.1.6's actual compiled ContainerExtensions.FindSlotForItem & TryFillContainerMapWithItem
                var findSlot = containerMap.FindSlotForItem(itemW, itemH);
                if (!(findSlot.Success ?? false))
                {
                    droppedGridFit++;
                    if (failedFitAttempts > fitLootIntoContainerAttempts)
                    {
                        break;
                    }
                    failedFitAttempts++;
                    continue;
                }

                containerMap.TryFillContainerMapWithItem(
                    findSlot.X!.Value,
                    findSlot.Y!.Value,
                    itemW,
                    itemH,
                    findSlot.Rotation ?? false,
                    out _);

                placedInContainer++;
                totalPlacedItems++;
                if (keySet.Contains(tpl))
                {
                    totalPlacedKeys++;
                    foundKeyInContainer = true;
                }
            }

            if (foundKeyInContainer)
            {
                containersWithKey++;
            }
        }

        return new MonteCarloContainerMetrics(
            Trials: trials,
            SimulatedItemsPerContainer: (double)totalPlacedItems / trials,
            SimulatedKeysPer100Containers: 100.0 * totalPlacedKeys / trials,
            ContainersWithAtLeastOneKeyPercent: 100.0 * containersWithKey / trials,
            DroppedByNoDuplicates: droppedNoDups,
            DroppedByMaxCount: droppedMaxCount,
            DroppedByGridFit: droppedGridFit);
    }

    private static int DrawWithoutReplacement(
        Random rng,
        (MongoId Tpl, double Weight)[] basePool,
        double[] cumWeights,
        HashSet<int> drawnIndices,
        double remainingWeight)
    {
        if (drawnIndices.Count == 0)
        {
            double r0 = rng.NextDouble() * remainingWeight;
            int idx = Array.BinarySearch(cumWeights, r0);
            if (idx < 0) idx = ~idx;
            return Math.Clamp(idx, 0, basePool.Length - 1);
        }

        double roll = rng.NextDouble() * remainingWeight;
        double acc = 0;
        for (int i = 0; i < basePool.Length; i++)
        {
            if (drawnIndices.Contains(i))
            {
                continue;
            }
            acc += basePool[i].Weight;
            if (roll < acc)
            {
                return i;
            }
        }

        for (int i = basePool.Length - 1; i >= 0; i--)
        {
            if (!drawnIndices.Contains(i))
            {
                return i;
            }
        }
        return 0;
    }

    internal static void ApplyProfileToLoader(
        KeysInLootConfigLoader configLoader,
        string rawProfile,
        Action<KeysInLootCoreConfig>? configureCustom = null)
    {
        string configPath = System.IO.Path.Combine(AppContext.BaseDirectory, "config.jsonc");
        var freshConfig = JsonSerializer.Deserialize<KeysInLootCoreConfig>(
                File.ReadAllText(configPath),
                KeysInLootConfigLoader.JsonSettings)
            ?? throw new InvalidDataException("Failed to deserialize config.jsonc in test simulator.");

        if (string.Equals(rawProfile?.Trim(), "disabled", StringComparison.OrdinalIgnoreCase)
            || string.Equals(rawProfile?.Trim(), "off", StringComparison.OrdinalIgnoreCase))
        {
            freshConfig.Enabled = false;
        }
        else
        {
            freshConfig.Enabled = true;
            freshConfig.Preset = rawProfile ?? "Balanced";
        }

        configureCustom?.Invoke(freshConfig);
        KeysInLootConfigLoader.ApplyResolvedSettings(freshConfig);

        typeof(KeysInLootConfigLoader)
            .GetProperty(nameof(KeysInLootConfigLoader.Config))!
            .SetValue(configLoader, freshConfig);
    }

    private static (HashSet<MongoId> AllKeys, Dictionary<MongoId, string> RarityByKey) BuildKeyReferenceCatalog(
        InMemorySptEnvironment env,
        ItemFilterService itemFilterService)
    {
        const string keyBase = "543be5e94bdc2df1348b4568";
        const string keycardBase = "5c164d2286f774194c5e69fa";

        var explicitMapField = typeof(LootInjectionService).GetField(
            "ExplicitRarityMap",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        var explicitMap = (Dictionary<string, string>)explicitMapField.GetValue(null)!;

        var allKeys = new HashSet<MongoId>();
        var rarities = new Dictionary<MongoId, string>();
        var validHandbookIds = env.TemplateTable.Handbook?.Items?
            .Where(h => h.Price is > 1.0)
            .Select(h => h.Id)
            .ToHashSet() ?? [];
        bool hasHandbook = validHandbookIds.Count > 0;

        bool IsBase(string itemId, string targetBase)
        {
            if (itemId == targetBase) return false;
            string cur = itemId;
            var vis = new HashSet<string>();
            while (!string.IsNullOrEmpty(cur) && vis.Add(cur))
            {
                if (cur == targetBase) return true;
                if (!env.TemplateTable.Items.TryGetValue(cur, out var t)) break;
                cur = t.Parent;
            }
            return false;
        }

        foreach (var item in env.TemplateTable.Items.Values)
        {
            if (item.Properties?.QuestItem == true) continue;
            if (string.Equals(item.Type, "Node", StringComparison.OrdinalIgnoreCase)) continue;

            MongoId id;
            try
            {
                id = new MongoId(item.Id);
                if (itemFilterService.IsItemBlacklisted(id) || itemFilterService.IsLootableItemBlacklisted(id))
                    continue;
                if (hasHandbook && !validHandbookIds.Contains(id))
                    continue;
            }
            catch (Exception ex) when (ex is FormatException or ArgumentException)
            {
                continue;
            }

            if (IsBase(item.Id, keycardBase) || IsBase(item.Id, keyBase))
            {
                string rarity = item.Properties?.RarityPvE?.ToString() ?? "Not_exist";
                if (explicitMap.TryGetValue(item.Id, out var mapped))
                {
                    rarity = mapped;
                }
                allKeys.Add(id);
                rarities[id] = rarity.ToLowerInvariant();
            }
        }

        return (allKeys, rarities);
    }
}
