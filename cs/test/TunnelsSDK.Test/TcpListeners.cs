using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.DevTunnels.Management;

namespace Microsoft.DevTunnels.Test;
public sealed class TcpListeners : IAsyncDisposable
{
    private const int MaxAttempts = 10;

    private readonly TraceSource trace;
    private readonly CancellationTokenSource cts = new();
    private readonly List<TcpListener> listeners = new();
    private readonly List<Task> listenerTasks = new();

    public TcpListeners(int count, TraceSource trace)
    {
        Requires.Argument(count > 0, nameof(count), "Count must be greater than 0.");
        this.trace = trace.WithName("TcpListeners");
        Ports = new int[count];
        for (int index = 0; index < count; index++)
        {
            TcpListener listener = null;
            int port;
            int attempt = 0;
            while (true)
            {
                try
                {
                    port = TcpUtils.GetAvailableTcpPort(canReuseAddress: false);
                    listener = new TcpListener(IPAddress.Loopback, port);
                    listener.Start();
                    break;
                }
                catch (SocketException ex)
                {
                    listener?.Stop();
                    if (++attempt >= MaxAttempts)
                    {
                        throw new InvalidOperationException("Failed to find available port", ex);
                    }
                }
                catch
                {
                    listener?.Stop();
                    throw;
                }
            }

            Ports[index] = port;
            this.listeners.Add(listener);
            this.listenerTasks.Add(AcceptConnectionsAsync(listener, port));
        }

        this.trace.Info("Listening on ports: {0}", string.Join(", ", Ports));
    }

    public int Port { get; }

    public int[] Ports { get; }

    public async ValueTask DisposeAsync()
    {
        cts.Cancel();
        StopListeners();
        await Task.WhenAll(this.listenerTasks);
        this.listenerTasks.Clear();
    }

    private async Task AcceptConnectionsAsync(TcpListener listener, int port)
    {
        var tasks = new List<Task>();
        TaskCompletionSource allTasksCompleted = null;
        try
        {
            while (!cts.IsCancellationRequested)
            {
                var tcpClient = await listener.AcceptTcpClientAsync(cts.Token);
                var task = Task.Run(() => RunClientAsync(tcpClient, port));
                lock (tasks)
                {
                    tasks.Add(task);
                }

                _ = task.ContinueWith(
                    (t) =>
                    {
                        lock (tasks)
                        {
                            tasks.Remove(t);
                            if (tasks.Count == 0)
                            {
                                allTasksCompleted?.TrySetResult();
                            }
                        }
                    });
            }
        }
        catch (OperationCanceledException) when (this.cts.IsCancellationRequested)
        {
            // Ignore
        }
        catch (SocketException) when (this.cts.IsCancellationRequested)
        {
            // Ignore
        }
        catch (Exception ex)
        {
            this.trace.Error($"Error accepting TCP client for port {port}: ${ex}");
        }

        lock (tasks)
        {
            if (tasks.Count == 0)
            {
                return;
            }

            allTasksCompleted = new TaskCompletionSource();
        }

        await allTasksCompleted.Task;
    }

    private async Task RunClientAsync(TcpClient tcpClient, int port)
    {
        try
        {
            using var disposable = tcpClient;

            this.trace.Info($"Accepted client connection to TCP port {port}");
            await using var stream = tcpClient.GetStream();

            var bytes = Encoding.UTF8.GetBytes(port.ToString(CultureInfo.InvariantCulture));
            await stream.WriteAsync(bytes);

        }
        catch (OperationCanceledException) when (this.cts.IsCancellationRequested)
        {
            // Ignore
        }
        catch (SocketException) when (this.cts.IsCancellationRequested)
        {
            // Ignore
        }
        catch (Exception ex)
        {
            this.trace.Error($"Error handling TCP client on listener running on port {port}: ${ex}");
        }
    }

    private void StopListeners()
    {
        foreach (var listener in this.listeners)
        {
            listener.Stop();
        }

        this.listeners.Clear();
    }
}
