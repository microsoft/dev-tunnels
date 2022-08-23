// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import { CancellationToken, CancellationError } from '@vs/vs-ssh';
import { Disposable } from 'vscode-jsonrpc';

export class List {
    public static groupBy<T, K>(
        list: { forEach(f: (item: T) => void): void },
        keyGetter: (item: T) => K,
    ): Map<K, T[]> {
        const map = new Map();
        list.forEach((item: T) => {
            const key = keyGetter(item);
            const collection = map.get(key);
            if (!collection) {
                map.set(key, [item]);
            } else {
                collection.push(item);
            }
        });
        return map;
    }
}

/**
 * Resolves the promise after {@link milliseconds} have passed, or reject if {@link cancellation} is canceled.
 */
export function delay(milliseconds: number, cancellation?: CancellationToken): Promise<void> {
    return new Promise<void>((resolve, reject) => {
        let cancellationDisposable: Disposable | undefined;
        let timeout: NodeJS.Timeout | undefined;

        if (cancellation) {
            if (cancellation.isCancellationRequested) {
                reject(new CancellationError());
                return;
            }

            cancellationDisposable = cancellation.onCancellationRequested(() => {
                if (timeout) {
                    clearTimeout(timeout);
                }

                cancellationDisposable?.dispose();
                reject(new CancellationError());
            });
        }

        timeout = setTimeout(() => {
            cancellationDisposable?.dispose();
            resolve();
        }, milliseconds);
    });
}

/**
 * Checks if {@link e} is {@link CancellationError} and {@link cancellation} has been requested.
 */
export function isCancellation(e: any, cancellation?: CancellationToken): e is CancellationError {
    return (
        e &&
        typeof e === 'object' &&
        e instanceof CancellationError &&
        (!cancellation || cancellation.isCancellationRequested)
    );
}

/**
 * Checks if {@link e} is {@link Error}.
 */
export function isError(e: any): e is Error {
    return e && typeof e === 'object' && e instanceof Error;
}

/**
 * Gets the error message.
 */
export function getErrorMessage(e: any) {
    return String(e?.message ?? e);
}

/**
 * Wraps e in Error object if e is not Error. If e is Error, returns e as is.
 */
export function getError(e: any, messagePrefix?: string): Error {
    return isError(e) ? e : new Error(`${messagePrefix ?? ''}${getErrorMessage(e)}`);
}

/**
 * Races a promise and cancellation.
 */
export function withCancellation<T>(
    promise: Promise<T>,
    cancellation?: CancellationToken,
): Promise<T> {
    if (!cancellation) {
        return promise;
    }

    return Promise.race([
        promise,
        new Promise<T>((resolve, reject) => {
            if (cancellation.isCancellationRequested) {
                reject(new CancellationError());
            } else {
                cancellation.onCancellationRequested(() => {
                    reject(new CancellationError());
                });
            }
        }),
    ]);
}
