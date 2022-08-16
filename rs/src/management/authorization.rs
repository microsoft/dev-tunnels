use async_trait::async_trait;

use super::HttpError;

#[derive(Clone)]
pub enum Authorization {
    /// No authorization.
    Anonymous,
    /// Authentication scheme for AAD (or Microsoft account) access tokens.
    AAD(String),
    /// Authentication scheme for GitHub access tokens
    Github(String),
    /// Authentication scheme for tunnel access tokens
    Tunnel(String),
    /// Authentication scheme for classic OAuth bearer tokens.
    Bearer(String),
}

impl Authorization {
    pub fn as_header(&self) -> Option<String> {
        match self {
            Authorization::AAD(token) => Some(format!("aad {}", token)),
            Authorization::Github(token) => Some(format!("github {}", token)),
            Authorization::Tunnel(token) => Some(format!("tunnel {}", token)),
            Authorization::Bearer(token) => Some(format!("bearer {}", token)),
            Authorization::Anonymous => None,
        }
    }
}

#[async_trait]
pub trait AuthorizationProvider: Send + Sync {
    async fn get_authorization(&self) -> Result<Authorization, HttpError>;
}

pub(crate) struct StaticAuthorizationProvider(pub Authorization);

#[async_trait]
impl AuthorizationProvider for StaticAuthorizationProvider {
    async fn get_authorization(&self) -> Result<Authorization, HttpError> {
        Ok(self.0.clone())
    }
}
