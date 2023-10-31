// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import { CancellationToken, CancellationError } from '@microsoft/dev-tunnels-ssh';
import { Disposable, Emitter } from 'vscode-jsonrpc';

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
        let timeout: NodeJS.Timeout | undefined = undefined;

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
 * Gets the error message.
 */
export function getErrorMessage(e: any) {
    return String(e?.message ?? e);
}

/**
 * Wraps e in Error object if e is not Error. If e is Error, returns e as is.
 */
export function getError(e: any, messagePrefix?: string): Error {
    return e instanceof Error ? e : new Error(`${messagePrefix ?? ''}${e}`);
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

/**
 * Tracking event emitter.
 */
export class TrackingEmitter<T> extends Emitter<T> {
    private subscribed = false;
    public constructor () {
        super({
            onFirstListenerAdd: () => this.subscribed = true,
            onLastListenerRemove: () => this.subscribed = false,
        }); 
    }

    /**
     * A value indicating whether there event handlers subscribed to the event emitter.
     */
    public get isSubscribed(): boolean {
        return this.subscribed; 
    }
}
