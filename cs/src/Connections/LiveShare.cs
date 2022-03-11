// <copyright file="LiveShare.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.VsSaaS.TunnelService;

/// <summary>
/// Live Share RPC contracts.
/// </summary>
/// <remarks>
/// The types in this class define just enough of the Live Share RPC contracts to support
/// hosting LiveShareRelay tunnel connections. The alternative would be to reference these
/// types via the `Microsoft.VisualStudio.LiveShare.Contracts` nuget package, but that package
/// is broken (it has unpublished dependencies).
/// <para/>
/// If the Live Share RPC contracts ever change, the code here will need to be manually
/// updated. However it is extremely unlikely that LS would make _breaking_ changes to its
/// RPC protocol (as that would break collaboration between different versions).
/// <para/>
/// All of the Live Share protocol implementation is only temporary anyway, until the
/// TunnelRelay connection mode is fully operational.
/// </remarks>
internal class LiveShare
{
    public static class WellKnownServices
    {
        public const string Configuration = "version";
        public const string Workspace = "workspace";
        public const string ServerSharing = "serverSharing";
        public const string PortForwarding = "portForwarding";
        public const string StreamManager = "streamManager";
        public const string Stream = "stream";
    }

    public interface IWorkspaceService
    {
        Task<WorkspaceSessionInfo> JoinWorkspaceAsync(
            WorkspaceJoinInfo joinInfo,
            CancellationToken cancellationToken);
    }

    public interface IConfigurationService
    {
        Task<AgentVersionInfo> ExchangeVersionsAsync(
            AgentVersionInfo agentVersion,
            ClientVersionInfo clientVersion,
            CancellationToken cancellationToken);
        Task ExchangeSettingsAsync(
            UserSettings settings,
            CancellationToken cancellationToken);
    }

    public interface IServerSharingService
    {
        Task<SharedServer[]> GetSharedServersAsync(CancellationToken cancellationToken);

        Task<SharedPipe[]> GetSharedPipesAsync(CancellationToken cancellationToken);
    }

    public interface IStreamManagerService
    {
        Task<string?> GetStreamAsync(string streamName, string condition, CancellationToken cancellationToken);
    }

    [DataContract]
    public class AgentVersionInfo
    {
        [DataMember(Name = "version")]
        public string? VersionString { get; set; }

        [IgnoreDataMember]
        public Version? Version
        {
            get => VersionUtils.Parse(VersionString);
            set => VersionString = value?.ToString();
        }

        [DataMember(Name = "protocolVersion")]
        public string? ProtocolVersionString { get; set; }

        [IgnoreDataMember]
        public Version? ProtocolVersion
        {
            get => VersionUtils.Parse(ProtocolVersionString);
            set => ProtocolVersionString = value?.ToString();
        }

        [DataMember]
        public string? PlatformName { get; set; }

        [DataMember(Name = "platformVersion")]
        public string? PlatformVersionString { get; set; }

        [IgnoreDataMember]
        public Version? PlatformVersion
        {
            get => VersionUtils.Parse(PlatformVersionString);
            set => PlatformVersionString = value?.ToString();
        }
    }

    public class ClientVersionInfo
    {
    }

    public class UserSettings
    {
    }

    public class WorkspaceSessionInfo
    {
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public string? Id { get; set; }

        [DataMember(IsRequired = true)]
        public string? Name { get; set; }

        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public IDictionary<int, WorkspaceUserProfile>? Sessions { get; set; }

        [DataMember(IsRequired = true)]
        public int SessionNumber { get; set; }

        [DataMember(IsRequired = true)]
        public string? ConversationId { get; set; }
    }

    public class WorkspaceJoinInfo
    {
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public string? Id { get; set; }
    }

    public class WorkspaceUserProfile
    {
        [DataMember(IsRequired = true)]
        public string? Id { get; set; }

        [DataMember(IsRequired = true)]
        public string? Email { get; set; }

        [DataMember(IsRequired = true)]
        public bool IsOwner { get; set; }

        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public bool IsHost { get; set; }
    }

    public class SharedServer
    {
        [DataMember(IsRequired = true)]
        public int SourcePort { get; set; }

        [DataMember(IsRequired = false)]
        public int? DestinationPort { get; set; }

        [DataMember(IsRequired = true)]
        public string SessionName { get; set; } = null!;

        [DataMember(IsRequired = true)]
        public string StreamName { get; set; } = null!;

        [DataMember(IsRequired = true)]
        public string StreamCondition { get; set; } = null!;

        [DataMember(IsRequired = true)]
        public string? BrowseUrl { get; set; }

        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public PrivacyEnum? Privacy { get; set; }

        [DataMember(IsRequired = false)]
        public bool HasTLSHandshakePassed { get; set; }
    }

    [DataContract(Name = "Privacy")]
    public enum PrivacyEnum
    {
        [EnumMember(Value = "private")]
        Private,
        [EnumMember(Value = "public")]
        Public,
        [EnumMember(Value = "org")]
        Org,
    }

    public class SharedPipe
    {
    }

    private static class VersionUtils
    {
        public static Version? Parse(string? versionString)
        {
            if (versionString != null)
            {
                versionString = versionString.Split('-')[0];

                if (Version.TryParse(versionString, out var version))
                {
                    return version;
                }
            }

            return null;
        }
    }
}
