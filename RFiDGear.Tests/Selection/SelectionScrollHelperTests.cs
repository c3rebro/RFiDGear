using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RFiDGear.Helpers.Selection;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace RFiDGear.Tests.Selection
{
    [TestClass]
    public class SelectionScrollHelperTests
    {
        [TestMethod]
        public void ScrollSelection_RunsScrollWhenItemSelected()
        {
            var scrollCount = 0;

            SelectionScrollHelper.ScrollSelection(() => new object(), () => scrollCount++, null);

            Assert.AreEqual(1, scrollCount);
        }

        [TestMethod]
        public void ScrollSelection_LogsAndSwallowsErrors()
        {
            var sink = new CollectingSink();
            ILogger logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Sink(sink)
                .CreateLogger();

            SelectionScrollHelper.ScrollSelection(() => new object(), () => throw new InvalidOperationException("boom"), logger);

            Assert.AreEqual(1, sink.Events.Count);
            Assert.AreEqual(LogEventLevel.Warning, sink.Events[0].Level);
        }

        private class CollectingSink : ILogEventSink
        {
            public List<LogEvent> Events { get; } = new List<LogEvent>();

            public void Emit(LogEvent logEvent)
            {
                Events.Add(logEvent);
            }
        }
    }
}
