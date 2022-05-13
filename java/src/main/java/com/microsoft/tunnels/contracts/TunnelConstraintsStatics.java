// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

package com.microsoft.tunnels.contracts;

import org.apache.maven.shared.utils.StringUtils;

class TunnelConstraintsStatics {
  static boolean isValidClusterId(String clusterId) {
    var matcher = TunnelConstraints.clusterIdRegex.matcher(clusterId);
    return !StringUtils.isBlank(clusterId)
        && matcher.find()
        && matcher.group(0).equals(clusterId);
  }

  static boolean isValidTunnelId(String tunnelId) {
    return !StringUtils.isBlank(tunnelId)
        && tunnelId.length() == TunnelConstraints.tunnelIdLength
        && TunnelConstraints.tunnelIdRegex.matcher(tunnelId).find();
  }

  static boolean isValidTunnelName(String tunnelName) {
    var matcher = TunnelConstraints.tunnelNameRegex.matcher(tunnelName);
    return !StringUtils.isBlank(tunnelName)
        && matcher.find()
        && matcher.group(0).equals(tunnelName);
  }

  static boolean isValidTunnelTag(String tag) {
    return !StringUtils.isBlank(tag)
        && TunnelConstraints.tunnelTagRegex.matcher(tag).find();
  }

  static boolean isValidTunnelIdOrName(String tunnelIdOrName) {
    return !StringUtils.isBlank(tunnelIdOrName)
        && TunnelConstraints.tunnelTagRegex.matcher(tunnelIdOrName).find();
  }

  static String validateTunnelId(String tunnelId, String paramName) {
    if (StringUtils.isBlank(tunnelId)) {
      throw new IllegalArgumentException(tunnelId);
    }
    if (!isValidTunnelId(tunnelId)) {
      throw new IllegalArgumentException("Invalid tunnel id: " + tunnelId);
    }
    return tunnelId;
  }

  static String validateTunnelIdOrName(String tunnelIdOrName, String paramName) {
    if (StringUtils.isBlank(tunnelIdOrName)) {
      throw new IllegalArgumentException(tunnelIdOrName);
    }
    if (!isValidTunnelIdOrName(tunnelIdOrName)) {
      throw new IllegalArgumentException("Invalid tunnel id or name: " + tunnelIdOrName);
    }
    return tunnelIdOrName;
  }
}
