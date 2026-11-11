using System;
using System.Collections.Generic;

using RFiDGear.ExtensionLibraries.CardCheck;
using RFiDGear.Extensions.CardCheckAssistant.Services;
using RFiDGear.Extensions.CardCheckAssistant.Workflow;

using Keys = RFiDGear.Extensions.CardCheckAssistant.Localization.ExtensionStrings.Keys;

namespace RFiDGear.Extensions.CardCheckAssistant.Localization
{
    /// <summary>
    /// Turns a <see cref="Failure"/> of the workflow into the text the operator reads. The text is chosen by the failure's
    /// code, because the same <see cref="FailureKind"/> stands for quite different causes (an unreachable reader, a busy
    /// RFiDGear and an unreachable order system are all <see cref="FailureKind.Unreachable"/>) and the operator's next
    /// step differs. A code without a text of its own gets the family text of its ending (<c>.denied</c>, <c>.io</c>,
    /// <c>.unexpected</c>), and anything else the generic text, which carries the English safe message of the failure.
    /// </summary>
    internal static class FailureTexts
    {
        private static readonly Dictionary<string, string> Exact = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // Settings, folders and the order system.
            [ContextFailureCodes.SettingsUnreadable] = Keys.FailSettingsUnreadable,
            ["settings.busy"] = Keys.FailSettingsUnreadable,
            [ContextFailureCodes.SettingsNewerSchema] = Keys.FailSettingsNewer,
            [ContextFailureCodes.ReportRootNotSet] = Keys.FailReportRootNotSet,
            [ContextFailureCodes.ReportRootMissing] = Keys.FailReportRootInvalid,
            ["report.root.invalid"] = Keys.FailReportRootInvalid,
            ["report.root.notAbsolute"] = Keys.FailReportRootInvalid,
            [ContextFailureCodes.ReportFolderDenied] = Keys.FailReportFolderDenied,
            [ContextFailureCodes.ReportFolderIo] = Keys.FailReportFolderIo,
            ["orders.notConfigured"] = Keys.FailOrdersNotConfigured,
            ["orders.table.invalid"] = Keys.FailOrdersSetup,
            ["orders.connection.invalid"] = Keys.FailOrdersSetup,
            ["orders.schema"] = Keys.FailOrdersSetup,
            ["orders.driver.missing"] = Keys.FailOrdersDriver,
            ["orders.unreachable"] = Keys.FailOrdersUnreachable,
            ["orders.timeout"] = Keys.FailOrdersUnreachable,
            ["orders.denied"] = Keys.FailOrdersDenied,
            [ContextFailureCodes.Unavailable] = Keys.FailUnavailable,
            [ContextFailureCodes.OrdersUnexpected] = Keys.FailOrdersUnexpected,

            // The order and the card.
            [WorkflowFailureCodes.OrderUnknown] = Keys.FailOrderUnknown,
            [WorkflowFailureCodes.OrderNotStartable] = Keys.FailOrderNotStartable,
            [WorkflowFailureCodes.OrderInUse] = Keys.FailOrderInUse,
            [WorkflowFailureCodes.ProjectNotConfigured] = Keys.FailProjectNotConfigured,
            ["report.job.empty"] = Keys.FailJobNumberInvalid,
            ["report.job.invalid"] = Keys.FailJobNumberInvalid,
            ["report.chip.empty"] = Keys.FailChipNumberInvalid,
            ["report.chip.invalid"] = Keys.FailChipNumberInvalid,
            ["report.path.tooLong"] = Keys.FailPathTooLong,
            [WorkflowFailureCodes.WaitTimedOut] = Keys.FailWaitTimedOut,
            [WorkflowFailureCodes.CardChanged] = Keys.FailCardChanged,
            [WorkflowFailureCodes.CardUnclear] = Keys.FailCardUnclear,
            [WorkflowFailureCodes.MemoryProjectInvalid] = Keys.FailMemoryProjectInvalid,
            [WorkflowFailureCodes.CardMoveTimedOut] = Keys.FailCardMoveTimedOut,
            [WorkflowFailureCodes.CardMissing] = Keys.FailCardMissing,

