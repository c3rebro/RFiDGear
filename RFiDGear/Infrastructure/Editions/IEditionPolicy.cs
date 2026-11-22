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
        /// Gets the text that marks a special edition after the version number (empty for the MSI edition).
        /// </summary>
        string TitleSuffix { get; }
    }
}
