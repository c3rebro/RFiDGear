using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using RFiDGear.ExtensionLibraries.CardCheck;

namespace RFiDGear.Tests.CardCheck
{
    /// <summary>A log that remembers what it was given, so a test can read it.</summary>
    internal sealed class RecordingCardCheckLog : ICardCheckLog
    {
        private readonly List<string> _entries = new List<string>();

        /// <summary>Every message with its arguments, as one line each, in the order written.</summary>
        public IReadOnlyList<string> Entries
        {
            get
            {
                lock (_entries)
                {
                    return _entries.ToList();
                }
            }
        }

        /// <summary>Everything written, joined, for a "this must not appear" check.</summary>
        public string Everything => string.Join(" | ", Entries);

        public void Information(string messageTemplate, params object?[] values) => Add("I", messageTemplate, values);

        public void Warning(string messageTemplate, params object?[] values) => Add("W", messageTemplate, values);

        public void Error(string messageTemplate, params object?[] values) => Add("E", messageTemplate, values);

        public void Unexpected(string operation, UnexpectedFault fault) => Add("X", operation + " " + fault.TypeName, Array.Empty<object?>());

        private void Add(string level, string template, object?[] values)
        {
            lock (_entries)
            {
                _entries.Add(level + ": " + template + " [" + string.Join(", ", values.Select(v => v?.ToString() ?? "null")) + "]");
            }
        }
    }

    /// <summary>A temporary folder that is removed, read-only files and all, when the test ends.</summary>
    internal sealed class TempFolder : IDisposable
    {
        public TempFolder()
        {
            Path = Directory.CreateTempSubdirectory("rfidgear-cardcheck-").FullName;
        }

        public string Path { get; }

        public void Dispose()
        {
            foreach (var file in Directory.EnumerateFiles(Path, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(Path, recursive: true);
        }
    }
}
