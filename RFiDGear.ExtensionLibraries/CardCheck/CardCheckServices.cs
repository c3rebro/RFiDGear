namespace RFiDGear.ExtensionLibraries.CardCheck
{
    /// <summary>
    /// The way in for the extension. Everything else in the library is an interface or a plain value type, and
    /// its implementations are internal; this class hands out the ones an extension window needs. Later changes
    /// add the services here; none of them keeps process-wide state, so each window creates its own.
    /// </summary>
    public static class CardCheckServices
    {
        /// <summary>Creates the log that writes to the host's Serilog pipeline.</summary>
        /// <returns>A log that follows the host's current logger.</returns>
        public static ICardCheckLog CreateLog() => new SerilogCardCheckLog();
    }
}
