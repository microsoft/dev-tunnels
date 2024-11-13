import {
  ManagementApiVersions,
  ProductHeaderValue,
  TunnelManagementHttpClient,
  TunnelRequestOptions,
} from "@microsoft/dev-tunnels-management";
import {
  Tunnel,
  TunnelAccessControlEntryType,
  TunnelAccessScopes,
  TunnelEndpoint,
} from "@microsoft/dev-tunnels-contracts";
import { TunnelRelayTunnelHost } from "@microsoft/dev-tunnels-connections";
import * as http from "http";
import net from "net";

let portNumber = 8090;
const protocol = "http";

function findFreePort(startPort: number): Promise<number> {
  return new Promise((resolve, reject) => {
    const server = net.createServer();
    server.once("error", (err: NodeJS.ErrnoException) => {
      if (err.code === "EADDRINUSE") {
        findFreePort(startPort + 1)
          .then(resolve)
          .catch(reject);
      } else {
        reject(err);
      }
    });
    server.once("listening", () => server.close(() => resolve(startPort)));
    server.listen(startPort);
  });
};

async function startServer(): Promise<http.Server> {
  portNumber = await findFreePort(portNumber);

  const server = http.createServer((req, res) => {
    res.statusCode = 200;
    res.setHeader("Content-Type", "text/plain");
    res.end("Hello, world!\n");
  });

  await new Promise<void>((resolve) => {
    server.listen({ port: portNumber, exclusive: true }, () => {
      console.log(`Server running at ${protocol}://localhost:${portNumber}/`);
      resolve();
    });
  });

  return server;
}

async function createTunnelAndConnectHost(
  tunnelManagementClient: TunnelManagementHttpClient
) {
  const newTunnel: Tunnel = {
    ports: [
      {
        portNumber,
        protocol,
      },
    ],
    accessControl: {
      entries: [
        {
          type: TunnelAccessControlEntryType.Anonymous,
          subjects: [],
          scopes: [TunnelAccessScopes.Connect],
        },
      ],
    },
  };

  const tunnelRequestOptions: TunnelRequestOptions = {
    tokenScopes: [TunnelAccessScopes.Host, TunnelAccessScopes.Connect],
    includePorts: true,
  };

  const tunnel = await tunnelManagementClient.createTunnel(
    newTunnel,
    tunnelRequestOptions
  );

  const host = new TunnelRelayTunnelHost(tunnelManagementClient);
  host.trace = (level, eventId, msg, err) => {
    console.log(`host: ${msg}`);
  };

  await host.connect(tunnel!);

  console.log("Tunnel host connected:");
  console.log(`    Tunnel ID: ${tunnel.tunnelId}`);
  console.log(`    Cluster ID: ${tunnel.clusterId}`);
  const clientAccessToken = tunnel.accessTokens?.[TunnelAccessScopes.Connect];
  if (clientAccessToken) {
    console.log(`    Client access token: ${clientAccessToken}`);
  }

  tunnel.ports?.forEach(({ portNumber, portForwardingUris }) => {
    console.log(`    Tunnel port: ${portNumber}`);
    if (portForwardingUris) {
      portForwardingUris.forEach((uri) =>
        console.log(`        Port forwarding URI: ${uri}`)
      );
    } else {
      newTunnel.endpoints?.forEach((endpoint) => {
        console.log(
          `        Port forwarding URI: ${TunnelEndpoint.getPortUri(
            endpoint,
            portNumber
          )}`
        );
      });
    }
  });

  return { tunnel, host };
}

function readAnyKey() {
  return new Promise<void>((resolve) => {
    // Debugger console may not support raw mode. Check if it's available.
    if (typeof process.stdin.setRawMode === "function") {
      process.stdin.setRawMode(true);
    }

    process.stdin.resume();
    process.stdin.once("data", () => {
      if (typeof process.stdin.setRawMode === "function") {
        process.stdin.setRawMode(false);
      }

      resolve();
    });
  });
}

const aadToken = process.env.AAD_TOKEN;
if (!aadToken) {
  console.error(
    "AAD_TOKEN environment variable is required. You can get your AAD token by running 'devtunnels login --verbose' and copying the token from the output."
  );
  process.exit(1);
}

function createTunnelManagementClient() {
  const userAgent: ProductHeaderValue = {
    name: "devtunnels-sample-host",
    version: "1.0.0",
  };

  const tunnelManagementClient = new TunnelManagementHttpClient(
    userAgent,
    ManagementApiVersions.Version20230927preview,
    () => Promise.resolve(`Bearer ${aadToken}`)
  );

  tunnelManagementClient.trace = (msg) => {
    console.log(`tunnel: ${msg}`);
  };

  return tunnelManagementClient;
}

async function main() {
  try {
    const server = await startServer();

    const tunnelManagementClient = createTunnelManagementClient();

    console.log("\nCreating tunnel and starting host...");
    const { tunnel, host } = await createTunnelAndConnectHost(
      tunnelManagementClient
    );
    console.log("\nPress any key to exit...\n");
    await readAnyKey();

    console.log("\nStopping tunnel host...");
    host.dispose();
    server.close();

    console.log("\nDeleting tunnel...");
    await tunnelManagementClient.deleteTunnel(tunnel);
  } catch (err) {
    console.error(err);
  }

  process.exit();
}

main();
