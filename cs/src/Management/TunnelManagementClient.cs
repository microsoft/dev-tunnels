// <copyright file="TunnelManagementClient.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
#if NET5_0_OR_GREATER
using System.Net.Http.Json;
#endif
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Microsoft.VsSaaS.TunnelService.Contracts;
using static Microsoft.VsSaaS.TunnelService.Contracts.TunnelContracts;

namespace Microsoft.VsSaaS.TunnelService
{
    /// <summary>
    /// Implementation of a client that manages tunnels and tunnel ports via the tunnel service
    /// management API.
    /// </summary>
    public class TunnelManagementClient : ITunnelManagementClient
    {
        private const string ApiV1Path = "/api/v1";
        private const string TunnelsApiPath = ApiV1Path + "/tunnels";
        private const string SubjectsApiPath = ApiV1Path + "/subjects";
        private const string EndpointsApiSubPath = "/endpoints";
        private const string PortsApiSubPath = "/ports";
        private const string TunnelAuthenticationScheme = "Tunnel";
        private const string RequestIdHeaderName = "VsSaaS-Request-Id";

        private static readonly string[] ManageAccessTokenScope =
            new[] { TunnelAccessScopes.Manage };
        private static readonly string[] HostAccessTokenScope =
            new[] { TunnelAccessScopes.Host };
        private static readonly string[] HostOrManageAccessTokenScopes =
            new[] { TunnelAccessScopes.Manage, TunnelAccessScopes.Host };
        private static readonly string[] ReadAccessTokenScopes = new[]
        {
            TunnelAccessScopes.Manage,
            TunnelAccessScopes.Host,
            TunnelAccessScopes.Connect,
        };

        private static readonly ProductInfoHeaderValue TunnelSdkUserAgent =
            TunnelUserAgent.GetUserAgent(typeof(TunnelManagementClient).Assembly, "Visual-Studio-Tunnel-Service-SDK")!;

        private readonly HttpClient httpClient;
        private readonly Func<Task<AuthenticationHeaderValue?>> userTokenCallback;

