"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.KeysInLootContainer = void 0;
const KeysInLootModificationResult_1 = require("./KeysInLootModificationResult");
const RarityPVEEnum_1 = require("./RarityPVEEnum");
class KeysInLootContainer {
    context;
    container;
    itemDistributionService;
    keyItems;
    keycardItems;
    coreConfig;
    config;
    constructor(context, container, itemDistributionService, keyItems, keycardItems, coreConfig, config) {
        this.context = context;
        this.container = container;
        this.itemDistributionService = itemDistributionService;
        this.keyItems = keyItems;
        this.keycardItems = keycardItems;
        this.coreConfig = coreConfig;
        this.config = config;
    }
    modifyItemDistribution() {
        const result = KeysInLootModificationResult_1.KeysInLootModificationResult.empty();
        // For keys
        result.addResult(this.modifyItemRarityDistribution(this.keyItems, RarityPVEEnum_1.RarityPVEEnum.NOT_EXISTS, this.config.key.notExist));
        result.addResult(this.modifyItemRarityDistribution(this.keyItems, RarityPVEEnum_1.RarityPVEEnum.COMMON, this.config.key.common));
        result.addResult(this.modifyItemRarityDistribution(this.keyItems, RarityPVEEnum_1.RarityPVEEnum.RARE, this.config.key.rare));
        result.addResult(this.modifyItemRarityDistribution(this.keyItems, RarityPVEEnum_1.RarityPVEEnum.SUPER_RARE, this.config.key.superRare));
        // For keycards
        result.addResult(this.modifyItemRarityDistribution(this.keycardItems, RarityPVEEnum_1.RarityPVEEnum.NOT_EXISTS, this.config.keycard.notExist));
        result.addResult(this.modifyItemRarityDistribution(this.keycardItems, RarityPVEEnum_1.RarityPVEEnum.COMMON, this.config.keycard.common));
        result.addResult(this.modifyItemRarityDistribution(this.keycardItems, RarityPVEEnum_1.RarityPVEEnum.RARE, this.config.keycard.rare));
        result.addResult(this.modifyItemRarityDistribution(this.keycardItems, RarityPVEEnum_1.RarityPVEEnum.SUPER_RARE, this.config.keycard.superRare));
        return result;
    }
    modifyItemRarityDistribution(sourceItems, rarityPvE, minimumRelativeProbability) {
        try {
            if (minimumRelativeProbability === 0)
                return KeysInLootModificationResult_1.KeysInLootModificationResult.empty();
            const items = sourceItems.filter(item => item._props?.RarityPvE === rarityPvE);
            const result = this.itemDistributionService.ensureMinimumRelativeProbabilityForItemsInContainer(this.container, items, minimumRelativeProbability);
            if (this.coreConfig.consoleVerbosity && this.coreConfig.consoleVerbosity === "detailed")
                console.log(`Minimum relative probability ${minimumRelativeProbability} for rarity ${rarityPvE} in ${this.context}: ${result.adjustedWeights.length} adjusted, ${result.addedWeights.length} added`);
            return result;
        }
        catch (error) {
            console.error(`Error modifying item rarity distribution for ${rarityPvE} in ${this.context}:`, error);
            return KeysInLootModificationResult_1.KeysInLootModificationResult.empty();
        }
    }
    modifyItemCountDistribution(itemCountDistribution) {
        this.container.itemcountDistribution = itemCountDistribution;
    }
}
exports.KeysInLootContainer = KeysInLootContainer;
