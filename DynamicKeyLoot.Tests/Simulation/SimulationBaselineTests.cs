using System.Text;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Services.Items;
using Xunit;
using Xunit.Abstractions;

namespace DynamicKeyLoot.Tests.Simulation;

public class SimulationBaselineTests
{
    private readonly ITestOutputHelper _output;

    public SimulationBaselineTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task GoldenFixture_AnalyticalAndMonteCarlo_ShouldAgreeWithinStatisticalBounds()
    {
        var snapshot = SptDatabaseLoader.LoadGoldenFixture();
        var run = await ContainerLootSimulator.ExecuteModInMemoryAsync(snapshot, "Balanced");

        var analytical = ContainerLootSimulator.ComputeAnalyticalMetrics(
            run, "bigmap", "customs", "Jacket", ContainerLootSimulator.StandardJacketId);
        var mc = ContainerLootSimulator.RunMonteCarlo(
            run, "bigmap", ContainerLootSimulator.StandardJacketId, trials: 15_000, seed: 1337);

        Assert.True(analytical.ExistsOnMap);
        Assert.Equal(3, analytical.GridCellsH);
        Assert.Equal(3, analytical.GridCellsV);
        Assert.Equal(9, analytical.GridMaxCount);

        Assert.InRange(
            Math.Abs(analytical.ExpectedKeysPer100Containers - mc.SimulatedKeysPer100Containers),
            0.0,
            1.5);
        Assert.Equal(0, mc.DroppedByMaxCount);
    }

    [Theory]
    [InlineData("Hardcore")]
    [InlineData("Balanced")]
    [InlineData("Casual")]
    [InlineData("Custom")]
    [InlineData("Disabled")]
    public async Task AllPresets_ShouldNeverInjectQuestNodeOrBlacklistedItems(string preset)
    {
        var snapshot = SptDatabaseLoader.LoadGoldenFixture();
        var run = await ContainerLootSimulator.ExecuteModInMemoryAsync(snapshot, preset);
        var filterService = new ItemFilterService(run.Environment.ItemConfig);

        if (preset == "Disabled")
        {
            Assert.Empty(run.InjectedKeysService.InjectedKeyIds);
            return;
        }

        foreach (var id in run.InjectedKeysService.InjectedKeyIds)
        {
            var tpl = run.Environment.TemplateTable.Items[id];
            Assert.False(tpl.Properties?.QuestItem == true, $"Quest item {id} was injected!");
            Assert.False(string.Equals(tpl.Type, "Node", StringComparison.OrdinalIgnoreCase), $"Node {id} was injected!");
            Assert.False(filterService.IsItemBlacklisted(id), $"Blacklisted item {id} was injected!");
            Assert.False(filterService.IsLootableItemBlacklisted(id), $"Lootable-blacklisted item {id} was injected!");
        }
    }

    [Fact]
    public async Task MonteCarlo_ShouldEnforceMaxCountCap_AndSupportHighDensityCustomGrids()
    {
        var snapshot = SptDatabaseLoader.LoadGoldenFixture();

        // Simulate what happens if Casual's OverrideLootDistribution ran WITHOUT ContainerGridService expanding MaxCount=1
        var run = await ContainerLootSimulator.ExecuteModInMemoryAsync(snapshot, "Casual");
        var jacketGrid = run.Environment.TemplateTable.Items[ContainerLootSimulator.StandardJacketId]
            .Properties!.Grids!.First().Properties!;
        jacketGrid.MaxCount = 1;

        var mc = ContainerLootSimulator.RunMonteCarlo(
            run, "bigmap", ContainerLootSimulator.StandardJacketId, trials: 5_000, seed: 42);

        Assert.True(mc.DroppedByMaxCount > 3_000);
        Assert.InRange(mc.SimulatedItemsPerContainer, 0.90, 1.00);

        // Verify a high-density 5x5 Custom setup (10-25 items) runs cleanly without overflow or crashes
        var sandboxCounts = new List<ItemCountDistributionConfig>
        {
            new() { Count = 25, RelativeProbability = 100 },
            new() { Count = 20, RelativeProbability = 300 },
            new() { Count = 15, RelativeProbability = 500 },
            new() { Count = 10, RelativeProbability = 100 }
        };
        var customRun = await ContainerLootSimulator.ExecuteModInMemoryAsync(snapshot, "Custom", c =>
        {
            c.EnableLocationsConfig = false;
            c.Custom.KeyWeight = new DynamicKeyLootRarityConfig { NotExist = 10, Common = 10, Rare = 5000, SuperRare = 10000 };
            c.Custom.KeycardWeight = new DynamicKeyLootRarityConfig { NotExist = 10, Common = 10, Rare = 1000, SuperRare = 5000 };
            c.Custom.OverrideLootDistribution = true;
            c.Custom.JacketsItemCounts = sandboxCounts;
            c.Custom.MinCellsH = 5;
            c.Custom.MinCellsV = 5;
        });

        var customMc = ContainerLootSimulator.RunMonteCarlo(
            customRun, "bigmap", ContainerLootSimulator.StandardJacketId, trials: 5_000, seed: 42);
        Assert.InRange(customMc.SimulatedKeysPer100Containers, 1200.0, 1650.0);
    }

