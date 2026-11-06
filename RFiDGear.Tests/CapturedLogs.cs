using System;
using System.Collections.Generic;
using System.Linq;

using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace RFiDGear.Tests
{
    /// <summary>
    /// Replaces the global Serilog logger with one that records every event, and restores the previous
    /// logger when disposed. Tests run one at a time (see <c>AssemblyInfo.cs</c>), so swapping the global
    /// logger is safe.
    /// </summary>
    internal sealed class CapturedLogs : IDisposable
    {
        private readonly ILogger _previous;
        private readonly CollectingSink _sink = new CollectingSink();

        private CapturedLogs()
        {
            _previous = Log.Logger;
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.Sink(_sink)
                .CreateLogger();
        }

        /// <summary>Starts capturing; dispose the result to restore the previous logger.</summary>
        public static CapturedLogs Install() => new CapturedLogs();

        /// <summary>Events written by the class whose name is <paramref name="sourceTypeName"/>.</summary>
        public IReadOnlyList<LogEvent> From(string sourceTypeName) =>
            _sink.Snapshot()
                .Where(e => e.Properties.TryGetValue("SourceContext", out var source)
                    && source.ToString().Contains(sourceTypeName, StringComparison.Ordinal))
                .ToList();

        /// <summary>
        /// Everything a log sink could write for the event: the rendered message, every property value,
        /// and the attached exception (type, message, and stack trace) if there is one.
        /// </summary>
        public static string EverythingWrittenFor(LogEvent logEvent) =>
            string.Join(
                " | ",
                new[] { logEvent.RenderMessage() }
                    .Concat(logEvent.Properties.Select(p => $"{p.Key}={p.Value}"))
                    .Concat(new[] { logEvent.Exception?.ToString() ?? string.Empty }));

        public void Dispose()
        {
            (Log.Logger as IDisposable)?.Dispose();
            Log.Logger = _previous;
        }

        private sealed class CollectingSink : ILogEventSink
        {
            private readonly List<LogEvent> _events = new List<LogEvent>();

            public void Emit(LogEvent logEvent)
            {
                lock (_events)
                {
                    _events.Add(logEvent);
                }
            }

            public IReadOnlyList<LogEvent> Snapshot()
            {
                lock (_events)
                {
                    return _events.ToList();
                }
            }
        }
    }
}
