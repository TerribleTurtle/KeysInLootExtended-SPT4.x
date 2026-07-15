using System.Collections.Generic;
using SPTarkov.DI.Annotations;

namespace KeysInLootExtended;

[Injectable(InjectionType.Singleton)]
public class InjectedKeysService
{
    public HashSet<string> InjectedKeyIds { get; } = new();
}
