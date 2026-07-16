using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;

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
                // Replace comma with dot to handle European decimal formats gracefully
                stringValue = stringValue.Replace(',', '.');
                if (double.TryParse(stringValue, NumberStyles.Any, CultureInfo.InvariantCulture, out double result))
                {
                    return result;
                }
            }
        }

        throw new JsonException($"Unable to convert \"{reader.GetString()}\" to double.");
    }

    public override void Write(Utf8JsonWriter writer, double value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value);
    }
}

/// <summary>
/// Core configuration model representing the global state defined in config.jsonc.
/// </summary>
public class KeysInLootCoreConfig
{
    /// <summary>
    /// Selects the overarching spawn and economy profile. Valid values include "Balanced", "Bountiful", "Refined", "Hardcore Scarcity", "The Mod Classic", "The Loot Piñata", "Disabled", and "Custom".
    /// </summary>
    [JsonPropertyName("activeProfile")]
    public string ActiveProfile { get; set; } = "Balanced";

    [JsonPropertyName("keyWeight")]
    public KeysInLootRarityConfig KeyWeight { get; set; } = new();

    [JsonPropertyName("keycardWeight")]
    public KeysInLootRarityConfig KeycardWeight { get; set; } = new();

    [JsonPropertyName("keyTraderPricesMultiplier")]
    public double KeyTraderPricesMultiplier { get; set; } = 0.4;

    [JsonPropertyName("banKeysFromFence")]
    public bool BanKeysFromFence { get; set; } = false;

    [JsonPropertyName("keyFleaPricesMultiplier")]
    public double KeyFleaPricesMultiplier { get; set; } = 0.4;

    [JsonPropertyName("overrideLootDistribution")]
    public bool OverrideLootDistribution { get; set; } = true;

    [JsonPropertyName("overrideLootDistributionJackets")]
    public List<ItemCountDistributionConfig> OverrideLootDistributionJackets { get; set; } = new();

    [JsonPropertyName("overrideLootDistributionDuffleBags")]
    public List<ItemCountDistributionConfig> OverrideLootDistributionDuffleBags { get; set; } = new();

    [JsonPropertyName("overrideLootDistributionDeadScavs")]
    public List<ItemCountDistributionConfig> OverrideLootDistributionDeadScavs { get; set; } = new();

    [JsonPropertyName("cellsH")]
    public int CellsH { get; set; } = 3;

    [JsonPropertyName("cellsV")]
    public int CellsV { get; set; } = 3;

    [JsonPropertyName("enableLocationsConfig")]
    public bool EnableLocationsConfig { get; set; } = true;

    [JsonPropertyName("consoleVerbosity")]
    public string? ConsoleVerbosity { get; set; }
}

/// <summary>
/// Configuration model representing map-specific overrides defined in the locations directory.
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
/// Defines weight overrides for a specific type of container (e.g., Jacket, Duffle Bag).
/// </summary>
public class KeysInLootContainerConfig
{
    [JsonPropertyName("key")]
    public KeysInLootRarityConfig? Key { get; set; } = null;

    [JsonPropertyName("keycard")]
    public KeysInLootRarityConfig? Keycard { get; set; } = null;
}

/// <summary>
/// Represents the relative probabilities across the four standard SPT item rarities.
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
}

/// <summary>
/// Represents a targeted configuration for item count distributions within a container grid.
/// </summary>
public class ItemCountDistributionConfig
{
    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("relativeProbability")]
    public int RelativeProbability { get; set; }
}
