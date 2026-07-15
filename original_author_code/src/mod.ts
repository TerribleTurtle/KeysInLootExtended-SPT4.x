import { DependencyContainer } from "tsyringe";
import { ILogger } from "@spt/models/spt/utils/ILogger";
import { IPostDBLoadMod } from "@spt/models/external/IPostDBLoadMod";
import { DatabaseServer } from "@spt/servers/DatabaseServer";
import { ItemHelper } from "@spt/helpers/ItemHelper";
import { FileSystem } from "@spt/utils/FileSystem";
import { JsonUtil } from "@spt/utils/JsonUtil";
import { ItemPriceService } from "./ItemPriceService";
import { ContainerItemDistributionService } from "./ContainerItemDistributionService";
import { KeysInLootModificationResult } from "./KeysInLootModificationResult";
import { ConfigLoader } from "./ConfigLoader";
import { KeysInLootLocationFactory } from "./KeysInLootLocationFactory";

class KeysInLoot implements IPostDBLoadMod
{
    private logger: ILogger;
    public mod: string;
    public modShortName: string;

    constructor() 
    {
        this.mod = "MusicManiac-KeysInLoot";
        this.modShortName = "KeysInLoot";
    }

    public async postDBLoad(container: DependencyContainer): Promise<void>
    {
        this.logger = container.resolve<ILogger>("WinstonLogger");
        const logger = this.logger;
        logger.info(`[${this.modShortName}] ${this.mod} started loading`);

        // Resolve dependencies
        const itemHelper = container.resolve<ItemHelper>("ItemHelper");
        const db = container.resolve<DatabaseServer>("DatabaseServer");
        const fs = container.resolve<FileSystem>("FileSystem");
        const jsonUtil = container.resolve<JsonUtil>("JsonUtil");
        const configLoader = new ConfigLoader(fs, jsonUtil);
        const itemDistributionService = new ContainerItemDistributionService();
        const keysInLootLocationFactory = new KeysInLootLocationFactory(configLoader, itemDistributionService, itemHelper);

        // Load data
        const config = await configLoader.loadCoreConfig();
        const tables = db.getTables();
        const sptLocations = [
            tables.locations.bigmap, 
            tables.locations.factory4_day,
            tables.locations.factory4_night,
            tables.locations.interchange,
            tables.locations.laboratory, 
            tables.locations.lighthouse,
            tables.locations.rezervbase,
            tables.locations.sandbox,
            tables.locations.sandbox_high,
            tables.locations.shoreline,
            tables.locations.tarkovstreets,
            tables.locations.woods
        ];

        const totalResult = KeysInLootModificationResult.empty();
        for (const sptLocation of sptLocations)
        {
            try 
            {
                const keysInLootLocation = await keysInLootLocationFactory.createKeysInLootLocation(sptLocation);
                const locationResult = keysInLootLocation.modifyContainers();
                totalResult.addResult(locationResult);
            }
            catch (err) 
            {
                console.error(`[${this.modShortName}] Error while processing location ${sptLocation.base._Id}: ${err}`);
                continue;
            }
        }

        
        try 
        {
            // Get distinct list of item templates from totalResult
            const allItems = [...totalResult.adjustedWeights, ...totalResult.addedWeights];
            const distinctItems = allItems.filter((item, index, self) =>
                index === self.findIndex(t => t._id === item._id)
            );
            // Adjust prices for modified or added keys or keycards
            const itemPriceService = new ItemPriceService(tables);
            distinctItems.forEach(item => itemPriceService.adjustFleaMarketPrice(item, config.keyFleaPricesMultiplier));
            distinctItems.forEach(item => itemPriceService.adjustTraderPrice(item, config.keyTraderPricesMultiplier));
        }
        catch (err) 
        {
            console.error(`[${this.modShortName}] Error while processing prices: ${err}`);
        }

        logger.info(`[${this.modShortName}] ${totalResult.adjustedWeights.length} keys weights were adjusted`);
        logger.info(`[${this.modShortName}] different keys were added ${totalResult.addedWeights.length} times to jacket/duffle/dead scav loot`);

        // Adjust jacket cell size
        const itemDB = tables.templates.items;
        itemDB["578f8778245977358849a9b5"]._props.Grids[0]._props.cellsH = config.cellsH;
        itemDB["578f8778245977358849a9b5"]._props.Grids[0]._props.cellsV = config.cellsV;


        logger.success(`[${this.modShortName}] ${this.mod} finished loading`);
    }
}

module.exports = { mod: new KeysInLoot() }