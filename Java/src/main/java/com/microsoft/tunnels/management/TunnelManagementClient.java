package com.microsoft.tunnels.management;

import com.google.gson.Gson;
import com.google.gson.GsonBuilder;
import com.google.gson.reflect.TypeToken;
import com.microsoft.tunnels.contracts.Tunnel;
import com.microsoft.tunnels.contracts.TunnelAccessScopes;
import com.microsoft.tunnels.contracts.TunnelConnectionMode;
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
import java.util.Collection;
import java.util.concurrent.CompletableFuture;
import java.util.function.Supplier;
import org.apache.maven.shared.utils.StringUtils;

/**
 * TunnelManagementClient.
 */
public class TunnelManagementClient implements ITunnelManagementClient {
  private static String SDK_USER_AGENT = "tunnels-java-sdk/"
      + TunnelManagementClient.class.getPackage().getImplementationVersion();
  // Api strings
  private String prodServiceUri = "https://global.rel.tunnels.api.visualstudio.com";
  private String apiV1Path = "/api/v1";
  private String tunnelsApiPath = apiV1Path + "/tunnels";
  private String subjectsApiPath = apiV1Path + "/subjects";
  private String endpointsApiSubPath = "/endpoints";
  private String portsApiSubPath = "/ports";
  private String tunnelAuthentication = "Authorization";
  private String tunnelAuthenticationScheme = "Tunnel";

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
    this.accessTokenCallback = accessTokenCallback != null ? accessTokenCallback : () -> null;
    this.baseAddress = tunnelServiceUri != null ? tunnelServiceUri : prodServiceUri;
  }

  private <T, U> CompletableFuture<U> requestAsync(HttpMethod requestMethod, URI uri,
      T requestObject, Type responseType) {
    var client = HttpClient.newHttpClient();
    // TODO handle tunnel auth
    HttpRequest request = creatHttpRequest(requestMethod, uri, requestObject);
    return client.sendAsync(request, HttpResponse.BodyHandlers.ofString())
        .thenApply(response -> parseResponse(response, responseType));
  }

  private <T> HttpRequest creatHttpRequest(
      HttpMethod requestMethod,
      URI uri,
      T requestObject) {
    String userAgentString = this.userAgent.productName
        + "/" + this.userAgent.version + " " + SDK_USER_AGENT;
    var requestBuilder = HttpRequest.newBuilder()
        .uri(uri)
        .header("Authorization", this.accessTokenCallback.get())
        .header("User-Agent", userAgentString)
        .header("Content-Type", "application/json");

    Gson gson = new Gson();
    var bodyPublisher = requestMethod == HttpMethod.POST || requestMethod == HttpMethod.PUT
        ? BodyPublishers.ofString(gson.toJson(requestObject))
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
        host = (clusterId + baseAddress.getHost()).replace("global.", "");
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
          queryString,
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
    return requestAsync(HttpMethod.GET, requestUri, null /* requestObject */, responseType);
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
    return requestAsync(HttpMethod.GET, requestUri, null, responseType);
  }

  @Override
  public CompletableFuture<Tunnel> createTunnelAsync(Tunnel tunnel, TunnelRequestOptions options) {
    if (tunnel.name == null) {
      throw new IllegalArgumentException("Tunnel name must be specified.");
    }
    if (tunnel.tunnelId != null) {
      throw new IllegalArgumentException("Tunnel ID may not be specified when creating a tunnel.");
    }
    var uri = buildUri(tunnel.clusterId, tunnelsApiPath, options, null);
    final Type responseType = new TypeToken<Tunnel>() {
    }.getType();
    return requestAsync(HttpMethod.POST, uri, tunnel, responseType);
  }

  @Override
  public CompletableFuture<Tunnel> updateTunnelAsync(Tunnel tunnel, TunnelRequestOptions options) {
    // TODO Auto-generated method stub
    return null;
  }

  @Override
  public CompletableFuture<Boolean> deleteTunnelAsync(Tunnel tunnel, TunnelRequestOptions options) {
    var uri = buildUri(tunnel, options);
    final Type responseType = new TypeToken<Boolean>() {
    }.getType();
    return requestAsync(HttpMethod.DELETE, uri, tunnel, responseType);
  }

  @Override
  public CompletableFuture<Boolean> updateTunnelEndpointsAsync(
      Tunnel tunnel,
      TunnelEndpoint endpoint,
      TunnelRequestOptions options) {
    // TODO Auto-generated method stub
    return null;
  }

  @Override
  public CompletableFuture<Boolean> deleteTunnelEndpointsAsync(Tunnel tunnel, String hostId,
      TunnelConnectionMode tunnelConnectionMode, TunnelRequestOptions options) {
    // TODO Auto-generated method stub
    return null;
  }

  @Override
  public CompletableFuture<Collection<TunnelPort>> listTunnelPortsAsync(
      Tunnel tunnel,
      TunnelRequestOptions options) {
    // TODO Auto-generated method stub
    return null;
  }

  @Override
  public CompletableFuture<TunnelPort> getTunnelPortAsync(Tunnel tunnel, int portNumber,
      TunnelRequestOptions options) {
    // TODO Auto-generated method stub
    return null;
  }

  @Override
  public CompletableFuture<TunnelPort> createTunnelPortAsync(Tunnel tunnel, TunnelPort tunnelPort,
      TunnelRequestOptions options) {
    // TODO Auto-generated method stub
    return null;
  }

  @Override
  public CompletableFuture<TunnelPort> updateTunnelPortAsync(Tunnel tunnel, TunnelPort tunnelPort,
      TunnelRequestOptions options) {
    // TODO Auto-generated method stub
    return null;
  }

  @Override
  public CompletableFuture<Boolean> deleteTunnelPortAsync(Tunnel tunnel, int portNumber,
      TunnelRequestOptions options) {
    // TODO Auto-generated method stub
    return null;
  }
}
