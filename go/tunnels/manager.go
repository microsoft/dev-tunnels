// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

package tunnels

import (
	"bytes"
	"context"
	"crypto/tls"
	"encoding/json"
	"fmt"
	"io"
	"net/http"
	"net/url"
	"reflect"
	"strconv"
	"strings"
)

var ServiceProperties = TunnelServiceProperties{
	ServiceURI:           fmt.Sprintf("https://%s/", prodDnsName),
	ServiceAppID:         prodFirstPartyAppID,
	ServiceInternalAppID: prodThirdPartyAppID,
	GitHubAppClientID:    prodGitHubAppClientID,
}

var PpeServiceProperties = TunnelServiceProperties{
	ServiceURI:           fmt.Sprintf("https://%s/", ppeDnsName),
	ServiceAppID:         nonProdFirstPartyAppID,
	ServiceInternalAppID: ppeThirdPartyAppID,
	GitHubAppClientID:    nonProdGitHubAppClientID,
}

var DevServiceProperties = TunnelServiceProperties{
	ServiceURI:           fmt.Sprintf("https://%s/", devDnsName),
	ServiceAppID:         nonProdFirstPartyAppID,
	ServiceInternalAppID: devThirdPartyAppID,
	GitHubAppClientID:    nonProdGitHubAppClientID,
}

type tokenProviderfn func() string

const (
	apiV1Path                  = "/api/v1"
	tunnelsApiPath             = apiV1Path + "/tunnels"
	subjectsApiPath            = apiV1Path + "/subjects"
	endpointsApiSubPath        = "/endpoints"
	portsApiSubPath            = "/ports"
	tunnelAuthenticationScheme = "Tunnel"
	goUserAgent                = "Visual-Studio-Tunnel-Service-Go-SDK/" + PackageVersion
)

var (
	defaultServiceUrl = ServiceProperties.ServiceURI
)

var (
	manageAccessTokenScope       = []TunnelAccessScope{TunnelAccessScopeManage}
	hostAccessTokenScope         = []TunnelAccessScope{TunnelAccessScopeHost}
	hostOrManageAccessTokenScope = []TunnelAccessScope{TunnelAccessScopeManage, TunnelAccessScopeHost}
	readAccessTokenScope         = []TunnelAccessScope{TunnelAccessScopeManage, TunnelAccessScopeHost, TunnelAccessScopeConnect}
)

// UserAgent contains the name and version of the client.
type UserAgent struct {
	Name    string
	Version string
}

// Manager is used to interact with the Visual Studio Tunnel Service APIs.
type Manager struct {
	tokenProvider     tokenProviderfn
	httpClient        *http.Client
	uri               *url.URL
	additionalHeaders map[string]string
	userAgents        []UserAgent
}

// Creates a new Manager used for interacting with the Tunnels APIs.
// tokenProvider is an optional paramater containing a function that returns the access token to use for the request.
// If no tunnelServiceUrl or httpClient is provided, the default values will be used.
// Can return error if userAgent is empty or url is invalid.
func NewManager(userAgents []UserAgent, tp tokenProviderfn, tunnelServiceUrl *url.URL, httpHandler *http.Client) (*Manager, error) {
	if len(userAgents) == 0 {
		return nil, fmt.Errorf("user agents cannot be empty")
	}
	if tp == nil {
		tp = func() string {
			return ""
		}
	}

	if tunnelServiceUrl == nil {
		url, err := url.Parse(defaultServiceUrl)
		if err != nil {
			return nil, fmt.Errorf("error parsing default url %w", err)
		}
		tunnelServiceUrl = url
	}

	var client *http.Client
	if httpHandler == nil {
		if strings.Contains(tunnelServiceUrl.Host, "localhost") {
			client = &http.Client{Transport: &http.Transport{TLSClientConfig: &tls.Config{InsecureSkipVerify: true}}}
		} else {
			client = &http.Client{}
		}
	} else {
		client = httpHandler
	}

	return &Manager{tokenProvider: tp, httpClient: client, uri: tunnelServiceUrl, userAgents: userAgents}, nil
}

