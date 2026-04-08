// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

package tunnels

import (
	"context"
	"errors"
	"fmt"
	"net/http"
	"time"
)

const (
	reconnectBaseDelay = 100 * time.Millisecond
	reconnectMaxDelay  = 12800 * time.Millisecond
)

// reconnect attempts to re-establish the relay connection with exponential backoff.
func (h *Host) reconnect(ctx context.Context) error {
	if ctx == nil {
		ctx = context.Background()
	}

	delay := reconnectBaseDelay
	for {
		select {
		case <-ctx.Done():
			return ctx.Err()
		default:
		}

		h.logger.Printf("reconnecting in %v", delay)

		select {
		case <-time.After(delay):
		case <-ctx.Done():
			return ctx.Err()
		}

		h.mu.Lock()
		tunnel := h.tunnel
		h.mu.Unlock()

		err := h.connectOnce(ctx, tunnel)
		if err == nil {
			h.logger.Printf("reconnected to relay")
			return nil
		}

		// On 401 Unauthorized, try refreshing the access token.
		var tunnelErr *TunnelError
		if errors.As(err, &tunnelErr) && tunnelErr.StatusCode == http.StatusUnauthorized {
			if refreshErr := h.refreshAccessToken(ctx); refreshErr != nil {
				h.logger.Printf("error refreshing access token: %v", refreshErr)
			} else {
				// Token refreshed — retry immediately with the same delay.
				continue
			}
		}

		h.logger.Printf("error reconnecting: %v", err)

		// Exponential backoff.
		delay *= 2
		if delay > reconnectMaxDelay {
			delay = reconnectMaxDelay
		}
	}
}

// refreshAccessToken attempts to refresh the tunnel access token using the
// callback, or by re-fetching the tunnel from the management service.
func (h *Host) refreshAccessToken(ctx context.Context) error {
	h.mu.Lock()
	tunnel := h.tunnel
	cb := h.RefreshTunnelAccessTokenFunc
	h.mu.Unlock()

	if cb != nil {
		token, err := cb(ctx)
		if err != nil {
			return err
		}
		h.mu.Lock()
		if tunnel.AccessTokens == nil {
			tunnel.AccessTokens = make(map[TunnelAccessScope]string)
		}
		tunnel.AccessTokens[TunnelAccessScopeHost] = token
		h.mu.Unlock()
		return nil
	}

	// Fallback: re-fetch the tunnel from the management service.
	opts := &TunnelRequestOptions{
		TokenScopes: TunnelAccessScopes{TunnelAccessScopeHost},
	}
	refreshed, err := h.manager.GetTunnel(ctx, tunnel, opts)
	if err != nil {
		return fmt.Errorf("error refreshing tunnel: %w", err)
	}

	h.mu.Lock()
	if refreshed.AccessTokens != nil {
		if tunnel.AccessTokens == nil {
			tunnel.AccessTokens = make(map[TunnelAccessScope]string)
		}
		for scope, token := range refreshed.AccessTokens {
			tunnel.AccessTokens[scope] = token
		}
	}
	h.mu.Unlock()
	return nil
}
