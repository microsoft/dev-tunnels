// <copyright file="TunnelRequestOptions.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Web;
using Microsoft.DevTunnels.Contracts;

namespace Microsoft.DevTunnels.Management
{
    /// <summary>
    /// Options for tunnel service requests.
    /// </summary>
    /// <remarks>
    /// All options are disabled by default. In general, enabling options may result
    /// in slower queries (because the server has more work to do).
    /// 
    /// Certain options may only apply to certain kinds of requests.
    /// </remarks>
    public class TunnelRequestOptions
    {
        private static readonly string[] TrueOption = new[] { "true" };

        /// <summary>
        /// Gets or sets a tunnel access token for the request.
        /// </summary>
        /// <remarks>
        /// Note this should not be a _user_ access token (such as AAD or GitHub); use the
        /// callback parameter to the <see cref="TunnelManagementClient"/> constructor to
        /// supply user access tokens.
        /// <para/>
        /// When an access token is provided here, it is used instead of any user token from the
        /// callback.
        /// </remarks>
        public string? AccessToken { get; set; }

        /// <summary>
        /// Gets or sets additional headers to be included in the request.
        /// </summary>
        public IEnumerable<KeyValuePair<string, string>>? AdditionalHeaders { get; set; }

        /// <summary>
        /// Gets or sets additional query parameters to be included in the request.
        /// </summary>
        public IEnumerable<KeyValuePair<string, string>>? AdditionalQueryParameters { get; set; }

        /// <summary>
        /// Gets or sets additional http request options
        /// for <code>HttpRequestMessage.Options</code> (in net6.0) or 
        /// <code>HttpRequestMessage.Properties</code> (in netcoreapp3.1).
        /// </summary>
        public IEnumerable<KeyValuePair<string, object?>>? HttpRequestOptions { get; set; }

        /// <summary>
        /// Gets or sets a value that indicates whether HTTP redirect responses will be
        /// automatically followed.
        /// </summary>
        /// <remarks>
        /// The default is true. If false, a redirect response will cause a
        /// <see cref="HttpRequestException"/> to be thrown, with the redirect target location
        /// in the exception data.
        /// <para/>
        /// The tunnel service often redirects requests to the "home" cluster of the requested
        /// tunnel, when necessary to fulfill the request.
        /// </remarks>
        public bool FollowRedirects { get; set; } = true;

        /// <summary>
        /// Gets or sets a flag that requests tunnel ports when retrieving tunnels.
        /// </summary>
        public bool IncludePorts { get; set; }

        /// <summary>
        /// Gets or sets an optional list of tags to filter the requested tunnels or ports.
        /// </summary>
        /// <remarks>
        /// Requested tags are compared to the <see cref="Tunnel.Tags"/> or
        /// <see cref="TunnelPort.Tags"/> when calling
        /// <see cref="ITunnelManagementClient.ListTunnelsAsync"/> or
        /// <see cref="ITunnelManagementClient.ListTunnelPortsAsync"/> respectively. By default, an
        /// item is included if ANY tag matches; set <see cref="RequireAllTags" /> to match ALL
        /// tags instead.
        /// </remarks>
        public string[]? Tags { get; set; }

        /// <summary>
        /// Gets or sets a flag that indicates whether listed items must match all tags
        /// specified in <see cref="Tags"/>. If false, an item is included if any tag matches.
        /// </summary>
        public bool RequireAllTags { get; set; }

        /// <summary>
        /// Gets or sets an optional list of token scopes that are requested when retrieving
        /// a tunnel or tunnel port object.
        /// </summary>
        /// <remarks>
        /// Each item in the array must be a single scope from <see cref="TunnelAccessScopes"/>
        /// or a space-delimited combination of multiple scopes. The service issues an access
        /// token for each scope or combination and returns the token(s) in the
        /// <see cref="Tunnel.AccessTokens"/> or <see cref="TunnelPort.AccessTokens"/> dictionary.
        /// If the caller does not have permission to get a token for one or more scopes then a
        /// token is not returned but the overall request does not fail. Token properties including
        /// scopes and expiration may be checked using <see cref="TunnelAccessTokenProperties"/>.
        /// </remarks>
        public string[]? TokenScopes { get; set; }

        /// <summary>
        /// If true on a create or update request then upon a name conflict, attempt to rename the
        /// existing tunnel to null and give the name to the tunnel from the request.
        /// </summary>
        public bool ForceRename { get; set; }

        /// <summary>
        /// Converts tunnel request options to a query string for HTTP requests to the
        /// tunnel management API.
        /// </summary>
        protected internal virtual string ToQueryString()
        {
            var queryOptions = new Dictionary<string, string[]>();

            if (IncludePorts)
            {
                queryOptions["includePorts"] = TrueOption;
            }

            if (TokenScopes != null)
            {
                TunnelAccessControl.ValidateScopes(
                    TokenScopes, validScopes: null, allowMultiple: true);
                queryOptions["tokenScopes"] = TokenScopes;
            }

            if (ForceRename)
            {
                queryOptions["forceRename"] = TrueOption;
            }

            if (Tags != null && Tags.Length > 0)
            {
                queryOptions["tags"] = Tags;
                if (RequireAllTags)
                {
                    queryOptions["allTags"] = TrueOption;
                }
            }

            if (AdditionalQueryParameters != null)
            {
                foreach (var queryParam in AdditionalQueryParameters)
                {
                    queryOptions.Add(queryParam.Key, new string[] {queryParam.Value});
                }
            }

            // Note the comma separator for multi-valued options is NOT URL-encoded.
            return string.Join('&', queryOptions.Select(
                (o) => $"{o.Key}={string.Join(",", o.Value.Select(HttpUtility.UrlEncode))}"));
        }

        /// <summary>
        /// Set HTTP request options.
        /// </summary>
        /// <remarks>
        /// On net 6.0+ it sets <code>request.Options</code>.
        /// On netcoreapp 3.1 it sets <code>request.Properties</code>.
        /// </remarks>
        /// <param name="request">Http request, not null.</param>
        internal void SetRequestOptions(HttpRequestMessage request)
        {
            Requires.NotNull(request, nameof(request));
            foreach (var kvp in HttpRequestOptions ?? Enumerable.Empty<KeyValuePair<string, object?>>())
            {
#if NET6_0_OR_GREATER
                request.Options.Set(new HttpRequestOptionsKey<object?>(kvp.Key), kvp.Value);
#else
                request.Properties[kvp.Key] = kvp.Value;
#endif
            }
        }
    }
}
