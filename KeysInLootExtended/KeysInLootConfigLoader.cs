using System.Reflection;
using System.Text;
using System.Text.Json;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Server;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("KeysInLootExtended.Tests")]

namespace KeysInLootExtended;

/// <summary>
/// Loads, migrates, resolves, and scales config.jsonc and locations/*.jsonc.
/// </summary>
[Injectable(InjectionType.Singleton)]
public class KeysInLootConfigLoader
{
    internal const int CurrentConfigVersion = 3;
    internal const string EmbeddedConfigResourceName = "KeysInLootExtended.default_config.jsonc";
    internal const double ReduceCommonMultiplier = 0.15;

    private readonly ISptLogger<KeysInLootConfigLoader> _logger;
    private readonly ModHelper _modHelper;

    internal static readonly JsonSerializerOptions JsonSettings;

    static KeysInLootConfigLoader()
    {
        JsonSettings = new JsonSerializerOptions
        {
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            PropertyNameCaseInsensitive = true
        };
        JsonSettings.Converters.Add(new CultureInvariantDoubleConverter());
        JsonSettings.Converters.Add(new NullableCultureInvariantDoubleConverter());
    }

    /// <summary>
    /// Cached global configuration resolved at startup.
    /// </summary>
    public KeysInLootCoreConfig Config { get; private set; }

    public KeysInLootConfigLoader(
        ISptLogger<KeysInLootConfigLoader> logger,
        ModHelper modHelper)
    {
        _logger = logger;
        _modHelper = modHelper;

        var pathToMod = _modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        Config = LoadOrMigrateCoreConfig(pathToMod, _logger);
    }

    private static List<ItemCountDistributionConfig> CreateGentleJacketCounts() =>
    [
        new() { Count = 3, RelativeProbability = 500 },
        new() { Count = 2, RelativeProbability = 2500 },
        new() { Count = 1, RelativeProbability = 6700 },
        new() { Count = 0, RelativeProbability = 300 }
    ];

    private static List<ItemCountDistributionConfig> CreateCasualJacketCounts() =>
    [
        new() { Count = 4, RelativeProbability = 800 },
        new() { Count = 3, RelativeProbability = 3000 },
        new() { Count = 2, RelativeProbability = 4500 },
        new() { Count = 1, RelativeProbability = 1500 },
        new() { Count = 0, RelativeProbability = 200 }
    ];

    private static List<ItemCountDistributionConfig> CreateCasualDuffleCounts() =>
    [
        new() { Count = 4, RelativeProbability = 1000 },
        new() { Count = 3, RelativeProbability = 3500 },
        new() { Count = 2, RelativeProbability = 4000 },
        new() { Count = 1, RelativeProbability = 1300 },
        new() { Count = 0, RelativeProbability = 200 }
    ];

    private static List<ItemCountDistributionConfig>? CloneCounts(List<ItemCountDistributionConfig>? source) =>
        source?.Select(x => new ItemCountDistributionConfig
        {
            Count = x.Count,
            RelativeProbability = x.RelativeProbability
        }).ToList();

    private static readonly Dictionary<string, Func<KeysInLootCoreConfig, ResolvedLootSettings>> PresetDefinitions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["hardcore"] = _ => new ResolvedLootSettings
            {
                KeyWeight = new KeysInLootRarityConfig { NotExist = 65, Common = 65, Rare = 28, SuperRare = 10 },
                KeycardWeight = new KeysInLootRarityConfig { NotExist = 20, Common = 20, Rare = 10, SuperRare = 6 },
                OverrideLootDistribution = false,
                JacketsItemCounts = null,
                DuffleBagsItemCounts = null,
                DeadScavsItemCounts = null,
                FleaPriceMultiplier = 1.0,
                TraderPriceMultiplier = 1.0,
                MinCellsH = 2,
                MinCellsV = 2
            },

            ["balanced"] = _ => new ResolvedLootSettings
            {
                KeyWeight = new KeysInLootRarityConfig { NotExist = 200, Common = 200, Rare = 100, SuperRare = 40 },
                KeycardWeight = new KeysInLootRarityConfig { NotExist = 60, Common = 60, Rare = 30, SuperRare = 20 },
                OverrideLootDistribution = true,
                JacketsItemCounts = CreateGentleJacketCounts(),
                DuffleBagsItemCounts = null,
                DeadScavsItemCounts = null,
                FleaPriceMultiplier = 0.40,
                TraderPriceMultiplier = 0.40,
                MinCellsH = 3,
                MinCellsV = 3
            },

