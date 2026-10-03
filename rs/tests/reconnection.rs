// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

#![cfg(feature = "connections")]

use tunnels::connections::{ReconnectOptions, SessionHealthState};

#[test]
fn reconnect_options_have_expected_defaults() {
    let options = ReconnectOptions::default();

    assert_eq!(options.initial_delay_ms, 1_000);
    assert_eq!(options.max_delay_ms, 13_000);
    assert_eq!(options.max_attempts, None);
    assert_eq!(options.session_health_check_interval, None);
    assert!(options.token_refresher.is_none());
}

#[test]
fn max_attempts_zero_is_preserved() {
    let options = ReconnectOptions {
        max_attempts: Some(0),
        ..Default::default()
    };

    assert_eq!(options.max_attempts, Some(0));
}

#[test]
fn session_health_states_compare_by_variant_and_count() {
    assert_eq!(
        SessionHealthState::NotConfigured,
        SessionHealthState::NotConfigured
    );
    assert_eq!(
        SessionHealthState::Open { count: 2 },
        SessionHealthState::Open { count: 2 }
    );
    assert_ne!(
        SessionHealthState::Open { count: 2 },
        SessionHealthState::Closed { count: 2 }
    );
}
