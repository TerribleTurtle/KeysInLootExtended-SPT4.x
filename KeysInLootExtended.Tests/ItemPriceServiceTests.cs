using System;
using System.Collections.Generic;
using System.Text.Json;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using Xunit;

namespace KeysInLootExtended.Tests
{
    public class ItemPriceServiceTests
    {
        [Fact]
        public void AdjustPrices_ShouldMultiplyFleaAndTraderPrices()
        {
            // Arrange
            var keyId1 = new MongoId("543be5e94bdc2df1348b4568");
            var keyId2 = new MongoId("5c164d2286f774194c5e69fa");

            var fleaPrices = new Dictionary<MongoId, double>();
            fleaPrices[keyId1] = 10000;
            fleaPrices[keyId2] = 50000;

            // Use GetUninitializedObject to bypass C# 11 required properties
            var createHandbookItem = (MongoId id, double price) =>
            {
                var item = (HandbookItem)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(HandbookItem));
                typeof(HandbookItem).GetProperty("Id")!.SetValue(item, id);
                typeof(HandbookItem).GetProperty("Price")!.SetValue(item, price);
                return item;
            };

            var handbookItems = new List<HandbookItem>
            {
                createHandbookItem(keyId1, 10000),
                createHandbookItem(keyId2, 50000)
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
    }
}
