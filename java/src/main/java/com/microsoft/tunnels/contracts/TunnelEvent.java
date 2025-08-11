// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../../../../../../cs/src/Contracts/TunnelEvent.cs

package com.microsoft.tunnels.contracts;

import com.google.gson.annotations.Expose;
import java.util.Date;
import java.util.Map;

/**
 * Data contract for tunnel client events reported to the tunnel service.
 */
public class TunnelEvent {
    /**
     * Default event severity.
     */
    public static final String info = "info";

    /**
     * Warning event severity.
     */
    public static final String warning = "warning";

    /**
     * Error event severity.
     */
    public static final String error = "error";

    /**
     * Gets or sets the UTC timestamp of the event (using the client's clock).
     */
    @Expose
    public Date timestamp;

    /**
     * Gets or sets name of the event. This should be a short descriptive identifier.
     */
    @Expose
    public String name;

    /**
     * Gets or sets the severity of the event, such as {@link TunnelEvent#info}, {@link
     * TunnelEvent#warning}, or {@link TunnelEvent#error}.
     *
     * If not specified, the default severity is "info".
     */
    @Expose
    public String severity;

    /**
     * Gets or sets optional unstructured details about the event, such as a message or
     * description. For warning or error events this may include a stack trace.
     */
    @Expose
    public String details;

    /**
     * Gets or sets semi-structured event properties.
     */
    @Expose
    public Map<String, String> properties;
}
