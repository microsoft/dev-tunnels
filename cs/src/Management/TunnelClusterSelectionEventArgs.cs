// <copyright file="TunnelClusterSelectionEventArgs.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System;

namespace Microsoft.DevTunnels.Management
{
    /// <summary>
    /// How the cluster for a tunnel create request was chosen.
    /// </summary>
    /// <remarks>
    /// When a create request does not specify a cluster, the client asks the recommendations
    /// API which cluster to use. That call can fail, and when it does the client falls back to
    /// global (Traffic Manager) routing, which still works but picks the nearest cluster by
    /// latency rather than the recommended one. The fallback is therefore invisible to the
    /// caller, so this value records which path was actually taken.
    /// </remarks>
    public enum TunnelClusterSource
    {
        /// <summary>
        /// The caller specified the cluster, so no recommendation was requested.
        /// </summary>
        Explicit,

        /// <summary>
        /// The recommendations API was called and its cluster was used.
        /// </summary>
        Recommended,

        /// <summary>
        /// The recommendations API rejected the caller's token, and the retry without a token
        /// succeeded. Routing is correct but the caller was not identified, so it is treated as
        /// anonymous and cannot be assigned a service tier. This indicates a token problem on
        /// the caller's side that would otherwise be invisible.
        /// </summary>
        RecommendedAfterAuthRejected,

        /// <summary>
        /// The recommendations API returned unauthorized even without a token, so global
        /// routing was used instead.
        /// </summary>
        FallbackAuthFailed,

        /// <summary>
        /// The recommendations API returned no cluster, so global routing was used instead.
        /// </summary>
        FallbackEmpty,

        /// <summary>
        /// The recommendations API call failed, so global routing was used instead.
        /// </summary>
        FallbackError,
    }

    /// <summary>
    /// Event args reporting how the cluster for a tunnel create request was chosen.
    /// </summary>
    public class TunnelClusterSelectionEventArgs : EventArgs
    {
        /// <summary>
        /// Creates a new instance of the <see cref="TunnelClusterSelectionEventArgs"/> class.
        /// </summary>
        public TunnelClusterSelectionEventArgs(
            TunnelClusterSource source,
            string? clusterId = null,
            Exception? exception = null)
        {
            this.Source = source;
            this.ClusterId = clusterId;
            this.Exception = exception;
        }

        /// <summary>
        /// Gets how the cluster was chosen.
        /// </summary>
        public TunnelClusterSource Source { get; }

        /// <summary>
        /// Gets the cluster that was selected, if one was.
        /// </summary>
        public string? ClusterId { get; }

        /// <summary>
        /// Gets the failure that caused a fallback, if there was one.
        /// </summary>
        public Exception? Exception { get; }

        /// <summary>
        /// Gets a value indicating whether the recommendations API was bypassed or failed, so
        /// the tunnel was placed by global routing rather than by recommendation.
        /// </summary>
        public bool IsFallback =>
            this.Source == TunnelClusterSource.FallbackAuthFailed ||
            this.Source == TunnelClusterSource.FallbackEmpty ||
            this.Source == TunnelClusterSource.FallbackError;

        /// <summary>
        /// Converts a <see cref="TunnelClusterSource"/> to the stable wire value sent to the
        /// service, which is what makes the client-side path visible in service telemetry.
        /// </summary>
        public static string ToHeaderValue(TunnelClusterSource source) => source switch
        {
            TunnelClusterSource.Explicit => "explicit",
            TunnelClusterSource.Recommended => "recommended",
            TunnelClusterSource.RecommendedAfterAuthRejected => "recommended-after-auth-rejected",
            TunnelClusterSource.FallbackAuthFailed => "fallback-auth-failed",
            TunnelClusterSource.FallbackEmpty => "fallback-empty",
            TunnelClusterSource.FallbackError => "fallback-error",
            _ => "unknown",
        };
    }
}
