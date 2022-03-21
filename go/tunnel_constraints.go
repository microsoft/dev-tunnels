// Generated from ../../../cs/src/Contracts/TunnelConstraints.cs

package tunnels

import (
	"regexp"
	"strconv"
	"strings"
)

	// Min length of tunnel cluster ID.
var TunnelConstraintsClusterIDMinLength = 3

	// Max length of tunnel cluster ID.
var TunnelConstraintsClusterIDMaxLength = 12

	// Characters that are valid in tunnel id. Vowels and 'y' are excluded to avoid
	// accidentally generating any random words.
var TunnelConstraintsTunnelIDChars = "0123456789bcdfghjklmnpqrstvwxz"

	// Length of tunnel id.
var TunnelConstraintsTunnelIDLength = 8

	// Min length of tunnel name.
var TunnelConstraintsTunnelNameMinLength = 3

	// Max length of tunnel name.
var TunnelConstraintsTunnelNameMaxLength = 60

	// Gets a regular expression that can match or validate tunnel cluster ID strings.
	//
	// Cluster IDs are alphanumeric; hyphens are not permitted.
var TunnelConstraintsClusterIDRegex = regexp.MustCompile(
        "[a-z][a-z0-9]{" + strconv.Itoa(TunnelConstraintsClusterIDMinLength - 1) + "," + strconv.Itoa(TunnelConstraintsClusterIDMaxLength - 1) + "}")

	// Gets a regular expression that can match or validate tunnel ID strings.
	//
	// Tunnel IDs are fixed-length and have a limited character set of numbers and some
	// lowercase letters (minus vowels).
var TunnelConstraintsTunnelIDRegex = regexp.MustCompile(
        "[" + strings.Replace(TunnelConstraintsTunnelIDChars, "0123456789", "0-9", -1) + "]{" + strconv.Itoa(TunnelConstraintsTunnelIDLength) + "}")

	// Gets a regular expression that can match or validate tunnel names.
	//
	// Tunnel names are alphanumeric and may contain hyphens.
var TunnelConstraintsTunnelNameRegex = regexp.MustCompile(
        "[a-z0-9][a-z0-9-]{" +
        strconv.Itoa(TunnelConstraintsTunnelNameMinLength - 2) + "," + strconv.Itoa(TunnelConstraintsTunnelNameMaxLength - 2) +
        "}[a-z0-9]")

	// Gets a regular expression that can match or validate tunnel names.
var TunnelConstraintsTunnelTagRegex = regexp.MustCompile("^[\\w-=]+$")
