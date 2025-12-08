using Microsoft.VisualStudio.TestTools.UnitTesting;
using RFiDGear.Services;
using System;
using System.Threading.Tasks;

namespace RFiDGear.Tests.Services
{
    [TestClass]
    public class UpdateNotifierTests
    {
        [TestMethod]
        public async Task StartUpdateCheck_InvokesCallbackWhenUpdateAvailable()
        {
            var fakeUpdater = new FakeUpdaterAdapter { UpdateAvailable = true };
            var fakeTimerFactory = new FakeTimerFactory();
            var notifier = new UpdateNotifier(fakeUpdater, fakeTimerFactory);
            var called = false;

            notifier.StartUpdateCheck(() =>
            {
                called = true;
                return Task.CompletedTask;
            });

            await fakeTimerFactory.Timer.TriggerAsync().ConfigureAwait(false);

            Assert.IsTrue(called);
        }

        private class FakeUpdaterAdapter : IUpdaterAdapter
        {
            public bool UpdateAvailable { get; set; }

            public string UpdateInfoText { get; set; } = string.Empty;

            public bool AllowUpdate { get; set; }

            public Task ApplyUpdateAsync()
            {
                return Task.CompletedTask;
            }

            public Task StartMonitoringAsync()
            {
                return Task.CompletedTask;
            }

            public Task StopMonitoringAsync()
            {
                return Task.CompletedTask;
            }
        }

        private class FakeTimerFactory : ITimerFactory
        {
            public FakeTimerAdapter Timer { get; private set; }

            public ITimerAdapter Create(System.Threading.TimerCallback callback, int dueTime, int period)
            {
                Timer = new FakeTimerAdapter(callback);
                return Timer;
            }
        }

        private class FakeTimerAdapter : ITimerAdapter
        {
            private readonly System.Threading.TimerCallback callback;

            public FakeTimerAdapter(System.Threading.TimerCallback callback)
            {
                this.callback = callback;
            }

            public void Change(int dueTime, int period)
            {
            }

            public void Dispose()
            {
            }

            public Task TriggerAsync()
            {
                callback(null);
                return Task.CompletedTask;
            }
        }
    }
}
