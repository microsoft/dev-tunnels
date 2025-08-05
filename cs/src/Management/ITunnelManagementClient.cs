// <copyright file="ITunnelManagementClient.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DevTunnels.Contracts;

namespace Microsoft.DevTunnels.Management
{
    /// <summary>
    /// Interface for a client that manages tunnels and tunnel ports via the tunnel service
    /// management API.
    /// </summary>
    public interface ITunnelManagementClient : IAsyncDisposable
    {
        /// <summary>
        /// Lists tunnels that are owned by the caller.
        /// </summary>
        /// <param name="clusterId">A tunnel cluster ID, or null to list tunnels globally.</param>
        /// <param name="domain">Tunnel domain, or null for the default domain.</param>
        /// <param name="options">Request options.</param>
        /// <param name="ownedTunnelsOnly">If authenticated with a tunnel plan token, only show the tunnels the user owns.</param>
        /// <param name="cancellation">Cancellation token.</param>
        /// <returns>Array of tunnel objects.</returns>
        /// <exception cref="UnauthorizedAccessException">The client access token was missing,
        /// invalid, or unauthorized.</exception>
        /// <remarks>
        /// The list can be filtered by setting <see cref="TunnelRequestOptions.Labels"/>.
        /// Ports will not be included in the returned tunnels unless
        /// <see cref="TunnelRequestOptions.IncludePorts"/> is set to true.
        /// </remarks>
        Task<Tunnel[]> ListTunnelsAsync(
            string? clusterId = null,
            string? domain = null,
            TunnelRequestOptions? options = null,
            bool? ownedTunnelsOnly = null,
            CancellationToken cancellation = default);

        /// <summary>
        /// Search for all tunnels with matching labels.
        /// </summary>
        /// <param name="labels">The labels that will be searched for</param>
        /// <param name="requireAllLabels">If a tunnel must have all labels that are being searched for.</param>
        /// <param name="clusterId">A tunnel cluster ID, or null to list tunnels globally.</param>
        /// <param name="domain">Tunnel domain, or null for the default domain.</param>
        /// <param name="options">Request options.</param>
        /// <param name="cancellation">Cancellation token.</param>
        /// <returns>Array of tunnel objects.</returns>
        /// <exception cref="UnauthorizedAccessException">The client access token was missing,
        /// invalid, or unauthorized.</exception>
        [Obsolete("Use ListTunnelsAsync() method with TunnelRequestOptions.Labels instead.")]
        Task<Tunnel[]> SearchTunnelsAsync(
            string[] labels,
            bool requireAllLabels,
            string? clusterId = null,
            string? domain = null,
            TunnelRequestOptions? options = null,
            CancellationToken cancellation = default);

        /// <summary>
        /// Gets one tunnel by ID or name.
        /// </summary>
        /// <param name="tunnel">Tunnel object including at least either a tunnel name
        /// (globally unique, if configured) or tunnel ID and cluster ID.</param>
        /// <param name="options">Request options.</param>
        /// <param name="cancellation">Cancellation token.</param>
        /// <returns>The requested tunnel object, or null if the ID or name was not found.</returns>
        /// <exception cref="UnauthorizedAccessException">The client access token was missing,
        /// invalid, or unauthorized.</exception>
        /// <remarks>
        /// Ports will not be included in the returned tunnel unless
        /// <see cref="TunnelRequestOptions.IncludePorts"/> is set to true.
        /// </remarks>
        Task<Tunnel?> GetTunnelAsync(
            Tunnel tunnel,
            TunnelRequestOptions? options = null,
            CancellationToken cancellation = default);

