// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/TunnelListResponse.cs
/* eslint-disable */

import { TunnelV2 } from './tunnelV2';

/**
 * Data contract for response of a list tunnel call.
 */
export interface TunnelListResponse {
    /**
     * List of tunnels
     */
    value: TunnelV2[];

    /**
     * Link to get next page of results
     */
    nextLink?: string;
}
