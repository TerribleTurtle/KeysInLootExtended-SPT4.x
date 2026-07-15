using System.Collections.Generic;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Utils;
using MongoDB.Bson;

namespace KeysInLootExtended;

[Injectable(InjectionType.Singleton)]
public class InjectedKeysService
{
    public HashSet<MongoId> InjectedKeyIds { get; } = new HashSet<MongoId>();
}
