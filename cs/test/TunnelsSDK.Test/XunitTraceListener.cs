using System.Diagnostics;
using System.Text;
using Xunit.Abstractions;

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
        this.output.WriteLine($"{messageTime} {this.currentLine}{message}");
        this.currentLine.Clear();
        this.messageStart = null;
    }
}