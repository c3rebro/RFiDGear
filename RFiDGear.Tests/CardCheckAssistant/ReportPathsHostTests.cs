using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using RFiDGear.Contracts;
using RFiDGear.ExtensionLibraries.CardCheck;
using RFiDGear.Infrastructure.ExtensionHost;
using RFiDGear.Infrastructure.FileAccess;
using RFiDGear.Tests.CardCheck;

using Xunit;

namespace RFiDGear.Tests.CardCheckAssistant
{
    /// <summary>
    /// The paths of the reports that the library makes, against the rule of the real host for <c>REPORTTARGETPATH</c>: a
    /// path the library hands out must be one the host accepts, whatever the job and chip numbers look like.
    /// </summary>
    [Collection("AppDataPath")]
    public sealed class ReportPathsHostTests : IDisposable
    {
        private readonly TempFolder _root = new TempFolder();
        private readonly RecordingCardCheckLog _log = new RecordingCardCheckLog();

        public void Dispose() => _root.Dispose();

        private static async Task<bool> HostRefuses(string target)
        {
            using var host = new HeadlessExtensionHost(new DatabaseReaderWriter(), new CapturingExecService(), string.Empty);
            var request = new ExecutionRequest(new[] { new KeyValuePair<string, string>("REPORTTARGETPATH", target) });

            try
            {
                await host.Execution.ExecuteAllAsync(request);
                return false;
            }
            catch (ArgumentException)
            {
                return true;
            }
        }

        [Theory]
        [InlineData("KP-000005", "5")]
        [InlineData("J 7", "C 9")]
        [InlineData("A/B", "C?D")]
        [InlineData("J:1", "C*2")]
        [InlineData("ÄÖ", "ß")]
        [InlineData("com10", "aux1")]
        public async Task Resolve_EveryPathIsAcceptedByTheRealHost(string job, string chip)
        {
            var files = CardCheckServices.CreateReportFiles(_log);

            foreach (var subfolder in new[] { true, false })
            {
                var paths = files.Resolve(new ReportLocation(_root.Path, subfolder), job, chip).Value;
                Assert.True(files.EnsureOrderFolder(paths).IsSuccess);

                foreach (var file in Enum.GetValues<ReportFile>())
                {
                    Assert.False(await HostRefuses(paths.PathOf(file)), file + " of " + job + "/" + chip + " is refused by the host");
                }
            }
        }
    }
}
