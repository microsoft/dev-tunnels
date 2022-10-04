// <copyright file="JsonContent.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System;
using System.Diagnostics;
using System.IO;
using System.Net;
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
    /// The real `System.Net.Http.Json.JsonContent` was added in .NET 5.
    /// This class enables compatibility with .NET Core 3.1.
    /// </summary>
    internal sealed class JsonContent : HttpContent
    {
        private static readonly MediaTypeHeaderValue DefaultMediaTypeHeaderValue =
            new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
        private static readonly JsonSerializerOptions DefaultSerializerOptions =
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };

        private readonly JsonSerializerOptions? jsonSerializerOptions;
        public Type ObjectType { get; }
        public object? Value { get; }

        private JsonContent(
            object? inputValue,
            Type inputType,
            MediaTypeHeaderValue? mediaType,
            JsonSerializerOptions? options)
        {
            if (inputType == null)
            {
                throw new ArgumentNullException(nameof(inputType));
            }

            if (inputValue != null && !inputType.IsAssignableFrom(inputValue.GetType()))
            {
                throw new ArgumentException("Invalid input type: " + inputValue.GetType());
            }

            Value = inputValue;
            ObjectType = inputType;
            Headers.ContentType = mediaType ?? DefaultMediaTypeHeaderValue;
            jsonSerializerOptions = options ?? DefaultSerializerOptions;
        }

        public static JsonContent Create<T>(T inputValue, MediaTypeHeaderValue? mediaType = null, JsonSerializerOptions? options = null)
            => Create(inputValue, typeof(T), mediaType, options);

        public static JsonContent Create(object? inputValue, Type inputType, MediaTypeHeaderValue? mediaType = null, JsonSerializerOptions? options = null)
            => new JsonContent(inputValue, inputType, mediaType, options);

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
            => SerializeToStreamAsyncCore(stream, async: true, CancellationToken.None);

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }

        private async Task SerializeToStreamAsyncCore(Stream targetStream, bool async, CancellationToken cancellationToken)
        {
            Encoding? targetEncoding = GetEncoding(Headers.ContentType?.CharSet);

            // Wrap provided stream into a transcoding stream that buffers the data transcoded from utf-8 to the targetEncoding.
            if (targetEncoding != null && targetEncoding != Encoding.UTF8)
            {
                // Here the real JsonContent class supports transcoding.
                // But it's not necessary for the limited back-compat scenarios.
                throw new NotSupportedException("Only UTF8 encoding is supported.");
            }
            else
            {
                if (async)
                {
                    await SerializeAsyncHelper(targetStream, Value, ObjectType, jsonSerializerOptions, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    Debug.Fail("Synchronous serialization is only supported since .NET 5.0");
                }
            }

            static Task SerializeAsyncHelper(Stream utf8Json, object? value, Type inputType, JsonSerializerOptions? options, CancellationToken cancellationToken)
                => JsonSerializer.SerializeAsync(utf8Json, value, inputType, options, cancellationToken);
        }

        private static Encoding? GetEncoding(string? charset)
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
