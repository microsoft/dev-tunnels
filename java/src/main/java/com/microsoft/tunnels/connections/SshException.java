// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

package com.microsoft.tunnels.connections;

/**
 * Something went wrong during the Ssh connection. This is probably not recoverable.
 */
public class SshException extends RuntimeException {
  public SshException() {
  }

  public SshException(String message) {
    super(message);
  }

  public SshException(Throwable cause) {
    super(cause);
  }

  public SshException(String message, Throwable cause) {
    super(message, cause);
  }
}
