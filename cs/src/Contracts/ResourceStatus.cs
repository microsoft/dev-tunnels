// <copyright file="ResourceStatus.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.DevTunnels.Contracts;

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
    /// Gets or sets an optional source of the <see cref="Limit"/>, or null if there is no limit.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LimitSource { get; set; }

    /// <summary>
    /// Implicitly converts a number value to a resource status (with unspecified limit).
    /// </summary>
    /// <param name="value"></param>
    public static implicit operator ResourceStatus(ulong value)
        => new ResourceStatus { Current = value };

    /// <summary>
    /// Implicitly converts a resource status to a number value (ignoring any limit).
    /// </summary>
    /// <param name="status"></param>
    public static implicit operator ulong(ResourceStatus status) => status.Current;

    /// <inheritdoc/>
    public override string ToString()
    {
        return Current.ToString();
    }

    /// <summary>
    /// JSON converter that allows for compatibility with a simple number value
    /// when the resource status does not include a limit.
    /// </summary>
    public class Converter : JsonConverter<ResourceStatus>
    {
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
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException($"Unexpected token: {reader.TokenType}");
            }
            else
            {
                var currentPropertyName =
                    options.PropertyNamingPolicy?.ConvertName(nameof(Current)) ?? nameof(Current);
                var limitPropertyName =
                    options.PropertyNamingPolicy?.ConvertName(nameof(Limit)) ?? nameof(Limit);
                var comparison = options.PropertyNameCaseInsensitive ?
                    StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

                ulong? current = null;
                ulong? limit = null;
                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndObject)
                    {
                        break;
                    }
                    else if (reader.TokenType != JsonTokenType.PropertyName)
                    {
                        throw new JsonException($"Unexpected token: {reader.TokenType}");
                    }

                    var propertyName = reader.GetString();
                    reader.Read();

                    if (string.Equals(propertyName, currentPropertyName, comparison))
                    {
                        current = reader.GetUInt64();
                    }
                    else if (string.Equals(propertyName, limitPropertyName, comparison))
                    {
                        limit = reader.TokenType == JsonTokenType.Null ?
                            null : reader.GetUInt64();
                    }
                    else
                    {
                        reader.Skip();
                    }
                }

                if (current == null)
                {
                    throw new JsonException($"Missing required property: {currentPropertyName}");
                }

                return new ResourceStatus { Current = current.Value, Limit = limit };
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
                writer.WriteStartObject();
                writer.WriteNumber(
                    options.PropertyNamingPolicy?.ConvertName(nameof(Current)) ?? nameof(Current),
                    value.Current);
                writer.WriteNumber(
                    options.PropertyNamingPolicy?.ConvertName(nameof(Limit)) ?? nameof(Limit),
                    value.Limit.Value);
                writer.WriteEndObject();
            }
        }
    }
}
