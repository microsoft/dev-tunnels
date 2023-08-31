// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/TunnelV1.cs
/* eslint-disable */

import { TunnelBase } from './tunnelBase';

/**
 * Tunnel type used for tunnel service API version 2023-05-23-preview
 */
export interface TunnelV1 extends TunnelBase {
    /**
     * Gets or sets the ID of the tunnel, unique within the cluster.
     */
    tunnelId?: string;

    /**
     * Gets or sets the tags of the tunnel.
     */
    tags?: string[];
}
