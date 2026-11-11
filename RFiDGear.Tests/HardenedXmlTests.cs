using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

using RFiDGear.Infrastructure.FileAccess;

using Serilog;
using Serilog.Core;
using Serilog.Events;

using Xunit;

namespace RFiDGear.Tests
{
    /// <summary>
    /// Project and settings files may come from someone else. They are parsed with DTD processing prohibited, and a
    /// failure is logged by exception types and position only, because the text of a parser or serializer exception
    /// can echo values from the file.
    /// </summary>
    [Collection("AppDataPath")]
    public sealed class HardenedXmlTests : IsolatedProjectTestBase
    {
        private const string Leak = "LEAKY-VALUE-7731";

        private const string Declaration = "<?xml version=\"1.0\" encoding=\"utf-8\"?>";

        private const string ProjectBody =
            "<ChipTaskHandlerModel><ManifestVersion>1.0.0</ManifestVersion><TaskCollection /></ChipTaskHandlerModel>";

        private string WriteFile(string name, string content)
        {
            var path = Path.Combine(NewScratchDirectory(), name);
            File.WriteAllText(path, content, new UTF8Encoding(false));
            return path;
        }

        private static async Task<(DatabaseReaderWriter.DatabaseReadResult Result, IReadOnlyList<LogEvent> Logged)> ReadCapturingLog(
            string root, string file)
        {
            var sink = new CapturingLogSink();
            var previous = Log.Logger;
            Log.Logger = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();

            try
            {
                var reader = new DatabaseReaderWriter(new ProjectManager(root));
                var (result, _) = await reader.ReadProjectAsync(file);
                return (result, sink.Events);
            }
            finally
            {
                Log.Logger = previous;
            }
        }

        [Fact]
        public async Task ReadProject_PlainXmlWithADocumentType_IsRefused()
        {
            var plain = WriteFile("plain.xml", Declaration + ProjectBody);
            var withDocumentType = WriteFile(
                "doctype.xml", Declaration + "<!DOCTYPE ChipTaskHandlerModel [<!ENTITY e \"x\">]>" + ProjectBody);
            var reader = new DatabaseReaderWriter(new ProjectManager(_root));

            var (control, _) = await reader.ReadProjectAsync(plain);
            var (refused, model) = await reader.ReadProjectAsync(withDocumentType);

            Assert.True(control.Success);   // the same project without the declaration loads
            Assert.False(refused.Success);
            Assert.Null(model);
        }

