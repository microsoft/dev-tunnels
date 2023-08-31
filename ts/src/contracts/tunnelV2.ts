// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/Tunnel.cs
/* eslint-disable */

import { TunnelBase } from './tunnelBase';

/**
 * Tunnel type used for tunnel service API versions greater than 2023-05-23-preview
 */
export interface TunnelV2 extends TunnelBase {
    /**
     * Gets or sets the ID of the tunnel, unique within the cluster.
     */
    tunnelId?: string;

    /**
     * Gets or sets the tags of the tunnel.
     */
    labels?: string[];
}
