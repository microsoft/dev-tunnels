// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

package com.microsoft.tunnels;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertNull;
import static org.junit.Assert.assertTrue;
import static org.junit.Assert.fail;

import com.microsoft.tunnels.contracts.Tunnel;
import com.microsoft.tunnels.management.ProductHeaderValue;
import com.microsoft.tunnels.management.TunnelManagementClient;
import com.microsoft.tunnels.management.TunnelRequestOptions;

import com.sun.net.httpserver.HttpExchange;
import com.sun.net.httpserver.HttpServer;

import java.io.ByteArrayOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.net.InetSocketAddress;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.Collections;
import java.util.HashMap;
import java.util.HashSet;
import java.util.List;
import java.util.TreeMap;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.atomic.AtomicInteger;
import java.util.function.Function;
import java.util.function.Supplier;

import org.junit.After;
import org.junit.Before;
import org.junit.Test;

/**
 * Deterministic, non-live tests for the cluster-recommendation handling in
 * {@link TunnelManagementClient#createTunnelAsync}.
 *
 * <p>These tests run against a local, in-process HTTP server rather than the live tunnel
 * service (deliberately not extending {@link TunnelTest}, which targets the real service),
 * so they can assert exact request counts, headers, and retry behavior without any network
 * dependency.</p>
 */
public class TunnelClusterRecommendationsTests {
  private static final String API_VERSION = "2023-09-27-preview";
  private static final String CLUSTER_SOURCE_HEADER = "X-Tunnel-Cluster-Source";
  private static final String RECOMMENDATIONS_PATH = "/clusters/recommendations";

  private HttpServer server;
  private List<CapturedRequest> requests;
  private Function<CapturedRequest, TestResponse> handler;

  @Before
  public void startServer() throws IOException {
    this.requests = Collections.synchronizedList(new ArrayList<>());
    // Port 0 lets the OS pick a free local port so tests never collide.
    this.server = HttpServer.create(new InetSocketAddress("localhost", 0), 0);
    this.server.createContext("/", this::handleExchange);
    this.server.setExecutor(null);
    this.server.start();
  }

  @After
  public void stopServer() {
    if (this.server != null) {
      this.server.stop(0);
    }
  }

  private String baseAddress() {
    return "http://localhost:" + this.server.getAddress().getPort();
  }

  private void handleExchange(HttpExchange exchange) throws IOException {
    try {
      var body = readBody(exchange.getRequestBody());
      var capturedHeaders = new TreeMap<String, String>(String.CASE_INSENSITIVE_ORDER);
      exchange.getRequestHeaders().forEach((name, values) -> {
        if (!values.isEmpty()) {
          capturedHeaders.put(name, values.get(0));
        }
      });
      var request = new CapturedRequest(
          exchange.getRequestMethod(),
          exchange.getRequestURI().toString(),
          capturedHeaders,
          body);
      this.requests.add(request);

      var response = this.handler.apply(request);
      byte[] payload = response.body.getBytes(StandardCharsets.UTF_8);

      // Explicit Content-Length (never chunked) and Connection: close keep each request/
      // response exchange self-contained and simple to reason about in a short-lived,
      // per-test local server. sendResponseHeaders sets Content-Length itself from the
      // length argument below, so it must not also be set manually here.
      exchange.getResponseHeaders().set("Content-Type", "application/json");
      exchange.getResponseHeaders().set("Connection", "close");
      exchange.sendResponseHeaders(response.statusCode, payload.length);
      try (OutputStream responseBody = exchange.getResponseBody()) {
        responseBody.write(payload);
      }
    } finally {
      exchange.close();
    }
  }

  private static String readBody(InputStream stream) throws IOException {
    var buffer = new ByteArrayOutputStream();
    var chunk = new byte[4096];
    int read;
    while ((read = stream.read(chunk)) != -1) {
      buffer.write(chunk, 0, read);
    }
    return buffer.toString(StandardCharsets.UTF_8.name());
  }

