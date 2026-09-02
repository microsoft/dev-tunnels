// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

/// Describes how the cluster for a tunnel create request was chosen.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum TunnelClusterSource {
    /// The caller specified the cluster.
    Explicit,
    /// The recommendations API returned a cluster.
    Recommended,
    /// The recommendations API returned a cluster after rejecting the caller's token.
    RecommendedAfterAuthRejected,
    /// The recommendations API remained unauthorized after any anonymous retry.
    FallbackAuthFailed,
    /// The recommendations API returned no cluster.
    FallbackEmpty,
    /// The recommendations API request failed.
    FallbackError,
}

impl TunnelClusterSource {
    pub(crate) const fn as_header_value(self) -> &'static str {
        match self {
            Self::Explicit => "explicit",
            Self::Recommended => "recommended",
            Self::RecommendedAfterAuthRejected => "recommended-after-auth-rejected",
            Self::FallbackAuthFailed => "fallback-auth-failed",
            Self::FallbackEmpty => "fallback-empty",
            Self::FallbackError => "fallback-error",
        }
    }

    /// Gets whether global routing was used instead of a recommendation.
    pub const fn is_fallback(self) -> bool {
        matches!(
            self,
            Self::FallbackAuthFailed | Self::FallbackEmpty | Self::FallbackError
        )
    }
}
