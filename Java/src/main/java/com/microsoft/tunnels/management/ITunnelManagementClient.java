package com.microsoft.tunnels.management;

import java.util.Collection;
import java.util.concurrent.CompletableFuture;

import com.microsoft.tunnels.contracts.Tunnel;
import com.microsoft.tunnels.contracts.TunnelConnectionMode;
import com.microsoft.tunnels.contracts.TunnelEndpoint;
import com.microsoft.tunnels.contracts.TunnelPort;

public interface ITunnelManagementClient {
    public CompletableFuture<Collection<Tunnel>> listTunnelsAsync(String clusterId, TunnelRequestOptions options);

    public CompletableFuture<Collection<Tunnel>> searchTunnelsAsync(String[] tags, boolean requireAllTags,
            String clusterId, String domain, TunnelRequestOptions options);

    public CompletableFuture<Tunnel> getTunnelAsync(Tunnel tunnel, TunnelRequestOptions options);

    public CompletableFuture<Tunnel> createTunnelAsync(Tunnel tunnel, TunnelRequestOptions options);

    public CompletableFuture<Tunnel> updateTunnelAsync(Tunnel tunnel, TunnelRequestOptions options);

    public CompletableFuture<Boolean> deleteTunnelAsync(Tunnel tunnel, TunnelRequestOptions options);

    public CompletableFuture<Boolean> updateTunnelEndpointsAsync(Tunnel tunnel, TunnelEndpoint endpoint,
            TunnelRequestOptions options);

    public CompletableFuture<Boolean> deleteTunnelEndpointsAsync(Tunnel tunnel, String hostId,
            TunnelConnectionMode tunnelConnectionMode, TunnelRequestOptions options);

    public CompletableFuture<Collection<TunnelPort>> listTunnelPortsAsync(Tunnel tunnel, TunnelRequestOptions options);

    public CompletableFuture<TunnelPort> getTunnelPortAsync(Tunnel tunnel, int portNumber,
            TunnelRequestOptions options);

    public CompletableFuture<TunnelPort> createTunnelPortAsync(Tunnel tunnel, TunnelPort tunnelPort,
            TunnelRequestOptions options);

    public CompletableFuture<TunnelPort> updateTunnelPortAsync(Tunnel tunnel, TunnelPort tunnelPort,
            TunnelRequestOptions options);

    public CompletableFuture<Boolean> deleteTunnelPortAsync(Tunnel tunnel, int portNumber,
            TunnelRequestOptions options);
}
