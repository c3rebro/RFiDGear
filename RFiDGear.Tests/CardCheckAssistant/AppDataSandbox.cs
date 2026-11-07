using System;
using System.IO;
using System.Runtime.CompilerServices;

using RFiDGear.Extensions.CardCheckAssistant.Services;

namespace RFiDGear.Tests.CardCheckAssistant
{
    /// <summary>
    /// The Card Check Assistant reads and writes settings below the user's application data and looks there for the old
    /// assistant's files. A test must never do that on the machine of whoever runs it (a developer who has the old
    /// assistant installed would be shown its import question in the middle of a test run), so for the whole run the page's
    /// data folder is a temporary one.
    /// </summary>
    internal static class AppDataSandbox
    {
        /// <summary>The temporary folder of this test run.</summary>
        internal static string Root { get; private set; } = string.Empty;

        [ModuleInitializer]
        internal static void Install()
        {
            Root = Path.Combine(Path.GetTempPath(), "rfidgear-tests-appdata-" + Guid.NewGuid().ToString("N"));
            Environment.SetEnvironmentVariable(AssistantServicesFactory.AppDataOverrideVariable, Root);
            AppDomain.CurrentDomain.ProcessExit += (_, _) => Remove(Root);
        }

        private static void Remove(string root)
        {
            try
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                // Left to the operating system's temp cleanup.
            }
        }
    }
}
