using System;

using RFiDGear.ExtensionLibraries.CardCheck;

using Xunit;

namespace RFiDGear.Tests.CardCheck
{
    public sealed class ResultTests
    {
        private static readonly Failure SomeFailure = new Failure(FailureKind.NotFound, "order.notFound", "No such order.");

        [Fact]
        public void Ok_IsASuccessWithoutAFailure()
        {
            var result = Result.Ok();

            Assert.True(result.IsSuccess);
            Assert.Null(result.Failure);
        }

        [Fact]
        public void Fail_IsAFailureThatCarriesTheGivenFailure()
        {
            var result = Result.Fail(SomeFailure);

            Assert.False(result.IsSuccess);
            Assert.Same(SomeFailure, result.Failure);
        }

        [Fact]
        public void Fail_WithoutAFailure_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => Result.Fail(null!));
            Assert.Throws<ArgumentNullException>(() => Result<int>.Fail(null!));
        }

        [Fact]
        public void GenericOk_CarriesTheValue()
        {
            var result = Result<string>.Ok("value");

            Assert.True(result.IsSuccess);
            Assert.Null(result.Failure);
            Assert.Equal("value", result.Value);
        }

        [Fact]
        public void GenericOk_AcceptsAnEmptyOrDefaultValueAsASuccess()
        {
            Assert.True(Result<int>.Ok(0).IsSuccess);
            Assert.Equal(string.Empty, Result<string>.Ok(string.Empty).Value);
        }

        [Fact]
        public void GenericFail_HasNoValue_AndSaysWhichFailureInsteadOfReturningADefault()
        {
            var result = Result<string>.Fail(SomeFailure);

            Assert.False(result.IsSuccess);
            Assert.Same(SomeFailure, result.Failure);
            var exception = Assert.Throws<InvalidOperationException>(() => result.Value);
            Assert.Contains("order.notFound", exception.Message, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Failure_WithoutACode_Throws(string? code)
        {
            Assert.Throws<ArgumentException>(() => new Failure(FailureKind.Unexpected, code!));
        }

        [Fact]
        public void Failure_WithoutAMessage_HasAnEmptyMessageNotNull()
        {
            Assert.Equal(string.Empty, new Failure(FailureKind.Timeout, "db.timeout").SafeMessage);
            Assert.Equal(string.Empty, new Failure(FailureKind.Timeout, "db.timeout", null).SafeMessage);
        }

        [Fact]
        public void Failure_IsComparedByItsContent()
        {
            var a = new Failure(FailureKind.Io, "report.io", "Could not write the report.");
            var b = new Failure(FailureKind.Io, "report.io", "Could not write the report.");
            var other = new Failure(FailureKind.Io, "report.io.other", "Could not write the report.");

            Assert.Equal(a, b);
            Assert.NotEqual(a, other);
        }
    }
}
