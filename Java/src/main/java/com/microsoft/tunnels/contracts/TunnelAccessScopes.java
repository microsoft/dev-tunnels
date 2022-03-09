package com.microsoft.tunnels.contracts;

import com.google.gson.annotations.Expose;

import org.apache.maven.shared.utils.StringUtils;

import java.util.ArrayList;
import java.util.Collection;

/**
 * TunnelAccessScopes.
 */
public class TunnelAccessScopes {
  @Expose
  public static String Manage = "manage";
  @Expose
  public static String Host = "host";
  @Expose
  public static String Inspect = "inspect";
  @Expose
  public static String Connect = "connect";
  @Expose
  public static ArrayList<String> All = new ArrayList<String>() {
    {
      add(Manage);
      add(Host);
      add(Inspect);
      add(Connect);
    }
  };

  /**
   * Checks that the set of scopes matches the given validation set.
   */
  public static void validate(
      Collection<String> scopes,
      Collection<String> validScopes) {
    if (scopes == null) {
      throw new IllegalArgumentException("scopes must not be null");
    }
    scopes.forEach(scope -> {
      if (StringUtils.isBlank(scope)) {
        throw new IllegalArgumentException("Tunnel access scopes include a null/empty item.");
      } else if (!TunnelAccessScopes.All.contains(scope)) {
        throw new IllegalArgumentException("Invalid tunnel access scope: " + scope);
      }
    });

    if (validScopes != null) {
      scopes.forEach(scope -> {
        if (!validScopes.contains(scope)) {
          throw new IllegalArgumentException(
              "Tunnel access scope is invalid for current request: " + scope);
        }
      });
    }
  }

}
