// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/ClusterDetails.cs
/* eslint-disable */

/**
 * Details of a tunneling service cluster. Each cluster represents an instance of the
 * tunneling service running in a particular Azure region. New tunnels are created in the
 * current region unless otherwise specified.
 */
export interface ClusterDetails {
    /**
     * A cluster identifier based on its region.
     */
    clusterId: string;

    /**
     * The URI of the service cluster.
     */
    uri: string;

    /**
     * The Azure location of the cluster.
     */
    azureLocation: string;
}
