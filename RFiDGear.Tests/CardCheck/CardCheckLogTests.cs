using System;
using System.Collections.Generic;
using System.Linq;

using RFiDGear.ExtensionLibraries.CardCheck;

using Serilog;
using Serilog.Core;
using Serilog.Events;

using Xunit;

namespace RFiDGear.Tests.CardCheck
{
    /// <summary>
    /// The adapter between the library's log abstraction and the host's Serilog pipeline.
    /// </summary>
    [Collection("AppDataPath")]
    public sealed class CardCheckLogTests
    {
        private const string SecretServer = "SECRET-SERVER-4711";
        private const string SecretPassword = "hunter2-PASSWORD";

        private sealed class ListSink : ILogEventSink
        {
            public List<LogEvent> Events { get; } = new List<LogEvent>();

            public void Emit(LogEvent logEvent) => Events.Add(logEvent);
        }

        private static (ICardCheckLog Log, ListSink Sink) CreateLogWithOwnSink()
        {
            var sink = new ListSink();
            var logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(sink).CreateLogger();

            return (new SerilogCardCheckLog(() => logger), sink);
        }

        private static string EverythingWritten(LogEvent logEvent) => CapturedLogs.EverythingWrittenFor(logEvent);

        private static Exception ThrownWithSecrets()
        {
            try
            {
                var exception = new InvalidOperationException($"Cannot connect to {SecretServer} as sa with {SecretPassword}");
                exception.Data["connection"] = $"Server={SecretServer};Password={SecretPassword}";
                throw exception;
            }
            catch (InvalidOperationException caught)
            {
                return caught;
            }
        }

        [Theory]
        [InlineData("information", LogEventLevel.Information)]
        [InlineData("warning", LogEventLevel.Warning)]
        [InlineData("error", LogEventLevel.Error)]
        public void EachLevel_WritesAnEventOfThatLevelWithTheRenderedMessageAndTheSourceContext(string which, LogEventLevel expected)
        {
            var (log, sink) = CreateLogWithOwnSink();

            switch (which)
            {
                case "information":
                    log.Information("Staged {Count} files for {Step}", 3, "step1");
                    break;
                case "warning":
                    log.Warning("Staged {Count} files for {Step}", 3, "step1");
                    break;
                default:
                    log.Error("Staged {Count} files for {Step}", 3, "step1");
                    break;
            }

            var logEvent = Assert.Single(sink.Events);
            Assert.Equal(expected, logEvent.Level);
            Assert.Equal("Staged 3 files for \"step1\"", logEvent.RenderMessage());
            Assert.Equal("\"CardCheck\"", logEvent.Properties[Constants.SourceContextPropertyName].ToString());
        }

        [Fact]
        public void Unexpected_WritesTheTypeTheHResultAndTheStackTrace()
        {
            var (log, sink) = CreateLogWithOwnSink();
            var exception = ThrownWithSecrets();

            log.Unexpected("order.finalize", UnexpectedFault.From(exception));

            var logEvent = Assert.Single(sink.Events);
            Assert.Equal(LogEventLevel.Error, logEvent.Level);
            var written = EverythingWritten(logEvent);
            Assert.Contains("order.finalize", written, StringComparison.Ordinal);
            Assert.Contains(typeof(InvalidOperationException).FullName!, written, StringComparison.Ordinal);
            Assert.Contains(exception.HResult.ToString(System.Globalization.CultureInfo.InvariantCulture), written, StringComparison.Ordinal);
            Assert.Contains(nameof(ThrownWithSecrets), written, StringComparison.Ordinal);
        }

        [Fact]
        public void Unexpected_NeverWritesTheMessageOrTheDataOfTheException()
        {
            var (log, sink) = CreateLogWithOwnSink();

            log.Unexpected("order.finalize", UnexpectedFault.From(ThrownWithSecrets()));

            var logEvent = Assert.Single(sink.Events);
            var written = EverythingWritten(logEvent);
            Assert.DoesNotContain(SecretServer, written, StringComparison.Ordinal);
            Assert.DoesNotContain(SecretPassword, written, StringComparison.Ordinal);
            Assert.DoesNotContain("Cannot connect", written, StringComparison.Ordinal);
            // Not attached as an exception either: a sink would write its message.
            Assert.Null(logEvent.Exception);
        }

        [Fact]
        public void UnexpectedFault_FromAnExceptionThatWasNeverThrown_HasAnEmptyStackTrace()
        {
            var fault = UnexpectedFault.From(new TimeoutException("never thrown"));

            Assert.Equal(typeof(TimeoutException).FullName, fault.TypeName);
            Assert.Equal(string.Empty, fault.StackTrace);
            Assert.Equal(new TimeoutException().HResult, fault.HResult);
        }

        [Fact]
        public void UnexpectedFault_FromNothing_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => UnexpectedFault.From(null!));
        }

        [Fact]
        public void Constructor_WithoutALoggerSource_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new SerilogCardCheckLog(null!));
        }

        [Fact]
        public void Unexpected_WithoutAFault_Throws()
        {
            var (log, _) = CreateLogWithOwnSink();

            Assert.Throws<ArgumentNullException>(() => log.Unexpected("op", null!));
        }

        [Fact]
        public void TheLoggerIsReadAtEveryCall_SoALaterPipelineIsFollowed()
        {
            var first = new ListSink();
            var second = new ListSink();
            var current = new LoggerConfiguration().WriteTo.Sink(first).CreateLogger();
            var log = new SerilogCardCheckLog(() => current);

            log.Information("one");
            current = new LoggerConfiguration().WriteTo.Sink(second).CreateLogger();
            log.Information("two");

            Assert.Equal(new[] { "one" }, first.Events.Select(e => e.RenderMessage()));
            Assert.Equal(new[] { "two" }, second.Events.Select(e => e.RenderMessage()));
        }

        [Fact]
        public void TheFactoryLog_FollowsTheHostsStaticLoggerEvenWhenItIsReplacedAfterwards()
        {
            var log = CardCheckServices.CreateLog();

            using (var firstCapture = CapturedLogs.Install())
            {
                log.Information("before the replacement {Marker}", "first");
                Assert.Single(firstCapture.From("CardCheck"));

                using (var secondCapture = CapturedLogs.Install())
                {
                    log.Information("after the replacement {Marker}", "second");

                    var second = Assert.Single(secondCapture.From("CardCheck"));
                    Assert.Contains("second", second.RenderMessage(), StringComparison.Ordinal);
                }

                Assert.Single(firstCapture.From("CardCheck"));
            }
        }
    }
}
