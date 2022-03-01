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
