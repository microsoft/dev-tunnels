// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

/**
 * Describes how the cluster for a tunnel create request was chosen.
 *
 * When a create request does not specify a cluster, the client asks the recommendations
 * API which cluster to use. That call can fail, and when it does the client falls back to
 * global (Traffic Manager) routing, which still works but picks the nearest cluster by
 * latency rather than the recommended one. The fallback is therefore invisible to the
 * caller, so this value records which path was actually taken.
 */
export enum TunnelClusterSource {
    /**
     * The caller specified the cluster, so no recommendation was requested.
     */
    explicit = 'explicit',

    /**
     * The recommendations API was called and its cluster was used.
     */
    recommended = 'recommended',

    /**
     * The recommendations API rejected the caller's token and the retry without a token
     * succeeded. Routing is correct but the caller was not identified, so it is treated as
     * anonymous and cannot be assigned a service tier. This indicates a token problem that
     * would otherwise be invisible.
     */
    recommendedAfterAuthRejected = 'recommended-after-auth-rejected',

    /**
     * The recommendations API returned unauthorized even without a token, so global
     * routing was used instead.
     */
    fallbackAuthFailed = 'fallback-auth-failed',

    /**
     * The recommendations API returned no cluster, so global routing was used instead.
     */
    fallbackEmpty = 'fallback-empty',

    /**
     * The recommendations API call failed, so global routing was used instead.
     */
    fallbackError = 'fallback-error',
}

/**
 * Reports how the cluster for a tunnel create request was chosen.
 */
export interface TunnelClusterSelectionEventArgs {
    /**
     * How the cluster was chosen.
     */
    source: TunnelClusterSource;

    /**
     * The cluster that was selected, if one was.
     */
    clusterId?: string;

    /**
     * The failure that caused a fallback, if there was one.
     */
    error?: Error;
}

/**
 * Gets whether the tunnel was placed by global routing rather than by recommendation.
 */
export function isClusterFallback(source: TunnelClusterSource): boolean {
    return (
        source === TunnelClusterSource.fallbackAuthFailed ||
        source === TunnelClusterSource.fallbackEmpty ||
        source === TunnelClusterSource.fallbackError
    );
}
