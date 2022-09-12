// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

package com.microsoft.tunnels.contracts;

import java.net.URI;

import org.apache.maven.shared.utils.StringUtils;

class TunnelEndpointStatics {
  public static java.net.URI getPortUri(TunnelEndpoint endpoint, int portNumber) {
    if (portNumber == 0 && !StringUtils.isBlank(endpoint.tunnelUri)) {
      return URI.create(endpoint.tunnelUri);
    }

    if (StringUtils.isBlank(endpoint.portUriFormat)) {
      return null;
    }
    return URI.create(endpoint.portUriFormat.replace(
        TunnelEndpoint.portToken, Integer.toString(portNumber)));
  }

  public static String getPortSshCommand(TunnelEndpoint endpoint, int portNumber) {
    if (portNumber == 0 && !StringUtils.isBlank(endpoint.tunnelSshCommand)) {
      return endpoint.tunnelSshCommand;
    }

    if (StringUtils.isBlank(endpoint.portSshCommandFormat)) {
      return null;
    }
    return endpoint.portSshCommandFormat.replace(
        TunnelEndpoint.portToken, Integer.toString(portNumber));
  }
}
