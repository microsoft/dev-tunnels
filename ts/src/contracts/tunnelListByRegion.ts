// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/TunnelListByRegion.cs
/* eslint-disable */

import { ErrorDetail } from './errorDetail';
import { TunnelV2 } from './tunnelV2';

/**
 * Tunnel list by region.
 */
export interface TunnelListByRegion {
    /**
     * Azure region name.
     */
    regionName?: string;

    /**
     * Cluster id in the region.
     */
    clusterId?: string;

    /**
     * List of tunnels.
     */
    value?: TunnelV2[];

    /**
     * Error detail if getting list of tunnels in the region failed.
     */
    error?: ErrorDetail;
}
