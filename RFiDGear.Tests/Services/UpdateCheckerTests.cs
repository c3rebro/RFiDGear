using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RFiDGear.Services;

namespace RFiDGear.Tests.Services
{
    [TestClass]
    public class UpdateCheckerTests
    {
        [TestMethod]
        public void StartAndStopMonitoring_ForwardsToAdapter()
        {
            var adapter = new FakeUpdaterAdapter();
            var checker = new UpdateChecker(adapter);

            checker.StartMonitoringAsync().Wait();
            checker.StopMonitoringAsync().Wait();

            Assert.AreEqual(1, adapter.StartCount);
            Assert.AreEqual(1, adapter.StopCount);
        }

        [TestMethod]
        public void ApplyUpdate_UsesAdapter()
        {
            var adapter = new FakeUpdaterAdapter();
            var checker = new UpdateChecker(adapter);

            checker.ApplyUpdateAsync().Wait();

            Assert.AreEqual(1, adapter.UpdateCount);
        }

        [TestMethod]
        public void AllowUpdate_Passthrough()
        {
            var adapter = new FakeUpdaterAdapter();
            var checker = new UpdateChecker(adapter)
            {
                AllowUpdate = false
            };

            Assert.IsFalse(adapter.AllowUpdate);
            checker.AllowUpdate = true;
            Assert.IsTrue(adapter.AllowUpdate);
        }

        private class FakeUpdaterAdapter : IUpdaterAdapter
        {
            public int StartCount { get; private set; }
            public int StopCount { get; private set; }
            public int UpdateCount { get; private set; }
            public bool UpdateAvailable { get; set; }
            public string UpdateInfoText { get; set; }
            public bool AllowUpdate { get; set; }

            public Task ApplyUpdateAsync()
            {
                UpdateCount++;
                return Task.CompletedTask;
            }

            public Task StartMonitoringAsync()
            {
                StartCount++;
                return Task.CompletedTask;
            }

            public Task StopMonitoringAsync()
            {
                StopCount++;
                return Task.CompletedTask;
            }
        }
    }
}
