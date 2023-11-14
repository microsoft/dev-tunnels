// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/TunnelPortListResponse.cs
/* eslint-disable */

import { TunnelPort } from './tunnelPort';

/**
 * Data contract for response of a list tunnel ports call.
 */
export interface TunnelPortListResponse {
    /**
     * List of tunnels
     */
    value: TunnelPort[];

    /**
     * Link to get next page of results
     */
    nextLink?: string;
}
