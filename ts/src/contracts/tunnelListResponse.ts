// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/TunnelListResponse.cs
/* eslint-disable */

import { Tunnel } from './tunnel';

/**
 * Data contract for response of a list tunnel call.
 */
export interface TunnelListResponse {
    /**
     * List of tunnels
     */
    value: Tunnel[];

    /**
     * Link to get next page of results
     */
    nextLink?: string;
}
