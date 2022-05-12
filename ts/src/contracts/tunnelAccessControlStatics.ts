// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import { TunnelAccessScopes } from './tunnelAccessScopes';

const allScopes = [
    TunnelAccessScopes.Manage,
    TunnelAccessScopes.Host,
    TunnelAccessScopes.Inspect,
    TunnelAccessScopes.Connect,
];

/**
 * Checks that all items in an array of scopes are valid.
 * @param scopes List of scopes to validate.
 * @param validScopes Optional subset of scopes to be considered valid;
 * if omitted then all defined scopes are valid.
 * @throws Error if a scope is not valid.
 */
export function validateScopes(scopes: string[], validScopes?: string[]): void {
    if (!Array.isArray(scopes)) {
        throw new TypeError('A scopes array was expected.');
    }

    scopes.forEach((scope) => {
        if (!scope) {
            throw new Error('Tunnel access scopes include a null/empty item.');
        } else if (!allScopes.includes(<TunnelAccessScopes>scope)) {
            throw new Error('Invalid tunnel access scope: ' + scope);
        }
    });

    if (Array.isArray(validScopes)) {
        scopes.forEach((scope) => {
            if (!validScopes.includes(scope)) {
                throw new Error('Tunnel access scope is invalid for current request: scope');
            }
        });
    }
}
