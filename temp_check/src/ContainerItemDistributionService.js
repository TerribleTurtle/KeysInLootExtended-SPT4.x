"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.ContainerItemDistributionService = void 0;
const KeysInLootModificationResult_1 = require("./KeysInLootModificationResult");
class ContainerItemDistributionService {
    ensureMinimumRelativeProbabilityForItemsInContainer(container, items, minimumRelativeProbability) {
        const modification = KeysInLootModificationResult_1.KeysInLootModificationResult.empty();
        for (const key of items) {
            const result = this.ensureMinimumRelativeProbabilityForItemInContainer(container, key, minimumRelativeProbability);
            modification.addResult(result);
        }
        return modification;
    }
    ensureMinimumRelativeProbabilityForItemInContainer(container, itemTemplate, configWeight) {
        const result = KeysInLootModificationResult_1.KeysInLootModificationResult.empty();
        const foundItem = container.itemDistribution.find(item => item.tpl === itemTemplate._id);
        if (foundItem) {
            if (foundItem.relativeProbability < configWeight) {
                foundItem.relativeProbability = configWeight;
                result.adjusted(itemTemplate);
            }
        }
        else {
            container.itemDistribution.push({
                tpl: itemTemplate._id,
                relativeProbability: configWeight
            });
            result.added(itemTemplate);
        }
        return result;
    }
}
exports.ContainerItemDistributionService = ContainerItemDistributionService;
