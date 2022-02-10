package tunnels

func contains(s []string, e string) bool {
	for _, a := range s {
		if a == e {
			return true
		}
	}
	return false
}

func isEmpty(s string) bool {
	return len(s) == 0
}