        /// <summary>
        /// Creates a tunnel.
        /// </summary>
        /// <param name="tunnel">Tunnel object including all required properties.</param>
        /// <param name="options">Request options.</param>
        /// <param name="cancellation">Cancellation token.</param>
        /// <returns>The created tunnel object.</returns>
        /// <remarks>
        /// Ports may be created at the same time as creating the tunnel by supplying
        /// items in the <see cref="Tunnel.Ports" /> array.
        /// </remarks>
        /// <exception cref="UnauthorizedAccessException">The client access token was missing,
        /// invalid, or unauthorized.</exception>
        /// <exception cref="ArgumentException">A required property was missing, or a property
        /// value was invalid.</exception>
        Task<Tunnel> CreateTunnelAsync(
            Tunnel tunnel,
            TunnelRequestOptions? options = null,
            CancellationToken cancellation = default);

        /// <summary>
        /// Updates properties of a tunnel.
        /// </summary>
        /// <param name="tunnel">Tunnel object including at least either a tunnel name
        /// (globally unique, if configured) or tunnel ID and cluster ID. Any non-null
        /// properties on the object will be updated; null properties will not be modified.</param>
        /// <param name="options">Request options.</param>
        /// <param name="cancellation">Cancellation token.</param>
        /// <returns>Updated tunnel object, including both updated and unmodified
        /// properties.</returns>
        /// <exception cref="UnauthorizedAccessException">The client access token was missing,
        /// invalid, or unauthorized.</exception>
        /// <exception cref="InvalidOperationException">The tunnel ID or name was not found,
        /// or there was a conflict when updating the tunnel name. (The inner
        /// <see cref="HttpRequestException" /> status code may distinguish between these cases.)
        /// </exception>
        /// <exception cref="ArgumentException">An updated property value was invalid.</exception>
        Task<Tunnel> UpdateTunnelAsync(
            Tunnel tunnel,
            TunnelRequestOptions? options = null,
            CancellationToken cancellation = default);

        /// <summary>
        /// Updates a tunnel or creates it if it does not exist.
        /// </summary>
        /// <param name="tunnel">Tunnel object including all required properties.</param>
        /// <param name="options">Request options.</param>
        /// <param name="cancellation">Cancellation token.</param>
        /// <returns>The created tunnel object.</returns>
        /// <remarks>
        /// Ports may be created at the same time as creating the tunnel by supplying
        /// items in the <see cref="Tunnel.Ports" /> array.
        /// </remarks>
        /// <exception cref="UnauthorizedAccessException">The client access token was missing,
        /// invalid, or unauthorized.</exception>
        /// <exception cref="ArgumentException">A required property was missing, or a property
        /// value was invalid.</exception>
        Task<Tunnel> CreateOrUpdateTunnelAsync(
            Tunnel tunnel,
            TunnelRequestOptions? options = null,
            CancellationToken cancellation = default);

        /// <summary>
        /// Deletes a tunnel.
        /// </summary>
        /// <param name="tunnel">Tunnel object including at least either a tunnel name
        /// (globally unique, if configured) or tunnel ID and cluster ID.</param>
        /// <param name="options">Request options.</param>
        /// <param name="cancellation">Cancellation token.</param>
        /// <returns>True if the tunnel was deleted; false if it was not found.</returns>
        /// <exception cref="UnauthorizedAccessException">The client access token was missing,
        /// invalid, or unauthorized.</exception>
        Task<bool> DeleteTunnelAsync(
            Tunnel tunnel,
            TunnelRequestOptions? options = null,
            CancellationToken cancellation = default);

        /// <summary>
        /// Creates or updates an endpoint for the tunnel.
        /// </summary>
        /// <param name="tunnel">Tunnel object including at least either a tunnel name
        /// (globally unique, if configured) or tunnel ID and cluster ID.</param>
        /// <param name="endpoint">Endpoint object to add or update, including at least
        /// connection mode and host ID properties.</param>
        /// <param name="options">Request options.</param>
        /// <param name="cancellation">Cancellation token.</param>
        /// <returns>The created or updated tunnel endpoint, with any server-supplied
        /// properties filled.</returns>
        /// <exception cref="ArgumentException">A required property was missing, or a property
        /// value was invalid.</exception>
        /// <exception cref="UnauthorizedAccessException">The client access token was missing,
        /// invalid, or unauthorized.</exception>
        /// <exception cref="InvalidOperationException">The tunnel ID or name was not found.
        /// </exception>
        /// <remarks>
        /// A tunnel endpoint specifies how and where hosts and clients can connect to a tunnel.
        /// Hosts create one or more endpoints when they start accepting connections on a tunnel,
        /// and delete the endpoints when they stop accepting connections.
        /// </remarks>
        Task<TunnelEndpoint> UpdateTunnelEndpointAsync(
            Tunnel tunnel,
            TunnelEndpoint endpoint,
            TunnelRequestOptions? options = null,
            CancellationToken cancellation = default);

