using System;
using System.Globalization;
using RFiDGear.Extensions.VCNEditor.ViewModel;
using Xunit;

namespace RFiDGear.Tests
{
    public class TimeParsingTests
    {
        [Fact]
        public void ParseDateTimeFromTimeText_CombinesDateAndTime()
        {
            var baseDate = new DateTime(2024, 2, 14);
            var culture = new CultureInfo("de-DE");
            var formats = new[] { "dd.MM.yyyy HH':'mm':'ss" };

            var result = TimeParsing.ParseDateTimeFromTimeText(baseDate, "13:45:00", culture, formats);

            Assert.Equal(new DateTime(2024, 2, 14, 13, 45, 0), result);
        }

        [Fact]
        public void ParseDateTimeFromTimeText_ThrowsForInvalidTime()
        {
            var baseDate = new DateTime(2024, 2, 14);
            var culture = new CultureInfo("de-DE");
            var formats = new[] { "dd.MM.yyyy HH':'mm':'ss" };

            Assert.Throws<FormatException>(() =>
                TimeParsing.ParseDateTimeFromTimeText(baseDate, "not-a-time", culture, formats));
        }
    }
}
