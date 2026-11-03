// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

package com.microsoft.tunnels.management;

import com.google.gson.Gson;
import com.google.gson.reflect.TypeToken;
import com.microsoft.tunnels.contracts.ClusterDetails;
import com.microsoft.tunnels.contracts.ClusterRecommendationResponse;
import com.microsoft.tunnels.contracts.NamedRateStatus;
import com.microsoft.tunnels.contracts.Tunnel;
import com.microsoft.tunnels.contracts.TunnelAccessControlEntry;
import com.microsoft.tunnels.contracts.TunnelAccessScopes;
import com.microsoft.tunnels.contracts.TunnelConnectionMode;
import com.microsoft.tunnels.contracts.TunnelContracts;
import com.microsoft.tunnels.contracts.TunnelEndpoint;
import com.microsoft.tunnels.contracts.TunnelPort;

import java.io.UnsupportedEncodingException;
import java.lang.reflect.Type;
import java.net.URI;
import java.net.URISyntaxException;
import java.net.URLEncoder;
import java.net.http.HttpClient;
import java.net.http.HttpRequest;
import java.net.http.HttpRequest.BodyPublishers;
import java.net.http.HttpResponse;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.Collection;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.CompletionException;
import java.util.concurrent.ExecutionException;
import java.util.function.Supplier;
import java.util.stream.Collectors;

import org.apache.maven.shared.utils.StringUtils;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

/**
 * Implementation of a client that manages tunnels and tunnel ports via the
 * tunnel service management API.
 */
public class TunnelManagementClient implements ITunnelManagementClient {
  private static final String SDK_USER_AGENT = "tunnels-java-sdk/"
      + TunnelManagementClient.class.getPackage().getImplementationVersion();
  private static final String AUTH_HEADER = "Authorization";
  private static final String CONTENT_TYPE_HEADER = "Content-Type";
  private static final String USER_AGENT_HEADER = "User-Agent";

  // Api strings
  private static final String prodServiceUri = "https://global.rel.tunnels.api.visualstudio.com";
  private static final String tunnelsApiPath = "/tunnels";
  private static final String userLimitsApiPath =  "/userlimits";
  private static final String subjectsApiPath = "/subjects";
  private static final String endpointsApiSubPath = "/endpoints";
  private static final String portsApiSubPath = "/ports";
  private String clustersApiPath = "/clusters";
  private static final String recommendationsApiSubPath = "/recommendations";
  private static final String tunnelAuthenticationScheme = "Tunnel";
  private static final String checkTunnelNamePath = ":checkNameAvailability";
  private static final String clusterSourceHeaderName = "X-Tunnel-Cluster-Source";
  private static final int CreateNameRetries = 3;

  // Access Scopes
  private static final String[] ManageAccessTokenScope = {
      TunnelAccessScopes.manage
  };
  private static final String[] HostAccessTokenScope = {
      TunnelAccessScopes.host
  };
  private static final String[] ManagePortsAccessTokenScopes = {
      TunnelAccessScopes.manage,
      TunnelAccessScopes.managePorts,
      TunnelAccessScopes.host,
  };
  private static final String[] ReadAccessTokenScopes = {
      TunnelAccessScopes.manage,
      TunnelAccessScopes.managePorts,
      TunnelAccessScopes.host,
      TunnelAccessScopes.connect
  };

  private static final Logger logger = LoggerFactory.getLogger(TunnelManagementClient.class);

  private final HttpClient httpClient;
  private final ProductHeaderValue[] userAgents;
  private final Supplier<CompletableFuture<String>> userTokenCallback;
  private final String baseAddress;
  private final String apiVersion;
  private final boolean isCustomDomain;

  public static final String[] ApiVersions = {
    "2023-09-27-preview"
  };

  public TunnelManagementClient(ProductHeaderValue[] userAgents, String apiVersion) {
    this(userAgents, null, apiVersion);
  }

  public TunnelManagementClient(
      ProductHeaderValue[] userAgents,
      Supplier<CompletableFuture<String>> userTokenCallback,
      String apiVersion) {
    this(userAgents, userTokenCallback, null, apiVersion);
  }

  /**
   * Initiates a new instance of the TunnelManagementClient class.
   *
   * @param userAgents        List of User-Agent headers given as a
   *                          {@link ProductHeaderValue}.
   * @param userTokenCallback A callback which should resolve to the
   *                          Authentication header value.
   * @param tunnelServiceUri  Uri for the tunnel service. Defaults to the
   *                          production service url.
   */
  public TunnelManagementClient(
      ProductHeaderValue[] userAgents,
      Supplier<CompletableFuture<String>> userTokenCallback,
      String tunnelServiceUri,
      String apiVersion) {
    if (userAgents.length == 0) {
      throw new IllegalArgumentException("user agents cannot be empty");
    }
    if (!Arrays.asList(ApiVersions).contains(apiVersion))  {
      throw new IllegalArgumentException("apiVersion must be one of: " + Arrays.toString(ApiVersions));
    }
    this.userAgents = userAgents;
    this.userTokenCallback = userTokenCallback != null ? userTokenCallback :
        () -> CompletableFuture.completedFuture(null);
    this.baseAddress = tunnelServiceUri != null ? tunnelServiceUri : prodServiceUri;
    this.httpClient = HttpClient.newHttpClient();
    this.apiVersion = apiVersion;
    try {
      this.isCustomDomain = new URI(this.baseAddress).getHost().startsWith("cp.");
    } catch (URISyntaxException e) {
      throw new IllegalArgumentException("Invalid tunnel service URI: " + this.baseAddress, e);
    }
  }

