// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

package tunnels

import "bytes"

type buffer struct {
	bytes.Buffer
}

// Add a Close method to our buffer so that we satisfy io.ReadWriteCloser.
func (b *buffer) Close() error {
	b.Buffer.Reset()
	return nil
}
