// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

use std::task::Poll;

/// Helper used when converting Future interfaces to poll-based interfaces.
/// Stores excess data that can be reused on future polls.
#[derive(Default)]
pub(crate) struct ReadBuffer(Option<Vec<u8>>);

impl ReadBuffer {
    /// Removes any data stored in the read buffer
    pub fn take_data(&mut self) -> Option<Vec<u8>> {
        self.0.take()
    }

    /// Writes as many bytes as possible to the readbuf, stashing any extra.
    pub fn put_data(
        &mut self,
        target: &mut tokio::io::ReadBuf<'_>,
        bytes: &[u8],
    ) -> Poll<std::io::Result<()>> {
        if bytes.is_empty() {
            self.0 = None;
            // should not return Ok(), since if nothing is written to the target
            // it signals EOF. Instead wait for more data from the source.
            return Poll::Pending;
        }

        if target.remaining() >= bytes.len() {
            self.0 = None;
            target.put_slice(bytes);
        } else {
            self.0 = Some(bytes[target.remaining()..].to_vec());
            target.put_slice(&bytes[..target.remaining()]);
        }

        Poll::Ready(Ok(()))
    }
}
