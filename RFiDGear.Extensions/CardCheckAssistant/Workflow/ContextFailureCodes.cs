namespace RFiDGear.Extensions.CardCheckAssistant.Workflow
{
    /// <summary>
    /// The stable codes of the failures that come from reading the settings and picking the store, and of the two the
    /// workflow adds around a list that cannot be read. The page turns each into a text of its own.
    /// </summary>
    internal static class ContextFailureCodes
    {
        /// <summary>The settings cannot be read just now.</summary>
        public const string SettingsUnreadable = "settings.unreadable";

        /// <summary>The settings were written by a newer version.</summary>
        public const string SettingsNewerSchema = "settings.newerSchema";

        /// <summary>No report folder is set.</summary>
        public const string ReportRootNotSet = "report.root.notSet";

        /// <summary>The report folder does not exist.</summary>
        public const string ReportRootMissing = "report.root.missing";

        /// <summary>The folder for the reports cannot be made: access denied (the test folder, or the folder of an order).</summary>
        public const string ReportFolderDenied = "report.folder.denied";

        /// <summary>The folder for the reports cannot be made or reached (the test folder, or the folder of an order).</summary>
        public const string ReportFolderIo = "report.folder.io";

        /// <summary>Reading the list of orders went wrong in a way nobody expected; nothing about the exception but its type is kept.</summary>
        public const string OrdersUnexpected = "workflow.orders.unexpected";

        /// <summary>The assistant has no settings to work from (its application data could not be reached), so there is nothing to start.</summary>
        public const string Unavailable = "workflow.unavailable";
    }
}
