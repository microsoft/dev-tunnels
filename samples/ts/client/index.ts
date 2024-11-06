import {
  ManagementApiVersions,
  ProductHeaderValue,
  TunnelManagementHttpClient,
  TunnelRequestOptions,
} from "@microsoft/dev-tunnels-management";
import {
  Tunnel,
  TunnelAccessScopes,
} from "@microsoft/dev-tunnels-contracts";
import {
  TunnelRelayTunnelClient,
} from "@microsoft/dev-tunnels-connections";

const tunnelId = process.env.TUNNEL_ID;
if (!tunnelId) {
  console.error("TUNNEL_ID environment variable is required.");
  process.exit(1);
}

const clusterId = process.env.CLUSTER_ID || 'usw2';

const accessToken = process.env.ACCESS_TOKEN;
if (!accessToken) {
  console.error("ACCESS_TOKEN environment variable is required.");
  process.exit(1);
}

async function connectClient(tunnelManagementClient: TunnelManagementHttpClient) {
  const tunnelReference: Tunnel = {
    tunnelId,
    clusterId,    
  };

  const tunnelRequestOptions: TunnelRequestOptions = {
    tokenScopes: [TunnelAccessScopes.Connect],
    accessToken,
  };

  const tunnel = await tunnelManagementClient.getTunnel(tunnelReference, tunnelRequestOptions);
  let client = new TunnelRelayTunnelClient();
  client.trace = (level, eventId, msg, err) => {
    console.log(`client: ${msg}`);
  };

  await client.connect(tunnel!);
  console.log('Tunnel client connected');
  return { tunnel, client };
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
  console.error("AAD_TOKEN environment variable is required. You can get your AAD token by running 'devtunnels login --verbose' and copying the token from the output.");
  process.exit(1);
}

function createTunnelManagementClient() {
  const userAgent: ProductHeaderValue = {
    name: "devtunnels-sample-client",
    version: "1.0.0",
  };

  const tunnelManagementClient = new TunnelManagementHttpClient(
    userAgent,
    ManagementApiVersions.Version20230927preview,
    () => Promise.resolve(`Bearer ${aadToken}`)
  );

  tunnelManagementClient.trace = (msg) => {
    console.log(`tunnel: ${msg}`);
  }

  return tunnelManagementClient;
}

async function main() {
  try {
    const tunnelManagementClient = createTunnelManagementClient();
  
    console.log("\nConnecting client...");
    const { tunnel, client } = await connectClient(tunnelManagementClient);
    console.log("\nPress any key to exit...\n");
    await readAnyKey();

    console.log("\nStopping tunnel client..."); 
    client.dispose();

  } catch (err) {
    console.error(err);
  }

  process.exit();
}

main();