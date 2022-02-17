package com.microsoft.tunnels.management;

import java.io.UnsupportedEncodingException;
import java.lang.reflect.Type;
import java.net.URI;
import java.net.URISyntaxException;
import java.net.URLEncoder;
import java.net.http.HttpClient;
import java.net.http.HttpRequest;
import java.net.http.HttpResponse;
import java.util.ArrayList;
import java.util.Collection;
import java.util.concurrent.CompletableFuture;
import java.util.function.Supplier;

import com.google.gson.Gson;
import com.google.gson.GsonBuilder;
import com.google.gson.reflect.TypeToken;
import com.microsoft.tunnels.contracts.Tunnel;
import com.microsoft.tunnels.contracts.TunnelAccessScopes;
import com.microsoft.tunnels.contracts.TunnelConnectionMode;
import com.microsoft.tunnels.contracts.TunnelEndpoint;
import com.microsoft.tunnels.contracts.TunnelPort;

public class TunnelManagementClient implements ITunnelManagementClient {
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

    private <T> CompletableFuture<T> requestAsync(URI uri, Type typeOfT) {
        var client = HttpClient.newHttpClient();
        HttpRequest request = HttpRequest.newBuilder()
                .uri(uri)
                .header("Authorization", this.accessTokenCallback.get())
                .header("User-Agent", this.userAgent.productName + " " + this.userAgent.version)
                .build();

        return client.sendAsync(request, HttpResponse.BodyHandlers.ofString())
                .thenApply(response -> parseResponse(response, typeOfT));
    }

    private <T> T parseResponse(HttpResponse<String> response, Type typeOfT) {
        if (response.statusCode() != 200) {
            throw new Error("Error sending request, status code: " + response.statusCode());
        }
        var body = response.body();
        var builder = new GsonBuilder()
                .excludeFieldsWithoutExposeAnnotation();
        Gson gson = builder.create();
        return gson.fromJson(body, typeOfT);
    }

    public TunnelManagementClient(ProductHeaderValue userAgent) {
        this(userAgent, null, null);
    }

    public TunnelManagementClient(ProductHeaderValue userAgent, Supplier<String> accessTokenCallback) {
        this(userAgent, accessTokenCallback, null);
    }

    public TunnelManagementClient(
            ProductHeaderValue userAgent,
            Supplier<String> accessTokenCallback,
            String tunnelServiceUri) {
        this.userAgent = userAgent;
        this.accessTokenCallback = accessTokenCallback != null ? accessTokenCallback : () -> null;
        this.baseAddress = tunnelServiceUri != null ? tunnelServiceUri : prodServiceUri;
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
        // TODO - handle clusterId.

        String queryString = "";
        if (options != null) {
            queryString = toQueryString(options);
        }
        if (query != null) {
            queryString += query;
        }

        try {
            return new URI(
                    baseAddress.getScheme(),
                    baseAddress.getAuthority(),
                    path,
                    queryString,
                    null /* fragment */);
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
                queryOptions.add("tokenScopes=" + URLEncoder.encode(String.join(",", options.tokenScopes), encoding));
            } catch (UnsupportedEncodingException e) {
                throw new IllegalArgumentException("Bad encoding: " + encoding);
            }
        }
        return String.join("&", queryOptions);
    }

    @Override
    public CompletableFuture<Collection<Tunnel>> listTunnelsAsync(String clusterId, TunnelRequestOptions options) {
        var query = clusterId == null || clusterId.isBlank() ? "global=true" : null;
        var requestUri = this.buildUri(clusterId, tunnelsApiPath, options, query);
        final Type typeToken = new TypeToken<Collection<Tunnel>>() {
        }.getType();
        return requestAsync(requestUri, typeToken);
    }

    @Override
    public CompletableFuture<Collection<Tunnel>> searchTunnelsAsync(String[] tags, boolean requireAllTags,
            String clusterId, String domain, TunnelRequestOptions options) {
        // TODO Auto-generated method stub
        return null;
    }

    @Override
    public CompletableFuture<Tunnel> getTunnelAsync(Tunnel tunnel, TunnelRequestOptions options) {
        // TODO Auto-generated method stub
        return null;
    }

    @Override
    public CompletableFuture<Tunnel> createTunnelAsync(Tunnel tunnel, TunnelRequestOptions options) {
        // TODO Auto-generated method stub
        return null;
    }

    @Override
    public CompletableFuture<Tunnel> updateTunnelAsync(Tunnel tunnel, TunnelRequestOptions options) {
        // TODO Auto-generated method stub
        return null;
    }

    @Override
    public CompletableFuture<Boolean> deleteTunnelAsync(Tunnel tunnel, TunnelRequestOptions options) {
        // TODO Auto-generated method stub
        return null;
    }

    @Override
    public CompletableFuture<Boolean> updateTunnelEndpointsAsync(Tunnel tunnel, TunnelEndpoint endpoint,
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
    public CompletableFuture<Collection<TunnelPort>> listTunnelPortsAsync(Tunnel tunnel, TunnelRequestOptions options) {
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
