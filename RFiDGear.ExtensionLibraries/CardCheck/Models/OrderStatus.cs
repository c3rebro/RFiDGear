using System;

namespace RFiDGear.ExtensionLibraries.CardCheck
{
    /// <summary>
    /// Where an order stands in the external order system. The values and their names are the ones the system
    /// stores as text in the status column; this library only ever writes <see cref="CheckFinished"/>.
    /// </summary>
    public enum OrderStatus
    {
        /// <summary>No status, or one this library does not know.</summary>
        NA,

        /// <summary>The order was created.</summary>
        Created,

        /// <summary>The order was confirmed.</summary>
        OrderConfirmed,

        /// <summary>The order is ready to be exported.</summary>
        ExportReady,

        /// <summary>A check is in progress.</summary>
        InProgress,

        /// <summary>The customer was asked for feedback.</summary>
        RequestCustomerFeedback,

        /// <summary>The check is finished and its report is stored.</summary>
        CheckFinished,

        /// <summary>A mail is to be sent.</summary>
        SendMail,

        /// <summary>The order is ready for shipping.</summary>
        ReadyForShipping,

        /// <summary>The order is closed.</summary>
        Closed,

        /// <summary>The order was cancelled.</summary>
        Cancelled,
    }

    /// <summary>Conversion of the status text the order system stores.</summary>
    public static class OrderStatusText
    {
        /// <summary>The text this library writes when a check is finished.</summary>
        internal const string CheckFinished = "CheckFinished";

        /// <summary>
        /// Reads the stored status text. The text must be exactly one of the status names, ignoring case and
        /// surrounding blanks. Anything else (a missing value, an empty one, a number, two names in a list, a name
        /// the library does not know) is <see cref="OrderStatus.NA"/>; in particular a number is never taken for the
        /// value that happens to have it, and a list is never combined into a third status.
        /// </summary>
        /// <param name="text">The text from the status column; may be <c>null</c>.</param>
        /// <returns>The status, or <see cref="OrderStatus.NA"/>.</returns>
        public static OrderStatus Parse(string? text)
        {
            var trimmed = text?.Trim();

            if (string.IsNullOrEmpty(trimmed))
            {
                return OrderStatus.NA;
            }

            foreach (var name in Enum.GetNames(typeof(OrderStatus)))
            {
                if (string.Equals(name, trimmed, StringComparison.OrdinalIgnoreCase))
                {
                    return (OrderStatus)Enum.Parse(typeof(OrderStatus), name);
                }
            }

            return OrderStatus.NA;
        }
    }
}
