using System.Collections.Generic;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Models.Common;

namespace KeysInLootExtended;

[Injectable(InjectionType.Singleton)]
public class InjectedKeysService
{
    public HashSet<MongoId> InjectedKeyIds { get; } = new HashSet<MongoId>();
}
