using RFiDGear.Extensions.CardCheckAssistant.Localization;
using RFiDGear.Extensions.CardCheckAssistant.Mvvm;

namespace RFiDGear.Extensions.CardCheckAssistant.ViewModels
{
    /// <summary>What the list of orders is grouped by.</summary>
    internal enum OrderGrouping
    {
        /// <summary>By the status of the order.</summary>
        Status,

        /// <summary>By the customer.</summary>
        Customer,

        /// <summary>By the month of the last change.</summary>
        Month,

        /// <summary>Not grouped.</summary>
        None
    }

    /// <summary>One way to group the list of orders, with its name in the language of the page.</summary>
    internal sealed class OrderGroupingOption : ObservableBase
    {
        private readonly string _key;
        private readonly LocalizedStrings _strings;

        /// <summary>Creates the option.</summary>
        /// <param name="grouping">What it groups by.</param>
        /// <param name="key">The text that names it.</param>
        /// <param name="strings">The page's texts.</param>
        public OrderGroupingOption(OrderGrouping grouping, string key, LocalizedStrings strings)
        {
            Grouping = grouping;
            _key = key;
            _strings = strings;
        }

        /// <summary>Gets what the option groups by.</summary>
        public OrderGrouping Grouping { get; }

        /// <summary>Gets the name of the option, in the language of the page.</summary>
        public string Name => _strings[_key];

        /// <summary>Tells the page that the name changed because the language of the page did.</summary>
        public void Refresh() => OnPropertyChanged(nameof(Name));
    }
}
