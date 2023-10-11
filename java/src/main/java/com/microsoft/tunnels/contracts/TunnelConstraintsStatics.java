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

  static boolean isValidOldTunnelId(String tunnelId) {
    if (tunnelId == null || tunnelId.length() != TunnelConstraints.oldTunnelIdLength) {
      return false;
    }

    var matcher = TunnelConstraints.oldTunnelIdRegex.matcher(tunnelId);
    return matcher.find() && matcher.start() == 0 && matcher.end() == tunnelId.length();
  }

  static boolean isValidNewTunnelId(String tunnelId) {
    if (tunnelId == null || tunnelId.length() < TunnelConstraints.newTunnelIdMinLength  || tunnelId.length() > TunnelConstraints.newTunnelIdMaxLength) {
      return false;
    }

    var matcher = TunnelConstraints.newTunnelIdRegex.matcher(tunnelId);
    return matcher.find() && matcher.start() == 0 && matcher.end() == tunnelId.length();
  }


  static boolean isValidTunnelAlias(String alias) {
    if (alias == null || alias.length() != TunnelConstraints.tunnelAliasLength) {
      return false;
    }

    var matcher = TunnelConstraints.tunnelAliasRegex.matcher(alias);
    return matcher.find() && matcher.start() == 0 && matcher.end() == alias.length();
  }

  static boolean isValidTunnelName(String tunnelName) {
    if (StringUtils.isBlank(tunnelName)) {
      return false;
    }

    var matcher = TunnelConstraints.tunnelNameRegex.matcher(tunnelName);
    return matcher.find() && matcher.start() == 0 && matcher.end() == tunnelName.length() &&
      !isValidOldTunnelId(tunnelName);
  }

  static boolean isValidTag(String tag) {
    if (StringUtils.isBlank(tag)) {
      return false;
    }

    var matcher = TunnelConstraints.labelRegex.matcher(tag);
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

  static String validateOldTunnelId(String tunnelId, String paramName) {
    if (StringUtils.isBlank(tunnelId)) {
      throw new IllegalArgumentException(tunnelId);
    }
    if (!isValidOldTunnelId(tunnelId)) {
      throw new IllegalArgumentException("Invalid tunnel id: " + tunnelId);
    }
    return tunnelId;
  }

  static String validateNewTunnelId(String tunnelId, String paramName) {
    if (StringUtils.isBlank(tunnelId)) {
      throw new IllegalArgumentException(tunnelId);
    }
    if (!isValidNewTunnelId(tunnelId)) {
      throw new IllegalArgumentException("Invalid tunnel id: " + tunnelId);
    }
    return tunnelId;
  }

  static String validateNewOrOldTunnelId(String tunnelId, String paramName) {
    try {
      return validateNewTunnelId(tunnelId, paramName);
    } catch (IllegalArgumentException e) {
      return validateOldTunnelId(tunnelId, paramName);
    }
  }

    static String validateTunnelAlias(String alias, String paramName) {
    if (StringUtils.isBlank(alias)) {
      throw new IllegalArgumentException(alias);
    }
    if (!isValidTunnelAlias(alias)) {
      throw new IllegalArgumentException("Invalid tunnel id: " + alias);
    }
    return alias;
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
