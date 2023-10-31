// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import { Event } from 'vscode-jsonrpc';


/**
 * Error thrown by `withTimeout()` when the promise does not resolve before the timeout duration.
 */
export class TimeoutError extends Error {
    constructor(message?: string) {
        super(message ?? 'Operation timed out.');
        (<any>this).code = 'ETIMEDOUT';
    }
}

/**
 * Returns a new promise that either resolves to the result of the original promise
 * or rejects with a `TimeoutError` if the original promise did not complete before
 * the specified timeout.
 */
export async function withTimeout<T>(promise: Promise<T>, timeoutMs: number): Promise<T> {
    // Construct the timeout error object ahead of time to capture a better stack trace.
    const timeoutError = new TimeoutError();

    let timeoutRegistration: NodeJS.Timeout | undefined;
    const result = await Promise.race([
        promise,
        new Promise<never>((_, reject) => {
            timeoutRegistration = setTimeout(() => reject(timeoutError), timeoutMs);
        }),
    ]);
    clearTimeout(timeoutRegistration!);
    return result;
}

/**
 * Returns a promise that resolves when some condition is satisfied, with an optional
 * timeout after which the promise will reject with a `TimeoutError`.
 */
export async function until(
    condition: () => boolean | Promise<boolean>,
    timeoutMs?: number,
): Promise<void> {
    const promise = new Promise<void>(async (resolve, _) => {
        while (!(await condition())) {
            await new Promise((c) => setTimeout(c, 10));
        }
        resolve();
    });
    if (typeof timeoutMs === 'number' && timeoutMs > 0) {
        await withTimeout(promise, timeoutMs);
    } else {
        await promise;
    }
}

/**
 * If the promise resolves successfully, returns `null`.
 * If the promise rejects with an error having the expected code, returns the error.
 * If the promise rejects with any other error, that error is re-thrown.
 */
export async function expectError<T>(
    promise: Promise<T>,
    code: string | string[],
): Promise<Error | null> {
    try {
        await promise;
        return null;
    } catch (e: any) {
        if (Array.isArray(code) ? code.includes(e.code) : e.code === code) {
            return e;
        } else {
            throw e;
        }
    }
}

/**
 * Subscribe and wait for event.
 */
export async function waitForEvent<T>(event: Event<T>) {
    return await new Promise<T>((resolve) => {
        const disposable = event((e) => {
            disposable.dispose();
            resolve(e);
        })
    });
}
