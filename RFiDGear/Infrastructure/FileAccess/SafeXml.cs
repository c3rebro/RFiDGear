using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace RFiDGear.Infrastructure.FileAccess
{
    /// <summary>
    /// How project and settings files are read. These files may come from someone else, so they are parsed with
    /// DTD processing prohibited (no entity expansion, nothing external is resolved), and a failure is described for the
    /// log by exception types and position only: the text of an <see cref="XmlSerializer"/> or parser exception can
    /// echo values from the file (for example the attribute value it could not convert).
    /// </summary>
    internal static class SafeXml
    {
        /// <summary>The element that carries the version of the application that wrote a file.</summary>
        private const string ManifestVersionElement = "ManifestVersion";

        /// <summary>The most exception levels <see cref="Describe"/> follows.</summary>
        private const int MaxDescribedLevels = 5;

        /// <summary>
        /// Settings for reading untrusted XML: a document type declaration is refused and nothing external is resolved.
        /// </summary>
        /// <returns>New settings; they are not shared, so a caller may change them.</returns>
        internal static XmlReaderSettings CreateReaderSettings() =>
            new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };

        /// <summary>
        /// A reader for <see cref="XmlSerializer.Deserialize(XmlReader)"/> that behaves like the one the serializer builds
        /// around a <see cref="TextReader"/> itself (significant white space only, normalized values), except that a
        /// document type declaration is refused.
        /// </summary>
        /// <param name="text">The XML text.</param>
        /// <returns>The reader; the caller disposes it.</returns>
        internal static XmlTextReader CreateSerializerReader(TextReader text) =>
            new XmlTextReader(text)
            {
                WhitespaceHandling = WhitespaceHandling.Significant,
                Normalization = true,
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            };

        /// <summary>
        /// Reads the text of the first <c>ManifestVersion</c> element, trimmed, or <c>null</c> if there is none. The
        /// whole document is read, one node at a time and without building it in memory, so a document that is not
        /// well-formed, or whose encoding is damaged anywhere, is an <see cref="XmlException"/> here rather than text with
        /// replacement characters in the model.
        /// </summary>
        /// <param name="reader">A reader made from <see cref="CreateReaderSettings"/>.</param>
        /// <returns>The manifest version text, or <c>null</c>.</returns>
        /// <exception cref="XmlException">The document is not well-formed, has a document type declaration, or the element has child elements.</exception>
        internal static string ReadManifestVersion(XmlReader reader)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            string manifest = null;
            var found = false;

            while (!reader.EOF)
            {
                if (!found
                    && reader.NodeType == XmlNodeType.Element
                    && reader.NamespaceURI.Length == 0
                    && string.Equals(reader.LocalName, ManifestVersionElement, StringComparison.Ordinal))
                {
                    // Reading the content moves the reader to the node after the element.
                    manifest = reader.ReadElementContentAsString().Trim();
                    found = true;
                    continue;
                }

                reader.Read();
            }

            return manifest;
        }

        /// <summary>
        /// Deserializes an XML document that may come from someone else. A document type declaration is refused, and a
        /// failure is reported as an <see cref="XmlContentException"/> that knows where in the document it happened.
        /// </summary>
        /// <typeparam name="T">The type to return.</typeparam>
        /// <param name="serializer">The serializer of the document's root type.</param>
        /// <param name="text">The XML text.</param>
        /// <returns>The deserialized object, or <c>null</c> when the document deserializes to nothing or to another type.</returns>
        /// <exception cref="XmlContentException">The document could not be read.</exception>
        internal static T Deserialize<T>(XmlSerializer serializer, TextReader text)
            where T : class
        {
            using (var reader = CreateSerializerReader(text))
            {
                try
                {
                    return serializer.Deserialize(reader) as T;
                }
                catch (Exception e) when (e is InvalidOperationException || e is XmlException)
                {
                    throw new XmlContentException(reader.LineNumber, reader.LinePosition, e);
                }
            }
        }

        /// <summary>
        /// Describes a failure for the log without any text of the exception: the exception types from the outermost
        /// to the innermost, and the position in the document when it is known.
        /// </summary>
        /// <param name="exception">The failure.</param>
        /// <returns>For example <c>XmlContentException &gt; InvalidOperationException &gt; FormatException at line 3, position 14</c>.</returns>
        internal static string Describe(Exception exception)
        {
            if (exception == null)
            {
                throw new ArgumentNullException(nameof(exception));
            }

            var types = new List<string>();
            int? line = null;
            int? position = null;

            for (var current = exception; current != null && types.Count < MaxDescribedLevels; current = current.InnerException)
            {
                types.Add(current.GetType().Name);

                if (line == null && current is XmlContentException content && content.LineNumber > 0)
                {
                    line = content.LineNumber;
                    position = content.LinePosition;
                }
                else if (line == null && current is XmlException xml && xml.LineNumber > 0)
                {
                    line = xml.LineNumber;
                    position = xml.LinePosition;
                }
            }

            var description = string.Join(" > ", types);

            return line == null
                ? description
                : string.Format(CultureInfo.InvariantCulture, "{0} at line {1}, position {2}", description, line, position);
        }

        /// <summary>
        /// The stack trace of the innermost exception. It names code, not data, so unlike a message it is safe to log.
        /// </summary>
        /// <param name="exception">The failure.</param>
        /// <returns>The stack trace, or an empty string.</returns>
        internal static string InnermostStackTrace(Exception exception) =>
            exception?.GetBaseException().StackTrace ?? string.Empty;
    }

    /// <summary>
    /// An XML document could not be read. The message is fixed text and the position is where the reader was when it
    /// failed; the inner exception holds the details (which can contain values from the file), so it must not be written
    /// to a log. See <see cref="SafeXml.Describe"/>.
    /// </summary>
    internal sealed class XmlContentException : InvalidOperationException
    {
        /// <summary>Creates the exception.</summary>
        /// <param name="lineNumber">The line the reader was on, or 0 if unknown.</param>
        /// <param name="linePosition">The position in that line, or 0 if unknown.</param>
        /// <param name="inner">What the serializer or the parser reported.</param>
        public XmlContentException(int lineNumber, int linePosition, Exception inner)
            : base(
                string.Format(CultureInfo.InvariantCulture, "The XML document could not be read (line {0}, position {1}).", lineNumber, linePosition),
                inner)
        {
            LineNumber = lineNumber;
            LinePosition = linePosition;
        }

        /// <summary>The line the reader was on, or 0 if unknown.</summary>
        public int LineNumber { get; }

        /// <summary>The position in that line, or 0 if unknown.</summary>
        public int LinePosition { get; }
    }
}
