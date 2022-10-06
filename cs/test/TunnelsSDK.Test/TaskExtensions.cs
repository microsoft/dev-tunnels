using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DevTunnels.Test;

internal static class TaskExtensions
{
    public static async Task WithTimeout(this Task t, TimeSpan timeout)
    {
        // Don't timeout when a debugger is attached.
        if (Debugger.IsAttached)
        {
            await t;
            return;
        }

        Task returnedTask = await Task.WhenAny(t, Task.Delay(timeout));
        if (returnedTask != t)
        {
            throw new TimeoutException();
        }

        await t;
    }

    public static async Task<T> WithTimeout<T>(this Task<T> t, TimeSpan timeout)
    {
        // Don't timeout when a debugger is attached.
        if (Debugger.IsAttached)
        {
            return await t;
        }

        Task returnedTask = await Task.WhenAny(t, Task.Delay(timeout));
        if (returnedTask != t)
        {
            throw new TimeoutException();
        }

        return await t;
    }

    public static async Task WaitUntil(
        Func<bool> condition,
        CancellationToken cancellation = default)
    {
        while (!condition())
        {
            await Task.Delay(5, cancellation);
        }
    }

    public static async Task WaitUntil(
        Func<Task<bool>> condition,
        CancellationToken cancellation = default)
    {
        while (!(await condition()))
        {
            await Task.Delay(5, cancellation);
        }
    }

}
