using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Services.Items;
using Xunit;
using Xunit.Abstractions;

namespace KeysInLootExtended.Tests.Simulation;

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
        var run = await ContainerLootSimulator.ExecuteModInMemoryAsync(snapshot, "1"); // Balanced

        var analytical = ContainerLootSimulator.ComputeAnalyticalMetrics(
            run, "bigmap", "customs", "Jacket", ContainerLootSimulator.StandardJacketId);
        var mc = ContainerLootSimulator.RunMonteCarlo(
            run, "bigmap", ContainerLootSimulator.StandardJacketId, trials: 15_000, seed: 1337);

        Assert.True(analytical.ExistsOnMap);
        Assert.Equal(3, analytical.GridCellsH);
        Assert.Equal(3, analytical.GridCellsV);
        Assert.Equal(9, analytical.GridMaxCount);

        // Over 15,000 deterministic seeded openings on a 3x3 container (max 5 items rolled),
        // analytical and Monte Carlo must agree closely.
        Assert.InRange(
            Math.Abs(analytical.ExpectedKeysPer100Containers - mc.SimulatedKeysPer100Containers),
            0.0,
            1.5);
        Assert.Equal(0, mc.DroppedByMaxCount);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("2")]
    [InlineData("3")]
    [InlineData("4")]
    [InlineData("5")]
    [InlineData("6")]
    [InlineData("7")]
    [InlineData("9")]
    public async Task AllProfiles_ShouldNeverInjectQuestNodeOrBlacklistedItems(string profile)
    {
        var snapshot = SptDatabaseLoader.LoadGoldenFixture();
        var run = await ContainerLootSimulator.ExecuteModInMemoryAsync(snapshot, profile);
        var filterService = new ItemFilterService(run.Environment.ItemConfig);

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
    public async Task MonteCarlo_ShouldEnforceMaxCountCap_AndGridCapacity()
    {
        var snapshot = SptDatabaseLoader.LoadGoldenFixture();

        // Simulate what would happen if OverrideLootDistribution ran WITHOUT ContainerGridService expanding MaxCount=1
        var run = await ContainerLootSimulator.ExecuteModInMemoryAsync(snapshot, "1");
        var jacketGrid = run.Environment.TemplateTable.Items[ContainerLootSimulator.StandardJacketId]
            .Properties!.Grids!.First().Properties!;
        jacketGrid.MaxCount = 1; // Force vanilla MaxCount=1 cap

        var mc = ContainerLootSimulator.RunMonteCarlo(
            run, "bigmap", ContainerLootSimulator.StandardJacketId, trials: 5_000, seed: 42);

        // Every container rolling >1 item is physically capped at 1 item by MaxCount=1
        Assert.True(mc.DroppedByMaxCount > 3_000);
        Assert.InRange(mc.SimulatedItemsPerContainer, 0.90, 1.00);

        // Also verify Loot Pinata (profile 7: 10-25 items on 5x5=25 grid) runs cleanly without crashing
        var pinataRun = await ContainerLootSimulator.ExecuteModInMemoryAsync(snapshot, "7");
        var pinataMc = ContainerLootSimulator.RunMonteCarlo(
            pinataRun, "bigmap", ContainerLootSimulator.StandardJacketId, trials: 5_000, seed: 42);
        Assert.InRange(pinataMc.SimulatedKeysPer100Containers, 1200.0, 1650.0);
    }

    [Fact]
    public async Task Labyrinth_ShouldExpandGrids_AndInjectKeysIntoLabyrinthJacketAndDeadScav()
    {
        var snapshot = SptDatabaseLoader.LoadGoldenFixture();
        var run = await ContainerLootSimulator.ExecuteModInMemoryAsync(snapshot, "1"); // Balanced

        var labJacket = ContainerLootSimulator.ComputeAnalyticalMetrics(
            run, "labyrinth", "labyrinth", "Jacket", ContainerLootSimulator.LabyrinthJacketId);
        var labScav = ContainerLootSimulator.ComputeAnalyticalMetrics(
            run, "labyrinth", "labyrinth", "DeadScav", ContainerLootSimulator.LabyrinthDeadScavId);

        Assert.True(labJacket.ExistsOnMap);
        Assert.Equal(3, labJacket.GridCellsH);
        Assert.Equal(3, labJacket.GridCellsV);
        Assert.Equal(9, labJacket.GridMaxCount);
        Assert.True(labJacket.ExpectedKeysPer100Containers > 15.0,
            $"Expected Labyrinth Jacket > 15 keys/100 on Balanced, got {labJacket.ExpectedKeysPer100Containers:F2}");

        Assert.True(labScav.ExistsOnMap);
        Assert.Equal(4, labScav.GridCellsH);
        Assert.Equal(4, labScav.GridCellsV);
        Assert.Equal(16, labScav.GridMaxCount);
        Assert.True(labScav.ExpectedKeysPer100Containers > 4.0,
            $"Expected Labyrinth Dead Scav > 4 keys/100 on Balanced, got {labScav.ExpectedKeysPer100Containers:F2}");
    }

    [Fact]
    public async Task InjectedKeys_ShouldExcludeHandbookMissingPlaceholders_AndOneRoublePuzzleProps()
    {
        var snapshot = SptDatabaseLoader.LoadGoldenFixture();
        var run = await ContainerLootSimulator.ExecuteModInMemoryAsync(snapshot, "1");

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
    public async Task All13PlayableMaps_OnBalancedProfile_ShouldStayWithinTargetDropBands()
    {
        var snapshot = SptDatabaseLoader.LoadGoldenFixture();
        var vanillaRun = await ContainerLootSimulator.ExecuteModInMemoryAsync(snapshot, "9");
        var balancedRun = await ContainerLootSimulator.ExecuteModInMemoryAsync(snapshot, "1");

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
            var sVan = ContainerLootSimulator.ComputeAnalyticalMetrics(vanillaRun, folder, cfgName, "DeadScav", sId);
            if (sBal.ExistsOnMap)
            {
                Assert.InRange(sBal.ExpectedKeysPer100Containers, 5.5, 17.5);
                // Customs Dead Scavs should not suffer a major nerf below vanilla (~15.8)
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

            // Verify 100% mathematical parity between committed Golden Fixture and live SPT_Data
            var goldenBal = await ContainerLootSimulator.ExecuteModInMemoryAsync(golden, "1");
            var liveBal = await ContainerLootSimulator.ExecuteModInMemoryAsync(live, "1");

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
            // Hermetic CI mode (no full SPT_Data presets/staticContainers on GitHub runner)
            return;
        }

        var liveSnapshot = SptDatabaseLoader.LoadLiveSptData(livePath);
        var simRun = await ContainerLootSimulator.ExecuteModInMemoryAsync(liveSnapshot, "1"); // Balanced
        var realEngine = await RealSptRaidEngineHarness.CreateAsync(livePath, "1");

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
        sb.AppendLine($"# In-Memory Simulation Scoreboard — {snapshot.SourceLabel}");
        sb.AppendLine();

        var vanillaRun = await ContainerLootSimulator.ExecuteModInMemoryAsync(snapshot, "9");
        var balancedRun = await ContainerLootSimulator.ExecuteModInMemoryAsync(snapshot, "1");

        sb.AppendLine($"- **Injected Keys + Keycards:** `{balancedRun.InjectedKeysService.InjectedKeyIds.Count}`");
        sb.AppendLine();
        sb.AppendLine("## 1. 13-Map Scoreboard (`Disabled` vs. `Balanced`, Keys per 100 Containers)");
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
        sb.AppendLine("## 2. All Profiles Summary (Customs Jackets & 13-Map Jacket Average)");
        sb.AppendLine();
        sb.AppendLine("| Profile | Customs Math (C / R / SR) | Customs 10k MC Roll (`>=1 Key %`) | 13-Map Jacket Avg |");
        sb.AppendLine("| :--- | :---: | :---: | :---: |");

        string[] profileOrder = ["1", "2", "3", "4", "5", "6", "7", "9"];
        foreach (var p in profileOrder)
        {
            var pRun = await ContainerLootSimulator.ExecuteModInMemoryAsync(snapshot, p);
            var cMath = ContainerLootSimulator.ComputeAnalyticalMetrics(
                pRun, "bigmap", "customs", "Jacket", ContainerLootSimulator.StandardJacketId);
            var cMc = ContainerLootSimulator.RunMonteCarlo(
                pRun, "bigmap", ContainerLootSimulator.StandardJacketId, trials: 10_000, seed: 1337);

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
            sb.AppendLine(
                $"| `{p} - {pRun.NormalizedProfile}` | `{cMath.ExpectedKeysPer100Containers:F1}` (`{cMath.ExpectedCommonPer100:F1}` / `{cMath.ExpectedRarePer100:F1}` / `{cMath.ExpectedSuperRarePer100:F1}`) | `{cMc.SimulatedKeysPer100Containers:F1}` (`{cMc.ContainersWithAtLeastOneKeyPercent:F1}%`) | `{avg13:F1}` |");
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
            return "—";
        }

        var mc = ContainerLootSimulator.RunMonteCarlo(balancedRun, folder, cid, trials: 10_000, seed: 1337);
        return $"`{v.ExpectedKeysPer100Containers:F1}` -> **`{b.ExpectedKeysPer100Containers:F1}`** (`{mc.SimulatedKeysPer100Containers:F1}`)";
    }
}
