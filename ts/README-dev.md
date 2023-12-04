# Tunnels Library Development
This document contains information about building, debugging, testing,
and benchmarking the Tunnels libraries.

## Prerequisites
 - Install **Node.js** version 10.x or above.
 - Install dependency packages:
   ```
   npm install
   ```

## Building
Command-line builds are driven by scripts in `build.js`.

| Command         | Description                     |
| --------------- | ------------------------------- |
| `npm run build` | Build everything.               |
| `npm run pack`  | Pack everything.                |

### Node.js Testing
```
npm run test
```
To run/debug an individual test case or subset of test cases matching a
substring, use a command similar to the following:

## Making Changes
If you update this SDK, please update the package.json file in conenctions and management to require a dependeny that is > the current published version(You can find this with the `npm view @microsoft/dev-tunnels-contracts` command). This will fix issues where yarn will pull the old version of packages and will cause mismatched dependencies. See [example PR](https://github.com/microsoft/dev-tunnels/pull/358)