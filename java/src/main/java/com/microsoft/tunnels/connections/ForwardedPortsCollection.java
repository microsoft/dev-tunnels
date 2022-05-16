// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

package com.microsoft.tunnels.connections;

import java.util.AbstractList;
import java.util.ArrayList;
import java.util.List;

/**
 * An unmodifiable list of {@link ForwardedPort}s.
 * Also keeps a list of {@link ForwardedPortEventListener}s to be called when a
 * port is added or removed.
 */
public class ForwardedPortsCollection extends AbstractList<ForwardedPort> {
  private List<ForwardedPortEventListener> listeners = new ArrayList<ForwardedPortEventListener>();
  private List<ForwardedPort> ports = new ArrayList<ForwardedPort>();

  public ForwardedPort get(int i) {
    return ports.get(i);
  }

  public int size() {
    return ports.size();
  }

  /**
   * Adds the specified {@link ForwardedPortEventListener} which should implement
   * onForwardedPortAdded and/or onForwardedPortRemoved.
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
   * @param listener the {@link ForwardedPortEventListener} to add.
   */
  public void addListener(ForwardedPortEventListener listener) {
    if (!listeners.contains(listener)) {
      listeners.add(listener);
    }
  }

  /**
   * Removes the specified {@link ForwardedPortEventListener} listener.
   *
   * @param listener the {@link ForwardedPortEventListener} to remove.
   */
  public void removeListener(ForwardedPortEventListener listener) {
    listeners.remove(listener);
  }

  /**
   * Internal.
   * Adds the specified {@link ForwardedPort} and calls listeners that provice
   * onForwardedPortAdded.
   *
   * @param port the {@link ForwardedPort} port to add.
   */
  void addPort(ForwardedPort port) {
    if (ports.stream().anyMatch(p -> p.getRemotePort() == port.getRemotePort())) {
      throw new IllegalStateException("Port has already been added to the collection.");
    }
    ports.add(port);
    for (ForwardedPortEventListener listener : listeners) {
      listener.onForwardedPortAdded(port);
    }
  }

  /**
   * Internal.
   * Removes the specified {@link ForwardedPort} and notifies listeners that
   * provide onForwardedPortRemoved.
   *
   * @param port the {@link ForwardedPort} port to remove.
   */
  void removePort(ForwardedPort port) {
    if (ports.removeIf(p -> p.getRemotePort() == port.getRemotePort())) {
      for (ForwardedPortEventListener listener : listeners) {
        listener.onForwardedPortRemoved(port);
      }
    }
  }
}
