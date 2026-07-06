// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/ClusterRecommendation.cs
/* eslint-disable */

import { ClusterAvailability } from './clusterAvailability';

/**
 * A single cluster recommendation with availability and capacity details.
 */
export interface ClusterRecommendation {
    /**
     * Gets or sets the cluster ID, e.g. "usw2".
     */
    clusterId: string;

    /**
     * Gets or sets the Azure location name, e.g. "WestUs2".
     */
    azureLocation: string;

    /**
     * Gets or sets the Azure geography name for data residency, e.g. "United States".
     */
    azureGeo: string;

    /**
     * Gets or sets the cluster URI for API requests.
     */
    clusterUri: string;

    /**
     * Gets or sets the availability status of the cluster.
     */
    availability: ClusterAvailability;

    /**
     * Gets or sets the utilization percentage of the cluster.
     */
    utilizationPercent: number;

    /**
     * Gets or sets a human-readable reason for this recommendation's ranking.
     */
    reason: string;
}
