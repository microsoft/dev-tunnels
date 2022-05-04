package com.microsoft.tunnels.contracts;

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
      TunnelServiceProperties.nonProdFirstPartyAppId,
      TunnelServiceProperties.ppeThirdPartyAppId,
      TunnelServiceProperties.nonProdGitHubAppClientId);

  /**
   * Gets properties for the service in the development environment.
   */
  static final TunnelServiceProperties development = new TunnelServiceProperties(
      "https://" + TunnelServiceProperties.devDnsName + "/",
      TunnelServiceProperties.nonProdFirstPartyAppId,
      TunnelServiceProperties.devThirdPartyAppId,
      TunnelServiceProperties.nonProdGitHubAppClientId);

  public static TunnelServiceProperties environment(String environmentName) {
    throw new UnsupportedOperationException("Method not implemented");
  }
}
