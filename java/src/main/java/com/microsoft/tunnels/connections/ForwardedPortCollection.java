package com.microsoft.tunnels.connections;

import java.util.ArrayList;

/**
 * An ArrayList of {@link ForwardedPort}s and holder for a {@link ForwardedPortEventListener}.
 *
 */
public class ForwardedPortCollection extends ArrayList<ForwardedPort> {
  private ForwardedPortEventListener forwardedPortEventListener = new ForwardedPortEventListener() {};

  /**
   * Sets the event listener for port forwarding events. A {@link ForwardedPortEventListener} should
   * be provided which can implement onForwardedPortAdded and/or onForwardedPortRemoved.
   *
   * <pre>
   * setForwardedPortEventListener(new ForwardedPortEventListener() {
   *   &#64;Override
   *   public void onForwardedPortAdded(ForwardedPort port) {
   *     // Do something when the port is added.
   *   };
   *   public void onForwardedPortAdded(ForwardedPort port) {
   *     // Do something when the port is removed.
   *   };
   * });
   * </pre>
   *
   * @param listener
   */
  public void setForwardedPortEventListener(ForwardedPortEventListener listener) {
    this.forwardedPortEventListener = listener;
  }

  public ForwardedPortEventListener getForwardedPortEventListenerListener() {
    return forwardedPortEventListener;
  }
}