  /**
   * Creates a TunnelManagementClient configured for a custom domain.
   *
   * <p>When a custom domain is configured (e.g., "app.github.dev"), control plane calls
   * are routed to "cp.{domain}" and cluster ID hostname manipulation is skipped.
   *
   * @param customDomain  The custom domain (e.g., "app.github.dev").
   * @param userAgents    User agents.
   * @param userTokenCallback  Optional authentication callback.
   * @param apiVersion    API version.
   * @return A configured TunnelManagementClient instance.
   */
  public static TunnelManagementClient forCustomDomain(
      String customDomain,
      ProductHeaderValue[] userAgents,
      Supplier<CompletableFuture<String>> userTokenCallback,
      String apiVersion) {
    if (StringUtils.isBlank(customDomain)) {
      throw new IllegalArgumentException("Custom domain cannot be blank");
    }
    String serviceUri = "https://cp." + customDomain;
    return new TunnelManagementClient(userAgents, userTokenCallback, serviceUri, apiVersion);
  }

  private <T, U> CompletableFuture<U> requestAsync(
      Tunnel tunnel,
      TunnelRequestOptions options,
      HttpMethod requestMethod,
      URI uri,
      String[] scopes,
      T requestObject,
      Type responseType) {
    return createHttpRequest(tunnel, options, requestMethod, uri, requestObject, scopes)
      .thenCompose(request -> sendAndParseAsync(request, requestMethod, uri, responseType));
  }

  /**
   * Sends an already-built request and parses the response, logging the outcome.
   *
   * <p>Split out of {@link #requestAsync} so that callers that build their own request (such
   * as cluster recommendations, which needs to send an explicit auth override and retry with
   * a different one) can reuse the same send/log/parse logic.</p>
   */
  private <U> CompletableFuture<U> sendAndParseAsync(
      HttpRequest request,
      HttpMethod requestMethod,
      URI uri,
      Type responseType) {
    long startTime = System.nanoTime();
    return this.httpClient.sendAsync(request, HttpResponse.BodyHandlers.ofString())
      .thenApply(response -> {
        long stopTime = System.nanoTime();
        long durationMs = (stopTime - startTime) / 1000000;
        var statusCode = response.statusCode();
        var message = requestMethod + " " + uri + " -> " + statusCode + " (" + durationMs + " ms)";
        if (statusCode >= 200 && statusCode < 300) {
          logger.info(message);
        } else {
          logger.warn(message);
        }
        return response;
      })
      .thenApply(response -> parseResponse(response, responseType));
  }

  private CompletableFuture<String> getAuthHeaderValue(
      Tunnel tunnel,
      TunnelRequestOptions options,
      String[] accessTokenScopes) {
    if (options != null && StringUtils.isNotBlank(options.accessToken)) {
      logger.debug("Authenticating the request with an access token from request options.");
      return CompletableFuture.completedFuture(
        tunnelAuthenticationScheme + " " + options.accessToken);
    }

    return this.userTokenCallback.get().thenApply(userAuthHeader -> {
      if (StringUtils.isNotBlank(userAuthHeader)) {
        logger.debug("Authenticating the request via the user token callback.");
        return userAuthHeader;
      }

      if (tunnel != null && tunnel.accessTokens != null) {
        for (String scope : accessTokenScopes) {
          String accessToken = null;
          for (Map.Entry<String, String> scopeAndToken : tunnel.accessTokens.entrySet()) {
            // Each key may be either a single scope or space-delimited list of scopes.
            if (scopeAndToken.getKey().contains(" ")) {
              var scopes = scopeAndToken.getKey().split(" ");
              if (Arrays.asList(scopes).contains(scope)) {
                accessToken = scopeAndToken.getValue();
                break;
              }
            } else {
              accessToken = scopeAndToken.getValue();
              break;
            }
          }

          if (StringUtils.isNotBlank(accessToken)) {
            logger.debug(
              "Authenticating the request with a '" + scope + "' token from the tunnel object.");
            return tunnelAuthenticationScheme + " " + accessToken;
          }
        }
      }

      logger.debug("The request will be unauthenticated. No authentication token is available.");
      return null;
    });
  }

  private <T> CompletableFuture<HttpRequest> createHttpRequest(
      Tunnel tunnel,
      TunnelRequestOptions options,
      HttpMethod requestMethod,
      URI uri,
      T requestObject,
      String[] accessTokenScopes) {
    return getAuthHeaderValue(tunnel, options, accessTokenScopes).thenApply(authHeaderValue ->
        buildHttpRequest(options, requestMethod, uri, requestObject, authHeaderValue));
  }

  /**
   * Builds an HTTP request using an already-resolved authorization header value.
   *
   * <p>Split out of {@link #createHttpRequest} so that callers that need to resolve
   * authorization themselves (such as cluster recommendations, which sends an explicit token
   * and retries once without it if rejected) can build a request without going through
   * {@link #getAuthHeaderValue}'s tunnel/options-based resolution.</p>
   *
   * @param authHeaderValue The resolved value for the Authorization header, or null/blank to
   *                        send the request unauthenticated.
   */
  private <T> HttpRequest buildHttpRequest(
      TunnelRequestOptions options,
      HttpMethod requestMethod,
      URI uri,
      T requestObject,
      String authHeaderValue) {
    String userAgentString = "";
    for (ProductHeaderValue userAgent : this.userAgents) {
      userAgentString = userAgent.productName
          + "/" + userAgent.version + " " + userAgentString;
    }
    userAgentString = userAgentString + SDK_USER_AGENT;
    var requestBuilder = HttpRequest.newBuilder()
        .uri(uri)
        .header(USER_AGENT_HEADER, userAgentString)
        .header(CONTENT_TYPE_HEADER, "application/json");
    if (StringUtils.isNotBlank(authHeaderValue)) {
      requestBuilder.header(AUTH_HEADER, authHeaderValue);
    }

    if (options != null && options.additionalHeaders != null) {
      options.additionalHeaders.forEach(
        (key, value) -> requestBuilder.header(key, value)
      );
    }

    Gson gson = TunnelContracts.getGson();
    var requestJson = gson.toJson(requestObject);
    var bodyPublisher = requestMethod == HttpMethod.POST || requestMethod == HttpMethod.PUT
        ? BodyPublishers.ofString(requestJson)
        : BodyPublishers.noBody();
    requestBuilder.method(requestMethod.toString(), bodyPublisher);
    return requestBuilder.build();
  }

