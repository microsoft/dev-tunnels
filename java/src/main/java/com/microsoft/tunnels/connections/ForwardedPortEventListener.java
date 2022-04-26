package com.microsoft.tunnels.connections;

import java.util.EventListener;

public interface ForwardedPortEventListener extends EventListener {
  default void onForwardedPortAdded(ForwardedPort port) {
  };

  default void onForwardedPortRemoved(ForwardedPort port) {
  };
}
