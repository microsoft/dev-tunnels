// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

package com.microsoft.tunnels.management;

/**
 * How the cluster for a tunnel create request was chosen.
 *
 * <p>When a create request does not specify a cluster, the client asks the recommendations
 * API which cluster to use. That call can fail, and when it does the client falls back to
 * global (Traffic Manager) routing, which still works but picks the nearest cluster by
 * latency rather than the recommended one. The fallback is therefore invisible to the
 * caller, so this value records which path was actually taken. It is reported to the
 * service on every create request via the {@code X-Tunnel-Cluster-Source} header.</p>
 *
 * <p>Matches the values used by the C#, Go, and TypeScript SDKs.</p>
 */
public enum TunnelClusterSource {
  /**
   * The caller specified the cluster, so no recommendation was requested.
   */
  EXPLICIT("explicit"),

  /**
   * The recommendations API was called and its cluster was used.
   */
  RECOMMENDED("recommended"),

  /**
   * The recommendations API rejected the caller's token, and the retry without a token
   * succeeded. Routing is correct but the caller was not identified, so it is treated as
   * anonymous and cannot be assigned a service tier. This indicates a token problem on the
   * caller's side that would otherwise be invisible.
   */
  RECOMMENDED_AFTER_AUTH_REJECTED("recommended-after-auth-rejected"),

  /**
   * The recommendations API returned unauthorized even without a token, so global routing
   * was used instead.
   */
  FALLBACK_AUTH_FAILED("fallback-auth-failed"),

  /**
   * The recommendations API returned no cluster, so global routing was used instead.
   */
  FALLBACK_EMPTY("fallback-empty"),

  /**
   * The recommendations API call failed, so global routing was used instead.
   */
  FALLBACK_ERROR("fallback-error");

  private final String headerValue;

  TunnelClusterSource(String headerValue) {
    this.headerValue = headerValue;
  }

  /**
   * Gets the stable wire value sent to the service in the {@code X-Tunnel-Cluster-Source}
   * header, which is what makes the client-side selection path visible in service telemetry.
   *
   * @return The header value for this source.
   */
  public String toHeaderValue() {
    return this.headerValue;
  }

  /**
   * Gets a value indicating whether this source means the recommendations API was bypassed
   * or failed, so the tunnel was placed by global routing rather than by recommendation.
   *
   * @return True if this source is a fallback source.
   */
  public boolean isFallback() {
    return this == FALLBACK_AUTH_FAILED || this == FALLBACK_EMPTY || this == FALLBACK_ERROR;
  }
}