  private <T> T parseResponse(HttpResponse<String> response, Type typeOfT) {
    if (response.statusCode() >= 300) {
      throw new HttpResponseException(
          "Error sending request, status code: " + response.statusCode(),
          response.statusCode(),
          response.body());
    }
    Gson gson = TunnelContracts.getGson();
    return gson.fromJson(response.body(), typeOfT);
  }

  private URI buildUri(Tunnel tunnel, TunnelRequestOptions options, boolean isTunnelCreate) {
    return buildUri(tunnel, options, null, null, isTunnelCreate);
  }

  private URI buildUri(Tunnel tunnel, TunnelRequestOptions options, String path) {
    return buildUri(tunnel, options, path, null, false);
  }

  private URI buildUri(Tunnel tunnel, TunnelRequestOptions options, String path, String query, boolean isTunnelCreate) {
    if (tunnel == null) {
      throw new Error("Tunnel must be specified");
    }

    String tunnelPath;
    if ((StringUtils.isNotBlank(tunnel.clusterId) || isTunnelCreate) && StringUtils.isNotBlank(tunnel.tunnelId)) {
      tunnelPath = tunnelsApiPath + "/" + tunnel.tunnelId;
    } else {
      if (tunnel.name == null) {
        throw new Error("Tunnel object must include either a name or tunnel ID and cluster ID.");
      }
      if (StringUtils.isBlank(tunnel.domain)) {
        tunnelPath = tunnelsApiPath + "/" + tunnel.name;
      } else {
        tunnelPath = tunnelsApiPath + "/" + tunnel.name + "." + tunnel.domain;
      }
    }
    if (StringUtils.isNotBlank(path)) {
      tunnelPath += path;
    }
    return buildUri(tunnel.clusterId, tunnelPath, options, query);
  }

  private URI buildUri(String clusterId,
      String path,
      TunnelRequestOptions options,
      String query) {
    URI baseAddress;
    try {
      baseAddress = new URI(this.baseAddress);
    } catch (URISyntaxException e) {
      throw new Error("Error parsing URI: " + this.baseAddress);
    }
    String host = null;
    int port = baseAddress.getPort();

		// tunnels.local.api.visualstudio.com resolves to localhost (for local development).
    if (StringUtils.isNotBlank(clusterId) && !this.isCustomDomain) {
      if (!baseAddress.getHost().equals("localhost")
          && !baseAddress.getHost().equals("tunnels.local.api.visualstudio.com")
          && !baseAddress.getHost().startsWith(clusterId + ".")) {
        host = (clusterId + "." + baseAddress.getHost()).replace("global.", "");
      } else if (baseAddress.getScheme().equals("https")
          && clusterId.startsWith("localhost")
          && baseAddress.getPort() % 10 > 0) {
        var clusterNumber = Integer.parseInt(clusterId.substring("localhost".length()));
        if (clusterNumber > 0 && clusterNumber < 10) {
          port = baseAddress.getPort() - (baseAddress.getPort() % 10) + clusterNumber;
        }
      }
    }

    String queryString = "";
    if (options != null) {
      queryString = options.toQueryString();
    }
    if (query != null) {
      queryString += StringUtils.isBlank(queryString) ? query : "&" + query;
    }

    queryString += StringUtils.isBlank(queryString) ? "api-version="+this.apiVersion : "&api-version="+this.apiVersion;


    try {
      return new URI(
          baseAddress.getScheme(),
          null /* userInfo */,
          host != null ? host : baseAddress.getHost(),
          port,
          path,
          StringUtils.isNotBlank(queryString) ? queryString : null,
          null /* fragment */
      );
    } catch (URISyntaxException e) {
      throw new Error("Error building URI: " + e.getMessage());
    }
  }

  @Override
  public CompletableFuture<Collection<Tunnel>> listTunnelsAsync(
      String clusterId,
      String domain,
      TunnelRequestOptions options) {
    var query = StringUtils.isBlank(clusterId) ? "global=true" : null;
    var requestUri = this.buildUri(clusterId, tunnelsApiPath, options, query);
    final Type responseType = new TypeToken<Collection<Tunnel>>() {
    }.getType();
    return requestAsync(
        null /* tunnel */,
        options,
        HttpMethod.GET,
        requestUri,
        ReadAccessTokenScopes,
        null /* requestObject */,
        responseType);
  }

  @Override
  public CompletableFuture<Tunnel> getTunnelAsync(Tunnel tunnel, TunnelRequestOptions options) {
    var requestUri = buildUri(tunnel, options, null, null, false);
    final Type responseType = new TypeToken<Tunnel>() {
    }.getType();
    return requestAsync(
        tunnel,
        options,
        HttpMethod.GET,
        requestUri,
        ReadAccessTokenScopes,
        null,
        responseType);
  }

