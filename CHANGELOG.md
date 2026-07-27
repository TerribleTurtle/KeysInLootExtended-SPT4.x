# Changelog

## [2.1.3]

### Features & Balancing
* **New Preset: "Generous" (The Best of Both Worlds)**: Added a new `Generous` profile at position 3, providing a hybrid of Bountiful's volume and Refined's quality skew.
* **Profile Renumbering**: All numeric profiles have been shifted to accommodate the new Generous profile. (Balanced=1, Bountiful=2, Generous=3, Refined=4, Hardcore=5, MusicManiac=6, Piñata=7, Custom=8, Disabled=9).
* **True Boss Key Rarity (`Not_exist` Mapping)**: Many boss keys (like Shturman's Stash, TerraGroup Storage, and Colored Keycards) are internally flagged as "Not_exist" in the vanilla database. Previously, this meant they defaulted to a 50/50 fallback spawn rate, making them spawn as frequently as standard dorm keys. They have now been mapped to their true logical rarity pools (`Rare`, `Superrare`), fixing the balance.
* **Dynamic Ratio Location Scaling**: Map-specific location configs (like Bigmap) now scale dynamically! Instead of a flat multiplier (like `2.0x`), the mod calculates the exact ratio of your chosen profile against the default "Balanced" baseline. This means if you use a profile like "Refined" that skews heavily toward Rare keys, that exact skew is mathematically applied to the map's native loot density, preserving map uniqueness while perfectly inheriting your custom rarity balance.

### Bug Fixes
* **The "Never-Nerf" Fix**: Removed a bug in the injection engine (`Math.Max`) that prevented profiles from reducing key weights below vanilla rates. "Hardcore Scarcity" and "Refined" can now correctly reduce the spawn rates of common trash keys.
* **Location Scaling Fix (ActiveProfile Desync)**: Fixed a silent bug where `config.ActiveProfile` wasn't being reassigned after numeric normalization. This caused all users on numeric shorthand profiles (e.g., `"1"`) to have their map-specific location scaling silently skipped.
* **Disabled Profile Fix**: Fixed a casing regression where the `Disabled` profile check failed, causing the mod to run using Custom values instead of properly disabling itself.
* **Overflow Normalization Fix**: Fixed the container safe-ceiling normalization logic to explicitly preserve `0`-weight items, preventing disabled vanilla items from being accidentally revived into the loot pool.
* **Vanilla Duplicate Multiplication**: Fixed a bug where keys that had multiple identical entries in the vanilla map table would receive exponential weight multipliers.
* **The Case-Sensitive Switch Exploit**: C# `switch` statements are case-sensitive by default. Fixed a bug where anomalous casing from base game updates (e.g. `"rare"`) would accidentally fall through to the `Not_exist` pool and receive 0 spawn weight. The hot loop now correctly sanitizes strings using `.ToLowerInvariant()`.
* **Infinite Loop Hazard**: Found and patched a severe crash hazard where poorly written custom maps with circular `Parent` item inheritance would throw the mod into a permanent `while` loop on startup, permanently freezing the SPT server.
* **Dynamic Map Crash Hazard**: Wrapped several dynamic JSON evaluations (e.g., `location.StaticLoot`) in `try/catch` blocks. Previously, if a custom map omitted these fields, it would instantly crash the mod with an unhandled `RuntimeBinderException`.
* **Unknown Profile Fallback Leak**: Re-ordered the custom null-safety checks in `ConfigLoaderService`. Previously, a typo in the config file (e.g., `"balaced"`) would correctly trigger the fallback to `"Custom"`, but incorrectly bypass the null array initializations, causing a silent internal failure.
* **Custom Profile Null Safety**: Added fallback safety (`??=`) for the `Custom` profile to prevent a server crash (`NullReferenceException`) when a user explicitly set properties like `keyWeight` to `null` in `config.jsonc`.
* **Zero-Division & Truncation Safety**: Added robust math clamping to the new Dynamic Ratio engine to prevent integer division truncation and fatal zero-division errors.
## [2.0.1]
* **Hotfix:** Restored the missing `banKeysFromFence` setting to the default `config.jsonc` file.

## [2.0.0]

Welcome to the 2.0.0 release of **KeysInLootExtended**! 

🟢 **[FULLY SPT 4.x COMPATIBLE]** This mod has been completely rebuilt from the ground up to support the modern SPT 4.x ecosystem. 

In standard Escape from Tarkov, many keys and keycards are locked exclusively behind specific bosses or rare map spawns. This mod changes that by allowing *every single key* in the game to spawn naturally inside standard loot containers (Jackets, Duffle Bags, and Dead Scavs).

### Features
* **Every Key Can Spawn:** No more grinding bosses endlessly. Even the rarest keycards now have a tiny chance to spawn in ordinary containers across all maps.
* **Rarity Scaling:** Common dorm keys will spawn frequently, while high-tier loot remains incredibly rare to preserve the game's balance.
* **Container Expansion:** Jackets, Duffle Bags, and Dead Scavs are physically expanded on the inside (to a 3x3 grid by default) to make room for the additional loot without crowding out normal items.
* **Economy Balancing:** Finding more keys shouldn't mean infinite money. The mod automatically scales down Flea Market and Trader sell prices for keys to maintain a stable, realistic economy.
* **Experience Profiles:** Easily customize your gameplay by selecting a profile in the configuration file, ranging from the brutal "Hardcore Scarcity" to an absolute "Bountiful" loot explosion.
* **Fence Economy Management:** Because simulated PMCs will find these valuable keys more often, they will flood Fence's inventory. You can use the `banKeysFromFence` toggle in the configuration to permanently block these keys from Fence's assort.

### Installation
1. Delete any older `KeysInLootExtended` mod folders from your `user/mods/` directory to prevent conflicts.
2. Download `KeysInLootExtended-2.0.0.zip` below.
3. Extract the zip file directly into your SPT `user/mods/` directory.

*A huge thank you to [MusicManiac](https://github.com/MusicManiac/KeysInLoot), the creator of the original "Keys In Loot" mod for the old SPT AKI 3.x system, for pioneering the foundational concept!*
