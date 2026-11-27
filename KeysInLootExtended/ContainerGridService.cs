using System;
using System.Collections.Generic;
using System.Linq;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace KeysInLootExtended;

/// <summary>
/// Expands Jacket, Duffle Bag, and Dead Scav container grids to configured minimums.
/// </summary>
[Injectable(InjectionType.Singleton)]
public class ContainerGridService
{
    private readonly ISptLogger<ContainerGridService> _logger;
    private readonly TemplateTable _templateTable;
    private readonly KeysInLootConfigLoader _configLoader;

    public ContainerGridService(
        ISptLogger<ContainerGridService> logger,
        TemplateTable templateTable,
        KeysInLootConfigLoader configLoader)
    {
        _logger = logger;
        _templateTable = templateTable;
        _configLoader = configLoader;
    }

    public void AdjustGridSizes()
    {
        var config = _configLoader.Config;
        if (!config.Enabled)
            return;

        var resolved = config.Resolved;
        if (!resolved.OverrideLootDistribution)
            return;

        // EFT's container UI breaks above 14x14, so dimensions are clamped to [1, 14].
        int clampedH = Math.Clamp(resolved.MinCellsH, 1, 14);
        int clampedV = Math.Clamp(resolved.MinCellsV, 1, 14);

        var items = _templateTable.Items;

        AdjustGridSizesInternal(items, clampedH, clampedV);

        _logger.Success($"[KeysInLootExtended] Enforced minimum {clampedH}x{clampedV} container grid size across Jackets, Duffle Bags, and Dead Scavs.");
    }

    public static void AdjustGridSizesInternal(Dictionary<MongoId, TemplateItem> items, int targetCellsH, int targetCellsV)
    {
        int clampedH = Math.Clamp(targetCellsH, 1, 14);
        int clampedV = Math.Clamp(targetCellsV, 1, 14);

        foreach (var containerId in ContainerTargets.All)
        {
            if (items.TryGetValue(containerId, out var templateItem))
            {
                var grids = templateItem.Properties?.Grids;
                if (grids == null)
                {
                    throw new InvalidOperationException($"[KeysInLootExtended] Critical Error: TemplateItem {containerId} is missing Properties or Grids. The SPT database schema may have changed.");
                }

                // Only resize the primary grid; secondary grids can hold special or nested slots.
                var grid = grids.FirstOrDefault();
                if (grid?.Properties == null)
                {
                    throw new InvalidOperationException($"[KeysInLootExtended] Critical Error: Grid or Grid.Properties is null for TemplateItem {containerId}.");
                }
                
                int newH = Math.Max(grid.Properties.CellsH ?? 0, clampedH);
                int newV = Math.Max(grid.Properties.CellsV ?? 0, clampedV);
                grid.Properties.CellsH = newH;
                grid.Properties.CellsV = newV;

                // EFT caps spawned items per grid via MaxCount (e.g. 5 on Dead Scavs), so raise it to match the resized grid.
                grid.Properties.MaxCount = newH * newV;
            }
        }
    }
}