  @Override
  public CompletableFuture<Tunnel> createTunnelAsync(Tunnel tunnel, TunnelRequestOptions options) {
    var generatedId = tunnel.tunnelId == null;
    if (generatedId) {
      tunnel.tunnelId = IdGeneration.generateTunnelId();
    }

    // Never mutate the caller's options (or its additionalHeaders map): create needs to add
    // its own headers, including one that must not be spoofable by the caller.
    var requestOptions = copyRequestOptionsForCreate(options);

    // If the caller didn't specify a cluster, auto-select one via the recommendations API.
    // Failures fall back to global routing.
    CompletableFuture<TunnelClusterSource> clusterSourceFuture =
        StringUtils.isBlank(tunnel.clusterId)
            ? selectClusterAsync(tunnel, requestOptions.requiredGeo)
            : CompletableFuture.completedFuture(TunnelClusterSource.EXPLICIT);

    return clusterSourceFuture.thenCompose(clusterSource -> {
      // Report the client-side selection path to the service. Recommendation fallbacks are
      // otherwise invisible in service telemetry: a create that fell back looks identical to
      // one that was never recommended at all.
      requestOptions.additionalHeaders.put(
          clusterSourceHeaderName, clusterSource.toHeaderValue());

      var uri = buildUri(tunnel, requestOptions, true);
      final Type responseType = new TypeToken<Tunnel>() {
      }.getType();
      for (int i = 0; i <= CreateNameRetries; i++){
        try {
          return requestAsync(
            tunnel,
            requestOptions,
            HttpMethod.PUT,
            uri,
            ManageAccessTokenScope,
            convertTunnelForRequest(tunnel),
            responseType);
        }
        catch (Exception e) {
          if (generatedId) {
            tunnel.tunnelId = IdGeneration.generateTunnelId();;
          }
          else{
            throw e;
          }
        }
      }

      return requestAsync(
            tunnel,
            requestOptions,
            HttpMethod.PUT,
            uri,
            ManageAccessTokenScope,
            convertTunnelForRequest(tunnel),
            responseType);
    });
  }

  /**
   * Makes a copy of the given request options suitable for a create request, without
   * mutating the caller's instance.
   *
   * <p>The copy gets the "If-Not-Match" header that tunnel creation has always sent
   * (preserving the existing header name as-is; it does not match the standard
   * "If-None-Match" precondition header, but fixing that is a separate, unrelated behavior
   * change). Any caller-supplied {@code X-Tunnel-Cluster-Source} header is removed
   * case-insensitively so it cannot be spoofed — the client always sets that header itself,
   * to report how the cluster was actually selected.</p>
   */
  private TunnelRequestOptions copyRequestOptionsForCreate(TunnelRequestOptions options) {
    var copy = copyRequestOptions(options);
    copy.additionalHeaders.put("If-Not-Match", "*");
    return copy;
  }

  /**
   * Makes a shallow copy of the given request options (or a fresh default instance if null),
   * with a new, mutable {@code additionalHeaders} map that excludes any caller-supplied
   * {@code X-Tunnel-Cluster-Source} header (case-insensitively), so a copy is always safe to
   * mutate without affecting the caller or letting the caller spoof that header.
   */
  private TunnelRequestOptions copyRequestOptions(TunnelRequestOptions options) {
    var copy = new TunnelRequestOptions();
    if (options != null) {
      copy.accessToken = options.accessToken;
      copy.additionalQueryParameters = options.additionalQueryParameters == null
          ? null : new HashMap<>(options.additionalQueryParameters);
      copy.followRedirects = options.followRedirects;
      copy.includePorts = options.includePorts;
      copy.includeAccessControl = options.includeAccessControl;
      copy.labels = options.labels;
      copy.requireAllLabels = options.requireAllLabels;
      copy.tokenScopes = options.tokenScopes;
      copy.forceRename = options.forceRename;
      copy.limit = options.limit;
      copy.requiredGeo = options.requiredGeo;
    }

    copy.additionalHeaders = new HashMap<>();
    if (options != null && options.additionalHeaders != null) {
      options.additionalHeaders.forEach((key, value) -> {
        if (!clusterSourceHeaderName.equalsIgnoreCase(key)) {
          copy.additionalHeaders.put(key, value);
        }
      });
    }
    return copy;
  }

  /**
   * Attempts to auto-select a cluster for a new tunnel via the recommendations API, setting
   * {@code tunnel.clusterId} when a usable recommendation comes back.
   *
   * <p>Failures fall back to global (Traffic Manager) routing, which still succeeds but
   * picks the nearest cluster by latency rather than the recommended one — a silent fallback
   * that would otherwise look identical to normal operation. This never fails: the outcome is
   * instead reported via the returned {@link TunnelClusterSource}, which becomes the create
   * request's {@code X-Tunnel-Cluster-Source} header.</p>
   */
  private CompletableFuture<TunnelClusterSource> selectClusterAsync(
      Tunnel tunnel, String requiredGeo) {
    return getClusterRecommendationsInternalAsync(null /* preferredClusterId */, requiredGeo)
        .handle((result, throwable) -> {
          if (throwable != null) {
            Throwable cause = unwrapAsyncException(throwable);
            TunnelClusterSource source = cause instanceof HttpResponseException
                && isUnauthorizedOrForbidden((HttpResponseException) cause)
                ? TunnelClusterSource.FALLBACK_AUTH_FAILED
                : TunnelClusterSource.FALLBACK_ERROR;
            return source;
          }

          if (result.response != null
              && StringUtils.isNotBlank(result.response.recommendedClusterId)) {
            tunnel.clusterId = result.response.recommendedClusterId;
            return result.authRejected
                ? TunnelClusterSource.RECOMMENDED_AFTER_AUTH_REJECTED
                : TunnelClusterSource.RECOMMENDED;
          }

          return TunnelClusterSource.FALLBACK_EMPTY;
        });
  }

