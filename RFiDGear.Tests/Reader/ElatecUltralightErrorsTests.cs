using System;
using System.IO;
using Elatec.NET;
using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.ReaderProviders;
using Xunit;

namespace RFiDGear.Tests.Reader
{
    /// <summary>
    /// What Elatec.NET throws, as the normalized error. The library names the reason only in the text of the exception ("... error
    /// AccessDenied"; <c>ErrorNumber</c> is always <c>None</c> in 0.7.0-beta.1), and says <c>AccessDenied</c> both for a page the
    /// chip refused and for a password it did not take: the call decides which.
    /// </summary>
    public class ElatecUltralightErrorsTests
    {
        private static ReaderException Text(string error) => new ReaderException("Call was not successfull, error " + error, null);

        [Theory]
        [InlineData("AccessDenied", "Read", ERROR.PermissionDenied)]
        [InlineData("AccessDenied", "Write", ERROR.PermissionDenied)]
        [InlineData("AccessDenied", "AuthPassword", ERROR.AuthFailure)]
        [InlineData("AccessDenied", "AuthUltralightC", ERROR.AuthFailure)]
        [InlineData("NotOpen", "Write", ERROR.TransportError)]
        [InlineData("NotInit", "Read", ERROR.TransportError)]
        [InlineData("Timeout", "Select", ERROR.TransportError)]
        [InlineData("NotSupported", "Read", ERROR.ProtocolConstraint)]
        [InlineData("Range", "Write", ERROR.ProtocolConstraint)]
        [InlineData("Parameter", "AuthPassword", ERROR.ProtocolConstraint)]
        [InlineData("General", "Read", ERROR.Unknown)]
        [InlineData("State", "Write", ERROR.Unknown)]
        [InlineData("Data", "Version", ERROR.Unknown)]
        public void AReaderError_InTheTextOfTheException_IsMappedByTheCallThatThrewIt(string readerError, string call, ERROR expected)
        {
            Assert.Equal(expected, ElatecUltralightErrors.Classify(Text(readerError), Enum.Parse<UltralightCall>(call)));
        }

        [Fact]
        public void TheErrorNumber_IsUsedWhenTheLibrarySetsIt_AndWinsOverTheText()
        {
            // A fixed library sets ErrorNumber; the text can say something else, or nothing that can be read.
            Assert.Equal(
                ERROR.PermissionDenied,
                ElatecUltralightErrors.Classify(new ReaderException("odd text", ReaderError.AccessDenied), UltralightCall.Write));
            Assert.Equal(
                ERROR.PermissionDenied,
                ElatecUltralightErrors.Classify(new ReaderException("Call was not successfull, error NotOpen", ReaderError.AccessDenied), UltralightCall.Write));
            Assert.Equal(
                ERROR.AuthFailure,
                ElatecUltralightErrors.Classify(new ReaderException("odd text", ReaderError.AccessDenied), UltralightCall.AuthPassword));
        }

        [Theory]
        [InlineData("Reader not connected")]
        [InlineData("Call was not successfull, error Bogus")]
        [InlineData("Call was not successfull, error 206")]
        [InlineData("Call was not successfull, error None")]
        [InlineData("Call was not successfull, error ")]
        [InlineData("")]
        public void AReaderException_ThatNamesNoReaderError_IsUnknown(string message)
        {
            var exception = new ReaderException(message, null);

            Assert.False(ElatecUltralightErrors.TryGetReaderError(exception, out _));
            Assert.Equal(ERROR.Unknown, ElatecUltralightErrors.Classify(exception, UltralightCall.Read));
        }

        [Fact]
        public void TryGetReaderError_ReadsTheNameAtTheEndOfTheText()
        {
            Assert.True(ElatecUltralightErrors.TryGetReaderError(Text("AccessDenied"), out var error));
            Assert.Equal(ReaderError.AccessDenied, error);
            Assert.False(ElatecUltralightErrors.TryGetReaderError(new InvalidOperationException("error AccessDenied"), out _));
        }

        [Fact]
        public void OtherExceptions_AreTransportFaultsOrUnknown()
        {
            Assert.Equal(ERROR.TransportError, ElatecUltralightErrors.Classify(new IOException("port"), UltralightCall.Read));
            Assert.Equal(ERROR.TransportError, ElatecUltralightErrors.Classify(new TimeoutException(), UltralightCall.Write));
            Assert.Equal(ERROR.TransportError, ElatecUltralightErrors.Classify(new UnauthorizedAccessException(), UltralightCall.Select));
            Assert.Equal(ERROR.TransportError, ElatecUltralightErrors.Classify(new ObjectDisposedException("reader"), UltralightCall.Read));
            Assert.Equal(ERROR.TransportError, ElatecUltralightErrors.Classify(new TwnException("reader said no"), UltralightCall.Read));
            Assert.Equal(ERROR.Unknown, ElatecUltralightErrors.Classify(new InvalidOperationException(), UltralightCall.Read));
            Assert.Equal(ERROR.Unknown, ElatecUltralightErrors.Classify(new ArgumentException("our own bug"), UltralightCall.Write));
        }

        [Fact]
        public void TheDescriptionForTheLog_NamesTheTypeAndTheReaderError_NeverTheMessage()
        {
            var described = ElatecUltralightErrors.Describe(new ReaderException("secret A1B2C3D4 Call was not successfull, error AccessDenied", null));

            Assert.Contains(nameof(ReaderException), described);
            Assert.Contains("AccessDenied", described);
            Assert.DoesNotContain("A1B2C3D4", described);
            Assert.DoesNotContain("secret", described);
            Assert.Equal(nameof(InvalidOperationException), ElatecUltralightErrors.Describe(new InvalidOperationException("A1B2C3D4")));
        }
    }
}
