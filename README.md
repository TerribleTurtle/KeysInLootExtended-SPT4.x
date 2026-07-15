# TerribleTurtle - Keys In Loot Extended (2.0.0)

> **A heartfelt thanks to MusicManiac, the creator of the original "Keys In Loot" mod, for pioneering the foundational concept and mechanics that made this extended version possible.**

## Overview

In standard Escape from Tarkov, many keys and keycards are locked exclusively behind specific bosses or rare map spawns. **Keys In Loot Extended** changes this by allowing *every single key* in the game to spawn naturally inside standard loot containers, such as Jackets, Duffle Bags, and on Dead Scavs.

To keep the game balanced and prevent the economy from breaking, the mod introduces three core mechanics:
1. **Rarity Scaling**: Common dorm keys will spawn frequently, while high-tier loot like a Red Keycard remains incredibly rare.
2. **Container Expansion**: Jackets are physically expanded on the inside (default 3x3) to make room for the additional loot.
3. **Price Balancing**: Since you will be finding more keys overall, the mod automatically reduces their Flea Market and Trader sell prices to maintain a stable economy.

---

## 🎮 Experience Profiles

The easiest way to customize your experience is by selecting an `activeProfile` in the `config.jsonc` file. The mod includes several tailored profiles so you can choose the exact vibe you want:

| Profile | Description | Key Spawn Chance | Avg. Key Value |
|---|---|---|---|
| 🟢 **Balanced** | **The Default.** Feels like vanilla Tarkov, but you actually find keys. | ~15% (1 in 7 boxes) | ~3,900 ₽ |
| 🔵 **Bountiful** | **Loot Explosion.** Keys are found constantly. Sell prices are heavily slashed to keep the economy intact. | ~26% (1 in 4 boxes) | ~3,400 ₽ |
| 🟣 **Refined** | **Quality over Quantity.** Fewer junk keys and a higher chance for rare keys. Highly balanced economy. | ~9% (1 in 11 boxes) | ~3,740 ₽ |
| 🔴 **Hardcore** | **The Grind.** Keys spawn slightly less often than vanilla, but the loot pool includes ALL rare keys. Vanilla sell prices are restored. | ~5% (1 in 20 boxes) | ~2,600 ₽ |
| ❌ **Vanilla** | **Disabled.** Only common junk keys spawn. High-tier keys NEVER spawn in standard containers. | ~6% (1 in 16 boxes) | N/A |

> *Note: Additional profiles like `The Mod Classic` and `The Loot Piñata` are also included for more chaotic or specialized runs.*

---

## 📦 Installation

1. Download the latest release `.zip` file.
2. Extract the contents directly into your SPT installation directory, specifically into the `user/mods/` folder.
   - The final path should look like: `SPT/user/mods/KeysInLootExtended/`

---

## ⚙️ Configuration

All global settings can be adjusted in the `config.jsonc` file located in the mod folder:

- `activeProfile`: Selects the overarching spawn and economy profile (see Experience Profiles above).
- `keyWeight` / `keycardWeight`: Target spawn probability weights for keys and keycards.
- `keyFleaPricesMultiplier` / `keyTraderPricesMultiplier`: Multipliers for key sell prices. Set to `1.0` to disable the price reduction.
- `overrideLootDistribution`: Toggles the item density overrides for containers.
- `cellsH` / `cellsV`: Sets the physical grid size of targeted containers (default is 3x3).
- `enableLocationsConfig`: Toggles map-specific overrides using the `locations/` directory.

### 🗺️ Map-Specific Tweaks
If `enableLocationsConfig` is set to `true`, the mod will read configuration files from the `locations/` folder (e.g., `customs.jsonc`). This allows you to set custom rarity weights per map.

> [!WARNING]
> Map-specific files in the `locations/` folder will OVERRIDE your global profile weights for any map they are defined for.

---

## 👨‍💻 For Developers & Advanced Users

If you are a developer looking to build the mod from source, or an advanced user interested in the underlying mathematical weights, integer limits, and the new C# architecture, please refer to the [TECHNICAL_README.md](TECHNICAL_README.md) file included in this repository.
