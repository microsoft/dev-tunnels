// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

package com.microsoft.tunnels.connections;

import com.microsoft.tunnels.contracts.Tunnel;
import com.microsoft.tunnels.contracts.TunnelConnectionMode;
import com.microsoft.tunnels.contracts.TunnelEndpoint;
import com.microsoft.tunnels.contracts.TunnelRelayTunnelEndpoint;
import com.microsoft.tunnels.websocket.WebSocketServiceFactoryFactory;

import java.io.IOException;
import java.net.URI;
import java.net.URISyntaxException;
import java.time.Duration;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.List;
import java.util.Map;
import java.util.concurrent.CompletableFuture;
import java.util.stream.Collectors;

import org.apache.sshd.client.SshClient;
import org.apache.sshd.client.session.ClientSession;
import org.apache.sshd.common.SshConstants;
import org.apache.sshd.common.channel.RequestHandler;
import org.apache.sshd.common.session.ConnectionService;
import org.apache.sshd.server.forward.AcceptAllForwardingFilter;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

/**
 * Tunnel client implementation that connects via a tunnel relay.
 */
public class TunnelRelayTunnelClient implements TunnelClient {
  private static final Duration sshSessionTimeout = Duration.ofSeconds(20);
  private static final Duration sshAuthTimeout = Duration.ofSeconds(10);

  private static final Logger logger = LoggerFactory.getLogger(TunnelRelayTunnelClient.class);

  private ClientSession session = null;
  private SshClient sshClient = null;

  /**
   * <p>
   * By default the ssh library will only start local port forwarding for a
   * requested port.
   * </p>
   * <p>
   * We provide custom implementations of TcpipForwardRequestHandler and
   * CancelTcpipForwardHandler that allow different local ports to be selected if
   * the requested port is in
   * use.
   * </p>
   * <p>
   * That tracking is mapped here since TunnelClient consumers also have reason to
   * track port added/removed events.
   * </p>
   */
  private ForwardedPortsCollection forwardedPorts = new ForwardedPortsCollection();

  public TunnelRelayTunnelClient() {
  }

  @Override
  public ForwardedPortsCollection getForwardedPorts() {
    return forwardedPorts;
  }

  @Override
  public CompletableFuture<Void> connectAsync(Tunnel tunnel) {
    return connectAsync(tunnel, null);
  }

  @Override
  public CompletableFuture<Void> connectAsync(
      Tunnel tunnel,
      String hostId) {
    if (session != null) {
      throw new IllegalStateException(
          "Already connected. Use separate instances to connect to multiple tunnels.");
    }
    if (tunnel.endpoints == null || tunnel.endpoints.length == 0) {
      throw new IllegalStateException(
          "No hosts are currently accepting connections for the tunnel.");
    }

    var endpoint = (TunnelRelayTunnelEndpoint) groupEndpoints(tunnel, hostId).stream()
      .filter((e) -> e.connectionMode == TunnelConnectionMode.TunnelRelay)
      .findFirst().orElseThrow(() -> {
      throw new IllegalStateException(
          "The specified host is not currently accepting connections to the tunnel.");
    });

    sshClient = createConfiguredSshClient(tunnel, endpoint);

    logger.info("Connecting to client tunnel relay " + endpoint.clientRelayUri);

    return CompletableFuture.runAsync(() -> {
      sshClient.start();

      try {
        /*
         * The SshClient API doesn't have a connect method that doesn't require a
         * username/host/port.
         * However we are using a custom connector (WebSocketConnector) which is
         * ultimately what starts
         * the session, and it only uses the username.
         */
        session = sshClient.connect("tunnel@host:1")
            .verify(sshSessionTimeout)
            .getSession();
      } catch (IOException e) {
        throw new TunnelConnectionException("Error verifying the ssh session.", e);
      }

      try {
        session.auth().verify(sshAuthTimeout);
      } catch (IOException e) {
        throw new TunnelConnectionException("Error authenticating the ssh session.", e);
      }
    });
  }

  @Override
  public CompletableFuture<Void> refreshPortsAsync() {
    return CompletableFuture.runAsync(() -> {
      var refreshPortsRequestType = "RefreshPorts";
      var requestBuffer = this.session.createBuffer(SshConstants.SSH_MSG_GLOBAL_REQUEST);
      requestBuffer.putString(refreshPortsRequestType);
      requestBuffer.putBoolean(true); // WantReply
      try {
        this.session.request(refreshPortsRequestType, requestBuffer, sshSessionTimeout);
      } catch (IOException e) {
        throw new TunnelConnectionException("Error refreshing ports.", e);
      }
    });
  }

  /**
   * Creates an {@link SshClient} and configures it to connect to the endpoint's
   * clientRelayUri.
   *
   * @return the created {@link SshClient}.
   */
  private SshClient createConfiguredSshClient(Tunnel tunnel, TunnelRelayTunnelEndpoint endpoint) {
    SshClient client = SshClient.setUpDefaultClient();
    // Allows filtering based on request type or address. Currently allows all
    // requests.
    client.setForwardingFilter(AcceptAllForwardingFilter.INSTANCE);
    try {
      String accessToken = tunnel.accessTokens.get("connect");
      client.setIoServiceFactoryFactory(
          new WebSocketServiceFactoryFactory(new URI(endpoint.clientRelayUri), accessToken));
    } catch (URISyntaxException e) {
      // This would likely only occur as the result of manually created tunnel being
      // passed rather than one retrieved from the service.
      throw new IllegalArgumentException(
          "Error parsing tunnel clientRelayUri. "
              + "Check that the tunnel endpoint is correct: "
              + endpoint.clientRelayUri,
          e);
    }
    // Add the handler for tcpip-forward requests. getGlobalRequestHandlers returns
    // an unmodifiable collection so we have to copy it.
    List<RequestHandler<ConnectionService>> oldGlobals = client.getGlobalRequestHandlers();
    List<RequestHandler<ConnectionService>> newGlobals = new ArrayList<>();
    if (oldGlobals.size() > 0) {
      newGlobals.addAll(oldGlobals);
    }
    newGlobals.add(new TcpipForwardRequestHandler(forwardedPorts));
    newGlobals.add(new CancelTcpipForwardRequestHandler(forwardedPorts));
    client.setGlobalRequestHandlers(newGlobals);
    return client;
  }

  private List<TunnelEndpoint> groupEndpoints(Tunnel tunnel, String hostId) {
    Map<String, List<TunnelEndpoint>> endpointGroups = Arrays.asList(tunnel.endpoints)
        .stream().collect(Collectors.groupingBy(endpoint -> endpoint.hostId));
    if (hostId != null) {
      return endpointGroups.get(hostId);
    } else if (endpointGroups.size() > 1) {
      throw new IllegalStateException(
          "There are multiple hosts for the tunnel. Specify a host ID to connect to.");
    } else {
      return endpointGroups.values().stream().findFirst().orElseThrow(() -> {
        throw new IllegalStateException(
            "No host is currently accepting connections to the tunnel.");
      });
    }
  }

  @Override
  public void stop() {
    this.sshClient.stop();
  }
}
