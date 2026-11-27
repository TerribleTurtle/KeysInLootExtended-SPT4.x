using System.Threading;
using System.Threading.Tasks;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Spt.Mod;

namespace DynamicKeyLoot;

public record ModMetadata : IModMetadata
{
    public string ModGuid { get; init; } = "TerribleTurtle-DynamicKeyLoot";
    public string Name { get; init; } = "Dynamic Key Loot";
    public string Author { get; init; } = "TerribleTurtle";
    public SemanticVersioning.Version Version { get; init; } = new("3.0.0");
    public SemanticVersioning.Range SptVersion { get; init; } = new("~4.1.0");
    
    public List<string>? Incompatibilities { get; init; }
    public Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; }
    public string? Url { get; init; } = "https://github.com/TerribleTurtle/DynamicKeyLoot";
    public bool HasPrepatcher { get; init; }
    public string License { get; init; } = "MIT";
    public List<string>? Contributors { get; init; }
}

/// <summary>
/// SPT startup entrypoint that runs loot injection, price scaling, and container grid resizing.
/// </summary>
[Injectable(TypePriority = OnLoadOrder.Routers + 2)]
public class DynamicKeyLootPlugin(
    DynamicKeyLootConfigLoader configLoader,
    LootInjectionService lootInjectionService,
    ItemPriceService itemPriceService,
    ContainerGridService containerGridService,
    ISptLogger<DynamicKeyLootPlugin> logger) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!configLoader.Config.Enabled)
            {
                logger.Warning("[DynamicKeyLoot] Mod is disabled in config.jsonc (enabled: false).");
                return Task.CompletedTask;
            }

            logger.Success($"[DynamicKeyLoot] Initialized v{new ModMetadata().Version} (preset: {configLoader.Config.Preset}, reduceCommonKeys: {configLoader.Config.ReduceCommonKeys}).");

            lootInjectionService.InjectKeysIntoLocations();
            itemPriceService.AdjustPrices();
            containerGridService.AdjustGridSizes();
        }
        catch (Exception ex)
        {
            logger.Error($"[DynamicKeyLoot] Unhandled error during initialization: {ex}");
        }
        
        return Task.CompletedTask;
    }
}