// Lists all tunnels owned by the authenticated user.
// Returns a list of tunnels or an error if the search fails.
func (m *Manager) ListTunnels(
	ctx context.Context, clusterID string, domain string, options *TunnelRequestOptions,
) (ts []*Tunnel, err error) {
	queryParams := url.Values{}
	if clusterID == "" {
		queryParams.Add("global", "true")
	}
	if domain != "" {
		queryParams.Add("domain", domain)
	}
	url := m.buildUri(clusterID, tunnelsApiPath, options, queryParams.Encode())
	response, err := m.sendTunnelRequest(ctx, nil, options, http.MethodGet, url, nil, nil, readAccessTokenScope, false)
	if err != nil {
		return nil, fmt.Errorf("error sending list tunnel request: %w", err)
	}

	err = json.Unmarshal(response, &ts)
	if err != nil {
		return nil, fmt.Errorf("error parsing response json to tunnel: %w", err)
	}

	return ts, nil
}

// Search tunnels that the authenticated user has access to based on tags.
// If requireAllTags is true then tunnels returned must contain all tags in the tags slice.
// Returns a slice of the found tunnels or an error if the search fails.
func (m *Manager) SearchTunnels(
	ctx context.Context, tags []string, requireAllTags bool, clusterID string, domain string, options *TunnelRequestOptions,
) (ts []*Tunnel, err error) {
	queryParams := url.Values{}
	if clusterID == "" {
		queryParams.Add("global", "true")
	}
	if domain != "" {
		queryParams.Add("domain", domain)
	}
	queryParams.Add("allTags", strconv.FormatBool(requireAllTags))
	tagString := strings.Join(tags, ",")
	queryParams.Add("tags", tagString)

	url := m.buildUri(clusterID, tunnelsApiPath, options, queryParams.Encode())
	response, err := m.sendTunnelRequest(ctx, nil, options, http.MethodGet, url, nil, nil, readAccessTokenScope, false)
	if err != nil {
		return nil, fmt.Errorf("error sending search tunnel request: %w", err)
	}

	err = json.Unmarshal(response, &ts)
	if err != nil {
		return nil, fmt.Errorf("error parsing response json to tunnel: %w", err)
	}

	return ts, nil
}

// Gets a tunnel by id or name.
// If getting a tunnel by name the domain must be provided if the tunnel is not in the default domain.
// Returns the requested tunnel or an error if the tunnel is not found.
func (m *Manager) GetTunnel(ctx context.Context, tunnel *Tunnel, options *TunnelRequestOptions) (t *Tunnel, err error) {
	url, err := m.buildTunnelSpecificUri(tunnel, "", options, "")
	if err != nil {
		return nil, fmt.Errorf("error creating tunnel url: %w", err)
	}

	response, err := m.sendTunnelRequest(ctx, tunnel, options, http.MethodGet, url, nil, nil, readAccessTokenScope, true)
	if err != nil {
		return nil, fmt.Errorf("error sending get tunnel request: %w", err)
	}

	// Read response into a tunnel
	err = json.Unmarshal(response, &t)
	if err != nil {
		return nil, fmt.Errorf("error parsing response json to tunnel: %w", err)
	}

	return t, err
}

// Creates a new tunnel with the properties specified in tunnel.
// Tunnel fields may be nil but the tunnel struct must not be nil.
// Returns the created tunnel or an error if the create fails.
func (m *Manager) CreateTunnel(ctx context.Context, tunnel *Tunnel, options *TunnelRequestOptions) (t *Tunnel, err error) {
	if tunnel == nil {
		return nil, fmt.Errorf("tunnel must be provided")
	}
	if tunnel.TunnelID != "" {
		return nil, fmt.Errorf("tunnelId cannot be set for creating a tunnel")
	}
	url := m.buildUri(tunnel.ClusterID, tunnelsApiPath, options, "")
	convertedTunnel, err := tunnel.requestObject()
	if err != nil {
		return nil, fmt.Errorf("error converting tunnel for request: %w", err)
	}
	response, err := m.sendTunnelRequest(ctx, tunnel, options, http.MethodPost, url, convertedTunnel, nil, manageAccessTokenScope, false)
	if err != nil {
		return nil, fmt.Errorf("error sending create tunnel request: %w", err)
	}

	// Read response into a tunnel
	err = json.Unmarshal(response, &t)
	if err != nil {
		return nil, fmt.Errorf("error parsing response json to tunnel: %w", err)
	}

	return t, err
}

