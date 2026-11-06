using System;
using System.Collections.Generic;
using System.Linq;

using RFiDGear.ExtensionLibraries.CardCheck;

using Xunit;

namespace RFiDGear.Tests.CardCheck
{
    public sealed class OrderStatusTextTests
    {
        public static IEnumerable<object[]> EveryStatus() =>
            Enum.GetValues(typeof(OrderStatus)).Cast<OrderStatus>().Select(status => new object[] { status });

        [Theory]
        [MemberData(nameof(EveryStatus))]
        public void Parse_EveryStatusNameRoundTrips(OrderStatus status)
        {
            Assert.Equal(status, OrderStatusText.Parse(status.ToString()));
        }

        [Theory]
        [InlineData("checkfinished", OrderStatus.CheckFinished)]
        [InlineData("CHECKFINISHED", OrderStatus.CheckFinished)]
        [InlineData("  InProgress  ", OrderStatus.InProgress)]
        [InlineData("\tClosed\r\n", OrderStatus.Closed)]
        public void Parse_IgnoresCaseAndSurroundingBlanks(string text, OrderStatus expected)
        {
            Assert.Equal(expected, OrderStatusText.Parse(text));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("NotDefined")]
        [InlineData("SomethingTheLibraryDoesNotKnow")]
        [InlineData("CheckFinished;")]
        [InlineData("Check Finished")]
        public void Parse_AnythingThatIsNotAStatusName_IsNA(string? text)
        {
            Assert.Equal(OrderStatus.NA, OrderStatusText.Parse(text));
        }

        [Theory]
        [InlineData("0")]
        [InlineData("4")]
        [InlineData("6")]
        [InlineData("-1")]
        [InlineData("99")]
        public void Parse_ANumberIsNeverTakenForTheValueThatHappensToHaveIt(string text)
        {
            Assert.Equal(OrderStatus.NA, OrderStatusText.Parse(text));
        }

        [Theory]
        [InlineData("Created,InProgress")]
        [InlineData("Created, InProgress")]
        [InlineData("Closed,Cancelled")]
        public void Parse_AListOfNamesIsNeverCombinedIntoAThirdStatus(string text)
        {
            // Enum.TryParse would OR the values of a list into one, and 1|4 is another status.
            Assert.Equal(OrderStatus.NA, OrderStatusText.Parse(text));
        }

        [Fact]
        public void TheTextTheLibraryWritesWhenACheckIsFinished_IsTheNameOfTheStatus()
        {
            // The shared order table is read by another system: this word is part of the contract with it.
            Assert.Equal("CheckFinished", OrderStatusText.CheckFinished);
            Assert.Equal(nameof(OrderStatus.CheckFinished), OrderStatusText.CheckFinished);
            Assert.Equal(OrderStatus.CheckFinished, OrderStatusText.Parse(OrderStatusText.CheckFinished));
        }

        [Fact]
        public void TheStatusNamesAreTheOnesTheOrderSystemStores()
        {
            // Names and order as in the stored column; a rename or a reorder would change what is written and read.
            Assert.Equal(
                new[]
                {
                    "NA", "Created", "OrderConfirmed", "ExportReady", "InProgress", "RequestCustomerFeedback",
                    "CheckFinished", "SendMail", "ReadyForShipping", "Closed", "Cancelled",
                },
                Enum.GetNames(typeof(OrderStatus)));
        }
    }
}
