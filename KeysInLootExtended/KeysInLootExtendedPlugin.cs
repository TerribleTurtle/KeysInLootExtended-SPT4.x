using System.Threading.Tasks;
using System.Text.Json;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;

using SPTarkov.Server.Core.Servers;

namespace KeysInLootExtended;

/// <summary>
/// Defines the required metadata for the mod to be recognized by the SPT server.
/// </summary>
public record ModMetadata : AbstractModMetadata
{
    public override string ModGuid { get; init; } = "DJ-KeysInLootExtended";
    public override string Name { get; init; } = "KeysInLootExtended";
    public override string Author { get; init; } = "TerribleTurtle";
    public override SemanticVersioning.Version Version { get; init; } = new("2.0.4");
    public override SemanticVersioning.Range SptVersion { get; init; } = new("~4.0.13");
    
    public override List<string>? Incompatibilities { get; init; }
    public override Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; }
    public override string? Url { get; init; }
    public override bool? IsBundleMod { get; init; }
    public override string License { get; init; } = "MIT";
    public override List<string>? Contributors { get; init; }
}

/// <summary>
/// The main entry point for the KeysInLootExtended mod. 
/// Orchestrates configuration loading, loot injection, and economy adjustments.
/// </summary>
[Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 2)]
public class KeysInLootExtendedPlugin(
    KeysInLootConfigLoader configLoader,
    LootInjectionService lootInjectionService,
    ItemPriceService itemPriceService,
    ContainerGridService containerGridService,
    ISptLogger<KeysInLootExtendedPlugin> logger) : IOnLoad
{
    public Task OnLoad()
    {
        logger.Success($"[KeysInLootExtended] Initialized and Loaded Config: {configLoader.Config.ActiveProfile}");

        lootInjectionService.InjectKeysIntoLocations();
        itemPriceService.AdjustPrices();
        containerGridService.AdjustGridSizes();
        
        return Task.CompletedTask;
    }
}
