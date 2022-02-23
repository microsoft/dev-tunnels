package com.microsoft.tunnels.management;

/**
 * Runtime Exception thrown on failed http requests. Contains the response statusCode.
 */
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
