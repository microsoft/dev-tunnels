// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

package com.microsoft.tunnels.contracts;

import java.net.URI;

import org.apache.maven.shared.utils.StringUtils;

class TunnelEndpointStatics {
  public static java.net.URI getPortUri(TunnelEndpoint endpoint, int portNumber) {
    if (StringUtils.isBlank(endpoint.portUriFormat)) {
      return null;
    }
    return URI.create(endpoint.portUriFormat.replace(
        TunnelEndpoint.portToken, Integer.toString(portNumber)));
  }

  public static String getPortSshCommand(TunnelEndpoint endpoint, int portNumber) {
    if (StringUtils.isBlank(endpoint.portSshCommandFormat)) {
      return null;
    }
    return endpoint.portSshCommandFormat.replace(
        TunnelEndpoint.portToken, Integer.toString(portNumber));
  }
}
