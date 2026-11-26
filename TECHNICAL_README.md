# KeysInLootExtended - Technical Mechanics & Architecture

This document is intended for mod developers and advanced users who want to understand the underlying mathematics, the server limitations, and the architectural differences between this extended version and the original mod.

## 🏗️ Architectural Differences from the Original Mod

The original `MusicManiac-KeysInLoot` was a fantastic proof of concept, but it was built for an older version of SPT-AKI. `KeysInLootExtended` (v2.0.0+) is a complete rewrite.

1. **Migration to Native C# (.NET 10):** The original mod was written in TypeScript/JavaScript and ran on the Node.js backend. This version is a native C# Server Mod designed specifically for the SPT / SP-Tushonka 4.1+ (.NET 10) architecture.
2. **Performance Optimizations:** The original mod used array filtering to adjust prices, which scales poorly. This version uses pre-computed `HashSet<T>` lookups for O(1) performance during the price injection loop, drastically reducing server boot time.
3. **Dynamic Map & Variant Container Discovery:** The original mod hardcoded a list of 12 maps (e.g., `tables.locations.bigmap`) and 3 container IDs. This version queries SPT 4.1's `LocationTable.GetDictionary()`, registers `LazyLoad<T>` static loot transformers across all discovered locations, and maps logical container groups (`ContainerTargets`) so map-specific container variants (such as Labyrinth's dedicated Jacket and Dead Scav templates) receive both loot injection and grid/`MaxCount` expansion.
4. **Structural Item Hygiene & Strict Error Handling:** Includes strict type-checking, `MongoId` parsing protection, null-coalescing, and an `O(1)` Handbook tradability check (`Price > 1` RUB) that automatically excludes unfinished placeholder keys and 1-rouble in-raid puzzle props alongside `QuestItem`, abstract `Node`, and SPT blacklist filters.

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
3. **Grid Max Size & Non-Shrinking Expansion:** When `overrideLootDistribution` is enabled, the mod clamps `cellsH` and `cellsV` to a maximum of 14x14 (values higher than this cause the Escape from Tarkov client UI to fail to render the grid) and expands dimensions using `Math.Max(existing, target)` so smaller containers (like 2x2 Jackets) grow without shrinking larger vanilla containers (like 3x4 Duffle Bags).

## 🛠️ Building, Testing & Releasing

If you wish to compile or test the mod from source:
1. Ensure you have the **.NET 10 SDK** installed.
2. **Build (`Release`):**
   ```powershell
   dotnet build KeysInLootExtended/KeysInLootExtended.csproj -c Release
   ```
   The compiled mod and configuration files are automatically placed in `dist/SPT/user/mods/KeysInLootExtended/`.
3. **Run Unit, Golden-Fixture Simulation & End-to-End Tests (`< 2s`, CI-safe):**
   ```powershell
   dotnet test KeysInLootExtended.Tests/KeysInLootExtended.Tests.csproj -c Release
   ```
   This runs both unit tests and deterministic in-memory simulations (analytical closed-form math + seeded Monte Carlo container openings using SPT's `ContainerExtensions` 2D slot placement) against a compact SP-Tushonka 4.1.6 golden fixture—no local game installation required.
4. **Run Full Live-Database & Real SPT Raid Generator Parity Verification (Optional):**
   Point `SPT_DATA_PATH` at any local SPT `4.1.x` `SPT_Data` directory (read-only) to verify against all live items and simulate 590 full raids inside SPT's compiled `LocationLootGenerator`:
   ```powershell
   $env:SPT_DATA_PATH = "C:\Path\To\SPT\SPT_Runtime\SPT_Data"
   dotnet test KeysInLootExtended.Tests/KeysInLootExtended.Tests.csproj -c Release --logger "console;verbosity=detailed"
   ```
   *(Tip: On a live server with other loot mods installed, set `"consoleVerbosity": "debug"` in `config.jsonc` to log evaluated per-map container key weight shares when raids load.)*
5. **Automated Releases (`CI & Auto-Release`):**
   - Pushing to `main` automatically runs the `.NET 10` build and test suite in GitHub Actions (`.github/workflows/ci-release.yml`).
   - Whenever `<Version>` in `KeysInLootExtended/KeysInLootExtended.csproj` (and `ModMetadata.Version` in `KeysInLootExtendedPlugin.cs`) is bumped to a new version not yet published on GitHub Releases, the workflow automatically executes `.\release.ps1` to package `KeysInLootExtended-<Version>.zip`, extract the matching `## [<Version>]` notes from `CHANGELOG.md`, and publish the GitHub Release.