  private TunnelManagementClient createClient(Supplier<CompletableFuture<String>> userTokenCallback) {
    var userAgent = new ProductHeaderValue("cluster-recommendations-test", "1.0");
    return new TunnelManagementClient(
        new ProductHeaderValue[] { userAgent },
        userTokenCallback,
        baseAddress(),
        API_VERSION);
  }

  private static Supplier<CompletableFuture<String>> tokenCallback(String bearerToken) {
    return () -> CompletableFuture.completedFuture("Bearer " + bearerToken);
  }

  private static TestResponse recommendationResponse(String clusterId) {
    var json = clusterId == null
        ? "{\"recommendations\":[]}"
        : "{\"recommendedClusterId\":\"" + clusterId + "\",\"recommendations\":[]}";
    return new TestResponse(200, json);
  }

  private static TestResponse unauthorized(int statusCode) {
    return new TestResponse(statusCode, "{\"error\":\"unauthorized\"}");
  }

  private static TestResponse createResponse(String tunnelId) {
    return new TestResponse(200, "{\"tunnelId\":\"" + tunnelId + "\"}");
  }

  private List<CapturedRequest> recommendationRequests() {
    var result = new ArrayList<CapturedRequest>();
    for (var request : this.requests) {
      if (request.path.startsWith(RECOMMENDATIONS_PATH)) {
        result.add(request);
      }
    }
    return result;
  }

  private List<CapturedRequest> createRequests() {
    var result = new ArrayList<CapturedRequest>();
    for (var request : this.requests) {
      if (request.method.equals("PUT")) {
        result.add(request);
      }
    }
    return result;
  }

  private Tunnel createTunnel(
      TunnelManagementClient client, Tunnel tunnel, TunnelRequestOptions options) {
    try {
      return client.createTunnelAsync(tunnel, options).get(10, java.util.concurrent.TimeUnit.SECONDS);
    } catch (java.util.concurrent.ExecutionException | InterruptedException
        | java.util.concurrent.TimeoutException e) {
      throw new AssertionError("createTunnelAsync failed unexpectedly: " + e.getCause(), e);
    }
  }

  @Test
  public void authenticatedRecommendation_SelectsClusterOnCreate() {
    this.handler = request -> {
      if (request.path.startsWith(RECOMMENDATIONS_PATH)) {
        assertEquals("Bearer usertoken", request.headers.get("Authorization"));
        return recommendationResponse("usw4");
      }
      assertEquals("recommended", request.headers.get(CLUSTER_SOURCE_HEADER));
      return createResponse("tunnel001");
    };

    var client = createClient(tokenCallback("usertoken"));
    var tunnel = new Tunnel();
    tunnel.tunnelId = "tunnel001";

    createTunnel(client, tunnel, null);

    assertEquals(1, recommendationRequests().size());
    assertEquals(1, createRequests().size());
    assertEquals("usw4", tunnel.clusterId);
    assertEquals("recommended", createRequests().get(0).headers.get(CLUSTER_SOURCE_HEADER));
  }

  @Test
  public void unauthorized401_RetriesAnonymouslyAndSucceeds() {
    this.handler = request -> {
      if (request.path.startsWith(RECOMMENDATIONS_PATH)) {
        if (request.headers.containsKey("Authorization")) {
          return unauthorized(401);
        }
        return recommendationResponse("usw4");
      }
      return createResponse("tunnel002");
    };

    var client = createClient(tokenCallback("expired"));
    var tunnel = new Tunnel();
    tunnel.tunnelId = "tunnel002";

    createTunnel(client, tunnel, null);

    var recommendationCalls = recommendationRequests();
    assertEquals(2, recommendationCalls.size());
    assertEquals("Bearer expired", recommendationCalls.get(0).headers.get("Authorization"));
    // The retry must not carry any Authorization header at all -- not just a blank one.
    assertFalse(recommendationCalls.get(1).headers.containsKey("Authorization"));
    assertEquals("usw4", tunnel.clusterId);
    assertEquals(
        "recommended-after-auth-rejected",
        createRequests().get(0).headers.get(CLUSTER_SOURCE_HEADER));
  }

