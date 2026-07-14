import { IStaticLootDetails, ItemCountDistribution } from "@spt/models/eft/common/ILocation";
import { ITemplateItem } from "@spt/models/eft/common/tables/ITemplateItem";
import { ContainerItemDistributionService } from "./ContainerItemDistributionService";
import { IKeysInLootContainerConfig, IKeysInLootCoreConfig } from "./IKeysInLootConfig";
import { KeysInLootModificationResult } from "./KeysInLootModificationResult";
import { RarityPVEEnum } from "./RarityPVEEnum";

export class KeysInLootContainer
{
    constructor(
        private context: string,
        private container: IStaticLootDetails,
        private itemDistributionService: ContainerItemDistributionService,
        private keyItems: ITemplateItem[],
        private keycardItems: ITemplateItem[],
        private coreConfig: IKeysInLootCoreConfig,
        private config: IKeysInLootContainerConfig
    ) 
    { }

    public modifyItemDistribution(): KeysInLootModificationResult
    {
        const result = KeysInLootModificationResult.empty();

        // For keys
        result.addResult(this.modifyItemRarityDistribution(
            this.keyItems,
            RarityPVEEnum.NOT_EXISTS,
            this.config.key.notExist
        ));
        result.addResult(this.modifyItemRarityDistribution(
            this.keyItems,
            RarityPVEEnum.COMMON,
            this.config.key.common
        ));
        result.addResult(this.modifyItemRarityDistribution(
            this.keyItems,
            RarityPVEEnum.RARE,
            this.config.key.rare
        ));
        result.addResult(this.modifyItemRarityDistribution(
            this.keyItems,
            RarityPVEEnum.SUPER_RARE,
            this.config.key.superRare
        ));

        // For keycards
        result.addResult(this.modifyItemRarityDistribution(
            this.keycardItems,
            RarityPVEEnum.NOT_EXISTS,
            this.config.keycard.notExist
        ));
        result.addResult(this.modifyItemRarityDistribution(
            this.keycardItems,
            RarityPVEEnum.COMMON,
            this.config.keycard.common
        ));
        result.addResult(this.modifyItemRarityDistribution(
            this.keycardItems,
            RarityPVEEnum.RARE,
            this.config.keycard.rare
        ));
        result.addResult(this.modifyItemRarityDistribution(
            this.keycardItems,
            RarityPVEEnum.SUPER_RARE,
            this.config.keycard.superRare
        ));

        return result;
    }

    private modifyItemRarityDistribution(
        sourceItems: ITemplateItem[],
        rarityPvE: string,
        minimumRelativeProbability: number
    ): KeysInLootModificationResult
    {
        try 
        {
            if (minimumRelativeProbability === 0)
                return KeysInLootModificationResult.empty();

            const items = sourceItems.filter(item => item._props?.RarityPvE === rarityPvE);
            
            const result = this.itemDistributionService.ensureMinimumRelativeProbabilityForItemsInContainer(
                this.container,
                items,
                minimumRelativeProbability
            );

            if (this.coreConfig.consoleVerbosity && this.coreConfig.consoleVerbosity === "detailed")
                console.log(`Minimum relative probability ${minimumRelativeProbability} for rarity ${rarityPvE} in ${this.context}: ${result.adjustedWeights.length} adjusted, ${result.addedWeights.length} added`);

            return result;
        }
        catch (error) 
        {
            console.error(`Error modifying item rarity distribution for ${rarityPvE} in ${this.context}:`, error);
            return KeysInLootModificationResult.empty();
        }
    }

    public modifyItemCountDistribution(itemCountDistribution: ItemCountDistribution[]): void
    {
        this.container.itemcountDistribution = itemCountDistribution;
    }
}
