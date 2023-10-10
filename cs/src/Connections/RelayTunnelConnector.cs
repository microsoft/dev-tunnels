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
using Microsoft.DevTunnels.Management;
using Microsoft.DevTunnels.Ssh;

namespace Microsoft.DevTunnels.Connections;

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
        TunnelConnectionOptions? options,
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

        relayClient.StartConnecting();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(
            this.relayClient.DisposeToken,
            cancellation);

        while (true)
        {
            attempt++;
            isDelayNeeded = true;
            Stream? stream = null;
            errorDescription = null;
            disconnectReason = SshDisconnectReason.ConnectionLost;
            exception = null;
            var isFinalAttempt = false;
            try
            {
                try
                {
                    cts.Token.ThrowIfCancellationRequested();

                    // TODO: check tunnel access token expiration and try refresh it if it's expired.
                    stream = await this.relayClient.CreateSessionStreamAsync(cts.Token);
                    await this.relayClient.ConfigureSessionAsync(stream, isReconnect, cts.Token);

                    stream = null;
                    disconnectReason = SshDisconnectReason.None;
                    isFinalAttempt = true;
                    return;
                }
                catch (UnauthorizedAccessException uaex) // Tunnel access token validation failed.
                {
                    if (!IsRetryAllowed(uaex, SshDisconnectReason.AuthCancelledByUser, delayNeeded: false))
                    {
                        throw;
                    }

                    await RefreshTunnelAccessTokenAsync(uaex);
                }
                catch (SshReconnectException srex)
                {
                    if (!IsRetryAllowed(srex, SshDisconnectReason.ProtocolError, delayNeeded: false))
                    {
                        throw;
                    }

                    isReconnect = false;
                }
                catch (SshConnectionException scex)
                when (scex.DisconnectReason == SshDisconnectReason.ConnectionLost)
                {
                    // Recoverable
                    if (!IsRetryAllowed(scex, scex.DisconnectReason))
                    {
                        throw;
                    }
                }
                catch (SshConnectionException scex)
                {
                    // All other SSH connection exceptions are not recoverable.
                    disconnectReason = scex.DisconnectReason != SshDisconnectReason.None ? scex.DisconnectReason : SshDisconnectReason.ByApplication;
                    throw new TunnelConnectionException($"Failed to start tunnel SSH session: {scex.Message}", scex);
                }
                catch (WebSocketException wse)
                {
                    if (wse.WebSocketErrorCode == WebSocketError.UnsupportedProtocol)
                    {
                        throw new InvalidOperationException("Unsupported web socket sub-protocol.", wse);
                    }

                    if (wse.WebSocketErrorCode == WebSocketError.NotAWebSocket)
                    {
                        var statusCode = TunnelConnectionException.GetHttpStatusCode(wse);
                        switch (statusCode)
                        {
                            case HttpStatusCode.Unauthorized:
                                exception = new UnauthorizedAccessException(
                                    $"Unauthorized (401). Provide a fresh tunnel access token with '{this.relayClient.TunnelAccessScope}' scope.",
                                    wse);

                                ThrowIfRetryNotAllowed(exception, SshDisconnectReason.AuthCancelledByUser, delayNeeded: false);

                                // Unauthorized error may happen when the tunnel access token is no longer valid, e.g. expired.
                                // Try refreshing it.
                                await RefreshTunnelAccessTokenAsync(wse);
                                break;

                            case HttpStatusCode.Forbidden:
                                throw new UnauthorizedAccessException(
                                    $"Forbidden (403). Provide a fresh tunnel access token with '{this.relayClient.TunnelAccessScope}' scope.",
                                    wse);

                            case HttpStatusCode.NotFound:
                                throw new TunnelConnectionException($"The tunnel or port is not found (404).", wse);

                            case HttpStatusCode.TooManyRequests:
                                exception = new TunnelConnectionException(
                                    "Rate limit exceeded (429). Too many requests in a given amount of time.",
                                    wse);
                                if (attemptDelayMs < RetryMaxDelayMs)
                                {
                                    attemptDelayMs <<= 1;
                                }

                                if (!IsRetryAllowed(exception) || attempt > 4)
                                {
                                    throw exception;
                                }

                                Trace.Info($"Rate limit exceeded. Delaying for {attemptDelayMs / 1000.0}s before retrying.");
                                break;

                            case HttpStatusCode.ServiceUnavailable:
                                // Normally nginx choses another healthy pod when it encounters 503.
                                // However, if there are no other pods, it returns 503 to the client.
                                // This rare case may happen when the cluster recovers from a failure
                                // and the nginx controller has started but Relay service has not yet.
                                exception = new TunnelConnectionException("Service temporarily unavailable (503).", wse);
                                ThrowIfRetryNotAllowed(exception, SshDisconnectReason.ServiceNotAvailable);
                                break;

                            default:
                                // For any other status code we assume the error is not recoverable.
                                throw new TunnelConnectionException(wse.Message, wse);
                        }
                    }

                    // Other web socket errors may be recoverable
                    if (!IsRetryAllowed(wse))
                    {
                        throw;
                    }
                }
                catch (OperationCanceledException ocex)
                {
                    // Either caller cancelled connection or relay client was disposed.
                    if (TryAdjustCancellation(ocex) is Exception adjustedCancellation)
                    {
                        disconnectReason = SshDisconnectReason.ByApplication;
                        throw adjustedCancellation;
                    }

                    throw;
                }
                catch (ObjectDisposedException)
                {
                    // Relay client might have been disposed, then it's "By application" reason.
                    // Otherwise, a protocol error.
                    disconnectReason =
                        relayClient.DisposeToken.IsCancellationRequested ?
                        SshDisconnectReason.ByApplication :
                        SshDisconnectReason.ProtocolError;

                    throw;
                }
                catch (Exception ex)
                {
                    // These exceptions are not recoverable
                    if (ex is InvalidOperationException ||
                        ex is NotSupportedException ||
                        ex is NotImplementedException ||
                        ex is NullReferenceException ||
                        ex is ArgumentNullException ||
                        ex is ArgumentException)
                    {
                        disconnectReason = SshDisconnectReason.ProtocolError;
                        throw;
                    }

                    if (!IsRetryAllowed(ex, SshDisconnectReason.ProtocolError))
                    {
                        throw;
                    }
                }

                // If we got here, we're retrying.
            }
            catch (Exception ex)
            {
                // Bubble up the exception, no retries at this level.
                isFinalAttempt = true;
                exception ??= ex;
                Trace.Error(
                    "Error connecting {0} tunnel session: {1}",
                    relayClient.ConnectionRole,
                    ex is UnauthorizedAccessException || ex is TunnelConnectionException ? ex.Message : ex);

                throw;
            }
            finally
            {
                try
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
                finally
                {
                    if (isFinalAttempt)
                    {
                        this.relayClient.FinishConnecting(disconnectReason, exception);
                    }
                }
            }

            var retryTiming = isDelayNeeded ? $" in {(attemptDelayMs < 1000 ? $"0.{attemptDelayMs / 100}s" : $"{attemptDelayMs / 1000}s")}" : string.Empty;
            Trace.Verbose($"Error connecting to tunnel SSH session, retrying{retryTiming}{(errorDescription != null ? $": {errorDescription}" : string.Empty)}");

            if (isDelayNeeded)
            {
                try
                {
                    await Task.Delay(attemptDelayMs, cts.Token);
                }
                catch (Exception ex)
                {
                    // Either caller cancelled connection or relay client was disposed.
                    ex = TryAdjustCancellation(ex) ?? ex;
                    this.relayClient.FinishConnecting(SshDisconnectReason.ByApplication, ex);
                    throw ex;
                }

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
                        isTunnelAccessTokenRefreshed = await this.relayClient.RefreshTunnelAccessTokenAsync(cts.Token);
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
                        throw TryAdjustCancellation(ex) ??
                            new UnauthorizedAccessException(
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

            Exception? TryAdjustCancellation(Exception exception)
            {
                if (exception is not OperationCanceledException)
                {
                    return null;
                }

                if (this.relayClient.DisposeToken.IsCancellationRequested)
                {
                    // Throw ObjectDisposedException if relay client was disposed.
                    return new ObjectDisposedException(
                        $"{GetType().FullName} is disposed",
                        exception);
                }

                if (cancellation.IsCancellationRequested)
                {
                    // Throw better cancellation exception if caller cancelled.
                    return new OperationCanceledException(
                        "Operation cancelled",
                        exception,
                        cancellation);
                }

                return null;
            }

            void ThrowIfRetryNotAllowed(Exception ex, SshDisconnectReason reason, bool delayNeeded = true)
            {
                if (!IsRetryAllowed(ex,reason, delayNeeded))
                {
                    throw ex;
                }
            }

            bool IsRetryAllowed(
                Exception ex,
                SshDisconnectReason reason = SshDisconnectReason.ConnectionLost,
                bool delayNeeded = true)
            {
                disconnectReason = reason;
                errorDescription = ex.Message;
                exception = ex;
                if (options?.EnableRetry == false)
                {
                    return false;
                }

                isDelayNeeded = delayNeeded;
                var retryDelay = TimeSpan.FromMilliseconds(isDelayNeeded ? attemptDelayMs : 0);
                var retryingArgs = new RetryingTunnelConnectionEventArgs(ex, retryDelay);
                this.relayClient.OnRetrying(retryingArgs);
                if (!retryingArgs.Retry)
                {
                    return false;
                }

                if ((int)retryingArgs.Delay.TotalMilliseconds > 0)
                {
                    attemptDelayMs = (int)retryingArgs.Delay.TotalMilliseconds;
                    isDelayNeeded = true;
                }
                else
                {
                    isDelayNeeded = false;
                }

                return true;
            }
        }
    }
}