// Updates a tunnel's properties, to update a field the field name must be included in updateFields.
// Returns the updated tunnel or an error if the update fails.
func (m *Manager) UpdateTunnel(ctx context.Context, tunnel *Tunnel, updateFields []string, options *TunnelRequestOptions) (t *Tunnel, err error) {
	if tunnel == nil {
		return nil, fmt.Errorf("tunnel must be provided")
	}

	url, err := m.buildTunnelSpecificUri(tunnel, "", options, "")
	if err != nil {
		return nil, fmt.Errorf("error creating request url: %w", err)
	}

	convertedTunnel, err := tunnel.requestObject()
	if err != nil {
		return nil, fmt.Errorf("error converting tunnel for request: %w", err)
	}
	response, err := m.sendTunnelRequest(ctx, tunnel, options, http.MethodPut, url, convertedTunnel, updateFields, manageAccessTokenScope, false)
	if err != nil {
		return nil, fmt.Errorf("error sending update tunnel request: %w", err)
	}

	// Read response into a tunnel
	err = json.Unmarshal(response, &t)
	if err != nil {
		return nil, fmt.Errorf("error parsing response json to tunnel: %w", err)
	}

	return t, err
}

// Deletes a tunnel.
// Returns error if delete fails.
func (m *Manager) DeleteTunnel(ctx context.Context, tunnel *Tunnel, options *TunnelRequestOptions) error {
	url, err := m.buildTunnelSpecificUri(tunnel, "", options, "")
	if err != nil {
		return fmt.Errorf("error creating tunnel url: %w", err)
	}
	_, err = m.sendTunnelRequest(ctx, tunnel, options, http.MethodDelete, url, nil, nil, manageAccessTokenScope, true)
	if err != nil {
		return fmt.Errorf("error sending delete tunnel request: %w", err)
	}

	return nil
}

// Updates an endpoint on a tunnel.
// Returns the updated endpoint or an error if the update fails.
func (m *Manager) UpdateTunnelEndpoint(
	ctx context.Context, tunnel *Tunnel, endpoint *TunnelEndpoint, updateFields []string, options *TunnelRequestOptions,
) (te *TunnelEndpoint, err error) {
	if endpoint == nil {
		return nil, fmt.Errorf("endpoint must be provided and must not be nil")
	}
	if endpoint.HostID == "" {
		return nil, fmt.Errorf("endpoint hostId must be provided and must not be nil")
	}
	url, err := m.buildTunnelSpecificUri(tunnel, fmt.Sprintf("%s/%s/%s", endpointsApiSubPath, endpoint.HostID, endpoint.ConnectionMode), options, "")
	if err != nil {
		return nil, fmt.Errorf("error creating tunnel url: %w", err)
	}

	response, err := m.sendTunnelRequest(ctx, tunnel, options, http.MethodPut, url, endpoint, updateFields, hostAccessTokenScope, false)
	if err != nil {
		return nil, fmt.Errorf("error sending update tunnel endpoint request: %w", err)
	}

	// Read response into a tunnel endpoint
	err = json.Unmarshal(response, &te)
	if err != nil {
		return nil, fmt.Errorf("error parsing response json to tunnel: %w", err)
	}

	var newEndpoints []TunnelEndpoint
	for _, ep := range tunnel.Endpoints {
		if ep.HostID != endpoint.HostID || ep.ConnectionMode != endpoint.ConnectionMode {
			newEndpoints = append(newEndpoints, ep)
		}
	}
	newEndpoints = append(newEndpoints, *te)
	tunnel.Endpoints = newEndpoints

	return te, err
}

// Deletes endpoints on a tunnel.
// Returns error if the delete fails.
func (m *Manager) DeleteTunnelEndpoints(
	ctx context.Context, tunnel *Tunnel, hostID string, connectionMode TunnelConnectionMode, options *TunnelRequestOptions,
) error {
	if hostID == "" {
		return fmt.Errorf("hostId must be provided and must not be nil")
	}
	path := fmt.Sprintf("%s/%s/%s", endpointsApiSubPath, hostID, connectionMode)
	if connectionMode == "" {
		path = fmt.Sprintf("%s/%s", endpointsApiSubPath, hostID)
	}
	url, err := m.buildTunnelSpecificUri(tunnel, path, options, "")
	if err != nil {
		return fmt.Errorf("error creating tunnel url: %w", err)
	}

	_, err = m.sendTunnelRequest(ctx, tunnel, options, http.MethodDelete, url, nil, nil, hostAccessTokenScope, true)
	if err != nil {
		return fmt.Errorf("error sending delete tunnel endpoint request: %w", err)
	}

	var newEndpoints []TunnelEndpoint
	for _, ep := range tunnel.Endpoints {
		if ep.HostID != hostID || ep.ConnectionMode != connectionMode {
			newEndpoints = append(newEndpoints, ep)
		}
	}
	tunnel.Endpoints = newEndpoints

	return err
}

