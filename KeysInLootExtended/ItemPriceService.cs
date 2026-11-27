using System;
using System.Collections.Generic;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Models.Spt.Config;

namespace KeysInLootExtended;

/// <summary>
/// Scales Flea Market and Trader base prices for injected keys and optionally bans them from Fence.
/// </summary>
[Injectable(InjectionType.Singleton)]
public class ItemPriceService
{
    private readonly ISptLogger<ItemPriceService> _logger;
    private readonly TemplateTable _templateTable;
    private readonly KeysInLootConfigLoader _configLoader;
    private readonly InjectedKeysService _injectedKeysService;
    private readonly TraderConfig _traderConfig;

    public ItemPriceService(
        ISptLogger<ItemPriceService> logger,
        TemplateTable templateTable,
        KeysInLootConfigLoader configLoader,
        InjectedKeysService injectedKeysService,
        TraderConfig traderConfig)
    {
        _logger = logger;
        _templateTable = templateTable;
        _configLoader = configLoader;
        _injectedKeysService = injectedKeysService;
        _traderConfig = traderConfig;
    }

    public void AdjustPrices()
    {
        var config = _configLoader.Config;
        if (!config.Enabled)
            return;

        var resolved = config.Resolved;

        AdjustPricesInternal(
            _templateTable.Prices,
            _templateTable.Handbook.Items, 
            _injectedKeysService.InjectedKeyIds, 
            resolved.FleaPriceMultiplier, 
            resolved.TraderPriceMultiplier);

        _logger.Success($"[KeysInLootExtended] Flea prices adjusted by a {resolved.FleaPriceMultiplier}x multiplier for {_injectedKeysService.InjectedKeyIds.Count} injected keys.");
        _logger.Success($"[KeysInLootExtended] Trader prices adjusted by a {resolved.TraderPriceMultiplier}x multiplier for {_injectedKeysService.InjectedKeyIds.Count} injected keys.");

        if (config.BanKeysFromFence)
        {
            if (_traderConfig?.Fence?.Blacklist != null)
            {
                foreach (var mongoId in _injectedKeysService.InjectedKeyIds)
                {
                    _traderConfig.Fence.Blacklist.Add(mongoId);
                }
                _logger.Success($"[KeysInLootExtended] Successfully banned {_injectedKeysService.InjectedKeyIds.Count} keys from Fence's assort inventory.");
            }
        }
    }

    public static void AdjustPricesInternal(
        Dictionary<MongoId, double> fleaPrices,
        List<HandbookItem> handbookItems, 
        HashSet<MongoId> injectedKeyIds, 
        double fleaMultiplier, 
        double traderMultiplier)
    {
        double safeFleaMultiplier = SanitizeMultiplier(fleaMultiplier);
        double safeTraderMultiplier = SanitizeMultiplier(traderMultiplier);

        // Scale Flea Market prices, rounding to whole roubles and keeping positive prices at >= 1 RUB.
        foreach (var mongoId in injectedKeyIds)
        {
            if (fleaPrices.TryGetValue(mongoId, out var currentFleaPrice))
            {
                double rounded = Math.Round(currentFleaPrice * safeFleaMultiplier);
                fleaPrices[mongoId] = (safeFleaMultiplier > 0 && currentFleaPrice > 0)
                    ? Math.Max(1.0, rounded)
                    : rounded;
            }
        }

        // Scale Handbook base prices so all trader buy/sell calculations inherit the multiplier.
        foreach (var handbookEntry in handbookItems)
        {
            if (injectedKeyIds.Contains(handbookEntry.Id))
            {
                if (handbookEntry.Price.HasValue)
                {
                    double currentPrice = handbookEntry.Price.Value;
                    double rounded = Math.Round(currentPrice * safeTraderMultiplier);
                    handbookEntry.Price = (safeTraderMultiplier > 0 && currentPrice > 0)
                        ? Math.Max(1.0, rounded)
                        : rounded;
                }
            }
        }
    }

    private static double SanitizeMultiplier(double m)
    {
        if (double.IsNaN(m) || double.IsInfinity(m)) return 1.0;
        return Math.Max(0.0, m);
    }
}
