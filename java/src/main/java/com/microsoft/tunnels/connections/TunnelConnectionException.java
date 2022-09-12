// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

package com.microsoft.tunnels.connections;

/**
 * Exception thrown when a host or client failed to connect to a tunnel.
 */
public class TunnelConnectionException extends RuntimeException {
  public TunnelConnectionException() {
  }

  public TunnelConnectionException(String message) {
    super(message);
  }

  public TunnelConnectionException(Throwable cause) {
    super(cause);
  }

  public TunnelConnectionException(String message, Throwable cause) {
    super(message, cause);
  }
}
