import { ITemplateItem } from "@spt/models/eft/common/tables/ITemplateItem";

export class KeysInLootModificationResult
{
    constructor(
        public adjustedWeights: ITemplateItem[],
        public addedWeights: ITemplateItem[]
    ) 
    {}

    public adjusted(item: ITemplateItem) : void
    {
        this.adjustedWeights.push(item);
    }

    public added(item: ITemplateItem) : void
    {
        this.addedWeights.push(item);
    }

    public addResult(other: KeysInLootModificationResult): void
    {
        this.adjustedWeights.push(...other.adjustedWeights);
        this.addedWeights.push(...other.addedWeights);
    }

    public static empty(): KeysInLootModificationResult
    {
        return new KeysInLootModificationResult([], []);
    }
}