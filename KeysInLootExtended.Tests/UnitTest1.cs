using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using SPTarkov.Common.Models.Logging;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Services.Items;
using SPTarkov.Server.Core.Utils.Json;
using Xunit;

namespace KeysInLootExtended.Tests;

public class NoOpLoggerProxy : DispatchProxy
{
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => null;

    public static ISptLogger<T> Create<T>() => Create<ISptLogger<T>, NoOpLoggerProxy>();
}

public class ModMetadataTests
{
    [Fact]
    public void ModMetadata_ShouldImplementIModMetadataAndTargetSpt41()
    {
        IModMetadata metadata = new ModMetadata();
        var asmVersion = typeof(ModMetadata).Assembly.GetName().Version!;

        Assert.Equal("DJ-KeysInLootExtended", metadata.ModGuid);
        Assert.Equal(new SemanticVersioning.Version("2.2.0"), metadata.Version);
        Assert.Equal($"{asmVersion.Major}.{asmVersion.Minor}.{asmVersion.Build}", metadata.Version.ToString());
        Assert.True(metadata.SptVersion.IsSatisfied(new SemanticVersioning.Version("4.1.0")));
        Assert.True(metadata.SptVersion.IsSatisfied(new SemanticVersioning.Version("4.1.6")));
        Assert.False(metadata.SptVersion.IsSatisfied(new SemanticVersioning.Version("4.0.13")));
        Assert.False(metadata.HasPrepatcher);
    }

    [Fact]
    public void ConfigLoader_ShouldLoadRealConfigAndAll13LocationFiles()
    {
        var modHelper = (ModHelper)RuntimeHelpers.GetUninitializedObject(typeof(ModHelper));
        var configLoader = new KeysInLootConfigLoader(NoOpLoggerProxy.Create<KeysInLootConfigLoader>(), modHelper);

        Assert.NotNull(configLoader.Config);
        Assert.False(string.IsNullOrWhiteSpace(configLoader.Config.ActiveProfile));

        string[] mapNames =
        [
            "customs", "factory_day", "factory_night", "ground_zero", "ground_zero_high",
            "interchange", "laboratory", "labyrinth", "lighthouse", "reserve",
            "shoreline", "streets_of_tarkov", "woods"
        ];

        foreach (var map in mapNames)
        {
            var locConfig = configLoader.LoadLocationConfig(map);
            Assert.NotNull(locConfig);
            Assert.NotNull(locConfig!.JacketContainer?.Key);
            Assert.NotNull(locConfig.DuffleBagContainer?.Key);
            Assert.NotNull(locConfig.DeadScavContainer?.Key);
        }
    }

