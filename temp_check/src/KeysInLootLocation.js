"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.KeysInLootLocation = void 0;
const KeysInLootModificationResult_1 = require("./KeysInLootModificationResult");
const KeysInLootContainer_1 = require("./KeysInLootContainer");
class KeysInLootLocation {
    location;
    itemDistributionService;
    keyItems;
    keycardItems;
    coreConfig;
    config;
    constructor(location, itemDistributionService, keyItems, keycardItems, coreConfig, config) {
        this.location = location;
        this.itemDistributionService = itemDistributionService;
        this.keyItems = keyItems;
        this.keycardItems = keycardItems;
        this.coreConfig = coreConfig;
        this.config = config;
    }
    modifyContainers() {
        const result = KeysInLootModificationResult_1.KeysInLootModificationResult.empty();
        // Modify the default containers in the location to include keys and keycards
        const jacketContainer = this.getJacketContainer();
        if (jacketContainer) {
            const jacketResult = jacketContainer.modifyItemDistribution();
            if (this.coreConfig.overrideLootDistribution) {
                jacketContainer.modifyItemCountDistribution(this.coreConfig.overRideLootDistributionJackets);
            }
            result.addResult(jacketResult);
        }
        const duffleBagContainer = this.getDuffleBagContainer();
        if (duffleBagContainer) {
            const duffleBagResult = duffleBagContainer.modifyItemDistribution();
            if (this.coreConfig.overrideLootDistribution) {
                duffleBagContainer.modifyItemCountDistribution(this.coreConfig.overRideLootDistributionDuffleBags);
            }
            result.addResult(duffleBagResult);
        }
        const deadScavContainer = this.getDeadScavContainer();
        if (deadScavContainer) {
            const deadScavResult = deadScavContainer.modifyItemDistribution();
            if (this.coreConfig.overrideLootDistribution) {
                deadScavContainer.modifyItemCountDistribution(this.coreConfig.overRideLootDistributionDeadScavs);
            }
            result.addResult(deadScavResult);
        }
        return result;
    }
    getJacketContainer() {
        const staticLootCotainer = this.tryFindContainerInLocation("578f8778245977358849a9b5", this.location); // Jacket container ID
        if (!staticLootCotainer)
            return null;
        return new KeysInLootContainer_1.KeysInLootContainer(`Container Jacket for location ${this.location.base.Name} (${this.location.base._Id})`, staticLootCotainer, this.itemDistributionService, this.keyItems, this.keycardItems, this.coreConfig, this.config.jacketContainer);
    }
    getDuffleBagContainer() {
        const staticLootCotainer = this.tryFindContainerInLocation("578f87a3245977356274f2cb", this.location); // Duffel bag container ID
        if (!staticLootCotainer)
            return null;
        return new KeysInLootContainer_1.KeysInLootContainer(`Container Duffel Bag for location ${this.location.base.Name} (${this.location.base._Id})`, staticLootCotainer, this.itemDistributionService, this.keyItems, this.keycardItems, this.coreConfig, this.config.duffleBagContainer);
    }
    getDeadScavContainer() {
        const staticLootCotainer = this.tryFindContainerInLocation("5909e4b686f7747f5b744fa4", this.location); // Dead scav container ID
        if (!staticLootCotainer)
            return null;
        return new KeysInLootContainer_1.KeysInLootContainer(`Container Dead Scav for location ${this.location.base.Name} (${this.location.base._Id})`, staticLootCotainer, this.itemDistributionService, this.keyItems, this.keycardItems, this.coreConfig, this.config.deadScavContainer);
    }
    tryFindContainerInLocation(containerTplId, location) {
        if (!location.staticLoot) {
            return null;
        }
        const container = location.staticLoot[containerTplId];
        if (!container)
            return null;
        return container;
    }
}
exports.KeysInLootLocation = KeysInLootLocation;
