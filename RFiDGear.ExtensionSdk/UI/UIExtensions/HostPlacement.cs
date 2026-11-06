namespace RFiDGear.UI.UIExtensions
{
    /// <summary>
    /// Declares which host region an <see cref="Interfaces.IUIExtension"/> targets.
    /// </summary>
    public enum HostPlacement
    {
        /// <summary>Extension appears in card-task editor tabs (default; backward compatible).</summary>
        CardTask = 0,
        /// <summary>Extension appears in the main application window (Extensions menu).</summary>
        Application = 1
    }
}
