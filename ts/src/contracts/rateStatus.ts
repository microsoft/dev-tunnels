// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/RateStatus.cs
/* eslint-disable */

import { ResourceStatus } from './resourceStatus';

/**
 * Current value and limit information for a rate-limited operation related to a tunnel or
 * port.
 */
export interface RateStatus extends ResourceStatus {
    /**
     * Gets or sets the length of each period, in seconds, over which the rate is
     * measured.
     *
     * For rates that are limited by month (or billing period), this value may represent
     * an estimate, since the actual duration may vary by the calendar.
     */
    periodSeconds?: number;

    /**
     * Gets or sets the unix time in seconds when this status will be reset.
     */
    resetTime?: number;
}
