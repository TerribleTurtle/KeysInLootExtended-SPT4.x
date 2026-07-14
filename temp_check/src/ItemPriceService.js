"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.ItemPriceService = void 0;
class ItemPriceService {
    tables;
    constructor(tables) {
        this.tables = tables;
    }
    get handbook() {
        return this.tables.templates.handbook;
    }
    get fleaPrices() {
        return this.tables.templates.prices;
    }
    adjustFleaMarketPrice(itemTemplate, multiplier) {
        const fleaItem = this.fleaPrices[itemTemplate._id];
        if (fleaItem) {
            this.fleaPrices[itemTemplate._id] = Math.round(fleaItem * multiplier);
        }
    }
    adjustTraderPrice(itemTemplate, multiplier) {
        const itemToModify = this.handbook.Items.find(item => item.Id === itemTemplate._id);
        if (itemToModify) {
            itemToModify.Price = Math.round(itemToModify.Price * multiplier);
        }
    }
}
exports.ItemPriceService = ItemPriceService;