    [Fact]
    public async Task Presets_ShouldAscendMonotonically_PreserveNonKeyLoot_AndSupportReduceCommonKeys()
    {
        var snapshot = SptDatabaseLoader.LoadGoldenFixture();
        var disabled = await ContainerLootSimulator.ExecuteModInMemoryAsync(snapshot, "Disabled");
        var hardcore = await ContainerLootSimulator.ExecuteModInMemoryAsync(snapshot, "Hardcore");
        var balanced = await ContainerLootSimulator.ExecuteModInMemoryAsync(snapshot, "Balanced");
        var casual = await ContainerLootSimulator.ExecuteModInMemoryAsync(snapshot, "Casual");

        Assert.False(hardcore.ConfigLoader.Config.Resolved.OverrideLootDistribution);

        var vanJacket = ContainerLootSimulator.ComputeAnalyticalMetrics(
            disabled, "bigmap", "customs", "Jacket", ContainerLootSimulator.StandardJacketId);
        var hcJacket = ContainerLootSimulator.ComputeAnalyticalMetrics(
            hardcore, "bigmap", "customs", "Jacket", ContainerLootSimulator.StandardJacketId);
        var balJacket = ContainerLootSimulator.ComputeAnalyticalMetrics(
            balanced, "bigmap", "customs", "Jacket", ContainerLootSimulator.StandardJacketId);
        var casJacket = ContainerLootSimulator.ComputeAnalyticalMetrics(
            casual, "bigmap", "customs", "Jacket", ContainerLootSimulator.StandardJacketId);

        Assert.Equal(2, hcJacket.GridCellsH);
        Assert.Equal(2, hcJacket.GridCellsV);
        Assert.Equal(vanJacket.ExpectedTotalItemsPerContainer, hcJacket.ExpectedTotalItemsPerContainer, precision: 6);

        // Strict monotonic progression: Hardcore < Balanced < Casual
        Assert.True(hcJacket.ExpectedKeysPer100Containers < balJacket.ExpectedKeysPer100Containers);
        Assert.True(balJacket.ExpectedKeysPer100Containers < casJacket.ExpectedKeysPer100Containers);

        // Balanced preserves non-key Jacket items and leaves Duffle & Dead Scav counts vanilla
        double vanNonKeyJacket = (vanJacket.ExpectedTotalItemsPerContainer * 100.0) - vanJacket.ExpectedKeysPer100Containers;
        double balNonKeyJacket = (balJacket.ExpectedTotalItemsPerContainer * 100.0) - balJacket.ExpectedKeysPer100Containers;
        Assert.InRange(balNonKeyJacket, vanNonKeyJacket * 0.95, vanNonKeyJacket * 1.20);

        var vanDuffle = ContainerLootSimulator.ComputeAnalyticalMetrics(
            disabled, "bigmap", "customs", "Duffle", ContainerLootSimulator.StandardDuffleId);
        var balDuffle = ContainerLootSimulator.ComputeAnalyticalMetrics(
            balanced, "bigmap", "customs", "Duffle", ContainerLootSimulator.StandardDuffleId);
        Assert.Equal(vanDuffle.ExpectedTotalItemsPerContainer, balDuffle.ExpectedTotalItemsPerContainer, precision: 6);

        var vanScav = ContainerLootSimulator.ComputeAnalyticalMetrics(
            disabled, "bigmap", "customs", "DeadScav", ContainerLootSimulator.StandardDeadScavId);
        var balScav = ContainerLootSimulator.ComputeAnalyticalMetrics(
            balanced, "bigmap", "customs", "DeadScav", ContainerLootSimulator.StandardDeadScavId);
        Assert.Equal(vanScav.ExpectedTotalItemsPerContainer, balScav.ExpectedTotalItemsPerContainer, precision: 6);

        // Verify reduceCommonKeys=true achieves >= 85% Rare+SuperRare share across Hardcore, Balanced, and Casual
        foreach (var preset in new[] { "Hardcore", "Balanced", "Casual" })
        {
            var filteredRun = await ContainerLootSimulator.ExecuteModInMemoryAsync(
                snapshot, preset, c => c.ReduceCommonKeys = true);
            var m = ContainerLootSimulator.ComputeAnalyticalMetrics(
                filteredRun, "bigmap", "customs", "Jacket", ContainerLootSimulator.StandardJacketId);
            double highTierShare = (m.ExpectedRarePer100 + m.ExpectedSuperRarePer100) / m.ExpectedKeysPer100Containers;
            Assert.True(highTierShare >= 0.85, $"Expected {preset} + reduceCommonKeys Rare/SR share >= 85%, got {highTierShare:P1}");
        }
    }

