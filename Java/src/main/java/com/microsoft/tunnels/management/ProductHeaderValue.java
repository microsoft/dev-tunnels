package com.microsoft.tunnels.management;

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