            ["casual"] = _ => new ResolvedLootSettings
            {
                KeyWeight = new KeysInLootRarityConfig { NotExist = 300, Common = 300, Rare = 190, SuperRare = 85 },
                KeycardWeight = new KeysInLootRarityConfig { NotExist = 90, Common = 90, Rare = 57, SuperRare = 45 },
                OverrideLootDistribution = true,
                JacketsItemCounts = CreateCasualJacketCounts(),
                DuffleBagsItemCounts = CreateCasualDuffleCounts(),
                DeadScavsItemCounts = null,
                FleaPriceMultiplier = 0.25,
                TraderPriceMultiplier = 0.25,
                MinCellsH = 3,
                MinCellsV = 3
            },

            ["custom"] = c =>
            {
                var custom = c.Custom ?? new KeysInLootCustomConfig();
                return new ResolvedLootSettings
                {
                    KeyWeight = custom.KeyWeight?.Clone() ?? new KeysInLootRarityConfig(),
                    KeycardWeight = custom.KeycardWeight?.Clone() ?? new KeysInLootRarityConfig { NotExist = 60, Common = 60, Rare = 30, SuperRare = 20 },
                    OverrideLootDistribution = custom.OverrideLootDistribution,
                    JacketsItemCounts = CloneCounts(custom.JacketsItemCounts),
                    DuffleBagsItemCounts = CloneCounts(custom.DuffleBagsItemCounts),
                    DeadScavsItemCounts = CloneCounts(custom.DeadScavsItemCounts),
                    FleaPriceMultiplier = custom.FleaPriceMultiplier,
                    TraderPriceMultiplier = custom.TraderPriceMultiplier,
                    MinCellsH = custom.MinCellsH,
                    MinCellsV = custom.MinCellsV
                };
            }
        };

    internal static void ApplyResolvedSettings(KeysInLootCoreConfig config, ISptLogger<KeysInLootConfigLoader>? logger = null)
    {
        string presetKey = config.Preset?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!PresetDefinitions.TryGetValue(presetKey, out var buildResolved))
        {
            logger?.Warning($"[KeysInLootExtended] Unknown preset '{config.Preset}' in config.jsonc. Falling back to 'Balanced'.");
            presetKey = "balanced";
            buildResolved = PresetDefinitions["balanced"];
        }

        config.Preset = presetKey;
        var resolved = buildResolved(config);

        if (config.FleaPriceMultiplier.HasValue)
        {
            resolved.FleaPriceMultiplier = config.FleaPriceMultiplier.Value;
        }

        if (config.TraderPriceMultiplier.HasValue)
        {
            resolved.TraderPriceMultiplier = config.TraderPriceMultiplier.Value;
        }

        double weightMult = (double.IsNaN(config.SpawnWeightMultiplier) || double.IsInfinity(config.SpawnWeightMultiplier))
            ? 1.0
            : Math.Max(0.0, config.SpawnWeightMultiplier);

        double commonMult = weightMult * (config.ReduceCommonKeys ? ReduceCommonMultiplier : 1.0);
        double highTierMult = weightMult;

        if (Math.Abs(commonMult - 1.0) > 1e-9 || Math.Abs(highTierMult - 1.0) > 1e-9)
        {
            ScaleRarity(resolved.KeyWeight, commonMult, highTierMult, highTierMult, commonMult);
            ScaleRarity(resolved.KeycardWeight, commonMult, highTierMult, highTierMult, commonMult);
        }

        config.Resolved = resolved;
    }

    private static string ReadEmbeddedDefaultConfig()
    {
        using var stream = typeof(KeysInLootConfigLoader).Assembly.GetManifestResourceStream(EmbeddedConfigResourceName)
            ?? throw new InvalidOperationException($"[KeysInLootExtended] Embedded default config resource '{EmbeddedConfigResourceName}' is missing from assembly.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    internal static KeysInLootCoreConfig LoadOrMigrateCoreConfig(string pathToMod, ISptLogger<KeysInLootConfigLoader> logger)
    {
        var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        var configPath = Path.Combine(pathToMod, "config.jsonc");
        if (!File.Exists(configPath))
        {
            var freshDefault = ReadEmbeddedDefaultConfig();
            File.WriteAllText(configPath, freshDefault, utf8NoBom);
            logger.Warning($"[KeysInLootExtended] config.jsonc not found at {configPath}; generated default v{CurrentConfigVersion} config.jsonc.");
        }

        var configText = File.ReadAllText(configPath, Encoding.UTF8);
        var config = JsonSerializer.Deserialize<KeysInLootCoreConfig>(configText, JsonSettings)
            ?? throw new InvalidDataException("[KeysInLootExtended] Failed to deserialize config.jsonc to KeysInLootCoreConfig.");

        if (config.ConfigVersion < CurrentConfigVersion)
        {
            var backupPath = Path.Combine(pathToMod, "config.backup.jsonc");
            File.Copy(configPath, backupPath, overwrite: true);
            var defaultV3Text = ReadEmbeddedDefaultConfig();
            File.WriteAllText(configPath, defaultV3Text, utf8NoBom);
            logger.Warning(
                $"[KeysInLootExtended] Legacy config.jsonc detected (configVersion {config.ConfigVersion} < {CurrentConfigVersion}). " +
                $"Backed up old file to config.backup.jsonc and wrote fresh v{CurrentConfigVersion} config.jsonc " +
                $"(reset to enabled: true, preset: Balanced; set enabled: false in config.jsonc if you previously ran Disabled).");

            config = JsonSerializer.Deserialize<KeysInLootCoreConfig>(defaultV3Text, JsonSettings)
                ?? throw new InvalidDataException("[KeysInLootExtended] Failed to deserialize embedded default v3 config.jsonc.");
        }

        ApplyResolvedSettings(config, logger);
        return config;
    }

    public KeysInLootLocationConfig? LoadLocationConfig(string locationName)
    {
        var pathToMod = _modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        var configPath = Path.Combine(pathToMod, "locations", $"{locationName}.jsonc");

        if (!File.Exists(configPath))
        {
            _logger.Warning($"[KeysInLootExtended] Location config not found at {configPath}. Falling back to global weights.");
            return null;
        }

        var configText = File.ReadAllText(configPath, Encoding.UTF8);
        var locConfig = JsonSerializer.Deserialize<KeysInLootLocationConfig>(configText, JsonSettings)
            ?? throw new InvalidDataException($"[KeysInLootExtended] Failed to deserialize location config {locationName}.jsonc.");

        ScaleLocationConfig(locConfig);
        return locConfig;
    }

    internal void ScaleLocationConfig(KeysInLootLocationConfig locConfig)
    {
        var resolved = Config?.Resolved;
        if (resolved?.KeyWeight == null) return;

        double keyCommonRatio = (double)resolved.KeyWeight.Common / 200.0;
        double keyRareRatio = (double)resolved.KeyWeight.Rare / 100.0;
        double keySuperRareRatio = (double)resolved.KeyWeight.SuperRare / 40.0;
        double keyNotExistRatio = (double)resolved.KeyWeight.NotExist / 200.0;

        double keycardCommonRatio = resolved.KeycardWeight != null ? (double)resolved.KeycardWeight.Common / 60.0 : 1.0;
        double keycardRareRatio = resolved.KeycardWeight != null ? (double)resolved.KeycardWeight.Rare / 30.0 : 1.0;
        double keycardSuperRareRatio = resolved.KeycardWeight != null ? (double)resolved.KeycardWeight.SuperRare / 20.0 : 1.0;
        double keycardNotExistRatio = resolved.KeycardWeight != null ? (double)resolved.KeycardWeight.NotExist / 60.0 : 1.0;

        ScaleContainer(locConfig.JacketContainer, keyCommonRatio, keyRareRatio, keySuperRareRatio, keyNotExistRatio, keycardCommonRatio, keycardRareRatio, keycardSuperRareRatio, keycardNotExistRatio);
        ScaleContainer(locConfig.DuffleBagContainer, keyCommonRatio, keyRareRatio, keySuperRareRatio, keyNotExistRatio, keycardCommonRatio, keycardRareRatio, keycardSuperRareRatio, keycardNotExistRatio);
        ScaleContainer(locConfig.DeadScavContainer, keyCommonRatio, keyRareRatio, keySuperRareRatio, keyNotExistRatio, keycardCommonRatio, keycardRareRatio, keycardSuperRareRatio, keycardNotExistRatio);
    }

    internal static void ScaleContainer(
        KeysInLootContainerConfig? container,
        double keyCommonRatio, double keyRareRatio, double keySuperRareRatio, double keyNotExistRatio,
        double keycardCommonRatio, double keycardRareRatio, double keycardSuperRareRatio, double keycardNotExistRatio)
    {
        if (container == null) return;
        ScaleRarity(container.Key, keyCommonRatio, keyRareRatio, keySuperRareRatio, keyNotExistRatio);
        ScaleRarity(container.Keycard, keycardCommonRatio, keycardRareRatio, keycardSuperRareRatio, keycardNotExistRatio);
    }

    internal static void ScaleRarity(KeysInLootRarityConfig? rarity, double commonRatio, double rareRatio, double superRareRatio, double notExistRatio)
    {
        if (rarity == null) return;

        static int ScaleField(int currentWeight, double ratio)
        {
            if (currentWeight <= 0) return 0;
            if (ratio <= 0) return 0;
            return (int)Math.Clamp(Math.Round(currentWeight * ratio), 1.0, int.MaxValue);
        }

        rarity.NotExist = ScaleField(rarity.NotExist, notExistRatio);
        rarity.Common = ScaleField(rarity.Common, commonRatio);
        rarity.Rare = ScaleField(rarity.Rare, rareRatio);
        rarity.SuperRare = ScaleField(rarity.SuperRare, superRareRatio);
    }
}
