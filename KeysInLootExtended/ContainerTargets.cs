using System.Linq;
using SPTarkov.Server.Core.Models.Common;

namespace KeysInLootExtended;

/// <summary>
/// Maps logical loot container categories to standard and map-specific EFT template IDs.
/// </summary>
internal static class ContainerTargets
{
    public static readonly MongoId[] Jackets =
    [
        new("578f8778245977358849a9b5"), // Standard Jacket (12 maps)
        new("67adf4a95247ac91530fcec7"), // Labyrinth Jacket
    ];

    public static readonly MongoId[] DuffleBags =
    [
        new("578f87a3245977356274f2cb"), // Standard Duffle Bag (12 maps)
    ];

    public static readonly MongoId[] DeadScavs =
    [
        new("5909e4b686f7747f5b744fa4"), // Standard Dead Scav (11 maps)
        new("67adf5752fc5ee84020a9940"), // Labyrinth Dead Scav
    ];

    public static readonly MongoId[] All = Jackets
        .Concat(DuffleBags)
        .Concat(DeadScavs)
        .ToArray();
}
