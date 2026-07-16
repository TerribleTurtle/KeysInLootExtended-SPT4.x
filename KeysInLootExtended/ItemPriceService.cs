using System;
using System.Linq;
using System.Collections.Generic;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Server;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Models.Spt.Config;

namespace KeysInLootExtended;

/// <summary>
/// Service responsible for intercepting the database and adjusting the Flea Market and Trader prices of injected keys.
/// </summary>
[Injectable(InjectionType.Singleton)]
public class ItemPriceService
{
    private readonly ISptLogger<ItemPriceService> _logger;
    private readonly DatabaseServer _databaseServer;
    private readonly KeysInLootConfigLoader _configLoader;
    private readonly InjectedKeysService _injectedKeysService;
    private readonly ConfigServer _configServer;

    /// <summary>
    /// Initializes a new instance of the <see cref="ItemPriceService"/> class.
    /// </summary>
    /// <param name="logger">The SPT logger.</param>
    /// <param name="databaseServer">The SPT database server.</param>
    /// <param name="configLoader">The configuration loader containing user multipliers.</param>
    /// <param name="injectedKeysService">The service containing the fast-lookup HashSet of injected keys.</param>
    public ItemPriceService(
        ISptLogger<ItemPriceService> logger,
        DatabaseServer databaseServer,
        KeysInLootConfigLoader configLoader,
        InjectedKeysService injectedKeysService,
        ConfigServer configServer)
    {
        _logger = logger;
        _databaseServer = databaseServer;
        _configLoader = configLoader;
        _injectedKeysService = injectedKeysService;
        _configServer = configServer;
    }

    /// <summary>
    /// Adjusts all key prices in the database based on the active configuration multipliers.
    /// Safely exits early if the mod profile is "Disabled".
    /// </summary>
    public void AdjustPrices()
    {
        var config = _configLoader.Config;
        if (config.ActiveProfile == "Disabled")
            return;

        var tables = _databaseServer.GetTables();
        
        AdjustPricesInternal(
            tables.Templates.Prices,
            tables.Templates.Handbook.Items, 
            _injectedKeysService.InjectedKeyIds, 
            config.KeyFleaPricesMultiplier, 
            config.KeyTraderPricesMultiplier);

        _logger.Success($"[KeysInLootExtended] Flea prices adjusted by a {config.KeyFleaPricesMultiplier}x multiplier for {_injectedKeysService.InjectedKeyIds.Count} injected keys.");
        _logger.Success($"[KeysInLootExtended] Trader prices adjusted by a {config.KeyTraderPricesMultiplier}x multiplier for {_injectedKeysService.InjectedKeyIds.Count} injected keys.");

        if (config.BanKeysFromFence)
        {
            var traderConfig = _configServer.GetConfig<TraderConfig>();
            if (traderConfig?.Fence?.Blacklist != null)
            {
                foreach (var mongoId in _injectedKeysService.InjectedKeyIds)
                {
                    traderConfig.Fence.Blacklist.Add(mongoId);
                }
                _logger.Success($"[KeysInLootExtended] Successfully banned {_injectedKeysService.InjectedKeyIds.Count} keys from Fence's assort inventory.");
            }
        }
    }

    /// <summary>
    /// Internal logic for price adjustment, separated for unit testing without full server dependencies.
    /// </summary>
    /// <param name="fleaPrices">The dictionary of Flea Market prices from the database.</param>
    /// <param name="handbookItems">The list of trader items from the Handbook.</param>
    /// <param name="injectedKeyIds">The HashSet containing valid injected keys.</param>
    /// <param name="fleaMultiplier">The user-configured scale (e.g., 1.0 = 100%, 0.4 = 40%).</param>
    /// <param name="traderMultiplier">The user-configured scale (e.g., 1.0 = 100%, 0.4 = 40%).</param>
    /// <example>
    /// <code>
    /// ItemPriceService.AdjustPricesInternal(fleaDb, handbookDb, keysSet, 0.4, 0.4);
    /// </code>
    /// </example>
    public static void AdjustPricesInternal(
        Dictionary<MongoId, double> fleaPrices,
        List<HandbookItem> handbookItems, 
        HashSet<MongoId> injectedKeyIds, 
        double fleaMultiplier, 
        double traderMultiplier)
    {
        // Sanitize multipliers: Prevent negative multipliers, and fallback to 1.0 if NaN or Infinity is somehow passed.
        double safeFleaMultiplier = SanitizeMultiplier(fleaMultiplier);
        double safeTraderMultiplier = SanitizeMultiplier(traderMultiplier);

        foreach (var mongoId in injectedKeyIds)
        {
            // 1. Flea Market Prices (O(1) Dictionary Lookup)
            if (fleaPrices.TryGetValue(mongoId, out var currentFleaPrice))
            {
                // We use Math.Round to ensure clean integer values in the game UI, preventing float rounding errors.
                fleaPrices[mongoId] = Math.Round(currentFleaPrice * safeFleaMultiplier);
            }
        }

        // 2. Trader Base Prices (O(N) Iteration with O(1) HashSet Lookup)
        // We purposefully iterate over the Handbook instead of the individual Traders' Assorts.
        // The Handbook acts as the global base price for all traders, avoiding expensive O(N*M) assort iterations.
        foreach (var handbookEntry in handbookItems)
        {
            if (injectedKeyIds.Contains(handbookEntry.Id))
            {
                if (handbookEntry.Price.HasValue)
                {
                    handbookEntry.Price = Math.Round(handbookEntry.Price.Value * safeTraderMultiplier);
                }
            }
        }
    }

    private static double SanitizeMultiplier(double m)
    {
        double safeM = Math.Max(0.0, m);
        if (double.IsNaN(safeM) || double.IsInfinity(safeM)) safeM = 1.0;
        return safeM;
    }
}
