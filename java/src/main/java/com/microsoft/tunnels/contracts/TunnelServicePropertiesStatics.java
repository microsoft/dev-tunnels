// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

package com.microsoft.tunnels.contracts;

import java.util.Locale;

import org.apache.maven.shared.utils.StringUtils;

class TunnelServicePropertiesStatics {
  /**
   * Gets production service properties.
   */
  static final TunnelServiceProperties production = new TunnelServiceProperties(
      "https://" + TunnelServiceProperties.prodDnsName + "/",
      TunnelServiceProperties.prodFirstPartyAppId,
      TunnelServiceProperties.prodThirdPartyAppId,
      TunnelServiceProperties.prodGitHubAppClientId);

  /**
   * Gets properties for the service in the staging environment (PPE).
   */
  static final TunnelServiceProperties staging = new TunnelServiceProperties(
      "https://" + TunnelServiceProperties.ppeDnsName + "/",
      TunnelServiceProperties.ppeFirstPartyAppId,
      TunnelServiceProperties.ppeThirdPartyAppId,
      TunnelServiceProperties.nonProdGitHubAppClientId);

  /**
   * Gets properties for the service in the development environment.
   */
  static final TunnelServiceProperties development = new TunnelServiceProperties(
      "https://" + TunnelServiceProperties.devDnsName + "/",
      TunnelServiceProperties.devFirstPartyAppId,
      TunnelServiceProperties.devThirdPartyAppId,
      TunnelServiceProperties.nonProdGitHubAppClientId);

  public static TunnelServiceProperties environment(String environmentName) {
    if (StringUtils.isBlank(environmentName)) {
      throw new IllegalArgumentException(environmentName);
    }

    switch (environmentName.toLowerCase(Locale.ROOT)) {
      case "prod":
      case "production":
        return TunnelServiceProperties.production;
      case "ppe":
      case "preprod":
      case "staging":
        return TunnelServiceProperties.staging;
      case "dev":
      case "development":
        return TunnelServiceProperties.development;
      default:
        throw new IllegalArgumentException("Invalid service environment: " + environmentName);
    }
  }
}
