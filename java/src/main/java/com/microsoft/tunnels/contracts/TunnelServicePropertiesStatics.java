package com.microsoft.tunnels.contracts;

public class TunnelServicePropertiesStatics {
  /**
   * Gets production service properties.
   */
  public static final TunnelServiceProperties production = new TunnelServiceProperties(
      "https://" + TunnelServiceProperties.prodDnsName + "/",
      TunnelServiceProperties.prodFirstPartyAppId,
      TunnelServiceProperties.prodThirdPartyAppId,
      TunnelServiceProperties.prodGitHubAppClientId);

  /**
   * Gets properties for the service in the staging environment (PPE).
   */
  public static final TunnelServiceProperties staging = new TunnelServiceProperties(
      "https://" + TunnelServiceProperties.ppeDnsName + "/",
      TunnelServiceProperties.nonProdFirstPartyAppId,
      TunnelServiceProperties.ppeThirdPartyAppId,
      TunnelServiceProperties.nonProdGitHubAppClientId);

  /**
   * Gets properties for the service in the development environment.
   */
  public static final TunnelServiceProperties development = new TunnelServiceProperties(
      "https://" + TunnelServiceProperties.devDnsName + "/",
      TunnelServiceProperties.nonProdFirstPartyAppId,
      TunnelServiceProperties.devThirdPartyAppId,
      TunnelServiceProperties.nonProdGitHubAppClientId);
}
