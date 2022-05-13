// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

package com.microsoft.tunnels.connections;

import java.util.Objects;
import java.util.function.IntUnaryOperator;

import org.apache.sshd.common.SshConstants;
import org.apache.sshd.common.forward.Forwarder;
import org.apache.sshd.common.session.ConnectionService;
import org.apache.sshd.common.session.Session;
import org.apache.sshd.common.session.helpers.AbstractConnectionServiceRequestHandler;
import org.apache.sshd.common.util.buffer.Buffer;
import org.apache.sshd.common.util.functors.Int2IntFunction;
import org.apache.sshd.common.util.net.SshdSocketAddress;

/**
 * Handler for &quot;cancel-tcpip-forward&quot; global request.
 *
 * <p>
 * Modified from org.apache.sshd.server.global.CancelTcpipForwardHandler to
 * track the requested
 * port since the default implementation only keeps track of the local port.
 * </p>
 */
class CancelTcpipForwardRequestHandler extends AbstractConnectionServiceRequestHandler {
  public static final String REQUEST = "cancel-tcpip-forward";
  /**
   * Default growth factor function used to resize response buffers.
   */
  public static final IntUnaryOperator RESPONSE_BUFFER_GROWTH_FACTOR = Int2IntFunction
      .add(Byte.SIZE);

  public static final CancelTcpipForwardRequestHandler INSTANCE = new CancelTcpipForwardRequestHandler(
      new ForwardedPortsCollection());

  private ForwardedPortsCollection forwardedPorts;

  public CancelTcpipForwardRequestHandler(ForwardedPortsCollection forwardedPorts) {
    super();
    this.forwardedPorts = forwardedPorts;
  }

  @Override
  public Result process(
      ConnectionService connectionService, String request, boolean wantReply, Buffer buffer)
      throws Exception {
    if (!REQUEST.equals(request)) {
      return super.process(connectionService, request, wantReply, buffer);
    }

    String address = buffer.getString();
    int port = buffer.getInt();
    SshdSocketAddress socketAddress = new SshdSocketAddress(address, port);
    if (log.isDebugEnabled()) {
      log.debug("process({})[{}] {} reply={}",
          connectionService, request, socketAddress, wantReply);
    }

    Forwarder forwarder = Objects.requireNonNull(connectionService.getForwarder(),
        "No TCP/IP forwarder");

    // local ports can be chosen dynamically so we have to keep track of which
    // remote
    // port the local port is associated with.
    ForwardedPort forwardedPort = this.forwardedPorts
        .stream()
        .filter(p -> p.getRemotePort() == port)
        .findFirst()
        .orElse(null);
    if (forwardedPort != null) {
      SshdSocketAddress localAddress = new SshdSocketAddress(address, port);
      forwarder.localPortForwardingCancelled(localAddress);
    } else {
      return Result.ReplyFailure;
    }

    if (wantReply) {
      Session session = connectionService.getSession();
      buffer = session.createBuffer(SshConstants.SSH_MSG_REQUEST_SUCCESS, Integer.BYTES);
      buffer.putInt(port);
      session.writePacket(buffer);
    }
    forwardedPorts.removePort(forwardedPort);

    return Result.Replied;
  }
}
