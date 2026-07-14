"use strict";
var __importDefault = (this && this.__importDefault) || function (mod) {
    return (mod && mod.__esModule) ? mod : { "default": mod };
};
Object.defineProperty(exports, "__esModule", { value: true });
exports.ConfigLoader = exports.LocationsEnum = void 0;
const path_1 = __importDefault(require("path"));
var LocationsEnum;
(function (LocationsEnum) {
    LocationsEnum["CUSTOMS"] = "customs";
    LocationsEnum["FACTORY_DAY"] = "factory_day";
    LocationsEnum["FACTORY_NIGHT"] = "factory_night";
    LocationsEnum["INTERCHANGE"] = "interchange";
    LocationsEnum["LABORATORY"] = "laboratory";
    LocationsEnum["LIGHTHOUSE"] = "lighthouse";
    LocationsEnum["RESERVE"] = "reserve";
    LocationsEnum["GROUND_ZERO"] = "ground_zero";
    LocationsEnum["GROUND_ZERO_HIGH"] = "ground_zero_high";
    LocationsEnum["SHORELINE"] = "shoreline";
    LocationsEnum["STREETS_OF_TARKOV"] = "streets_of_tarkov";
    LocationsEnum["WOODS"] = "woods";
})(LocationsEnum || (exports.LocationsEnum = LocationsEnum = {}));
class ConfigLoader {
    fileSystem;
    jsonUtil;
    static locationIdToEnum = {
        "bigmap": LocationsEnum.CUSTOMS,
        // eslint-disable-next-line @typescript-eslint/naming-convention
        "factory4_day": LocationsEnum.FACTORY_DAY,
        // eslint-disable-next-line @typescript-eslint/naming-convention
        "factory4_night": LocationsEnum.FACTORY_NIGHT,
        "Interchange": LocationsEnum.INTERCHANGE,
        "laboratory": LocationsEnum.LABORATORY,
        "Lighthouse": LocationsEnum.LIGHTHOUSE,
        "RezervBase": LocationsEnum.RESERVE,
        "Sandbox": LocationsEnum.GROUND_ZERO,
        // eslint-disable-next-line @typescript-eslint/naming-convention
        "Sandbox_high": LocationsEnum.GROUND_ZERO_HIGH,
        "Shoreline": LocationsEnum.SHORELINE,
        "TarkovStreets": LocationsEnum.STREETS_OF_TARKOV,
        "Woods": LocationsEnum.WOODS
    };
    constructor(fileSystem, jsonUtil) {
        this.fileSystem = fileSystem;
        this.jsonUtil = jsonUtil;
    }
    async loadCoreConfig() {
        const config = await this.loadConfig("../config.jsonc");
        // Strict schema check for backward compatibility to prevent server crash
        if (!config.keyWeight || typeof config.keyWeight !== "object" || !config.keycardWeight || typeof config.keycardWeight !== "object") {
            throw new Error("[KeysInLootExtended] FATAL: Your config.jsonc is outdated. 'keyWeight' and 'keycardWeight' must now be rarity objects, not flat numbers. Please update your config.jsonc to the latest version!");
        }
        // Apply profile overrides
        switch (config.activeProfile) {
            case "Vanilla Plus":
                config.keyWeight = { notExist: 200, common: 200, rare: 100, superRare: 40 };
                config.keycardWeight = { notExist: 60, common: 60, rare: 30, superRare: 15 };
                config.overrideLootDistribution = true;
                config.keyFleaPricesMultiplier = 0.4;
                config.keyTraderPricesMultiplier = 0.4;
                config.cellsH = 3;
                config.cellsV = 3;
                break;
            case "Hardcore Scarcity":
                config.keyWeight = { notExist: 60, common: 60, rare: 30, superRare: 15 };
                config.keycardWeight = { notExist: 15, common: 15, rare: 8, superRare: 4 };
                config.overrideLootDistribution = false;
                config.keyFleaPricesMultiplier = 1.0;
                config.keyTraderPricesMultiplier = 1.0;
                config.cellsH = 3;
                config.cellsV = 3;
                break;
            case "The Original Experience":
                config.keyWeight = { notExist: 500, common: 500, rare: 500, superRare: 500 };
                config.keycardWeight = { notExist: 200, common: 200, rare: 200, superRare: 200 };
                config.overrideLootDistribution = true;
                config.keyFleaPricesMultiplier = 0.4;
                config.keyTraderPricesMultiplier = 0.4;
                config.cellsH = 3;
                config.cellsV = 3;
                break;
            case "The Loot Piñata":
                config.keyWeight = { notExist: 10, common: 10, rare: 5000, superRare: 10000 };
                config.keycardWeight = { notExist: 10, common: 10, rare: 1000, superRare: 5000 };
                config.overrideLootDistribution = true;
                config.keyFleaPricesMultiplier = 0.1;
                config.keyTraderPricesMultiplier = 0.1;
                config.cellsH = 5;
                config.cellsV = 5;
                break;
            case "Disabled":
                break;
            case "Custom":
                break;
            default:
                console.warn(`[KeysInLootExtended] WARNING: Unknown profile '${config.activeProfile}' selected. Defaulting to 'Custom' settings.`);
                config.activeProfile = "Custom";
                break;
        }
        return config;
    }
    async loadLocationConfig(location) {
        const fileName = `${location}.jsonc`;
        const filePath = path_1.default.resolve(__dirname, "../locations", fileName);
        return this.loadConfig(filePath);
    }
    async loadConfig(relativeFilePath) {
        const configPath = path_1.default.resolve(__dirname, relativeFilePath);
        const configFileContent = await this.fileSystem.read(configPath);
        const configString = configFileContent.toString();
        const configModel = this.jsonUtil.deserializeJsonC(configString);
        if (!configModel) {
            throw new Error(`Failed to deserialize config from ${configPath}`);
        }
        return configModel;
    }
    async getOrCreateLocationConfig(location, coreConfig) {
        if (coreConfig.enableLocationsConfig) {
            return await this.loadLocationSpecificConfig(location);
        }
        else {
            return ConfigLoader.createDefaultLocationConfig(coreConfig.keyWeight, coreConfig.keycardWeight);
        }
    }
    /// <summary>
    /// Loads the location-specific configuration for keys in loot.
    /// </summary>
    loadLocationSpecificConfig(location) {
        const enumValue = ConfigLoader.locationIdToEnum[location.base.Id];
        if (!enumValue) {
            throw new Error(`Unknown location: ${location.base.Id}`);
        }
        // Load the config for this location from file
        return this.loadLocationConfig(enumValue);
    }
    /// <summary>
    /// Creates a default location configuration with the global weights for keys and keycards.
    /// </summary>
    static createDefaultLocationConfig(keyWeight, keycardWeight) {
        const containerConfig = {
            key: keyWeight,
            keycard: keycardWeight
        };
        return {
            jacketContainer: containerConfig,
            duffleBagContainer: containerConfig,
            deadScavContainer: containerConfig
        };
    }
}
exports.ConfigLoader = ConfigLoader;