  private Tunnel convertTunnelForRequest(Tunnel tunnel) {
    Tunnel converted = new Tunnel();
    converted.tunnelId = tunnel.tunnelId;
    converted.name = tunnel.name;
    converted.domain = tunnel.domain;
    converted.description = tunnel.description;
    converted.labels = tunnel.labels;
    converted.options = tunnel.options;
    converted.accessControl = tunnel.accessControl;
    converted.customExpiration = tunnel.customExpiration;
    converted.endpoints = tunnel.endpoints;
    if (tunnel.accessControl != null && tunnel.accessControl.entries != null) {
      List<TunnelAccessControlEntry> entries = Arrays.asList(tunnel.accessControl.entries);
      List<TunnelAccessControlEntry> filtered = entries.stream()
          .filter((e) -> !e.isInherited)
          .collect(Collectors.toList());
      converted.accessControl.entries = filtered.toArray(new TunnelAccessControlEntry[0]);
    }
    if (tunnel.ports == null) {
      converted.ports = null;
    } else {
      var convertedPorts = new TunnelPort[tunnel.ports.length];
      for (int i = 0; i < tunnel.ports.length; i++) {
        convertedPorts[i] = convertTunnelPortForRequest(tunnel, tunnel.ports[i]);
      }
      converted.ports = convertedPorts;
    }
    return converted;
  }

  @Override
  public CompletableFuture<Tunnel> updateTunnelAsync(Tunnel tunnel, TunnelRequestOptions options) {
    options = options == null ? new TunnelRequestOptions() : options;
    options.additionalHeaders = options.additionalHeaders == null
        ? new HashMap<>() : options.additionalHeaders;
    options.additionalHeaders.put("If-Match", "*");
    var uri = buildUri(tunnel, options, true);
    final Type responseType = new TypeToken<Tunnel>() {
    }.getType();
    return requestAsync(
        tunnel,
        options,
        HttpMethod.PUT,
        uri,
        ManageAccessTokenScope,
        convertTunnelForRequest(tunnel),
        responseType);
  }

  @Override
  public CompletableFuture<Tunnel> createOrUpdateTunnelAsync(Tunnel tunnel, TunnelRequestOptions options) {
    var generatedId = tunnel.tunnelId == null;
    if (generatedId) {
      tunnel.tunnelId = IdGeneration.generateTunnelId();
    }

    var uri = buildUri(tunnel, options, true);
    final Type responseType = new TypeToken<Tunnel>() {
    }.getType();
    for (int i = 0; i <= CreateNameRetries; i++){
      try {
        return requestAsync(
          tunnel,
          options,
          HttpMethod.PUT,
          uri,
          ManageAccessTokenScope,
          convertTunnelForRequest(tunnel),
          responseType);
      }
      catch (Exception e) {
        if (generatedId) {
          tunnel.tunnelId = IdGeneration.generateTunnelId();;
        }
        else{
          throw e;
        }
      }
    }

    return requestAsync(
          tunnel,
          options,
          HttpMethod.PUT,
          uri,
          ManageAccessTokenScope,
          convertTunnelForRequest(tunnel),
          responseType);
  }

  public CompletableFuture<Boolean> deleteTunnelAsync(Tunnel tunnel) {
    return deleteTunnelAsync(tunnel, null);
  }

  @Override
  public CompletableFuture<Boolean> deleteTunnelAsync(Tunnel tunnel, TunnelRequestOptions options) {
    var uri = buildUri(tunnel, options, true);
    final Type responseType = new TypeToken<Boolean>() {
    }.getType();
    return requestAsync(
        tunnel,
        options,
        HttpMethod.DELETE,
        uri,
        ManageAccessTokenScope,
        convertTunnelForRequest(tunnel),
        responseType);
  }

  @Override
  public CompletableFuture<TunnelEndpoint> updateTunnelEndpointAsync(
      Tunnel tunnel,
      TunnelEndpoint endpoint,
      TunnelRequestOptions options) {
    if (endpoint == null) {
      throw new IllegalArgumentException("Endpoint must not be null.");
    }
    if (StringUtils.isBlank(endpoint.id)) {
      throw new IllegalArgumentException("Endpoint id must not be null.");
    }

    var path = endpointsApiSubPath + "/" + endpoint.id;
    var uri = buildUri(
        tunnel,
        options,
        path,
        "connectionMode=" + endpoint.connectionMode.toString(),
        false);

    final Type responseType = new TypeToken<TunnelEndpoint>() {
    }.getType();
    CompletableFuture<TunnelEndpoint> result = requestAsync(
        tunnel,
        options,
        HttpMethod.PUT,
        uri,
        HostAccessTokenScope,
        endpoint,
        responseType);

    if (tunnel.endpoints != null) {
      var updatedEndpoints = new ArrayList<TunnelEndpoint>();
      for (TunnelEndpoint e : tunnel.endpoints) {
        if (e.id != endpoint.id) {
          updatedEndpoints.add(e);
        }
      }
      updatedEndpoints.add(result.join());
      tunnel.endpoints = updatedEndpoints
          .toArray(new TunnelEndpoint[updatedEndpoints.size()]);
    }
    return result;
  }

  @Override
  public CompletableFuture<Boolean> deleteTunnelEndpointsAsync(
      Tunnel tunnel,
      String id,
      TunnelRequestOptions options) {
    if (id == null) {
      throw new IllegalArgumentException("id must not be null");
    }
    var path = endpointsApiSubPath + "/" + id;
    var uri = buildUri(tunnel, options, path);

    final Type responseType = new TypeToken<Boolean>() {
    }.getType();
    CompletableFuture<Boolean> result = requestAsync(
        tunnel,
        options,
        HttpMethod.DELETE,
        uri,
        ManageAccessTokenScope,
        convertTunnelForRequest(tunnel),
        responseType);

    if (tunnel.endpoints != null) {
      var updatedEndpoints = new ArrayList<TunnelEndpoint>();
      for (TunnelEndpoint e : tunnel.endpoints) {
        if (e.id != id) {
          updatedEndpoints.add(e);
        }
      }
      tunnel.endpoints = updatedEndpoints
          .toArray(new TunnelEndpoint[updatedEndpoints.size()]);
    }
    return result;
  }

