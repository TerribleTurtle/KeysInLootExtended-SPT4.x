# Changelog

## [2.1.2]

### Features
* **New Preset: "Generous" (The Best of Both Worlds)**: Added a new `Generous` profile at position 3, providing a hybrid of Bountiful's volume and Refined's quality skew.
* **Profile Renumbering**: All numeric profiles have been shifted to accommodate the new Generous profile. (Balanced=1, Bountiful=2, Generous=3, Refined=4, Hardcore=5, MusicManiac=6, Piñata=7, Custom=8, Disabled=9).

### Bug Fixes
* **Location Scaling Fix**: Fixed a silent bug where `config.ActiveProfile` wasn't being reassigned after normalization. This caused all users on numeric profiles (e.g., `"1"`) to have their map-specific location scaling silently skipped, resulting in raw unscaled weights instead of the profile's intended math.
* **Disabled Profile Fix**: Fixed a casing regression where the `Disabled` profile check failed, causing the mod to run using Custom values instead of properly disabling itself.
* **Overflow Normalization Fix**: Fixed the container safe-ceiling normalization logic to explicitly preserve `0`-weight items, preventing disabled vanilla items from being accidentally revived into the loot pool.
* **Custom Profile Null Safety**: Added fallback safety for the `Custom` profile to prevent a server crash (`NullReferenceException`) when a user explicitly set properties like `keyWeight` to `null` in `config.jsonc`.
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