            // The check run.
            [CheckFailureCodes.ProjectRequired] = Keys.FailProjectSetting,
            [CheckFailureCodes.ProjectNotAbsolute] = Keys.FailProjectSetting,
            [CheckFailureCodes.ProjectNotFound] = Keys.FailProjectSetting,
            [CheckFailureCodes.LoadPathNotFound] = Keys.FailProjectSetting,
            [CheckFailureCodes.LoadUnsupportedFormat] = Keys.FailLoadFormat,
            [CheckFailureCodes.ProjectChanged] = Keys.FailProjectChanged,
            [CheckFailureCodes.LoadBusy] = Keys.FailHostBusy,
            [CheckFailureCodes.RunBusy] = Keys.FailHostBusy,
            [CheckFailureCodes.RunRefused] = Keys.FailRunRefused,
            [CheckFailureCodes.RunFailed] = Keys.FailRunFailed,
            [CheckFailureCodes.RunUnexpected] = Keys.FailUnexpected,
            [CheckFailureCodes.ReportMissing] = Keys.FailReportMissing,

            // Opening a report or a folder.
            [OpenerFailureCodes.PathInvalid] = Keys.FailOpenPathInvalid,
            [OpenerFailureCodes.NotFound] = Keys.FailOpenNotFound,
            [OpenerFailureCodes.NotAllowed] = Keys.FailOpenNotAllowed,
            [OpenerFailureCodes.NoViewer] = Keys.FailOpenNoViewer,
            [OpenerFailureCodes.StartFailed] = Keys.FailOpenFailed,

            // The reports.
            ["report.source.missing"] = Keys.FailReportFileMissing,
            ["report.source.notFound"] = Keys.FailReportFileMissing,
            ["report.unreadable"] = Keys.FailReportUnreadable,
            ["report.noForm"] = Keys.FailReportUnreadable,
            ["report.field.missing"] = Keys.FailReportUnreadable,
            ["report.licenseLimit"] = Keys.FailLicense,
            [WorkflowFailureCodes.FinalUnreadable] = Keys.FailFinalUnreadable,
            [WorkflowFailureCodes.Faulted] = Keys.FailUnexpected,
        };

        /// <summary>Whether a code has a text of its own (not only a family text or the generic one).</summary>
        /// <param name="code">The failure code.</param>
        /// <returns><c>true</c> if <see cref="Describe"/> picks a text for exactly this code.</returns>
        internal static bool HasExactText(string code) => code != null && Exact.ContainsKey(code);

        /// <summary>The text for a failure, in the language of <paramref name="strings"/>.</summary>
        /// <param name="failure">The failure.</param>
        /// <param name="strings">The page's texts.</param>
        /// <returns>The text; never empty. The code is not part of it: the page shows it beside the text.</returns>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        internal static string Describe(Failure failure, LocalizedStrings strings)
        {
            if (failure == null)
            {
                throw new ArgumentNullException(nameof(failure));
            }

            if (strings == null)
            {
                throw new ArgumentNullException(nameof(strings));
            }

            if (Exact.TryGetValue(failure.Code, out var key))
            {
                return strings[key];
            }

            var familyKey = FamilyKey(failure.Code);

            return familyKey.Length > 0 ? strings[familyKey] : strings.Format(Keys.FailGeneric, failure.SafeMessage);
        }

        // The family of a code by its ending: a report file that is locked or cannot be written, and anything that
        // went wrong in a way nobody expected. A code that is in no family has no key, and the generic text is used.
        private static string FamilyKey(string code)
        {
            if (code.StartsWith("report.", StringComparison.Ordinal) && code.EndsWith(".denied", StringComparison.Ordinal))
            {
                return Keys.FailReportDenied;
            }

            if (code.StartsWith("report.", StringComparison.Ordinal) && code.EndsWith(".io", StringComparison.Ordinal))
            {
                return Keys.FailReportIo;
            }

            return code.EndsWith(".unexpected", StringComparison.Ordinal) ? Keys.FailUnexpected : string.Empty;
        }
    }
}
