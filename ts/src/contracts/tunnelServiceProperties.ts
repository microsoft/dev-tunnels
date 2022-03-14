/**
 * Provides environment-dependent properties about the service.
 */
export class TunnelServiceProperties {
    private static readonly prodAppId = '46da2f7e-b5ef-422a-88d4-2a7f9de6a0b2';
    private static readonly nonProdAppId = '54c45752-bacd-424a-b928-652f3eca2b18';
    private static readonly nonProdGitHubAppClientId = 'Iv1.b231c327f1eaa229';
    private static readonly prodGitHubAppClientId = 'Iv1.e7b89e013f801f03';

    /**
     * Gets the base URI of the service.
     */
    public readonly serviceUri: string;

    /**
     * Gets the AAD AppId for the service.
     */
    public readonly serviceAppId: string;

    /**
     * Gets the client ID for the service's GitHub app.
     *
     */
    public readonly githubAppClientId: string;

    private constructor(serviceUri: string, serviceAppId: string, gitHubAppClientId: string) {
        this.serviceUri = serviceUri;
        this.serviceAppId = serviceAppId;
        this.githubAppClientId = gitHubAppClientId;
    }

    /**
     * Gets production service properties.
     */
    public static readonly production = new TunnelServiceProperties(
        'https://global.rel.tunnels.api.visualstudio.com/',
        TunnelServiceProperties.prodAppId,
        TunnelServiceProperties.prodGitHubAppClientId,
    );

    /**
     * Gets properties for the service in the staging environment (PPE).
     */
    public static readonly staging = new TunnelServiceProperties(
        'https://global.rel.tunnels.ppe.api.visualstudio.com/',
        TunnelServiceProperties.nonProdAppId,
        TunnelServiceProperties.nonProdGitHubAppClientId,
    );

    /**
     * Gets properties for the service in the development environment.
     */
    public static readonly development = new TunnelServiceProperties(
        'https://global.ci.tunnels.dev.api.visualstudio.com/',
        TunnelServiceProperties.nonProdAppId,
        TunnelServiceProperties.nonProdGitHubAppClientId,
    );

    /**
     * @param environmentName
     * @returns Properties for the service in the specified environment.
     */
    public static environment(environmentName: string): TunnelServiceProperties {
        if (!environmentName) {
            throw new Error(`Invalid argument: ${environmentName}`);
        }

        switch (environmentName.toLowerCase()) {
            case 'prod':
            case 'production':
                return this.production;
            case 'ppe':
            case 'preprod':
                return this.staging;
            case 'dev':
            case 'development':
                return this.development;
            default:
                throw new Error(`Invalid service environment: ${environmentName}`);
        }
    }
}
