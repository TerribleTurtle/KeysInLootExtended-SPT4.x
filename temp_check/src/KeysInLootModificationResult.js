"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.KeysInLootModificationResult = void 0;
class KeysInLootModificationResult {
    adjustedWeights;
    addedWeights;
    constructor(adjustedWeights, addedWeights) {
        this.adjustedWeights = adjustedWeights;
        this.addedWeights = addedWeights;
    }
    adjusted(item) {
        this.adjustedWeights.push(item);
    }
    added(item) {
        this.addedWeights.push(item);
    }
    addResult(other) {
        this.adjustedWeights.push(...other.adjustedWeights);
        this.addedWeights.push(...other.addedWeights);
    }
    static empty() {
        return new KeysInLootModificationResult([], []);
    }
}
exports.KeysInLootModificationResult = KeysInLootModificationResult;