  @Override
  public CompletableFuture<Collection<TunnelPort>> listTunnelPortsAsync(
      Tunnel tunnel,
      TunnelRequestOptions options) {
    var uri = buildUri(tunnel, options, portsApiSubPath);

    final Type responseType = new TypeToken<Collection<TunnelPort>>() {
    }.getType();
    return requestAsync(
        tunnel,
        options,
        HttpMethod.GET,
        uri,
        ReadAccessTokenScopes,
        null /* requestObject */,
        responseType);
  }

  @Override
  public CompletableFuture<TunnelPort> getTunnelPortAsync(
      Tunnel tunnel,
      int portNumber,
      TunnelRequestOptions options) {
    var uri = buildUri(tunnel, options, portsApiSubPath + "/" + portNumber);
    final Type responseType = new TypeToken<TunnelPort>() {
    }.getType();
    return requestAsync(
        tunnel,
        options,
        HttpMethod.GET,
        uri,
        ReadAccessTokenScopes,
        null,
        responseType);
  }

  @Override
  public CompletableFuture<TunnelPort> createTunnelPortAsync(
      Tunnel tunnel,
      TunnelPort tunnelPort,
      TunnelRequestOptions options) {
    if (tunnel == null) {
      throw new IllegalArgumentException("Tunnel must not be null.");
    }
    if (tunnelPort == null) {
      throw new IllegalArgumentException("Tunnel port must be specified");
    }
    options = options == null ? new TunnelRequestOptions() : options;
    options.additionalHeaders = options.additionalHeaders == null
        ? new HashMap<>() : options.additionalHeaders;
    options.additionalHeaders.put("If-Not-Match", "*");

    var path = portsApiSubPath + "/" + tunnelPort.portNumber;
    var uri = buildUri(
        tunnel,
        options,
        path);
    final Type responseType = new TypeToken<TunnelPort>() {
    }.getType();
    CompletableFuture<TunnelPort> result = requestAsync(
        tunnel,
        options,
        HttpMethod.PUT,
        uri,
        ManagePortsAccessTokenScopes,
        convertTunnelPortForRequest(tunnel, tunnelPort),
        responseType);

    if (tunnel.ports == null){
      tunnel.ports = new TunnelPort[0];
    }
    var updatedPorts = new ArrayList<TunnelPort>();
    for (TunnelPort p : tunnel.ports) {
      if (p.portNumber != tunnelPort.portNumber) {
        updatedPorts.add(p);
      }
    }
    updatedPorts.add(result.join());
    updatedPorts.sort((p1, p2) -> Integer.compare(p1.portNumber, p2.portNumber));
    tunnel.ports = updatedPorts.toArray(new TunnelPort[updatedPorts.size()]);
    return result;
  }

  private TunnelPort convertTunnelPortForRequest(Tunnel tunnel, TunnelPort tunnelPort) {
    if (tunnelPort.clusterId != null
        && tunnel.clusterId != null
        && !tunnelPort.clusterId.equals(tunnel.clusterId)) {
      throw new IllegalArgumentException(
          "Tunnel port cluster ID does not match tunnel.");
    }

    if (tunnelPort.tunnelId != null
        && tunnel.tunnelId != null
        && !tunnelPort.tunnelId.equals(tunnel.tunnelId)) {
      throw new IllegalArgumentException(
          "Tunnel port tunnel ID does not match tunnel.");
    }

    var converted = new TunnelPort();
    converted.portNumber = tunnelPort.portNumber;
    converted.protocol = tunnelPort.protocol;
    converted.isDefault = tunnelPort.isDefault;
    converted.description = tunnelPort.description;
    converted.labels = tunnelPort.labels;
    converted.sshUser = tunnelPort.sshUser;
    converted.options = tunnelPort.options;
    if (tunnelPort.accessControl != null && tunnelPort.accessControl.entries != null) {
      List<TunnelAccessControlEntry> entries = Arrays.asList(tunnel.accessControl.entries);
      List<TunnelAccessControlEntry> filtered = entries.stream()
          .filter((e) -> !e.isInherited)
          .collect(Collectors.toList());
      converted.accessControl = tunnelPort.accessControl;
      converted.accessControl.entries = filtered.toArray(new TunnelAccessControlEntry[0]);
    }
    return converted;
  }

  @Override
  public CompletableFuture<TunnelPort> updateTunnelPortAsync(
      Tunnel tunnel,
      TunnelPort tunnelPort,
      TunnelRequestOptions options) {
    if (tunnel == null) {
      throw new IllegalArgumentException("Tunnel must not be null.");
    }
    if (tunnelPort == null) {
      throw new IllegalArgumentException("Tunnel port must not be null.");
    }

    options = options == null ? new TunnelRequestOptions() : options;
    options.additionalHeaders = options.additionalHeaders == null
        ? new HashMap<>() : options.additionalHeaders;
    options.additionalHeaders.put("If-Match", "*");

    if (StringUtils.isNotBlank(tunnelPort.clusterId)
        && StringUtils.isNotBlank(tunnel.clusterId)
        && tunnelPort.clusterId != tunnel.clusterId) {
      throw new Error("Tunnel port cluster ID is not consistent.");
    }

    var path = portsApiSubPath + "/" + tunnelPort.portNumber;
    var uri = buildUri(
        tunnel,
        options,
        path);

    final Type responseType = new TypeToken<TunnelPort>() {
    }.getType();
    CompletableFuture<TunnelPort> result = requestAsync(
        tunnel,
        options,
        HttpMethod.PUT,
        uri,
        ManagePortsAccessTokenScopes,
        convertTunnelPortForRequest(tunnel, tunnelPort),
        responseType);

    if (tunnel.ports == null){
      tunnel.ports = new TunnelPort[0];
    }
    var updatedPorts = new ArrayList<TunnelPort>();
    for (TunnelPort p : tunnel.ports) {
      if (p.portNumber != tunnelPort.portNumber) {
        updatedPorts.add(p);
      }
    }
    updatedPorts.add(result.join());
    updatedPorts.sort((p1, p2) -> Integer.compare(p1.portNumber, p2.portNumber));
    tunnel.ports = updatedPorts.toArray(new TunnelPort[updatedPorts.size()]);
    return result;
  }