  @Test
  public void forbidden403_RetriesAnonymouslyAndSucceeds() {
    this.handler = request -> {
      if (request.path.startsWith(RECOMMENDATIONS_PATH)) {
        if (request.headers.containsKey("Authorization")) {
          return unauthorized(403);
        }
        return recommendationResponse("usw4");
      }
      return createResponse("tunnel003");
    };

    var client = createClient(tokenCallback("expired"));
    var tunnel = new Tunnel();
    tunnel.tunnelId = "tunnel003";

    createTunnel(client, tunnel, null);

    var recommendationCalls = recommendationRequests();
    assertEquals(2, recommendationCalls.size());
    assertFalse(recommendationCalls.get(1).headers.containsKey("Authorization"));
    assertEquals("usw4", tunnel.clusterId);
    assertEquals(
        "recommended-after-auth-rejected",
        createRequests().get(0).headers.get(CLUSTER_SOURCE_HEADER));
  }

  @Test
  public void serverError500_DoesNotRetryAndFallsBack() {
    this.handler = request -> {
      if (request.path.startsWith(RECOMMENDATIONS_PATH)) {
        return new TestResponse(500, "{\"error\":\"boom\"}");
      }
      return createResponse("tunnel004");
    };

    var client = createClient(tokenCallback("usertoken"));
    var tunnel = new Tunnel();
    tunnel.tunnelId = "tunnel004";

    createTunnel(client, tunnel, null);

    // A 500 is not an auth rejection, so it must not be retried: exactly one attempt.
    assertEquals(1, recommendationRequests().size());
    assertNull(tunnel.clusterId);
    assertEquals("fallback-error", createRequests().get(0).headers.get(CLUSTER_SOURCE_HEADER));
  }

  @Test
  public void anonymousRetryAlsoUnauthorized_FallsBackToAuthFailed() {
    this.handler = request -> {
      if (request.path.startsWith(RECOMMENDATIONS_PATH)) {
        return unauthorized(401);
      }
      return createResponse("tunnel005");
    };

    var client = createClient(tokenCallback("expired"));
    var tunnel = new Tunnel();
    tunnel.tunnelId = "tunnel005";

    createTunnel(client, tunnel, null);

    // One authenticated attempt, one anonymous retry, both rejected.
    assertEquals(2, recommendationRequests().size());
    assertNull(tunnel.clusterId);
    assertEquals(
        "fallback-auth-failed", createRequests().get(0).headers.get(CLUSTER_SOURCE_HEADER));
  }

  @Test
  public void emptyRecommendationResponse_FallsBackToEmpty() {
    this.handler = request -> {
      if (request.path.startsWith(RECOMMENDATIONS_PATH)) {
        return recommendationResponse(null);
      }
      return createResponse("tunnel006");
    };

    var client = createClient(tokenCallback("usertoken"));
    var tunnel = new Tunnel();
    tunnel.tunnelId = "tunnel006";

    createTunnel(client, tunnel, null);

    // A successful, complete response with no cluster does not need a retry.
    assertEquals(1, recommendationRequests().size());
    assertNull(tunnel.clusterId);
    assertEquals("fallback-empty", createRequests().get(0).headers.get(CLUSTER_SOURCE_HEADER));
  }

  @Test
  public void explicitClusterId_SkipsRecommendationsEntirely() {
    this.handler = request -> {
      if (request.path.startsWith(RECOMMENDATIONS_PATH)) {
        fail("Recommendations should not be called when a cluster is already set.");
      }
      return createResponse("tunnel007");
    };

    var client = createClient(tokenCallback("usertoken"));
    var tunnel = new Tunnel();
    tunnel.tunnelId = "tunnel007";
    tunnel.clusterId = "usw3";

    createTunnel(client, tunnel, null);

    assertEquals(0, recommendationRequests().size());
    assertEquals(1, createRequests().size());
    assertEquals("explicit", createRequests().get(0).headers.get(CLUSTER_SOURCE_HEADER));
  }

