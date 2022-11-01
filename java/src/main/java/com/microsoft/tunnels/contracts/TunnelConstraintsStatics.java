// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

package com.microsoft.tunnels.contracts;

import org.apache.maven.shared.utils.StringUtils;

class TunnelConstraintsStatics {
  static boolean isValidClusterId(String clusterId) {
    if (StringUtils.isBlank(clusterId)) {
      return false;
    }

    var matcher = TunnelConstraints.clusterIdRegex.matcher(clusterId);
    return matcher.find() && matcher.start() == 0 && matcher.end() == clusterId.length();
  }

  static boolean isValidTunnelId(String tunnelId) {
    if (tunnelId == null || tunnelId.length() != TunnelConstraints.tunnelIdLength) {
      return false;
    }

    var matcher = TunnelConstraints.tunnelIdRegex.matcher(tunnelId);
    return matcher.find() && matcher.start() == 0 && matcher.end() == tunnelId.length();
  }

  static boolean isValidTunnelName(String tunnelName) {
    if (StringUtils.isBlank(tunnelName)) {
      return false;
    }

    var matcher = TunnelConstraints.tunnelNameRegex.matcher(tunnelName);
    return matcher.find() && matcher.start() == 0 && matcher.end() == tunnelName.length() &&
      !isValidTunnelId(tunnelName);
  }

  static boolean isValidTag(String tag) {
    if (StringUtils.isBlank(tag)) {
      return false;
    }

    var matcher = TunnelConstraints.tagRegex.matcher(tag);
    return matcher.find() && matcher.start() == 0 && matcher.end() == tag.length();
  }

  static boolean isValidTunnelIdOrName(String tunnelIdOrName) {
    if (StringUtils.isBlank(tunnelIdOrName)) {
      return false;
    }

    // Tunnel ID Regex is a subset of Tunnel name Regex
    var matcher = TunnelConstraints.tunnelNameRegex.matcher(tunnelIdOrName);
    return matcher.find() && matcher.start() == 0 && matcher.end() == tunnelIdOrName.length();
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