// Lists all ports on the tunnel.
func (m *Manager) ListTunnelPorts(
	ctx context.Context, tunnel *Tunnel, options *TunnelRequestOptions,
) (tp []*TunnelPort, err error) {
	url, err := m.buildTunnelSpecificUri(tunnel, portsApiSubPath, options, "")
	if err != nil {
		return nil, fmt.Errorf("error creating tunnel url: %w", err)
	}

	response, err := m.sendTunnelRequest(ctx, tunnel, options, http.MethodGet, url, nil, nil, readAccessTokenScope, false)
	if err != nil {
		return nil, fmt.Errorf("error sending list tunnel ports request: %w", err)
	}

	// Read response into a tunnel port
	err = json.Unmarshal(response, &tp)
	if err != nil {
		return nil, fmt.Errorf("error parsing response json to tunnel ports: %w", err)
	}
	return tp, nil
}

func (m *Manager) GetTunnelPort(
	ctx context.Context, tunnel *Tunnel, port int, options *TunnelRequestOptions,
) (tp *TunnelPort, err error) {
	url, err := m.buildTunnelSpecificUri(tunnel, fmt.Sprintf("%s/%d", portsApiSubPath, port), options, "")
	if err != nil {
		return nil, fmt.Errorf("error creating tunnel url: %w", err)
	}

	response, err := m.sendTunnelRequest(ctx, tunnel, options, http.MethodGet, url, nil, nil, readAccessTokenScope, true)
	if err != nil {
		return nil, fmt.Errorf("error sending get tunnel port request: %w", err)
	}

	// Read response into a tunnel port
	err = json.Unmarshal(response, &tp)
	if err != nil {
		return nil, fmt.Errorf("error parsing response json to tunnel ports: %w", err)
	}
	return tp, nil
}

// Creates a port on the tunnel.
// Returns the created port or error if create fails.
func (m *Manager) CreateTunnelPort(
	ctx context.Context, tunnel *Tunnel, port *TunnelPort, options *TunnelRequestOptions,
) (tp *TunnelPort, err error) {
	url, err := m.buildTunnelSpecificUri(tunnel, portsApiSubPath, options, "")
	if err != nil {
		return nil, fmt.Errorf("error creating tunnel url: %w", err)
	}

	convertedPort, err := port.requestObject(tunnel)
	if err != nil {
		return nil, fmt.Errorf("error converting port for request: %w", err)
	}

	response, err := m.sendTunnelRequest(ctx, tunnel, options, http.MethodPost, url, convertedPort, nil, hostOrManageAccessTokenScope, true)
	if err != nil {
		return nil, fmt.Errorf("error sending create tunnel port request: %w", err)
	}

	// Read response into a tunnel port
	err = json.Unmarshal(response, &tp)
	if err != nil {
		return nil, fmt.Errorf("error parsing response json to tunnel port: %w", err)
	}

	// Updated local tunnel ports
	var newPorts []TunnelPort
	for _, p := range tunnel.Ports {
		if p.PortNumber != tp.PortNumber {
			newPorts = append(newPorts, p)
		}
	}
	newPorts = append(newPorts, *tp)
	tunnel.Ports = newPorts

	return tp, nil
}

// Updates a tunnel port.
// Returns the updated port or an error if the update fails.
func (m *Manager) UpdateTunnelPort(
	ctx context.Context, tunnel *Tunnel, port *TunnelPort, updateFields []string, options *TunnelRequestOptions,
) (tp *TunnelPort, err error) {
	if port.ClusterID != "" && tunnel.ClusterID != "" && port.ClusterID != tunnel.ClusterID {
		return nil, fmt.Errorf("cluster ids do not match")
	}
	path := fmt.Sprintf("%s/%d", portsApiSubPath, port.PortNumber)
	url, err := m.buildTunnelSpecificUri(tunnel, path, options, "")
	if err != nil {
		return nil, fmt.Errorf("error creating tunnel url: %w", err)
	}

	convertedPort, err := port.requestObject(tunnel)
	if err != nil {
		return nil, fmt.Errorf("error converting port for request: %w", err)
	}

	response, err := m.sendTunnelRequest(ctx, tunnel, options, http.MethodPut, url, convertedPort, updateFields, hostOrManageAccessTokenScope, true)
	if err != nil {
		return nil, fmt.Errorf("error sending update tunnel port request: %w", err)
	}

	// Read response into a tunnel port
	err = json.Unmarshal(response, &tp)
	if err != nil {
		return nil, fmt.Errorf("error parsing response json to tunnel port: %w", err)
	}

	// Updated local tunnel ports
	var newPorts []TunnelPort
	for _, p := range tunnel.Ports {
		if p.PortNumber != tp.PortNumber {
			newPorts = append(newPorts, p)
		}
	}
	newPorts = append(newPorts, *tp)
	tunnel.Ports = newPorts

	return tp, nil
}

