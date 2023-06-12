// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/ResourceStatus.cs
/* eslint-disable */

/**
 * Current value and limit for a limited resource related to a tunnel or tunnel port.
 */
export interface ResourceStatus {
    /**
     * Gets or sets the current value.
     */
    current: number;

    /**
     * Gets or sets the limit enforced by the service, or null if there is no limit.
     *
     * Any requests that would cause the limit to be exceeded may be denied by the
     * service. For HTTP requests, the response is generally a 403 Forbidden status, with
     * details about the limit in the response body.
     */
    limit?: number;

    /**
     * Gets or sets an optional source of the {@link ResourceStatus.limit}, or null if
     * there is no limit.
     */
    limitSource?: string;
}
