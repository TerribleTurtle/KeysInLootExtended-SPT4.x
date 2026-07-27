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
    }

    private static readonly Dictionary<string, ProfileDefinition> ProfileDefinitions = new(StringComparer.OrdinalIgnoreCase)
    {
        { "balanced", new ProfileDefinition {
            ApplyCoreConfig = c => {
                c.KeyWeight = new KeysInLootRarityConfig { NotExist = 200, Common = 200, Rare = 100, SuperRare = 40 };
                c.KeycardWeight = new KeysInLootRarityConfig { NotExist = 60, Common = 60, Rare = 30, SuperRare = 15 };
                c.OverrideLootDistribution = true;
                
                var balancedCounts = new System.Collections.Generic.List<ItemCountDistributionConfig>
                {
                    new ItemCountDistributionConfig { Count = 5, RelativeProbability = 200 },
                    new ItemCountDistributionConfig { Count = 4, RelativeProbability = 800 },
                    new ItemCountDistributionConfig { Count = 3, RelativeProbability = 3500 },
                    new ItemCountDistributionConfig { Count = 2, RelativeProbability = 4300 },
                    new ItemCountDistributionConfig { Count = 1, RelativeProbability = 1000 },
                    new ItemCountDistributionConfig { Count = 0, RelativeProbability = 200 }
                };
                
                c.OverrideLootDistributionJackets = balancedCounts;
                c.OverrideLootDistributionDuffleBags = balancedCounts;
                // Leave Dead Scavs as default for balanced
                c.OverrideLootDistributionDeadScavs = null;
                
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
                
                var bountifulCounts = new System.Collections.Generic.List<ItemCountDistributionConfig>
                {
                    new ItemCountDistributionConfig { Count = 5, RelativeProbability = 1000 },
                    new ItemCountDistributionConfig { Count = 4, RelativeProbability = 4000 },
                    new ItemCountDistributionConfig { Count = 3, RelativeProbability = 3800 },
                    new ItemCountDistributionConfig { Count = 2, RelativeProbability = 1000 },
                    new ItemCountDistributionConfig { Count = 1, RelativeProbability = 150 },
                    new ItemCountDistributionConfig { Count = 0, RelativeProbability = 50 }
                };
                
                c.OverrideLootDistributionJackets = bountifulCounts;
                c.OverrideLootDistributionDuffleBags = bountifulCounts;
                c.OverrideLootDistributionDeadScavs = bountifulCounts;
                
                c.KeyFleaPricesMultiplier = 0.2;
                c.KeyTraderPricesMultiplier = 0.2;
                c.CellsH = 3;
                c.CellsV = 3;
            }
        }},
        { "generous", new ProfileDefinition {
            ApplyCoreConfig = c => {
                c.KeyWeight = new KeysInLootRarityConfig { NotExist = 120, Common = 120, Rare = 350, SuperRare = 200 };
                c.KeycardWeight = new KeysInLootRarityConfig { NotExist = 30, Common = 30, Rare = 100, SuperRare = 60 };
                c.OverrideLootDistribution = true;
                
                var bountifulCounts = new System.Collections.Generic.List<ItemCountDistributionConfig>
                {
                    new ItemCountDistributionConfig { Count = 5, RelativeProbability = 1000 },
                    new ItemCountDistributionConfig { Count = 4, RelativeProbability = 4000 },
                    new ItemCountDistributionConfig { Count = 3, RelativeProbability = 3800 },
                    new ItemCountDistributionConfig { Count = 2, RelativeProbability = 1000 },
                    new ItemCountDistributionConfig { Count = 1, RelativeProbability = 150 },
                    new ItemCountDistributionConfig { Count = 0, RelativeProbability = 50 }
                };
                
                c.OverrideLootDistributionJackets = bountifulCounts;
                c.OverrideLootDistributionDuffleBags = bountifulCounts;
                c.OverrideLootDistributionDeadScavs = bountifulCounts;
                
                c.KeyFleaPricesMultiplier = 0.5;
                c.KeyTraderPricesMultiplier = 0.5;
                c.CellsH = 3;
                c.CellsV = 3;
            }
        }},
        { "refined", new ProfileDefinition {
            ApplyCoreConfig = c => {
                c.KeyWeight = new KeysInLootRarityConfig { NotExist = 60, Common = 60, Rare = 170, SuperRare = 110 };
                c.KeycardWeight = new KeysInLootRarityConfig { NotExist = 18, Common = 18, Rare = 51, SuperRare = 41 };
                c.OverrideLootDistribution = true;
                
                var refinedCounts = new System.Collections.Generic.List<ItemCountDistributionConfig>
                {
                    new ItemCountDistributionConfig { Count = 5, RelativeProbability = 100 },
                    new ItemCountDistributionConfig { Count = 4, RelativeProbability = 200 },
                    new ItemCountDistributionConfig { Count = 3, RelativeProbability = 700 },
                    new ItemCountDistributionConfig { Count = 2, RelativeProbability = 4000 },
                    new ItemCountDistributionConfig { Count = 1, RelativeProbability = 4500 },
                    new ItemCountDistributionConfig { Count = 0, RelativeProbability = 500 }
                };
                c.OverrideLootDistributionJackets = refinedCounts;
                c.OverrideLootDistributionDuffleBags = refinedCounts;
                c.OverrideLootDistributionDeadScavs = null;
                
                c.KeyFleaPricesMultiplier = 1.0;
                c.KeyTraderPricesMultiplier = 1.0;
                c.CellsH = 3;
                c.CellsV = 3;
            }
        }},
        { "hardcore scarcity", new ProfileDefinition {
            ApplyCoreConfig = c => {
                c.KeyWeight = new KeysInLootRarityConfig { NotExist = 60, Common = 60, Rare = 30, SuperRare = 15 };
                c.KeycardWeight = new KeysInLootRarityConfig { NotExist = 15, Common = 15, Rare = 8, SuperRare = 4 };
                c.OverrideLootDistribution = true;
                
                var hardcoreCounts = new System.Collections.Generic.List<ItemCountDistributionConfig>
                {
                    new ItemCountDistributionConfig { Count = 5, RelativeProbability = 10 },
                    new ItemCountDistributionConfig { Count = 4, RelativeProbability = 40 },
                    new ItemCountDistributionConfig { Count = 3, RelativeProbability = 100 },
                    new ItemCountDistributionConfig { Count = 2, RelativeProbability = 850 },
                    new ItemCountDistributionConfig { Count = 1, RelativeProbability = 4000 },
                    new ItemCountDistributionConfig { Count = 0, RelativeProbability = 5000 }
                };
                
                c.OverrideLootDistributionJackets = hardcoreCounts;
                c.OverrideLootDistributionDuffleBags = hardcoreCounts;
                c.OverrideLootDistributionDeadScavs = hardcoreCounts;
                
                c.KeyFleaPricesMultiplier = 1.0;
                c.KeyTraderPricesMultiplier = 1.0;
                c.CellsH = 3;
                c.CellsV = 3;
            }
        }},
        { "the musicmaniac classic", new ProfileDefinition {
            ApplyCoreConfig = c => {
                c.KeyWeight = new KeysInLootRarityConfig { NotExist = 500, Common = 500, Rare = 500, SuperRare = 500 };
                // Fixed original mod bug: The original mod accidentally set 'NotExist' to 500, but the intent was 50 for all keycards.
                c.KeycardWeight = new KeysInLootRarityConfig { NotExist = 50, Common = 50, Rare = 50, SuperRare = 50 };
                c.OverrideLootDistribution = true;
                
                var classicCounts = new System.Collections.Generic.List<ItemCountDistributionConfig>
                {
                    new ItemCountDistributionConfig { Count = 5, RelativeProbability = 500 },
                    new ItemCountDistributionConfig { Count = 4, RelativeProbability = 3000 },
                    new ItemCountDistributionConfig { Count = 3, RelativeProbability = 3000 },
                    new ItemCountDistributionConfig { Count = 2, RelativeProbability = 3000 },
                    new ItemCountDistributionConfig { Count = 1, RelativeProbability = 400 },
                    new ItemCountDistributionConfig { Count = 0, RelativeProbability = 100 }
                };
                
                c.OverrideLootDistributionJackets = classicCounts;
                c.OverrideLootDistributionDuffleBags = classicCounts;
                c.OverrideLootDistributionDeadScavs = classicCounts;
                
                c.KeyFleaPricesMultiplier = 0.75;
                c.KeyTraderPricesMultiplier = 0.75;
                c.CellsH = 3;
                c.CellsV = 3;
                c.EnableLocationsConfig = false;
            },
        }},
        { "the loot pinata", new ProfileDefinition {
            ApplyCoreConfig = c => {
                c.KeyWeight = new KeysInLootRarityConfig { NotExist = 10, Common = 10, Rare = 5000, SuperRare = 10000 };
                c.KeycardWeight = new KeysInLootRarityConfig { NotExist = 10, Common = 10, Rare = 1000, SuperRare = 5000 };
                c.OverrideLootDistribution = true;
                c.EnableLocationsConfig = false;
                
                var pinataCounts = new System.Collections.Generic.List<ItemCountDistributionConfig>
                {
                    new ItemCountDistributionConfig { Count = 25, RelativeProbability = 100 },
                    new ItemCountDistributionConfig { Count = 20, RelativeProbability = 300 },
                    new ItemCountDistributionConfig { Count = 15, RelativeProbability = 500 },
                    new ItemCountDistributionConfig { Count = 10, RelativeProbability = 100 }
                };
                
                c.OverrideLootDistributionJackets = pinataCounts;
                c.OverrideLootDistributionDuffleBags = pinataCounts;
                c.OverrideLootDistributionDeadScavs = pinataCounts;

                c.KeyFleaPricesMultiplier = 1.0;
                c.KeyTraderPricesMultiplier = 1.0;
                c.CellsH = 5;
                c.CellsV = 5;
            }
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

        string profileKey = config.ActiveProfile?.Trim().ToLowerInvariant() ?? string.Empty;
        
        if (profileKey.Contains("balanced") || profileKey.Contains("(1)")) profileKey = "1";
        else if (profileKey.Contains("bountiful") || profileKey.Contains("(2)")) profileKey = "2";
        else if (profileKey.Contains("generous") || profileKey.Contains("(3)")) profileKey = "3";
        else if (profileKey.Contains("refined") || profileKey.Contains("(4)")) profileKey = "4";
        else if (profileKey.Contains("hardcore scarcity") || profileKey.Contains("(5)")) profileKey = "5";
        else if (profileKey.Contains("the musicmaniac classic") || profileKey.Contains("the mod classic") || profileKey.Contains("(6)")) profileKey = "6";
        else if (profileKey.Contains("piñata") || profileKey.Contains("pinata") || profileKey.Contains("piata") || profileKey.Contains("(7)")) profileKey = "7";
        else if (profileKey.Contains("custom") || profileKey.Contains("(8)")) profileKey = "8";
        else if (profileKey.Contains("disabled") || profileKey.Contains("(9)")) profileKey = "9";

        profileKey = profileKey switch
        {
            "1" => "balanced",
            "2" => "bountiful",
            "3" => "generous",
            "4" => "refined",
            "5" => "hardcore scarcity",
            "6" => "the musicmaniac classic",
            "7" => "the loot pinata",
            "8" => "custom",
            "9" => "disabled",
            _ => profileKey
        };

        if (!ProfileDefinitions.ContainsKey(profileKey))
        {
            _logger.Warning($"[KeysInLootExtended] WARNING: Unknown profile '{config.ActiveProfile}' selected. Defaulting to 'Custom' settings.");
            profileKey = "custom";
        }

        // Apply profile overrides safely handling null profiles
        if (profileKey == "custom")
        {
            config.KeyWeight ??= new();
            config.KeycardWeight ??= new();
            if (config.OverrideLootDistribution)
            {
                config.OverrideLootDistributionJackets ??= new();
                config.OverrideLootDistributionDuffleBags ??= new();
                config.OverrideLootDistributionDeadScavs ??= new();
            }
        }

        config.ActiveProfile = profileKey;
        ProfileDefinitions[profileKey].ApplyCoreConfig?.Invoke(config);

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

        ScaleLocationConfig(locConfig);
        return locConfig;
    }

    private void ScaleLocationConfig(KeysInLootLocationConfig locConfig)
    {
        if (Config?.KeyWeight == null) return;

        double commonRatio = (double)Config.KeyWeight.Common / Math.Max(1, 200);
        double rareRatio = (double)Config.KeyWeight.Rare / Math.Max(1, 100);
        double superRareRatio = (double)Config.KeyWeight.SuperRare / Math.Max(1, 40);
        double notExistRatio = (double)Config.KeyWeight.NotExist / Math.Max(1, 60);

        ScaleContainer(locConfig.JacketContainer, commonRatio, rareRatio, superRareRatio, notExistRatio);
        ScaleContainer(locConfig.DuffleBagContainer, commonRatio, rareRatio, superRareRatio, notExistRatio);
        ScaleContainer(locConfig.DeadScavContainer, commonRatio, rareRatio, superRareRatio, notExistRatio);
    }

    private void ScaleContainer(KeysInLootContainerConfig? container, double commonRatio, double rareRatio, double superRareRatio, double notExistRatio)
    {
        if (container == null) return;
        ScaleRarity(container.Key, commonRatio, rareRatio, superRareRatio, notExistRatio);
        ScaleRarity(container.Keycard, commonRatio, rareRatio, superRareRatio, notExistRatio);
    }

    private void ScaleRarity(KeysInLootRarityConfig? rarity, double commonRatio, double rareRatio, double superRareRatio, double notExistRatio)
    {
        if (rarity == null) return;
        
        if (rarity.NotExist > 0)
            rarity.NotExist = Math.Max(1, (int)Math.Round(rarity.NotExist * notExistRatio));
            
        if (rarity.Common > 0)
            rarity.Common = Math.Max(1, (int)Math.Round(rarity.Common * commonRatio));
            
        if (rarity.Rare > 0)
            rarity.Rare = Math.Max(1, (int)Math.Round(rarity.Rare * rareRatio));
            
        if (rarity.SuperRare > 0)
            rarity.SuperRare = Math.Max(1, (int)Math.Round(rarity.SuperRare * superRareRatio));
    }
}

