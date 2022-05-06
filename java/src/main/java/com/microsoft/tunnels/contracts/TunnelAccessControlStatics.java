package com.microsoft.tunnels.contracts;

import java.util.Arrays;
import java.util.Collection;

import org.apache.maven.shared.utils.StringUtils;

class TunnelAccessControlStatics {
  static void validateScopes(
      Collection<String> scopes,
      Collection<String> validScopes) {
    if (scopes == null) {
      throw new IllegalArgumentException("scopes must not be null");
    }
    var allScopes = Arrays.asList(new String[] {
        TunnelAccessScopes.connect,
        TunnelAccessScopes.create,
        TunnelAccessScopes.host,
        TunnelAccessScopes.inspect,
        TunnelAccessScopes.manage });
    scopes.forEach(scope -> {
      if (StringUtils.isBlank(scope)) {
        throw new IllegalArgumentException("Tunnel access scopes include a null/empty item.");
      } else if (!allScopes.contains(scope)) {
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
