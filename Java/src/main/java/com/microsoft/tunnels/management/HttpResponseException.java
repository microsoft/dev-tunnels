package com.microsoft.tunnels.management;

public class HttpResponseException extends RuntimeException {
    public int statusCode;
    public HttpResponseException(String message, int statusCode) {
        super(message);
        this.statusCode = statusCode;
    }
    public HttpResponseException(String message, int statusCode, Throwable cause) {
        super(message, cause);
        this.statusCode = statusCode;
    }
}
