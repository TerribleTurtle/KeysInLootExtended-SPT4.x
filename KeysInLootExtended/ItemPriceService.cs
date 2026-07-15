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
        double safeFleaMultiplier = Math.Max(0.0, fleaMultiplier);
        if (double.IsNaN(safeFleaMultiplier) || double.IsInfinity(safeFleaMultiplier)) safeFleaMultiplier = 1.0;

        double safeTraderMultiplier = Math.Max(0.0, traderMultiplier);
        if (double.IsNaN(safeTraderMultiplier) || double.IsInfinity(safeTraderMultiplier)) safeTraderMultiplier = 1.0;

        foreach (var mongoId in injectedKeys.InjectedKeyIds)
        {
            // 1. Flea Market Prices (O(1) Dictionary Lookup)
            if (fleaPrices.TryGetValue(mongoId, out var currentFleaPrice))
            {
                fleaPrices[mongoId] = Math.Round(currentFleaPrice * safeFleaMultiplier);
            }
        }

        // 2. Trader Base Prices (O(N) Iteration with O(1) HashSet Lookup)
        foreach (var handbookEntry in handbookItems)
        {
            if (injectedKeys.InjectedKeyIds.Contains(handbookEntry.Id))
            {
                if (handbookEntry.Price.HasValue)
                {
                    handbookEntry.Price = Math.Round(handbookEntry.Price.Value * safeTraderMultiplier);
                }
            }
        }
    }
}
