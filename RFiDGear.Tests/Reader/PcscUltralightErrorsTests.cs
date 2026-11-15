using System;
using System.IO;
using LibLogicalAccess;
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.ReaderProviders;
using Xunit;

namespace RFiDGear.Tests.Reader
{
    /// <summary>
    /// What LibLogicalAccess says no with, as the normalized error. A card refuses with an error code, and the same code is a failed
    /// authentication to an authentication and a refusal to a page operation: the call decides.
    /// </summary>
    public class PcscUltralightErrorsTests
    {
        private static PcscCardRefusal Refusal(CardException.ErrorType code) => new PcscCardRefusal(code);

        [Theory]
        [InlineData(CardException.ErrorType.AUTHENTICATION_FAILURE, "AuthUltralightC", ERROR.AuthFailure)]
        [InlineData(CardException.ErrorType.SECURITY_STATUS, "AuthUltralightC", ERROR.AuthFailure)]
        [InlineData(CardException.ErrorType.COMMAND_NOT_ALLOWED, "AuthUltralightC", ERROR.AuthFailure)]
        [InlineData(CardException.ErrorType.REFERENCE_KEY_UNUSABLE, "AuthUltralightC", ERROR.AuthFailure)]
        [InlineData(CardException.ErrorType.AUTHENTICATION_FAILURE, "Read", ERROR.PermissionDenied)]
        [InlineData(CardException.ErrorType.SECURITY_STATUS, "Read", ERROR.PermissionDenied)]
        [InlineData(CardException.ErrorType.SECURITY_STATUS, "Write", ERROR.PermissionDenied)]
        [InlineData(CardException.ErrorType.COMMAND_NOT_ALLOWED, "Write", ERROR.PermissionDenied)]
        [InlineData(CardException.ErrorType.REFERENCE_KEY_UNUSABLE, "Write", ERROR.PermissionDenied)]
        [InlineData(CardException.ErrorType.WRONG_LENGTH, "Read", ERROR.ProtocolConstraint)]
        [InlineData(CardException.ErrorType.WRONG_P1_P2, "Write", ERROR.ProtocolConstraint)]
        [InlineData(CardException.ErrorType.WRONG_LE, "Read", ERROR.ProtocolConstraint)]
        [InlineData(CardException.ErrorType.WRONG_INSTRUCTION, "Read", ERROR.ProtocolConstraint)]
        [InlineData(CardException.ErrorType.WRONG_CLASS, "Write", ERROR.ProtocolConstraint)]
        [InlineData(CardException.ErrorType.INVALID_CLA_BYTE, "Read", ERROR.ProtocolConstraint)]
        [InlineData(CardException.ErrorType.COMMAND_INCOMPATIBLE, "AuthUltralightC", ERROR.ProtocolConstraint)]
        [InlineData(CardException.ErrorType.KEY_NUMBER_INVALID, "AuthUltralightC", ERROR.ProtocolConstraint)]
        [InlineData(CardException.ErrorType.FUNCTION_NOT_SUPPORTED, "Read", ERROR.ProtocolConstraint)]
        [InlineData(CardException.ErrorType.FUNCTION_NOT_SUPPORTED, "AuthUltralightC", ERROR.ProtocolConstraint)]
        [InlineData(CardException.ErrorType.EXECUTION_ERROR, "Write", ERROR.Unknown)]
        [InlineData(CardException.ErrorType.MEMORY_FAILURE, "Write", ERROR.Unknown)]
        [InlineData(CardException.ErrorType.NOT_FOUND, "Read", ERROR.Unknown)]
        [InlineData(CardException.ErrorType.AID_NOT_FOUND, "Read", ERROR.Unknown)]
        [InlineData(CardException.ErrorType.DEFAULT, "Read", ERROR.Unknown)]
        [InlineData(CardException.ErrorType.UNKOWN_ERROR, "Write", ERROR.Unknown)]
        public void ACardRefusal_IsMappedByTheCallThatThrewIt(CardException.ErrorType code, string call, ERROR expected)
        {
            Assert.Equal(expected, PcscUltralightErrors.Classify(Refusal(code), Enum.Parse<UltralightCall>(call)));
        }

        [Fact]
        public void EveryErrorCode_IsMapped_ForEveryCall()
        {
            // A code that LibLogicalAccess adds later is Unknown, never a thrown exception or a success.
            foreach (CardException.ErrorType code in Enum.GetValues(typeof(CardException.ErrorType)))
            {
                foreach (UltralightCall call in Enum.GetValues(typeof(UltralightCall)))
                {
                    Assert.NotEqual(ERROR.NoError, PcscUltralightErrors.Classify(Refusal(code), call));
                }
            }

            Assert.Equal(ERROR.Unknown, PcscUltralightErrors.Classify(Refusal((CardException.ErrorType)9999), UltralightCall.Read));
        }

        [Fact]
        public void OtherExceptions_AreTransportFaultsOrUnknown()
        {
            // A fault that is no card refusal is the conversation with the reader (the reader is gone, the card was taken away, the
            // Smart Card service stopped). A LibLogicalAccessException is one too, by its type; it cannot be built here, because the
            // exception types of LibLogicalAccess need its native library, so that one case of the classifier is not run by a test.
            Assert.Equal(ERROR.TransportError, PcscUltralightErrors.Classify(new IOException("port"), UltralightCall.Read));
            Assert.Equal(ERROR.TransportError, PcscUltralightErrors.Classify(new TimeoutException(), UltralightCall.Write));
            Assert.Equal(ERROR.TransportError, PcscUltralightErrors.Classify(new UnauthorizedAccessException(), UltralightCall.Select));
            Assert.Equal(ERROR.TransportError, PcscUltralightErrors.Classify(new ObjectDisposedException("reader"), UltralightCall.Read));
            Assert.Equal(ERROR.Unknown, PcscUltralightErrors.Classify(new InvalidOperationException(), UltralightCall.Read));
            Assert.Equal(ERROR.Unknown, PcscUltralightErrors.Classify(new ArgumentException("our own bug"), UltralightCall.Write));
        }

        [Fact]
        public void TheDescriptionForTheLog_NamesTheTypeAndTheErrorCode_NeverTheMessage()
        {
            var described = PcscUltralightErrors.Describe(Refusal(CardException.ErrorType.SECURITY_STATUS));

            Assert.Contains(nameof(PcscCardRefusal), described);
            Assert.Contains("SECURITY_STATUS", described);
            Assert.Equal(nameof(InvalidOperationException), PcscUltralightErrors.Describe(new InvalidOperationException("A1B2C3D4 secret")));
            Assert.DoesNotContain("A1B2C3D4", PcscUltralightErrors.Describe(new IOException("A1B2C3D4 secret")));
        }

        [Fact]
        public void TheRefusal_CarriesItsCode_AndNothingOfTheNativeMessage()
        {
            var refusal = Refusal(CardException.ErrorType.WRONG_LENGTH);

            Assert.Equal(CardException.ErrorType.WRONG_LENGTH, refusal.Code);
            Assert.DoesNotContain("WRONG", refusal.Message);
        }
    }
}
