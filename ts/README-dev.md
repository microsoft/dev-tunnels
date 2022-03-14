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
