using System;
using System.Linq;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Server;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Models.Utils;

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
        InjectedKeysService injectedKeysService)
    {
        _logger = logger;
        _databaseServer = databaseServer;
        _configLoader = configLoader;
        _injectedKeysService = injectedKeysService;
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
            _injectedKeysService, 
            config.KeyFleaPricesMultiplier, 
            config.KeyTraderPricesMultiplier);

        _logger.Success($"[KeysInLootExtended] Flea prices adjusted by a {config.KeyFleaPricesMultiplier}x multiplier for {_injectedKeysService.InjectedKeyIds.Count} injected keys.");
        _logger.Success($"[KeysInLootExtended] Trader prices adjusted by a {config.KeyTraderPricesMultiplier}x multiplier for {_injectedKeysService.InjectedKeyIds.Count} injected keys.");
    }

    /// <summary>
    /// Internal logic for price adjustment, separated for unit testing without full server dependencies.
    /// </summary>
    /// <param name="fleaPrices">The dictionary of Flea Market prices from the database.</param>
    /// <param name="handbookItems">The list of trader items from the Handbook.</param>
    /// <param name="injectedKeys">The pre-populated service containing valid injected keys.</param>
    /// <param name="fleaMultiplier">The user-configured scale (e.g., 1.0 = 100%, 0.4 = 40%).</param>
    /// <param name="traderMultiplier">The user-configured scale (e.g., 1.0 = 100%, 0.4 = 40%).</param>
    /// <example>
    /// <code>
    /// ItemPriceService.AdjustPricesInternal(fleaDb, handbookDb, keysSvc, 0.4, 0.4);
    /// </code>
    /// </example>
    public static void AdjustPricesInternal(
        Dictionary<MongoId, double> fleaPrices,
        List<HandbookItem> handbookItems, 
        InjectedKeysService injectedKeys, 
        double fleaMultiplier, 
        double traderMultiplier)
    {
        // Sanitize multipliers: Prevent negative multipliers, and fallback to 1.0 if NaN or Infinity is somehow passed.
        double safeFleaMultiplier = Math.Max(0.0, fleaMultiplier);
        if (double.IsNaN(safeFleaMultiplier) || double.IsInfinity(safeFleaMultiplier)) safeFleaMultiplier = 1.0;

        double safeTraderMultiplier = Math.Max(0.0, traderMultiplier);
        if (double.IsNaN(safeTraderMultiplier) || double.IsInfinity(safeTraderMultiplier)) safeTraderMultiplier = 1.0;

        foreach (var mongoId in injectedKeys.InjectedKeyIds)
        {
            // 1. Flea Market Prices (O(1) Dictionary Lookup)
            if (fleaPrices.TryGetValue(mongoId, out var currentFleaPrice))
            {
                // We use Math.Round to ensure clean integer values in the game UI, preventing float rounding errors.
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
