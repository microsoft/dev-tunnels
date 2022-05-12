// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// Generated from ../../../cs/src/Contracts/TunnelConstraints.cs

package tunnels

import (
	"regexp"
	"strconv"
	"strings"
)

const (
	// Min length of tunnel cluster ID.
	TunnelConstraintsClusterIDMinLength = 3

	// Max length of tunnel cluster ID.
	TunnelConstraintsClusterIDMaxLength = 12

	// Characters that are valid in tunnel id. Vowels and 'y' are excluded to avoid
	// accidentally generating any random words.
	TunnelConstraintsTunnelIDChars = "0123456789bcdfghjklmnpqrstvwxz"

	// Length of tunnel id.
	TunnelConstraintsTunnelIDLength = 8

	// Min length of tunnel name.
	TunnelConstraintsTunnelNameMinLength = 3

	// Max length of tunnel name.
	TunnelConstraintsTunnelNameMaxLength = 60
)

var (
	// A regular expression that can match or validate tunnel cluster ID strings.
	//
	// Cluster IDs are alphanumeric; hyphens are not permitted.
	TunnelConstraintsClusterIDRegex = regexp.MustCompile(
		"[a-z][a-z0-9]{" + strconv.Itoa(TunnelConstraintsClusterIDMinLength-1) + "," + strconv.Itoa(TunnelConstraintsClusterIDMaxLength-1) + "}")

	// A regular expression that can match or validate tunnel ID strings.
	//
	// Tunnel IDs are fixed-length and have a limited character set of numbers and some
	// lowercase letters (minus vowels).
	TunnelConstraintsTunnelIDRegex = regexp.MustCompile(
		"[" + strings.Replace(TunnelConstraintsTunnelIDChars, "0123456789", "0-9", -1) + "]{" + strconv.Itoa(TunnelConstraintsTunnelIDLength) + "}")

	// A regular expression that can match or validate tunnel names.
	//
	// Tunnel names are alphanumeric and may contain hyphens.
	TunnelConstraintsTunnelNameRegex = regexp.MustCompile(
		"[a-z0-9][a-z0-9-]{" +
			strconv.Itoa(TunnelConstraintsTunnelNameMinLength-2) + "," + strconv.Itoa(TunnelConstraintsTunnelNameMaxLength-2) +
			"}[a-z0-9]")

	// A regular expression that can match or validate tunnel names.
	TunnelConstraintsTunnelTagRegex = regexp.MustCompile(`^[\w-=]+$`)
)
