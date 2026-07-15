import { FileSystem } from "@spt/utils/FileSystem";
import { JsonUtil } from "@spt/utils/JsonUtil";
import { IKeysInLootCoreConfig, IKeysInLootLocationConfig } from "./IKeysInLootConfig";
import path from "path";
import { ILocation } from "@spt/models/eft/common/ILocation";

export enum LocationsEnum 
    {
    CUSTOMS = "customs",
    FACTORY_DAY = "factory_day",
    FACTORY_NIGHT = "factory_night",
    INTERCHANGE = "interchange",
    LABORATORY = "laboratory",
    LIGHTHOUSE = "lighthouse",
    RESERVE = "reserve",
    GROUND_ZERO = "ground_zero",
    GROUND_ZERO_HIGH = "ground_zero_high",
    SHORELINE = "shoreline",
    STREETS_OF_TARKOV = "streets_of_tarkov",
    WOODS = "woods"
}

export class ConfigLoader
{
    public static locationIdToEnum: Record<string, LocationsEnum> = {
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

    constructor(
        private fileSystem: FileSystem,
        private jsonUtil: JsonUtil
    ) 
    { }

    public async loadCoreConfig() : Promise<IKeysInLootCoreConfig>
    {
        return this.loadConfig<IKeysInLootCoreConfig>("../config.jsonc");
    }

    public async loadLocationConfig(location: LocationsEnum) : Promise<IKeysInLootLocationConfig>
    {
        const fileName = `${location}.jsonc`;
        const filePath = path.resolve(__dirname, "../locations", fileName);
        return this.loadConfig<IKeysInLootLocationConfig>(filePath);
    }

    public async loadConfig<T>(relativeFilePath: string): Promise<T>
    {
        const configPath = path.resolve(__dirname, relativeFilePath);
        const configFileContent = await this.fileSystem.read(configPath);
        const configString = configFileContent.toString();
        const configModel = this.jsonUtil.deserializeJsonC<T>(configString);
        if (!configModel) 
        {
            throw new Error(`Failed to deserialize config from ${configPath}`);
        }
        return configModel;
    }

    public async getOrCreateLocationConfig(location: ILocation, coreConfig: IKeysInLootCoreConfig): Promise<IKeysInLootLocationConfig>
    {
        if (coreConfig.enableLocationsConfig)
        {
            return await this.loadLocationSpecificConfig(location);
        }
        else
        {
            return ConfigLoader.createDefaultLocationConfig(coreConfig.keyWeight, coreConfig.keycardWeight);
        }
    }

    /// <summary>
    /// Loads the location-specific configuration for keys in loot.
    /// </summary>
    private loadLocationSpecificConfig(location: ILocation): Promise<IKeysInLootLocationConfig>
    {
        const enumValue = ConfigLoader.locationIdToEnum[location.base.Id];
        if (!enumValue) 
        {
            throw new Error(`Unknown location: ${location.base.Id}`);
        }

        // Load the config for this location from file
        return this.loadLocationConfig(enumValue);
    }

    /// <summary>
    /// Creates a default location configuration with the global weights for keys and keycards.
    /// </summary>
    private static createDefaultLocationConfig(keyWeight: number, keycardWeight: number): IKeysInLootLocationConfig 
    {
        const containerConfig = { 
            key: { notExist: keyWeight, common: keyWeight, rare: keyWeight, superRare: keyWeight }, 
            keycard: { notExist: keyWeight, common: keycardWeight, rare: keycardWeight, superRare: keycardWeight } 
        };
        return {
            jacketContainer: containerConfig,
            duffleBagContainer: containerConfig,
            deadScavContainer: containerConfig
        };
    }
}

