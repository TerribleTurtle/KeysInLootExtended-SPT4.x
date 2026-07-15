using System;
using System.Collections.Generic;
using System.Linq;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Server;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;

namespace KeysInLootExtended;

/// <summary>
/// Service responsible for adjusting the internal grid dimensions (CellsH, CellsV) 
/// of specific loot containers (Jackets, Duffle Bags, Dead Scavs).
/// </summary>
[Injectable(InjectionType.Singleton)]
public class ContainerGridService
{
    private readonly ISptLogger<ContainerGridService> _logger;
    private readonly DatabaseServer _databaseServer;
    private readonly KeysInLootConfigLoader _configLoader;

    private static readonly MongoId[] TargetContainerIds = new[]
    {
        new MongoId("578f8778245977358849a9b5"), // Jacket
        new MongoId("578f87a3245977356274f2cb"), // Duffle Bag
        new MongoId("5909e4b686f7747f5b744fa4")  // Dead Scav
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="ContainerGridService"/> class.
    /// </summary>
    /// <param name="logger">The SPT logger.</param>
    /// <param name="databaseServer">The SPT database server.</param>
    /// <param name="configLoader">The configuration loader containing target grid sizes.</param>
    public ContainerGridService(
        ISptLogger<ContainerGridService> logger,
        DatabaseServer databaseServer,
        KeysInLootConfigLoader configLoader)
    {
        _logger = logger;
        _databaseServer = databaseServer;
        _configLoader = configLoader;
    }

    /// <summary>
    /// Main entry point to adjust grid sizes based on loaded configurations.
    /// Safely exits early if the profile is set to "Disabled".
    /// </summary>
    /// <remarks>
    /// Escape from Tarkov UI has a hard limit where grids > 14x14 will fail to render 
    /// properly or cause client issues, so we strictly clamp the dimensions.
    /// </remarks>
    public void AdjustGridSizes()
    {
        var config = _configLoader.Config;
        if (config.ActiveProfile == "Disabled")
            return;

        int clampedH = Math.Clamp(config.CellsH, 1, 14);
        int clampedV = Math.Clamp(config.CellsV, 1, 14);

        var items = _databaseServer.GetTables().Templates.Items;

        AdjustGridSizesInternal(items, clampedH, clampedV);

        _logger.Success($"[KeysInLootExtended] Adjusted Jacket, Duffle Bag, and Dead Scav grid sizes to {clampedH}x{clampedV}.");
    }

    /// <summary>
    /// Internal logic for overriding grid properties, separated for testability.
    /// </summary>
    /// <param name="items">The dictionary of all item templates.</param>
    /// <param name="targetCellsH">The clamped horizontal cell count.</param>
    /// <param name="targetCellsV">The clamped vertical cell count.</param>
    /// <exception cref="InvalidOperationException">Thrown when a target container is missing required Properties or Grids.</exception>
    public static void AdjustGridSizesInternal(Dictionary<MongoId, TemplateItem> items, int targetCellsH, int targetCellsV)
    {
        foreach (var containerId in TargetContainerIds)
        {
            if (items.TryGetValue(containerId, out var templateItem))
            {
                // Enforce proper "Fail Loudly" by preventing raw NREs and throwing explicitly
                var grids = templateItem.Properties?.Grids;
                if (grids == null)
                {
                    throw new InvalidOperationException($"[KeysInLootExtended] Critical Error: TemplateItem {containerId} is missing Properties or Grids. The SPT database schema may have changed.");
                }

                // We modify only the primary grid (FirstOrDefault). Secondary grids (if they exist)
                // are usually intended for nested/secure slots which we do not want to arbitrarily expand.
                var grid = grids.FirstOrDefault();
                if (grid?.Properties == null)
                {
                    throw new InvalidOperationException($"[KeysInLootExtended] Critical Error: Grid or Grid.Properties is null for TemplateItem {containerId}.");
                }
                
                grid.Properties.CellsH = targetCellsH;
                grid.Properties.CellsV = targetCellsV;
            }
        }
    }
}
