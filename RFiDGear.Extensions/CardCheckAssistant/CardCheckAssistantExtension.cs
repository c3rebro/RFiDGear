using System;
using System.Windows;
using System.Windows.Threading;

using RFiDGear.Contracts;
using RFiDGear.Extensions.CardCheckAssistant.Services;
using RFiDGear.Extensions.CardCheckAssistant.ViewModels;
using RFiDGear.Extensions.CardCheckAssistant.Views;
using RFiDGear.UI.UIExtensions;
using RFiDGear.UI.UIExtensions.Interfaces;

namespace RFiDGear.Extensions.CardCheckAssistant
{
    /// <summary>
    /// The Card Check Assistant: pick a project, enter job and chip number and a report file, run it once on the card on
    /// the reader and read the result. It appears in RFiDGear's Extensions menu.
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

            return CreatePage(host, viewModel => new CardCheckAssistantView(viewModel), AssistantServicesFactory.Create);
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
        /// Builds a page: a view model for the calling thread's dispatcher, and the view that shows it. The view model
        /// subscribes to the host as soon as it exists, so if the view cannot be built it is disposed here; nobody else
        /// holds it, and it would go on listening to the host for the rest of the session.
        /// </summary>
        /// <param name="host">The initialized host service.</param>
        /// <param name="createView">Builds the view for a view model.</param>
        /// <param name="createServices">
        /// Builds what the page remembers and offers (see <see cref="AssistantServices"/>) for the window's dispatcher;
        /// the page remembers and offers nothing when omitted, or when this fails.
        /// </param>
        /// <returns>The view.</returns>
        internal static FrameworkElement CreatePage(
            IRFiDGearExtensionHost host,
            Func<CardCheckAssistantViewModel, FrameworkElement> createView,
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

            var viewModel = new CardCheckAssistantViewModel(host, dispatcher, new OpenFileDialogService(), null, services.Settings, services.ImportOffer);

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
    }
}
