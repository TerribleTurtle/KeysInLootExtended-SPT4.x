# KeysInLootExtended - Technical Mechanics & Architecture

This document is intended for mod developers and advanced users who want to understand the underlying mathematics, the server limitations, and the architectural differences between this extended version and the original mod.

## 🏗️ Architectural Differences from the Original Mod

The original `MusicManiac-KeysInLoot` was a fantastic proof of concept, but it was built for an older version of SPT-AKI. `KeysInLootExtended` (v2.0.0+) is a complete rewrite.

1. **Migration to Native C# (.NET 9):** The original mod was written in TypeScript/JavaScript and ran on the Node.js backend. This version is a native C# Server Mod designed specifically for the SPT 4.0+ architecture.
2. **Performance Optimizations:** The original mod used array filtering to adjust prices, which scales poorly. This version uses pre-computed `HashSet<T>` lookups for O(1) performance during the price injection loop, drastically reducing server boot time.
3. **Dynamic Map Discovery:** The original mod hardcoded a list of 12 maps (e.g., `tables.locations.bigmap`). This version uses C# Reflection to dynamically discover maps. This means if you install custom modded maps, `KeysInLootExtended` will automatically attempt to inject keys into their jackets without requiring an update.
4. **Strict Error Handling:** Includes strict type-checking, `MongoId` parsing protection, and null-coalescing. Misconfigured JSON variables or missing item IDs from other mods will be safely caught and logged, rather than crashing the database generation.

## ⚖️ Understanding Loot Weights & The "Lottery Ticket" System

SPT loot generation uses a system called `relativeProbability`. Imagine every loot container (like a Jacket) is a hat filled with lottery tickets. A standard Jacket on Customs contains roughly **200,000 tickets** representing all possible items that can spawn there.
- Common junk (like matches or bolts) might have ~5,000 tickets.
- An ultra-rare key might only have ~100 tickets.

When you inject keys using this mod's config, you are adding *new tickets* to the hat.

### ⚠️ The Pool Inflation Problem
In the actual vanilla game, a "Very Common" key has a raw weight of around 10,000. However, a standard vanilla jacket only has a total ticket pool of about 56,000 (mostly filled with junk items). 

Because this mod injects over **200 missing keys** simultaneously, if we used the raw vanilla weight of 10,000 for all of them, we would instantly bloat the jacket pool to over 500,000 tickets. Keys would become 90% of the entire loot pool, completely destroying the vanilla economy (you would almost never find regular junk or meds in jackets again). 

To solve this, our presets use **Scaled Target Weights**. These mathematically preserve the true rarity ratios of the keys, but shrink their footprint so they don't overpower the junk loot pool. 

### Scaled Reference Values
If you want to manually tweak the JSON variables to feel authentic to the vanilla game, use these scaled target weights:

| JSON Property | Rarity Level | Scaled Injection Weight | Native Vanilla Equivalent |
|---|---|---|---|
| **`notExist`** | Very Common | **200 - 500** | 10k - 15k |
| **`common`** | Common | **100 - 300** | 2k - 3k |
| **`rare`** | Rare | **50 - 100** | 500 - 1k |
| **`superRare`** | Ultra Rare | **10 - 40** | 10 - 50 |

### ⚠️ The Danger of Flat Weights (Rarity Flattening)
If you set the global weight config to use the same flat number for everything (e.g., setting everything to `500`), you are giving **every single missing key** exactly 500 tickets. 
This means a super rare Red Keycard and a common Dorms 206 key will both have exactly 500 tickets in the hat, completely destroying the concept of rarity in Tarkov. Always use a tiered approach!

## ⚠️ Engine Limitations

1. **Integer Overflow (`int.MaxValue / 2`):** Because the SPT game engine adds up all the weights in a container to calculate the total pool size, it is bound by standard 32-bit integer limits. If the total sum of all weights in a container exceeds `2,147,483,647`, it triggers an integer overflow. `KeysInLootExtended` has a built-in safety clamp that detects if a container's weight is approaching `int.MaxValue / 2` and scales it down automatically to prevent crashes.
2. **No Decimals Allowed:** Do not use decimal numbers (e.g., `0.5`) in the `overrideLootDistribution` configurations. The game engine expects strict integers, and using decimals will immediately crash the server when you attempt to load a map.
3. **Grid Max Size:** The mod clamps `cellsH` and `cellsV` to a maximum of 14x14. Values higher than this have been known to cause the Escape from Tarkov client UI to fail to render the container grid.

## 🛠️ Building from Source

If you wish to compile the mod yourself for development or debugging:
1. Ensure you have the **.NET 9 SDK** installed.
2. Open a terminal in the repository root and run: `dotnet build KeysInLootExtended/KeysInLootExtended.csproj -c Release`
3. The compiled DLLs will be automatically placed in the correct `dist/user/mods/KeysInLootExtended/` folder structure.
4. Alternatively, you can run the provided `.\release.ps1` script to automatically build and package the mod into a ready-to-use `.zip` archive.
