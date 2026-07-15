using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Utils;

namespace KeysInLootExtended;

/// <summary>
/// Singleton service responsible for loading, parsing, and caching the mod's configuration files (config.jsonc and locations/*.jsonc).
/// </summary>
[Injectable(InjectionType.Singleton)]
public class KeysInLootConfigLoader
{
    private readonly ISptLogger<KeysInLootConfigLoader> _logger;
    private readonly ModHelper _modHelper;
    
    private static readonly JsonSerializerOptions _jsonSettings;

    static KeysInLootConfigLoader()
    {
        _jsonSettings = new JsonSerializerOptions
        {
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            PropertyNameCaseInsensitive = true
        };
        _jsonSettings.Converters.Add(new CultureInvariantDoubleConverter());
    }

    /// <summary>
    /// The globally cached core configuration, accessible to other services.
    /// </summary>
    public KeysInLootCoreConfig Config { get; private set; }

    public KeysInLootConfigLoader(
        ISptLogger<KeysInLootConfigLoader> logger,
        ModHelper modHelper)
    {
        _logger = logger;
        _modHelper = modHelper;
        
        Config = LoadCoreConfig();
    }

    /// <summary>
    /// Loads the core config.jsonc file and applies the selected ActiveProfile overrides.
    /// </summary>
    private KeysInLootCoreConfig LoadCoreConfig()
    {
        var pathToMod = _modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        var configPath = Path.Combine(pathToMod, "config.jsonc");
        if (!File.Exists(configPath))
        {
            _logger.Error("[KeysInLootExtended] FATAL ERROR: Core config.jsonc not found!");
            throw new FileNotFoundException($"[KeysInLootExtended] Core config.jsonc not found at {configPath}");
        }

        var configText = File.ReadAllText(configPath);
        var config = JsonSerializer.Deserialize<KeysInLootCoreConfig>(configText, _jsonSettings);

        if (config == null)
        {
            _logger.Error("[KeysInLootExtended] FATAL ERROR: Failed to deserialize config.jsonc!");
            throw new InvalidDataException("[KeysInLootExtended] Failed to deserialize config.jsonc to KeysInLootCoreConfig.");
        }

        // Apply profile overrides
        switch (config.ActiveProfile.ToLowerInvariant())
        {
            case "balanced":
                config.KeyWeight = new KeysInLootRarityConfig { NotExist = 200, Common = 200, Rare = 100, SuperRare = 40 };
                config.KeycardWeight = new KeysInLootRarityConfig { NotExist = 60, Common = 60, Rare = 30, SuperRare = 15 };
                config.OverrideLootDistribution = true;
                config.KeyFleaPricesMultiplier = 0.4;
                config.KeyTraderPricesMultiplier = 0.4;
                config.CellsH = 3;
                config.CellsV = 3;
                break;
            case "bountiful":
                config.KeyWeight = new KeysInLootRarityConfig { NotExist = 400, Common = 400, Rare = 200, SuperRare = 80 };
                config.KeycardWeight = new KeysInLootRarityConfig { NotExist = 120, Common = 120, Rare = 60, SuperRare = 30 };
                config.OverrideLootDistribution = true;
                config.KeyFleaPricesMultiplier = 0.2;
                config.KeyTraderPricesMultiplier = 0.2;
                config.CellsH = 3;
                config.CellsV = 3;
                break;
            case "refined":
                config.KeyWeight = new KeysInLootRarityConfig { NotExist = 60, Common = 60, Rare = 170, SuperRare = 110 };
                config.KeycardWeight = new KeysInLootRarityConfig { NotExist = 18, Common = 18, Rare = 51, SuperRare = 41 };
                config.OverrideLootDistribution = true;
                config.KeyFleaPricesMultiplier = 0.25;
                config.KeyTraderPricesMultiplier = 0.25;
                config.CellsH = 3;
                config.CellsV = 3;
                break;
            case "hardcore scarcity":
                config.KeyWeight = new KeysInLootRarityConfig { NotExist = 60, Common = 60, Rare = 30, SuperRare = 15 };
                config.KeycardWeight = new KeysInLootRarityConfig { NotExist = 15, Common = 15, Rare = 8, SuperRare = 4 };
                config.OverrideLootDistribution = false;
                config.KeyFleaPricesMultiplier = 1.0;
                config.KeyTraderPricesMultiplier = 1.0;
                config.CellsH = 3;
                config.CellsV = 3;
                break;
            case "the mod classic":
                config.KeyWeight = new KeysInLootRarityConfig { NotExist = 500, Common = 500, Rare = 500, SuperRare = 500 };
                config.KeycardWeight = new KeysInLootRarityConfig { NotExist = 200, Common = 200, Rare = 200, SuperRare = 200 };
                config.OverrideLootDistribution = true;
                config.KeyFleaPricesMultiplier = 0.15;
                config.KeyTraderPricesMultiplier = 0.15;
                config.CellsH = 3;
                config.CellsV = 3;
                config.EnableLocationsConfig = false;
                break;
            case "the loot piñata":
                config.KeyWeight = new KeysInLootRarityConfig { NotExist = 10, Common = 10, Rare = 5000, SuperRare = 10000 };
                config.KeycardWeight = new KeysInLootRarityConfig { NotExist = 10, Common = 10, Rare = 1000, SuperRare = 5000 };
                config.OverrideLootDistribution = true;
                config.KeyFleaPricesMultiplier = 1.0;
                config.KeyTraderPricesMultiplier = 1.0;
                config.CellsH = 5;
                config.CellsV = 5;
                break;
            case "disabled":
            case "custom":
                break;
            default:
                _logger.Warning($"[KeysInLootExtended] WARNING: Unknown profile '{config.ActiveProfile}' selected. Defaulting to 'Custom' settings.");
                config.ActiveProfile = "Custom";
                break;
        }

        return config;
    }