// Deletes a tunnel port.
// Returns error if the delete fails.
func (m *Manager) DeleteTunnelPort(
	ctx context.Context, tunnel *Tunnel, port uint16, options *TunnelRequestOptions,
) error {

	path := fmt.Sprintf("%s/%d", portsApiSubPath, port)
	url, err := m.buildTunnelSpecificUri(tunnel, path, options, "")
	if err != nil {
		return fmt.Errorf("error creating tunnel url: %w", err)
	}

	_, err = m.sendTunnelRequest(ctx, tunnel, options, http.MethodDelete, url, nil, nil, hostOrManageAccessTokenScope, true)
	if err != nil {
		return fmt.Errorf("error sending get tunnel request: %w", err)
	}

	// Updated local tunnel ports
	var newPorts []TunnelPort
	for _, p := range tunnel.Ports {
		if p.PortNumber != port {
			newPorts = append(newPorts, p)
		}
	}
	tunnel.Ports = newPorts
	return nil
}

func (m *Manager) sendTunnelRequest(
	ctx context.Context,
	tunnel *Tunnel,
	tunnelRequestOptions *TunnelRequestOptions,
	method string,
	uri *url.URL,
	requestObject interface{},
	partialFields []string,
	accessTokenScopes []TunnelAccessScope,
	allowNotFound bool,
) ([]byte, error) {
	tunnelJson, err := partialMarshal(requestObject, partialFields)
	if err != nil {
		return nil, fmt.Errorf("error converting tunnel to json: %w", err)
	}
	request, err := http.NewRequest(method, uri.String(), bytes.NewBuffer(tunnelJson))
	if err != nil {
		return nil, fmt.Errorf("error creating tunnel request request: %w", err)
	}

	//Add authorization header
	if token := m.getAccessToken(tunnel, tunnelRequestOptions, accessTokenScopes); token != "" {
		request.Header.Add("Authorization", token)
	}
	userAgentString := ""
	for _, userAgent := range m.userAgents {
		if len(userAgent.Version) == 0 {
			userAgent.Version = "unknown"
		}
		if len(userAgent.Name) == 0 {
			return nil, fmt.Errorf("userAgent name cannot be empty")
		}
		userAgentString = fmt.Sprintf("%s%s/%s ", userAgentString, userAgent.Name, userAgent.Version)
	}
	userAgentString = strings.TrimSpace(userAgentString)
	request.Header.Add("User-Agent", fmt.Sprintf("%s %s", goUserAgent, userAgentString))
	request.Header.Add("Content-Type", "application/json;charset=UTF-8")

	// Add additional headers
	for header, headerValue := range m.additionalHeaders {
		request.Header.Add(header, headerValue)
	}
	for header, headerValue := range tunnelRequestOptions.AdditionalHeaders {
		request.Header.Add(header, headerValue)
	}

	result, err := m.httpClient.Do(request)
	if err != nil {
		return nil, fmt.Errorf("error sending request: %w", err)
	}

	defer result.Body.Close()

	// Handle non 200s responses
	if result.StatusCode > 300 {
		errorMessage, err := m.readProblemDetails(result)
		if err == nil && errorMessage != nil {
			return nil, fmt.Errorf("unsuccessful request, response: %d %s\n\t%s",
				result.StatusCode, http.StatusText(result.StatusCode), *errorMessage)
		} else {
			return nil, fmt.Errorf("unsuccessful request, response: %d: %s",
				result.StatusCode, http.StatusText(result.StatusCode))
		}
	}

	return io.ReadAll(result.Body)
}

