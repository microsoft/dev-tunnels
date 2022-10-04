// <copyright file="FollowRedirectsHttpHandler.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DevTunnels.Management;

/// <summary>
/// HTTP 
/// </summary>
internal class FollowRedirectsHttpHandler : DelegatingHandler
{
    private const string FollowRedirectsRequestPropertyName = "FollowRedirects";

    public FollowRedirectsHttpHandler(HttpMessageHandler innerHandler)
        : base(innerHandler)
    {
    }

    public int MaxRedirects { get; set; } = 3;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellation)
    {
        var response = await base.SendAsync(request, cancellation);

        for (int redirectCount = 0; redirectCount < MaxRedirects &&
            (response.StatusCode == HttpStatusCode.Redirect ||
            response.StatusCode == HttpStatusCode.TemporaryRedirect ||
            response.StatusCode == HttpStatusCode.PermanentRedirect) &&
            response.Headers.Location != null &&
            IsFollowRedirectsEnabledForRequest(request); redirectCount++)
        {
            var redirectedRequest = new HttpRequestMessage
            {
                Method = request.Method,
                RequestUri = response.Headers.Location,
                Content = request.Content,
            };
            foreach (var header in request.Headers)
            {
                redirectedRequest.Headers.Add(header.Key, header.Value);
            }

            response = await base.SendAsync(redirectedRequest, cancellation);
        }

        return response;
    }

    public static bool IsFollowRedirectsEnabledForRequest(HttpRequestMessage request)
    {
        IDictionary<string, object?> requestOptions;
#if NET6_0_OR_GREATER
        requestOptions = request.Options;
#else
        requestOptions = request.Properties;
#endif

        if (requestOptions.TryGetValue(FollowRedirectsRequestPropertyName, out var value) &&
            value is bool)
        {
            return (bool)value;
        }

        // Redirects are enabled by default for requests unless specifically set to false.
        return true;
    }

    public static void SetFollowRedirectsEnabledForRequest(HttpRequestMessage request, bool value)
    {
        IDictionary<string, object?> requestOptions;
#if NET6_0_OR_GREATER
        requestOptions = request.Options;
#else
        requestOptions = request.Properties;
#endif

        requestOptions[FollowRedirectsRequestPropertyName] = value;
    }
}
