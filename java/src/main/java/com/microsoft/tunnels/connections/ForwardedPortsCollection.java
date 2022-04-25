package com.microsoft.tunnels.connections;

import java.util.ArrayList;
import java.util.List;

/**
 * An ArrayList of {@link ForwardedPort}s and holder for a
 * {@link ForwardedPortEventListener}.
 *
 */
public class ForwardedPortsCollection {
  private List<ForwardedPortEventListener> listeners = new ArrayList<ForwardedPortEventListener>();
  private List<ForwardedPort> ports = new ArrayList<ForwardedPort>();

  public List<ForwardedPort> getPorts() {
    return ports;
  }

  /**
   * Sets the event listener for port forwarding events. A
   * {@link ForwardedPortEventListener} should
   * be provided which can implement onForwardedPortAdded and/or
   * onForwardedPortRemoved.
   *
   * <pre>
   * setForwardedPortEventListener(new ForwardedPortEventListener() {
   *   &#64;Override
   *   public void onForwardedPortAdded(ForwardedPort port) {
   *     // Do something when the port is added.
   *   };
   *
   *   public void onForwardedPortAdded(ForwardedPort port) {
   *     // Do something when the port is removed.
   *   };
   * });
   * </pre>
   *
   * @param listener
   */
  public void addListener(ForwardedPortEventListener listener) {
    if (!listeners.contains(listener)) {
      listeners.add(listener);
    }
  }

  public void removeListener(ForwardedPortEventListener listener) {
    listeners.remove(listener);
  }

  public void addPort(ForwardedPort port) {
    if (ports.stream().anyMatch(p -> p.getRemotePort() == port.getRemotePort())) {
      throw new IllegalStateException("Port has already been added to the collection.");
    }
    ports.add(port);
    for (ForwardedPortEventListener listener : listeners) {
      listener.onForwardedPortAdded(port);
    }
  }

  public void removePort(ForwardedPort port) {
    if (ports.removeIf(p -> p.getRemotePort() == port.getRemotePort())) {
      for (ForwardedPortEventListener listener : listeners) {
        listener.onForwardedPortRemoved(port);
      }
    }
  }
}