        /// <summary>
        /// Deletes a tunnel endpoint.
        /// </summary>
        /// <param name="tunnel">Tunnel object including at least either a tunnel name
        /// (globally unique, if configured) or tunnel ID and cluster ID.</param>
        /// <param name="Id">Required ID of the endpoint to be deleted.</param>
        /// <param name="options">Request options.</param>
        /// <param name="cancellation">Cancellation token.</param>
        /// <returns>True if one or more endpoints were deleted, false if none were found.</returns>
        /// <exception cref="UnauthorizedAccessException">The client access token was missing,
        /// invalid, or unauthorized.</exception>
        /// <exception cref="InvalidOperationException">The tunnel ID or name was not found.
        /// </exception>
        /// <remarks>
        /// Hosts create one or more endpoints when they start accepting connections on a tunnel,
        /// and delete the endpoints when they stop accepting connections.
        /// </remarks>
        Task<bool> DeleteTunnelEndpointsAsync(
            Tunnel tunnel,
            string Id,
            TunnelRequestOptions? options = null,
            CancellationToken cancellation = default);

        /// <summary>
        /// Lists ports on a tunnel.
        /// </summary>
        /// <param name="tunnel">Tunnel object including at least either a tunnel name
        /// (globally unique, if configured) or tunnel ID and cluster ID.</param>
        /// <param name="options">Request options.</param>
        /// <param name="cancellation">Cancellation token.</param>
        /// <returns>Array of tunnel port objects.</returns>
        /// <exception cref="UnauthorizedAccessException">The client access token was missing,
        /// invalid, or unauthorized.</exception>
        /// <exception cref="InvalidOperationException">The tunnel ID or name was not found.
        /// </exception>
        /// <remarks>
        /// The list can be filtered by setting <see cref="TunnelRequestOptions.Labels"/>.
        /// </remarks>
        Task<TunnelPort[]> ListTunnelPortsAsync(
            Tunnel tunnel,
            TunnelRequestOptions? options = null,
            CancellationToken cancellation = default);

        /// <summary>
        /// Gets one port on a tunnel by port number.
        /// </summary>
        /// <param name="tunnel">Tunnel object including at least either a tunnel name
        /// (globally unique, if configured) or tunnel ID and cluster ID.</param>
        /// <param name="portNumber">Port number.</param>
        /// <param name="options">Request options.</param>
        /// <param name="cancellation">Cancellation token.</param>
        /// <returns>The requested tunnel port object, or null if the port number
        /// was not found.</returns>
        /// <exception cref="UnauthorizedAccessException">The client access token was missing,
        /// invalid, or unauthorized.</exception>
        /// <exception cref="InvalidOperationException">The tunnel ID or name was not found.
        /// </exception>
        Task<TunnelPort?> GetTunnelPortAsync(
            Tunnel tunnel,
            ushort portNumber,
            TunnelRequestOptions? options = null,
            CancellationToken cancellation = default);

