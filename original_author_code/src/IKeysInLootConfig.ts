import { ItemCountDistribution } from "@spt/models/eft/common/ILocation";

export interface IKeysInLootCoreConfig
{
    keyWeight: number;
    keycardWeight: number;
    keyTraderPricesMultiplier: number;
    keyFleaPricesMultiplier: number;
    overrideLootDistribution: boolean;
    overRideLootDistributionJackets: ItemCountDistribution[];
    overRideLootDistributionDuffleBags: ItemCountDistribution[];
    overRideLootDistributionDeadScavs: ItemCountDistribution[];
    cellsH: number;
    cellsV: number;
    enableLocationsConfig: boolean;
    consoleVerbosity?: string;
}

/// <summary>
/// Location configuration for keys in loot.
/// </summary>
export interface IKeysInLootLocationConfig 
{
    jacketContainer: IKeysInLootContainerConfig;
    duffleBagContainer: IKeysInLootContainerConfig;
    deadScavContainer: IKeysInLootContainerConfig;
}

/// <summary>
/// Defines the keys in loot for a container type (for example jacket).
/// </summary>
export interface IKeysInLootContainerConfig 
{
    key: IKeysInLootRarityConfig;
    keycard: IKeysInLootRarityConfig;
}

/// <summary>
/// Defines the minimum relative probability for a base class type (for example key or keycard)
/// to spawn in a container, configurable for rarity. Rarity is based on 'RarityPvE' property on the item template.
/// </summary>>
export interface IKeysInLootRarityConfig 
{
    /// <summary>
    /// The relative probability for a non-existant item to spawn in this container.
    /// </summary>
    notExist: number;
    /// <summary>
    /// The relative probability for a common item to spawn in this container.
    /// </summary>
    common: number;
    /// <summary>
    /// The relative probability for an uncommon item to spawn in this container.
    /// </summary>
    rare: number;
    /// <summary>
    /// The relative probability for a very rare item to spawn in this container.
    /// </summary>
    superRare: number;
}