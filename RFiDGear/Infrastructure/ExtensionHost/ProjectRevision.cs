#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using System.Xml.Serialization;

using RFiDGear.Models;
using RFiDGear.ViewModel.TaskSetupViewModels;

namespace RFiDGear.Infrastructure.ExtensionHost
{
    /// <summary>
    /// Says in which state a project is: a value that is the same for the same project and another when anything that a run
    /// would use is another. It is made of the text the project is written as (the serializer of
    /// <see cref="DatabaseReaderWriter.WriteDatabase(ChipTaskHandlerModel, string, bool)"/>), so changes that were not saved
    /// count, without what a run itself leaves in that text (see below).
    /// <para>
    /// It is a keyed hash, with a key made when the process started and never written anywhere. The text of a project holds the
    /// keys that were typed into a task and are not bound to a key provider, and a plain hash of it would let anybody who gets
    /// to see a revision test guesses for such a key offline. Because the key is the process's, a revision is valid for this
    /// process only: it is compared, never stored for later or compared with a file.
    /// </para>
    /// <para>
    /// What a run writes into the tasks is not part of it, or a project that reads a card would have another revision after
    /// every run, and a client that approved one could not tell that from a change made in the window. The data that a task
    /// has read from a card (<c>ChildNodeViewModelFromChip</c>) is never an input. The data a task holds in
    /// <c>ChildNodeViewModelTemp</c> is the payload of a write task, and is part of the revision for that task only: for every
    /// other task it is where a read puts what it found. A write task that takes its data from a file just before it writes
    /// (<c>RefreshDesfireDataFromFileBeforeWrite</c>) replaces it, so for that task the file is the input, and a revision does
    /// not cover a file: it says what is in the project, not what is on the disk. The focus of a task in the list
    /// (<c>IsFocused</c>) is a state of the window.
    /// </para>
    /// </summary>
    internal static class ProjectRevision
    {
        private const int RevisionBytes = 16;

        private static readonly byte[] Key = RandomNumberGenerator.GetBytes(32);

        private static readonly Lazy<XmlSerializer> Serializer = new Lazy<XmlSerializer>(() => new XmlSerializer(typeof(ChipTaskHandlerModel)));

        /// <summary>Makes the revision of a project. It reads the tasks, so it runs on the thread they belong to (the main window's).</summary>
        /// <param name="model">The project as it is now.</param>
        /// <returns>The revision: 32 lower-case hexadecimal digits.</returns>
        internal static string Compute(ChipTaskHandlerModel model)
        {
            var mac = HMACSHA256.HashData(Key, Encoding.UTF8.GetBytes(CanonicalText(model)));

            return Convert.ToHexString(mac, 0, RevisionBytes).ToLowerInvariant();
        }

        /// <summary>
        /// Makes the text a revision is made of: the project as a save would write it, without what a run writes into the tasks.
        /// White space is kept: a task whose text is a space and one whose text is two spaces are different projects (a report
        /// passes such a text on as it is). It holds typed keys, so it is never logged or handed out; it is internal for the tests.
        /// </summary>
        /// <param name="model">The project as it is now.</param>
        /// <returns>The text.</returns>
        internal static string CanonicalText(ChipTaskHandlerModel model)
        {
            ArgumentNullException.ThrowIfNull(model);

            using var stream = new MemoryStream();

            using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, leaveOpen: true))
            {
                Serializer.Value.Serialize(writer, model);
            }

            stream.Position = 0;
            var document = XDocument.Load(stream, LoadOptions.PreserveWhitespace);
            LeaveOutWhatARunWrites(document);
            AddWhatAWriteWillWrite(document, model);

            return document.ToString(SaveOptions.DisableFormatting);
        }

        // A DESFire write sends a slice of the buffer of its task: from a start and for a length that the data explorer selects, and the
        // selection is a state of the window that the project does not save. The bytes that would go to the card are what counts, so
        // they are part of the text: another slice of the same buffer is another project.
        private static void AddWhatAWriteWillWrite(XDocument document, ChipTaskHandlerModel model)
        {
            var elements = document.Descendants("TaskCollection").Elements().ToList();
            var tasks = model.TaskCollection?.ToList();

            if (tasks == null || tasks.Count != elements.Count)
            {
                return;
            }

            for (var i = 0; i < tasks.Count; i++)
            {
                if (tasks[i] is MifareDesfireSetupViewModel desfire && HoldsThePayloadToWrite(elements[i]))
                {
                    elements[i].Add(new XElement("EffectiveWritePayload", desfire.TryGetDesfireWritePayload(out var payload, out _) ? Convert.ToHexString(payload) : string.Empty));
                }
            }
        }

        // The tasks are the elements below TaskCollection. A name that no task has is simply not found.
        private static void LeaveOutWhatARunWrites(XDocument document)
        {
            foreach (var task in document.Descendants("TaskCollection").Elements())
            {
                task.Element("ChildNodeViewModelFromChip")?.Remove();
                task.Element("IsFocused")?.Remove();

                if (!HoldsThePayloadToWrite(task))
                {
                    task.Element("ChildNodeViewModelTemp")?.Remove();
                }
            }
        }

        private static bool HoldsThePayloadToWrite(XElement task) =>
            string.Equals(task.Element("SelectedTaskType")?.Value, "WriteData", StringComparison.Ordinal)
            && !string.Equals(task.Element("RefreshDesfireDataFromFileBeforeWrite")?.Value, "true", StringComparison.OrdinalIgnoreCase);
    }
}
