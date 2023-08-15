// <copyright file="TunnelManagementClient.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
using Microsoft.DevTunnels.Contracts;
using static Microsoft.DevTunnels.Contracts.TunnelContracts;

namespace Microsoft.DevTunnels.Management
{
    /// <summary>
    /// Implementation of a client that manages tunnels and tunnel ports via the tunnel service
    /// management API.
    /// </summary>
    public class TunnelManagementClient : ITunnelManagementClient
    {
        private const string ApiV1Path = "/api/v1";
        private const string TunnelsV1ApiPath = ApiV1Path + "/tunnels";
        private const string SubjectsV1ApiPath = ApiV1Path + "/subjects";
        private const string UserLimitsV1ApiPath = ApiV1Path + "/userlimits";
        private const string TunnelsApiPath = "/tunnels";
        private const string SubjectsApiPath = "/subjects";
        private const string UserLimitsApiPath = "/userlimits";
        private const string EndpointsApiSubPath = "/endpoints";
        private const string PortsApiSubPath = "/ports";
        private const string ClustersApiPath = "/clusters";
        private const string ClustersV1ApiPath = ApiV1Path + "/clusters";
        private const string TunnelAuthenticationScheme = "Tunnel";
        private const string RequestIdHeaderName = "VsSaaS-Request-Id";
        private const string CheckAvailableSubPath = "/checkNameAvailability";

        private static readonly string[] ManageAccessTokenScope =
            new[] { TunnelAccessScopes.Manage };
        private static readonly string[] HostAccessTokenScope =
            new[] { TunnelAccessScopes.Host };
        private static readonly string[] ManagePortsAccessTokenScopes = new[]
        {
            TunnelAccessScopes.Manage,
            TunnelAccessScopes.ManagePorts,
            TunnelAccessScopes.Host,
        };
        private static readonly string[] ReadAccessTokenScopes = new[]
        {
            TunnelAccessScopes.Manage,
            TunnelAccessScopes.ManagePorts,
            TunnelAccessScopes.Host,
            TunnelAccessScopes.Connect,
        };

        /// <summary>
        /// Accepted management client api versions
        /// </summary>
        public string[] TunnelsApiVersions =
        {
            "2023-05-23-preview"
        };