        /// <summary>
        /// Creates a tunnel port.
        /// </summary>
        /// <param name="tunnel">Tunnel object including at least either a tunnel name
        /// (globally unique, if configured) or tunnel ID and cluster ID.</param>
        /// <param name="tunnelPort">Tunnel port object including all required properties.</param>
        /// <param name="options">Request options.</param>
        /// <param name="cancellation">Cancellation token.</param>
        /// <returns>The created tunnel port object.</returns>
        /// <exception cref="UnauthorizedAccessException">The client access token was missing,
        /// invalid, or unauthorized.</exception>
        /// <exception cref="InvalidOperationException">The tunnel ID or name was not found,
        /// or a port with the specified port number already exists. (The inner
        /// <see cref="HttpRequestException" /> status code may distinguish between these cases.)
        /// </exception>
        /// <exception cref="ArgumentException">A required property was missing, or a property
        /// value was invalid.</exception>
        Task<TunnelPort> CreateTunnelPortAsync(
            Tunnel tunnel,
            TunnelPort tunnelPort,
            TunnelRequestOptions? options = null,
            CancellationToken cancellation = default);

        /// <summary>
        /// Updates properties of a tunnel port.
        /// </summary>
        /// <param name="tunnel">Tunnel object including at least either a tunnel name
        /// (globally unique, if configured) or tunnel ID and cluster ID.</param>
        /// <param name="tunnelPort">Tunnel port object including at least a port number.
        /// Any additional non-null properties on the object will be updated; null properties
        /// will not be modified.</param>
        /// <param name="options">Request options.</param>
        /// <param name="cancellation">Cancellation token.</param>
        /// <returns>Updated tunnel port object, including both updated and unmodified
        /// properties.</returns>
        /// <exception cref="UnauthorizedAccessException">The client access token was missing,
        /// invalid, or unauthorized.</exception>
        /// <exception cref="InvalidOperationException">The tunnel ID or name was not found, the
        /// port was not found, or there was a conflict when updating the tunnel name. (The inner
        /// <see cref="HttpRequestException" /> status code may distinguish between these cases.)
        /// </exception>
        /// <exception cref="ArgumentException">An updated property value was invalid.</exception>
        Task<TunnelPort> UpdateTunnelPortAsync(
            Tunnel tunnel,
            TunnelPort tunnelPort,
            TunnelRequestOptions? options = null,
            CancellationToken cancellation = default);

        /// <summary>
        /// Updates a port or creates it if it does not exist.
        /// </summary>
        /// <param name="tunnel">Tunnel object including at least either a tunnel name
        /// (globally unique, if configured) or tunnel ID and cluster ID.</param>
        /// <param name="tunnelPort">Tunnel port object including all required properties.</param>
        /// <param name="options">Request options.</param>
        /// <param name="cancellation">Cancellation token.</param>
        /// <returns>The created tunnel port object.</returns>
        /// <exception cref="UnauthorizedAccessException">The client access token was missing,
        /// invalid, or unauthorized.</exception>
        /// <exception cref="InvalidOperationException">The tunnel ID or name was not found,
        /// or a port with the specified port number already exists. (The inner
        /// <see cref="HttpRequestException" /> status code may distinguish between these cases.)
        /// </exception>
        /// <exception cref="ArgumentException">A required property was missing, or a property
        /// value was invalid.</exception>
        Task<TunnelPort> CreateOrUpdateTunnelPortAsync(
            Tunnel tunnel,
            TunnelPort tunnelPort,
            TunnelRequestOptions? options = null,
            CancellationToken cancellation = default);

        /// <summary>
        /// Deletes a tunnel port.
        /// </summary>
        /// <param name="tunnel">Tunnel object including at least either a tunnel name
        /// (globally unique, if configured) or tunnel ID and cluster ID.</param>
        /// <param name="portNumber">Port number of the port to delete.</param>
        /// <param name="options">Request options.</param>
        /// <param name="cancellation">Cancellation token.</param>
        /// <returns>True if the tunnel port was deleted; false if it was not found.</returns>
        /// <exception cref="UnauthorizedAccessException">The client access token was missing,
        /// invalid, or unauthorized.</exception>
        Task<bool> DeleteTunnelPortAsync(
            Tunnel tunnel,
            ushort portNumber,
            TunnelRequestOptions? options = null,
            CancellationToken cancellation = default);