    [Fact]
    public async Task Labyrinth_ShouldExpandGrids_AndInjectKeysIntoLabyrinthJacketAndDeadScav()
    {
        var snapshot = SptDatabaseLoader.LoadGoldenFixture();
        var run = await ContainerLootSimulator.ExecuteModInMemoryAsync(snapshot, "Balanced");

        var labJacket = ContainerLootSimulator.ComputeAnalyticalMetrics(
            run, "labyrinth", "labyrinth", "Jacket", ContainerLootSimulator.LabyrinthJacketId);
        var labScav = ContainerLootSimulator.ComputeAnalyticalMetrics(
            run, "labyrinth", "labyrinth", "DeadScav", ContainerLootSimulator.LabyrinthDeadScavId);

        Assert.True(labJacket.ExistsOnMap);
        Assert.Equal(3, labJacket.GridCellsH);
        Assert.Equal(3, labJacket.GridCellsV);
        Assert.Equal(9, labJacket.GridMaxCount);
        Assert.True(labJacket.ExpectedKeysPer100Containers > 15.0);

        Assert.True(labScav.ExistsOnMap);
        Assert.Equal(4, labScav.GridCellsH);
        Assert.Equal(4, labScav.GridCellsV);
        Assert.Equal(16, labScav.GridMaxCount);
        Assert.True(labScav.ExpectedKeysPer100Containers > 4.0);
    }

    [Fact]
    public async Task InjectedKeys_ShouldExcludeHandbookMissingPlaceholders_AndOneRoublePuzzleProps()
    {
        var snapshot = SptDatabaseLoader.LoadGoldenFixture();
        var run = await ContainerLootSimulator.ExecuteModInMemoryAsync(snapshot, "Balanced");

        var valveHandwheel = new MongoId("678fa929819ddc4c350c0317");
        var ariadneKey = new MongoId("67e183377c6c2011970f3149");
        var cutCityKey = new MongoId("63a39e0f64283b5e9c56b282");

        Assert.DoesNotContain(valveHandwheel, run.InjectedKeysService.InjectedKeyIds);
        Assert.DoesNotContain(ariadneKey, run.InjectedKeysService.InjectedKeyIds);
        Assert.DoesNotContain(cutCityKey, run.InjectedKeysService.InjectedKeyIds);

        var handbookPrices = run.Environment.TemplateTable.Handbook.Items
            .ToDictionary(h => h.Id, h => h.Price ?? 0.0);

        foreach (var id in run.InjectedKeysService.InjectedKeyIds)
        {
            Assert.True(handbookPrices.TryGetValue(id, out var hbPrice) && hbPrice > 1.0,
                $"Injected item {id} is missing from Handbook or has placeholder price <= 1 RUB.");
        }

        Assert.Equal(217, run.InjectedKeysService.InjectedKeyIds.Count);
    }

