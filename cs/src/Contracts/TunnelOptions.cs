// <copyright file="TunnelOptions.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Microsoft.DevTunnels.Contracts
{
    /// <summary>
    /// Data contract for <see cref="Tunnel"/> or <see cref="TunnelPort"/> options.
    /// </summary>
    public class TunnelOptions
    {
        // Max DNS name length (255) + 1 for ':' + 5 for '65535', max port length.
        private const int HostHeaderMaxLength = 300;

        // TODO: Consider adding an option to enable multiple hosts for a tunnel.
        // The system supports it, but it would only be used in advanced scenarios,
        // and otherwise could cause confusion in case of mistakes.
        // When not enabled, an existing host should be disconnected when a new host connects.

        /// <summary>
        /// Gets or sets a value indicating whether web-forwarding of this tunnel can run on any cluster (region)
        /// without redirecting to the home cluster.
        /// This is only applicable if the tunnel has a name and web-forwarding uses it.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool IsGloballyAvailable { get; set; }

        /// <summary>
        /// Gets or sets a value for `Host` header rewriting to use in web-forwarding of this tunnel or port.
        /// By default, with this property null or empty, web-forwarding uses "localhost" to rewrite the header.
        /// Web-fowarding will use this property instead if it is not null or empty.
        /// Port-level option, if set, takes precedence over this option on the tunnel level.
        /// The option is ignored if IsHostHeaderUnchanged is true.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        [StringLength(HostHeaderMaxLength)]
        public string? HostHeader { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether `Host` header is rewritten or the header value stays intact.
        /// By default, if false, web-forwarding rewrites the host header with the value from HostHeader property or "localhost".
        /// If true, the host header will be whatever the tunnel's web-forwarding host is, e.g. tunnel-name-8080.devtunnels.ms.
        /// Port-level option, if set, takes precedence over this option on the tunnel level.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool IsHostHeaderUnchanged { get; set; }

        /// <summary>
        /// Gets or sets a value for `Origin` header rewriting to use in web-forwarding of this tunnel or port.
        /// By default, with this property null or empty, web-forwarding uses "http(s)://localhost" to rewrite the header.
        /// Web-fowarding will use this property instead if it is not null or empty.
        /// Port-level option, if set, takes precedence over this option on the tunnel level.
        /// The option is ignored if IsOriginHeaderUnchanged is true.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        [StringLength(HostHeaderMaxLength)]
        public string? OriginHeader { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether `Origin` header is rewritten or the header value stays intact.
        /// By default, if false, web-forwarding rewrites the origin header with the value from OriginHeader property or
        /// "http(s)://localhost".
        /// If true, the Origin header will be whatever the tunnel's web-forwarding Origin is, e.g. https://tunnel-name-8080.devtunnels.ms.
        /// Port-level option, if set, takes precedence over this option on the tunnel level.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool IsOriginHeaderUnchanged { get; set; }

        /// <summary>
        /// Gets or sets if inspection is enabled for the tunnel.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool IsInspectionEnabled { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether web requests to a tunnel can use the tunnel web authentication cookie
        /// if they come from a different site. Specifically, this controls whether the tunnel web-forwarding authentication
        /// cookie is marked as SameSite=None. The default is false, which means the cookie is marked as SameSite=Lax.
        /// This only applies to tunnels that require authentication.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool? IsCrossSiteAuthenticationEnabled { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the tunnel web-forwarding authentication cookie is set as
        /// Partitioned (CHIPS). The default is false. This only applies to tunnels that require authentication.
        /// </summary>
        /// <remarks>
        /// A partitioned cookie always also has SameSite=None for compatbility with browsers that do not support partitioning.
        /// </remarks>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool? IsPartitionedSiteAuthenticationEnabled { get; set; }

        /// <summary>
        /// Gets or sets the timeout for HTTP requests to the tunnel or port.
        /// </summary>
        /// <remarks>
        /// The default timeout is 100 seconds. Set this to 0 to disable the timeout. The timeout
        /// will reset when response headers are received or after successfully reading or writing
        /// any request, response, or streaming data like gRPC or WebSockets. TCP keep-alives and
        /// HTTP/2 protocol pings will not reset the timeout, but WebSocket pings will. When a
        /// request times out, the tunnel relay aborts the request and returns 504 Gateway Timeout.
        /// </remarks>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int? RequestTimeoutSeconds { get; set; }
    }
}
