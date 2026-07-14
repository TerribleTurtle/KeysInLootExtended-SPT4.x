"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
console.log("HELLO FROM KEYSINLOOTEXTENDED! If you see this, the mod is being loaded.");
const ItemPriceService_1 = require("./ItemPriceService");
const ContainerItemDistributionService_1 = require("./ContainerItemDistributionService");
const KeysInLootModificationResult_1 = require("./KeysInLootModificationResult");
const ConfigLoader_1 = require("./ConfigLoader");
const KeysInLootLocationFactory_1 = require("./KeysInLootLocationFactory");
class KeysInLoot {
    logger;
    mod;
    modShortName;
    constructor() {
        this.mod = "TerribleTurtle-KeysInLootExtended";
        this.modShortName = "KeysInLootExtended";
    }
    async postDBLoad(container) {
        this.logger = container.resolve("WinstonLogger");
        const logger = this.logger;
        logger.info(`[${this.modShortName}] ${this.mod} started loading`);
        // Resolve dependencies
        const itemHelper = container.resolve("ItemHelper");
        const db = container.resolve("DatabaseServer");
        const fs = container.resolve("FileSystem");
        const jsonUtil = container.resolve("JsonUtil");
        const configLoader = new ConfigLoader_1.ConfigLoader(fs, jsonUtil);
        const itemDistributionService = new ContainerItemDistributionService_1.ContainerItemDistributionService();
        const keysInLootLocationFactory = new KeysInLootLocationFactory_1.KeysInLootLocationFactory(configLoader, itemDistributionService, itemHelper);
        // Load data
        const config = await configLoader.loadCoreConfig();
        if (config.activeProfile === "Disabled") {
            logger.info(`[${this.modShortName}] Profile is Disabled. Skipping all modifications.`);
            return;
        }
        logger.info(`[${this.modShortName}] Active Profile Loaded: ${config.activeProfile}`);
        logger.info(`[${this.modShortName}] Global Key Weights: {notExist: ${config.keyWeight.notExist}, common: ${config.keyWeight.common}, rare: ${config.keyWeight.rare}, superRare: ${config.keyWeight.superRare}}`);
        logger.info(`[${this.modShortName}] Global Keycard Weights: {notExist: ${config.keycardWeight.notExist}, common: ${config.keycardWeight.common}, rare: ${config.keycardWeight.rare}, superRare: ${config.keycardWeight.superRare}}`);
        logger.info(`[${this.modShortName}] Jacket Density Override: ${config.overrideLootDistribution}`);
        logger.info(`[${this.modShortName}] Jacket Grid Size: ${config.cellsH}x${config.cellsV}`);
        const tables = db.getTables();
        const sptLocations = [
            tables.locations.bigmap,
            tables.locations.factory4_day,
            tables.locations.factory4_night,
            tables.locations.interchange,
            tables.locations.laboratory,
            tables.locations.lighthouse,
            tables.locations.rezervbase,
            tables.locations.sandbox,
            tables.locations.sandbox_high,
            tables.locations.shoreline,
            tables.locations.tarkovstreets,
            tables.locations.woods
        ];
        const totalResult = KeysInLootModificationResult_1.KeysInLootModificationResult.empty();
        for (const sptLocation of sptLocations) {
            try {
                const keysInLootLocation = await keysInLootLocationFactory.createKeysInLootLocation(sptLocation);
                const locationResult = keysInLootLocation.modifyContainers();
                totalResult.addResult(locationResult);
            }
            catch (err) {
                console.error(`[${this.modShortName}] Error while processing location ${sptLocation.base._Id}: ${err}`);
                continue;
            }
        }
        try {
            // Get distinct list of item templates from totalResult
            const allItems = [...totalResult.adjustedWeights, ...totalResult.addedWeights];
            const distinctItems = allItems.filter((item, index, self) => index === self.findIndex(t => t._id === item._id));
            // Adjust prices for modified or added keys or keycards
            const itemPriceService = new ItemPriceService_1.ItemPriceService(tables);
            distinctItems.forEach(item => itemPriceService.adjustFleaMarketPrice(item, config.keyFleaPricesMultiplier));
            distinctItems.forEach(item => itemPriceService.adjustTraderPrice(item, config.keyTraderPricesMultiplier));
        }
        catch (err) {
            console.error(`[${this.modShortName}] Error while processing prices: ${err}`);
        }
        logger.info(`[${this.modShortName}] Successfully injected ${totalResult.addedWeights.length} keys across 12 locations!`);
        logger.info(`[${this.modShortName}] Flea & Trader prices adjusted by a ${config.keyFleaPricesMultiplier}x multiplier.`);
        // Adjust jacket cell size
        const itemDB = tables.templates.items;
        itemDB["578f8778245977358849a9b5"]._props.Grids[0]._props.cellsH = config.cellsH;
        itemDB["578f8778245977358849a9b5"]._props.Grids[0]._props.cellsV = config.cellsV;
        logger.success(`[${this.modShortName}] ${this.mod} finished loading`);
    }
}
module.exports = { mod: new KeysInLoot() };
