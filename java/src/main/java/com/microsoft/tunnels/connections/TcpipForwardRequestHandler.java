// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

package com.microsoft.tunnels.connections;

import java.net.BindException;
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
 * Handler for &quot;tcpip-forward&quot; global request.
 *
 * <p>
 * Modified from org.apache.sshd.server.global.TcpipForwardRequestHandler to
 * track the requested
 * port since the default implementation only keeps track of the local port.
 * </p>
 */
class TcpipForwardRequestHandler extends AbstractConnectionServiceRequestHandler {
  public static final String REQUEST = "tcpip-forward";

  /**
   * Default growth factor function used to resize response buffers.
   */
  public static final IntUnaryOperator RESPONSE_BUFFER_GROWTH_FACTOR = Int2IntFunction.add(
      Byte.SIZE);

  public static final TcpipForwardRequestHandler INSTANCE = new TcpipForwardRequestHandler(
      new ForwardedPortsCollection());

  public ForwardedPortsCollection forwardedPorts;

  public TcpipForwardRequestHandler(ForwardedPortsCollection forwardedPorts) {
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
    Forwarder forwarder = Objects.requireNonNull(
        connectionService.getForwarder(), "No TCP/IP forwarder");

    String address = buffer.getString();
    int requestedPort = buffer.getInt();

    // Ignore the request if the port is already in the collection.
    // This can happen if concurrent refreshPorts requests are made.
    if (forwardedPorts.stream().filter(p -> p.getRemotePort() == requestedPort)
        .findFirst().isPresent()) {
      if (wantReply) {
        Session session = connectionService.getSession();
        buffer = session.createBuffer(SshConstants.SSH_MSG_REQUEST_SUCCESS, Integer.BYTES);
        buffer.putInt(requestedPort);
        session.writePacket(buffer);
      }
      return Result.Replied;
    }

    SshdSocketAddress socketAddress = null;
    SshdSocketAddress bound = null;

    // If the port is in use we will get a bind exception so we increment the port
    // until we succeed or run out of attempts.
    for (int attempt = 0; attempt < 10; attempt++) {
      int port = requestedPort + attempt;
      socketAddress = new SshdSocketAddress(address, port);
      try {
        bound = forwarder.localPortForwardingRequested(socketAddress);
        break;
      } catch (BindException e) {
        if (log.isDebugEnabled()) {
          log.debug("Caught BindException {} attempting to connect to port {}, "
              + "incrementing port and retrying.", e, port);
        }
        continue;
      }
    }
    // If bound is still null, try wildcard port.
    if (bound == null) {
      socketAddress = new SshdSocketAddress(address, 0);
      bound = forwarder.localPortForwardingRequested(socketAddress);
    }

    if (log.isDebugEnabled()) {
      log.debug("process({})[{}][want-reply-{}] {} => {}",
          connectionService, request, wantReply, socketAddress, bound);
    }

    // If we somehow still failed to bind to a port after multiple tries, reply with
    // failure.
    if (bound == null) {
      return Result.ReplyFailure;
    }

    int port = bound.getPort();
    if (wantReply) {
      Session session = connectionService.getSession();
      buffer = session.createBuffer(SshConstants.SSH_MSG_REQUEST_SUCCESS, Integer.BYTES);
      buffer.putInt(port);
      session.writePacket(buffer);
    }
    forwardedPorts.addPort(new ForwardedPort(port, requestedPort));
    return Result.Replied;
  }
}
