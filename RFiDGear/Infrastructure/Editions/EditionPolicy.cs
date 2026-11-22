namespace RFiDGear.Infrastructure.Editions
{
    /// <summary>
    /// The two editions of the program: the standard one (the MSI and the bundle, and any run without a package) and the one
    /// that runs from the Microsoft Store.
    /// </summary>
    public sealed class EditionPolicy : IEditionPolicy
    {
        private EditionPolicy(bool allowSelfUpdate, string titleSuffix)
        {
            AllowSelfUpdate = allowSelfUpdate;
            TitleSuffix = titleSuffix;
        }

        /// <summary>Gets the policy of the MSI edition. It updates itself from GitHub, as before.</summary>
        public static IEditionPolicy Standard { get; } = new EditionPolicy(true, string.Empty);

        /// <summary>Gets the policy of the Store edition: no self-update, and the version is marked as such.</summary>
        public static IEditionPolicy Store { get; } = new EditionPolicy(false, "(Store)");

        /// <summary>Gets the policy of the edition this process runs as: the Store policy when it is packaged.</summary>
        public static IEditionPolicy Current => AppEdition.IsPackaged ? Store : Standard;

        /// <inheritdoc />
        public bool AllowSelfUpdate { get; }

        /// <inheritdoc />
        public string TitleSuffix { get; }
    }
}
