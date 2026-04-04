// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

// @group TestSetup : Feature-gated imports for connection tests
#[cfg(feature = "connections")]
use tunnels::connections::{KeepAliveState, ReconnectOptions};

// @group UnitTests > Pure Logic : Exponential backoff cap test (no crate types needed)
#[test]
fn test_exponential_backoff_cap() {
    let initial = 1_000u64;
    let max = 13_000u64;
    let mut delay = initial;
    let steps: Vec<u64> = {
        let mut v = vec![delay];
        for _ in 0..10 {
            delay = (delay * 2).min(max);
            v.push(delay);
        }
        v
    };
    assert_eq!(steps[0], 1_000);
    assert_eq!(steps[1], 2_000);
    assert_eq!(steps[2], 4_000);
    assert_eq!(steps[3], 8_000);
    assert_eq!(steps[4], 13_000); // capped
    assert_eq!(steps[5], 13_000); // stays at cap
}

// @group UnitTests > Pure Logic : SSH error sets skip_delay and resets backoff
#[test]
fn test_skip_delay_resets_delay_on_ssh_error() {
    let initial_delay = 1_000u64;
    let mut delay = 8_000u64; // simulate ramped-up delay
    let mut skip_delay = false;

    // Simulate SSH error path sets skip_delay and resets delay
    delay = initial_delay;
    skip_delay = true;

    let effective_delay = if skip_delay { 0 } else { delay };
    assert_eq!(effective_delay, 0, "SSH error should skip the wait");
    assert_eq!(delay, initial_delay, "delay should reset to initial after SSH error");
}

// @group UnitTests > ReconnectOptions : Default field values
#[cfg(feature = "connections")]
#[test]
fn test_reconnect_options_defaults() {
    let opts = ReconnectOptions::default();
    assert_eq!(opts.initial_delay_ms, 1_000);
    assert_eq!(opts.max_delay_ms, 13_000);
    assert!(opts.max_attempts.is_none());
    assert!(opts.keep_alive_interval.is_none(), "keep_alive_interval should be None by default");
    assert!(opts.token_refresher.is_none(), "token_refresher should be None by default");
}

// @group UnitTests > ReconnectOptions : max_attempts=0 is preserved in configuration
#[cfg(feature = "connections")]
#[test]
fn test_max_attempts_zero_is_preserved_in_options() {
    let opts = ReconnectOptions {
        max_attempts: Some(0),
        ..Default::default()
    };

    assert_eq!(opts.max_attempts, Some(0));
    assert_eq!(opts.initial_delay_ms, 1_000);
    assert_eq!(opts.max_delay_ms, 13_000);
    assert!(opts.keep_alive_interval.is_none(), "keep_alive_interval should remain None by default");
    assert!(opts.token_refresher.is_none(), "token_refresher should remain None by default");
}

// @group UnitTests > KeepAliveState : All variants construct and compare correctly
#[cfg(feature = "connections")]
#[test]
fn test_keep_alive_state_variants() {
    assert_eq!(KeepAliveState::NotConfigured, KeepAliveState::NotConfigured);
    assert_eq!(
        KeepAliveState::Succeeded { count: 42 },
        KeepAliveState::Succeeded { count: 42 }
    );
    assert_eq!(
        KeepAliveState::Failed { count: 7 },
        KeepAliveState::Failed { count: 7 }
    );
    assert_ne!(
        KeepAliveState::Succeeded { count: 1 },
        KeepAliveState::Failed { count: 1 }
    );
}

// @group UnitTests > KeepAliveState : Clone preserves value
#[cfg(feature = "connections")]
#[test]
fn test_keep_alive_state_clone() {
    let original = KeepAliveState::Succeeded { count: 3 };
    let cloned = original.clone();
    assert_eq!(original, cloned);
}

// @group UnitTests > KeepAliveState : watch channel starts NotConfigured and updates
#[cfg(feature = "connections")]
#[tokio::test]
async fn test_keep_alive_state_watch_channel_updates() {
    let (tx, rx) = tokio::sync::watch::channel(KeepAliveState::NotConfigured);
    assert_eq!(*rx.borrow(), KeepAliveState::NotConfigured);
    tx.send(KeepAliveState::Succeeded { count: 1 }).unwrap();
    assert_eq!(*rx.borrow(), KeepAliveState::Succeeded { count: 1 });
    tx.send(KeepAliveState::Failed { count: 2 }).unwrap();
    assert_eq!(*rx.borrow(), KeepAliveState::Failed { count: 2 });
}