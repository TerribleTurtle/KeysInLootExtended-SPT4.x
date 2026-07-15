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

## ⚖️ How the Mod Works (For Normal Humans)

Tarkov normally locks most keys behind specific bosses or rare spawn locations. This mod fundamentally changes that by putting *every* key into standard loot containers (Jackets, Duffle Bags, and Dead Scavs).

To prevent the game from becoming too easy or the economy from breaking, the mod does three things:
1. **Rarity Scaling**: Common dorm keys will spawn frequently, but a Red Keycard will remain incredibly rare.
2. **Container Expansion**: It makes Jackets physically larger on the inside so there is actual room for the extra keys.
3. **Price Balancing**: Because you are finding more keys, the mod automatically reduces how much they sell for on the Flea Market and to Traders. This keeps you from becoming a millionaire overnight.

### 🎮 Experience Profiles (Choose Your Vibe)

The easiest way to configure this mod is by picking an `activeProfile` in the `config.jsonc` file. We ran mathematical simulations to show exactly what you can expect from the four main profiles:

| Profile | What it feels like | Key Spawn Chance (Per Box) | Avg. Key Value (Roubles) |
|---|---|---|---|
| 🟢 **Balanced** | **The Default.** Feels like vanilla Tarkov but you actually find keys. | ~15% (1 in 7 boxes) | ~3,900 |
| 🔵 **Bountiful** | **Loot Explosion.** You find keys constantly. Sell prices are heavily slashed so it doesn't break the game. | ~26% (1 in 4 boxes) | ~3,400 |
| 🟣 **Refined** | **Quality over Quantity.** You find fewer junk keys and more rare keys. Very balanced economy. | ~9% (1 in 11 boxes) | ~3,740 |
| 🔴 **Hardcore** | **The Grind.** Keys spawn slightly less often than vanilla, but the loot pool includes ALL rare keys (like colored keycards). Vanilla sell prices are restored. | ~5% (1 in 20 boxes) | ~2,600 |
| ❌ **Vanilla (Disabled)** | **No Mod.** You only find common junk keys. High-tier keys NEVER spawn in jackets. | ~6% (1 in 16 boxes) | N/A (High-tier keys cannot spawn here) |

*Other fun profiles included: `The Mod Classic` (no map tuning, total randomness) and `The Loot Piñata` (absolute chaos, multiple ultra-rare keys per jacket).*

---

### 👨‍💻 For Developers and Advanced Users

If you want to manually tweak the integer limits, understand the underlying mathematical weights, or see what changed from the original mod's architecture, please read the [TECHNICAL_README.md](TECHNICAL_README.md) file included in this directory.
