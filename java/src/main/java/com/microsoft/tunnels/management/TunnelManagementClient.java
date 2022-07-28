// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

package com.microsoft.tunnels.management;

import com.google.gson.Gson;
import com.google.gson.reflect.TypeToken;
import com.microsoft.tunnels.contracts.Tunnel;
import com.microsoft.tunnels.contracts.TunnelAccessControlEntry;
import com.microsoft.tunnels.contracts.TunnelAccessScopes;
import com.microsoft.tunnels.contracts.TunnelConnectionMode;
import com.microsoft.tunnels.contracts.TunnelContracts;
import com.microsoft.tunnels.contracts.TunnelEndpoint;
import com.microsoft.tunnels.contracts.TunnelPort;

import java.lang.reflect.Type;
import java.net.URI;
import java.net.URISyntaxException;
import java.net.http.HttpClient;
import java.net.http.HttpRequest;
import java.net.http.HttpRequest.BodyPublishers;
import java.net.http.HttpResponse;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.Collection;
import java.util.List;
import java.util.concurrent.CompletableFuture;
import java.util.function.Supplier;
import java.util.stream.Collectors;

import org.apache.maven.shared.utils.StringUtils;

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
  private String prodServiceUri = "https://global.rel.tunnels.api.visualstudio.com";
  private String apiV1Path = "/api/v1";
  private String tunnelsApiPath = apiV1Path + "/tunnels";
  private String subjectsApiPath = apiV1Path + "/subjects";
  private String endpointsApiSubPath = "/endpoints";
  private String portsApiSubPath = "/ports";
  private String tunnelAuthenticationScheme = "Tunnel";

  // Access Scopes
  private static String[] HostAccessTokenScope = {
      TunnelAccessScopes.host
  };
  private static String[] HostOrManageAccessTokenScope = {
      TunnelAccessScopes.host,
      TunnelAccessScopes.manage,
  };
  private static String[] ManageAccessTokenScope = {
      TunnelAccessScopes.manage
  };
  private static String[] ReadAccessTokenScopes = {
      TunnelAccessScopes.manage,
      TunnelAccessScopes.host,
      TunnelAccessScopes.connect
  };

  private ProductHeaderValue[] userAgents;
  private Supplier<String> userTokenCallback;
  private String baseAddress;

  public TunnelManagementClient(ProductHeaderValue[] userAgents) {
    this(userAgents, null, null);
  }

  public TunnelManagementClient(
      ProductHeaderValue[] userAgents,
      Supplier<String> userTokenCallback) {
    this(userAgents, userTokenCallback, null);
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
      Supplier<String> userTokenCallback,
      String tunnelServiceUri) {
    if (userAgents.length == 0) {
      throw new IllegalArgumentException("user agents cannot be empty");
    }
    this.userAgents = userAgents;
    this.userTokenCallback = userTokenCallback != null ? userTokenCallback : () -> "";
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
      authHeaderValue = this.userTokenCallback.get();
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
    if (StringUtils.isNotBlank(tunnel.clusterId) && StringUtils.isNotBlank(tunnel.tunnelId)) {
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
      queryString = options.toQueryString();
    }
    if (query != null) {
      queryString += StringUtils.isBlank(queryString) ? query : "&" + query;
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
    Tunnel converted = new Tunnel();
    converted.name = tunnel.name;
    converted.domain = tunnel.domain;
    converted.description = tunnel.description;
    converted.tags = tunnel.tags;
    converted.options = tunnel.options;
    converted.accessControl = tunnel.accessControl;
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

  public CompletableFuture<Boolean> deleteTunnelAsync(Tunnel tunnel) {
    return deleteTunnelAsync(tunnel, null);
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
  public CompletableFuture<TunnelEndpoint> updateTunnelEndpointAsync(
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
        if (e.hostId != endpoint.hostId || e.connectionMode != endpoint.connectionMode) {
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
      var updatedEndpoints = new ArrayList<TunnelEndpoint>();
      for (TunnelEndpoint e : tunnel.endpoints) {
        if (e.hostId != hostId || e.connectionMode != connectionMode) {
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
      var updatedPorts = new ArrayList<TunnelPort>();
      for (TunnelPort p : tunnel.ports) {
        if (p.portNumber != tunnelPort.portNumber) {
          updatedPorts.add(p);
        }
      }
      updatedPorts.add(result.join());
      updatedPorts.sort((p1, p2) -> Integer.compare(p1.portNumber, p2.portNumber));
      tunnel.ports = updatedPorts.toArray(new TunnelPort[updatedPorts.size()]);
    }
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
      var updatedPorts = new ArrayList<TunnelPort>();
      for (TunnelPort p : tunnel.ports) {
        if (p.portNumber != tunnelPort.portNumber) {
          updatedPorts.add(p);
        }
      }
      updatedPorts.add(result.join());
      updatedPorts.sort((p1, p2) -> Integer.compare(p1.portNumber, p2.portNumber));
      tunnel.ports = updatedPorts.toArray(new TunnelPort[updatedPorts.size()]);
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
}
