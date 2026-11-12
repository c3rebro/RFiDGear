using RFiDGear.Contracts;

using System;

using Xunit;

namespace RFiDGear.Tests.SecureKeys
{
    /// <summary>The small types around the provider: the answer, the request and the failure reasons.</summary>
    public class SecureKeyContractTypesTests
    {
        [Fact]
        public void A_resolved_answer_holds_the_key_and_no_failure()
        {
            using var lease = new SecureKeyLease(SecureKeyTestData.SixteenBytes());
            using var answer = SecureKeyResolution.Resolved(lease);

            Assert.True(answer.IsResolved);
            Assert.Same(lease, answer.Key);
            Assert.Null(answer.FailureReason);
        }

        [Fact]
        public void A_failed_answer_holds_the_reason_and_no_key()
        {
            var answer = SecureKeyResolution.Failed(SecureKeyFailureReason.NotAuthorized);

            Assert.False(answer.IsResolved);
            Assert.Null(answer.Key);
            Assert.Equal(SecureKeyFailureReason.NotAuthorized, answer.FailureReason);
        }

        [Fact]
        public void An_answer_without_a_key_cannot_be_called_resolved()
        {
            Assert.Throws<ArgumentNullException>(() => SecureKeyResolution.Resolved(null!));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(8)]
        [InlineData(-1)]
        public void A_failure_has_to_be_one_of_the_defined_reasons(int undefined)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => SecureKeyResolution.Failed((SecureKeyFailureReason)undefined));
        }

        [Fact]
        public void Disposing_the_answer_disposes_the_key_and_may_be_repeated()
        {
            var key = new UnknownKindOfKey();
            var answer = SecureKeyResolution.Resolved(key);

            answer.Dispose();
            answer.Dispose();
            SecureKeyResolution.Failed(SecureKeyFailureReason.Timeout).Dispose();

            Assert.Equal(2, key.Disposals);
        }

        [Fact]
        public void The_answer_prints_without_the_key()
        {
            using var lease = new SecureKeyLease(new byte[] { 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC });
            using var answer = SecureKeyResolution.Resolved(lease);

            Assert.DoesNotContain("123456", answer.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.Contains("failed: Timeout", SecureKeyResolution.Failed(SecureKeyFailureReason.Timeout).ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public void The_request_prints_what_identifies_the_slot_and_leaves_the_card_out()
        {
            var text = SecureKeyTestData.Request().ToString();

            Assert.Contains("desfire.app.current", text, StringComparison.Ordinal);
            Assert.Contains("cards/main", text, StringComparison.Ordinal);
            Assert.Contains("run-1", text, StringComparison.Ordinal);
            Assert.DoesNotContain("04AABBCCDD", text, StringComparison.Ordinal);
        }

        [Fact]
        public void A_new_request_says_nothing_about_where_the_key_is_used()
        {
            var request = new SecureKeyRequest();

            Assert.Same(SecureKeyScope.None, request.Scope);
            Assert.Null(SecureKeyScope.None.ApplicationId);
            Assert.Null(SecureKeyScope.None.KeyNumber);
            Assert.Equal(string.Empty, request.SlotId);
            Assert.False(request.Interactive);
        }

        [Fact]
        public void The_failure_reasons_keep_their_numbers()
        {
            // Compiled providers carry these as numbers; they must not change, and 0 must stay undefined.
            Assert.Equal(1, (int)SecureKeyFailureReason.NotFound);
            Assert.Equal(2, (int)SecureKeyFailureReason.NotAuthorized);
            Assert.Equal(3, (int)SecureKeyFailureReason.ProviderUnavailable);
            Assert.Equal(4, (int)SecureKeyFailureReason.Timeout);
            Assert.Equal(5, (int)SecureKeyFailureReason.Cancelled);
            Assert.Equal(6, (int)SecureKeyFailureReason.InvalidKey);
            Assert.Equal(7, (int)SecureKeyFailureReason.Unsupported);
            Assert.False(Enum.IsDefined((SecureKeyFailureReason)0));
        }
    }
}
