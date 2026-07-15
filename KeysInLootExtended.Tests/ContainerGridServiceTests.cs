using System;
using System.Collections.Generic;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using Xunit;

namespace KeysInLootExtended.Tests
{
    public class ContainerGridServiceTests
    {
        [Fact]
        public void AdjustGridSizes_ShouldModifyCellsHAndCellsV()
        {
            // Arrange
            var jacketId = new MongoId("578f8778245977358849a9b5");
            var duffleId = new MongoId("578f87a3245977356274f2cb");
            var deadScavId = new MongoId("5909e4b686f7747f5b744fa4");

            var items = new Dictionary<MongoId, TemplateItem>();

            // Setup mock templates using GetUninitializedObject to bypass C# 11 required properties
            var createMockTemplate = (MongoId id) =>
            {
                var template = (TemplateItem)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(TemplateItem));
                typeof(TemplateItem).GetProperty("Id")!.SetValue(template, id);

                var props = (TemplateItemProperties)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(TemplateItemProperties));
                
                var grid = (Grid)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Grid));
                var gridProps = (GridProperties)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(GridProperties));
                
                gridProps.CellsH = 2; // Original size
                gridProps.CellsV = 2; // Original size
                
                typeof(Grid).GetProperty("Properties")!.SetValue(grid, gridProps);
                
                var grids = new List<Grid> { grid };
                typeof(TemplateItemProperties).GetProperty("Grids")!.SetValue(props, grids);
                
                typeof(TemplateItem).GetProperty("Properties")!.SetValue(template, props);
                
                return template;
            };

            items[jacketId] = createMockTemplate(jacketId);
            items[duffleId] = createMockTemplate(duffleId);
            items[deadScavId] = createMockTemplate(deadScavId);

            int targetCellsH = 5;
            int targetCellsV = 6;

            // Act
            ContainerGridService.AdjustGridSizesInternal(items, targetCellsH, targetCellsV);

            // Assert
            foreach (var id in new[] { jacketId, duffleId, deadScavId })
            {
                var grids = items[id].Properties?.Grids;
                Assert.NotNull(grids);
                var gridProps = System.Linq.Enumerable.FirstOrDefault(grids!)?.Properties;
                Assert.NotNull(gridProps);
                Assert.Equal(targetCellsH, gridProps!.CellsH);
                Assert.Equal(targetCellsV, gridProps!.CellsV);
            }
        }
    }
}
