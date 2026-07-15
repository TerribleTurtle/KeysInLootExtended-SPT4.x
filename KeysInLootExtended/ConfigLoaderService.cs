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

    private class ProfileDefinition
    {
        public Action<KeysInLootCoreConfig> ApplyCoreConfig { get; init; } = _ => { };
        public double CommonScale { get; init; } = 1.0;
        public double RareScale { get; init; } = 1.0;
        public double SuperRareScale { get; init; } = 1.0;
    }

    private static readonly Dictionary<string, ProfileDefinition> ProfileDefinitions = new(StringComparer.OrdinalIgnoreCase)
    {
        { "balanced", new ProfileDefinition {
            ApplyCoreConfig = c => {
                c.KeyWeight = new KeysInLootRarityConfig { NotExist = 200, Common = 200, Rare = 100, SuperRare = 40 };
                c.KeycardWeight = new KeysInLootRarityConfig { NotExist = 60, Common = 60, Rare = 30, SuperRare = 15 };
                c.OverrideLootDistribution = true;
                c.KeyFleaPricesMultiplier = 0.4;
                c.KeyTraderPricesMultiplier = 0.4;
                c.CellsH = 3;
                c.CellsV = 3;
            }
        }},
        { "bountiful", new ProfileDefinition {
            ApplyCoreConfig = c => {
                c.KeyWeight = new KeysInLootRarityConfig { NotExist = 400, Common = 400, Rare = 200, SuperRare = 80 };
                c.KeycardWeight = new KeysInLootRarityConfig { NotExist = 120, Common = 120, Rare = 60, SuperRare = 30 };
                c.OverrideLootDistribution = true;
                c.KeyFleaPricesMultiplier = 0.2;
                c.KeyTraderPricesMultiplier = 0.2;
                c.CellsH = 3;
                c.CellsV = 3;
            },
            CommonScale = 2.0, RareScale = 2.0, SuperRareScale = 2.0
        }},
        { "refined", new ProfileDefinition {
            ApplyCoreConfig = c => {
                c.KeyWeight = new KeysInLootRarityConfig { NotExist = 60, Common = 60, Rare = 170, SuperRare = 110 };
                c.KeycardWeight = new KeysInLootRarityConfig { NotExist = 18, Common = 18, Rare = 51, SuperRare = 41 };
                c.OverrideLootDistribution = true;
                c.KeyFleaPricesMultiplier = 0.25;
                c.KeyTraderPricesMultiplier = 0.25;
                c.CellsH = 3;
                c.CellsV = 3;
            },
            CommonScale = 0.3, RareScale = 1.7, SuperRareScale = 2.75
        }},
        { "hardcore scarcity", new ProfileDefinition {
            ApplyCoreConfig = c => {
                c.KeyWeight = new KeysInLootRarityConfig { NotExist = 60, Common = 60, Rare = 30, SuperRare = 15 };
                c.KeycardWeight = new KeysInLootRarityConfig { NotExist = 15, Common = 15, Rare = 8, SuperRare = 4 };
                c.OverrideLootDistribution = false;
                c.KeyFleaPricesMultiplier = 1.0;
                c.KeyTraderPricesMultiplier = 1.0;
                c.CellsH = 3;
                c.CellsV = 3;
            },
            CommonScale = 0.3, RareScale = 0.3, SuperRareScale = 0.3
        }},
        { "the mod classic", new ProfileDefinition {
            ApplyCoreConfig = c => {
                c.KeyWeight = new KeysInLootRarityConfig { NotExist = 500, Common = 500, Rare = 500, SuperRare = 500 };
                c.KeycardWeight = new KeysInLootRarityConfig { NotExist = 200, Common = 200, Rare = 200, SuperRare = 200 };
                c.OverrideLootDistribution = true;
                c.KeyFleaPricesMultiplier = 0.15;
                c.KeyTraderPricesMultiplier = 0.15;
                c.CellsH = 3;
                c.CellsV = 3;
                c.EnableLocationsConfig = false;
            }
        }},
        { "the loot piñata", new ProfileDefinition {
            ApplyCoreConfig = c => {
                c.KeyWeight = new KeysInLootRarityConfig { NotExist = 10, Common = 10, Rare = 5000, SuperRare = 10000 };
                c.KeycardWeight = new KeysInLootRarityConfig { NotExist = 10, Common = 10, Rare = 1000, SuperRare = 5000 };
                c.OverrideLootDistribution = true;
                c.KeyFleaPricesMultiplier = 1.0;
                c.KeyTraderPricesMultiplier = 1.0;
                c.CellsH = 5;
                c.CellsV = 5;
            },
            CommonScale = 0.05, RareScale = 50.0, SuperRareScale = 250.0
        }},
        { "disabled", new ProfileDefinition() },
        { "custom", new ProfileDefinition() }
    };

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

        // Apply profile overrides safely handling null profiles
        if (ProfileDefinitions.TryGetValue(config.ActiveProfile ?? string.Empty, out var profileDef))
        {
            profileDef.ApplyCoreConfig(config);
        }
        else
        {
            _logger.Warning($"[KeysInLootExtended] WARNING: Unknown profile '{config.ActiveProfile}' selected. Defaulting to 'Custom' settings.");
            config.ActiveProfile = "Custom";
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
        if (!ProfileDefinitions.TryGetValue(profile ?? string.Empty, out var profileDef))
        {
            return;
        }

        ScaleContainer(locConfig.JacketContainer, profileDef.CommonScale, profileDef.RareScale, profileDef.SuperRareScale);
        ScaleContainer(locConfig.DuffleBagContainer, profileDef.CommonScale, profileDef.RareScale, profileDef.SuperRareScale);
        ScaleContainer(locConfig.DeadScavContainer, profileDef.CommonScale, profileDef.RareScale, profileDef.SuperRareScale);
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
