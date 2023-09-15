// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/TunnelListByRegionResponse.cs
/* eslint-disable */

import { TunnelListByRegion } from './tunnelListByRegion';

/**
 * Data contract for response of a list tunnel by region call.
 */
export interface TunnelListByRegionResponse {
    /**
     * List of tunnels
     */
    value?: TunnelListByRegion[];

    /**
     * Link to get next page of results.
     */
    nextLink?: string;
}
