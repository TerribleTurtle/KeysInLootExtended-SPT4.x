import { IStaticLootDetails } from "@spt/models/eft/common/ILocation";
import { ITemplateItem } from "@spt/models/eft/common/tables/ITemplateItem";
import { KeysInLootModificationResult } from "./KeysInLootModificationResult";

export class ContainerItemDistributionService
{
    public ensureMinimumRelativeProbabilityForItemsInContainer(
        container: IStaticLootDetails,
        items: ITemplateItem[],
        minimumRelativeProbability: number
    ): KeysInLootModificationResult
    {
        const modification = KeysInLootModificationResult.empty();
        for (const key of items)
        {
            const result = this.ensureMinimumRelativeProbabilityForItemInContainer(container, key, minimumRelativeProbability);
            modification.addResult(result);
        }

        return modification;
    }

    private ensureMinimumRelativeProbabilityForItemInContainer(
        container: IStaticLootDetails, 
        itemTemplate: ITemplateItem, 
        configWeight: number
    ): KeysInLootModificationResult
    {
        const result = KeysInLootModificationResult.empty();
        const foundItem = container.itemDistribution.find(item => item.tpl === itemTemplate._id);
        if (foundItem)
        {
            if (foundItem.relativeProbability < configWeight)
            {
                foundItem.relativeProbability = configWeight;
                result.adjusted(itemTemplate);
            }
        }
        else
        {
            container.itemDistribution.push({
                tpl: itemTemplate._id,
                relativeProbability: configWeight
            });
            result.added(itemTemplate);
        }
        return result;
    }
}
