import { ILocation, IStaticLootDetails } from "@spt/models/eft/common/ILocation";
import { ITemplateItem } from "@spt/models/eft/common/tables/ITemplateItem";
import { ContainerItemDistributionService } from "./ContainerItemDistributionService";
import { KeysInLootModificationResult } from "./KeysInLootModificationResult";
import { IKeysInLootCoreConfig, IKeysInLootLocationConfig } from "./IKeysInLootConfig";
import { KeysInLootContainer } from "./KeysInLootContainer";

export class KeysInLootLocation
{
    constructor(
        private location: ILocation,
        private itemDistributionService: ContainerItemDistributionService,
        private keyItems: ITemplateItem[],
        private keycardItems: ITemplateItem[],
        private coreConfig: IKeysInLootCoreConfig,
        private config: IKeysInLootLocationConfig
    ) 
    {}

    public modifyContainers(): KeysInLootModificationResult
    {
        const result = KeysInLootModificationResult.empty();
        // Modify the default containers in the location to include keys and keycards
        const jacketContainer = this.getJacketContainer();
        if (jacketContainer)
        {
            const jacketResult = jacketContainer.modifyItemDistribution();
            jacketContainer.modifyItemCountDistribution(this.coreConfig.overRideLootDistributionJackets);
            result.addResult(jacketResult);
        }
        const duffleBagContainer = this.getDuffleBagContainer();
        if (duffleBagContainer)
        {
            const duffleBagResult = duffleBagContainer.modifyItemDistribution();
            duffleBagContainer.modifyItemCountDistribution(this.coreConfig.overRideLootDistributionDuffleBags);
            result.addResult(duffleBagResult);
        }
        const deadScavContainer = this.getDeadScavContainer();
        if (deadScavContainer)
        {
            const deadScavResult = deadScavContainer.modifyItemDistribution();
            deadScavContainer.modifyItemCountDistribution(this.coreConfig.overRideLootDistributionDeadScavs);
            result.addResult(deadScavResult);
        }
        
        return result;
    }

    public getJacketContainer() : KeysInLootContainer | null
    {
        const staticLootCotainer = this.tryFindContainerInLocation("578f8778245977358849a9b5", this.location); // Jacket container ID
        if (!staticLootCotainer)
            return null;
        return new KeysInLootContainer(
            `Container Jacket for location ${this.location.base.Name} (${this.location.base._Id})`,
            staticLootCotainer,
            this.itemDistributionService,
            this.keyItems,
            this.keycardItems,
            this.coreConfig,
            this.config.jacketContainer
        );
    }

    public getDuffleBagContainer() : KeysInLootContainer | null
    {
        const staticLootCotainer = this.tryFindContainerInLocation("578f87a3245977356274f2cb", this.location); // Duffel bag container ID
        if (!staticLootCotainer)
            return null;
        return new KeysInLootContainer(
            `Container Duffel Bag for location ${this.location.base.Name} (${this.location.base._Id})`,
            staticLootCotainer,
            this.itemDistributionService,
            this.keyItems,
            this.keycardItems,
            this.coreConfig,
            this.config.duffleBagContainer
        );
    }

    public getDeadScavContainer() : KeysInLootContainer | null
    {
        const staticLootCotainer = this.tryFindContainerInLocation("5909e4b686f7747f5b744fa4", this.location); // Dead scav container ID
        if (!staticLootCotainer)
            return null;
        return new KeysInLootContainer(
            `Container Dead Scav for location ${this.location.base.Name} (${this.location.base._Id})`,
            staticLootCotainer,
            this.itemDistributionService,
            this.keyItems,
            this.keycardItems,
            this.coreConfig,
            this.config.deadScavContainer
        );
    }

    private tryFindContainerInLocation(containerTplId: string, location: ILocation): IStaticLootDetails | null
    {
        if (!location.staticLoot)
        {
            return null;
        }
        const container = location.staticLoot[containerTplId];
        if (!container)
            return null;

        return container;
    }
}
