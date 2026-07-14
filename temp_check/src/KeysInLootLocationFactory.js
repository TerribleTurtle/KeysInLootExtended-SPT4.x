"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.KeysInLootLocationFactory = void 0;
const KeysInLootLocation_1 = require("./KeysInLootLocation");
const BaseClasses_1 = require("@spt/models/enums/BaseClasses");
class KeysInLootLocationFactory {
    configLoader;
    itemDistributionService;
    itemHelper;
    items;
    keys;
    keycards;
    _coreConfigPromise = null;
    constructor(configLoader, itemDistributionService, itemHelper) {
        this.configLoader = configLoader;
        this.itemDistributionService = itemDistributionService;
        this.itemHelper = itemHelper;
        this.items = this.itemHelper.getItems();
        this.keys = this.items.filter(item => this.itemHelper.isOfBaseclass(item._id, BaseClasses_1.BaseClasses.KEY));
        this.keycards = this.items.filter(item => this.itemHelper.isOfBaseclass(item._id, BaseClasses_1.BaseClasses.KEYCARD));
    }
    async createKeysInLootLocation(location) {
        const coreConfig = await this.getCoreConfig();
        const locationConfig = await this.configLoader.getOrCreateLocationConfig(location, coreConfig);
        return new KeysInLootLocation_1.KeysInLootLocation(location, this.itemDistributionService, this.keys, this.keycards, coreConfig, locationConfig);
    }
    async getCoreConfig() {
        if (!this._coreConfigPromise) {
            this._coreConfigPromise = this.configLoader.loadCoreConfig();
        }
        return this._coreConfigPromise;
    }
}
exports.KeysInLootLocationFactory = KeysInLootLocationFactory;
