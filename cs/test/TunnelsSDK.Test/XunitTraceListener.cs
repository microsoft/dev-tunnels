using System.Diagnostics;
using System.Text;
using Xunit;

namespace Microsoft.DevTunnels.Test;

internal sealed class XunitTraceListener : TraceListener
{
    private readonly ITestOutputHelper output;
    private readonly StringBuilder currentLine = new ();
    private readonly DateTimeOffset loggingStart = DateTimeOffset.UtcNow;
    private DateTimeOffset? messageStart;

    public XunitTraceListener(ITestOutputHelper output)
    {
        this.output = output;
    }

    public override void Write(string message)
    {
        this.messageStart ??= DateTimeOffset.UtcNow;
        this.currentLine.Append(message);
    }

    public override void WriteLine(string message)
    {
        var messageTime = (this.messageStart ?? DateTimeOffset.UtcNow) - this.loggingStart;
        try
        {
            this.output.WriteLine($"{messageTime} {this.currentLine}{message}");
        }
        catch (InvalidOperationException)
        {
            // Ignore writes that arrive after the test has ended. Background SSH tasks
            // may still emit trace output after xUnit's TestOutputHelper is no longer
            // associated with an active test, which would otherwise crash the test host.
        }

        this.currentLine.Clear();
        this.messageStart = null;
    }
}