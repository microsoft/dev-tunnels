## Tunnels Java SDK

### Setting up development
These instructions assume you are using vscode for development as SDK is configured for it.
1. Clone this repo and open the `java` folder in vscode.
2. Install the recommended extension pack "Extension Pack for Java"
3. The extension will prompt you to install a JDK. Choose JDK version 11 (LTS).
4. Download and [install Maven](https://maven.apache.org/install.html).
   - 👉 You may need to set up [M2_HOME and/or additional environment variables](https://www.tutorialspoint.com/maven/maven_environment_setup.htm) manually.
5. Once you have the extension and JDK installed, run `mvn test` (see next section for test setup).

### Testing
1. Get a user token using the CLI command: `user show --verbose`
2. Create a new environment variable `TEST_TUNNEL_TOKEN` with a string value "Bearer <token>".
3. Create a new environment variable `TEST_TUNNEL_NAME` with a value containing the name of the tunnel.
4. Optionally: set `TEST_TUNNEL_VERBOSE=1` to enable verbose console logging during tests.
5. Use the CLI to host the tunnel.
6. Run the tests with `mvn test`, or run a single test with `mvn test -Dtest=TunnelClientTests#connectClient`

### Publishing
The Tunnels Java SDK is published as a GitHub package through a [GitHub Action](../.github/workflows/java-sdk-release.yml). Since the repo is shared by multiple language SDKs, the Java packages are distinguished with a tag of the form `java-vX.Y.Z`. See [tags](https://github.com/microsoft/dev-tunnels/tags) for examples.

Follow these steps to publish a new version of the Java package:
1. Create a new [release](https://github.com/microsoft/dev-tunnels/releases/new).
2. Create a new tag in the `java-vX.Y.Z` format. The version needs to be greater than the latest `java-*` version in the [releases](https://github.com/microsoft/dev-tunnels/releases) page.
3. Set the release title the same as the version tag. Increment the major, minor, or patch version from the latest release as appropriate.
4. Publish the release.
