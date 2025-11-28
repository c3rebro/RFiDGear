using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RFiDGear.Services;

namespace RFiDGear.Tests.Services
{
    [TestClass]
    public class MainWindowTimerFactoryTests
    {
        [TestMethod]
        public void CreateTriggerReadTimer_ConfiguresTimer()
        {
            var factory = new MainWindowTimerFactory();

            var timer = factory.CreateTriggerReadTimer((_, __) => { });

            Assert.AreEqual(new TimeSpan(0, 0, 0, 2, 500), timer.Interval);
            Assert.IsFalse(timer.IsEnabled);
            Assert.AreEqual(false, timer.Tag);
        }

        [TestMethod]
        public void CreateTaskTimeoutTimer_ConfiguresTimer()
        {
            var factory = new MainWindowTimerFactory();

            var timer = factory.CreateTaskTimeoutTimer((_, __) => { });

#if DEBUG
            Assert.AreEqual(new TimeSpan(0, 1, 0, 0, 0), timer.Interval);
#else
            Assert.AreEqual(new TimeSpan(0, 0, 0, 4, 0), timer.Interval);
#endif
            Assert.IsFalse(timer.IsEnabled);
        }
    }
}