        /// <summary>
        /// Looks up and formats subject names for display.
        /// </summary>
        /// <param name="subjects">Array of <see cref="TunnelAccessSubject"/>
        /// objects with <see cref="TunnelAccessSubject.Id"/> values to be formatted. For AAD the
        /// IDs are user or group object ID GUIDs; for GitHub they are user or team ID
        /// integers.</param>
        /// <param name="options">Request options.</param>
        /// <param name="cancellation">Cancellation token.</param>
        /// <returns>Array of the same length as <paramref name="subjects"/>, where each item
        /// includes the formatted <see cref="TunnelAccessSubject.Name" />, or a null name value
        /// if the subject ID was not found.</returns>
        /// <remarks>
        /// If the caller is not authenticated via the same identity provider as a subject
        /// (or for AAD, is not authenticated in the same AAD tenant) then the subject cannot
        /// be formatted, and a null name result is returned for that item.
        /// </remarks>
        Task<TunnelAccessSubject[]> FormatSubjectsAsync(
            TunnelAccessSubject[] subjects,
            TunnelRequestOptions? options = null,
            CancellationToken cancellation = default);

        /// <summary>
        /// Resolves partial or full subject display names or emails to IDs.
        /// </summary>
        /// <param name="subjects">Array of <see cref="TunnelAccessSubject"/>
        /// objects whose <see cref="TunnelAccessSubject.Name" /> values are partial or full names
        /// to be resolved to IDs. For AAD the subjects are user or group emails or display names;
        /// for GitHub they are user or team names or display names.</param>
        /// <param name="options">Request options.</param>
        /// <param name="cancellation">Cancellation token.</param>
        /// <returns>Array of the same length as <paramref name="subjects"/>, where each item
        /// includes the resolved <see cref="TunnelAccessSubject.Id"/> and full
        /// <see cref="TunnelAccessSubject.Name"/>, or null ID value if no match was found, or an
        /// array of potential matches if more than one match was found.</returns>
        /// <remarks>
        /// If the caller is not authenticated via the same identity provider as a subject
        /// (or for AAD, is not authenticated in the same AAD tenant) then the subject cannot
        /// be resolved, and a null ID result is returned for that item.
        /// </remarks>
        Task<TunnelAccessSubject[]> ResolveSubjectsAsync(
            TunnelAccessSubject[] subjects,
            TunnelRequestOptions? options = null,
            CancellationToken cancellation = default);

        /// <summary>
        /// Lists current consumption status and limits applied to the calling user.
        /// </summary>
        /// <param name="cancellation">Cancellation token.</param>
        /// <returns>Array of <see cref="NamedRateStatus"/>.</returns>
        Task<NamedRateStatus[]> ListUserLimitsAsync(CancellationToken cancellation = default);

        /// <summary>
        /// Lists details of tunneling service clusters in all supported Azure regions.
        /// </summary>
        /// <param name="cancellation">Cancellation token.</param>
        /// <returns>Array of <see cref="ClusterDetails"/></returns>
        Task<ClusterDetails[]> ListClustersAsync(CancellationToken cancellation = default);

        /// <summary>
        /// Checks for tunnel name availability.
        /// </summary>
        /// <param name="name">Tunnel name to check.</param>
        /// <param name="cancellation">Cancellation token.</param>
        /// <returns>True if the name is available; false if it is already in use.</returns>
        Task<bool> CheckNameAvailabilityAsync(
            string name,
            CancellationToken cancellation = default);

        /// <summary>
        /// Reports a tunnel event to the tunnel service.
        /// </summary>
        /// <remarks>
        /// This method does not block; events are batched and uploaded by a background task.
        /// The tunnel service and SDK automatically record some events related to tunnel operations
        /// and connections. This method allows applications to report additional custom events.
        /// </remarks>
        void ReportEvent(
            Tunnel tunnel,
            TunnelEvent tunnelEvent,
            TunnelRequestOptions? options = null);
    }
}
