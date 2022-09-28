// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

package com.microsoft.tunnels.management;

import java.io.UnsupportedEncodingException;
import java.net.URLEncoder;
import java.util.Arrays;
import java.util.Collection;
import java.util.HashMap;
import java.util.stream.Collectors;
import java.util.stream.Stream;

import com.microsoft.tunnels.contracts.TunnelAccessControl;

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
   * Note this should not be a _user_ access token (such as AAD or GitHub); use the
   * callback parameter to the `TunnelManagementClient` constructor to
   * supply user access tokens.
   * </p>
   */
  public String accessToken;

  /**
   * Gets or sets additional headers to be included in the request.
   */
  public HashMap<String, String> additionalHeaders;

  /**
   * Gets or sets additional query parameters to be included in the request.
   */
  public HashMap<String, String> additionalQueryParameters;

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
   * Gets or sets an optional list of tags to filter the requested tunnels or ports.
   *
   * Requested tags are compared to the `Tunnel.tags` or `TunnelPort.tags` when calling
   * `TunnelManagementClient.listTunnels` or `TunnelManagementClient.listTunnelPorts` respectively.
   * By default, an item is included if ANY tag matches; set `requireAllTags` to match
   * ALL tags instead.
   */
  public Collection<String> tags;

  /*
   * Gets or sets a flag that indicates whether listed items must match all tags
   * specified in `tags`. If false, an item is included if any tag matches.
   */
  public boolean requireAllTags;

  /**
    * Gets or sets an optional list of token scopes that are requested when retrieving a
    * tunnel or tunnel port object.
    *
    * Each item in the list must be a single scope from `TunnelAccessScopes` or a space-
    * delimited combination of multiple scopes. The service issues an access token for
    * each scope or combination and returns the token(s) in the `Tunnel.accessTokens` or
    * `TunnelPort.accessTokens` dictionary. If the caller does not have permission to get
    * a token for one or more scopes then a token is not returned but the overall request
    * does not fail. Token properties including scopes and expiration may be checked using
    * `TunnelAccessTokenProperties`.
    */
  public Collection<String> tokenScopes;

  /**
   * If true on a create or update request then upon a name conflict, attempt to rename the
   * existing tunnel to null and give the name to the tunnel from the request.
   */
  public boolean forceRename;


  /**
   * Converts tunnel request options to a query string for HTTP requests to the
   * tunnel management API.
   */
  public String toQueryString() {
    var queryOptions = new HashMap<String, Collection<String>>();

    if (this.includePorts) {
      queryOptions.put("includePorts", Arrays.asList("true"));
    }

    if (this.tokenScopes != null) {
      TunnelAccessControl.validateScopes(this.tokenScopes, null, true);
      queryOptions.put("tokenScopes", this.tokenScopes);
    }

    if (this.forceRename) {
      queryOptions.put("forceRename", Arrays.asList("true"));
    }

    if (this.tags != null) {
      queryOptions.put("tags", this.tags);
      if (this.requireAllTags) {
        queryOptions.put("allTags", Arrays.asList("true"));
      }
    }

    if (this.additionalQueryParameters != null) {
      this.additionalQueryParameters.forEach(
        (key, value) -> queryOptions.put(key, Arrays.asList(value))
      );
    }

    Stream<String> encodedParameters = queryOptions.entrySet().stream().map(o -> {
      Stream<String> encodedValues = o.getValue().stream().map(v -> {
        final String encoding = "UTF-8";
        try {
          return URLEncoder.encode(v, encoding);
        } catch (UnsupportedEncodingException e) {
          throw new IllegalArgumentException("Bad encoding: " + encoding);
        }
      });
      return o.getKey() + "=" + encodedValues.collect(Collectors.joining("&"));
    });
    return encodedParameters.collect(Collectors.joining("&"));
  }
}
