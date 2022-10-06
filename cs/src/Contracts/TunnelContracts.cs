// <copyright file="TunnelContracts.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.DevTunnels.Contracts
{
    /// <summary>
    /// Utilities for serializing and deserializing tunnel data contracts.
    /// </summary>
    public static class TunnelContracts
    {
        /// <summary>
        /// Gets JSON options configured for serializing and deserializing tunnel data contracts.
        /// </summary>
        public static JsonSerializerOptions JsonOptions { get; } = CreateJsonOptions();

        private static JsonSerializerOptions CreateJsonOptions()
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new JsonStringEnumConverter());
            options.Converters.Add(new TunnelEndpoint.Converter());
            options.Converters.Add(new TunnelAccessControl.Converter());
            options.Converters.Add(new ResourceStatus.Converter());
            options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            return options;
        }
    }
}
