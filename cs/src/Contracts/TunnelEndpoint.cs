// <copyright file="TunnelEndpoint.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.VsSaaS.TunnelService.Contracts;

/// <summary>
/// Base class for tunnel connection parameters.
/// </summary>
/// <remarks>
/// A tunnel endpoint specifies how and where hosts and clients can connect to a tunnel.
/// There is a subclass for each connection mode, each having different connection
/// parameters. A tunnel may have multiple endpoints for one host (or multiple hosts),
/// and clients can select their preferred endpoint(s) from those depending on network
/// environment or client capabilities.
/// </remarks>
public abstract class TunnelEndpoint
{
    /// <summary>
    /// Gets or sets the connection mode of the endpoint.
    /// </summary>
    /// <remarks>
    /// This property is required when creating or updating an endpoint.
    /// 
    /// The subclass type is also an indication of the connection mode, but this property
    /// is necessary to determine the subclass type when deserializing.
    /// </remarks>
    public TunnelConnectionMode ConnectionMode { get; set; }

    /// <summary>
    /// Gets or sets the ID of the host that is listening on this endpoint.
    /// </summary>
    /// <remarks>
    /// This property is required when creating or updating an endpoint.
    /// 
    /// If the host supports multiple connection modes, the host's ID is the same for
    /// all the endpoints it supports. However different hosts may simultaneously accept
    /// connections at different endpoints for the same tunnel, if enabled in tunnel
    /// options.
    /// </remarks>
    public string HostId { get; set; } = null!;

    /// <summary>
    /// Gets or sets an array of public keys, which can be used by clients to authenticate
    /// the host.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? HostPublicKeys { get; set; }

    /// <summary>
    /// Gets or sets a string used to format URIs where a web client can connect to
    /// ports of the tunnel. The string includes a <see cref="PortToken"/> that must be
    /// replaced with the actual port number.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PortUriFormat { get; set; }

    /// <summary>
    /// Gets or sets a string used to format ssh command where ssh client can connect to
    /// shared ssh port of the tunnel. The string includes a <see cref="PortToken"/> that must be
    /// replaced with the actual port number.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PortSshCommandFormat { get; set; }

    /// <summary>
    /// Token included in <see cref="PortUriFormat"/> and <see cref="PortSshCommandFormat"/>
    ///  that is to be replaced by a specified port number.
    /// </summary>
    public const string PortToken = "{port}";

    /// <summary>
    /// Gets a URI where a web client can connect to a tunnel port. 
    /// </summary>
    /// <param name="endpoint">A tunnel endpoint containing a port URI format.</param>
    /// <param name="portNumber">The port number to connect to; the port is assumed to be
    /// separately shared by a tunnel host.</param>
    /// <returns>URI for the requested port, or null if the endpoint does not support
    /// web client connections.</returns>
    /// <remarks>
    /// Requests to the URI may result in HTTP 307 redirections, so the client may need to
    /// follow the redirection in order to connect to the port.
    /// <para />
    /// If the port is not currently shared via the tunnel, or if a host is not currently
    /// connected to the tunnel, then requests to the port URI may result in a 502 Bad Gateway
    /// response.
    /// </remarks>
    public static Uri? GetPortUri(TunnelEndpoint endpoint, int? portNumber)
    {
        if (portNumber == null || string.IsNullOrEmpty(endpoint.PortUriFormat))
        {
            return null;
        }

        return new Uri(endpoint.PortUriFormat.Replace(
            PortToken, portNumber.Value.ToString(CultureInfo.InvariantCulture)));
    }

    /// <summary>
    /// Gets a ssh command which can be used to connect to a tunnel ssh port. 
    /// </summary>
    /// <param name="endpoint">A tunnel endpoint containing a port ssh URI format.</param>
    /// <param name="portNumber">The port number to connect to; the port is assumed to be
    /// separately shared by a tunnel host.</param>
    /// <returns>ssh command for the requested ssh port, or null if the endpoint does not support
    /// ssh client connections.</returns>
    /// <remarks>
    /// SSH client on Windows/Linux/MacOS  are supported.
    /// <para />
    /// If the port is not currently shared via the tunnel, or if a host is not currently
    /// connected to the tunnel, then ssh connection might fail.
    /// </remarks>
    public static string? GetPortSshCommand(TunnelEndpoint endpoint, int? portNumber)
    {
        if (portNumber == null || string.IsNullOrEmpty(endpoint.PortSshCommandFormat))
        {
            return null;
        }

        return endpoint.PortSshCommandFormat.Replace(
            PortToken, portNumber.Value.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Enables instantiation of a <see cref="TunnelEndpoint"/> subclass when deserializing.
    /// </summary>
    public class Converter : JsonConverter<TunnelEndpoint>
    {
        /// <inheritdoc/>
        public override bool CanConvert(Type typeToConvert)
        {
            // The custom converter is only needed when deserializing to the base class.
            // If the derived class is known when deserializing, then there's no need for the
            // custom converter. And there's never any need for the converter when serializing.
            return typeToConvert == typeof(TunnelEndpoint);
        }

        /// <inheritdoc/>
#if NET5_0_OR_GREATER
        public override TunnelEndpoint? Read(
#else
        public override TunnelEndpoint Read(
#endif
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            Utf8JsonReader readerClone = reader;

            if (readerClone.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException("Expected JSON object start.");
            }

            while (readerClone.Read() && !(
                readerClone.TokenType == JsonTokenType.PropertyName &&
                string.Equals(
                    readerClone.GetString(),
                    nameof(ConnectionMode),
                    StringComparison.OrdinalIgnoreCase)))
            {
            }

            if (readerClone.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException(
                    "Expected JSON connectionMode property.");
            }

            readerClone.Read();
            if (readerClone.TokenType != JsonTokenType.String)
            {
                throw new JsonException("Expected JSON string value.");
            }

            var modeString = readerClone.GetString();
            if (!Enum.TryParse<TunnelConnectionMode>(modeString, out var mode))
            {
                throw new JsonException($"Invalid connection mode value: {modeString}");
            }

            TunnelEndpoint? tunnelEndpoint = mode switch
            {
                TunnelConnectionMode.LocalNetwork =>
                    JsonSerializer.Deserialize<LocalNetworkTunnelEndpoint>(ref reader, options),
                TunnelConnectionMode.TunnelRelay =>
                    JsonSerializer.Deserialize<TunnelRelayTunnelEndpoint>(ref reader, options),
                _ => throw new JsonException($"Unsupported connection mode: {mode}")
            };

            return tunnelEndpoint;
        }

        /// <inheritdoc/>
        public override void Write(
            Utf8JsonWriter writer,
            TunnelEndpoint value,
            JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, value, value.GetType(), options);
        }
    }
}
