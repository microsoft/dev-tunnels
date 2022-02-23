package com.microsoft.tunnels.management;

public enum HttpMethod {
    GET("GET"),
    POST("POST"),
    PUT("PUT"),
    DELETE("DELETE");

    private final String stringValue;

    HttpMethod(final String s) {
        stringValue = s;
    }

    public String toString() {
        return stringValue;
    }
}
