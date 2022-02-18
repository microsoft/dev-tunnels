package com.microsoft.tunnels;

import com.microsoft.tunnels.management.ProductHeaderValue;
import com.microsoft.tunnels.management.TunnelManagementClient;

/**
 * Used for local manual testing.
 */
public class ConnectionTest {
    private static String AuthToken = "USER_TOKEN";

    public static void main(String[] args) {
        var userAgent = new ProductHeaderValue("tunnels-java-sdk", "0");
        TunnelManagementClient managementClient = new TunnelManagementClient(userAgent, () -> "Bearer " + AuthToken);
        var tunnels = managementClient.listTunnelsAsync(null, null).join();
        if (tunnels != null && !tunnels.isEmpty()) {
            tunnels.forEach((tunnel) -> {
                System.out.println(tunnel.name);
                tunnel.endpoints.forEach((endpoint) -> System.out.println(endpoint));
            });
        }
    }
}
