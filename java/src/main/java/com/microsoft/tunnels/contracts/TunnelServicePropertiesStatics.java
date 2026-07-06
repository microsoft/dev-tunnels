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
      TunnelServiceProperties.ppeGitHubAppClientId);

  /**
   * Gets properties for the service in the development environment.
   */
  static final TunnelServiceProperties development = new TunnelServiceProperties(
      "https://" + TunnelServiceProperties.devDnsName + "/",
      TunnelServiceProperties.devServiceAppId,
      TunnelServiceProperties.devThirdPartyAppId,
      TunnelServiceProperties.devGitHubAppClientId);

  /**
   * Gets properties for the service when running locally.
   *
   * Uses the same service app IDs as the development environment, but a different
   * GitHub app with localhost callback URLs.
   */
  static final TunnelServiceProperties local = new TunnelServiceProperties(
      "https://" + TunnelServiceProperties.localDnsName + "/",
      TunnelServiceProperties.devServiceAppId,
      TunnelServiceProperties.devThirdPartyAppId,
      TunnelServiceProperties.localGitHubAppClientId);

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
      case "local":
        return TunnelServiceProperties.local;
      default:
        throw new IllegalArgumentException("Invalid service environment: " + environmentName);
    }
  }
}
