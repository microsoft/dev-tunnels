// <copyright file="JsonIgnoreAttribute.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System;

namespace Microsoft.DevTunnels.Contracts
{
#if !NET5_0_OR_GREATER
    /// <summary>
    /// The real `System.Text.Json.Serialization.JsonIgnoreAttribute` was added
    /// in .NET 5. This attribute does nothing but enables compatibility with .NET Core 3.1.
    /// It means JSON serialized with .NET Core 3.1 will have some extra default/null properties,
    /// which is generally not a problem.
    /// </summary>
    internal class JsonIgnoreAttribute : Attribute
    {
        public JsonIgnoreCondition Condition { get; set; }
    }

    internal enum JsonIgnoreCondition
    {
        WhenWritingDefault,
        WhenWritingNull,
    }
#endif
}