        private static readonly ProductInfoHeaderValue TunnelSdkUserAgent =
            TunnelUserAgent.GetUserAgent(typeof(TunnelManagementClient).Assembly, "Dev-Tunnels-Service-CSharp-SDK")!;

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
        /// <param name="apiVersion"> Api version to use for tunnels requests, accepted
        /// values are <see cref="TunnelsApiVersions"/></param>
        public TunnelManagementClient(
            ProductInfoHeaderValue userAgent,
            Func<Task<AuthenticationHeaderValue?>>? userTokenCallback = null,
            string? apiVersion = null)
            : this(new[] { userAgent }, userTokenCallback, tunnelServiceUri: null, httpHandler: null, apiVersion)
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
        /// <param name="apiVersion"> Api version to use for tunnels requests, accepted
        /// values are <see cref="TunnelsApiVersions"/></param>
        public TunnelManagementClient(
            ProductInfoHeaderValue[] userAgents,
            Func<Task<AuthenticationHeaderValue?>>? userTokenCallback = null,
            string? apiVersion = null)
            : this(userAgents, userTokenCallback, tunnelServiceUri: null, httpHandler: null, apiVersion)
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
        /// <param name="apiVersion"> Api version to use for tunnels requests, accepted
        /// values are <see cref="TunnelsApiVersions"/></param>
        public TunnelManagementClient(
            ProductInfoHeaderValue userAgent,
            Func<Task<AuthenticationHeaderValue?>>? userTokenCallback = null,
            Uri? tunnelServiceUri = null,
            HttpMessageHandler? httpHandler = null,
            string? apiVersion = null)
            : this(new[] { userAgent }, userTokenCallback, tunnelServiceUri, httpHandler, apiVersion)
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
        /// <param name="apiVersion"> Api version to use for tunnels requests, accepted
        /// values are <see cref="TunnelsApiVersions"/></param>
        public TunnelManagementClient(
            ProductInfoHeaderValue[] userAgents,
            Func<Task<AuthenticationHeaderValue?>>? userTokenCallback = null,
            Uri? tunnelServiceUri = null,
            HttpMessageHandler? httpHandler = null,
            string? apiVersion = null)
        {
            Requires.NotNullEmptyOrNullElements(userAgents, nameof(userAgents));
            UserAgents = Requires.NotNull(userAgents, nameof(userAgents));
            if (!string.IsNullOrEmpty(apiVersion) && !TunnelsApiVersions.Contains(apiVersion))
            {
                throw new ArgumentException(
                    $"Invalid apiVersion, accpeted values are {string.Join(", ", TunnelsApiVersions)} ");
            }
            ApiVersion = apiVersion;

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

        private string? ApiVersion { get; }

        private string TunnelsPath
        {
            get { return string.IsNullOrEmpty(ApiVersion) ? TunnelsV1ApiPath : TunnelsApiPath; }
        }

        private string ClustersPath
        {
            get { return string.IsNullOrEmpty(ApiVersion) ? ClustersV1ApiPath : ClustersApiPath; }
        }

        private string SubjectsPath
        {
            get { return string.IsNullOrEmpty(ApiVersion) ? SubjectsV1ApiPath : SubjectsApiPath; }
        }

        private string UserLimitsPath
        {
            get { return string.IsNullOrEmpty(ApiVersion) ? UserLimitsV1ApiPath : UserLimitsApiPath; }
        }

        /// <summary>
        /// Sends an HTTP request to the tunnel management API, targeting a specific tunnel.
        /// </summary>
        /// <param name="method">HTTP request method.</param>
        /// <param name="tunnel">Tunnel that the request is targeting.</param>
        /// <param name="accessTokenScopes">Required list of access scopes for tokens in
        /// <paramref name="tunnel"/> <see cref="Tunnel.AccessTokens"/> that could be used to
        /// authorize the request.</param>
        /// <param name="path">Optional request sub-path relative to the tunnel.</param>
        /// <param name="query">Optional query string to append to the request.</param>
        /// <param name="options">Request options.</param>
        /// <param name="cancellation">Cancellation token.</param>
        /// <typeparam name="TResult">The expected result type.</typeparam>
        /// <returns>Result of the request.</returns>
        /// <exception cref="ArgumentException">The request parameters were invalid.</exception>
        /// <exception cref="UnauthorizedAccessException">The request was unauthorized or forbidden.
        /// The WWW-Authenticate response header may be captured in the exception data.</exception>
        /// <exception cref="InvalidOperationException">The request would have caused a conflict
        /// or exceeded a limit.</exception>
        /// <exception cref="HttpRequestException">The request failed for some other
        /// reason.</exception>
        /// <remarks>
        /// This protected method enables subclasses to support additional tunnel management APIs.
        /// Authentication will use one of the following, if available, in order of preference:
        ///   - <see cref="TunnelRequestOptions.AccessToken"/> on <paramref name="options"/>
        ///   - token provided by the user token callback
        ///   - token in <paramref name="tunnel"/> <see cref="Tunnel.AccessTokens"/> that matches
        ///     one of the scopes in <paramref name="accessTokenScopes"/>
        /// </remarks>
        protected Task<TResult?> SendTunnelRequestAsync<TResult>(
            HttpMethod method,
            Tunnel tunnel,
            string[] accessTokenScopes,
            string? path,
            string? query,
            TunnelRequestOptions? options,
            CancellationToken cancellation)
        {
            return SendTunnelRequestAsync<object, TResult>(
                method,
                tunnel,
                accessTokenScopes,
                path,
                query,
                options,
                body: null,
                cancellation);
        }

        /// <summary>
        /// Sends an HTTP request with body content to the tunnel management API, targeting a
        /// specific tunnel.
        /// </summary>
        /// <param name="method">HTTP request method.</param>
        /// <param name="tunnel">Tunnel that the request is targeting.</param>
        /// <param name="accessTokenScopes">Required list of access scopes for tokens in
        /// <paramref name="tunnel"/> <see cref="Tunnel.AccessTokens"/> that could be used to
        /// authorize the request.</param>
        /// <param name="path">Optional request sub-path relative to the tunnel.</param>
        /// <param name="query">Optional query string to append to the request.</param>
        /// <param name="options">Request options.</param>
        /// <param name="body">Request body object.</param>
        /// <param name="cancellation">Cancellation token.</param>
        /// <typeparam name="TRequest">The request body type.</typeparam>
        /// <typeparam name="TResult">The expected result type.</typeparam>
        /// <returns>Result of the request.</returns>
        /// <exception cref="ArgumentException">The request parameters were invalid.</exception>
        /// <exception cref="UnauthorizedAccessException">The request was unauthorized or forbidden.
        /// The WWW-Authenticate response header may be captured in the exception data.</exception>
        /// <exception cref="InvalidOperationException">The request would have caused a conflict
        /// or exceeded a limit.</exception>
        /// <exception cref="HttpRequestException">The request failed for some other
        /// reason.</exception>
        /// <remarks>
        /// This protected method enables subclasses to support additional tunnel management APIs.
        /// Authentication will use one of the following, if available, in order of preference:
        ///   - <see cref="TunnelRequestOptions.AccessToken"/> on <paramref name="options"/>
        ///   - token provided by the user token callback
        ///   - token in <paramref name="tunnel"/> <see cref="Tunnel.AccessTokens"/> that matches
        ///     one of the scopes in <paramref name="accessTokenScopes"/>
        /// </remarks>
        protected async Task<TResult?> SendTunnelRequestAsync<TRequest, TResult>(
            HttpMethod method,
            Tunnel tunnel,
            string[] accessTokenScopes,
            string? path,
            string? query,
            TunnelRequestOptions? options,
            TRequest? body,
            CancellationToken cancellation)
            where TRequest : class
        {
            var uri = BuildTunnelUri(tunnel, path, query, options);
            var authHeader = await GetAuthenticationHeaderAsync(tunnel, accessTokenScopes, options);
            return await SendRequestAsync<TRequest, TResult>(
                method, uri, options, authHeader, body, cancellation);
        }

        /// <summary>
        /// Sends an HTTP request to the tunnel management API.
        /// </summary>
        /// <param name="method">HTTP request method.</param>
        /// <param name="clusterId">Optional tunnel service cluster ID to direct the request to.
        /// If unspecified, the request will use the global traffic-manager to find the nearest
        /// cluster.</param>
        /// <param name="path">Required request path.</param>
        /// <param name="query">Optional query string to append to the request.</param>
        /// <param name="options">Request options.</param>
        /// <param name="cancellation">Cancellation token.</param>
        /// <typeparam name="TResult">The expected result type.</typeparam>
        /// <returns>Result of the request.</returns>
        /// <exception cref="ArgumentException">The request parameters were invalid.</exception>
        /// <exception cref="UnauthorizedAccessException">The request was unauthorized or forbidden.
        /// The WWW-Authenticate response header may be captured in the exception data.</exception>
        /// <exception cref="InvalidOperationException">The request would have caused a conflict
        /// or exceeded a limit.</exception>
        /// <exception cref="HttpRequestException">The request failed for some other
        /// reason.</exception>
        /// <remarks>
        /// This protected method enables subclasses to support additional tunnel management APIs.
        /// Authentication will use one of the following, if available, in order of preference:
        ///   - <see cref="TunnelRequestOptions.AccessToken"/> on <paramref name="options"/>
        ///   - token provided by the user token callback
        /// </remarks>
        protected Task<TResult?> SendRequestAsync<TResult>(
            HttpMethod method,
            string? clusterId,
            string path,
            string? query,
            TunnelRequestOptions? options,
            CancellationToken cancellation)
        {
            return SendRequestAsync<object, TResult>(
                method,
                clusterId,
                path, query,
                options,
                body: null,
                cancellation);
        }

        /// <summary>
        /// Sends an HTTP request with body content to the tunnel management API.
        /// </summary>
        /// <param name="method">HTTP request method.</param>
        /// <param name="clusterId">Optional tunnel service cluster ID to direct the request to.
        /// If unspecified, the request will use the global traffic-manager to find the nearest
        /// cluster.</param>
        /// <param name="path">Required request path.</param>
        /// <param name="query">Optional query string to append to the request.</param>
        /// <param name="options">Request options.</param>
        /// <param name="body">Request body object.</param>
        /// <param name="cancellation">Cancellation token.</param>
        /// <typeparam name="TRequest">The request body type.</typeparam>
        /// <typeparam name="TResult">The expected result type.</typeparam>
        /// <returns>Result of the request.</returns>
        /// <exception cref="ArgumentException">The request parameters were invalid.</exception>
        /// <exception cref="UnauthorizedAccessException">The request was unauthorized or forbidden.
        /// The WWW-Authenticate response header may be captured in the exception data.</exception>
        /// <exception cref="InvalidOperationException">The request would have caused a conflict
        /// or exceeded a limit.</exception>
        /// <exception cref="HttpRequestException">The request failed for some other
        /// reason.</exception>
        /// <remarks>
        /// This protected method enables subclasses to support additional tunnel management APIs.
        /// Authentication will use one of the following, if available, in order of preference:
        ///   - <see cref="TunnelRequestOptions.AccessToken"/> on <paramref name="options"/>
        ///   - token provided by the user token callback
        /// </remarks>
        protected async Task<TResult?> SendRequestAsync<TRequest, TResult>(
            HttpMethod method,
            string? clusterId,
            string path,
            string? query,
            TunnelRequestOptions? options,
            TRequest? body,
            CancellationToken cancellation)
            where TRequest : class
        {
            var uri = BuildUri(clusterId, path, query, options);
            var authHeader = await GetAuthenticationHeaderAsync(
                tunnel: null, accessTokenScopes: null, options);
            return await SendRequestAsync<TRequest, TResult>(
                method, uri, options, authHeader, body, cancellation);
        }

        /// <summary>
        /// Sends an HTTP request with body content to the tunnel management API, with an
        /// explicit authentication header value.
        /// </summary>
        private async Task<TResult?> SendRequestAsync<TRequest, TResult>(
            HttpMethod method,
            Uri uri,
            TunnelRequestOptions? options,
            AuthenticationHeaderValue? authHeader,
            TRequest? body,
            CancellationToken cancellation)
            where TRequest : class
        {
            if (authHeader?.Scheme == TunnelAuthenticationSchemes.TunnelPlan)
            {
                var token = TunnelPlanTokenProperties.TryParse(authHeader.Parameter ?? string.Empty);
                if (!string.IsNullOrEmpty(token?.ClusterId))
                {
                    var uriStr = uri.ToString().Replace("global.", $"{token.ClusterId}.");
                    uri = new Uri(uriStr);
                }
            }

            var request = new HttpRequestMessage(method, uri);
            request.Headers.Authorization = authHeader;

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

            if (body != null)
            {
                request.Content = JsonContent.Create(body, null, JsonOptions);
            }

            if (options?.FollowRedirects == false)
            {
                FollowRedirectsHttpHandler.SetFollowRedirectsEnabledForRequest(request, false);
            }

            options?.SetRequestOptions(request);

            var response = await this.httpClient.SendAsync(request, cancellation);
            var result = await ConvertResponseAsync<TResult>(
                method,
                response,
                cancellation);
            return result;
        }

        /// <summary>
        /// Converts a tunnel service HTTP response to a result object (or exception).
        /// </summary>
        /// <typeparam name="T">Type of result expected, or bool to just check for either success or
        /// not-found.</typeparam>
        /// <param name="method">Request method.</param>
        /// <param name="response">Response from a tunnel service request.</param>
        /// <param name="cancellation">Cancellation token.</param>
        /// <returns>Result object of the requested type, or false if the response was 404 and
        /// the result type is boolean, or null if a GET request for a non-array result object type
        /// returned 404 Not Found.</returns>
        /// <exception cref="ArgumentException">The service returned a
        /// 400 Bad Request response.</exception>
        /// <exception cref="UnauthorizedAccessException">The service returned a 401 Unauthorized
        /// or 403 Forbidden response.</exception>
        private static async Task<T?> ConvertResponseAsync<T>(
            HttpMethod method,
            HttpResponseMessage response,
            CancellationToken cancellation)
        {
            Requires.NotNull(response, nameof(response));

            // Requests that expect a boolean result just check for success or not-found result.
            // GET requests that expect a single object result return null for not found result.
            // GET requests that expect an array result should throw an error for not-found result
            // because empty array was expected instead.
            // PUT/POST/PATCH requests should also throw an error for not-found.
            bool allowNotFound = typeof(T) == typeof(bool) ||
                ((method == HttpMethod.Get || method == HttpMethod.Head) && !typeof(T).IsArray);

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
                        // but this allows tests to validate the service's redirection behavior
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
            string? query,
            TunnelRequestOptions? options)
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

        private Uri BuildTunnelUri(
            Tunnel tunnel,
            string? path,
            string? query,
            TunnelRequestOptions? options)
        {
            Requires.NotNull(tunnel, nameof(tunnel));

            string tunnelPath;
            var pathBase = TunnelsPath;
            if (!string.IsNullOrEmpty(tunnel.ClusterId) && !string.IsNullOrEmpty(tunnel.Id))
            {
                tunnelPath = $"{pathBase}/{tunnel.Id}";
            }
            else
            {
                Requires.Argument(
                    !string.IsNullOrEmpty(tunnel.Alias),
                    nameof(tunnel),
                    "Tunnel object must include either a name or tunnel ID and cluster ID.");

                if (string.IsNullOrEmpty(tunnel.Domain))
                {

                    tunnelPath = $"{pathBase}/{tunnel.Alias}";
                }
                else
                {
                    // Append the domain to the tunnel name.
                    tunnelPath = $"{pathBase}/{tunnel.Alias}.{tunnel.Domain}";
                }
            }

            return BuildUri(
                tunnel.ClusterId,
                tunnelPath + (!string.IsNullOrEmpty(path) ? path : string.Empty),
                query,
                options);
        }

        private async Task<AuthenticationHeaderValue?> GetAuthenticationHeaderAsync(
            Tunnel? tunnel,
            string[]? accessTokenScopes,
            TunnelRequestOptions? options)
        {
            AuthenticationHeaderValue? authHeader = null;

            if (!string.IsNullOrEmpty(options?.AccessToken))
            {
                TunnelAccessTokenProperties.ValidateTokenExpiration(options.AccessToken);
                authHeader = new AuthenticationHeaderValue(
                    TunnelAuthenticationScheme, options.AccessToken);
            }

            if (authHeader == null)
            {
                authHeader = await this.userTokenCallback();
            }

            if (authHeader == null && tunnel?.AccessTokens != null && accessTokenScopes != null)
            {
                foreach (var scope in accessTokenScopes)
                {
                    if (tunnel.TryGetValidAccessToken(scope, out string? accessToken))
                    {
                        authHeader = new AuthenticationHeaderValue(
                            TunnelAuthenticationScheme, accessToken);
                        break;
                    }
                }
            }

            return authHeader;
        }

        /// <inheritdoc />
        public async Task<Tunnel[]> ListTunnelsAsync(
            string? clusterId,
            string? domain,
            TunnelRequestOptions? options,
            bool? ownedTunnelsOnly,
            CancellationToken cancellation)
        {
            var queryParams = new string?[]
            {
                string.IsNullOrEmpty(clusterId) ? "global=true" : null,
                !string.IsNullOrEmpty(domain) ? $"domain={HttpUtility.UrlEncode(domain)}" : null,
                !string.IsNullOrEmpty(ApiVersion) ? GetApiQuery() : null,
                ownedTunnelsOnly == true ? "ownedTunnelsOnly=true" : null,
            };
            var query = string.Join("&", queryParams.Where((p) => p != null));
            var result = await this.SendRequestAsync<Tunnel[]>(
                HttpMethod.Get,
                clusterId,
                TunnelsPath,
                query,
                options,
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
                !string.IsNullOrEmpty(ApiVersion) ? GetApiQuery() : null,
            };
            var query = string.Join("&", queryParams.Where((p) => p != null));
            var result = await this.SendRequestAsync<Tunnel[]>(
                HttpMethod.Get,
                clusterId,
                TunnelsPath,
                query,
                options,
                cancellation);
            return result!;
        }

        /// <inheritdoc />
        public async Task<Tunnel?> GetTunnelAsync(
            Tunnel tunnel,
            TunnelRequestOptions? options,
            CancellationToken cancellation)
        {
            var result = await this.SendTunnelRequestAsync<Tunnel>(
                HttpMethod.Get,
                tunnel,
                ReadAccessTokenScopes,
                path: null,
                query: GetApiQuery(),
                options,
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

            var tunnelId = tunnel.Id;
            //if (tunnelId != null)
            //{
            //    throw new ArgumentException(
            //        "An ID may not be specified when creating a tunnel.", nameof(tunnelId));
            //}

            var result = await this.SendRequestAsync<Tunnel, Tunnel>(
                HttpMethod.Post,
                tunnel.ClusterId,
                TunnelsPath,
                query: GetApiQuery(),
                options,
                ConvertTunnelForRequest(tunnel),
                cancellation);
            return result!;
        }

        /// <inheritdoc />
        public async Task<Tunnel> UpdateTunnelAsync(
            Tunnel tunnel,
            TunnelRequestOptions? options,
            CancellationToken cancellation)
        {
            var result = await this.SendTunnelRequestAsync<Tunnel, Tunnel>(
                HttpMethod.Put,
                tunnel,
                ManageAccessTokenScope,
                path: null,
                query: GetApiQuery(),
                options,
                ConvertTunnelForRequest(tunnel),
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
            var result = await this.SendTunnelRequestAsync<bool>(
                HttpMethod.Delete,
                tunnel,
                ManageAccessTokenScope,
                path: null,
                query: GetApiQuery(),
                options,
                cancellation);
            return result;
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

            var path = $"{EndpointsApiSubPath}/{endpoint.HostId}/{endpoint.ConnectionMode}";
            var result = (await this.SendTunnelRequestAsync<TunnelEndpoint, TunnelEndpoint>(
                HttpMethod.Put,
                tunnel,
                HostAccessTokenScope,
                path,
                query: GetApiQuery(),
                options,
                endpoint,
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
            var result = await this.SendTunnelRequestAsync<bool>(
                HttpMethod.Delete,
                tunnel,
                HostAccessTokenScope,
                path,
                query: GetApiQuery(),
                options,
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
            var result = await this.SendTunnelRequestAsync<TunnelPort[]>(
                HttpMethod.Get,
                tunnel,
                ReadAccessTokenScopes,
                PortsApiSubPath,
                query: GetApiQuery(),
                options,
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
            var path = $"{PortsApiSubPath}/{portNumber}";
            var result = await this.SendTunnelRequestAsync<TunnelPort>(
                HttpMethod.Get,
                tunnel,
                ReadAccessTokenScopes,
                path,
                query: GetApiQuery(),
                options,
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

            var result = (await this.SendTunnelRequestAsync<TunnelPort, TunnelPort>(
                HttpMethod.Post,
                tunnel,
                ManagePortsAccessTokenScopes,
                PortsApiSubPath,
                query: GetApiQuery(),
                options,
                ConvertTunnelPortForRequest(tunnel, tunnelPort),
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
            var path = $"{PortsApiSubPath}/{portNumber}";
            var result = (await this.SendTunnelRequestAsync<TunnelPort, TunnelPort>(
                HttpMethod.Put,
                tunnel,
                ManagePortsAccessTokenScopes,
                path,
                query: GetApiQuery(),
                options,
                ConvertTunnelPortForRequest(tunnel, tunnelPort),
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
            var path = $"{PortsApiSubPath}/{portNumber}";
            var result = await this.SendTunnelRequestAsync<bool>(
                HttpMethod.Delete,
                tunnel,
                ManagePortsAccessTokenScopes,
                path,
                query: GetApiQuery(),
                options,
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
                Id = tunnel.Id,
                Alias = tunnel.Alias,
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

            if (tunnelPort.TunnelId != null && tunnel.Id != null &&
                tunnelPort.TunnelId != tunnel.Id)
            {
                throw new ArgumentException(
                    "Tunnel port tunnel ID does not match tunnel.", nameof(tunnelPort));
            }

            return new TunnelPort
            {
                PortNumber = tunnelPort.PortNumber,
                Protocol = tunnelPort.Protocol,
                IsDefault = tunnelPort.IsDefault,
                Description = tunnelPort.Description,
                Tags = tunnelPort.Tags,
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

            var formattedSubjects = await SendRequestAsync
                <TunnelAccessSubject[], TunnelAccessSubject[]>(
                HttpMethod.Post,
                clusterId: null,
                SubjectsPath + "/format",
                query: GetApiQuery(),
                options,
                subjects,
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

            var resolvedSubjects = await SendRequestAsync
                <TunnelAccessSubject[], TunnelAccessSubject[]>(
                HttpMethod.Post,
                clusterId: null,
                SubjectsPath + "/resolve",
                query: GetApiQuery(),
                options,
                subjects,
                cancellation);
            return resolvedSubjects!;
        }

        /// <inheritdoc/>
        public async Task<NamedRateStatus[]> ListUserLimitsAsync(CancellationToken cancellation = default)
        {
            var userLimits = await SendRequestAsync<NamedRateStatus[]>(
                HttpMethod.Get,
                clusterId: null,
                UserLimitsPath,
                query: GetApiQuery(),
                options: null,
                cancellation);
            return userLimits!;
        }

        /// <inheritdoc/>
        public async Task<ClusterDetails[]> ListClustersAsync(CancellationToken cancellation) {
            var baseAddress = this.httpClient.BaseAddress!;
            var builder = new UriBuilder(baseAddress);
            builder.Path = ClustersPath;
            builder.Query = GetApiQuery();
            var clusterDetails = await SendRequestAsync<object, ClusterDetails[]>(
                HttpMethod.Get,
                builder.Uri,
                options: null,
                authHeader: null,
                body: null,
                cancellation);
            return clusterDetails!;
        }

        /// <inheritdoc/>
        public async Task<bool> CheckNameAvailabilityAsync(
            string name,
            CancellationToken cancellation = default)
        {
            name = Uri.EscapeDataString(name);
            Requires.NotNull(name, nameof(name));
            return await this.SendRequestAsync<bool>(
                HttpMethod.Get,
                clusterId: null,
                TunnelsPath + "/" + name + CheckAvailableSubPath,
                query: null,
                options: null,
                cancellation
            );
        }

        /// <summary>
        /// Gets required query string parmeters
        /// </summary>
        /// <returns>Query string</returns>
        protected virtual string? GetApiQuery()
        {
            return string.IsNullOrEmpty(ApiVersion) ? null : $"api-version={ApiVersion}";
        }
    }
}
