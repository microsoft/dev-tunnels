// <copyright file="HttpContentJsonExtensions.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DevTunnels.Management
{
#if !NET5_0_OR_GREATER
    /// <summary>
    /// The real `System.Net.Http.Json.HttpContentJsonExtensions` was added in .NET 5.
    /// This class enables compatibility with .NET Core 3.1.
    /// </summary>
    internal static class HttpContentJsonExtensions
    {
        private const string JsonContentType = "application/json";

        public static Task<T?> ReadFromJsonAsync<T>(
            this HttpContent content,
            JsonSerializerOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            Encoding? sourceEncoding = GetEncoding(content.Headers.ContentType?.CharSet);

            return ReadFromJsonAsyncCore<T>(content, sourceEncoding, options, cancellationToken);
        }

        private static async Task<T?> ReadFromJsonAsyncCore<T>(
            HttpContent content,
            Encoding? sourceEncoding,
            JsonSerializerOptions? options,
            CancellationToken cancellationToken)
        {
            using (Stream contentStream = await GetContentStream(
                content, sourceEncoding, cancellationToken).ConfigureAwait(false))
            {
                return await JsonSerializer.DeserializeAsync<T>(
                    contentStream, options ?? new JsonSerializerOptions(), cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        public static Task<HttpResponseMessage> PutAsJsonAsync<T>(
            this HttpClient client,
            Uri? requestUri,
            T value,
            JsonSerializerOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            // Here the real HttpContentJsonExtensions streams the serialization, which involves more code.
            // For back-compat, this just converts the value to a string, which is simpler.
            var content = new StringContent(
                JsonSerializer.Serialize<T>(value, options), Encoding.UTF8, JsonContentType);

            return client.PutAsync(requestUri, content, cancellationToken);
        }

        public static Task<HttpResponseMessage> PostAsJsonAsync<T>(
            this HttpClient client,
            Uri? requestUri,
            T value,
            JsonSerializerOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            // Here the real HttpContentJsonExtensions streams the serialization, which involves more code.
            // For back-compat, this just converts the value to a string, which is simpler.
            var content = new StringContent(
                JsonSerializer.Serialize<T>(value, options), Encoding.UTF8, JsonContentType);

            return client.PostAsync(requestUri, content, cancellationToken);
        }

        private static async Task<Stream> GetContentStream(
            HttpContent content,
            Encoding? sourceEncoding,
            CancellationToken cancellationToken)
        {
            Stream contentStream = await content.ReadAsStreamAsync().ConfigureAwait(false);

            // Wrap content stream into a transcoding stream that buffers the data transcoded
            // from the sourceEncoding to utf-8.
            if (sourceEncoding != null && sourceEncoding != Encoding.UTF8)
            {
                // Here the real HttpContentJsonExtensions class supports transcoding.
                // But it's not necessary for the limited back-compat scenarios.
                throw new NotSupportedException("Only UTF8 encoding is supported.");
            }

            return contentStream;
        }

        internal static Encoding? GetEncoding(string? charset)
        {
            Encoding? encoding = null;

            if (charset != null)
            {
                try
                {
                    // Remove at most a single set of quotes.
                    if (charset.Length > 2 && charset[0] == '\"' && charset[charset.Length - 1] == '\"')
                    {
                        encoding = Encoding.GetEncoding(charset.Substring(1, charset.Length - 2));
                    }
                    else
                    {
                        encoding = Encoding.GetEncoding(charset);
                    }
                }
                catch (ArgumentException e)
                {
                    throw new InvalidOperationException("Invalid charset.", e);
                }
            }

            return encoding;
        }
    }
#endif
}
