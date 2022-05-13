// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

package tunnelssh

import "golang.org/x/crypto/ssh"

// SSHRequest represents an SSH request.
type SSHRequest interface {
	Type() string
	Reply(ok bool, payload []byte) error
}
type sshRequest struct {
	request *ssh.Request
}

func (sr *sshRequest) Type() string {
	return sr.request.Type
}

func (sr *sshRequest) Reply(ok bool, payload []byte) error {
	return sr.request.Reply(ok, payload)
}

func (s *Session) convertRequests(reqs <-chan *ssh.Request) <-chan SSHRequest {
	out := make(chan SSHRequest)
	go func() {
		for req := range reqs {
			out <- &sshRequest{req}
		}
		close(out)
	}()
	return out
}
