# Publishing

1. Update the packageVersion constant in tunnels.go to the new version 

2. Tag the new version with `git tag v0.0.X` (replace X with new version number)

3. Push the tag to github with `git push origin v0.0.X`

4. Publish the new version to the go package index `go list -m github.com/microsoft/dev-tunnels@v0.0.X`