using System.Collections.Generic;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;

namespace KeysInLootExtended;

/// <summary>
/// Shared cache of valid key and keycard template IDs discovered during loot injection
/// for reuse by price scaling and Fence blacklist updates.
/// </summary>
[Injectable(InjectionType.Singleton)]
public class InjectedKeysService
{
    public HashSet<MongoId> InjectedKeyIds { get; } = new HashSet<MongoId>();
}
