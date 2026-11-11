using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

        public string? SaveAnswer { get; set; }

        public string? OpenAnswer { get; set; }

        public List<string> SaveRequests { get; } = new List<string>();

        public int OpenRequests { get; set; }

        public string? PickSettingsFileToSave(string suggestedName, string title, string filter)
        {
            SaveRequests.Add(suggestedName);
            return SaveAnswer;
        }

        public string? PickSettingsFileToOpen(string title, string filter)
        {
            OpenRequests++;
            return OpenAnswer;
        }

        public string? FolderAnswer { get; set; }

        public List<(string? Current, string Title)> FolderRequests { get; } = new List<(string?, string)>();

        public string? PickFolder(string? currentPath, string title)
        {
            FolderRequests.Add((currentPath, title));
            return FolderAnswer;
        }
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

    /// <summary>Collects what WPF reports about bindings that could not be resolved.</summary>
    internal sealed class BindingErrorListener : TraceListener
    {
        private readonly List<string> _messages = new List<string>();
        private SourceLevels _previousLevel;

        /// <summary>
        /// Starts listening to the data binding trace of WPF. The trace is read again first: without that a listener added
        /// later hears nothing, and a test that expects "no binding error" would pass whatever the XAML says. The trace is
        /// shared by the whole process, so a test that breaks a binding on purpose must not run beside one that listens.
        /// </summary>
        /// <returns>The listener; call <see cref="Stop"/> when done.</returns>
        public static BindingErrorListener Start()
        {
            PresentationTraceSources.Refresh();

            var listener = new BindingErrorListener { _previousLevel = PresentationTraceSources.DataBindingSource.Switch.Level };
            PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
            PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Warning;

            return listener;
        }

        /// <summary>Stops listening and puts the level of the trace back.</summary>
        public void Stop()
        {
            PresentationTraceSources.DataBindingSource.Listeners.Remove(this);
            PresentationTraceSources.DataBindingSource.Switch.Level = _previousLevel;
        }

        public IReadOnlyList<string> Messages
        {
            get
            {
                lock (_messages)
                {
                    return _messages.ToList();
                }
            }
        }

        public override void Write(string? message) => Record(message);

        public override void WriteLine(string? message) => Record(message);

        private void Record(string? message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                lock (_messages)
                {
                    _messages.Add(message);
                }
            }
        }
    }

    internal static class ViewModelTestSupport
    {
        /// <summary>Lets everything already queued on the dispatcher run, including work the host's events queued.</summary>
        public static async Task Flush() => await Dispatcher.Yield(DispatcherPriority.Background);

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
