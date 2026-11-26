using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KeysInLootExtended;

public class CultureInvariantDoubleConverter : JsonConverter<double>
{
    public override double Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            return reader.GetDouble();
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var stringValue = reader.GetString();
            if (stringValue != null)
            {
                stringValue = stringValue.Replace(',', '.');
                if (double.TryParse(stringValue, NumberStyles.Any, CultureInfo.InvariantCulture, out double result))
                {
                    return result;
                }
            }
            throw new JsonException($"Unable to convert \"{stringValue}\" to double.");
        }

        throw new JsonException($"Unable to convert token type {reader.TokenType} to double.");
    }

    public override void Write(Utf8JsonWriter writer, double value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value);
    }
}

public class NullableCultureInvariantDoubleConverter : JsonConverter<double?>
{
    private static readonly CultureInvariantDoubleConverter Inner = new();

    public override double? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }
        return Inner.Read(ref reader, typeof(double), options);
    }

    public override void Write(Utf8JsonWriter writer, double? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
        {
            writer.WriteNumberValue(value.Value);
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}

/// <summary>
/// v3.0.0 root configuration model deserialized from config.jsonc.
/// </summary>
public class KeysInLootCoreConfig
{
    /// <summary>
    /// Schema version. Files missing configVersion (deserializing as 0) are backed up and upgraded to v3.
    /// </summary>
    [JsonPropertyName("configVersion")]
    public int ConfigVersion { get; set; } = 0;

    /// <summary>
    /// Master on/off switch. When false, the mod leaves all loot pools, prices, Fence, and container grids untouched.
    /// </summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Selected preset: "Hardcore", "Balanced", "Casual", or "Custom".
    /// </summary>
    [JsonPropertyName("preset")]
    public string Preset { get; set; } = "Balanced";

    /// <summary>
    /// Cuts Common and NotExist key/keycard weights by 85% on top of any preset so drops lean ~90% Rare & SuperRare.
    /// </summary>
    [JsonPropertyName("reduceCommonKeys")]
    public bool ReduceCommonKeys { get; set; } = false;

    /// <summary>
    /// Master multiplier applied to key and keycard weights on top of any preset (1.0 = preset default).
    /// </summary>
    [JsonPropertyName("spawnWeightMultiplier")]
    public double SpawnWeightMultiplier { get; set; } = 1.0;

    /// <summary>
    /// Optional Flea Market sell-price multiplier override. Null uses the active preset's multiplier.
    /// </summary>
    [JsonPropertyName("fleaPriceMultiplier")]
    public double? FleaPriceMultiplier { get; set; } = null;

    /// <summary>
    /// Optional Trader sell-price multiplier override. Null uses the active preset's multiplier.
    /// </summary>
    [JsonPropertyName("traderPriceMultiplier")]
    public double? TraderPriceMultiplier { get; set; } = null;

    [JsonPropertyName("banKeysFromFence")]
    public bool BanKeysFromFence { get; set; } = false;

    [JsonPropertyName("enableLocationsConfig")]
    public bool EnableLocationsConfig { get; set; } = true;

    [JsonPropertyName("debugLogs")]
    public bool DebugLogs { get; set; } = false;

    [JsonPropertyName("custom")]
    public KeysInLootCustomConfig Custom { get; set; } = new();

    /// <summary>
    /// Computed effective settings after resolving the selected preset and applying top-level overrides.
    /// </summary>
    [JsonIgnore]
    internal ResolvedLootSettings Resolved { get; set; } = new();
}

/// <summary>
/// Manual tables evaluated only when preset is set to "Custom".
/// </summary>
public class KeysInLootCustomConfig
{
    [JsonPropertyName("keyWeight")]
    public KeysInLootRarityConfig KeyWeight { get; set; } = new();

    [JsonPropertyName("keycardWeight")]
    public KeysInLootRarityConfig KeycardWeight { get; set; } = new() { NotExist = 60, Common = 60, Rare = 30, SuperRare = 20 };

    [JsonPropertyName("fleaPriceMultiplier")]
    public double FleaPriceMultiplier { get; set; } = 0.4;

    [JsonPropertyName("traderPriceMultiplier")]
    public double TraderPriceMultiplier { get; set; } = 0.4;

    [JsonPropertyName("overrideLootDistribution")]
    public bool OverrideLootDistribution { get; set; } = true;

    [JsonPropertyName("jacketsItemCounts")]
    public List<ItemCountDistributionConfig>? JacketsItemCounts { get; set; } = null;

    [JsonPropertyName("duffleBagsItemCounts")]
    public List<ItemCountDistributionConfig>? DuffleBagsItemCounts { get; set; } = null;

    [JsonPropertyName("deadScavsItemCounts")]
    public List<ItemCountDistributionConfig>? DeadScavsItemCounts { get; set; } = null;

    [JsonPropertyName("minCellsH")]
    public int MinCellsH { get; set; } = 3;

    [JsonPropertyName("minCellsV")]
    public int MinCellsV { get; set; } = 3;
}

internal sealed class ResolvedLootSettings
{
    public KeysInLootRarityConfig KeyWeight { get; set; } = new();
    public KeysInLootRarityConfig KeycardWeight { get; set; } = new() { NotExist = 60, Common = 60, Rare = 30, SuperRare = 20 };
    public double FleaPriceMultiplier { get; set; } = 0.4;
    public double TraderPriceMultiplier { get; set; } = 0.4;
    public bool OverrideLootDistribution { get; set; } = true;
    public List<ItemCountDistributionConfig>? JacketsItemCounts { get; set; }
    public List<ItemCountDistributionConfig>? DuffleBagsItemCounts { get; set; }
    public List<ItemCountDistributionConfig>? DeadScavsItemCounts { get; set; }
    public int MinCellsH { get; set; } = 3;
    public int MinCellsV { get; set; } = 3;
}

/// <summary>
/// Configuration model representing map-specific baseline weights in the locations directory.
/// </summary>
public class KeysInLootLocationConfig
{
    [JsonPropertyName("jacketContainer")]
    public KeysInLootContainerConfig? JacketContainer { get; set; } = null;

    [JsonPropertyName("duffleBagContainer")]
    public KeysInLootContainerConfig? DuffleBagContainer { get; set; } = null;

    [JsonPropertyName("deadScavContainer")]
    public KeysInLootContainerConfig? DeadScavContainer { get; set; } = null;
}

/// <summary>
/// Defines weight baselines for a container category (Jacket, Duffle Bag, Dead Scav).
/// </summary>
public class KeysInLootContainerConfig
{
    [JsonPropertyName("key")]
    public KeysInLootRarityConfig? Key { get; set; } = null;

    [JsonPropertyName("keycard")]
    public KeysInLootRarityConfig? Keycard { get; set; } = null;
}

/// <summary>
/// Relative spawn weights across the four EFT RarityPvE tiers.
/// </summary>
public class KeysInLootRarityConfig
{
    [JsonPropertyName("notExist")]
    public int NotExist { get; set; } = 200;

    [JsonPropertyName("common")]
    public int Common { get; set; } = 200;

    [JsonPropertyName("rare")]
    public int Rare { get; set; } = 100;

    [JsonPropertyName("superRare")]
    public int SuperRare { get; set; } = 40;

    public KeysInLootRarityConfig Clone() => new()
    {
        NotExist = NotExist,
        Common = Common,
        Rare = Rare,
        SuperRare = SuperRare
    };
}

/// <summary>
/// Item count probability entry for a container.
/// </summary>
public class ItemCountDistributionConfig
{
    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("relativeProbability")]
    public int RelativeProbability { get; set; }
}