  @Override
  public CompletableFuture<TunnelPort> createOrUpdateTunnelPortAsync(
      Tunnel tunnel,
      TunnelPort tunnelPort,
      TunnelRequestOptions options) {
    if (tunnel == null) {
      throw new IllegalArgumentException("Tunnel must not be null.");
    }
    if (tunnelPort == null) {
      throw new IllegalArgumentException("Tunnel port must be specified");
    }
    var path = portsApiSubPath + "/" + tunnelPort.portNumber;
    var uri = buildUri(
        tunnel,
        options,
        path,
        null,
        false);
    final Type responseType = new TypeToken<TunnelPort>() {
    }.getType();
    CompletableFuture<TunnelPort> result = requestAsync(
        tunnel,
        options,
        HttpMethod.PUT,
        uri,
        ManagePortsAccessTokenScopes,
        convertTunnelPortForRequest(tunnel, tunnelPort),
        responseType);

    if (tunnel.ports == null){
      tunnel.ports = new TunnelPort[0];
    }
    var updatedPorts = new ArrayList<TunnelPort>();
    for (TunnelPort p : tunnel.ports) {
      if (p.portNumber != tunnelPort.portNumber) {
        updatedPorts.add(p);
      }
    }
    updatedPorts.add(result.join());
    updatedPorts.sort((p1, p2) -> Integer.compare(p1.portNumber, p2.portNumber));
    tunnel.ports = updatedPorts.toArray(new TunnelPort[updatedPorts.size()]);
    return result;
  }

  @Override
  public CompletableFuture<Boolean> deleteTunnelPortAsync(
      Tunnel tunnel,
      int portNumber,
      TunnelRequestOptions options) {
    if (tunnel == null) {
      throw new IllegalArgumentException("Tunnel must not be null.");
    }

    var path = portsApiSubPath + "/" + portNumber;
    var uri = buildUri(tunnel, options, path);

    final Type responseType = new TypeToken<Boolean>() {
    }.getType();
    CompletableFuture<Boolean> result = requestAsync(
        tunnel,
        options,
        HttpMethod.DELETE,
        uri,
        ManagePortsAccessTokenScopes,
        null /* requestObject */,
        responseType);

    if (tunnel.ports != null) {
      var updatedPorts = new ArrayList<TunnelPort>();
      for (TunnelPort p : tunnel.ports) {
        if (p.portNumber != portNumber) {
          updatedPorts.add(p);
        }
      }
      updatedPorts.sort((p1, p2) -> Integer.compare(p1.portNumber, p2.portNumber));
      tunnel.ports = updatedPorts.toArray(new TunnelPort[updatedPorts.size()]);
    }
    return result;
  }

  /**
   * {@inheritDoc}
   */
  public CompletableFuture<Collection<ClusterDetails>> listClustersAsync() {
    URI uri;
    try {
      uri = new URI(this.baseAddress + this.clustersApiPath);
      final Type responseType = new TypeToken<Collection<ClusterDetails>>() {
      }.getType();
      return requestAsync(
          null,
          null,
          HttpMethod.GET,
          uri,
          null,
          null,
          responseType);
    } catch (URISyntaxException e) {
      throw new Error("Error parsing URI: " + this.baseAddress + this.clustersApiPath);
    }
  }

    /**
   * {@inheritDoc}
   */
  public CompletableFuture<Boolean> checkNameAvailabilityAsync(String name) {
    URI uri;
    try {
      uri = new URI(this.baseAddress + this.tunnelsApiPath + name + this.checkTunnelNamePath);
      final Type responseType = new TypeToken<Boolean>() {
      }.getType();
      name = URLEncoder.encode(name, "UTF-8");
      return requestAsync(
          null,
          null,
          HttpMethod.GET,
          uri,
          null,
          null,
          responseType);
    } catch (URISyntaxException e) {
      throw new Error("Error parsing URI: " + this.baseAddress + this.tunnelsApiPath + name + this.checkTunnelNamePath);
    }
    catch (UnsupportedEncodingException e) {
      throw new Error("Error encoding tunnel name: " + name);
    }
  }

  @Override
  public CompletableFuture<Collection<NamedRateStatus>> listUserLimitsAsync() {
    URI uri;
    try {
      uri = new URI(this.baseAddress + TunnelManagementClient.userLimitsApiPath);
      final Type responseType = new TypeToken<Collection<NamedRateStatus>>() {}.getType();
      return requestAsync(
          null,
          null,
          HttpMethod.GET,
          uri,
          null,
          null,
          responseType);
    } catch (URISyntaxException e) {
      throw new Error("Error parsing URI: " + this.baseAddress + TunnelManagementClient.userLimitsApiPath);
    }
  }

  /**
   * {@inheritDoc}
   */
  @Override
  public CompletableFuture<ClusterRecommendationResponse> getClusterRecommendationsAsync(
      String preferredClusterId,
      String requiredGeo) {
    return getClusterRecommendationsInternalAsync(preferredClusterId, requiredGeo)
        .thenApply(result -> result.response);
  }

