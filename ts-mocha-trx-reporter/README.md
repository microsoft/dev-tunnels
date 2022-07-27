### Building
Run `npm install` and `npm run build`.

### Publishing
This package is published to GitHub packages. You will need to add a new line to your user `.npmrc` containing `//npm.pkg.github.com/:_authToken=TOKEN`, where `TOKEN` is a GH personal access token with repo write permissions. [Read the full instructions here](https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-npm-registry#authenticating-with-a-personal-access-token).

Then run `npm run publish`.

### Installing the published package
Authenticate to GitHub packages using the method above. For installing, your PAT only needs repo read permissions.

Add `@microsoft:registry=https://npm.pkg.github.com` to the `.npmrc` of the project that will take a dependency on this package.

Finally add `"@microsoft/ts-mocha-trx-reporter": "^1.0.0"` to the project's `package.json`.