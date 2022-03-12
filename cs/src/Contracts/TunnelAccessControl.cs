// <copyright file="TunnelAccessControl.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.VsSaaS.TunnelService.Contracts
{
    /// <summary>
    /// Data contract for access control on a <see cref="Tunnel"/> or <see cref="TunnelPort"/>.
    /// </summary>
    /// <remarks>
    /// Tunnels and tunnel ports can each optionally have an access-control property set on them.
    /// An access-control object contains a list (ACL) of entries (ACEs) that specify the
    /// access scopes granted or denied to some subjects. Tunnel ports inherit the ACL from the
    /// tunnel, though ports may include ACEs that augment or override the inherited rules.
    ///
    /// Currently there is no capability to define "roles" for tunnel access (where a role
    /// specifies a set of related access scopes), and assign roles to users. That feature
    /// may be added in the future. (It should be represented as a separate `RoleAssignments`
    /// property on this class.)
    /// </remarks>
    /// <seealso cref="TunnelAccessControlEntry" />
    [DebuggerDisplay("{ToString(),nq}")]
    public class TunnelAccessControl : IEnumerable<TunnelAccessControlEntry>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TunnelAccessControl"/> class
        /// with an empty list of access control entries.
        /// </summary>
        public TunnelAccessControl()
        {
            Entries = Array.Empty<TunnelAccessControlEntry>();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TunnelAccessControl"/> class
        /// with a specified list of access control entries.
        /// </summary>
        public TunnelAccessControl(IEnumerable<TunnelAccessControlEntry> entries)
        {
            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            Entries = entries.ToArray();
        }

        /// <summary>
        /// Gets or sets the list of access control entries.
        /// </summary>
        /// <remarks>
        /// The order of entries is significant: later entries override earlier entries that apply
        /// to the same subject. However, deny rules are always processed after allow rules,
        /// therefore an allow rule cannot override a deny rule for the same subject.
        /// </remarks>
        public TunnelAccessControlEntry[] Entries { get; set; }

        /// <inheritdoc/>
        public IEnumerator<TunnelAccessControlEntry> GetEnumerator() =>
            (Entries ?? Enumerable.Empty<TunnelAccessControlEntry>()).GetEnumerator();

        /// <inheritdoc/>
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
            GetEnumerator();

        /// <summary>
        /// Gets a compact textual representation of all the access control entries.
        /// </summary>
        public override string ToString()
        {
            return "{" + string.Join<TunnelAccessControlEntry>("; ", Entries) + "}";
        }

        /// <summary>
        /// Workaround for System.Text.Json bug with classes that implement IEnumerable.
        /// See https://github.com/dotnet/runtime/issues/1808
        /// </summary>
        public class Converter : JsonConverter<TunnelAccessControl>
        {
            /// <inheritdoc/>
            public override bool CanConvert(Type typeToConvert)
            {
                return typeToConvert == typeof(TunnelAccessControl);
            }

            /// <inheritdoc/>
#if NET5_0_OR_GREATER
            public override TunnelAccessControl? Read(
#else
            public override TunnelAccessControl Read(
#endif
                ref Utf8JsonReader reader,
                Type typeToConvert,
                JsonSerializerOptions options)
            {
                // By default, serializer handles null deserialization for reference types.
                Debug.Assert(
                    reader.TokenType != JsonTokenType.Null,
                    "JSON token to be deserialized should not be null");

                if (reader.TokenType != JsonTokenType.StartObject)
                {
                    throw new JsonException();
                }

                var entriesPropertyName =
                    options.PropertyNamingPolicy?.ConvertName(nameof(Entries)) ?? nameof(Entries);

                var value = new TunnelAccessControl();

                do
                {
                    reader.Read();

                    if (reader.TokenType == JsonTokenType.PropertyName)
                    {
                        var propertyName = reader.GetString();
                        reader.Read();

                        if (propertyName == entriesPropertyName)
                        {
                            value.Entries = JsonSerializer.Deserialize<TunnelAccessControlEntry[]>(
                                ref reader, options) !;
                        }
                        else
                        {
                            reader.Skip();
                        }
                    }
                    else if (reader.TokenType == JsonTokenType.EndObject)
                    {
                        break;
                    }
                }
                while (true);

                return value;
            }

            /// <inheritdoc/>
            public override void Write(
                Utf8JsonWriter writer,
                TunnelAccessControl value,
                JsonSerializerOptions options)
            {
                // By default, serializer handles null serialization.
                Debug.Assert(value != null, "Value to be serialized should not be null.");

                writer.WriteStartObject();
                writer.WritePropertyName(
                    options.PropertyNamingPolicy?.ConvertName(nameof(Entries)) ?? nameof(Entries));
                JsonSerializer.Serialize(writer, value.Entries, options);
                writer.WriteEndObject();
            }
        }
    }
}
