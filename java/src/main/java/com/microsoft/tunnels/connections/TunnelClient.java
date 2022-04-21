package com.microsoft.tunnels.connections;

import com.microsoft.tunnels.contracts.Tunnel;
import com.microsoft.tunnels.contracts.TunnelRelayTunnelEndpoint;
import com.microsoft.tunnels.websocket.WebSocketServiceFactoryFactory;

import java.io.IOException;
import java.net.URI;
import java.net.URISyntaxException;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.List;
import java.util.Map;
import java.util.stream.Collectors;

import org.apache.sshd.client.SshClient;
import org.apache.sshd.client.session.ClientSession;
import org.apache.sshd.common.channel.RequestHandler;
import org.apache.sshd.common.session.ConnectionService;
import org.apache.sshd.server.forward.AcceptAllForwardingFilter;

public class TunnelClient {
  private static final int sshSessionTimeoutMs = 10000;
  private static final int sshAuthTimeoutMs = 10000;
  public ClientSession session = null;

  public TunnelClient() {
  }

  /**
   * Connects to the specified tunnel.
   *
   * @param tunnel Tunnel to connect to.
   * @return
   */
  public void connect(Tunnel tunnel) {
    connect(tunnel, null);
  }

  /**
   * Connects to the specified tunnel and host.
   *
   * @param tunnel Tunnel to connect to.
   * @param hostId ID of the host connected to the tunnel.
   * @return
   */
  public void connect(
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

    var endpoint = groupEndpoints(tunnel, hostId).stream().findFirst().orElseThrow(() -> {
      throw new IllegalStateException(
          "The specified host is not currently accepting connections to the tunnel.");
    });

    SshClient client = createConfiguredSshClient(tunnel, endpoint);
    client.start();

    try {
      // The SshClient API doesn't have a connect method that doesn't require a
      // username/host/port.
      // However we are using a custom connector (WebSocketConnector) which is
      // ultimately what starts
      // the session, and it only uses the username.
      session = client.connect("tunnel@host:1")
          .verify(sshSessionTimeoutMs)
          .getSession();
    } catch (IOException e) {
      throw new SshException("Error verifying the ssh session.", e);
    }

    try {
      session.auth().verify(sshAuthTimeoutMs);
    } catch (IOException e) {
      throw new SshException("Error authenticating the ssh session.", e);
    }
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
    newGlobals.add(new PortForwardHandler());
    client.setGlobalRequestHandlers(newGlobals);
    return client;
  }

  private List<TunnelRelayTunnelEndpoint> groupEndpoints(Tunnel tunnel, String hostId) {
    Map<String, List<TunnelRelayTunnelEndpoint>> endpointGroups = Arrays.asList(tunnel.endpoints)
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
}
