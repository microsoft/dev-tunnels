// <copyright file="RelayTunnelConnector.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// </copyright>

using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DevTunnels.Ssh;

namespace Microsoft.DevTunnels.TunnelService;

/// <summary>
/// Tunnel connector that connects to Tunnel Relay service.
/// </summary>
internal sealed class RelayTunnelConnector : ITunnelConnector
{
    private const int RetryMaxDelayMs = 12_800; // After the 6th attempt the delay will reach 2^7 * 100ms = 12.8s and stop doubling
    private const int RetryInitialDelayMs = 100;

    private readonly IRelayClient relayClient;

    public RelayTunnelConnector(IRelayClient relayClient)
    {
        this.relayClient = Requires.NotNull(relayClient, nameof(relayClient));
        Requires.NotNull(relayClient.Trace, nameof(IRelayClient.Trace));
    }

    private TraceSource Trace => this.relayClient.Trace;

    /// <inheritdoc/>
    public async Task ConnectSessionAsync(
        bool isReconnect,
        CancellationToken cancellation)
    {
        int attempt = 0;
        int attemptDelayMs = RetryInitialDelayMs;
        bool isTunnelAccessTokenRefreshed = false;

        bool isDelayNeeded;
        string? errorDescription;
        SshDisconnectReason disconnectReason;
        Exception? exception;

        while (true)
        {
            cancellation.ThrowIfCancellationRequested();
            attempt++;
            isDelayNeeded = true;
            Stream? stream = null;
            errorDescription = null;
            disconnectReason = SshDisconnectReason.ConnectionLost;
            exception = null;
            try
            {
                // TODO: check tunnel access token expiration and try refresh it if it's expired.
                stream = await this.relayClient.CreateSessionStreamAsync(cancellation);
                await this.relayClient.ConfigureSessionAsync(stream, isReconnect, cancellation);

                stream = null;
                disconnectReason = SshDisconnectReason.None;
                return;
            }
            catch (UnauthorizedAccessException uaex) // Tunnel access token validation failed.
            {
                await RefreshTunnelAccessTokenAsync(uaex);
            }
            catch (SshReconnectException srex)
            {
                errorDescription = srex.Message;
                disconnectReason = SshDisconnectReason.ProtocolError;
                exception = srex;
                isDelayNeeded = false;
                isReconnect = false;
            }
            catch (SshConnectionException scex) when (scex.DisconnectReason == SshDisconnectReason.ConnectionLost)
            {
                // Recoverable
                errorDescription = scex.Message;
            }
            catch (SshConnectionException scex)
            {
                // All other SSH connection exceptions are not recoverable.
                disconnectReason = scex.DisconnectReason != SshDisconnectReason.None ? scex.DisconnectReason : SshDisconnectReason.ByApplication;
                throw exception = new TunnelConnectionException($"Failed to start tunnel SSH session: {scex.Message}", scex);
            }
            catch (WebSocketException wse)
            {
                if (wse.WebSocketErrorCode == WebSocketError.UnsupportedProtocol)
                {
                    throw exception = new InvalidOperationException("Unsupported web socket sub-protocol.", wse);
                }

                if (wse.WebSocketErrorCode == WebSocketError.NotAWebSocket)
                {
                    var statusCode = TunnelConnectionException.GetHttpStatusCode(wse);
                    switch (statusCode)
                    {
                        case HttpStatusCode.Unauthorized:
                            // Unauthorized error may happen when the tunnel access token is no longer valid, e.g. expired.
                            // Try refreshing it.
                            await RefreshTunnelAccessTokenAsync(wse);
                            exception = new UnauthorizedAccessException(
                                $"Unauthorized (401). Provide a fresh tunnel access token with '{this.relayClient.TunnelAccessScope}' scope.",
                                wse);
                            break;

                        case HttpStatusCode.Forbidden:
                            throw exception = new UnauthorizedAccessException(
                                $"Forbidden (403). Provide a fresh tunnel access token with '{this.relayClient.TunnelAccessScope}' scope.", 
                                wse);

                        case HttpStatusCode.NotFound:
                            throw exception = new TunnelConnectionException($"The tunnel or port is not found (404).", wse);

                        case HttpStatusCode.TooManyRequests:
                            errorDescription = "Rate limit exceeded (429). Too many requests in a given amount of time.";
                            exception = new TunnelConnectionException(errorDescription, wse);
                            break;

                        default:
                            // For any other status code we assume the error is not recoverable.
                            throw exception = new TunnelConnectionException(wse.Message, wse);
                    }
                }

                // Other web socket errors may be recoverable
                exception ??= wse;
                errorDescription ??= wse.Message;
            }
            catch (OperationCanceledException)
            {
                disconnectReason = SshDisconnectReason.ByApplication;
                throw;
            }
            catch (Exception ex)
            {
                // These exceptions are not recoverable
                if (ex is InvalidOperationException ||
                    ex is ObjectDisposedException ||
                    ex is NullReferenceException ||
                    ex is ArgumentNullException ||
                    ex is ArgumentException)
                {
                    throw;
                }

                errorDescription = ex.Message;
                exception = ex;
            }
            finally
            {
                if (disconnectReason != SshDisconnectReason.None)
                {
                    await this.relayClient.CloseSessionAsync(disconnectReason, exception);
                }

                if (stream != null)
                {
                    await stream.DisposeAsync();
                }
            }

            if (exception != null)
            {
                var retryDelay = TimeSpan.FromMilliseconds(isDelayNeeded ? attemptDelayMs : 0);
                var retryingArgs = new RetryingTunnelConnectionEventArgs(exception, retryDelay);
                this.relayClient.OnRetrying(retryingArgs);
                if (!retryingArgs.Retry)
                {
                    throw exception;
                }
                else if ((int)retryingArgs.Delay.TotalMilliseconds > 0)
                {
                    attemptDelayMs = (int)retryingArgs.Delay.TotalMilliseconds;
                    isDelayNeeded = true;
                }
                else
                {
                    isDelayNeeded = false;
                }
            }

            var retryTiming = isDelayNeeded ? $" in {(attemptDelayMs < 1000 ? $"0.{attemptDelayMs / 100}s" : $"{attemptDelayMs / 1000}s")}" : string.Empty;
            Trace.Verbose($"Error connecting to tunnel SSH session, retrying{retryTiming}{(errorDescription != null ? $": {errorDescription}" : string.Empty)}");

            if (isDelayNeeded)
            {
                await Task.Delay(attemptDelayMs, cancellation);
                if (attemptDelayMs < RetryMaxDelayMs)
                {
                    attemptDelayMs <<= 1;
                }
            }

            async Task RefreshTunnelAccessTokenAsync(Exception exception)
            {
                var statusCode = TunnelConnectionException.GetHttpStatusCode(exception);
                var statusCodeText = statusCode != default ? $" ({(int)statusCode})" : string.Empty;
                if (!isTunnelAccessTokenRefreshed)
                {
                    try
                    {
                        isTunnelAccessTokenRefreshed = await this.relayClient.RefreshTunnelAccessTokenAsync(cancellation);
                    }
                    catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (UnauthorizedAccessException uaex)
                    {
                        // The refreshed token is not valid.
                        throw new UnauthorizedAccessException(
                            $"Not authorized{statusCode}. Refreshed tunnel access token is not valid. {uaex.Message}",
                            uaex);
                    }
                    catch (Exception ex)
                    {
                        throw new UnauthorizedAccessException(
                            $"Not authorized{statusCode}. Refreshing tunnel access token failed with error {ex.Message}",
                            ex);
                    }

                    if (isTunnelAccessTokenRefreshed)
                    {
                        isDelayNeeded = false;
                        errorDescription = "The tunnel access token was no longer valid and had just been refreshed.";
                        return;
                    }
                }

                if (exception is UnauthorizedAccessException)
                {
                    throw exception;
                }

                throw new UnauthorizedAccessException(
                    "Not authorized (401). " +
                    (isTunnelAccessTokenRefreshed ?
                        "Refreshed tunnel access token also doesn't work." :
                        $"Provide a fresh tunnel access token with '{this.relayClient.TunnelAccessScope}' scope."),
                    exception);
            }
        }
    }
}
