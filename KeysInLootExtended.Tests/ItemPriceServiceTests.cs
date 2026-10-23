using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using Xunit;

namespace KeysInLootExtended.Tests
{
    public class ItemPriceServiceTests
    {
        private static HandbookItem CreateHandbookItem(MongoId id, double price)
        {
            var item = (HandbookItem)RuntimeHelpers.GetUninitializedObject(typeof(HandbookItem));
            typeof(HandbookItem).GetProperty("Id")!.SetValue(item, id);
            typeof(HandbookItem).GetProperty("Price")!.SetValue(item, price);
            return item;
        }

        [Fact]
        public void AdjustPrices_ShouldMultiplyFleaAndTraderPrices()
        {
            // Arrange
            var keyId1 = new MongoId("543be5e94bdc2df1348b4568");
            var keyId2 = new MongoId("5c164d2286f774194c5e69fa");

            var fleaPrices = new Dictionary<MongoId, double>
            {
                [keyId1] = 10000,
                [keyId2] = 50000
            };

            var handbookItems = new List<HandbookItem>
            {
                CreateHandbookItem(keyId1, 10000),
                CreateHandbookItem(keyId2, 50000)
            };

            var keysService = new InjectedKeysService();
            keysService.InjectedKeyIds.Add(keyId1);
            keysService.InjectedKeyIds.Add(keyId2);

            // Act - Multipliers: 0.5x for flea, 2.0x for trader
            ItemPriceService.AdjustPricesInternal(fleaPrices, handbookItems, keysService.InjectedKeyIds, 0.5, 2.0);

            // Assert
            Assert.Equal(5000, fleaPrices[keyId1]); // 10000 * 0.5
            Assert.Equal(25000, fleaPrices[keyId2]); // 50000 * 0.5

            Assert.Equal(20000, handbookItems[0].Price); // 10000 * 2.0
            Assert.Equal(100000, handbookItems[1].Price); // 50000 * 2.0
        }

        [Fact]
        public void SanitizeMultiplier_ShouldHandleNaNAndInfinity_ByFallingBackToOne()
        {
            var keyId = new MongoId("543be5e94bdc2df1348b4568");
            var fleaPrices = new Dictionary<MongoId, double> { [keyId] = 10000 };
            var handbook = new List<HandbookItem> { CreateHandbookItem(keyId, 10000) };
            var keys = new HashSet<MongoId> { keyId };

            // NaN and Infinity should fallback to 1.0x
            ItemPriceService.AdjustPricesInternal(fleaPrices, handbook, keys, double.NaN, double.PositiveInfinity);

            Assert.Equal(10000, fleaPrices[keyId]);
            Assert.Equal(10000, handbook[0].Price);
        }

        [Fact]
        public void AdjustPricesInternal_ShouldClampToMinimumOne_WhenMultiplierAndPriceArePositive()
        {
            var keyId = new MongoId("543be5e94bdc2df1348b4568");
            var fleaPrices = new Dictionary<MongoId, double> { [keyId] = 1 };
            var handbook = new List<HandbookItem> { CreateHandbookItem(keyId, 1) };
            var keys = new HashSet<MongoId> { keyId };

            // 1 * 0.1 = 0.1 -> Math.Round is 0, but since price > 0 and mult > 0, clamp to 1.0!
            ItemPriceService.AdjustPricesInternal(fleaPrices, handbook, keys, 0.1, 0.1);

            Assert.Equal(1.0, fleaPrices[keyId]);
            Assert.Equal(1.0, handbook[0].Price);
        }

        [Fact]
        public void AdjustPricesInternal_ShouldAllowZero_WhenMultiplierIsZero()
        {
            var keyId = new MongoId("543be5e94bdc2df1348b4568");
            var fleaPrices = new Dictionary<MongoId, double> { [keyId] = 10000 };
            var handbook = new List<HandbookItem> { CreateHandbookItem(keyId, 10000) };
            var keys = new HashSet<MongoId> { keyId };

            ItemPriceService.AdjustPricesInternal(fleaPrices, handbook, keys, 0.0, 0.0);

            Assert.Equal(0.0, fleaPrices[keyId]);
            Assert.Equal(0.0, handbook[0].Price);
        }
    }
}
