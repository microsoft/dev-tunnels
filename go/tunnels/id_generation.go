package tunnels

import (
	"math/rand"
	"strings"
	"time"
)

var nouns = []string{"pond", "hill", "mountain", "field", "fog", "ant", "dog", "cat", "rabbit", "shoe", "campsite", "plane", "sofa", "chair", "library", "book", "ocean", "lake", "river", "horse"}
var adjectives = []string{"fun", "happy", "interesting", "neat", "peaceful", "puzzeled", "thoughtful", "kind", "joyful", "overjoyed", "new", "giant", "sneaky", "quick", "majestic", "gleaming", "jolly", "fancy", "tidy", "marvelous", "glamorous", "swift", "silent", "amusing", "spiffy"}

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
