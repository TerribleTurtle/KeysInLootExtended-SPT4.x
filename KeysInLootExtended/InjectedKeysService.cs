using System.Collections.Generic;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Models.Common;

namespace KeysInLootExtended;

/// <summary>
/// A shared singleton service that stores the O(1) HashSet of injected keys.
/// Used to pass key IDs from the Loot Injection phase to the Economy phase without heavy array iterations.
/// </summary>
[Injectable(InjectionType.Singleton)]
public class InjectedKeysService
{
    /// <summary>
    /// A fast-lookup set of all MongoIds belonging to valid Keys and Keycards discovered during initialization.
    /// This state is populated synchronously during the early server startup phase and acts as a write-once cache.
    /// </summary>
    public HashSet<MongoId> InjectedKeyIds { get; } = new HashSet<MongoId>();
}
