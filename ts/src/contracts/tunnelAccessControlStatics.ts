// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import { TunnelAccessScopes } from './tunnelAccessScopes';

const allScopes = [
    TunnelAccessScopes.Manage,
    TunnelAccessScopes.ManagePorts,
    TunnelAccessScopes.Host,
    TunnelAccessScopes.Inspect,
    TunnelAccessScopes.Connect,
];

/**
 * Checks that all items in an array of scopes are valid.
 * @param scopes List of scopes to validate.
 * @param validScopes Optional subset of scopes to be considered valid;
 * if omitted then all defined scopes are valid.
 * @param allowMultiple Whether to allow multiple space-delimited scopes in a single item.
 * Multiple scopes are supported when requesting a tunnel access token with a combination of scopes.
 * @throws Error if a scope is not valid.
 */
export function validateScopes(
    scopes: string[],
    validScopes?: string[],
    allowMultiple?: boolean,
): void {
    if (!Array.isArray(scopes)) {
        throw new TypeError('A scopes array was expected.');
    }

    if (allowMultiple) {
        scopes = scopes.map((s) => s.split(' ')).reduce((a, b) => a.concat(b), []);
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
