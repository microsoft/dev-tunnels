// <copyright file="ResourceStatus.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.VsSaaS.TunnelService.Contracts;

/// <summary>
/// Current value and limit for a limited resource related to a tunnel or tunnel port.
/// </summary>
public class ResourceStatus
{
    /// <summary>
    /// Gets or sets the current value.
    /// </summary>
    public ulong Current { get; set; }

    /// <summary>
    /// Gets or sets the limit enforced by the service, or null if there is no limit.
    /// </summary>
    /// <remarks>
    /// Any requests that would cause the limit to be exceeded may be denied by the service.
    /// For HTTP requests, the response is generally a 403 Forbidden status, with details about
    /// the limit in the response body.
    /// </remarks>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ulong? Limit { get; set; }

    /// <summary>
    /// JSON converter that allows for compatibility with a simple number value
    /// when the resource status does not include a limit.
    /// </summary>
    public class Converter : JsonConverter<ResourceStatus>
    {
        /// <inheritdoc/>
        public override bool CanConvert(Type typeToConvert)
        {
            return typeToConvert == typeof(ResourceStatus);
        }

        /// <inheritdoc/>
#if NET5_0_OR_GREATER
        public override ResourceStatus? Read(
#else
        public override ResourceStatus Read(
#endif
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            // By default, serializer handles null deserialization for reference types.
            Debug.Assert(
                reader.TokenType != JsonTokenType.Null,
                "JSON token to be deserialized should not be null");

            if (reader.TokenType == JsonTokenType.Number)
            {
                return new ResourceStatus
                {
                    Current = reader.GetUInt64(),
                };
            }
            else
            {
                return JsonSerializer.Deserialize<ResourceStatus>(ref reader, options);
            }
        }

        /// <inheritdoc/>
        public override void Write(
            Utf8JsonWriter writer,
            ResourceStatus value,
            JsonSerializerOptions options)
        {
            // By default, serializer handles null serialization.
            Debug.Assert(value != null, "Value to be serialized should not be null.");

            if (value.Limit == null)
            {
                writer.WriteNumberValue(value.Current);
            }
            else
            {
                JsonSerializer.Serialize<ResourceStatus>(writer, value, options);
            }
        }
    }
}