func (m *Manager) readProblemDetails(response *http.Response) (*string, error) {
	errorBody, err := io.ReadAll(response.Body)
	if err != nil {
		return nil, fmt.Errorf("failed to read response body")
	}

	var problemDetails *ProblemDetails
	err = json.Unmarshal(errorBody, &problemDetails)
	if err != nil {
		return nil, fmt.Errorf("failed to unmarshal ProblemDetails")
	}

	if problemDetails.Title == "" && problemDetails.Detail == "" {
		return nil, fmt.Errorf("empty ProblemDetails")
	}

	var errorMessage string
	if problemDetails.Title != "" {
		errorMessage += problemDetails.Title
	}
	if problemDetails.Detail != "" {
		if len(errorMessage) > 0 {
			errorMessage += " "
		}
		errorMessage += problemDetails.Detail
	}
	for errorKey, errorDetail := range problemDetails.Errors {
		errorMessage += "\n\t" + errorKey + ": "
		for _, errorDetailMessage := range errorDetail {
			errorMessage += " "
			errorMessage += errorDetailMessage
		}
	}

	return &errorMessage, nil
}

func (m *Manager) getAccessToken(tunnel *Tunnel, tunnelRequestOptions *TunnelRequestOptions, scopes []TunnelAccessScope) (token string) {
	if tunnelRequestOptions.AccessToken != "" {
		token = fmt.Sprintf("%s %s", tunnelAuthenticationScheme, tunnelRequestOptions.AccessToken)
	}
	if token == "" {
		token = m.tokenProvider()
	}
	if token == "" && tunnel != nil {
		for _, scope := range scopes {
			if tunnelToken, ok := tunnel.AccessTokens[scope]; ok {
				token = fmt.Sprintf("%s %s", tunnelAuthenticationScheme, tunnelToken)
			}
		}
	}
	return token
}

func (m *Manager) buildUri(clusterId string, path string, options *TunnelRequestOptions, query string) *url.URL {
	baseAddress := m.uri
	if clusterId != "" {
		if !strings.HasPrefix(baseAddress.Host, "localhost") && !strings.HasPrefix(baseAddress.Host, clusterId) {
			// A specific cluster ID was specified (while not running on localhost).
			// Prepend the cluster ID to the hostname, and optionally strip a global prefix.
			baseAddress.Host = fmt.Sprintf("%s.%s", clusterId, baseAddress.Host)
			baseAddress.Host = strings.Replace(baseAddress.Host, "global.", "", 1)
		}
	}
	if options != nil {
		optionsQuery := options.queryString()
		if optionsQuery != "" {
			if query != "" {
				query = fmt.Sprintf("%s&%s", query, optionsQuery)
			} else {
				query = optionsQuery
			}
		}
	}
	baseAddress.Path = path
	baseAddress.RawQuery = query
	return baseAddress
}

func (m *Manager) buildTunnelSpecificUri(tunnel *Tunnel, path string, options *TunnelRequestOptions, query string) (*url.URL, error) {
	var tunnelPath string
	if tunnel == nil {
		return nil, fmt.Errorf("tunnel cannot be nil to make uri")
	}
	switch {
	case tunnel.ClusterID != "" && tunnel.TunnelID != "":
		tunnelPath = fmt.Sprintf("%s/%s", tunnelsApiPath, tunnel.TunnelID)
	case tunnel.Name != "":
		tunnelPath = fmt.Sprintf("%s/%s", tunnelsApiPath, tunnel.Name)
		if tunnel.Domain != "" {
			tunnelPath = fmt.Sprintf("%s/%s.%s", tunnelsApiPath, tunnel.Name, tunnel.Domain)
		}
	default:
		return nil, fmt.Errorf("tunnel must have either a name or cluster id and tunnel id")
	}
	return m.buildUri(tunnel.ClusterID, tunnelPath+path, options, query), nil
}

// The omitempty JSON tags on string fields make it impossible to intentionally supply
// empty string values when updating. As a workaround, this method marshals a given
// list of fields regardless of whether they are empty.
func partialMarshal(value interface{}, fields []string) ([]byte, error) {
	if len(fields) == 0 {
		return json.Marshal(value)
	}

	reflectValue := reflect.Indirect(reflect.ValueOf(value))
	reflectType := reflectValue.Type()

	m := map[string]interface{}{}

	for _, name := range fields {
		field, found := reflectType.FieldByName(name)
		if !found {
			return nil, fmt.Errorf("field '%s' not found in type '%s'", name, reflectType.Name())
		}

		jsonKey := strings.Split(field.Tag.Get("json"), ",")[0]
		value := reflectValue.FieldByIndex(field.Index).Interface()
		m[jsonKey] = value
	}

	return json.Marshal(m)
}