  /**
   * Requests cluster recommendations, reporting whether the caller's token was rejected.
   *
   * <p>The token from the user token callback is resolved once and sent explicitly on the
   * request (rather than through {@link #getAuthHeaderValue}/{@link #createHttpRequest}, so
   * that it can be retried with a different, explicit auth override). This is so the service
   * can identify the caller and apply their service tier. If the token is rejected with a 401
   * or 403 the request is retried once without it, because the service rejects a bad token
   * before the controller runs and does not fall back to treating the caller as anonymous.
   * Without the retry, one expired token would silently disable recommendation-based routing
   * for that caller. A response with no token to offer is an ordinary anonymous request, not
   * a rejected one, so it is not retried. Failures other than a 401/403 on the authenticated
   * attempt (500s, network errors, parse failures) are also not retried.</p>
   *
   * <p>Uses {@code handle} plus {@code thenCompose} rather than {@code exceptionallyCompose}
   * (Java 12+) to stay on the Java 11 API surface this project targets.</p>
   */
  private CompletableFuture<ClusterRecommendationResult> getClusterRecommendationsInternalAsync(
      String preferredClusterId,
      String requiredGeo) {
    var uri = buildClusterRecommendationsUri(preferredClusterId, requiredGeo);
    final Type responseType = new TypeToken<ClusterRecommendationResponse>() {
    }.getType();

    CompletableFuture<String> userTokenFuture;
    try {
      userTokenFuture = this.userTokenCallback.get();
    } catch (RuntimeException exception) {
      userTokenFuture = new CompletableFuture<>();
      userTokenFuture.completeExceptionally(exception);
    }

    return userTokenFuture.thenCompose(userAuthHeader -> {
      if (StringUtils.isBlank(userAuthHeader)) {
        // No token to offer, so this is an ordinary anonymous request rather than a
        // rejected one.
        var request = buildHttpRequest(null, HttpMethod.GET, uri, null, null);
        CompletableFuture<ClusterRecommendationResponse> response =
            sendAndParseAsync(request, HttpMethod.GET, uri, responseType);
        return response.thenApply(r -> new ClusterRecommendationResult(r, false));
      }

      var authedRequest = buildHttpRequest(null, HttpMethod.GET, uri, null, userAuthHeader);
      CompletableFuture<ClusterRecommendationResponse> authedResponse =
          sendAndParseAsync(authedRequest, HttpMethod.GET, uri, responseType);

      CompletableFuture<CompletableFuture<ClusterRecommendationResult>> handled =
          authedResponse.handle((response, throwable) -> {
            if (throwable == null) {
              return CompletableFuture.completedFuture(
                  new ClusterRecommendationResult(response, false));
            }

            Throwable cause = unwrapAsyncException(throwable);
            if (cause instanceof HttpResponseException
                && isUnauthorizedOrForbidden((HttpResponseException) cause)) {
              // The service rejects a bad token before the controller runs rather than
              // falling back to treating the caller as anonymous, so retry without it.
              var anonymousRequest = buildHttpRequest(null, HttpMethod.GET, uri, null, null);
              CompletableFuture<ClusterRecommendationResponse> anonymousResponse =
                  sendAndParseAsync(anonymousRequest, HttpMethod.GET, uri, responseType);
              return anonymousResponse.thenApply(r -> new ClusterRecommendationResult(r, true));
            }

            // 500s, network errors, and parse failures are not retried: only an actual
            // auth rejection justifies falling back to an anonymous attempt.
            CompletableFuture<ClusterRecommendationResult> failed = new CompletableFuture<>();
            failed.completeExceptionally(cause);
            return failed;
          });
      return handled.thenCompose(future -> future);
    });
  }

  private URI buildClusterRecommendationsUri(String preferredClusterId, String requiredGeo) {
    var queryParts = new ArrayList<String>();
    if (StringUtils.isNotBlank(preferredClusterId)) {
      queryParts.add("preferredClusterId=" + urlEncode(preferredClusterId));
    }
    if (StringUtils.isNotBlank(requiredGeo)) {
      queryParts.add("requiredGeo=" + urlEncode(requiredGeo));
    }
    var query = queryParts.isEmpty() ? null : String.join("&", queryParts);
    return buildUri(
        null /* clusterId */, clustersApiPath + recommendationsApiSubPath, null /* options */,
        query);
  }

  private static String urlEncode(String value) {
    try {
      return URLEncoder.encode(value, "UTF-8");
    } catch (UnsupportedEncodingException e) {
      throw new Error("Error encoding value: " + value);
    }
  }

  /**
   * Reports whether the given exception is an {@link HttpResponseException} carrying a 401
   * or 403 status code from the service.
   */
  private static boolean isUnauthorizedOrForbidden(HttpResponseException exception) {
    return exception.statusCode == 401 || exception.statusCode == 403;
  }

  /**
   * Unwraps {@link CompletionException} and {@link ExecutionException} wrappers that
   * {@link CompletableFuture} chains add around the exception that actually caused a stage
   * to fail, so callers can inspect the real cause (for example an
   * {@link HttpResponseException} status code) regardless of how many async stages it
   * propagated through.
   */
  private static Throwable unwrapAsyncException(Throwable throwable) {
    Throwable current = throwable;
    while ((current instanceof CompletionException || current instanceof ExecutionException)
        && current.getCause() != null) {
      current = current.getCause();
    }
    return current;
  }

  /**
   * Result of an internal cluster-recommendations request, additionally reporting whether
   * the caller's token was rejected and a retry without it was needed.
   */
  private static final class ClusterRecommendationResult {
    private final ClusterRecommendationResponse response;
    private final boolean authRejected;

    private ClusterRecommendationResult(
        ClusterRecommendationResponse response, boolean authRejected) {
      this.response = response;
      this.authRejected = authRejected;
    }
  }
}
