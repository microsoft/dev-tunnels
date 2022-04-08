package main

import (
	"context"
	"errors"
	"fmt"
	"log"
	"net"
	"net/url"
	"os"

	tunnels "github.com/microsoft/tunnels/go"
)

// Set the tunnelId and cluster Id for the tunnels you want to connect to
const (
	tunnelId                      = "l52bmg0h"
	clusterId                     = "usw2"
	portToConnect1                = 5001
	portToConnect1ListenerAddress = 5030
	portToConnect2                = 5002
	portToConnect2ListenerAddress = 5031
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
	managementClient, err := tunnels.NewManager(userAgent, getAccessToken, url, nil)
	if err != nil {
		fmt.Println(fmt.Errorf(err.Error()))
		return
	}

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
		logger.Println(fmt.Sprintf("Got tunnel with id %s", getTunnel.TunnelID))
	}

	// create channels for errors and listeners
	done := make(chan error)
	listeners := make(chan net.Listener, 2)

	go func() {
		// start client connection to tunnel
		c, err := tunnels.Connect(context.Background(), logger, getTunnel, "")
		if err != nil {
			done <- fmt.Errorf("connect failed: %v", err)
			return
		}
		if c == nil {
			done <- errors.New("nil connection")
			return
		}

		// create listener to connect to port using supplied port number
		listen, err := net.Listen("tcp", fmt.Sprintf(":%d", portToConnect1ListenerAddress))

		// send listener to channel to be closed at end of run
		listeners <- listen
		if err != nil {
			done <- fmt.Errorf("failed to listen: %v", err)
		}

		// wait for port to be forwarded and then connect
		c.WaitForForwardedPort(ctx, portToConnect1)
		go func() {
			done <- c.ConnectToForwardedPort(ctx, listen, portToConnect1)
		}()

		// create listener to connect to port using supplied port number
		listen2, err := net.Listen("tcp", fmt.Sprintf(":%d", portToConnect2ListenerAddress))

		// send listener to channel to be closed at end of run
		listeners <- listen2
		if err != nil {
			done <- fmt.Errorf("failed to listen: %v", err)
		}

		// wait for port to be forwarded and then connect
		c.WaitForForwardedPort(ctx, portToConnect2)
		go func() {
			done <- c.ConnectToForwardedPort(ctx, listen2, portToConnect2)
		}()
	}()
	for {
		select {
		case err := <-done:
			if err != nil {
				fmt.Println(fmt.Errorf(err.Error()))
			}
			break
		case <-ctx.Done():
			break
		case listener := <-listeners:
			defer listener.Close()
		}
	}

}
