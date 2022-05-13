// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

package com.microsoft.tunnels.management;

/**
 * Runtime Exception thrown on failed http requests. Contains the response
 * statusCode.
 */
public class HttpResponseException extends RuntimeException {
  public int statusCode;
  public String responseBody;

  public HttpResponseException(String message, int statusCode) {
    super(message);
    this.statusCode = statusCode;
  }

  public HttpResponseException(String message, int statusCode, Throwable cause) {
    super(message, cause);
    this.statusCode = statusCode;
  }

  /**
   * Exception thrown for http response with status code > 300.
   */
  public HttpResponseException(
      String message,
      int statusCode,
      String responseBody) {
    super(message);
    this.statusCode = statusCode;
    this.responseBody = responseBody;
  }
}
