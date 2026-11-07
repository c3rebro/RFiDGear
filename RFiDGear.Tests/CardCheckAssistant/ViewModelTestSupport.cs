using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

using RFiDGear.Extensions.CardCheckAssistant.Services;
using RFiDGear.Extensions.CardCheckAssistant.ViewModels;

namespace RFiDGear.Tests.CardCheckAssistant
{
    /// <summary>A picker that answers what the test says, and remembers how it was asked.</summary>
    internal sealed class FakeFileDialogService : IFileDialogService
    {
        public string? Answer { get; set; }

        public List<(string? Current, string Title, string Filter)> Requests { get; } = new List<(string?, string, string)>();

        public string? PickProjectFile(string? currentPath, string title, string filter)
        {
            Requests.Add((currentPath, title, filter));
            return Answer;
        }

        public string? FolderAnswer { get; set; }

        public List<(string? Current, string Title)> FolderRequests { get; } = new List<(string?, string)>();

        public string? PickFolder(string? currentPath, string title)
        {
            FolderRequests.Add((currentPath, title));
            return FolderAnswer;
        }
    }

    /// <summary>
    /// A checker that waits to be released and remembers which thread it ran on, to show that the checks happen off the
    /// UI thread and that the window stays responsive meanwhile.
    /// </summary>
    internal sealed class BlockingPathChecker : IPathChecker, IDisposable
    {
        private readonly ManualResetEventSlim _release = new ManualResetEventSlim(false);
        private int _started;
        private int _threadId = -1;

        public bool Started => Volatile.Read(ref _started) == 1;

        public int ThreadId => Volatile.Read(ref _threadId);

        public void Release() => _release.Set();

        public void Dispose() => _release.Dispose();

        public ProjectPathCheck CheckProject(string? path)
        {
            Volatile.Write(ref _threadId, Environment.CurrentManagedThreadId);
            Volatile.Write(ref _started, 1);
            _release.Wait(TimeSpan.FromSeconds(10));
            return ProjectPathCheck.Valid;
        }

        public ReportTargetCheck CheckReportTarget(string? target) => ReportTargetCheck.None;
    }

    /// <summary>A folder with a project file in it and room for a report, removed when disposed.</summary>
    internal sealed class TempArea : IDisposable
    {
        public TempArea()
        {
            Directory = System.IO.Directory.CreateTempSubdirectory("CardCheckAssistant").FullName;
            ProjectPath = Path.Combine(Directory, "check.rfPrj");
            File.WriteAllText(ProjectPath, "not read by the fake host");
            ReportPath = Path.Combine(Directory, "report.pdf");
        }

        public string Directory { get; }

        public string ProjectPath { get; }

        public string ReportPath { get; }

        public void Dispose()
        {
            try
            {
                System.IO.Directory.Delete(Directory, recursive: true);
            }
            catch (IOException)
            {
                // Leave it to the operating system's temp cleanup.
            }
        }
    }

    internal static class ViewModelTestSupport
    {
        /// <summary>A view model for the calling thread's dispatcher; call on the STA test thread.</summary>
        public static CardCheckAssistantViewModel Create(RFiDGear.Contracts.IRFiDGearExtensionHost host, IFileDialogService? dialogs = null) =>
            new CardCheckAssistantViewModel(host, Dispatcher.CurrentDispatcher, dialogs ?? new FakeFileDialogService());

        /// <summary>Lets everything already queued on the dispatcher run, including work the host's events queued.</summary>
        public static async Task Flush() => await Dispatcher.Yield(DispatcherPriority.Background);

        public static TaskCompletionSource<bool> NewGate() =>
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// Lets the dispatcher keep working until a condition holds. For the real host, whose load runs on another thread,
        /// where a fixed number of yields would be a guess.
        /// </summary>
        public static async Task WaitUntil(Func<bool> condition, string what, int timeoutMilliseconds = 10000)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMilliseconds);
            while (!condition())
            {
                if (DateTime.UtcNow > deadline)
                {
                    throw new TimeoutException("Gave up waiting for: " + what);
                }

                await Task.Delay(10);
            }
        }

        /// <summary>
        /// Deletes a file without letting a failure to do so hide the failure of the test that was using it: a run that
        /// went wrong may still have the file open.
        /// </summary>
        public static void TryDelete(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
                // The operating system's temp cleanup gets it later.
            }
        }
    }
}