    /// <summary>
    /// Dynamically loads a map-specific location configuration file and applies the ActiveProfile multipliers to it.
    /// </summary>
    /// <param name="locationName">The name of the location (e.g. "bigmap", "factory4_day").</param>
    /// <returns>The parsed and scaled location configuration, or null if the file doesn't exist.</returns>
    public KeysInLootLocationConfig? LoadLocationConfig(string locationName)
    {
        var pathToMod = _modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        var configPath = Path.Combine(pathToMod, "locations", $"{locationName}.jsonc");
        
        if (!File.Exists(configPath))
        {
            _logger.Warning($"[KeysInLootExtended] WARNING: Location config not found at {configPath}. Falling back to default weights.");
            return null;
        }

        var configText = File.ReadAllText(configPath);
        var locConfig = JsonSerializer.Deserialize<KeysInLootLocationConfig>(configText, _jsonSettings) 
            ?? throw new InvalidDataException($"[KeysInLootExtended] Failed to deserialize location config {locationName}.jsonc.");

        ScaleLocationConfig(locConfig, Config.ActiveProfile);
        return locConfig;
    }

    private void ScaleLocationConfig(KeysInLootLocationConfig locConfig, string profile)
    {
        // By default, if the profile isn't actively adjusting map ratios, we use a 1.0 multiplier (no change).
        double commonScale = 1.0;
        double rareScale = 1.0;
        double superRareScale = 1.0;

        switch (profile.ToLowerInvariant())
        {
            case "bountiful":
                // Bountiful flatly doubles all key drops across the board without filtering trash.
                commonScale = 2.0; rareScale = 2.0; superRareScale = 2.0;
                break;
            case "refined":
                // Refined severely drops common trash keys, but redistributes that lost probability mass
                // up into the Rare and SuperRare brackets to maintain the overall total key drop frequency.
                commonScale = 0.3; rareScale = 1.7; superRareScale = 2.75;
                break;
            case "hardcore scarcity":
                // Hardcore globally suppresses all keys to a fraction of their vanilla map configurations.
                commonScale = 0.3; rareScale = 0.3; superRareScale = 0.3;
                break;
            case "the loot piñata":
                // Loot Piñata almost entirely purges common keys and artificially forces incredibly high
                // spawn weights for Rare/SuperRare keys to simulate a broken economy.
                commonScale = 0.05; rareScale = 50.0; superRareScale = 250.0;
                break;
            case "balanced":
            case "the mod classic":
            case "custom":
            default:
                return;
        }

        ScaleContainer(locConfig.JacketContainer, commonScale, rareScale, superRareScale);
        ScaleContainer(locConfig.DuffleBagContainer, commonScale, rareScale, superRareScale);
        ScaleContainer(locConfig.DeadScavContainer, commonScale, rareScale, superRareScale);
    }

    private void ScaleContainer(KeysInLootContainerConfig? container, double commonScale, double rareScale, double superRareScale)
    {
        if (container == null) return;
        ScaleRarity(container.Key, commonScale, rareScale, superRareScale);
        ScaleRarity(container.Keycard, commonScale, rareScale, superRareScale);
    }

    private void ScaleRarity(KeysInLootRarityConfig? rarity, double commonScale, double rareScale, double superRareScale)
    {
        if (rarity == null) return;
        
        // NotExist is intentionally not scaled up by commonScale so that profile modifiers don't accidentally
        // double the chance of empty container slots when they meant to increase key drops.
        // We use Math.Max(1, ...) to ensure that low-weight vanilla keys aren't mathematically rounded down 
        // to 0 and completely purged from the drop pool by fractional scalars (e.g. 0.05).
        rarity.Common = rarity.Common > 0 ? Math.Max(1, (int)Math.Round(rarity.Common * commonScale)) : 0;
        rarity.Rare = rarity.Rare > 0 ? Math.Max(1, (int)Math.Round(rarity.Rare * rareScale)) : 0;
        rarity.SuperRare = rarity.SuperRare > 0 ? Math.Max(1, (int)Math.Round(rarity.SuperRare * superRareScale)) : 0;
    }
}
