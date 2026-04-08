// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

// This example demonstrates how to use the Go SDK to host a tunnel,
// forwarding a local TCP port to remote clients through the relay.
//
// Prerequisites:
//   - A tunnel created via the CLI or management API
//   - A host access token for the tunnel
//
// Usage:
//   TUNNELS_TOKEN=<host-access-token> go run host_example.go

package main

import (
	"context"
	"fmt"
	"log"
	"net/url"
	"os"
	"os/signal"
	"syscall"

	tunnels "github.com/microsoft/dev-tunnels/go/tunnels"
)

// Set the tunnel ID and cluster ID for the tunnel you want to host.
const (
	hostTunnelID  = ""
	hostClusterID = "usw2"

	// The local port to forward through the tunnel.
	localPort = 8080
)

var (
	hostURI       = tunnels.ServiceProperties.ServiceURI
	hostUserAgent = []tunnels.UserAgent{{Name: "Tunnels-Go-SDK-Host-Example", Version: "0.0.1"}}
)

// getHostAccessToken returns the host access token from the TUNNELS_TOKEN
// environment variable.
func getHostAccessToken() string {
	if token := os.Getenv("TUNNELS_TOKEN"); token != "" {
		return token
	}
	return ""
}

func main() {
	ctx, stop := signal.NotifyContext(context.Background(), syscall.SIGINT, syscall.SIGTERM)
	defer stop()

	logger := log.New(os.Stdout, "[host] ", log.LstdFlags)

	parsedURL, err := url.Parse(hostURI)
	if err != nil {
		logger.Fatalf("Failed to parse service URI: %v", err)
	}

	// Create management client.
	mgr, err := tunnels.NewManager(hostUserAgent, getHostAccessToken, parsedURL, nil, "2023-09-27-preview")
	if err != nil {
		logger.Fatalf("Failed to create manager: %v", err)
	}

	// Fetch the tunnel with a host access token.
	tunnel := &tunnels.Tunnel{
		TunnelID:  hostTunnelID,
		ClusterID: hostClusterID,
	}
	options := &tunnels.TunnelRequestOptions{
		IncludePorts: true,
		TokenScopes:  []tunnels.TunnelAccessScope{"host"},
	}

	tunnel, err = mgr.GetTunnel(ctx, tunnel, options)
	if err != nil {
		logger.Fatalf("Failed to get tunnel: %v", err)
	}
	logger.Printf("Got tunnel: %s", tunnel.TunnelID)

	// Create the host.
	host, err := tunnels.NewHost(logger, mgr)
	if err != nil {
		logger.Fatalf("Failed to create host: %v", err)
	}

	// Optional: enable automatic reconnection on relay disconnect.
	host.EnableReconnect = true

	// Optional: log connection status changes.
	host.ConnectionStatusChanged = func(prev, curr tunnels.ConnectionStatus) {
		logger.Printf("Connection status: %v -> %v", prev, curr)
	}

	// Connect to the relay.
	if err := host.Connect(ctx, tunnel); err != nil {
		logger.Fatalf("Failed to connect: %v", err)
	}
	logger.Printf("Connected to relay")

	// Add a port to forward. This registers the port with the management API
	// and notifies any connected clients via SSH tcpip-forward.
	port := &tunnels.TunnelPort{PortNumber: localPort}
	if err := host.AddPort(ctx, port); err != nil {
		logger.Fatalf("Failed to add port: %v", err)
	}
	logger.Printf("Forwarding local port %d", localPort)

	// Wait for the relay connection (blocks until disconnect or signal).
	// With EnableReconnect=true, this will automatically reconnect on drops.
	go func() {
		if err := host.Wait(); err != nil {
			logger.Printf("Relay connection ended: %v", err)
		}
	}()

	// Wait for interrupt signal.
	<-ctx.Done()
	logger.Printf("Shutting down...")

	// Close gracefully: closes the SSH session and unregisters the endpoint.
	if err := host.Close(); err != nil {
		logger.Printf("Close error: %v", err)
	}
	logger.Printf("Host shut down")

	fmt.Println("Done.")
}
