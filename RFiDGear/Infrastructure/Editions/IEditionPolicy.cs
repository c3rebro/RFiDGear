namespace RFiDGear.Infrastructure.Editions
{
    /// <summary>
    /// What the running edition (the MSI edition or the Microsoft Store edition) allows the program to do on its own.
    /// </summary>
    public interface IEditionPolicy
    {
        /// <summary>
        /// Gets a value indicating whether the program may look for an update on the internet, download it and start its
        /// installer. The Store edition is updated by the Store and must not do any of it.
        /// </summary>
        bool AllowSelfUpdate { get; }

        /// <summary>
        /// Gets a value indicating whether extension code may come from outside the installation: the machine-wide folder
        /// under <c>%ProgramData%</c> and the packages that a user installs from the File menu. The Store edition loads only
        /// what is inside its own package, which cannot be changed after the Store has signed it.
        /// </summary>
        bool AllowExternalExtensions { get; }

        /// <summary>
        /// Gets the text that marks a special edition after the version number (empty for the MSI edition).
        /// </summary>
        string TitleSuffix { get; }
    }
}
