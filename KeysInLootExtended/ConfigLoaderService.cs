using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Utils;

namespace KeysInLootExtended;

[Injectable(InjectionType.Singleton)]
public class KeysInLootConfigLoader
{
    private readonly ISptLogger<KeysInLootConfigLoader> _logger;
    private readonly ModHelper _modHelper;
    
    public KeysInLootCoreConfig Config { get; private set; }

    public KeysInLootConfigLoader(
        ISptLogger<KeysInLootConfigLoader> logger,
        ModHelper modHelper)
    {
        _logger = logger;
        _modHelper = modHelper;
        
        Config = LoadCoreConfig();
    }

    private KeysInLootCoreConfig LoadCoreConfig()
    {
        var pathToMod = _modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        var configPath = Path.Combine(pathToMod, "config.jsonc");
        if (!File.Exists(configPath))
        {
            _logger.Error("[KeysInLootExtended] FATAL ERROR: Core config.jsonc not found!");
            throw new FileNotFoundException($"[KeysInLootExtended] Core config.jsonc not found at {configPath}");
        }

        var jsonSettings = new JsonSerializerOptions
        {
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            PropertyNameCaseInsensitive = true
        };
        jsonSettings.Converters.Add(new CultureInvariantDoubleConverter());

        var configText = File.ReadAllText(configPath);
        var config = JsonSerializer.Deserialize<KeysInLootCoreConfig>(configText, jsonSettings);

        if (config == null)
        {
            _logger.Error("[KeysInLootExtended] FATAL ERROR: Failed to deserialize config.jsonc!");
            throw new InvalidDataException("[KeysInLootExtended] Failed to deserialize config.jsonc to KeysInLootCoreConfig.");
        }

        // Apply profile overrides
        switch (config.ActiveProfile)
        {
            case "Vanilla Plus":
                config.KeyWeight = new KeysInLootRarityConfig { NotExist = 200, Common = 200, Rare = 100, SuperRare = 40 };
                config.KeycardWeight = new KeysInLootRarityConfig { NotExist = 60, Common = 60, Rare = 30, SuperRare = 15 };
                config.OverrideLootDistribution = true;
                config.KeyFleaPricesMultiplier = 0.4;
                config.KeyTraderPricesMultiplier = 0.4;
                config.CellsH = 3;
                config.CellsV = 3;
                break;
            case "Hardcore Scarcity":
                config.KeyWeight = new KeysInLootRarityConfig { NotExist = 60, Common = 60, Rare = 30, SuperRare = 15 };
                config.KeycardWeight = new KeysInLootRarityConfig { NotExist = 15, Common = 15, Rare = 8, SuperRare = 4 };
                config.OverrideLootDistribution = false;
                config.KeyFleaPricesMultiplier = 1.0;
                config.KeyTraderPricesMultiplier = 1.0;
                config.CellsH = 3;
                config.CellsV = 3;
                break;
            case "The Original Experience":
                config.KeyWeight = new KeysInLootRarityConfig { NotExist = 500, Common = 500, Rare = 500, SuperRare = 500 };
                config.KeycardWeight = new KeysInLootRarityConfig { NotExist = 200, Common = 200, Rare = 200, SuperRare = 200 };
                config.OverrideLootDistribution = true;
                config.KeyFleaPricesMultiplier = 0.4;
                config.KeyTraderPricesMultiplier = 0.4;
                config.CellsH = 3;
                config.CellsV = 3;
                break;
            case "The Loot Piñata":
                config.KeyWeight = new KeysInLootRarityConfig { NotExist = 10, Common = 10, Rare = 5000, SuperRare = 10000 };
                config.KeycardWeight = new KeysInLootRarityConfig { NotExist = 10, Common = 10, Rare = 1000, SuperRare = 5000 };
                config.OverrideLootDistribution = true;
                config.KeyFleaPricesMultiplier = 0.1;
                config.KeyTraderPricesMultiplier = 0.1;
                config.CellsH = 5;
                config.CellsV = 5;
                break;
            case "Disabled":
            case "Custom":
                break;
            default:
                _logger.Warning($"[KeysInLootExtended] WARNING: Unknown profile '{config.ActiveProfile}' selected. Defaulting to 'Custom' settings.");
                config.ActiveProfile = "Custom";
                break;
        }

        return config;
    }

    public KeysInLootLocationConfig? LoadLocationConfig(string locationName)
    {
        var pathToMod = _modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        var configPath = Path.Combine(pathToMod, "locations", $"{locationName}.jsonc");
        
        if (!File.Exists(configPath))
        {
            _logger.Warning($"[KeysInLootExtended] WARNING: Location config not found at {configPath}. Falling back to default weights.");
            return null;
        }

        var jsonSettings = new JsonSerializerOptions
        {
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            PropertyNameCaseInsensitive = true
        };
        jsonSettings.Converters.Add(new CultureInvariantDoubleConverter());
        
        var configText = File.ReadAllText(configPath);
        return JsonSerializer.Deserialize<KeysInLootLocationConfig>(configText, jsonSettings) 
            ?? throw new InvalidDataException($"[KeysInLootExtended] Failed to deserialize location config {locationName}.jsonc.");
    }
}