    [Fact]
    public async Task All13PlayableMaps_OnBalancedPreset_ShouldStayWithinTargetDropBands()
    {
        var snapshot = SptDatabaseLoader.LoadGoldenFixture();
        var vanillaRun = await ContainerLootSimulator.ExecuteModInMemoryAsync(snapshot, "Disabled");
        var balancedRun = await ContainerLootSimulator.ExecuteModInMemoryAsync(snapshot, "Balanced");

        foreach (var (folder, cfgName) in ContainerLootSimulator.PlayableMaps)
        {
            MongoId jId = folder == "labyrinth"
                ? ContainerLootSimulator.LabyrinthJacketId
                : ContainerLootSimulator.StandardJacketId;
            var jBal = ContainerLootSimulator.ComputeAnalyticalMetrics(balancedRun, folder, cfgName, "Jacket", jId);
            var jVan = ContainerLootSimulator.ComputeAnalyticalMetrics(vanillaRun, folder, cfgName, "Jacket", jId);

            Assert.True(jBal.ExistsOnMap, $"Jacket missing on {cfgName}");
            Assert.True(jBal.ExpectedKeysPer100Containers > jVan.ExpectedKeysPer100Containers,
                $"{cfgName} Jacket Balanced ({jBal.ExpectedKeysPer100Containers:F2}) should exceed Vanilla ({jVan.ExpectedKeysPer100Containers:F2})");
            Assert.InRange(jBal.ExpectedKeysPer100Containers, 21.0, 28.0);

            var dBal = ContainerLootSimulator.ComputeAnalyticalMetrics(
                balancedRun, folder, cfgName, "Duffle", ContainerLootSimulator.StandardDuffleId);
            if (dBal.ExistsOnMap)
            {
                Assert.InRange(dBal.ExpectedKeysPer100Containers, 9.5, 14.0);
            }

            MongoId sId = folder == "labyrinth"
                ? ContainerLootSimulator.LabyrinthDeadScavId
                : ContainerLootSimulator.StandardDeadScavId;
            var sBal = ContainerLootSimulator.ComputeAnalyticalMetrics(balancedRun, folder, cfgName, "DeadScav", sId);
            if (sBal.ExistsOnMap)
            {
                Assert.InRange(sBal.ExpectedKeysPer100Containers, 5.5, 17.5);
                if (cfgName == "customs")
                {
                    Assert.InRange(sBal.ExpectedKeysPer100Containers, 14.0, 17.5);
                }
            }
        }
    }

    [Fact]
    public async Task LiveSptDatabase_WhenConfigured_ShouldMatchGoldenFixtureAndEmitScoreboard()
    {
        string? livePath = Environment.GetEnvironmentVariable("SPT_DATA_PATH");
        var golden = SptDatabaseLoader.LoadGoldenFixture();

        SptDatabaseSnapshot activeSnapshot = golden;
        if (!string.IsNullOrWhiteSpace(livePath) && Directory.Exists(livePath))
        {
            var live = SptDatabaseLoader.LoadLiveSptData(livePath);
            activeSnapshot = live;

            var goldenBal = await ContainerLootSimulator.ExecuteModInMemoryAsync(golden, "Balanced");
            var liveBal = await ContainerLootSimulator.ExecuteModInMemoryAsync(live, "Balanced");

            Assert.Equal(goldenBal.InjectedKeysService.InjectedKeyIds.Count, liveBal.InjectedKeysService.InjectedKeyIds.Count);

            foreach (var (folder, cfgName) in ContainerLootSimulator.PlayableMaps)
            {
                MongoId jId = folder == "labyrinth"
                    ? ContainerLootSimulator.LabyrinthJacketId
                    : ContainerLootSimulator.StandardJacketId;

                var gMetrics = ContainerLootSimulator.ComputeAnalyticalMetrics(goldenBal, folder, cfgName, "Jacket", jId);
                var lMetrics = ContainerLootSimulator.ComputeAnalyticalMetrics(liveBal, folder, cfgName, "Jacket", jId);

                Assert.Equal(lMetrics.ExpectedKeysPer100Containers, gMetrics.ExpectedKeysPer100Containers, precision: 4);
            }
        }

        string report = await BuildMarkdownScoreboardAsync(activeSnapshot);
        _output.WriteLine(report);

        string? outFile = Environment.GetEnvironmentVariable("SIMULATION_SCOREBOARD_OUT");
        if (!string.IsNullOrWhiteSpace(outFile))
        {
            File.WriteAllText(outFile, report, Encoding.UTF8);
        }
    }

