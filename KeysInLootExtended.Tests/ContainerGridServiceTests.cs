using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Tables;
using Xunit;

namespace KeysInLootExtended.Tests
{
    public class ContainerGridServiceTests
    {
        private static TemplateItem CreateMockContainer(MongoId id, int cellsH = 2, int cellsV = 2)
        {
            var template = (TemplateItem)RuntimeHelpers.GetUninitializedObject(typeof(TemplateItem));
            typeof(TemplateItem).GetProperty("Id")!.SetValue(template, id);

            var props = (TemplateItemProperties)RuntimeHelpers.GetUninitializedObject(typeof(TemplateItemProperties));
            var grid = (Grid)RuntimeHelpers.GetUninitializedObject(typeof(Grid));
            var gridProps = (GridProperties)RuntimeHelpers.GetUninitializedObject(typeof(GridProperties));

            gridProps.CellsH = cellsH;
            gridProps.CellsV = cellsV;
            gridProps.MaxCount = cellsH * cellsV;

            typeof(Grid).GetProperty("Properties")!.SetValue(grid, gridProps);
            var grids = new List<Grid> { grid };
            typeof(TemplateItemProperties).GetProperty("Grids")!.SetValue(props, grids);
            typeof(TemplateItem).GetProperty("Properties")!.SetValue(template, props);

            return template;
        }

        [Fact]
        public void AdjustGridSizes_ShouldModifyCellsHAndCellsV()
        {
            // Arrange
            var jacketId = new MongoId("578f8778245977358849a9b5");
            var duffleId = new MongoId("578f87a3245977356274f2cb");
            var deadScavId = new MongoId("5909e4b686f7747f5b744fa4");

            var items = new Dictionary<MongoId, TemplateItem>
            {
                [jacketId] = CreateMockContainer(jacketId, 2, 2),
                [duffleId] = CreateMockContainer(duffleId, 2, 2),
                [deadScavId] = CreateMockContainer(deadScavId, 2, 2)
            };

            int targetCellsH = 5;
            int targetCellsV = 6;

            // Act
            ContainerGridService.AdjustGridSizesInternal(items, targetCellsH, targetCellsV);

            // Assert
            foreach (var id in new[] { jacketId, duffleId, deadScavId })
            {
                var grids = items[id].Properties?.Grids;
                Assert.NotNull(grids);
                var gridProps = grids!.FirstOrDefault()?.Properties;
                Assert.NotNull(gridProps);
                Assert.Equal(targetCellsH, gridProps!.CellsH);
                Assert.Equal(targetCellsV, gridProps!.CellsV);
            }
        }

        [Fact]
        public void AdjustGridSizesInternal_ShouldClampTargetDimensions_To1Through14()
        {
            var jacketId = new MongoId("578f8778245977358849a9b5");

            // Lower bound clamp: target 0 or negative should clamp to 1 (container starting at 1 stays 1, not 0)
            var container1 = CreateMockContainer(jacketId, cellsH: 1, cellsV: 1);
            ContainerGridService.AdjustGridSizesInternal(new() { [jacketId] = container1 }, 0, -5);
            var g1 = container1.Properties!.Grids!.First().Properties!;
            Assert.Equal(1, g1.CellsH);
            Assert.Equal(1, g1.CellsV);

            // Upper bound clamp: target > 14 should clamp to 14
            var container2 = CreateMockContainer(jacketId, cellsH: 2, cellsV: 2);
            ContainerGridService.AdjustGridSizesInternal(new() { [jacketId] = container2 }, 20, 99);
            var g2 = container2.Properties!.Grids!.First().Properties!;
            Assert.Equal(14, g2.CellsH);
            Assert.Equal(14, g2.CellsV);
            Assert.Equal(196, g2.MaxCount);
        }

        [Fact]
        public void AdjustGridSizesInternal_ShouldNeverShrinkContainers_OnlyExpand()
        {
            var duffleId = new MongoId("578f87a3245977356274f2cb");
            var deadScavId = new MongoId("5909e4b686f7747f5b744fa4");

            // Vanilla Duffle Bag is 3x4, Dead Scav is 4x4
            var items = new Dictionary<MongoId, TemplateItem>
            {
                [duffleId] = CreateMockContainer(duffleId, cellsH: 3, cellsV: 4),
                [deadScavId] = CreateMockContainer(deadScavId, cellsH: 4, cellsV: 4)
            };

            // Target 3x3 (e.g. Balanced profile)
            ContainerGridService.AdjustGridSizesInternal(items, 3, 3);

            var duffleProps = items[duffleId].Properties!.Grids!.First().Properties!;
            Assert.Equal(3, duffleProps.CellsH);
            Assert.Equal(4, duffleProps.CellsV); // 4 must not shrink to 3!
            Assert.Equal(12, duffleProps.MaxCount);

            var scavProps = items[deadScavId].Properties!.Grids!.First().Properties!;
            Assert.Equal(4, scavProps.CellsH); // 4 must not shrink to 3!
            Assert.Equal(4, scavProps.CellsV); // 4 must not shrink to 3!
            Assert.Equal(16, scavProps.MaxCount);
        }

        [Fact]
        public void AdjustGridSizes_ShouldReturnEarly_WhenOverrideLootDistributionIsFalse()
        {
            var jacketId = new MongoId("578f8778245977358849a9b5");
            var items = new Dictionary<MongoId, TemplateItem>
            {
                [jacketId] = CreateMockContainer(jacketId, cellsH: 2, cellsV: 2)
            };

            var templateTable = (TemplateTable)RuntimeHelpers.GetUninitializedObject(typeof(TemplateTable));
            typeof(TemplateTable).GetProperty("Items")!.SetValue(templateTable, items);

            var modHelper = (ModHelper)RuntimeHelpers.GetUninitializedObject(typeof(ModHelper));
            var configLoader = new KeysInLootConfigLoader(NoOpLoggerProxy.Create<KeysInLootConfigLoader>(), modHelper);
            configLoader.Config.OverrideLootDistribution = false;
            configLoader.Config.CellsH = 5;
            configLoader.Config.CellsV = 5;

            var gridService = new ContainerGridService(NoOpLoggerProxy.Create<ContainerGridService>(), templateTable, configLoader);
            gridService.AdjustGridSizes();

            var gProps = items[jacketId].Properties!.Grids!.First().Properties!;
            Assert.Equal(2, gProps.CellsH); // Untouched because OverrideLootDistribution == false
            Assert.Equal(2, gProps.CellsV);
        }
    }
}
