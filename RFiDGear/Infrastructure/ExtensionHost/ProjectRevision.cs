#nullable enable
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Serialization;

using RFiDGear.Models;

namespace RFiDGear.Infrastructure.ExtensionHost
{
    /// <summary>
    /// Says in which state a project is: a value that is the same for the same project and another when anything that a save
    /// would write is another. It is made of the text the project is written as (the serializer of
    /// <see cref="DatabaseReaderWriter.WriteDatabase(ChipTaskHandlerModel, string, bool)"/>), so changes that were not saved
    /// count, and what a run only shows (the result of a task, the state of its execution) is not part of it.
    /// <para>
    /// It is a keyed hash, with a key made when the process started and never written anywhere. The text of a project holds the
    /// keys that were typed into a task and are not bound to a key provider, and a plain hash of it would let anybody who gets
    /// to see a revision test guesses for such a key offline. Because the key is the process's, a revision is valid for this
    /// process only: it is compared, never stored for later or compared with a file.
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
            ArgumentNullException.ThrowIfNull(model);

            using var stream = new MemoryStream();

            using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, leaveOpen: true))
            {
                Serializer.Value.Serialize(writer, model);
            }

            var mac = HMACSHA256.HashData(Key, stream.ToArray());

            return Convert.ToHexString(mac, 0, RevisionBytes).ToLowerInvariant();
        }
    }
}
