using System;
using System.Windows;
using System.Windows.Threading;

using RFiDGear.Contracts;
using RFiDGear.Extensions.CardCheckAssistant.Services;
using RFiDGear.Extensions.CardCheckAssistant.ViewModels;
using RFiDGear.Extensions.CardCheckAssistant.Views;
using RFiDGear.Extensions.CardCheckAssistant.Workflow;
using RFiDGear.UI.UIExtensions;
using RFiDGear.UI.UIExtensions.Interfaces;

namespace RFiDGear.Extensions.CardCheckAssistant
{
    /// <summary>
    /// The Card Check Assistant: guides the operator through the check of the chip of an order, from the list of orders
    /// to the finished report in the order system. It appears in RFiDGear's Extensions menu.
    /// <para>
    /// RFiDGear creates one shared instance the first time the user opens the entry, and calls
    /// <see cref="CreateContent"/> every time. The instance holds no state; each window gets its own page.
    /// </para>
    /// </summary>
    /// <remarks>
    /// There is deliberately no <c>Uri</c> in the attribute: a <c>Uri</c> makes the host open that page and never ask this
    /// class for content. The menu entry's name is the same in every language.
    /// </remarks>
    [UiExtension(Name = "Card Check Assistant", SortOrder = 100, HostPlacement = HostPlacement.Application)]
    public sealed class CardCheckAssistantExtension : IApplicationUIExtension
    {
        /// <inheritdoc/>
        public FrameworkElement CreateContent(IRFiDGearExtensionHost host)
        {
            return CreateContent(host, ComponentCheck.TouchLibrary);
        }

        /// <summary>
        /// Builds the window's content. The support library is used for the first time inside
        /// <see cref="ComponentCheck.IsAvailable"/>; if it cannot be, the content is a page that says so and nothing
        /// that needs the library has been created. This class itself mentions no library type: MEF cannot see a
        /// missing sibling file, so the failure has to be met here, where it can be shown, not when the class is created.
        /// </summary>
        /// <param name="host">The initialized host service.</param>
        /// <param name="touchLibrary">The first use of the library; replaced by a test.</param>
        /// <returns>The page, or the page that explains why there is none.</returns>
        internal static FrameworkElement CreateContent(IRFiDGearExtensionHost host, Func<object?> touchLibrary)
        {
            if (host == null)
            {
                throw new ArgumentNullException(nameof(host));
            }

            if (!ComponentCheck.IsAvailable(touchLibrary))
            {
                return MissingComponentPage.Create(CultureNameOf(host));
            }

            return CreatePage(host, viewModel => new GuidedCheckView(viewModel), AssistantServicesFactory.Create);
        }

        // A host that cannot say what language it uses is read as "no language", which shows English.
        private static string CultureNameOf(IRFiDGearExtensionHost host)
        {
            try
            {
                return host.AppContext.UICultureName ?? string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Builds a page: the workflow of the window, a view model for the calling thread's dispatcher, and the view that
        /// shows it. The view model subscribes to the host and owns the workflow as soon as it exists, so if the view
        /// cannot be built it is disposed here; nobody else holds it, and it would go on listening to the host for the
        /// rest of the session.
        /// </summary>
        /// <param name="host">The initialized host service.</param>
        /// <param name="createView">Builds the view for a view model.</param>
        /// <param name="createServices">
        /// Builds what the page offers and works from (see <see cref="AssistantServices"/>) for the window's dispatcher;
        /// the page offers nothing and works from nothing when omitted, or when this fails.
        /// </param>
        /// <returns>The view.</returns>
        internal static FrameworkElement CreatePage(
            IRFiDGearExtensionHost host,
            Func<GuidedCheckViewModel, FrameworkElement> createView,
            Func<Dispatcher, AssistantServices>? createServices = null)
        {
            // Called on the UI thread, so this is the dispatcher of the window the page will live in.
            var dispatcher = Dispatcher.CurrentDispatcher;
            var services = AssistantServices.None;
            if (createServices != null)
            {
                try
                {
                    services = createServices(dispatcher);
                }
                catch (Exception)
                {
                    // The page works without its memory; that is better than no page.
                }
            }

            var dialogs = new OpenFileDialogService();
            var viewModel = new GuidedCheckViewModel(
                host,
                dispatcher,
                CreateWorkflow(host, services),
                new SettingsDialog(services.Editor, dialogs),
                services.ImportOffer,
                new MessageBoxCloseConfirmation(),
                DispatcherTimerStarter.For(dispatcher));

            try
            {
                return createView(viewModel);
            }
            catch (Exception)
            {
                viewModel.Dispose();
                throw;
            }
        }

        // A workflow that cannot be built is no reason for no page: the page then says that it has nothing to work from.
        private static IGuidedWorkflow CreateWorkflow(IRFiDGearExtensionHost host, AssistantServices services)
        {
            try
            {
                return services.CreateWorkflow(host);
            }
            catch (Exception)
            {
                return AssistantServices.None.CreateWorkflow(host);
            }
        }
    }
}
