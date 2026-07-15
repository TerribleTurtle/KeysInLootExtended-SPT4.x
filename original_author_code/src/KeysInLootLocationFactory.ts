import { ITemplateItem } from "@spt/models/eft/common/tables/ITemplateItem";
import { ContainerItemDistributionService } from "./ContainerItemDistributionService";
import { KeysInLootLocation } from "./KeysInLootLocation";
import { ILocation } from "@spt/models/eft/common/ILocation";
import { ConfigLoader } from "./ConfigLoader";
import { IKeysInLootCoreConfig } from "./IKeysInLootConfig";
import { ItemHelper } from "@spt/helpers/ItemHelper";
import { BaseClasses } from "@spt/models/enums/BaseClasses";


export class KeysInLootLocationFactory
{
    private items: ITemplateItem[];
    private keys: ITemplateItem[];
    private keycards: ITemplateItem[];
    private _coreConfigPromise: Promise<IKeysInLootCoreConfig> | null = null;

    constructor(
        private configLoader: ConfigLoader,
        private itemDistributionService: ContainerItemDistributionService,
        private itemHelper: ItemHelper
    ) 
    {
        this.items = this.itemHelper.getItems();
        this.keys = this.items.filter(item => this.itemHelper.isOfBaseclass(item._id, BaseClasses.KEY));
        this.keycards = this.items.filter(item => this.itemHelper.isOfBaseclass(item._id, BaseClasses.KEYCARD));
    }

    public async createKeysInLootLocation(
        location: ILocation
    ): Promise<KeysInLootLocation>
    {
        const coreConfig = await this.getCoreConfig();
        const locationConfig = await this.configLoader.getOrCreateLocationConfig(location, coreConfig);
        return new KeysInLootLocation(
            location, 
            this.itemDistributionService, 
            this.keys, 
            this.keycards,
            coreConfig,
            locationConfig);
    }

    private async getCoreConfig(): Promise<IKeysInLootCoreConfig>
    {
        if (!this._coreConfigPromise)
        {
            this._coreConfigPromise = this.configLoader.loadCoreConfig();
        }
        return this._coreConfigPromise;
    }
}
