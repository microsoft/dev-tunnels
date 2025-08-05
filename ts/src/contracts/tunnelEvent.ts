// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/TunnelEvent.cs
/* eslint-disable */

/**
 * Data contract for tunnel client events reported to the tunnel service.
 */
export interface TunnelEvent {
    /**
     * Gets or sets the UTC timestamp of the event (using the client's clock).
     */
    timestamp?: Date;

    /**
     * Gets or sets name of the event. This should be a short descriptive identifier.
     */
    name: string;

    /**
     * Gets or sets the severity of the event, such as {@link TunnelEvent.info}, {@link
     * TunnelEvent.warning}, or {@link TunnelEvent.error}.
     *
     * If not specified, the default severity is "info".
     */
    severity?: string;

    /**
     * Gets or sets optional unstructured details about the event, such as a message or
     * description. For warning or error events this may include a stack trace.
     */
    details?: string;

    /**
     * Gets or sets semi-structured event properties.
     */
    properties?: { [key: string]: string };
}

/**
 * Default event severity.
 */
export const info = 'info';

/**
 * Warning event severity.
 */
export const warning = 'warning';

/**
 * Error event severity.
 */
export const error = 'error';

export const TunnelEvent = {
    info,
    warning,
    error,
};
