package com.microsoft.tunnels.contracts;

import java.lang.reflect.Type;

import com.google.gson.Gson;
import com.google.gson.GsonBuilder;
import com.google.gson.JsonDeserializationContext;
import com.google.gson.JsonDeserializer;
import com.google.gson.JsonElement;
import com.google.gson.JsonParseException;

public class TunnelContracts {
  private TunnelContracts() {}

  private static Gson gson = createConfiguredGson();

  public static Gson getGson() {
    return gson;
  }

  private static Gson createConfiguredGson() {
    // TODO - serializeNulls?
    var builder = new GsonBuilder()
        .excludeFieldsWithoutExposeAnnotation()
        .registerTypeAdapter(ResourceStatus.class, new ResourceStatusDeserializer())
        .registerTypeAdapter(TunnelEndpoint.class, new TunnelEndpointDeserializer());
    return builder.create();
  }

  private static class TunnelEndpointDeserializer implements JsonDeserializer<TunnelEndpoint> {
    private Gson gson;

    TunnelEndpointDeserializer() {
      this.gson = new Gson();
    }

    @Override
    public TunnelEndpoint deserialize(
        JsonElement json,
        Type typeOfT,
        JsonDeserializationContext context)
        throws JsonParseException {
      var endpointObject = json.getAsJsonObject();
      var connectionMode = endpointObject.get("connectionMode").getAsString();
      if (connectionMode.equals("TunnelRelay")) {
        return gson.fromJson(endpointObject, TunnelRelayTunnelEndpoint.class);
      } else if (connectionMode.equals("LocalNetwork")) {
        return gson.fromJson(endpointObject, LocalNetworkTunnelEndpoint.class);
      } else {
        throw new JsonParseException("Unable to parse TunnelEnpoint: " + endpointObject);
      }
    }
  }

  private static class ResourceStatusDeserializer implements JsonDeserializer<ResourceStatus> {
    private Gson gson;

    ResourceStatusDeserializer() {
      this.gson = new Gson();
    }

    @Override
    public ResourceStatus deserialize(
        JsonElement json,
        Type typeOfT,
        JsonDeserializationContext context)
        throws JsonParseException {
      if (json.isJsonObject()) {
        return gson.fromJson(json, ResourceStatus.class);
      } else {
        var status = new ResourceStatus();
        status.current = json.getAsLong();
        return status;
      }
    }
  }
}
