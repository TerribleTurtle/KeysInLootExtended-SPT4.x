# TerribleTurtle - Keys In Loot Extended
[GitHub Repository](https://github.com/TerribleTurtle/KeysInLootExtended)

> **Based on the original "Keys In Loot" mod by MusicManiac**

This mod allows every single key in Escape from Tarkov to spawn inside standard loot containers (like Jackets and Duffle Bags) instead of being locked exclusively behind bosses and specific map spawns.

## Features

- **Dynamic Loot Adjustment:** Automatically hooks into SPT's database to find all current keys and keycards (including new ones added in recent updates).
- **Customizable Spawn Weights:** Increases the chance of keys spawning in Jackets, Duffle Bags, and on Dead Scavs based on their rarity (Common, Rare, Superrare, etc.).
- **Container Overrides:** Modifies the internal probability distributions so that containers are much more likely to spawn multiple items, rather than being empty. 
- **Expanded Jackets:** Automatically expands the internal size of Jackets (defaulting to 3x3 grid) to mathematically support the increased number of items that can spawn inside them.
- **Economy Rebalance:** Because keys are now much easier to find, the mod automatically reduces their Flea Market and Trader sell prices (default: 60% reduction) to prevent players from ruining their economy and getting rich too quickly.
- **Per-Map Configuration:** Allows tweaking spawn weights globally or fine-tuning them on a map-by-map basis using the `locations/` config files.

## Configuration

All global settings can be tweaked in the `config.jsonc` file located in the mod folder:

- `keyWeight` & `keycardWeight`: The target spawn probability weight for keys and keycards.
- `keyFleaPricesMultiplier` & `keyTraderPricesMultiplier`: Multipliers to adjust the sell price of keys. Set to `1.0` to disable the price nerf.
- `overRideLootDistributionJackets`: The probability matrix defining how many total items spawn in a jacket. If you feel jackets are still too dense or not dense enough, you can scroll down in the config to the `overRideLootDistributionJackets` array and tweak the probabilities yourself!
- `cellsH` & `cellsV`: The physical grid size of jacket containers (default is 3x3).

## ⚖️ Understanding Loot Weights & Rarity

Keys In Loot allows you to inject missing keys into the loot pool, but tuning their spawn chances can be tricky. Here is how SPT's underlying math works, decoded for your convenience.

### The "Lottery Ticket" System
SPT loot generation uses a system called `relativeProbability`. Imagine every loot container (like a Jacket) is a hat filled with lottery tickets. A standard Jacket on Customs contains roughly **200,000 tickets** representing all possible items that can spawn there.
- Common junk (like matches or bolts) might have ~5,000 tickets.
- An ultra-rare key might only have ~100 tickets.

When you inject keys using this mod's config, you are adding *new tickets* to the hat.

### 🎮 Experience Profiles

You can completely change the "vibe" of the mod just by changing the `"activeProfile"` setting at the very top of `config.jsonc`. 

Valid options include:

*   🟢 **`"Vanilla Plus"` (Balanced):** The default configuration. Corrects the math so that common keys are found somewhat regularly, while rare keys remain appropriately scarce. Averages roughly 1 key per 4 jackets.
*   🔴 **`"Hardcore Scarcity"`:** Drastically reduces global key weights and disables jacket density overrides. Averages roughly 1 key per 16 jackets.
*   🟡 **`"The Original Experience"`:** Restores the mod's original "flat" distribution. Gives common and ultra-rare keys the exact same spawn chance. Averages roughly 1 key per jacket.
*   🟣 **`"The Loot Piñata"`:** Pure chaos. Heavily suppresses common keys and violently inflates ultra-rare keys. Averages 2-8 high-tier keys per jacket.
*   ❌ **`"Disabled"`:** Completely skips all logic. Leaves loot tables, prices, and jacket sizes 100% vanilla.

If you want to manually tweak the individual variables, simply set `"activeProfile": "Custom"`. This will tell the mod to ignore the built-in presets and instead use the exact variables defined in your config file.

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
Do not use decimal numbers (e.g., `0.5`) in the `itemcountDistribution` configurations. The game engine expects strict integers, and using decimals will immediately crash the server when you attempt to load a map.

*(Note: The list below is an older reference of keys from SPT 3.11.3, but the mod dynamically pulls all current keys regardless of this list).*

---

# 3.11.3 Keys Reference
Here is some information about keys in SPT 3.11.3

## RarityPvE: "Not_exist"
Count = 31
- [0]: "Pistol case key"
- [1]: "Key 3"
- [2]: "Key 5"
- [3]: "Key 2"
- [4]: "Pumping station front door key"
- [5]: "Folding car key"
- [6]: "Machinery key"
- [7]: "Pumping station back door key"
- [8]: "Unknown key"
- [9]: "Quest test key"
- [10]: "Key to the closed premises of the Health Resort"
- [11]: "Shturman's stash key"
- [12]: "Cold storage room key"
- [13]: "Primorsky Ave apartment key"
- [14]: "Backup hideout key"
- [15]: "Car dealership closed section key"
- [16]: "Apartment locked room safe key"
- [17]: "Сity key"
- [18]: "Housing office second floor safe key"
- [19]: "Housing office first floor safe key"
- [20]: "Сity key"
- [21]: "Сity key"
- [22]: "Aspect company office key"
- [23]: "Horse restaurant toilet key"
- [24]: "Unity Credit Bank archive room key"
- [25]: "USEC cottage room key"
- [26]: "Shatun's hideout key"
- [27]: "Grumpy's hideout key"
- [28]: "Voron's hideout key"
- [29]: "Leon's hideout key"
- [30]: "Dorm overseer key"

## RarityPvE: "Common"
Count = 42
- [0]: "Dorm room 118 key"
- [1]: "Dorm room 306 key"
- [2]: "Dorm room 315 key"
- [3]: "Dorm room 308 key"
- [4]: "Dorm room 218 key"
- [5]: "Gas station office key"
- [6]: "Portable cabin key"
- [7]: "Trailer park portable cabin key"
- [8]: "VAZ car key"
- [9]: "Folding car key"
- [10]: "Dorm room 104 key"
- [11]: "Dorm room 108 key"
- [12]: "Weapon safe key"
- [13]: "Yotota car key"
- [14]: "Machinery key"
- [15]: "Portable bunkhouse key"
- [16]: "Dorm room 203 key"
- [17]: "Dorm room 206 key"
- [18]: "Dorm room 103 key"
- [19]: "Dorm room 303 key"
- [20]: "Health Resort east wing office room 108 key"
- [21]: "SMW car key"
- [22]: "Health Resort west wing room 207 key"
- [23]: "Health Resort east wing room 213 key"
- [24]: "Health Resort east wing room 216 key"
- [25]: "Health Resort west wing room 303 key"
- [26]: "Health Resort west wing room 309 key"
- [27]: "Health Resort west wing room 325 key"
- [28]: "Health Resort east wing room 322 key"
- [29]: "Gas station safe key"
- [30]: "Store safe key"
- [31]: "Health Resort west wing room 323 key"
- [32]: "OLI outlet utility room key"
- [33]: "Power substation utility cabin key"
- [34]: "RB-AK key"
- [35]: "RB-OP key"
- [36]: "RB-PP key"
- [37]: "RB-KORL key"
- [38]: "RB-GN key"
- [39]: "RB-RH key"
- [40]: "Supply department director's office key"
- [41]: "Stair landing key"

## RarityPvE: "Rare"
Count = 68
- [0]: "Factory emergency exit key"
- [1]: "Dorm room 214 key"
- [2]: "Dorm room 220 key"
- [3]: "Tarcone Director's office key"
- [4]: "Dorm guard desk key"
- [5]: "Dorm room 110 key"
- [6]: "Dorm room 105 key"
- [7]: "Gas station storage room key"
- [8]: "Military checkpoint key"
- [9]: "Dorm room 204 key"
- [10]: "ZB-014 key"
- [11]: "Dorm room 114 key"
- [12]: "Health Resort universal utility room key"
- [13]: "Health Resort east wing room 209 key"
- [14]: "Health Resort west wing room 321 safe key"
- [15]: "Weather station safe key"
- [16]: "Cottage safe key"
- [17]: "Health Resort management office safe key"
- [18]: "Health Resort management warehouse safe key"
- [19]: "Health Resort west wing room 219 key"
- [20]: "Health Resort west wing room 306 key"
- [21]: "Health Resort east wing room 316 key"
- [22]: "OLI administration office key"
- [23]: "OLI cash register key"
- [24]: "IDEA cash register key"
- [25]: "Goshan cash register key"
- [26]: "RB-AO key"
- [27]: "RB-OB key"
- [28]: "RB-TB key"
- [29]: "RB-AM key"
- [30]: "RB-MP11 key"
- [31]: "RB-MP12 key"
- [32]: "RB-MP21 key"
- [33]: "RB-MP22 key"
- [34]: "RB-PSP1 key"
- [35]: "RB-PSV1 key"
- [36]: "RB-MP13 key"
- [37]: "RB-ORB1 key"
- [38]: "RB-ORB2 key"
- [39]: "RB-ORB3 key"
- [40]: "RB-SMP key"
- [41]: "RB-KSM key"
- [42]: "RB-PSV2 key"
- [43]: "RB-PSP2 key"
- [44]: "RB-RS key"
- [45]: "Convenience store storage room key"
- [46]: "Hillside house key"
- [47]: "Police truck cabin key"
- [48]: "Merin car trunk key"
- [49]: "USEC cottage first safe key"
- [50]: "USEC cottage second safe key"
- [51]: "Rogue USEC workshop key"
- [52]: "Operating room key"
- [53]: "Water treatment plant storage room key"
- [54]: "Rogue USEC barrack key"
- [55]: "Financial institution office key"
- [56]: "Store manager's key"
- [57]: "Construction site bunkhouse key"
- [58]: "Zmeisky 5 apartment 20 key"
- [59]: "Zmeisky 3 apartment 8 key"
- [60]: "Archive room key"
- [61]: "Pinewood hotel room 215 key"
- [62]: "Pinewood hotel room 206 key"
- [63]: "Iron gate key"
- [64]: "Cargo container mesh door key"
- [65]: "Concordia apartment 8 room key"
- [66]: "Primorsky 48 apartment key"
- [67]: "Financial institution small office key"

## RarityPvE: "Superrare"
Count = 74
- [0]: "Dorm room 314 marked key"
- [1]: "Health Resort west wing office room 104 key"
- [2]: "Health Resort west wing office room 112 key"
- [3]: "Health Resort east wing office room 107 key"
- [4]: "Cottage back door key"
- [5]: "Health Resort west wing room 205 key"
- [6]: "Health Resort west wing room 216 key"
- [7]: "Health Resort west wing room 220 key"
- [8]: "Health Resort west wing room 221 key"
- [9]: "Health Resort east wing room 206 key"
- [10]: "Health Resort east wing room 310 key"
- [11]: "Health Resort east wing room 313 key"
- [12]: "Health Resort east wing room 314 key"
- [13]: "Health Resort east wing room 328 key"
- [14]: "Health Resort west wing room 218 key"
- [15]: "Health Resort west wing room 301 key"
- [16]: "Health Resort east wing room 222 key"
- [17]: "Health Resort east wing room 226 key"
- [18]: "Health Resort east wing room 205 key"
- [19]: "Health Resort west wing room 203 key"
- [20]: "Health Resort west wing room 222 key"
- [21]: "Health Resort east wing room 306 key"
- [22]: "Health Resort east wing room 308 key"
- [23]: "OLI logistics department office key"
- [24]: "NecrusPharm pharmacy key"
- [25]: "Kiba Arms outer door key"
- [26]: "EMERCOM medical unit key"
- [27]: "Kiba Arms inner grate door key"
- [28]: "TerraGroup Labs manager's office room key"
- [29]: "TerraGroup Labs weapon testing area key"
- [30]: "TerraGroup Labs arsenal storage room key"
- [31]: "RB-BK marked key"
- [32]: "RB-VO marked key"
- [33]: "RB-KPRL key"
- [34]: "HEP station storage room key"
- [35]: "RB-ST key"
- [36]: "USEC stash key"
- [37]: "ULTRA medical storage key"
- [38]: "RB-PKPM marked key"
- [39]: "RB-RLSA key"
- [40]: "Health Resort office key with a blue tape"
- [41]: "Rogue USEC stash key"
- [42]: "Radar station commandant room key"
- [43]: "Conference room key"
- [44]: "Shared bedroom marked key"
- [45]: "Missam forklift key"
- [46]: "Car dealership director's office room key"
- [47]: "Concordia security room key"
- [48]: "Primorsky 46-48 skybridge key"
- [49]: "Chekannaya 15 apartment key"
- [50]: "Abandoned factory marked key"
- [51]: "Concordia apartment 64 office room key"
- [52]: "Concordia apartment 64 key"
- [53]: "Concordia apartment 34 room key"
- [54]: "Concordia apartment 8 home cinema key"
- [55]: "Concordia apartment 63 room key"
- [56]: "Beluga restaurant director key"
- [57]: "TerraGroup meeting room key"
- [58]: "Tarbank cash register department key"
- [59]: "X-ray room key"
- [60]: "TerraGroup security armory key"
- [61]: "Mysterious room marked key"
- [62]: "PE teacher's office key"
- [63]: "Rusted bloody key"
- [64]: "Unity Credit Bank cash register key"
- [65]: "Underground parking utility room key"
- [66]: "TerraGroup science office key"
- [67]: "\"Negotiation\" room key"
- [68]: "Relaxation room key"
- [69]: "MVD academy entrance hall guard room key"
- [70]: "Real estate agency office room key"
- [71]: "Tarkov City souvenir key"
- [72]: "Old house room key"
- [73]: "Company director's room key"

---

# 3.11.3 Keycards Reference
Here is the baseline rarity data for Keycards in SPT 3.11.3

## RarityPvE: "Rare"
Count = 1
- [0]: "TerraGroup Labs access keycard"

## RarityPvE: "Superrare"
Count = 8
- [0]: "TerraGroup Labs keycard (Blue)"
- [1]: "TerraGroup Labs keycard (Green)"
- [2]: "TerraGroup Labs keycard (Red)"
- [3]: "TerraGroup Labs keycard (Violet)"
- [4]: "TerraGroup Labs keycard (Yellow)"
- [5]: "TerraGroup Labs keycard (Black)"
- [6]: "#11SR secret room keycard"
- [7]: "#21WS space restriction safe keycard"