    [Fact]
    public async Task EndToEnd_OnLoadAsync_ShouldInjectKeysViaLazyLoad_ScalePrices_BanFromFence_AndResizeGrids()
    {
        // 1. Setup real ConfigLoader reading the repo's config.jsonc and locations/*.jsonc
        var modHelper = (ModHelper)RuntimeHelpers.GetUninitializedObject(typeof(ModHelper));
        var configLoader = new KeysInLootConfigLoader(NoOpLoggerProxy.Create<KeysInLootConfigLoader>(), modHelper);
        configLoader.Config.BanKeysFromFence = true;

        // 2. Setup TemplateTable with baseclasses, valid keys, ExplicitRarityMap boss key, quest key, blacklisted key, circular item, and containers
        var keyBaseId = new MongoId("543be5e94bdc2df1348b4568");
        var keycardBaseId = new MongoId("5c164d2286f774194c5e69fa");
        var commonKeyId = new MongoId("5780cf7f2459777de4559322"); // Dorm room 206 key
        var shturmanKeyId = new MongoId("5d08d21286f774736e7c94c3"); // In ExplicitRarityMap -> Superrare
        var keycardId = new MongoId("5c1d0d6d86f7744bb2683e1f"); // Yellow keycard
        var questKeyId = new MongoId("593962ca86f774068014d9af"); // Quest key (QuestItem = true)
        var blacklistedKeyId = new MongoId("600000000000000000000001"); // Blacklisted in ItemConfig
        var circularItemId = new MongoId("600000000000000000000002"); // Self-referencing parent cycle
        var nodeItemId = new MongoId("600000000000000000000003"); // Item with Type == "Node"
        var badFormatItemId = new MongoId("600000000000000000000004"); // Malformed MongoId in item.Id

        var jacketId = new MongoId("578f8778245977358849a9b5");
        var duffleId = new MongoId("578f87a3245977356274f2cb");
        var deadScavId = new MongoId("5909e4b686f7747f5b744fa4");

        var rootParentId = new MongoId("54009119af1c881c07000029");

        static TemplateItem CreateItem(MongoId id, MongoId parentId, string? rarityPvE = null, bool questItem = false, string? type = null)
        {
            var item = (TemplateItem)RuntimeHelpers.GetUninitializedObject(typeof(TemplateItem));
            typeof(TemplateItem).GetProperty("Id")!.SetValue(item, id);
            typeof(TemplateItem).GetProperty("Parent")!.SetValue(item, parentId);
            if (type != null)
            {
                typeof(TemplateItem).GetProperty("Type")!.SetValue(item, type);
            }
            var props = (TemplateItemProperties)RuntimeHelpers.GetUninitializedObject(typeof(TemplateItemProperties));
            typeof(TemplateItemProperties).GetProperty("RarityPvE")!.SetValue(props, rarityPvE);
            typeof(TemplateItemProperties).GetProperty("QuestItem")!.SetValue(props, questItem);
            typeof(TemplateItem).GetProperty("Properties")!.SetValue(item, props);
            return item;
        }

        TemplateItem CreateContainer(MongoId id)
        {
            var item = CreateItem(id, rootParentId);
            var grid = (Grid)RuntimeHelpers.GetUninitializedObject(typeof(Grid));
            var gridProps = (GridProperties)RuntimeHelpers.GetUninitializedObject(typeof(GridProperties));
            gridProps.CellsH = 2;
            gridProps.CellsV = 2;
            gridProps.MaxCount = 5;
            typeof(Grid).GetProperty("Properties")!.SetValue(grid, gridProps);
            typeof(TemplateItemProperties).GetProperty("Grids")!.SetValue(item.Properties, new List<Grid> { grid });
            return item;
        }

        static HandbookItem CreateHandbookItem(MongoId id, double price)
        {
            var hb = (HandbookItem)RuntimeHelpers.GetUninitializedObject(typeof(HandbookItem));
            typeof(HandbookItem).GetProperty("Id")!.SetValue(hb, id);
            typeof(HandbookItem).GetProperty("Price")!.SetValue(hb, price);
            return hb;
        }

        var badFormatItem = CreateItem(badFormatItemId, keyBaseId, "Common");
        var uninitMongoId = (MongoId)RuntimeHelpers.GetUninitializedObject(typeof(MongoId));
        typeof(TemplateItem).GetProperty("Id")!.SetValue(badFormatItem, uninitMongoId);

        var itemsDict = new Dictionary<MongoId, TemplateItem>
        {
            [keyBaseId] = CreateItem(keyBaseId, rootParentId),
            [keycardBaseId] = CreateItem(keycardBaseId, rootParentId),
            [commonKeyId] = CreateItem(commonKeyId, keyBaseId, "Common"),
            [shturmanKeyId] = CreateItem(shturmanKeyId, keyBaseId, "Not_exist"),
            [keycardId] = CreateItem(keycardId, keycardBaseId, "Superrare"),
            [questKeyId] = CreateItem(questKeyId, keyBaseId, "Common", questItem: true),
            [blacklistedKeyId] = CreateItem(blacklistedKeyId, keyBaseId, "Common"),
            [circularItemId] = CreateItem(circularItemId, circularItemId, "Common"),
            [nodeItemId] = CreateItem(nodeItemId, keyBaseId, "Common", type: "Node"),
            [badFormatItemId] = badFormatItem,
            [jacketId] = CreateContainer(jacketId),
            [duffleId] = CreateContainer(duffleId),
            [deadScavId] = CreateContainer(deadScavId)
        };

        var pricesDict = new Dictionary<MongoId, double>
        {
            [commonKeyId] = 10000,
            [shturmanKeyId] = 200000,
            [keycardId] = 500000,
            [questKeyId] = 10000
        };

        var handbook = (HandbookBase)RuntimeHelpers.GetUninitializedObject(typeof(HandbookBase));
        typeof(HandbookBase).GetProperty("Items")!.SetValue(handbook, new List<HandbookItem>
        {
            CreateHandbookItem(commonKeyId, 10000),
            CreateHandbookItem(shturmanKeyId, 200000),
            CreateHandbookItem(keycardId, 500000),
            CreateHandbookItem(questKeyId, 10000)
        });

        var templateTable = (TemplateTable)RuntimeHelpers.GetUninitializedObject(typeof(TemplateTable));
        typeof(TemplateTable).GetProperty("Items")!.SetValue(templateTable, itemsDict);
        typeof(TemplateTable).GetProperty("Prices")!.SetValue(templateTable, pricesDict);
        typeof(TemplateTable).GetProperty("Handbook")!.SetValue(templateTable, handbook);

        // 3. Setup LocationTable with Bigmap (Customs) and Laboratory using SPT 4.1.6's real LazyLoad<T>
        var locationTable = (LocationTable)RuntimeHelpers.GetUninitializedObject(typeof(LocationTable));
        Location CreateTestLocation(string id)
        {
            var loc = (Location)RuntimeHelpers.GetUninitializedObject(typeof(Location));
            var locBase = (LocationBase)RuntimeHelpers.GetUninitializedObject(typeof(LocationBase));
            typeof(LocationBase).GetProperty("Id")!.SetValue(locBase, id);
            typeof(Location).GetProperty("Base")!.SetValue(loc, locBase);

            var lazyLoot = new LazyLoad<Dictionary<MongoId, StaticLootDetails>>(() => new Dictionary<MongoId, StaticLootDetails>
            {
                [jacketId] = new StaticLootDetails { ItemDistribution = [], ItemCountDistribution = [] },
                [duffleId] = new StaticLootDetails { ItemDistribution = [], ItemCountDistribution = [] },
                [deadScavId] = new StaticLootDetails { ItemDistribution = [], ItemCountDistribution = [] }
            }, false);
            typeof(Location).GetProperty("StaticLoot")!.SetValue(loc, lazyLoot);
            return loc;
        }

        typeof(LocationTable).GetProperty("Bigmap")!.SetValue(locationTable, CreateTestLocation("bigmap"));
        typeof(LocationTable).GetProperty("Terminal")!.SetValue(locationTable, CreateTestLocation("Terminal"));

        // 4. Setup ItemFilterService & TraderConfig
        var itemConfig = (ItemConfig)RuntimeHelpers.GetUninitializedObject(typeof(ItemConfig));
        typeof(ItemConfig).GetProperty("Blacklist")!.SetValue(itemConfig, new HashSet<MongoId> { blacklistedKeyId });
        typeof(ItemConfig).GetProperty("LootableItemBlacklist")!.SetValue(itemConfig, new HashSet<MongoId>());

        var itemFilterService = new ItemFilterService(itemConfig);
        var fenceConfig = (FenceConfig)RuntimeHelpers.GetUninitializedObject(typeof(FenceConfig));
        typeof(FenceConfig).GetProperty("Blacklist")!.SetValue(fenceConfig, new HashSet<MongoId>());
        var traderConfig = (TraderConfig)RuntimeHelpers.GetUninitializedObject(typeof(TraderConfig));
        typeof(TraderConfig).GetProperty("Fence")!.SetValue(traderConfig, fenceConfig);

        var injectedKeysService = new InjectedKeysService();
        var itemHelper = (ItemHelper)RuntimeHelpers.GetUninitializedObject(typeof(ItemHelper));

        var lootService = new LootInjectionService(
            NoOpLoggerProxy.Create<LootInjectionService>(),
            templateTable,
            locationTable,
            configLoader,
            injectedKeysService,
            itemFilterService);

        var priceService = new ItemPriceService(
            NoOpLoggerProxy.Create<ItemPriceService>(),
            templateTable,
            configLoader,
            injectedKeysService,
            traderConfig);

        var gridService = new ContainerGridService(
            NoOpLoggerProxy.Create<ContainerGridService>(),
            templateTable,
            configLoader);

        var plugin = new KeysInLootExtendedPlugin(
            configLoader,
            lootService,
            priceService,
            gridService,
            NoOpLoggerProxy.Create<KeysInLootExtendedPlugin>());

        // 5. Execute the full 4.1.6 OnLoadAsync entrypoint
        await plugin.OnLoadAsync(CancellationToken.None);

        // 6. Assert key discovery & filtering
        Assert.Contains(commonKeyId, injectedKeysService.InjectedKeyIds);
        Assert.Contains(shturmanKeyId, injectedKeysService.InjectedKeyIds);
        Assert.Contains(keycardId, injectedKeysService.InjectedKeyIds);
        Assert.DoesNotContain(keyBaseId, injectedKeysService.InjectedKeyIds); // Baseclasses must not match themselves
        Assert.DoesNotContain(keycardBaseId, injectedKeysService.InjectedKeyIds);
        Assert.DoesNotContain(questKeyId, injectedKeysService.InjectedKeyIds);
        Assert.DoesNotContain(blacklistedKeyId, injectedKeysService.InjectedKeyIds);
        Assert.DoesNotContain(circularItemId, injectedKeysService.InjectedKeyIds);
        Assert.DoesNotContain(nodeItemId, injectedKeysService.InjectedKeyIds); // Node items must be skipped
        Assert.DoesNotContain(badFormatItemId, injectedKeysService.InjectedKeyIds); // Invalid MongoId must be caught & skipped

        // 7. Assert LazyLoad<T> transformer modifies Bigmap and Laboratory StaticLoot on the fly
        var resolvedCustomsLoot = locationTable.Bigmap.StaticLoot!.Value;
        Assert.NotNull(resolvedCustomsLoot);
        var customsJacketDist = resolvedCustomsLoot![jacketId].ItemDistribution!.ToDictionary(x => x.Tpl, x => x.RelativeProbability);

        var expectedCustomsConfig = configLoader.LoadLocationConfig("customs")!;
        Assert.Equal(expectedCustomsConfig.JacketContainer!.Key!.Common, customsJacketDist[commonKeyId]);
        // Shturman's Stash Key is "Not_exist" in vanilla, but ExplicitRarityMap maps it to "Superrare"
        Assert.Equal(expectedCustomsConfig.JacketContainer!.Key!.SuperRare, customsJacketDist[shturmanKeyId]);
        // On Customs, keycards are now enabled proportionally
        Assert.True(customsJacketDist.ContainsKey(keycardId));
        Assert.Equal(expectedCustomsConfig.JacketContainer!.Keycard!.SuperRare, customsJacketDist[keycardId]);
        Assert.False(customsJacketDist.ContainsKey(questKeyId));
        Assert.False(customsJacketDist.ContainsKey(blacklistedKeyId));

        // On Terminal (no location override file), fallback to global config weights (where keycards are > 0)
        var resolvedTerminalLoot = locationTable.Terminal!.StaticLoot!.Value;
        Assert.NotNull(resolvedTerminalLoot);
        var terminalJacketDist = resolvedTerminalLoot![jacketId].ItemDistribution!.ToDictionary(x => x.Tpl, x => x.RelativeProbability);
        Assert.Equal(configLoader.Config.KeycardWeight.SuperRare, terminalJacketDist[keycardId]);

        // 8. Assert Flea & Handbook price scaling + Fence blacklist
        double expectedFleaMult = configLoader.Config.KeyFleaPricesMultiplier;
        double expectedTraderMult = configLoader.Config.KeyTraderPricesMultiplier;
        Assert.Equal(Math.Round(10000 * expectedFleaMult), templateTable.Prices[commonKeyId]);
        Assert.Equal(10000, templateTable.Prices[questKeyId]); // Unchanged because quest keys are excluded
        Assert.Equal(Math.Round(10000 * expectedTraderMult), templateTable.Handbook.Items.First(x => x.Id == commonKeyId).Price);

        Assert.Contains(commonKeyId, traderConfig.Fence.Blacklist);
        Assert.Contains(shturmanKeyId, traderConfig.Fence.Blacklist);
        Assert.DoesNotContain(questKeyId, traderConfig.Fence.Blacklist);

        // 9. Assert container grid & MaxCount resizing
        int expectedH = Math.Clamp(configLoader.Config.CellsH, 1, 14);
        int expectedV = Math.Clamp(configLoader.Config.CellsV, 1, 14);
        foreach (var cid in new[] { jacketId, duffleId, deadScavId })
        {
            var gProps = templateTable.Items[cid].Properties!.Grids!.First().Properties!;
            Assert.Equal(expectedH, gProps.CellsH);
            Assert.Equal(expectedV, gProps.CellsV);
            Assert.Equal(expectedH * expectedV, gProps.MaxCount);
        }
    }

