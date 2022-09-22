// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

use thiserror::Error;

/// Type of error returned from tunnel operations.
#[derive(Debug, Error)]
pub enum TunnelError {
    #[error("{reason}: {error}")]
    HttpError {
        error: crate::management::HttpError,
        reason: &'static str,
    },

    #[error("the tunnel relay was disconnected: {0}")]
    TunnelRelayDisconnected(#[from] russh::Error),

    #[error("the tunnel host relay endpoint URI is missing")]
    MissingHostEndpoint,

    #[error("invalid host relay uri: {0}")]
    InvalidHostEndpoint(String),

    #[error("websocket error: {0}")]
    WebSocketError(#[from] tungstenite::Error),

    #[error("port {0} already exists in the relay")]
    PortAlreadyExists(u32),
}
