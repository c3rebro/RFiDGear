using Microsoft.VisualStudio.TestTools.UnitTesting;
using RFiDGear.Services;
using System;
using System.Threading;

namespace RFiDGear.Tests.Services
{
    [TestClass]
    public class PollingSchedulerTests
    {
        [TestMethod]
        public void PauseAndResume_ForwardToTimers()
        {
            var factory = new FakeTimerFactory();
            using (var scheduler = new PollingScheduler(factory))
            {
                scheduler.PauseReader();
                scheduler.ResumeReader();
            }

            Assert.AreEqual(1, factory.ReaderTimer.PauseCount);
            Assert.AreEqual(1, factory.ReaderTimer.ResumeCount);
        }

        [TestMethod]
        public void SchedulerRaisesCallbacks()
        {
            var factory = new FakeTimerFactory();
            using (var scheduler = new PollingScheduler(factory))
            {
                var updateCount = 0;
                var readerCount = 0;
                scheduler.OnUpdateRequested += _ => updateCount++;
                scheduler.OnReaderRequested += _ => readerCount++;

                factory.TriggerUpdate();
                factory.TriggerReader();

                Assert.AreEqual(1, updateCount);
                Assert.AreEqual(1, readerCount);
            }
        }

        private class FakeTimerFactory : ITimerFactory
        {
            public FakeTimerAdapter UpdateTimer { get; private set; }
            public FakeTimerAdapter ReaderTimer { get; private set; }
            private TimerCallback updateCallback;
            private TimerCallback readerCallback;

            public ITimerAdapter Create(TimerCallback callback, int dueTime, int period)
            {
                if (updateCallback == null)
                {
                    updateCallback = callback;
                    UpdateTimer = new FakeTimerAdapter();
                    return UpdateTimer;
                }

                readerCallback = callback;
                ReaderTimer = new FakeTimerAdapter();
                return ReaderTimer;
            }

            public void TriggerUpdate()
            {
                updateCallback?.Invoke(null);
            }

            public void TriggerReader()
            {
                readerCallback?.Invoke(null);
            }
        }

        private class FakeTimerAdapter : ITimerAdapter
        {
            public int PauseCount { get; private set; }
            public int ResumeCount { get; private set; }

            public void Change(int dueTime, int period)
            {
                if (dueTime == Timeout.Infinite)
                {
                    PauseCount++;
                }
                else
                {
                    ResumeCount++;
                }
            }

            public void Dispose()
            {
            }
        }
    }
}
