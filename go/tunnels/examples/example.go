// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

package main

import (
	"context"
	"errors"
	"fmt"
	"log"
	"net/url"
	"os"

	tunnels "github.com/microsoft/dev-tunnels/go/tunnels"
)

// Set the tunnelId and cluster Id for the tunnels you want to connect to
const (
	tunnelId  = ""
	clusterId = "usw2"
)

var (
	uri       = tunnels.ServiceProperties.ServiceURI
	userAgent = []tunnels.UserAgent{{Name: "Tunnels-Go-SDK-Example", Version: "0.0.1"}}
	ctx       = context.Background()
)

// Put your tunnels access token in the return statement or set the TUNNELS_TOKEN env variable
func getAccessToken() string {
	if token := os.Getenv("TUNNELS_TOKEN"); token != "" {
		return token
	}
	return ""
}

func main() {
	logger := log.New(os.Stdout, "", log.LstdFlags)

	url, err := url.Parse(uri)
	if err != nil {
		fmt.Println(fmt.Errorf(err.Error()))
		return
	}

	// create manager to get tunnel
	managementClient, err := tunnels.NewManager(userAgent, getAccessToken, url, nil, "2023-09-27-preview")
	if err != nil {
		fmt.Println(fmt.Errorf(err.Error()))
		return
	}

	limits, err := managementClient.ListUserLimits(ctx)
	if err != nil {
		fmt.Println(fmt.Errorf(err.Error()))
		return
	}

	logger.Printf("Successfully retrieved %d user limit(s)", len(limits))

	// set up options to request a connect token
	options := &tunnels.TunnelRequestOptions{IncludePorts: true, TokenScopes: []tunnels.TunnelAccessScope{"connect"}}

	newTunnel := &tunnels.Tunnel{
		TunnelID:  tunnelId,
		ClusterID: clusterId,
	}

	// get tunnel for connection
	getTunnel, err := managementClient.GetTunnel(ctx, newTunnel, options)
	if err != nil {
		fmt.Println(fmt.Errorf(err.Error()))
		return
	}
	if getTunnel.TunnelID == "" {
		fmt.Println(fmt.Errorf(err.Error()))
		return
	} else {
		logger.Printf(fmt.Sprintf("Got tunnel with id %s", getTunnel.TunnelID))
	}

	// create channels for errors and listeners
	done := make(chan error)

	go func() {
		// start client connection to tunnel
		c, err := tunnels.NewClient(logger, getTunnel, true)
		c.Connect(ctx, "")
		if err != nil {
			done <- fmt.Errorf("connect failed: %v", err)
			return
		}
		if c == nil {
			done <- errors.New("nil connection")
			return
		}
	}()
	for {
		select {
		case err := <-done:
			if err != nil {
				fmt.Println(fmt.Errorf(err.Error()))
			}
			return
		case <-ctx.Done():
			return
		}
	}
}