  @Test
  public void allClusterSourceHeaderValues_MatchExpectedWireValues() {
    // Reconfirms the exact wire values in one place, so a rename of the enum constants can't
    // silently drift from what the service expects.
    var expected = new HashSet<>(java.util.Arrays.asList(
        "explicit",
        "recommended",
        "recommended-after-auth-rejected",
        "fallback-auth-failed",
        "fallback-empty",
        "fallback-error"));

    var actual = new HashSet<String>();
    for (var source : com.microsoft.tunnels.management.TunnelClusterSource.values()) {
      actual.add(source.toHeaderValue());
    }

    assertEquals(expected, actual);
  }

  @Test
  public void callerOptions_AreNotMutatedAndSourceHeaderCannotBeSpoofed() {
    this.handler = request -> {
      if (request.path.startsWith(RECOMMENDATIONS_PATH)) {
        fail("Recommendations should not be called when a cluster is already set.");
      }
      return createResponse("tunnel008");
    };

    var client = createClient(tokenCallback("usertoken"));
    var tunnel = new Tunnel();
    tunnel.tunnelId = "tunnel008";
    tunnel.clusterId = "usw3";

    var options = new TunnelRequestOptions();
    var callerHeaders = new HashMap<String, String>();
    callerHeaders.put("X-Tunnel-Cluster-Source", "explicit-spoofed");
    callerHeaders.put("X-Custom-Header", "keep-me");
    options.additionalHeaders = callerHeaders;

    createTunnel(client, tunnel, options);

    var sentHeaders = createRequests().get(0).headers;
    // The client's own value wins over anything the caller supplied.
    assertEquals("explicit", sentHeaders.get(CLUSTER_SOURCE_HEADER));
    // Other caller headers are preserved.
    assertEquals("keep-me", sentHeaders.get("X-Custom-Header"));

    // The caller's options object (and its additionalHeaders map) must be untouched: no
    // "If-Not-Match" header added, and the spoofed value still present exactly as given.
    assertEquals(2, options.additionalHeaders.size());
    assertEquals("explicit-spoofed", options.additionalHeaders.get("X-Tunnel-Cluster-Source"));
    assertEquals("keep-me", options.additionalHeaders.get("X-Custom-Header"));
    assertFalse(options.additionalHeaders.containsKey("If-Not-Match"));
  }

  @Test
  public void callerOptions_SpoofHeaderRemovedCaseInsensitively() {
    this.handler = request -> {
      if (request.path.startsWith(RECOMMENDATIONS_PATH)) {
        return recommendationResponse("usw4");
      }
      return createResponse("tunnel009");
    };

    var client = createClient(tokenCallback("usertoken"));
    var tunnel = new Tunnel();
    tunnel.tunnelId = "tunnel009";

    var options = new TunnelRequestOptions();
    var callerHeaders = new HashMap<String, String>();
    // Lowercase variant of the header name the client uses -- must still be treated as a
    // spoof attempt and removed, since HTTP header names are case-insensitive.
    callerHeaders.put("x-tunnel-cluster-source", "recommended-spoofed");
    options.additionalHeaders = callerHeaders;

    createTunnel(client, tunnel, options);

    var sentHeaders = createRequests().get(0).headers;
    assertEquals("recommended", sentHeaders.get(CLUSTER_SOURCE_HEADER));
    assertTrue(sentHeaders.get(CLUSTER_SOURCE_HEADER) == null
        || !sentHeaders.get(CLUSTER_SOURCE_HEADER).contains("spoofed"));

    // Caller's map is untouched.
    assertEquals(1, options.additionalHeaders.size());
    assertEquals("recommended-spoofed", options.additionalHeaders.get("x-tunnel-cluster-source"));
  }