        [Fact]
        public async Task ReadProject_AnEntityExpansionBomb_FailsAtOnceInsteadOfExpanding()
        {
            // Nine levels of ten references each would expand to a billion characters if the declaration were honoured.
            var entities = new StringBuilder("<!ENTITY l0 \"lol\">");
            for (var level = 1; level <= 9; level++)
            {
                entities.Append($"<!ENTITY l{level} \"{string.Concat(Enumerable.Repeat($"&l{level - 1};", 10))}\">");
            }

            var bomb = WriteFile(
                "bomb.xml",
                Declaration + "<!DOCTYPE ChipTaskHandlerModel [" + entities + "]>"
                + "<ChipTaskHandlerModel><ManifestVersion>&l9;</ManifestVersion><TaskCollection /></ChipTaskHandlerModel>");
            var reader = new DatabaseReaderWriter(new ProjectManager(_root));

            var started = DateTime.UtcNow;
            var (result, model) = await reader.ReadProjectAsync(bomb);

            Assert.False(result.Success);
            Assert.Null(model);
            Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void LoadSettings_WithADocumentType_IsRefused()
        {
            var directory = NewScratchDirectory();
            var path = Path.Combine(directory, "settings.xml");
            using (var settings = new SettingsReaderWriter(directory, loadSettings: false))
            {
                settings.SaveSettings(new RFiDGear.Models.DefaultSpecification(true), path);
            }

            var xml = File.ReadAllText(path);
            var afterDeclaration = xml.IndexOf("?>", StringComparison.Ordinal) + 2;
            File.WriteAllText(
                path,
                xml.Insert(afterDeclaration, "<!DOCTYPE DefaultSpecification [<!ENTITY e \"x\">]>"),
                new UTF8Encoding(false));

            Assert.Throws<XmlException>(() => new ProjectManager(_root).LoadSettings(path));
        }

        [Fact]
        public async Task ReadProject_AnXmlErrorIsLoggedByTypeAndPosition_NotByItsText()
        {
            // The parser's message names the element it did not expect.
            var file = WriteFile(
                "malformed.xml",
                Declaration + "<ChipTaskHandlerModel><ManifestVersion>1.0.0</ManifestVersion><" + Leak + "></ChipTaskHandlerModel>");

            var (result, logged) = await ReadCapturingLog(_root, file);

            Assert.False(result.Success);
            var error = Assert.Single(logged, e => e.Level == LogEventLevel.Error);
            Assert.Null(error.Exception);   // the exception object would write its message, and its inner ones, again
            var rendered = error.RenderMessage();
            Assert.DoesNotContain(Leak, rendered);
            Assert.Contains("XmlException", rendered);
            Assert.Contains("line 1, position", rendered);
        }

        [Fact]
        public async Task ReadProject_AManifestThatIsNotAVersion_IsLoggedByTypeNotByItsValue()
        {
            // The conversion error says: the string '<value>' is not a valid number.
            var file = WriteFile(
                "badmanifest.xml",
                Declaration + "<ChipTaskHandlerModel><ManifestVersion>" + Leak + "</ManifestVersion><TaskCollection /></ChipTaskHandlerModel>");

            var (result, logged) = await ReadCapturingLog(_root, file);

            Assert.False(result.Success);
            var error = Assert.Single(logged, e => e.Level == LogEventLevel.Error);
            Assert.Null(error.Exception);
            var rendered = error.RenderMessage();
            Assert.DoesNotContain(Leak, rendered);
            Assert.Contains("FormatException", rendered);
        }

        [Fact]
        public void ReadSettings_AValueTheSerializerCannotConvert_IsLoggedByTypeAndPosition_NotByItsText()
        {
            var directory = NewScratchDirectory();
            var path = Path.Combine(directory, "settings.xml");
            using (var writer = new SettingsReaderWriter(directory, loadSettings: false))
            {
                writer.SaveSettings(new RFiDGear.Models.DefaultSpecification(true), path);
            }

            // The serializer's inner exception says: the string '<value>' was not recognized as a valid Boolean.
            var xml = File.ReadAllText(path);
            var start = xml.IndexOf("<AutoCheckForUpdates>", StringComparison.Ordinal);
            Assert.True(start >= 0, "the settings file has the element this test changes");
            var end = xml.IndexOf("</AutoCheckForUpdates>", start, StringComparison.Ordinal);
            File.WriteAllText(
                path,
                xml.Substring(0, start) + "<AutoCheckForUpdates>" + Leak + xml.Substring(end),
                new UTF8Encoding(false));

            var sink = new CapturingLogSink();
            var previous = Log.Logger;
            Log.Logger = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();
            try
            {
                using var reader = new SettingsReaderWriter(directory, loadSettings: false);
                reader.ReadSettings(path);
            }
            finally
            {
                Log.Logger = previous;
            }

            var error = Assert.Single(sink.Events, e => e.Level == LogEventLevel.Error);
            Assert.Null(error.Exception);
            var rendered = error.RenderMessage();
            Assert.DoesNotContain(Leak, rendered);
            Assert.Contains("XmlContentException", rendered);
            Assert.Contains("FormatException", rendered);
            Assert.Contains("line ", rendered);
        }
    }

    /// <summary>A log sink that keeps what it receives, for tests that look at what was logged.</summary>
    internal sealed class CapturingLogSink : ILogEventSink
    {
        private readonly List<LogEvent> _events = new();

        public IReadOnlyList<LogEvent> Events
        {
            get
            {
                lock (_events)
                {
                    return _events.ToList();
                }
            }
        }

        public void Emit(LogEvent logEvent)
        {
            lock (_events)
            {
                _events.Add(logEvent);
            }
        }
    }
}
