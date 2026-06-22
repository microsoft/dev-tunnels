// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/ClusterAvailability.cs
/* eslint-disable */

/**
 * Availability status of a tunneling service cluster.
 */
export enum ClusterAvailability {
    /**
     * Cluster has sufficient capacity and is fully available.
     */
    Available = 'Available',

    /**
     * Cluster is approaching capacity limits and may experience delays.
     */
    Degraded = 'Degraded',

    /**
     * Cluster is at or beyond capacity and should not be used for new tunnels.
     */
    Unavailable = 'Unavailable',
}