  @Test
  public void unauthenticatedClient_SendsNoAuthorizationHeaderAndDoesNotRetry() {
    this.handler = request -> {
      if (request.path.startsWith(RECOMMENDATIONS_PATH)) {
        assertFalse(request.headers.containsKey("Authorization"));
        return recommendationResponse("usw4");
      }
      return createResponse("tunnel010");
    };

    // No user token callback configured at all.
    var client = createClient(null);
    var tunnel = new Tunnel();
    tunnel.tunnelId = "tunnel010";

    createTunnel(client, tunnel, null);

    assertEquals(1, recommendationRequests().size());
    assertEquals("usw4", tunnel.clusterId);
    assertEquals("recommended", createRequests().get(0).headers.get(CLUSTER_SOURCE_HEADER));
  }

  @Test
  public void getClusterRecommendationsAsync_ReturnsResponseDirectly() {
    this.handler = request -> {
      assertTrue(request.path.startsWith(RECOMMENDATIONS_PATH));
      assertTrue(request.path.contains("preferredClusterId=preferred+cluster"));
      assertTrue(request.path.contains("requiredGeo=eu+west"));
      return recommendationResponse("usw5");
    };

    var client = createClient(tokenCallback("usertoken"));
    var response = client
        .getClusterRecommendationsAsync("preferred cluster", "eu west")
        .join();

    assertEquals("usw5", response.recommendedClusterId);
    assertEquals(1, recommendationRequests().size());
  }

  @Test
  public void requiredGeo_IsForwardedOnlyToRecommendations() {
    this.handler = request -> {
      if (request.path.startsWith(RECOMMENDATIONS_PATH)) {
        assertTrue(request.path.contains("requiredGeo=eu+west"));
        return recommendationResponse("usw4");
      }
      assertFalse(request.path.contains("requiredGeo"));
      return createResponse("tunnel011");
    };

    var client = createClient(tokenCallback("usertoken"));
    var tunnel = new Tunnel();
    tunnel.tunnelId = "tunnel011";
    var options = new TunnelRequestOptions();
    options.requiredGeo = "eu west";

    createTunnel(client, tunnel, options);

    assertEquals("usw4", tunnel.clusterId);
    assertEquals(1, recommendationRequests().size());
    assertEquals(1, createRequests().size());
  }

  @Test
  public void tokenCallbackFailure_FallsBackToCreate() {
    this.handler = request -> {
      if (request.path.startsWith(RECOMMENDATIONS_PATH)) {
        fail("Recommendations request should not be sent when token resolution fails.");
      }
      return createResponse("tunnel012");
    };

    var callbackCount = new AtomicInteger();
    Supplier<CompletableFuture<String>> tokenCallback = () -> {
      if (callbackCount.getAndIncrement() == 0) {
        throw new IllegalStateException("token unavailable");
      }
      return CompletableFuture.completedFuture("Bearer recovered");
    };
    var client = createClient(tokenCallback);
    var tunnel = new Tunnel();
    tunnel.tunnelId = "tunnel012";

    createTunnel(client, tunnel, null);

    assertEquals(2, callbackCount.get());
    assertEquals(0, recommendationRequests().size());
    assertEquals(
        "fallback-error", createRequests().get(0).headers.get(CLUSTER_SOURCE_HEADER));
  }

  /**
   * A recorded request as observed by the local server.
   */
  private static final class CapturedRequest {
    private final String method;
    private final String path;
    private final TreeMap<String, String> headers;
    private final String body;

    private CapturedRequest(
        String method, String path, TreeMap<String, String> headers, String body) {
      this.method = method;
      this.path = path;
      this.headers = headers;
      this.body = body;
    }
  }

  /**
   * A canned response for the local server to send back for a captured request.
   */
  private static final class TestResponse {
    private final int statusCode;
    private final String body;

    private TestResponse(int statusCode, String body) {
      this.statusCode = statusCode;
      this.body = body;
    }
  }
}
