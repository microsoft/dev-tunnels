package tunnelssh

import (
	"io"
	"log"
	"net"

	"golang.org/x/crypto/ssh"
)

type SSHSession struct {
	*ssh.Session
	socket net.Conn
	conn   ssh.Conn
	reader io.Reader
	writer io.Writer
	logger *log.Logger
}

func (s *SSHSession) Read(p []byte) (n int, err error) {
	return s.reader.Read(p)
}

func (s *SSHSession) Write(p []byte) (n int, err error) {
	return s.writer.Write(p)
}
