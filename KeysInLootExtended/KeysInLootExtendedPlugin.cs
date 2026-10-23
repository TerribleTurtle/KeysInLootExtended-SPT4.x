using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Spt.Mod;

namespace KeysInLootExtended;

/// <summary>
/// Defines the required metadata for the mod to be recognized by the SPT server.
/// </summary>
public record ModMetadata : IModMetadata
{
    public string ModGuid { get; init; } = "DJ-KeysInLootExtended";
    public string Name { get; init; } = "KeysInLootExtended";
    public string Author { get; init; } = "TerribleTurtle";
    public SemanticVersioning.Version Version { get; init; } = new("2.2.0");
    public SemanticVersioning.Range SptVersion { get; init; } = new("~4.1.0");
    
    public List<string>? Incompatibilities { get; init; }
    public Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; }
    public string? Url { get; init; }
    public bool HasPrepatcher { get; init; }
    public string License { get; init; } = "MIT";
    public List<string>? Contributors { get; init; }
}

/// <summary>
/// The main entry point for the KeysInLootExtended mod. 
/// Orchestrates configuration loading, loot injection, and economy adjustments.
/// </summary>
[Injectable(TypePriority = OnLoadOrder.Routers + 2)]
public class KeysInLootExtendedPlugin(
    KeysInLootConfigLoader configLoader,
    LootInjectionService lootInjectionService,
    ItemPriceService itemPriceService,
    ContainerGridService containerGridService,
    ISptLogger<KeysInLootExtendedPlugin> logger) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            logger.Success($"[KeysInLootExtended] Initialized and Loaded Config: {configLoader.Config.ActiveProfile}");

            lootInjectionService.InjectKeysIntoLocations();
            itemPriceService.AdjustPrices();
            containerGridService.AdjustGridSizes();
        }
        catch (Exception ex)
        {
            logger.Error($"[KeysInLootExtended] Unhandled error during initialization: {ex}");
        }
        
        return Task.CompletedTask;
    }
}
