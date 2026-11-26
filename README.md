# Keys In Loot Extended (SP-Tushonka / SPT 4.1.x)

> Compatibility: Versions `2.2.0+` and `3.0.0+` target SP-Tushonka / SPT `4.1.x` on `.NET 10` (`3.0.0+` uses the current `configVersion: 3` format). For SPT `4.0.x`, use the [`v2.1.4` release](https://github.com/TerribleTurtle/KeysInLootExtended-SPT4.x/releases/tag/2.1.4).
>
> Credit and thanks to [MusicManiac](https://github.com/MusicManiac/KeysInLoot), author of the original *Keys In Loot* mod for SPT-AKI 3.x, whose core concept inspired this extended C# rewrite.

## Overview

Keys In Loot Extended adds all `217` tradable Escape from Tarkov keys and keycards into static Jackets, Duffle Bags, and Dead Scavs across all 13 playable maps (including Labyrinth), with presets tuned around solo SPT playtimes and proportional sell-price scaling.

In vanilla Tarkov, static container rates are tuned around 50,000 live players feeding a multiplayer Flea Market, creating two walls for solo SPT players:
1. `67` of the game's `217` tradable keys and keycards (`31%`) have a `0%` chance of ever spawning in any Jacket, Duffle Bag, or Dead Scav, including all `13` keycards, `20` SuperRare boss/Arena/Streets keys, and `25` Rare keys (such as Resort West 205).
2. Even keys that do exist in vanilla Jackets are tuned for crowd farming: Dorm room 314 Marked averages `1 in 1,968` Jackets (~225 hours of solo raiding), and Cottage back door averages `1 in 326,481` Jackets.

All `217` tradable items injected by the mod break down into four groups:
- `47` Common keys: Early quest and utility doors (2-story Dorms, Portable Bunkhouse, Pumping Station, cash registers).
- `75` Rare keys: Mid-to-high loot and quest rooms (Health Resort wings, Reserve `RB-OB`/`ORB`, Streets apartments, USEC Cottage, Labyrinth rooms).
- `82` SuperRare keys: Marked Rooms (Dorms 314, `RB-BK`, `RB-VO`, Abandoned Factory, Shared Bedroom), Shturman's Stash, Kaban's LexOs dealership, and all 4 Arena boss hideouts.
- `13` SuperRare keycards: Labs Access, all 6 Colored Labs cards, Labrys Access, TerraGroup Storage, Blue Marking, `#11SR`, and `#21WS`.

---

## Installation & Upgrading from v2.x

1. Download the latest `.zip` archive matching your server version from [GitHub Releases](https://github.com/TerribleTurtle/KeysInLootExtended-SPT4.x/releases):
   - SP-Tushonka / SPT `4.1.x` (`.NET 10`): `KeysInLootExtended-3.0.0.zip` (or latest `2.2.0+` / `3.x` release)
   - SPT `4.0.x` (`.NET 9`): `KeysInLootExtended-2.1.4.zip`
2. Extract the archive into your SPT root folder so the files land at `<SPT_Root>/user/mods/KeysInLootExtended/`.
3. Upgrading from `v2.x`: `v3.0.0` introduces a clean `configVersion: 3` layout. If a mod manager preserves your older `v2.x` `config.jsonc`, the mod automatically backs it up to `config.backup.jsonc` on startup and writes a fresh `v3` `config.jsonc` (`Balanced`).

---

## Presets & Real Playtime Pacing

Set `preset` at the top of `config.jsonc` (not case-sensitive). Estimates below are verified against SP-Tushonka `4.1.6` pools assuming ~35 minutes per full SPT raid + stash cycle (~`3.5` raids in a 2-hour session, searching `5` Jackets, `4` Duffle Bags, and `1` Dead Scav per raid).

| Mode | Keys / 100 Jackets (C / R / SR) | Keys in a 2-Hr Night (`3.5` Raids) | Avg Time: `Rare+` / `SuperRare` / `Keycard` | Unique Unlocks (`12h` / `40h`) | Container Item Counts | Sell Value |
| :--- | :---: | :---: | :---: | :---: | :--- | :---: |
| Vanilla (`enabled: false`) | `13.2` Customs / `5.8` avg | `~1-3` (`67` keys = `0%`) | `~8-12h` / `~35h+` / Never (`0%`) | Caps below `150 / 217` | 100% vanilla counts & grids | `1.00x` |
| `Hardcore` | `7.1` (`3.6` / `2.5` / `1.0`) | `~1.9` keys | `~2.2h` / `~7.2h` / `~83h` | `11` / `33` of `217` | 100% vanilla counts & grids | `1.00x` |
| `Balanced` *(Default)* | `25.4` (`11.7` / `9.3` / `4.4`) | `~6.6` keys (~1 SR) | `~35m` / `~1.8h` / `~24h` | `35` / `90` of `217` | `1.32` avg Jacket; vanilla Duffle & Scav | `0.40x` |
| `Casual` | `67.0` (`26.4` / `26.6` / `14.1`) | `~15.8` keys (~3 SR) | `~12m` / `~36m` / `~7.8h` | `74` / `152` of `217` | `2.27` avg Jacket, `2.38` avg Duffle | `0.25x` |
| `Custom` | User-defined | User-defined | User-defined | User-defined | Manual `"custom"` block | User |

*(Why `Balanced` can drop ~1 SuperRare per 2-hour night without ending progression early: there are `95` different SuperRare keys and keycards in the pool. Because duplicates accumulate over time, hunting one specific Marked key still averages ~150 hours on `Balanced`.)*

### How Each Mode Feels & Why You Pick It
- Vanilla (`"enabled": false`): Leaves Tarkov untouched. Most maps drop ~1 common key every couple of raids, and a third of all keys/keycards never appear in containers. Use this only to test vanilla behavior or temporarily turn the mod off.
- `Hardcore`: Built for zero-to-hero, No-Flea, and long-wipe players (`10-20+ hrs/week`). Keeps vanilla container pacing (~1 key per hour, `100%` untouched meds/food/barter counts, full `1.00x` sell value), except all `217` keys can actually drop. Finding a Rare room (~2.2 hrs) or SuperRare key (~7.2 hrs, ~1 per week) becomes a milestone that shapes where you raid next.
- `Balanced` *(Default)*: Built for regular solo SPT players (`6-10 hrs/week`, `40-80 hr` wipe). Checking Jackets reliably pays off (~2 keys per raid, ~1 SuperRare room per 2-hour evening, ~1 Keycard every ~24 hours) without crowding out normal Jacket items (`106.6` non-key items per 100 Jackets vs `94.0` in vanilla). Sell prices scale to `0.40x` so keys are worth bringing into raid to unlock doors rather than flipping for easy millions.
- `Casual`: Built for busy adults and weekend players (`2 hours` free after work, `12-25 hr` total playthrough). Over half of Jackets hold a key, unlocking `~74` unique keys (`~18` SuperRares) in `12 hours` (matching the room variety `Balanced` reaches around `35 hours`, plus a Keycard roughly every `8 hours` so you actually get to run Labs and Labrys), while `0.25x` sell value keeps rouble progression grounded.
- `reduceCommonKeys: true` *(Mid/Late-Wipe Filter)*: Early wipe (`false`), you want the `47` Common Dorms and utility keys. Once your keytool already has them, setting `"reduceCommonKeys": true` cuts Common weights by `85%` on any preset so `86%-91%` of drops are `Rare` or `SuperRare` (`4.2` keys/100 on `Hardcore`, `16.7` on `Balanced`, `49.5` on `Casual`) without changing your tier's Rare/SuperRare pace.
- `Custom`: Uses the nested `"custom": { ... }` block in `config.jsonc` when you want to zero out a specific rarity tier (`"superRare": 0`), write custom item-count curves, or expand container grids up to `14x14`.

---

## Configuration (`config.jsonc`)

### 1. General Settings (Top of `config.jsonc`, apply to all presets)
- `configVersion`: Internal schema marker (`3`). Missing or older values trigger an automatic backup to `config.backup.jsonc` and a fresh `v3` `config.jsonc` write.
- `enabled`: Master boolean switch (`true` by default).
- `preset`: `"Hardcore"`, `"Balanced"`, `"Casual"`, or `"Custom"`.
- `reduceCommonKeys`: Cuts `Common` and `NotExist` weights to `15%` (`0.15x`) on top of any preset.
- `spawnWeightMultiplier`: Master drop-rate multiplier applied on top of your preset (`1.0` = default, `1.5` = +50% weight, `0.5` = half weight).
- `fleaPriceMultiplier` / `traderPriceMultiplier`: Leave `null` to use your preset's default sell value, or set a number (`1.0` for full vanilla prices, `0.0` for worthless `0`-rouble keys; positive prices clamp `>= 1` RUB).
- `banKeysFromFence`: Simulated SPT PMCs loot static map containers and sell items to Fence. Set `true` to block all injected keys from Fence's shop inventory.
- `enableLocationsConfig`: Scales per-map baselines in `locations/*.jsonc` proportionally by your preset so small maps (`Labyrinth`, `Ground Zero`) and large maps (`Customs`, `Streets`) stay within `25.0` to `25.7` keys per 100 Jackets on `Balanced`.
- `debugLogs`: Set `true` to log evaluated per-map container key pool percentages when raids load.

### 2. Custom Preset Block (`"custom": { ... }`)
Evaluated only when `"preset"` is `"Custom"`:
- `keyWeight` / `keycardWeight`: Integer ticket weights (`notExist`, `common`, `rare`, `superRare`). Setting any tier to `0` removes that tier from targeted containers.
- `fleaPriceMultiplier` / `traderPriceMultiplier`: Sell value multipliers used in `Custom` mode.
- `overrideLootDistribution` & `jacketsItemCounts` / `duffleBagsItemCounts` / `deadScavsItemCounts`: Integer item-count probability arrays (`null` keeps vanilla item counts for that container).
- `minCellsH` / `minCellsV`: Minimum container grid width and height (`1` to `14`), expanding smaller `2x2` Jackets via `Math.Max` without shrinking larger `4x3` Duffle Bags or `4x4` Dead Scavs.

---

## Item Filtering & Uncategorized Key Mappings

At startup, `LootInjectionService` filters `TemplateTable.Items` down to `217` tradable items by excluding quest items (`QuestItem == true`), SPT global/lootable blacklist entries, abstract category nodes (`Type == "Node"`), unfinished placeholder items absent from `Handbook.Items` (7 cut Streets `"City key"` entries), and 1-rouble in-raid puzzle props (`Valve handwheel` and `Ariadne symbol key`).

In vanilla Tarkov data, 21 valid keys and keycards carry an uncategorized `Not_exist` rarity tag (including several boss stash keys and high-tier keycards). Rather than letting boss keys roll at common rates, the mod maps those 21 items to explicit tiers:

<details>
<summary>View all 21 explicit rarity mappings</summary>

- Common (5): Pumping station front door key, Pumping station back door key, Unknown key, Backup hideout key, Horse restaurant toilet key.
- Rare (7): Cold storage room key, Aspect company office key, Unity Credit Bank archive room key, USEC cottage room key, Observation room key, Torture room key, Labyrinth key.
- Superrare (9): Shturman's stash key, Keycard with a blue marking, Car dealership closed section key, TerraGroup storage room keycard, Labrys access keycard, Shatun's hideout key, Grumpy's hideout key, Voron's hideout key, Leon's hideout key.
</details>

---

## Architecture, Building & Testing

- SPT 4.1 Pipeline: Runs at `OnLoadOrder.Routers + 2` (`400002`) so handbook, Flea Market, and Fence blacklist updates settle before SPT's startup caches initialize. Static container injection hooks `Location.StaticLoot.AddTransformer(...)` across all discovered maps and maps both standard and Labyrinth container template IDs via `ContainerTargets`, clamping total container pool weights safely below `int.MaxValue / 2` and grid sizes to EFT's `14x14` UI ceiling.
- Build (`Release`, requires `.NET 10 SDK`):
  ```powershell
  dotnet build .\KeysInLootExtended\KeysInLootExtended.csproj -c Release
  ```
  Compiled output and configs are staged to `dist/SPT/user/mods/KeysInLootExtended/`.
- Run Unit & Golden-Fixture Simulation Tests (`< 1s`, no SPT install required):
  ```powershell
  dotnet test .\KeysInLootExtended.Tests\KeysInLootExtended.Tests.csproj -c Release
  ```
- Run Optional Live-Database & 590-Raid `LocationLootGenerator` Parity Check (Read-Only):
  ```powershell
  $env:SPT_DATA_PATH = "C:\Path\To\SPT\SPT_Runtime\SPT_Data"
  dotnet test .\KeysInLootExtended.Tests\KeysInLootExtended.Tests.csproj -c Release --logger "console;verbosity=detailed"
  ```
- Release Packaging: Run `.\release.ps1 -PackageOnly` to test, build, and verify `KeysInLootExtended-<Version>.zip` locally without uploading, or `.\release.ps1` to publish directly via `gh release`. In CI (`.github/workflows/ci-release.yml`), every push and pull request verifies the package, while pushing a matching version tag (such as `3.0.0` or `v3.0.0`, synced between `<Version>` in `KeysInLootExtended.csproj` and `ModMetadata.Version`) or running `workflow_dispatch` publishes the release notes from `CHANGELOG.md` and attaches the VirusTotal scan link.
