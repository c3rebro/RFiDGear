namespace RFiDGear.Services.Licensing
{
    /// <summary>
    /// What the Store says about the right to use the program.
    /// </summary>
    public enum LicenseKind
    {
        /// <summary>The license could not be read. The program goes on as if it were licensed (it fails open).</summary>
        Unknown,

        /// <summary>The full version is bought, or this edition needs no license.</summary>
        Full,

        /// <summary>A free trial that runs until <see cref="LicenseSnapshot.TrialEnds"/>.</summary>
        Trial,

        /// <summary>The trial is over and nothing was bought.</summary>
        Expired,
    }
}
