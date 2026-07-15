using System.Text.Json.Serialization;

namespace KeysInLootExtended;

public class KeysInLootCoreConfig
{
    [JsonPropertyName("activeProfile")]
    public string ActiveProfile { get; set; } = "Vanilla Plus";

    [JsonPropertyName("keyWeight")]
    public KeysInLootRarityConfig KeyWeight { get; set; } = new();

    [JsonPropertyName("keycardWeight")]
    public KeysInLootRarityConfig KeycardWeight { get; set; } = new();

    [JsonPropertyName("keyTraderPricesMultiplier")]
    public double KeyTraderPricesMultiplier { get; set; } = 0.4;

    [JsonPropertyName("keyFleaPricesMultiplier")]
    public double KeyFleaPricesMultiplier { get; set; } = 0.4;

    [JsonPropertyName("overrideLootDistribution")]
    public bool OverrideLootDistribution { get; set; } = true;

    [JsonPropertyName("overRideLootDistributionJackets")]
    public List<ItemCountDistributionConfig> OverrideLootDistributionJackets { get; set; } = new();

    [JsonPropertyName("overRideLootDistributionDuffleBags")]
    public List<ItemCountDistributionConfig> OverrideLootDistributionDuffleBags { get; set; } = new();

    [JsonPropertyName("overRideLootDistributionDeadScavs")]
    public List<ItemCountDistributionConfig> OverrideLootDistributionDeadScavs { get; set; } = new();

    [JsonPropertyName("cellsH")]
    public int CellsH { get; set; } = 3;

    [JsonPropertyName("cellsV")]
    public int CellsV { get; set; } = 3;

    [JsonPropertyName("enableLocationsConfig")]
    public bool EnableLocationsConfig { get; set; } = false;

    [JsonPropertyName("consoleVerbosity")]
    public string? ConsoleVerbosity { get; set; }
}

public class KeysInLootLocationConfig
{
    [JsonPropertyName("jacketContainer")]
    public KeysInLootContainerConfig? JacketContainer { get; set; } = null;

    [JsonPropertyName("duffleBagContainer")]
    public KeysInLootContainerConfig? DuffleBagContainer { get; set; } = null;

    [JsonPropertyName("deadScavContainer")]
    public KeysInLootContainerConfig? DeadScavContainer { get; set; } = null;
}

public class KeysInLootContainerConfig
{
    [JsonPropertyName("key")]
    public KeysInLootRarityConfig? Key { get; set; } = null;

    [JsonPropertyName("keycard")]
    public KeysInLootRarityConfig? Keycard { get; set; } = null;
}

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

public class ItemCountDistributionConfig
{
    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("relativeProbability")]
    public int RelativeProbability { get; set; }
}
