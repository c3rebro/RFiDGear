using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using RFiDGear.Infrastructure.FileAccess;
using Xunit;

namespace RFiDGear.Tests
{
    public class DatabaseReaderWriterTests
    {
        [Fact]
        public async Task ExecuteOnStaThread_WhenApplicationIsNull_UsesStaThread()
        {
            var readerWriter = new DatabaseReaderWriter(new ProjectManager());
            var method = typeof(DatabaseReaderWriter).GetMethod(
                "ExecuteOnStaThread",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(method);

            ApartmentState? observedState = null;

            await Task.Run(() =>
            {
                method.Invoke(readerWriter, new object[]
                {
                    (Action)(() => observedState = Thread.CurrentThread.GetApartmentState())
                });
            });

            Assert.Equal(ApartmentState.STA, observedState);
        }
    }
}