    [Fact]
    public void CultureInvariantDoubleConverter_ShouldThrowJsonException_OnNonStringTokens()
    {
        var converter = new CultureInvariantDoubleConverter();
        var jsonBytes = System.Text.Encoding.UTF8.GetBytes("{\"nested\": 123}");
        var reader = new Utf8JsonReader(jsonBytes);
        reader.Read(); // Position on StartObject

        var ex = Record.Exception(() =>
        {
            // We use helper to invoke without lambda capturing ref struct
        });
        try
        {
            converter.Read(ref reader, typeof(double), new JsonSerializerOptions());
            Assert.Fail("Expected JsonException but no exception was thrown.");
        }
        catch (Exception exCaught)
        {
            Assert.IsType<JsonException>(exCaught);
        }
    }

    [Theory]
    [InlineData("hardcore", "hardcore scarcity")]
    [InlineData("Hardcore Scarcity", "hardcore scarcity")]
    [InlineData("musicmaniac", "the musicmaniac classic")]
    [InlineData("classic", "the musicmaniac classic")]
    [InlineData("the musicmaniac classic", "the musicmaniac classic")]
    [InlineData("balanced", "balanced")]
    [InlineData("(1)", "balanced")]
    [InlineData("custom", "custom")]
    [InlineData("unknown_xyz", "unknown_xyz")]
    public void NormalizeProfileKey_ShouldMapAliasesCorrectly(string input, string expected)
    {
        string actual = KeysInLootConfigLoader.NormalizeProfileKey(input);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ScaleRarity_ShouldZeroOutWhenRatioIsZeroOrNegative_AndClampToOneWhenPositive()
    {
        var rarity = new KeysInLootRarityConfig
        {
            NotExist = 100,
            Common = 100,
            Rare = 100,
            SuperRare = 100
        };

        // When ratio <= 0, should become 0
        KeysInLootConfigLoader.ScaleRarity(rarity, 0.0, -0.5, 0.001, 2.0);

        Assert.Equal(0, rarity.Common);
        Assert.Equal(0, rarity.Rare);
        // 100 * 0.001 = 0.1 -> clamps to minimum 1
        Assert.Equal(1, rarity.SuperRare);
        // 100 * 2.0 = 200
        Assert.Equal(200, rarity.NotExist);
    }

    [Fact]
    public void ScaleContainer_ShouldApplySeparateKeyAndKeycardRatios()
    {
        var container = new KeysInLootContainerConfig
        {
            Key = new KeysInLootRarityConfig { Common = 200 },
            Keycard = new KeysInLootRarityConfig { Common = 60 }
        };

        // key common ratio: 1.5x, keycard common ratio: 0.5x
        KeysInLootConfigLoader.ScaleContainer(container, 1.5, 1.0, 1.0, 1.0, 0.5, 1.0, 1.0, 1.0);

        Assert.Equal(300, container.Key!.Common);
        Assert.Equal(30, container.Keycard!.Common);
    }
}
