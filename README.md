# Keys In Loot Extended

> **For SP-Tushonka / SPT 4.1.x** (on SPT 4.0.x? Use [v2.1.4](https://github.com/TerribleTurtle/KeysInLootExtended-SPT4.x/releases/tag/2.1.4)) · Credit to [MusicManiac](https://github.com/MusicManiac/KeysInLoot) for the original *Keys In Loot* concept

## Why Use This Mod?

In vanilla Tarkov, nearly a third of all keys and keycards (including every Labs/Labrys keycard and several boss and quest keys) have a **0% chance** to ever spawn inside containers, and the rest are tuned around thousands of live players feeding the Flea Market.

**Keys In Loot Extended** puts every real key and keycard into **Jackets, Duffle Bags, and Dead Scavs** across all 13 maps (including Labyrinth), tuned around solo SPT playtimes—while scaling down key sell prices so finding keys unlocks doors instead of printing free millions.

## Install

1. Download `KeysInLootExtended-3.0.0.zip` from [Releases](https://github.com/TerribleTurtle/KeysInLootExtended-SPT4.x/releases).
2. Extract it straight into your SPT root folder (`<SPT_Root>/user/mods/KeysInLootExtended/`).

*Upgrading from 2.x?* Delete your old `KeysInLootExtended` folder first. If a mod manager keeps your old `config.jsonc`, the mod backs it up to `config.backup.jsonc` and starts fresh on **Balanced** (set `"enabled": false` in the new file if you previously had the mod disabled).

## Pick a Preset

Open `config.jsonc` and set `"preset"` right at the top:

| Mode | Who It's For | Typical 2-Hour Session (~3–4 Raids) | Key Sell Value |
| :--- | :--- | :--- | :---: |
| *Vanilla (Mod Off)* | Base Tarkov reference (`"enabled": false`) | ~1–2 common keys (67 keys/keycards never spawn) | 100% |
| **Hardcore** | Zero-to-hero, No-Flea, long wipes | ~2 keys (all 217 unlocked; ~1 SuperRare a week) | 100% |
| **Balanced** *(Default)* | Normal solo SPT playthrough | ~6–7 keys (~1 SuperRare room per session) | 40% |
| **Casual** | Busy schedule / a couple hours after work | ~16 keys (~3 SuperRare rooms per session) | 25% |
| **Custom** | Full manual control | Uses the `"custom"` block at the bottom of `config.jsonc` | Custom |

- **Normal container loot stays intact:** Checking Jackets and Duffles still gives normal meds, food, and hideout barter parts instead of starving or flooding containers.
- **Getting spammed with duplicate Dorms keys mid-wipe?** Flip `"reduceCommonKeys": true` in `config.jsonc` to cut common filler keys by 85% on *any* preset, so ~90% of keys you pull are Rare or SuperRare rooms.

## Quick Tweaks (`config.jsonc`)

All everyday options sit at the top of `config.jsonc` and work on **any** preset (no need to switch to `Custom`):

| Setting | Default | What It Does |
| :--- | :---: | :--- |
| `enabled` | `true` | Turns the mod on or off without uninstalling. |
| `preset` | `"Balanced"` | `"Hardcore"`, `"Balanced"`, `"Casual"`, or `"Custom"`. |
| `reduceCommonKeys` | `false` | Cuts common filler key drops by 85% once you already own early quest/Dorms keys. |
| `spawnWeightMultiplier` | `1.0` | Master drop-rate dial (`1.5` = 50% more keys, `0.5` = half as many). |
| `fleaPriceMultiplier` | `null` | Leave `null` for preset default, or set e.g. `1.0` for full vanilla Flea key prices. |
| `traderPriceMultiplier` | `null` | Leave `null` for preset default, or set e.g. `1.0` for full vanilla Trader key prices. |
| `banKeysFromFence` | `false` | Set `true` to stop simulated PMCs from selling found keys to Fence's shop. |

---

## Reference & Developer Details

<details>
<summary><strong>Advanced & Custom Settings (per-map files, debug logs, and the "custom" block)</strong></summary>

- `enableLocationsConfig` (`true`): Scales per-map baselines in `locations/*.jsonc` by your active preset so small pools (Labyrinth, Ground Zero) and large pools (Customs, Streets) drop keys at the same rate (~25 keys per 100 Jackets on Balanced).
- `debugLogs` (`false`): Logs evaluated per-map container key pool percentages to the server console when raids load.
- `"custom"` block (used only when `"preset": "Custom"`):
  - `keyWeight` / `keycardWeight`: Integer pool weights (`notExist`, `common`, `rare`, `superRare`). Set any tier to `0` to block that tier completely.
  - `fleaPriceMultiplier` / `traderPriceMultiplier`: Sell-price multipliers (`0.0` = 0 RUB; positive values clamp to at least 1 RUB).
  - `overrideLootDistribution` & `jacketsItemCounts` / `duffleBagsItemCounts` / `deadScavsItemCounts`: Custom integer item-count curves (`null` keeps vanilla counts on that container).
  - `minCellsH` / `minCellsV`: Minimum container grid size from 1 to 14 (expands 2x2 Jackets without shrinking 4x3 Duffles or 4x4 Dead Scavs).

</details>

<details>
<summary><strong>Exact Drop Rates, Pool Breakdown & Boss Key Mappings</strong></summary>

The mod filters Tarkov's item database down to **217 tradable keys and keycards** (47 Common keys, 75 Rare keys, 82 SuperRare keys, and 13 SuperRare keycards), automatically excluding quest-only items, blacklisted developer items, 7 unfinished Streets placeholder keys, and 2 1-rouble Labyrinth puzzle items.

| Mode | Keys per 100 Jackets (Common / Rare / SuperRare) | Avg Time: Rare+ / SuperRare / Keycard | Unique Keys Found (12h / 40h) |
| :--- | :---: | :---: | :---: |
| **Vanilla** (`enabled: false`) | 13.2 Customs / 5.8 map avg (67 keys = 0%) | ~8–12h / ~35h+ / Never (0%) | Caps below 150 / 217 |
| **Hardcore** | 7.1 (3.6 / 2.5 / 1.0) | ~2.2h / ~7.2h / ~83h | 11 / 33 of 217 |
| **Balanced** | 25.4 (11.7 / 9.3 / 4.4) | ~35m / ~1.8h / ~24h | 35 / 90 of 217 |
| **Casual** | 67.0 (26.4 / 26.6 / 14.1) | ~12m / ~36m / ~7.8h | 74 / 152 of 217 |

*(With `reduceCommonKeys: true`, Jackets average 4.2 keys/100 on Hardcore, 16.7 on Balanced, and 49.5 on Casual, with 86%–91% Rare and SuperRare drops.)*

**21 Uncategorized (`Not_exist`) Keys Mapped to Proper Tiers:**
- **Common (5):** Pumping station front door key, Pumping station back door key, Unknown key, Backup hideout key, Horse restaurant toilet key.
- **Rare (7):** Cold storage room key, Aspect company office key, Unity Credit Bank archive room key, USEC cottage room key, Observation room key, Torture room key, Labyrinth key.
- **Superrare (9):** Shturman's stash key, Keycard with a blue marking, Car dealership closed section key, TerraGroup storage room keycard, Labrys access keycard, Shatun's hideout key, Grumpy's hideout key, Voron's hideout key, Leon's hideout key.

</details>

<details>
<summary><strong>Architecture, Building & Testing (.NET 10)</strong></summary>

- **SPT 4.1 Pipeline:** Loads at `OnLoadOrder.Routers + 2` (`400002`) before startup handbook/Flea/Fence caches initialize, registers `Location.StaticLoot.AddTransformer(...)` callbacks across all discovered maps (including Labyrinth's dedicated Jacket and Dead Scav templates), normalizes pools exceeding `int.MaxValue / 2`, and clamps grids to EFT's 14x14 UI limit.
- **Build (`Release`):**
  ```powershell
  dotnet build .\KeysInLootExtended\KeysInLootExtended.csproj -c Release
  ```
- **Run Unit & Golden-Fixture Simulation Tests (`< 1s`, no SPT install required):**
  ```powershell
  dotnet test .\KeysInLootExtended.Tests\KeysInLootExtended.Tests.csproj -c Release
  ```
- **Run Optional Live-Database & 590-Raid `LocationLootGenerator` Parity Check (Read-Only):**
  ```powershell
  $env:SPT_DATA_PATH = "C:\Path\To\SPT\SPT_Runtime\SPT_Data"
  dotnet test .\KeysInLootExtended.Tests\KeysInLootExtended.Tests.csproj -c Release --logger "console;verbosity=detailed"
  ```
- **Release Packaging:** `.\release.ps1 -PackageOnly` tests, builds, and verifies `KeysInLootExtended-<Version>.zip` locally; pushing a matching version tag (`3.0.0` / `v3.0.0`) or running `workflow_dispatch` on `main` publishes via `.github/workflows/ci-release.yml`.

</details>
