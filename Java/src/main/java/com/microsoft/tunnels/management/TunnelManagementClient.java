package com.microsoft.tunnels.management;

import com.google.gson.Gson;
import com.google.gson.GsonBuilder;
import com.google.gson.reflect.TypeToken;
import com.microsoft.tunnels.contracts.Tunnel;
import com.microsoft.tunnels.contracts.TunnelAccessScopes;
import com.microsoft.tunnels.contracts.TunnelConnectionMode;
import com.microsoft.tunnels.contracts.TunnelEndpoint;
import com.microsoft.tunnels.contracts.TunnelPort;
import com.microsoft.tunnels.contracts.TunnelRelayTunnelEndpoint;

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
import java.util.Collection;
import java.util.concurrent.CompletableFuture;
import java.util.function.Supplier;
import java.util.stream.Collectors;

import org.apache.maven.shared.utils.StringUtils;

/**
 * TunnelManagementClient.
 */
public class TunnelManagementClient implements ITunnelManagementClient {
  private static final String SDK_USER_AGENT = "tunnels-java-sdk/"
      + TunnelManagementClient.class.getPackage().getImplementationVersion();
  private static final String AUTH_HEADER = "Authorization";
  private static final String CONTENT_TYPE_HEADER = "Content-Type";
  private static final String USER_AGENT_HEADER = "User-Agent";

  // Api strings
  private String prodServiceUri = "https://global.rel.tunnels.api.visualstudio.com";
  private String apiV1Path = "/api/v1";
  private String tunnelsApiPath = apiV1Path + "/tunnels";
  private String subjectsApiPath = apiV1Path + "/subjects";
  private String endpointsApiSubPath = "/endpoints";
  private String portsApiSubPath = "/ports";
  private String tunnelAuthenticationScheme = "Tunnel";

  // Access Scopes
  private static String[] HostAccessTokenScope = {
      TunnelAccessScopes.Host
  };
  private static String[] HostOrManageAccessTokenScope = {
      TunnelAccessScopes.Host,
      TunnelAccessScopes.Manage,
  };
  private static String[] ManageAccessTokenScope = {
      TunnelAccessScopes.Manage
  };
  private static String[] ReadAccessTokenScopes = {
      TunnelAccessScopes.Manage,
      TunnelAccessScopes.Host,
      TunnelAccessScopes.Connect
  };

  private ProductHeaderValue userAgent;
  private Supplier<String> accessTokenCallback;
  private String baseAddress;

  public TunnelManagementClient(ProductHeaderValue userAgent) {
    this(userAgent, null, null);
  }

  public TunnelManagementClient(
      ProductHeaderValue userAgent,
      Supplier<String> accessTokenCallback) {
    this(userAgent, accessTokenCallback, null);
  }

  /**
   * Initiates a new instance of the TunnelManagementClient class.
   *
   * @param userAgent           User-Agent header given as a
   *                            {@link ProductHeaderValue}.
   * @param accessTokenCallback A callback which should resolve to the
   *                            Authentication header value.
   * @param tunnelServiceUri    Uri for the tunnel service. Defaults to the
   *                            production service url.
   */
  public TunnelManagementClient(
      ProductHeaderValue userAgent,
      Supplier<String> accessTokenCallback,
      String tunnelServiceUri) {
    this.userAgent = userAgent;
    this.accessTokenCallback = accessTokenCallback != null ? accessTokenCallback : () -> "";
    this.baseAddress = tunnelServiceUri != null ? tunnelServiceUri : prodServiceUri;
  }

  private <T, U> CompletableFuture<U> requestAsync(
      Tunnel tunnel,
      TunnelRequestOptions options,
      HttpMethod requestMethod,
      URI uri,
      String[] scopes,
      T requestObject,
      Type responseType) {
    var client = HttpClient.newHttpClient();
    HttpRequest request = creatHttpRequest(
        tunnel, options, requestMethod, uri, requestObject, scopes);
    return client.sendAsync(request, HttpResponse.BodyHandlers.ofString())
        .thenApply(response -> parseResponse(response, responseType));
  }

