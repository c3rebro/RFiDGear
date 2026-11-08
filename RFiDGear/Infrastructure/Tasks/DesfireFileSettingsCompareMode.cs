namespace RFiDGear.Infrastructure.Tasks
{
    /// <summary>
    /// Which of the selected settings of a file have to match for the file-settings check to pass. The names are saved
    /// in project files: add members at the end, never rename or remove one.
    /// </summary>
    public enum DesfireFileSettingsCompareMode
    {
        /// <summary>Every selected setting has to match.</summary>
        AllSelected = 0,

        /// <summary>At least one selected setting has to match.</summary>
        AnySelected = 1,
    }
}
