using System;
using System.ComponentModel.Composition;
using RFiDGear.Contracts;

namespace RFiDGear.ExtensionSdk
{
    /// <summary>
    /// Marks a class as a secure key provider and exports it to RFiDGear as <see cref="ISecureKeyProvider"/>, together
    /// with the id a project uses to name it and the name the user sees. The class needs a public parameterless
    /// constructor, is created once at start-up and has to be thread-safe (see <see cref="ISecureKeyProvider"/>).
    /// <para>
    /// The id is stored in project files, so it is a promise: it must stay the same for as long as projects use it. It
    /// consists of 1 to 64 lower-case letters, digits, <c>.</c>, <c>_</c> or <c>-</c>; one that does not is ignored with
    /// a log entry. Ids are not shared: if two extensions use one id, neither provider is used, and an id that belongs
    /// to a provider built into RFiDGear cannot be taken over.
    /// </para>
    /// </summary>
    [MetadataAttribute]
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class SecureKeyProviderAttribute : ExportAttribute
    {
        /// <summary>Creates the attribute.</summary>
        /// <param name="id">The provider id stored in projects, for example <c>acme.vault</c>.</param>
        /// <param name="displayName">The name shown to the user.</param>
        public SecureKeyProviderAttribute(string id, string displayName)
            : base(typeof(ISecureKeyProvider))
        {
            Id = id;
            DisplayName = displayName;
        }

        /// <summary>Gets the provider id stored in projects.</summary>
        public string Id { get; }

        /// <summary>Gets the name shown to the user.</summary>
        public string DisplayName { get; }
    }
}
