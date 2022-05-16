// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

package com.microsoft.tunnels.management;

/**
 * Represents the User-Agent header value in the form of productName/version.
 */
public class ProductHeaderValue {
  public String productName;
  public String version;

  public ProductHeaderValue(String productName) {
    this(productName, "unknown");
  }

  public ProductHeaderValue(String productName, String version) {
    this.productName = productName;
    this.version = version;
  }
}