    [Fact]
    public async Task RealSptLocationLootGenerator_WhenLiveDbConfigured_ShouldMatchSimulatorRaidParity()
    {
        string? livePath = Environment.GetEnvironmentVariable("SPT_DATA_PATH");
        if (string.IsNullOrWhiteSpace(livePath) || !Directory.Exists(livePath))
        {
            return;
        }

        var liveSnapshot = SptDatabaseLoader.LoadLiveSptData(livePath);
        var simRun = await ContainerLootSimulator.ExecuteModInMemoryAsync(liveSnapshot, "Balanced");
        var realEngine = await RealSptRaidEngineHarness.CreateAsync(livePath, "Balanced");

        var testMaps = new (string Folder, string CfgName, MongoId JacketId, MongoId? DuffleId, MongoId ScavId, int Raids)[]
        {
            ("bigmap", "customs", ContainerLootSimulator.StandardJacketId, ContainerLootSimulator.StandardDuffleId, ContainerLootSimulator.StandardDeadScavId, 120),
            ("factory4_night", "factory_night", ContainerLootSimulator.StandardJacketId, ContainerLootSimulator.StandardDuffleId, ContainerLootSimulator.StandardDeadScavId, 150),
            ("sandbox_high", "ground_zero_high", ContainerLootSimulator.StandardJacketId, ContainerLootSimulator.StandardDuffleId, ContainerLootSimulator.StandardDeadScavId, 120),
            ("labyrinth", "labyrinth", ContainerLootSimulator.LabyrinthJacketId, null, ContainerLootSimulator.LabyrinthDeadScavId, 200),
        };

        var sb = new StringBuilder();
        sb.AppendLine("# Real SPT 4.1.6 `LocationLootGenerator` Raid Parity Verification (`Balanced`)");
        sb.AppendLine();
        sb.AppendLine("| Map | Container | Raids | Avg Spawned / Raid | Avg Keys / Raid | Real SPT Random (Keys/100) | Real SPT Incl. Forced | Fast Simulator MC | Exact Math |");
        sb.AppendLine("| :--- | :--- | ---: | ---: | ---: | :---: | :---: | :---: | :---: |");

        var failures = new List<string>();
        foreach (var (folder, cfgName, jId, dId, sId, raids) in testMaps)
        {
            var tracked = new List<MongoId> { jId, sId };
            if (dId.HasValue) tracked.Add(dId.Value);

            var realStats = realEngine.SimulateRealRaids(folder, raids, tracked);

            foreach (var (label, cid) in new (string, MongoId?)[] { ("Jacket", jId), ("DuffleBag", dId), ("DeadScav", sId) })
            {
                if (!cid.HasValue) continue;
                var rStat = realStats[cid.Value];
                var math = ContainerLootSimulator.ComputeAnalyticalMetrics(simRun, folder, cfgName, label, cid.Value);
                var mc = ContainerLootSimulator.RunMonteCarlo(simRun, folder, cid.Value, trials: 10_000, seed: 1337);

                sb.AppendLine(
                    $"| `{cfgName}` | `{label}` | `{raids}` | `{rStat.AvgContainersPerRaid:F1}` | `{rStat.AvgTotalKeysPerRaid:F2}` | **`{rStat.RandomKeysPer100SpawnedContainers:F1}`** | `{rStat.TotalKeysPer100IncludingForced:F1}` | `{mc.SimulatedKeysPer100Containers:F1}` | `{math.ExpectedKeysPer100Containers:F1}` |");

                if (rStat.TotalContainersSpawned >= 300 &&
                    Math.Abs(rStat.RandomKeysPer100SpawnedContainers - mc.SimulatedKeysPer100Containers) > 4.0)
                {
                    failures.Add($"{cfgName} {label}: RealRandom={rStat.RandomKeysPer100SpawnedContainers:F2} vs SimMC={mc.SimulatedKeysPer100Containers:F2} (spawned={rStat.TotalContainersSpawned})");
                }
            }
        }

        _output.WriteLine(sb.ToString());
        string? parityOut = Environment.GetEnvironmentVariable("RAID_PARITY_OUT");
        if (!string.IsNullOrWhiteSpace(parityOut))
        {
            File.WriteAllText(parityOut, sb.ToString(), Encoding.UTF8);
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    private static async Task<string> BuildMarkdownScoreboardAsync(SptDatabaseSnapshot snapshot)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# In-Memory Simulation Scoreboard (v3.0.0) - {snapshot.SourceLabel}");
        sb.AppendLine();

        var vanillaRun = await ContainerLootSimulator.ExecuteModInMemoryAsync(snapshot, "Disabled");
        var balancedRun = await ContainerLootSimulator.ExecuteModInMemoryAsync(snapshot, "Balanced");

        sb.AppendLine($"- **Injected Keys + Keycards:** `{balancedRun.InjectedKeysService.InjectedKeyIds.Count}`");
        sb.AppendLine();
        sb.AppendLine("## 1. 13-Map Scoreboard (`enabled: false` vs. `Balanced`, Keys per 100 Containers)");
        sb.AppendLine();
        sb.AppendLine("| Map | Jackets (Van -> Bal Math / Bal 10k Roll) | Duffles (Van -> Bal Math / Bal 10k Roll) | Dead Scavs (Van -> Bal Math / Bal 10k Roll) |");
        sb.AppendLine("| :--- | :---: | :---: | :---: |");

        foreach (var (folder, cfgName) in ContainerLootSimulator.PlayableMaps)
        {
            MongoId jId = folder == "labyrinth" ? ContainerLootSimulator.LabyrinthJacketId : ContainerLootSimulator.StandardJacketId;
            MongoId dId = ContainerLootSimulator.StandardDuffleId;
            MongoId sId = folder == "labyrinth" ? ContainerLootSimulator.LabyrinthDeadScavId : ContainerLootSimulator.StandardDeadScavId;

            string jCell = FormatCell(vanillaRun, balancedRun, folder, cfgName, "Jacket", jId);
            string dCell = FormatCell(vanillaRun, balancedRun, folder, cfgName, "Duffle", dId);
            string sCell = FormatCell(vanillaRun, balancedRun, folder, cfgName, "DeadScav", sId);

            sb.AppendLine($"| `{cfgName}` | {jCell} | {dCell} | {sCell} |");
        }

        sb.AppendLine();
        sb.AppendLine("## 2. v3.0.0 Presets & `reduceCommonKeys` Summary (Customs & 13-Map Jacket Average)");
        sb.AppendLine();
        sb.AppendLine("| Mode | Customs Jackets (C / R / SR) | 10k MC Roll (`>=1 Key %`) | 2-Hr Night (~3.5 Raids) | Avg Hrs / SuperRare | Non-Key Jacket / 100 | 13-Map Avg | Sell Mult |");
        sb.AppendLine("| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: |");

        var modes = new (string Label, string Preset, bool ReduceCommon)[]
        {
            ("Hardcore", "Hardcore", false),
            ("Balanced", "Balanced", false),
            ("Casual", "Casual", false),
            ("Hardcore (reduceCommon)", "Hardcore", true),
            ("Balanced (reduceCommon)", "Balanced", true),
            ("Casual (reduceCommon)", "Casual", true),
            ("Vanilla (enabled: false)", "Disabled", false)
        };

        foreach (var (label, preset, reduceCommon) in modes)
        {
            var pRun = await ContainerLootSimulator.ExecuteModInMemoryAsync(
                snapshot, preset, c => c.ReduceCommonKeys = reduceCommon);
            var cJacket = ContainerLootSimulator.ComputeAnalyticalMetrics(
                pRun, "bigmap", "customs", "Jacket", ContainerLootSimulator.StandardJacketId);
            var cDuffle = ContainerLootSimulator.ComputeAnalyticalMetrics(
                pRun, "bigmap", "customs", "Duffle", ContainerLootSimulator.StandardDuffleId);
            var cScav = ContainerLootSimulator.ComputeAnalyticalMetrics(
                pRun, "bigmap", "customs", "DeadScav", ContainerLootSimulator.StandardDeadScavId);
            var cMc = ContainerLootSimulator.RunMonteCarlo(
                pRun, "bigmap", ContainerLootSimulator.StandardJacketId, trials: 10_000, seed: 1337);

            double raidKeys = (0.05 * cJacket.ExpectedKeysPer100Containers)
                + (0.04 * cDuffle.ExpectedKeysPer100Containers)
                + (0.01 * cScav.ExpectedKeysPer100Containers);
            double raidSr = (0.05 * cJacket.ExpectedSuperRarePer100)
                + (0.04 * cDuffle.ExpectedSuperRarePer100)
                + (0.01 * cScav.ExpectedSuperRarePer100);
            double twoHourKeys = raidKeys * 3.5;
            double hrsPerSr = raidSr > 0 ? 1.0 / (raidSr * 1.75) : double.PositiveInfinity;
            double nonKeyJacket = (cJacket.ExpectedTotalItemsPerContainer * 100.0) - cJacket.ExpectedKeysPer100Containers;

            var mapValues = new List<double>();
            foreach (var (folder, cfgName) in ContainerLootSimulator.PlayableMaps)
            {
                MongoId jId = folder == "labyrinth" ? ContainerLootSimulator.LabyrinthJacketId : ContainerLootSimulator.StandardJacketId;
                var m = ContainerLootSimulator.ComputeAnalyticalMetrics(pRun, folder, cfgName, "Jacket", jId);
                if (m.ExistsOnMap)
                {
                    mapValues.Add(m.ExpectedKeysPer100Containers);
                }
            }

            double avg13 = mapValues.Count > 0 ? mapValues.Average() : 0.0;
            double sellMult = pRun.NormalizedProfile == "disabled" ? 1.0 : pRun.ConfigLoader.Config.Resolved.FleaPriceMultiplier;
            string srHrsStr = double.IsInfinity(hrsPerSr) ? "inf" : $"{hrsPerSr:F1}h";
            sb.AppendLine(
                $"| `{label}` | `{cJacket.ExpectedKeysPer100Containers:F1}` (`{cJacket.ExpectedCommonPer100:F1}` / `{cJacket.ExpectedRarePer100:F1}` / `{cJacket.ExpectedSuperRarePer100:F1}`) | `{cMc.SimulatedKeysPer100Containers:F1}` (`{cMc.ContainersWithAtLeastOneKeyPercent:F1}%`) | `{twoHourKeys:F1}` | `{srHrsStr}` | `{nonKeyJacket:F1}` | `{avg13:F1}` | `{sellMult:F2}x` |");
        }

        return sb.ToString();
    }

    private static string FormatCell(
        ProfileSimulationRun vanillaRun,
        ProfileSimulationRun balancedRun,
        string folder,
        string cfgName,
        string kind,
        MongoId cid)
    {
        var v = ContainerLootSimulator.ComputeAnalyticalMetrics(vanillaRun, folder, cfgName, kind, cid);
        var b = ContainerLootSimulator.ComputeAnalyticalMetrics(balancedRun, folder, cfgName, kind, cid);
        if (!b.ExistsOnMap)
        {
            return "-";
        }

        var mc = ContainerLootSimulator.RunMonteCarlo(balancedRun, folder, cid, trials: 10_000, seed: 1337);
        return $"`{v.ExpectedKeysPer100Containers:F1}` -> **`{b.ExpectedKeysPer100Containers:F1}`** (`{mc.SimulatedKeysPer100Containers:F1}`)";
    }
}