        /// <summary>
        /// Initializes a new instance of the <see cref="TunnelManagementClient"/> class
        /// with an optional client authentication callback.
        /// </summary>
        /// <param name="userAgent">User agent.</param>
        /// <param name="userTokenCallback">Optional async callback for retrieving a client
        /// authentication header, for AAD or GitHub user authentication. This may be null
        /// for anonymous tunnel clients, or if tunnel access tokens will be specified via
        /// <see cref="TunnelRequestOptions.AccessToken"/>.</param>
        public TunnelManagementClient(
            ProductInfoHeaderValue userAgent,
            Func<Task<AuthenticationHeaderValue?>>? userTokenCallback = null)
            : this(new[] { userAgent }, userTokenCallback, tunnelServiceUri: null, httpHandler: null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TunnelManagementClient"/> class
        /// with an optional client authentication callback.
        /// </summary>
        /// <param name="userAgents">User agent. Muiltiple user agents can be supplied in the 
        /// case that this SDK is used in a program, such as a CLI, that has users that want 
        /// to be differentiated. </param>
        /// <param name="userTokenCallback">Optional async callback for retrieving a client
        /// authentication header, for AAD or GitHub user authentication. This may be null
        /// for anonymous tunnel clients, or if tunnel access tokens will be specified via
        /// <see cref="TunnelRequestOptions.AccessToken"/>.</param>
        public TunnelManagementClient(
            ProductInfoHeaderValue[] userAgents,
            Func<Task<AuthenticationHeaderValue?>>? userTokenCallback = null)
            : this(userAgents, userTokenCallback, tunnelServiceUri: null, httpHandler: null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TunnelManagementClient"/> class
        /// with a client authentication callback, service URI, and HTTP handler.
        /// </summary>
        /// <param name="userAgent">User agent.</param>
        /// <param name="userTokenCallback">Optional async callback for retrieving a client
        /// authentication header value with access token, for AAD or GitHub user authentication.
        /// This may be null for anonymous tunnel clients, or if tunnel access tokens will be
        /// specified via <see cref="TunnelRequestOptions.AccessToken"/>.</param>
        /// <param name="tunnelServiceUri">Optional tunnel service URI (not including any path),
        /// or null to use the default global service URI.</param>
        /// <param name="httpHandler">Optional HTTP handler or handler chain that will be invoked
        /// for HTTPS requests to the tunnel service. The <see cref="SocketsHttpHandler"/> or
        /// <see cref="HttpClientHandler"/> specified (or at the end of the chain) must have
        /// automatic redirection disabled. The provided HTTP handler will not be disposed
        /// by <see cref="Dispose"/>.</param>
        public TunnelManagementClient(
            ProductInfoHeaderValue userAgent,
            Func<Task<AuthenticationHeaderValue?>>? userTokenCallback = null,
            Uri? tunnelServiceUri = null,
            HttpMessageHandler? httpHandler = null)
            : this(new[] { userAgent }, userTokenCallback, tunnelServiceUri, httpHandler)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TunnelManagementClient"/> class
        /// with a client authentication callback, service URI, and HTTP handler.
        /// </summary>
        /// <param name="userAgents">User agent. Muiltiple user agents can be supplied in the 
        /// case that this SDK is used in a program, such as a CLI, that has users that want 
        /// to be differentiated. </param>
        /// <param name="userTokenCallback">Optional async callback for retrieving a client
        /// authentication header value with access token, for AAD or GitHub user authentication.
        /// This may be null for anonymous tunnel clients, or if tunnel access tokens will be
        /// specified via <see cref="TunnelRequestOptions.AccessToken"/>.</param>
        /// <param name="tunnelServiceUri">Optional tunnel service URI (not including any path),
        /// or null to use the default global service URI.</param>
        /// <param name="httpHandler">Optional HTTP handler or handler chain that will be invoked
        /// for HTTPS requests to the tunnel service. The <see cref="SocketsHttpHandler"/> or
        /// <see cref="HttpClientHandler"/> specified (or at the end of the chain) must have
        /// automatic redirection disabled. The provided HTTP handler will not be disposed
        /// by <see cref="Dispose"/>.</param>
        public TunnelManagementClient(
            ProductInfoHeaderValue[] userAgents,
            Func<Task<AuthenticationHeaderValue?>>? userTokenCallback = null,
            Uri? tunnelServiceUri = null,
            HttpMessageHandler? httpHandler = null)
        {
            Requires.NotNullEmptyOrNullElements(userAgents, nameof(userAgents));
            UserAgents = Requires.NotNull(userAgents, nameof(userAgents));

            this.userTokenCallback = userTokenCallback ??
                (() => Task.FromResult<AuthenticationHeaderValue?>(null));

            httpHandler ??= new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
            };
            ValidateHttpHandler(httpHandler);

            tunnelServiceUri ??= new Uri(TunnelServiceProperties.Production.ServiceUri);
            if (!tunnelServiceUri.IsAbsoluteUri || tunnelServiceUri.PathAndQuery != "/")
            {
                throw new ArgumentException(
                    $"Invalid tunnel service URI: {tunnelServiceUri}", nameof(tunnelServiceUri));
            }

            // The `SocketsHttpHandler` or `HttpClientHandler` automatic redirection is disabled
            // because they do not keep the Authorization header when redirecting. This handler
            // will keep all headers when redirecting, and also supports switching the behavior
            // per-request.
            httpHandler = new FollowRedirectsHttpHandler(httpHandler);

            this.httpClient = new HttpClient(httpHandler, disposeHandler: false)
            {
                BaseAddress = tunnelServiceUri,
            };
        }

        private static void ValidateHttpHandler(HttpMessageHandler httpHandler)
        {
            while (httpHandler is DelegatingHandler delegatingHandler)
            {
                httpHandler = delegatingHandler.InnerHandler!;
            }

            if (httpHandler is SocketsHttpHandler socketsHandler)
            {
                if (socketsHandler.AllowAutoRedirect)
                {
                    throw new ArgumentException(
                        "Tunnel client HTTP handler must have automatic redirection disabled.",
                        nameof(httpHandler));
                }
            }
            else if (httpHandler is HttpClientHandler httpClientHandler)
            {
                if (httpClientHandler.AllowAutoRedirect)
                {
                    throw new ArgumentException(
                        "Tunnel client HTTP handler must have automatic redirection disabled.",
                        nameof(httpHandler));
                }
                else if (httpClientHandler.UseDefaultCredentials)
                {
                    throw new ArgumentException(
                        "Tunnel client HTTP handler must not use default credentials.",
                        nameof(httpHandler));
                }
            }
            else
            {
                throw new NotSupportedException(
                    $"Unsupported HTTP handler type: {httpHandler?.GetType().Name}. " +
                    "HTTP handler chain must consist of 0 or more DelegatingHandlers " +
                    "ending with a HttpClientHandler.");
            }
        }

        /// <summary>
        /// Gets or sets additional headers that are added to every request.
        /// </summary>
        public IEnumerable<KeyValuePair<string, string>>? AdditionalRequestHeaders { get; set; }

        private ProductInfoHeaderValue[] UserAgents { get; }

        /// <summary>
        /// Sends an HTTP request for a tunnel, with authorization header from either tunnel
        /// properties or options.
        /// </summary>
        private Task<TResult?> SendTunnelRequestAsync<TResult>(
            Tunnel? tunnel,
            TunnelRequestOptions? options,
            HttpMethod method,
            Uri uri,
            string[] accessTokenScopes,
            bool allowNotFound,
            CancellationToken cancellation)
        {
            return SendTunnelRequestAsync<object, TResult>(
                tunnel, options, method, uri, null, accessTokenScopes, allowNotFound, cancellation);
        }

        /// <summary>
        /// Sends an HTTP request for a tunnel, with authorization header from either tunnel
        /// properties or options, along with body content.
        /// </summary>
        private async Task<TResult?> SendTunnelRequestAsync<TRequest, TResult>(
            Tunnel? tunnel,
            TunnelRequestOptions? options,
            HttpMethod method,
            Uri uri,
            TRequest? requestObject,
            string[] accessTokenScopes,
            bool allowNotFound,
            CancellationToken cancellation)
            where TRequest : class
        {
            var request = new HttpRequestMessage(method, uri);

            request.Headers.Authorization = await GetAccessTokenAsync(
                tunnel, options, accessTokenScopes);

            var emptyHeadersList = Enumerable.Empty<KeyValuePair<string, string>>();
            var additionalHeaders = (AdditionalRequestHeaders ?? emptyHeadersList).Concat(
                options?.AdditionalHeaders ?? emptyHeadersList);

            foreach (var headerNameAndValue in additionalHeaders)
            {
                request.Headers.Add(headerNameAndValue.Key, headerNameAndValue.Value);
            }

            foreach (ProductInfoHeaderValue userAgent in UserAgents)
            {
                request.Headers.UserAgent.Add(userAgent);
            }
            request.Headers.UserAgent.Add(TunnelSdkUserAgent);

            if (requestObject != null)
            {
                request.Content = JsonContent.Create(requestObject, null, JsonOptions);
            }

            if (options?.FollowRedirects == false)
            {
                FollowRedirectsHttpHandler.SetFollowRedirectsEnabledForRequest(request, false);
            }

            var response = await this.httpClient.SendAsync(request, cancellation);
            var result = await ConvertResponseAsync<TResult>(
                response,
                allowNotFound,
                cancellation);
            return result;
        }

        /// <summary>
        /// Converts a tunnel service HTTP response to a result object (or exception).
        /// </summary>
        /// <typeparam name="T">Type of result expected.</typeparam>
        /// <param name="response">Response from a tunnel service request.</param>
        /// <param name="allowNotFound">True if 404 Not Found is a valid response.</param>
        /// <param name="cancellation">Cancellation token.</param>
        /// <returns>Result object of the requested type, or null if the response is Not Found
        /// and <paramref name="allowNotFound"/> is true.</returns>
        /// <exception cref="ArgumentException">The service returned a
        /// 400 Bad Request response.</exception>
        /// <exception cref="UnauthorizedAccessException">The service returned a 401 Unauthorized
        /// or 403 Forbidden response.</exception>
        private static async Task<T?> ConvertResponseAsync<T>(
            HttpResponseMessage response,
            bool allowNotFound,
            CancellationToken cancellation)
        {
            Requires.NotNull(response, nameof(response));

            string? errorMessage = null;
            Exception? innerException = null;
            if (response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.NoContent || response.Content == null)
                {
                    return typeof(T) == typeof(bool) ? (T?)(object)(bool?)true : default;
                }

                try
                {
                    T? result = await response.Content.ReadFromJsonAsync<T>(
                        JsonOptions, cancellation);
                    return result;
                }
                catch (Exception ex)
                {
                    innerException = ex;
                    errorMessage = "Tunnel service response deserialization error: " + ex.Message;
                }
            }

            if (errorMessage == null && response.Content != null)
            {
                try
                {
                    if ((int)response.StatusCode >= 400 && (int)response.StatusCode < 500)
                    {
                        // 4xx status responses may include standard ProblemDetails.
                        var problemDetails = await response.Content
                            .ReadFromJsonAsync<ProblemDetails>(JsonOptions, cancellation);
                        if (!string.IsNullOrEmpty(problemDetails?.Title) ||
                            !string.IsNullOrEmpty(problemDetails?.Detail))
                        {
                            if (allowNotFound && response.StatusCode == HttpStatusCode.NotFound &&
                                problemDetails.Detail == null)
                            {
                                return default;
                            }

                            errorMessage = "Tunnel service error: " +
                                problemDetails!.Title + " " + problemDetails.Detail;
                            if (problemDetails.Errors != null)
                            {
                                foreach (var error in problemDetails.Errors)
                                {
                                    var messages = string.Join(" ", error.Value);
                                    errorMessage += $"\n{error.Key}: {messages}";
                                }
                            }
                        }
                    }
                    else if ((int)response.StatusCode >= 500)
                    {
                        // 5xx status responses may include VS SaaS error details.
                        var errorDetails = await response.Content.ReadFromJsonAsync<ErrorDetails>(
                            JsonOptions, cancellation);
                        if (!string.IsNullOrEmpty(errorDetails?.Message))
                        {
                            errorMessage = "Tunnel service error: " + errorDetails!.Message;
                            if (!string.IsNullOrEmpty(errorDetails.StackTrace))
                            {
                                errorMessage += "\n" + errorDetails.StackTrace;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // A default error message will be filled in below.
                    innerException = ex;
                }
            }

            errorMessage ??= "Tunnel service response status code: " + response.StatusCode;

            if (response.Headers.TryGetValues(RequestIdHeaderName, out var requestId))
            {
                errorMessage += $"\nRequest ID: {requestId.First()}";
            }

            try
            {
                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException hrex)
            {
                switch (response.StatusCode)
                {
                    case HttpStatusCode.BadRequest:
                        throw new ArgumentException(errorMessage, hrex);

                    case HttpStatusCode.Unauthorized:
                    case HttpStatusCode.Forbidden:
                        var ex = new UnauthorizedAccessException(errorMessage, hrex);

                        // The HttpResponseHeaders.WwwAuthenticate property does not correctly
                        // handle multiple values! Get the values by name instead.
                        if (response.Headers.TryGetValues(
                            "WWW-Authenticate", out var authHeaderValues))
                        {
                            ex.SetAuthenticationSchemes(authHeaderValues);
                        }

                        throw ex;

                    case HttpStatusCode.NotFound:
                    case HttpStatusCode.Conflict:
                    case HttpStatusCode.TooManyRequests:
                        throw new InvalidOperationException(errorMessage, hrex);

                    case HttpStatusCode.Redirect:
                    case HttpStatusCode.RedirectKeepVerb:
                        // Add the redirect location to the exception data.
                        // Normally the HTTP client should automatically follow redirects,
                        // but this allows tests to  validate the service's redirection behavior
                        // when client auto redirection is disabled.
                        hrex.Data["Location"] = response.Headers.Location;
                        throw;

                    default: throw;
                }
            }

            throw new Exception(errorMessage, innerException);
        }

        /// <summary>
        /// Error details that may be returned from the service with 500 status responses
        /// (when in development mode).
        /// </summary>
        /// <remarks>
        /// Copied from Microsoft.VsSaaS.Common to avoid taking a dependency on that assembly.
        /// </remarks>
        private class ErrorDetails
        {
            public string? Message { get; set; }
            public string? StackTrace { get; set; }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.httpClient.Dispose();
        }

        private Uri BuildUri(
            string? clusterId,
            string path,
            TunnelRequestOptions? options,
            string? query = null)
        {
            Requires.NotNullOrEmpty(path, nameof(path));

            var baseAddress = this.httpClient.BaseAddress!;
            var builder = new UriBuilder(baseAddress);

            if (!string.IsNullOrEmpty(clusterId) &&
                baseAddress.HostNameType == UriHostNameType.Dns)
            {
                if (baseAddress.Host != "localhost" &&
                    !baseAddress.Host.StartsWith($"{clusterId}."))
                {
                    // A specific cluster ID was specified (while not running on localhost).
                    // Prepend the cluster ID to the hostname, and optionally strip a global prefix.
                    builder.Host = $"{clusterId}.{builder.Host}".Replace("global.", string.Empty);
                }
                else if (baseAddress.Scheme == "https" &&
                    clusterId.StartsWith("localhost") && builder.Port % 10 > 0 &&
                    ushort.TryParse(clusterId.Substring("localhost".Length), out var clusterNumber))
                {
                    // Local testing simulates clusters by running the service on multiple ports.
                    // Change the port number to match the cluster ID suffix.
                    if (clusterNumber > 0 && clusterNumber < 10)
                    {
                        builder.Port = builder.Port - (builder.Port % 10) + clusterNumber;
                    }
                }
            }

            if (options != null)
            {
                var optionsQuery = options.ToQueryString();
                if (!string.IsNullOrEmpty(optionsQuery))
                {
                    query = optionsQuery +
                        (!string.IsNullOrEmpty(query) ? '&' + query : string.Empty);
                }
            }

            builder.Path = path;
            builder.Query = query;
            return builder.Uri;
        }

        private Uri BuildUri(
            Tunnel tunnel,
            TunnelRequestOptions? options,
            string? path = null,
            string? query = null)
        {
            Requires.NotNull(tunnel, nameof(tunnel));

            string tunnelPath;
            if (!string.IsNullOrEmpty(tunnel.ClusterId) && !string.IsNullOrEmpty(tunnel.TunnelId))
            {
                tunnelPath = $"{TunnelsApiPath}/{tunnel.TunnelId}";
            }
            else
            {
                Requires.Argument(
                    !string.IsNullOrEmpty(tunnel.Name),
                    nameof(tunnel),
                    "Tunnel object must include either a name or tunnel ID and cluster ID.");

                if (string.IsNullOrEmpty(tunnel.Domain))
                {
                    tunnelPath = $"{TunnelsApiPath}/{tunnel.Name}";
                }
                else
                {
                    // Append the domain to the tunnel name.
                    tunnelPath = $"{TunnelsApiPath}/{tunnel.Name}.{tunnel.Domain}";
                }
            }

            return BuildUri(
                tunnel.ClusterId,
                tunnelPath + (!string.IsNullOrEmpty(path) ? path : string.Empty),
                options,
                query);
        }

        private async Task<AuthenticationHeaderValue?> GetAccessTokenAsync(
            Tunnel? tunnel,
            TunnelRequestOptions? options,
            string[] scopes)
        {
            AuthenticationHeaderValue? token = null;

            if (!string.IsNullOrEmpty(options?.AccessToken))
            {
                TunnelAccessTokenProperties.ValidateTokenExpiration(options.AccessToken);
                token = new AuthenticationHeaderValue(
                    TunnelAuthenticationScheme, options.AccessToken);
            }

            if (token == null)
            {
                token = await this.userTokenCallback();
            }

            if (token == null && tunnel?.AccessTokens != null)
            {
                foreach (var scope in scopes)
                {
                    if (tunnel.AccessTokens.TryGetValue(scope, out var accessToken) == true &&
                        !string.IsNullOrEmpty(accessToken))
                    {
                        TunnelAccessTokenProperties.ValidateTokenExpiration(accessToken);
                        token = new AuthenticationHeaderValue(
                            TunnelAuthenticationScheme, accessToken);
                        break;
                    }
                }
            }

            return token;
        }

        /// <inheritdoc />
        public async Task<Tunnel[]> ListTunnelsAsync(
            string? clusterId,
            string? domain,
            TunnelRequestOptions? options,
            CancellationToken cancellation)
        {
            var queryParams = new string?[]
            {
                string.IsNullOrEmpty(clusterId) ? "global=true" : null,
                !string.IsNullOrEmpty(domain) ? $"domain={HttpUtility.UrlEncode(domain)}" : null,
            };
            var query = string.Join("&", queryParams.Where((p) => p != null));

            var uri = BuildUri(clusterId, TunnelsApiPath, options, query);
            var result = await this.SendTunnelRequestAsync<Tunnel[]>(
                tunnel: null,
                options,
                HttpMethod.Get,
                uri,
                ReadAccessTokenScopes,
                allowNotFound: false,
                cancellation);
            return result!;
        }

        /// <inheritdoc />
        [Obsolete("Use ListTunnelsAsync() method with TunnelRequestOptions.Tags instead.")]
        public async Task<Tunnel[]> SearchTunnelsAsync(
            string[] tags,
            bool requireAllTags,
            string? clusterId,
            string? domain,
            TunnelRequestOptions? options,
            CancellationToken cancellation)
        {
            var queryParams = new string?[]
            {
                string.IsNullOrEmpty(clusterId) ? "global=true" : null,
                !string.IsNullOrEmpty(domain) ? $"domain={HttpUtility.UrlEncode(domain)}" : null,
                $"tags={string.Join(",", tags.Select(HttpUtility.UrlEncode))}",
                $"allTags={requireAllTags}",
            };
            var query = string.Join("&", queryParams.Where((p) => p != null));

            var uri = BuildUri(clusterId, TunnelsApiPath, options, query);
            var result = await this.SendTunnelRequestAsync<Tunnel[]>(
                tunnel: null,
                options,
                HttpMethod.Get,
                uri,
                ReadAccessTokenScopes,
                allowNotFound: false,
                cancellation);
            return result!;
        }

        /// <inheritdoc />
        public async Task<Tunnel?> GetTunnelAsync(
            Tunnel tunnel,
            TunnelRequestOptions? options,
            CancellationToken cancellation)
        {
            var uri = BuildUri(tunnel, options);
            var result = await this.SendTunnelRequestAsync<Tunnel>(
                tunnel,
                options,
                HttpMethod.Get,
                uri,
                ReadAccessTokenScopes,
                allowNotFound: true,
                cancellation);
            return result;
        }

        /// <inheritdoc />
        public async Task<Tunnel> CreateTunnelAsync(
            Tunnel tunnel,
            TunnelRequestOptions? options,
            CancellationToken cancellation)
        {
            Requires.NotNull(tunnel, nameof(tunnel));

            var tunnelId = tunnel.TunnelId;
            if (tunnelId != null)
            {
                throw new ArgumentException(
                    "An ID may not be specified when creating a tunnel.", nameof(tunnelId));
            }

            var uri = BuildUri(tunnel.ClusterId, TunnelsApiPath, options);
            var result = await this.SendTunnelRequestAsync<Tunnel, Tunnel>(
                tunnel,
                options,
                HttpMethod.Post,
                uri,
                ConvertTunnelForRequest(tunnel),
                ManageAccessTokenScope,
                allowNotFound: false,
                cancellation);
            return result!;
        }

        /// <inheritdoc />
        public async Task<Tunnel> UpdateTunnelAsync(
            Tunnel tunnel,
            TunnelRequestOptions? options,
            CancellationToken cancellation)
        {
            var uri = BuildUri(tunnel, options);

            var result = await this.SendTunnelRequestAsync<Tunnel, Tunnel>(
                tunnel,
                options,
                HttpMethod.Put,
                uri,
                ConvertTunnelForRequest(tunnel),
                ManageAccessTokenScope,
                allowNotFound: false,
                cancellation);

            // If no new tokens were requested in the update, preserve any existing
            // access tokens in the resulting tunnel object.
            if (options?.TokenScopes == null)
            {
                result!.AccessTokens = tunnel.AccessTokens;
            }

            return result!;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteTunnelAsync(
            Tunnel tunnel,
            TunnelRequestOptions? options,
            CancellationToken cancellation)
        {
            var uri = BuildUri(tunnel, options);
            bool? result = await this.SendTunnelRequestAsync<bool>(
                tunnel,
                options,
                HttpMethod.Delete,
                uri,
                ManageAccessTokenScope,
                allowNotFound: true,
                cancellation);
            return result ?? false;
        }

        /// <inheritdoc />
        public async Task<TunnelEndpoint> UpdateTunnelEndpointAsync(
            Tunnel tunnel,
            TunnelEndpoint endpoint,
            TunnelRequestOptions? options = null,
            CancellationToken cancellation = default)
        {
            Requires.NotNull(endpoint, nameof(endpoint));
            Requires.NotNullOrEmpty(endpoint.HostId!, nameof(TunnelEndpoint.HostId));

            var uri = BuildUri(
                tunnel,
                options,
                $"{EndpointsApiSubPath}/{endpoint.HostId}/{endpoint.ConnectionMode}");
            var result = (await this.SendTunnelRequestAsync<TunnelEndpoint, TunnelEndpoint>(
                tunnel,
                options,
                HttpMethod.Put,
                uri,
                endpoint,
                HostAccessTokenScope,
                allowNotFound: false,
                cancellation))!;

            if (tunnel.Endpoints != null)
            {
                // Also update the endpoint in the local tunnel object.
                tunnel.Endpoints = tunnel.Endpoints
                    .Where((e) => e.HostId != endpoint.HostId ||
                        e.ConnectionMode != endpoint.ConnectionMode)
                    .Append(result)
                    .ToArray();
            }

            return result;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteTunnelEndpointsAsync(
            Tunnel tunnel,
            string hostId,
            TunnelConnectionMode? connectionMode,
            TunnelRequestOptions? options = null,
            CancellationToken cancellation = default)
        {
            Requires.NotNullOrEmpty(hostId, nameof(hostId));

            var path = connectionMode == null ? $"{EndpointsApiSubPath}/{hostId}" :
                    $"{EndpointsApiSubPath}/{hostId}/{connectionMode}";
            var uri = BuildUri(tunnel, options, path);
            var result = await this.SendTunnelRequestAsync<bool>(
                tunnel,
                options,
                HttpMethod.Delete,
                uri,
                HostAccessTokenScope,
                allowNotFound: true,
                cancellation);

            if (result && tunnel.Endpoints != null)
            {
                // Also delete the endpoint in the local tunnel object.
                tunnel.Endpoints = tunnel.Endpoints
                    .Where((e) => e.HostId != hostId || e.ConnectionMode != connectionMode)
                    .ToArray();
            }

            return result;
        }

        /// <inheritdoc />
        public async Task<TunnelPort[]> ListTunnelPortsAsync(
            Tunnel tunnel,
            TunnelRequestOptions? options,
            CancellationToken cancellation)
        {
            var uri = BuildUri(tunnel, options, PortsApiSubPath);
            var result = await this.SendTunnelRequestAsync<TunnelPort[]>(
                tunnel,
                options,
                HttpMethod.Get,
                uri,
                ReadAccessTokenScopes,
                allowNotFound: false,
                cancellation);
            return result!;
        }

        /// <inheritdoc />
        public async Task<TunnelPort?> GetTunnelPortAsync(
            Tunnel tunnel,
            ushort portNumber,
            TunnelRequestOptions? options,
            CancellationToken cancellation)
        {
            var uri = BuildUri(
                tunnel,
                options,
                $"{PortsApiSubPath}/{portNumber}");
            var result = await this.SendTunnelRequestAsync<TunnelPort>(
                tunnel,
                options,
                HttpMethod.Get,
                uri,
                ReadAccessTokenScopes,
                allowNotFound: true,
                cancellation);
            return result;
        }

        /// <inheritdoc />
        public async Task<TunnelPort> CreateTunnelPortAsync(
            Tunnel tunnel,
            TunnelPort tunnelPort,
            TunnelRequestOptions? options,
            CancellationToken cancellation)
        {
            Requires.NotNull(tunnelPort, nameof(tunnelPort));

            var uri = BuildUri(tunnel, options, PortsApiSubPath);
            var result = (await this.SendTunnelRequestAsync<TunnelPort, TunnelPort>(
                tunnel,
                options,
                HttpMethod.Post,
                uri,
                ConvertTunnelPortForRequest(tunnel, tunnelPort),
                HostOrManageAccessTokenScopes,
                allowNotFound: false,
                cancellation))!;

            if (tunnel.Ports != null)
            {
                // Also add the port to the local tunnel object.
                tunnel.Ports = tunnel.Ports
                    .Where((p) => p.PortNumber != tunnelPort.PortNumber)
                    .Append(result)
                    .OrderBy((p) => p.PortNumber)
                    .ToArray();
            }

            return result;
        }

        /// <inheritdoc />
        public async Task<TunnelPort> UpdateTunnelPortAsync(
            Tunnel tunnel,
            TunnelPort tunnelPort,
            TunnelRequestOptions? options,
            CancellationToken cancellation)
        {
            Requires.NotNull(tunnelPort, nameof(tunnelPort));

            if (tunnelPort.ClusterId != null && tunnel.ClusterId != null &&
                tunnelPort.ClusterId != tunnel.ClusterId)
            {
                throw new ArgumentException(
                    "Tunnel port cluster ID is not consistent.", nameof(tunnelPort));
            }

            var portNumber = tunnelPort.PortNumber;
            var uri = BuildUri(
                tunnel,
                options,
                $"{PortsApiSubPath}/{portNumber}");
            var result = (await this.SendTunnelRequestAsync<TunnelPort, TunnelPort>(
                tunnel,
                options,
                HttpMethod.Put,
                uri,
                ConvertTunnelPortForRequest(tunnel, tunnelPort),
                HostOrManageAccessTokenScopes,
                allowNotFound: false,
                cancellation))!;

            if (tunnel.Ports != null)
            {
                // Also update the port in the local tunnel object.
                tunnel.Ports = tunnel.Ports
                    .Where((p) => p.PortNumber != tunnelPort.PortNumber)
                    .Append(result)
                    .OrderBy((p) => p.PortNumber)
                    .ToArray();
            }

            // If no new tokens were requested in the update, preserve any existing
            // access tokens in the resulting port object.
            if (options?.TokenScopes == null)
            {
                result!.AccessTokens = tunnelPort.AccessTokens;
            }

            return result;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteTunnelPortAsync(
            Tunnel tunnel,
            ushort portNumber,
            TunnelRequestOptions? options,
            CancellationToken cancellation)
        {
            var uri = BuildUri(
                tunnel,
                options,
                $"{PortsApiSubPath}/{portNumber}");
            var result = await this.SendTunnelRequestAsync<bool>(
                tunnel,
                options,
                HttpMethod.Delete,
                uri,
                HostOrManageAccessTokenScopes,
                allowNotFound: true,
                cancellation);

            if (result && tunnel.Ports != null)
            {
                // Also delete the port in the local tunnel object.
                tunnel.Ports = tunnel.Ports
                    .Where((p) => p.PortNumber != portNumber)
                    .OrderBy((p) => p.PortNumber)
                    .ToArray();
            }

            return result;
        }

        /// <summary>
        /// Removes read-only properties like tokens and status from create/update requests.
        /// </summary>
        private Tunnel ConvertTunnelForRequest(Tunnel tunnel)
        {
            return new Tunnel
            {
                Name = tunnel.Name,
                Domain = tunnel.Domain,
                Description = tunnel.Description,
                Tags = tunnel.Tags,
                Options = tunnel.Options,
                AccessControl = tunnel.AccessControl == null ? null : new TunnelAccessControl(
                    tunnel.AccessControl.Where((ace) => !ace.IsInherited)),
                Endpoints = tunnel.Endpoints,
                Ports = tunnel.Ports?
                    .Select((p) => ConvertTunnelPortForRequest(tunnel, p))
                    .ToArray(),
            };
        }

        /// <summary>
        /// Removes read-only properties like tokens and status from create/update requests.
        /// </summary>
        private TunnelPort ConvertTunnelPortForRequest(Tunnel tunnel, TunnelPort tunnelPort)
        {
            if (tunnelPort.ClusterId != null && tunnel.ClusterId != null &&
                tunnelPort.ClusterId != tunnel.ClusterId)
            {
                throw new ArgumentException(
                    "Tunnel port cluster ID does not match tunnel.", nameof(tunnelPort));
            }

            if (tunnelPort.TunnelId != null && tunnel.TunnelId != null &&
                tunnelPort.TunnelId != tunnel.TunnelId)
            {
                throw new ArgumentException(
                    "Tunnel port tunnel ID does not match tunnel.", nameof(tunnelPort));
            }

            return new TunnelPort
            {
                PortNumber = tunnelPort.PortNumber,
                Protocol = tunnelPort.Protocol,
                Options = tunnelPort.Options,
                AccessControl = tunnelPort.AccessControl == null ? null : new TunnelAccessControl(
                    tunnelPort.AccessControl.Where((ace) => !ace.IsInherited)),
                SshUser = tunnelPort.SshUser,
            };
        }

        /// <inheritdoc/>
        public async Task<TunnelAccessSubject[]> FormatSubjectsAsync(
            TunnelAccessSubject[] subjects,
            TunnelRequestOptions? options = null,
            CancellationToken cancellation = default)
        {
            Requires.NotNull(subjects, nameof(subjects));

            if (subjects.Length == 0)
            {
                return subjects;
            }

            var uri = BuildUri(clusterId: null, SubjectsApiPath + "/format", options);
            var formattedSubjects = await SendTunnelRequestAsync
                <TunnelAccessSubject[], TunnelAccessSubject[]>(
                tunnel: null,
                options,
                HttpMethod.Post,
                uri,
                subjects,
                Array.Empty<string>(),
                allowNotFound: false,
                cancellation);
            return formattedSubjects!;
        }

        /// <inheritdoc/>
        public async Task<TunnelAccessSubject[]> ResolveSubjectsAsync(
            TunnelAccessSubject[] subjects,
            TunnelRequestOptions? options = null,
            CancellationToken cancellation = default)
        {
            Requires.NotNull(subjects, nameof(subjects));

            if (subjects.Length == 0)
            {
                return subjects;
            }

            var uri = BuildUri(clusterId: null, SubjectsApiPath + "/resolve", options);
            var resolvedSubjects = await SendTunnelRequestAsync
                <TunnelAccessSubject[], TunnelAccessSubject[]>(
                tunnel: null,
                options,
                HttpMethod.Post,
                uri,
                subjects,
                Array.Empty<string>(),
                allowNotFound: false,
                cancellation);
            return resolvedSubjects!;
        }
    }
}
