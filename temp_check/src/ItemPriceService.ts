import { IHandbookBase } from "@spt/models/eft/common/tables/IHandbookBase";
import { ITemplateItem } from "@spt/models/eft/common/tables/ITemplateItem";
import { IDatabaseTables } from "@spt/models/spt/server/IDatabaseTables";

export class ItemPriceService
{
    private tables: IDatabaseTables;

    constructor(tables: IDatabaseTables)
    {
        this.tables = tables;
    }

    private get handbook(): IHandbookBase
    {
        return this.tables.templates.handbook;
    }

    private get fleaPrices(): Record<string, number>
    {
        return this.tables.templates.prices;
    }

    public adjustFleaMarketPrice(itemTemplate: ITemplateItem, multiplier: number) : void
    {
        const fleaItem = this.fleaPrices[itemTemplate._id];
        if (fleaItem)
        {
            this.fleaPrices[itemTemplate._id] = Math.round(fleaItem * multiplier);
        }
    }

    public adjustTraderPrice(itemTemplate: ITemplateItem, multiplier: number) : void
    {
        const itemToModify = this.handbook.Items.find(item => item.Id === itemTemplate._id);
        if (itemToModify)
        {
            itemToModify.Price = Math.round(itemToModify.Price * multiplier);
        }
    }
}
