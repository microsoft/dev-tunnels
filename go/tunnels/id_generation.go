package tunnels

import (
	"math/rand"
	"strings"
	"time"
)

var nouns = []string{"pond", "hill", "mountain", "field", "fog", "ant", "dog", "cat", "shoe", "plane", "chair", "book", "ocean", "lake", "river", "horse"}
var adjectives = []string{"fun", "happy", "interesting", "neat", "peaceful", "puzzled", "kind", "joyful", "new", "giant", "sneaky", "quick", "majestic", "jolly", "fancy", "tidy", "swift", "silent", "amusing", "spiffy"}

func generateTunnelId() string {
	rand.Seed(time.Now().UnixNano())
	var sb strings.Builder
	sb.WriteString(adjectives[rand.Intn(len(adjectives))])
	sb.WriteString("-")
	sb.WriteString(nouns[rand.Intn(len(nouns))])
	sb.WriteString("-")

	for i := 0; i < 7; i++ {
		sb.WriteByte(TunnelConstraintsOldTunnelIDChars[rand.Intn(len(TunnelConstraintsOldTunnelIDChars))])
	}
	return sb.String()
}
