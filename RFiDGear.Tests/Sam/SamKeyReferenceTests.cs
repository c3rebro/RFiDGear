#nullable enable
using RFiDGear.Services.Sam;

using System;

using Xunit;

namespace RFiDGear.Tests.Sam
{
    /// <summary>The reference to a key that stays in a SAM: the entry and the version, as a task stores them.</summary>
    public class SamKeyReferenceTests
    {
        [Theory]
        [InlineData("1:0", 1, 0)]
        [InlineData("2:0", 2, 0)]
        [InlineData("127:255", 127, 255)]
        [InlineData("64:7", 64, 7)]
        [InlineData("3:10", 3, 10)]
        public void A_reference_is_the_entry_and_the_version_in_decimal(string text, int entry, int version)
        {
            Assert.True(SamKeyReference.TryParse(text, out var reference));
            Assert.Equal(entry, reference.Entry);
            Assert.Equal(version, reference.Version);
            Assert.True(reference.IsValid);
            Assert.Equal(text, reference.Format());
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(":")]
        [InlineData("1")]
        [InlineData("1:")]
        [InlineData(":1")]
        [InlineData("0:0")]
        [InlineData("128:0")]
        [InlineData("1:256")]
        [InlineData("1:-1")]
        [InlineData("-1:0")]
        [InlineData("+1:0")]
        [InlineData("01:0")]
        [InlineData("1:00")]
        [InlineData(" 1:0")]
        [InlineData("1:0 ")]
        [InlineData("1: 0")]
        [InlineData("1:0\n")]
        [InlineData("1:0:0")]
        [InlineData("a:b")]
        [InlineData("1,0")]
        [InlineData("0x1:0")]
        [InlineData("1000:0")]
        [InlineData("\uFF11:0")]
        public void Anything_else_is_no_reference(string? text)
        {
            Assert.False(SamKeyReference.TryParse(text, out var reference));
            Assert.False(reference.IsValid);
            Assert.Equal(0, reference.Entry);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(128)]
        [InlineData(255)]
        public void The_master_entry_and_numbers_outside_the_sam_are_no_reference(int entry)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SamKeyReference((byte)entry, 0));
        }

        [Fact]
        public void A_reference_is_never_shown_and_two_of_the_same_key_are_equal()
        {
            var a = new SamKeyReference(5, 3);
            var b = new SamKeyReference(5, 3);

            Assert.Equal(a, b);
            Assert.True(a == b);
            Assert.True(a != new SamKeyReference(5, 4));
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
            Assert.DoesNotContain("5", a.ToString(), StringComparison.Ordinal);
            Assert.Equal("sam", SamKeyReference.ProviderId);
            Assert.False(default(SamKeyReference).IsValid);
        }
    }
}