  private <T> HttpRequest creatHttpRequest(
      Tunnel tunnel,
      TunnelRequestOptions options,
      HttpMethod requestMethod,
      URI uri,
      T requestObject,
      String[] scopes) {

    String authHeaderValue = null;
    if (options != null && options.accessToken != null) {
      authHeaderValue = tunnelAuthenticationScheme + " " + options.accessToken;
    }

    if (StringUtils.isBlank(authHeaderValue)) {
      authHeaderValue = this.accessTokenCallback.get();
    }

    if (StringUtils.isBlank(authHeaderValue) && tunnel != null && tunnel.accessTokens != null) {
      for (String scope : scopes) {
        var accessToken = tunnel.accessTokens.get(scope);
        if (StringUtils.isNotBlank(accessToken)) {
          authHeaderValue = tunnelAuthenticationScheme + " " + accessToken;
          break;
        }
      }
    }

    String userAgentString = this.userAgent.productName
        + "/" + this.userAgent.version + " " + SDK_USER_AGENT;
    var requestBuilder = HttpRequest.newBuilder()
        .uri(uri)
        .header(USER_AGENT_HEADER, userAgentString)
        .header(CONTENT_TYPE_HEADER, "application/json");
    if (StringUtils.isNotBlank(authHeaderValue)) {
      requestBuilder.header(AUTH_HEADER, authHeaderValue);
    }

    Gson gson = new Gson();
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
          response.statusCode());
    }
    var builder = new GsonBuilder()
        .excludeFieldsWithoutExposeAnnotation();
    Gson gson = builder.create();
    return gson.fromJson(response.body(), typeOfT);
  }

  private URI buildUri(Tunnel tunnel, TunnelRequestOptions options) {
    return buildUri(tunnel, options, null, null);
  }

  private URI buildUri(Tunnel tunnel, TunnelRequestOptions options, String path) {
    return buildUri(tunnel, options, path, null);
  }

  private URI buildUri(Tunnel tunnel, TunnelRequestOptions options, String path, String query) {
    if (tunnel == null) {
      throw new Error("Tunnel must be specified");
    }

    String tunnelPath;
    if (tunnel.clusterId != null && !tunnel.clusterId.isBlank() && tunnel.tunnelId != null
        && !tunnel.tunnelId.isBlank()) {
      tunnelPath = tunnelsApiPath + "/" + tunnel.tunnelId;
    } else {
      if (tunnel.name == null) {
        throw new Error("Tunnel object must include either a name or tunnel ID and cluster ID.");
      }
      if (tunnel.domain == null || tunnel.domain.isBlank()) {
        tunnelPath = tunnelsApiPath + "/" + tunnel.name;
      } else {
        tunnelPath = tunnelsApiPath + "/" + tunnel.name + "." + tunnel.domain;
      }
    }
    if (path != null && !path.isBlank()) {
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
    int port = -1;

    if (StringUtils.isNotBlank(clusterId)) {
      if (baseAddress.getHost() != "localhost"
          && !baseAddress.getHost().startsWith(clusterId + ".")) {
        host = (clusterId + "." + baseAddress.getHost()).replace("global.", "");
      } else if (baseAddress.getScheme() == "https"
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
      queryString = toQueryString(options);
    }
    if (query != null) {
      queryString += queryString.isBlank() ? query : "&" + query;
    }

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

  private String toQueryString(TunnelRequestOptions options) {
    final String encoding = "UTF-8";
    var queryOptions = new ArrayList<String>();
    if (options.includePorts) {
      queryOptions.add("includePorts=true");
    }

    if (options.scopes != null) {
      TunnelAccessScopes.validate(options.scopes, null);
      try {
        queryOptions.add("scopes=" + URLEncoder.encode(String.join(",", options.scopes), encoding));
      } catch (UnsupportedEncodingException e) {
        throw new IllegalArgumentException("Bad encoding: " + encoding);
      }
    }

    if (options.tokenScopes != null) {
      TunnelAccessScopes.validate(options.tokenScopes, null);
      try {
        queryOptions.add(
            "tokenScopes=" + URLEncoder.encode(String.join(",", options.tokenScopes), encoding));
      } catch (UnsupportedEncodingException e) {
        throw new IllegalArgumentException("Bad encoding: " + encoding);
      }
    }
    return String.join("&", queryOptions);
  }

  @Override
  public CompletableFuture<Collection<Tunnel>> listTunnelsAsync(
      String clusterId,
      TunnelRequestOptions options) {
    var query = clusterId == null || clusterId.isBlank() ? "global=true" : null;
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
  public CompletableFuture<Collection<Tunnel>> searchTunnelsAsync(
      String[] tags,
      boolean requireAllTags,
      String clusterId,
      String domain,
      TunnelRequestOptions options) {
    // TODO Auto-generated method stub
    return null;
  }

  @Override
  public CompletableFuture<Tunnel> getTunnelAsync(Tunnel tunnel, TunnelRequestOptions options) {
    var requestUri = buildUri(tunnel, options, null, null);
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
    if (tunnel.tunnelId != null) {
      throw new IllegalArgumentException("Tunnel ID may not be specified when creating a tunnel.");
    }
    var uri = buildUri(tunnel.clusterId, tunnelsApiPath, options, null);
    final Type responseType = new TypeToken<Tunnel>() {
    }.getType();
    return requestAsync(
        tunnel,
        options,
        HttpMethod.POST,
        uri,
        ManageAccessTokenScope,
        convertTunnelForRequest(tunnel),
        responseType);
  }

  private Tunnel convertTunnelForRequest(Tunnel tunnel) {
    // TODO
    return tunnel;
  }

  @Override
  public CompletableFuture<Tunnel> updateTunnelAsync(Tunnel tunnel, TunnelRequestOptions options) {
    var uri = buildUri(tunnel, options);
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
  public CompletableFuture<Boolean> deleteTunnelAsync(Tunnel tunnel, TunnelRequestOptions options) {
    var uri = buildUri(tunnel, options);
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
  public CompletableFuture<TunnelRelayTunnelEndpoint> updateTunnelEndpointsAsync(
      Tunnel tunnel,
      TunnelEndpoint endpoint,
      TunnelRequestOptions options) {
    if (endpoint == null) {
      throw new IllegalArgumentException("Endpoint must not be null.");
    }
    if (StringUtils.isBlank(endpoint.hostId)) {
      throw new IllegalArgumentException("Endpoint hostId must not be null.");
    }

    var path = endpointsApiSubPath + "/" + endpoint.hostId + "/" + endpoint.connectionMode;
    var uri = buildUri(
        tunnel,
        options,
        path);

    final Type responseType = new TypeToken<TunnelRelayTunnelEndpoint>() {
    }.getType();
    CompletableFuture<TunnelRelayTunnelEndpoint> result = requestAsync(
        tunnel,
        options,
        HttpMethod.PUT,
        uri,
        HostAccessTokenScope,
        endpoint,
        responseType);

    if (tunnel.endpoints != null) {
      tunnel.endpoints = tunnel.endpoints.stream().filter((e) -> {
        return e.hostId != endpoint.hostId || e.connectionMode != endpoint.connectionMode;
      }).collect(Collectors.toList());
      tunnel.endpoints.add(result.join());
    }
    return result;
  }

  @Override
  public CompletableFuture<Boolean> deleteTunnelEndpointsAsync(
      Tunnel tunnel,
      String hostId,
      TunnelConnectionMode connectionMode,
      TunnelRequestOptions options) {
    if (hostId == null) {
      throw new IllegalArgumentException("hostId must not be null");
    }
    var path = endpointsApiSubPath + "/" + hostId;
    if (connectionMode != null) {
      path += "/" + connectionMode;
    }
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
      tunnel.endpoints = tunnel.endpoints.stream().filter((e) -> {
        return e.hostId != hostId || e.connectionMode != connectionMode;
      }).collect(Collectors.toList());
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
    var uri = buildUri(tunnel, options, portsApiSubPath);
    final Type responseType = new TypeToken<TunnelPort>() {
    }.getType();
    CompletableFuture<TunnelPort> result = requestAsync(
        tunnel,
        options,
        HttpMethod.POST,
        uri,
        ManageAccessTokenScope,
        convertTunnelPortForRequest(tunnel, tunnelPort),
        responseType);

    if (tunnel.ports != null) {
      tunnel.ports = tunnel.ports.stream().filter((p) -> {
        return p.portNumber != tunnelPort.portNumber;
      }).collect(Collectors.toList());
      tunnel.ports.add(result.join());
    }
    return result;
  }

  private TunnelPort convertTunnelPortForRequest(Tunnel tunnel, TunnelPort tunnelPort) {
    /* TODO */
    return tunnelPort;
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
        HostAccessTokenScope,
        convertTunnelPortForRequest(tunnel, tunnelPort),
        responseType);

    if (tunnel.ports != null) {
      tunnel.ports = tunnel.ports.stream().filter((p) -> {
        return p.portNumber != tunnelPort.portNumber;
      }).collect(Collectors.toList());
      tunnel.ports.add(result.join());
    }
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
        HostOrManageAccessTokenScope,
        null /* requestObject */,
        responseType);

    if (tunnel.ports != null) {
      tunnel.ports = tunnel.ports.stream().filter((p) -> {
        return p.portNumber != portNumber;
      }).collect(Collectors.toList());
    }
    return result;
  }
}
