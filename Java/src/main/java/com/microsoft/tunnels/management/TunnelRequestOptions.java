package com.microsoft.tunnels.management;

import java.util.Collection;
import java.util.HashMap;

/**
 * TunnelRequestOptions.
 */
public class TunnelRequestOptions {
  /**
   * <p>
   * Gets or sets an access token for the request.
   * </p>
   *
   * <p>
   * Note this should not be a _user_ access token (such as AAD or GitHub); use
   * the
   * callback parameter to the `TunnelManagementHttpClient` constructor to
   * supply user access tokens.
   * </p>
   */
  public String accessToken;

  /**
   * Gets or sets additional headers to be included in the request.
   */
  public HashMap<String, String> additionalHeaders;

  /**
   * <p>
   * Gets or sets a value that indicates whether HTTP redirect responses will be
   * automatically followed.
   * </p>
   *
   * <p>
   * The default is true. If false, a redirect response will cause an error to be
   * thrown,
   * with redirect target URI available at `error.response.headers.location`.
   * </p>
   *
   * <p>
   * The tunnel service often redirects requests to the "home" cluster of the
   * requested
   * tunnel, when necessary to fulfill the request.
   * </p>
   */
  public boolean followRedirects;

  /**
   * Gets or sets a flag that requests tunnel ports when retrieving a tunnel
   * object.
   */
  public boolean includePorts;

  /**
   * Gets or sets an optional list of scopes that should be authorized when
   * retrieving a tunnel or tunnel port object.
   */
  public Collection<String> scopes;

  /**
   * Gets or sets an optional list of token scopes that
   * are requested when retrieving a tunnel or tunnel port object.
   */
  public Collection<String> tokenScopes;
}
