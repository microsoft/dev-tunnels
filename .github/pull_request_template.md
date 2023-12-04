Fixes #

### Changes proposed: 
-
-
-

### Other Tasks:

- [ ] If you updated the Go SDK did you update the PackageVersion in tunnels.go
- [ ] If you updated the TS SDK did you update the dependencies in package.json for connections and management to require a dependency that is > the current published version(Found using `npm view @microsoft/dev-tunnels-contracts`). This will fix issues where yarn will pull the old version of packages and will cause mismatched dependencies. See [example PR](https://github.com/microsoft/dev-tunnels/pull/358)
