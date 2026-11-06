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
            if (host == null)
            {
                throw new ArgumentNullException(nameof(host));
            }

            // Called on the UI thread, so this is the dispatcher of the window the page will live in.
            var viewModel = new CardCheckAssistantViewModel(host, Dispatcher.CurrentDispatcher, new OpenFileDialogService());
            return new CardCheckAssistantView(viewModel);
        }
    }
}
