/**
 * Structure of error details returned by the tunnel service, including validation errors.
 */
export interface ProblemDetails {
    /**
     * Gets or sets the error title.
     */
    title?: string;

    /**
     * Gets or sets the error detail.
     */
    detail?: string[];

    /**
     * Gets or sets additional details about individual request properties.
     */
    error?: {};
}
