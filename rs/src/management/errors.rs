// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

use std::{error::Error, fmt::Display};

use reqwest::StatusCode;
use url::Url;

use crate::contracts::ProblemDetails;

/// Type of result returned from HTTP operations.
pub type HttpResult<R> = Result<R, HttpError>;

/// Type of error returned from HTTP operations.
#[derive(Debug)]
pub enum HttpError {
    /// An error during connection to the remote.
    ConnectionError(reqwest::Error),
    /// An error returned from the remote server.
    ResponseError(ResponseError),
    /// An error was returned from the authorization callback.
    AuthorizationError(String),
}

impl Error for HttpError {
    fn source(&self) -> Option<&(dyn Error + 'static)> {
        match self {
            HttpError::ConnectionError(e) => Some(e),
            _ => None,
        }
    }
}

impl Display for HttpError {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            HttpError::ConnectionError(e) => write!(f, "connection error: {}", e),
            HttpError::ResponseError(e) => write!(f, "response error: {}", e),
            HttpError::AuthorizationError(e) => write!(f, "authorization error: {}", e),
        }
    }
}

/// Part of the `HttpError` returned from a non-successfl response.
#[derive(Debug)]
pub struct ResponseError {
    /// Original request URL.
    pub url: Url,
    /// Response status code
    pub status_code: StatusCode,
    /// Error contents of the response, if any
    pub data: Option<String>,
    /// Request ID for debugging purposes
    pub request_id: Option<String>,
}

impl ResponseError {
    /// Attempts to get service problem details, if available.
    pub fn get_details(&self) -> Option<ProblemDetails> {
        self.data
            .as_deref()
            .and_then(|d| serde_json::from_str(d).ok())
    }
}

impl Display for ResponseError {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        write!(
            f,
            "HTTP status {} from {} (request ID {}): {}",
            self.status_code,
            self.url,
            self.request_id.as_deref().unwrap_or("<none>"),
            self.data.as_deref().unwrap_or("(empty body)")
        )
    }
}
