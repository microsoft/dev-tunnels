# Getting Started

## Client Example

To use the client example you must do the following setup first:

1. Create a tunnel on the CLI or another SDK and put the tunnelId and clusterId in the constants section of example.go
2. Create ports on the tunnel that you want to be hosted
3. Get a tunnels access token and paste it in the return value of getAccessToken() in example.go or set it as the TUNNELS_TOKEN environment variable
4. Start hosting the tunnel either on the CLI or on a different SDK
5. Run example.go with the command `go run example.go`

## Host Example

To use the host example:

1. Create a tunnel on the CLI or management API
2. Get a host access token for the tunnel and set it as the TUNNELS_TOKEN environment variable
3. Set the `hostTunnelID` and `hostClusterID` constants in host/host_example.go
4. Set the `localPort` constant to the local TCP port you want to forward (default: 8080)
5. Start a local service on that port (e.g., `python -m http.server 8080`)
6. Run the host: `cd host && TUNNELS_TOKEN=<token> go run host_example.go`

The host will:
- Connect to the relay and register an endpoint
- Forward the specified local port to remote clients
- Automatically reconnect if the relay connection drops
- Shut down gracefully on Ctrl+C (unregisters the endpoint)

### Host API Overview

```go
// Create a host
host, err := tunnels.NewHost(logger, manager)

// Optional: enable reconnection and status callbacks
host.EnableReconnect = true
host.ConnectionStatusChanged = func(prev, curr tunnels.ConnectionStatus) { ... }

// Connect to the relay
host.Connect(ctx, tunnel)

// Add/remove forwarded ports dynamically
host.AddPort(ctx, &tunnels.TunnelPort{PortNumber: 8080})
host.RemovePort(ctx, 8080)

// Sync ports with the management service
host.RefreshPorts(ctx)

// Block until disconnected (reconnects automatically if enabled)
host.Wait()

// Graceful shutdown
host.Close()
```
