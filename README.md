# TerribleTurtle - Keys In Loot Extended (2.0.0)

> **A heartfelt thanks to MusicManiac, the creator of the original "Keys In Loot" mod, for pioneering the foundational concept and mechanics that made this extended version possible.**

This mod allows every single key in Escape from Tarkov to spawn inside standard loot containers (like Jackets and Duffle Bags) instead of being locked exclusively behind bosses and specific map spawns.

## 2.0.0 Architectural Update
KeysInLootExtended has been rewritten as a native C# (.NET 9) Server Mod to align with the SPT 4.0+ architecture.

- **Memory Optimizations:** Utilizes `HashSet<T>` lookups to reduce Garbage Collection (GC) pressure and improve server boot times.
- **Dynamic Modded Map Support:** The mod uses C# Reflection to automatically discover and support custom maps injected into the SPT database.
- **Explicit Error Handling:** Includes strict type-checking, `MongoId` parsing protection, and null-coalescing. Misconfigured JSON variables or missing item IDs will print explicit error messages to the server console rather than failing silently.

## Features
- **Dynamic Loot Adjustment:** Automatically hooks into SPT's database to find all current keys and keycards (including new ones added in recent updates).
- **Customizable Spawn Weights:** Increases the chance of keys spawning in Jackets, Duffle Bags, and on Dead Scavs based on their rarity (Common, Rare, Superrare, etc.).
- **Container Overrides:** Modifies the internal probability distributions so that containers are much more likely to spawn multiple items, rather than being empty. 
- **Expanded Jackets:** Automatically expands the internal size of Jackets (defaulting to 3x3 grid) to mathematically support the increased number of items that can spawn inside them.
- **Economy Rebalance:** Because keys are now much easier to find, the mod automatically reduces their Flea Market and Trader sell prices (default: 60% reduction) to maintain economy balance.
- **Per-Map Configuration:** Allows tweaking spawn weights globally or fine-tuning them on a map-by-map basis using the `locations/` config files.

## 📦 Installation
1. Download the latest release `.zip` file.
2. Extract the contents directly into your SPT install directory, specifically the `user/mods/` folder. 
   - The final path should look like: `SPT/user/mods/KeysInLootExtended/`

## 🛠️ Building from Source
If you are a developer and want to compile the mod yourself:
1. Ensure you have the **.NET 9 SDK** installed.
2. Open a terminal in the mod directory and run `dotnet build KeysInLootExtended/KeysInLootExtended.csproj`.
3. The compiled DLLs will be automatically placed in `dist/user/mods/KeysInLootExtended/`.

## Configuration

All global settings can be tweaked in the `config.jsonc` file located in the mod folder:

- `keyWeight` & `keycardWeight`: The target spawn probability weight for keys and keycards.
- `keyFleaPricesMultiplier` & `keyTraderPricesMultiplier`: Multipliers to adjust the sell price of keys. Set to `1.0` to disable the price nerf.
- `overrideLootDistribution`: Boolean toggle to enable/disable the item density overrides below.
- `overrideLootDistributionJackets`, `overrideLootDistributionDuffleBags`, `overrideLootDistributionDeadScavs`: The probability matrices defining how many total items spawn in those containers.
- `cellsH` & `cellsV`: The physical grid size of targeted containers (default is 3x3).
- `enableLocationsConfig`: Boolean toggle to allow map-specific overrides using the `locations/` directory.
- `consoleVerbosity`: Set to `"debug"` for deeper console logging or `"info"` for standard logs.

### 🗺️ Locations Schema
If `enableLocationsConfig` is true, the mod reads JSONC files from the `locations/` folder (e.g. `customs.jsonc`).
These files follow a schema utilizing `jacketContainer`, `duffleBagContainer`, and `deadScavContainer`, each containing an inner `key` and `keycard` rarity weights object. See the provided defaults for examples.

> [!WARNING]
> If enabled, map-specific files in the `locations/` folder will OVERRIDE your global active profile weights for any map they are defined for.

## ⚖️ Understanding Loot Weights & Rarity

Keys In Loot allows you to inject missing keys into the loot pool, but tuning their spawn chances can be tricky. Here is how SPT's underlying math works, decoded for your convenience.

### The "Lottery Ticket" System
SPT loot generation uses a system called `relativeProbability`. Imagine every loot container (like a Jacket) is a hat filled with lottery tickets. A standard Jacket on Customs contains roughly **200,000 tickets** representing all possible items that can spawn there.
- Common junk (like matches or bolts) might have ~5,000 tickets.
- An ultra-rare key might only have ~100 tickets.

When you inject keys using this mod's config, you are adding *new tickets* to the hat.

### 🎮 Experience Profiles

## 🛠️ Profiles (Map-Tuned Multipliers)

The easiest way to change the vibe of this mod is by using profiles. 
By default, this mod uses mathematically precise **per-map tuning** to ensure keys spawn at exactly the same frequency on Customs as they do on Woods, despite differing container counts. When you select a profile, it dynamically scales these map-specific baselines to perfectly achieve your desired experience.

- 🟢 **Balanced**: (Default. Roughly 1 key every 4 jackets. Prices are 40%)
- 🔵 **Bountiful**: (Double the keys of Balanced. Trader/Flea prices dropped to 20%)
- 🟣 **Refined**: (Massively reduces common keys, buffs rare keys to maintain overall key frequency. Prices are 25%)
- 🔴 **Hardcore Scarcity**: (Grind for keys. Roughly 1 key every 16 jackets. Vanilla key prices)
- 🟡 **The Mod Classic**: (Ignores map tuning. Flattens rarity, averages 1 key per jacket. Prices dropped to 15%)
- 🟠 **The Loot Piñata**: (Absolute chaos. 2-8 ultra-rare keys per jacket. Vanilla key prices)
- ⚪ **Custom**: (Uses the manual variable settings)
- ❌ **Disabled**: (Completely bypasses the mod and leaves game 100% vanilla)

If you want to manually tweak the individual variables, simply set `"activeProfile": "Custom"`. This will tell the mod to ignore the built-in presets and instead use the exact variables defined in your config file. *(Note: Invalid profile strings will log a warning and automatically fall back to the "Custom" profile).*

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

## ⚠️ Troubleshooting & Engine Limitations

**DO NOT set probabilities to absurdly high numbers!**
Because the SPT game engine adds up all the weights in a container to calculate the total pool size, it is bound by the standard 32-bit integer limit. 
If the total sum of all weights in a container exceeds `2,147,483,647`, it will trigger an **integer overflow** in the game engine. This will completely break the loot generator, cause the server console to spam red errors, and containers will spawn completely empty. Keep your weights reasonable (e.g. `500` to `5000`).

**No Decimals Allowed!**
Do not use decimal numbers (e.g., `0.5`) in the `overrideLootDistribution` configurations. The game engine expects strict integers, and using decimals will immediately crash the server when you attempt to load a map.
