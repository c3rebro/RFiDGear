using System;
using System.Runtime.CompilerServices;

using RFiDGear.Contracts;
using RFiDGear.ExtensionLibraries.CardCheck;
using RFiDGear.Extensions.CardCheckAssistant.Workflow;

namespace RFiDGear.Extensions.CardCheckAssistant.Services
{
    /// <summary>
    /// Builds the guided workflow of one window from the real parts: the settings, the order system (or the made-up
    /// orders), the reports, the check runner and the wait for a card. It uses the support library, so it is only
    /// called after the library is known to be there (see <see cref="ComponentCheck"/>), and the page sees only the
    /// <see cref="IGuidedWorkflow"/> it returns.
    /// </summary>
    internal static class GuidedWorkflowFactory
    {
        /// <summary>Builds the workflow of a window.</summary>
        /// <param name="host">The initialized host service.</param>
        /// <param name="settings">Where the settings are read from, each time an order list is read or an order started.</param>
        /// <param name="log">Where unexpected problems are noted, by type only.</param>
        /// <param name="reservations">Which orders the windows are checking; shared by all windows (<see cref="OrderReservations.Shared"/>).</param>
        /// <param name="testFallbackFolder">The folder the <c>Test</c> folder is made in when the settings name no usable report folder.</param>
        /// <returns>The workflow, in the state of showing the list of orders (it has not read it yet).</returns>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static IGuidedWorkflow Create(
            IRFiDGearExtensionHost host,
            ISettingsStore settings,
            ICardCheckLog log,
            IOrderReservations reservations,
            string testFallbackFolder)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (log == null)
            {
                throw new ArgumentNullException(nameof(log));
            }

            var context = new WorkflowContextSource(
                settings,
                database => CardCheckServices.CreateOrderStore(database, log),
                () => CardCheckServices.CreateStubOrderStore(log),
                testFallbackFolder);

            return Assemble(host, context, log, reservations);
        }

        /// <summary>
        /// Builds a workflow that has nothing to work from: the application data of the assistant could not be reached,
        /// so every read of the list ends in the failure <see cref="ContextFailureCodes.Unavailable"/> and says so.
        /// </summary>
        /// <param name="host">The initialized host service.</param>
        /// <param name="log">Where unexpected problems are noted, by type only.</param>
        /// <param name="reservations">Which orders the windows are checking.</param>
        /// <returns>The workflow.</returns>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static IGuidedWorkflow CreateUnavailable(IRFiDGearExtensionHost host, ICardCheckLog log, IOrderReservations reservations)
        {
            if (log == null)
            {
                throw new ArgumentNullException(nameof(log));
            }

            return Assemble(host, new UnavailableContextSource(), log, reservations);
        }

        private static IGuidedWorkflow Assemble(IRFiDGearExtensionHost host, IWorkflowContextSource context, ICardCheckLog log, IOrderReservations reservations)
        {
            if (host == null)
            {
                throw new ArgumentNullException(nameof(host));
            }

            var files = CardCheckServices.CreateReportFiles(log);
            var reports = CardCheckServices.CreateReportService(log);

            return new CardCheckWorkflow(
                context,
                CardCheckServices.CreateWorkflowSession(log),
                files,
                reports,
                new CheckRunner(host.Project, host.Execution, files, reports, null, log),
                new CardWaiter(host.Reader),
                reservations,
                log);
        }

        private sealed class UnavailableContextSource : IWorkflowContextSource
        {
            public Result<WorkflowContext> Resolve() =>
                Result<WorkflowContext>.Fail(new Failure(FailureKind.NotConfigured, ContextFailureCodes.Unavailable, "The assistant has no settings to work from."));
        }
    }
}
