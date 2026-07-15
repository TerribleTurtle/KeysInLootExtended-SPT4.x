using System;
using System.Linq;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Server;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Models.Utils;

namespace KeysInLootExtended;

[Injectable(InjectionType.Singleton)]
public class ItemPriceService
{
    private readonly ISptLogger<ItemPriceService> _logger;
    private readonly DatabaseServer _databaseServer;
    private readonly KeysInLootConfigLoader _configLoader;
    private readonly InjectedKeysService _injectedKeysService;

    public ItemPriceService(
        ISptLogger<ItemPriceService> logger,
        DatabaseServer databaseServer,
        KeysInLootConfigLoader configLoader,
        InjectedKeysService injectedKeysService)
    {
        _logger = logger;
        _databaseServer = databaseServer;
        _configLoader = configLoader;
        _injectedKeysService = injectedKeysService;
    }

    public void AdjustPrices()
    {
        var config = _configLoader.Config;
        if (config.ActiveProfile == "Disabled")
            return;

        var tables = _databaseServer.GetTables();
        
        AdjustPricesInternal(
            tables.Templates.Prices,
            tables.Templates.Handbook.Items, 
            _injectedKeysService, 
            config.KeyFleaPricesMultiplier, 
            config.KeyTraderPricesMultiplier);

        _logger.Success($"[KeysInLootExtended] Flea prices adjusted by a {config.KeyFleaPricesMultiplier}x multiplier for {_injectedKeysService.InjectedKeyIds.Count} injected keys.");
        _logger.Success($"[KeysInLootExtended] Trader prices adjusted by a {config.KeyTraderPricesMultiplier}x multiplier for {_injectedKeysService.InjectedKeyIds.Count} injected keys.");
    }

    public static void AdjustPricesInternal(
        Dictionary<MongoId, double> fleaPrices,
        List<HandbookItem> handbookItems, 
        InjectedKeysService injectedKeys, 
        double fleaMultiplier, 
        double traderMultiplier)
    {
        // Pre-compute MongoIds to prevent repeated string parsing and closure allocations
        var targetMongoIds = new HashSet<MongoId>();
        var foundFleaKeys = new HashSet<MongoId>();
        foreach (var keyIdString in injectedKeys.InjectedKeyIds)
        {
            var mongoId = new MongoId(keyIdString);
            targetMongoIds.Add(mongoId);

            // 1. Flea Market Prices (O(1) Dictionary Lookup)
            if (fleaPrices.TryGetValue(mongoId, out var currentFleaPrice))
            {
                fleaPrices[mongoId] = Math.Round(currentFleaPrice * fleaMultiplier);
                foundFleaKeys.Add(mongoId);
            }
        }

        var foundTraderKeys = new HashSet<MongoId>();
        // 2. Trader Base Prices (O(N) Iteration with O(1) HashSet Lookup)
        foreach (var handbookEntry in handbookItems)
        {
            if (targetMongoIds.Contains(handbookEntry.Id))
            {
                if (handbookEntry.Price.HasValue)
                {
                    handbookEntry.Price = Math.Round(handbookEntry.Price.Value * traderMultiplier);
                    foundTraderKeys.Add(handbookEntry.Id);
                }
            }
        }
    }
}
