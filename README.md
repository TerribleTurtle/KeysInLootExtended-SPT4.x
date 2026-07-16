# TerribleTurtle - Keys In Loot Extended (SPT 4.x Compatible)

> 🟢 **[FULLY SPT 4.x COMPATIBLE]** This mod has been completely rebuilt from the ground up for the modern SPT 4.x ecosystem.
> 
> *A heartfelt thanks to [MusicManiac](https://github.com/MusicManiac/KeysInLoot), the creator of the original "Keys In Loot" mod for the old SPT AKI 3.x system, for pioneering the foundational concept and mechanics that made this extended version possible.*

## Overview

In standard Escape from Tarkov, many keys and keycards are locked exclusively behind specific bosses or rare map spawns. **Keys In Loot Extended** changes this by allowing *every single key* in the game to spawn naturally inside standard loot containers, such as Jackets, Duffle Bags, and on Dead Scavs.

To keep the game balanced and prevent the economy from breaking, the mod introduces three core mechanics:
1. **Rarity Scaling**: Common dorm keys will spawn frequently, while high-tier loot like a Red Keycard remains incredibly rare.
2. **Container Expansion**: Jackets, Duffle Bags, and Dead Scavs are physically expanded on the inside (default 3x3) to make room for the additional loot.
3. **Price Balancing**: Since you will be finding more keys overall, the mod automatically reduces their Flea Market and Trader sell prices to maintain a stable economy.

---

## 🎮 Experience Profiles

The easiest way to customize your experience is by selecting an `activeProfile` in the `config.jsonc` file. The mod includes several tailored profiles so you can choose the exact vibe you want:

| Profile | Description | Keys per 100 Jackets | Market Price |
|---|---|---|---|
| 1 - 🟢 **Balanced (The Working Adult)** | **Quantity & Consistency.** Finds keys frequently to keep the dopamine flowing, though many will be common. The market crashes to simulate supply/demand. | **~15 keys** (9 Common, 5 Rare, 1 SuperRare) | **0.4x** (Crashed) |
| 2 - 🔵 **Bountiful (The Looter Shooter)** | **Loot Explosion.** Every jacket is stuffed. You find keys constantly. To compensate, market prices are completely crashed. | **~30 keys** (18 Common, 9 Rare, 3 SuperRare) | **0.2x** (Crashed) |
| 3 - 🟣 **Refined (The Anti-Trash)** | **Quality over Quantity.** You don't find keys often, but the junk has been purged. When you see a key, it's almost guaranteed to be a banger. | **~10 keys** (1 Common, 8 Rare, 1 SuperRare) | **1.0x** (Vanilla) |
| 4 - 🔴 **Hardcore Scarcity (The Masochist)** | **The Grind.** Brutal scarcity. You open 20 jackets and find nothing. When you find a key, you gasp. You earn the full vanilla sell price. | **~5 keys** (3 Common, 2 Rare, 0.2 SuperRare) | **1.0x** (Vanilla) |
| 8 - ❌ **Disabled (The Purist / Vanilla)** | **100% Vanilla Tarkov.** High-tier keys almost NEVER spawn in standard containers. Mostly junk. | **~8 keys** (7 Common, 1 Rare, 0 SuperRare) | **1.0x** (Vanilla) |

> *Note: Additional cheat/testing profiles like `The MusicManiac Classic` and `The Loot Piñata` are also included for more chaotic runs.*

### 📊 Item Density Curves

The profiles also enforce specific item count distributions (how many items drop per container in total, not just keys):
*   🟢 **1. Balanced (Default):** Averages 2.5 items per container. Shifts the peak to 2-3 items (43% for 2, 35% for 3).
*   🔵 **2. Bountiful:** Averages 3.5 items. Heavily favors 3-4 items (38% for 3, 40% for 4) for a much denser looting experience.
*   🟣 **3. Refined:** Vanilla Plus. Averages 1.5 items. Highly favors 1-2 items (45% for 1, 40% for 2) to reduce empty containers.
*   🔴 **4. Hardcore Scarcity:** Brutally reduced curve. Highly favors 0 or 1 items (50% for 0, 40% for 1) to make finding loot a true grind.
*   🟡 **5. The MusicManiac Classic:** Enforces a custom probability curve that evenly and heavily favors 2-4 items (30% chance for each), emulating the original mod's behavior.
*   🟠 **6. The Loot Piñata:** Pure chaos. Forces 10-25 items per container.
*   ⚪ **7. Custom:** Reverts to whatever raw probability arrays you manually define inside your `config.jsonc` file.
*   ❌ **8. Disabled:** 100% vanilla Tarkov container densities.

---

## 📦 Installation

1. Download the latest release `.zip` file.
2. Extract the contents directly into your SPT installation directory, specifically into the `user/mods/` folder.
   - The final path should look like: `SPT/user/mods/KeysInLootExtended/`

---

## ⚙️ Configuration

All global settings can be adjusted in the `config.jsonc` file located in the mod folder:

> [!WARNING]
> Selecting an active profile (other than `"Custom"`) will permanently **OVERRIDE** your manual `keyWeight`, `keycardWeight`, and `cellsH`/`cellsV` settings in memory! To use your own weights, you must set `activeProfile` to `"Custom"`.

- `activeProfile`: Selects the overarching spawn and economy profile (see Experience Profiles above).
- `keyWeight` / `keycardWeight`: Target spawn probability weights for keys and keycards.
- `keyFleaPricesMultiplier` / `keyTraderPricesMultiplier`: Multipliers for key sell prices. Set to `1.0` to disable the price reduction.
- `overrideLootDistribution`: Toggles the item density overrides for containers.
- `overrideLootDistributionJackets` / `overrideLootDistributionDuffleBags` / `overrideLootDistributionDeadScavs`: The arrays configuring the probability of how many items spawn in the containers.
- `cellsH` / `cellsV`: Sets the physical grid size of targeted containers (default is 3x3).
- `enableLocationsConfig`: Toggles map-specific overrides using the `locations/` directory.
- `consoleVerbosity`: Controls the verbosity of log output.

### 🗺️ Map-Specific Tweaks
If `enableLocationsConfig` is set to `true`, the mod will read configuration files from the `locations/` folder (e.g., `customs.jsonc`). This allows you to set custom rarity weights per map.

> [!WARNING]
> Map-specific files in the `locations/` folder will OVERRIDE your global profile weights for any map they are defined for.

---

## 🛒 The Fence Economy
Because this mod injects highly valuable keys into common containers, simulated PMCs in SPT will find these keys much more frequently. As a side effect, they will sell them to Fence. You will see significantly more rare keys available in Fence's shop than in the vanilla game, and they will be priced according to your active economy profile.

If you prefer to find keys exclusively in-raid and want to prevent them from showing up in Fence's inventory entirely, you can set `"banKeysFromFence": true` in your `config.jsonc`.

---

## 👨‍💻 For Developers & Advanced Users

If you are a developer looking to build the mod from source, or an advanced user interested in the underlying mathematical weights, integer limits, and the new C# architecture, please refer to the [TECHNICAL_README.md](TECHNICAL_README.md) file included in this repository.
