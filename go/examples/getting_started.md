# Getting Started

To use the example you must do the following setup first:

1. Create a tunnel on the CLI or another SDK and put the tunnelId and clusterId in the constants section of example.go
2. Create ports on the tunnel that you want to be hosted
3. Get a tunnels access token and paste it in the return value of getAccessToken() in example.go or set it as the TUNNELS_TOKEN environment variable
4. Start hosting the tunnel either on the CLI or on a different SDK
5. Run example.go with the command `go run example.go`