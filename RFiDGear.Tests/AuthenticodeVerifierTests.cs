using System;
using System.IO;

using RFiDGear.Infrastructure.ExtensionPackages;
using RFiDGear.Tests.CardCheck;

using Xunit;

namespace RFiDGear.Tests
{
    /// <summary>
    /// The check of the signature of a file, with the operating system's own: a program that Microsoft signed holds, the same
    /// program with one byte changed does not, and a file that nobody signed is only unsigned. The signed file is the dotnet.exe that
    /// runs the tests.
    /// </summary>
    public sealed class AuthenticodeVerifierTests : IDisposable
    {
        private readonly TempFolder _folder = new TempFolder();

        public void Dispose() => _folder.Dispose();

        private static string SignedProgram => DotNetTool.Executable();

        // The program with a byte of its code changed after it was signed: the signature is still in it, and no longer fits.
        private string ChangedCopyOfTheSignedProgram()
        {
            Directory.CreateDirectory(_folder.Path);
            var copy = Path.Combine(_folder.Path, "dotnet-changed.exe");
            var bytes = File.ReadAllBytes(SignedProgram);
            bytes[bytes.Length / 4] ^= 0xFF;
            File.WriteAllBytes(copy, bytes);
            return copy;
        }

        private string UnsignedFile()
        {
            Directory.CreateDirectory(_folder.Path);
            var file = Path.Combine(_folder.Path, "not-signed.dll");
            File.WriteAllText(file, "nobody signed this");
            return file;
        }

        [Fact]
        public void AProgramThatMicrosoftSigned_HasAValidSignature()
        {
            Assert.Equal(SignatureCheck.Valid, AuthenticodeVerifier.Check(SignedProgram));
        }

        [Fact]
        public void ThatProgramWithAByteChanged_HasASignatureThatDoesNotHold()
        {
            Assert.Equal(SignatureCheck.Invalid, AuthenticodeVerifier.Check(ChangedCopyOfTheSignedProgram()));
        }

        [Fact]
        public void AFileThatNobodySigned_IsUnsigned_AndAMissingFileIsNotValid()
        {
            Assert.Equal(SignatureCheck.Unsigned, AuthenticodeVerifier.Check(UnsignedFile()));
            Assert.NotEqual(SignatureCheck.Valid, AuthenticodeVerifier.Check(Path.Combine(_folder.Path, "no-such-file.dll")));
        }

        [Theory]
        [InlineData(0, "Valid")]
        [InlineData(unchecked((int)0x800B0100), "Unsigned")]   // TRUST_E_NOSIGNATURE
        [InlineData(unchecked((int)0x800B0001), "Unsigned")]   // TRUST_E_PROVIDER_UNKNOWN
        [InlineData(unchecked((int)0x800B0003), "Unsigned")]   // TRUST_E_SUBJECT_FORM_UNKNOWN
        [InlineData(unchecked((int)0x80096010), "Invalid")]    // TRUST_E_BAD_DIGEST: the file was changed
        [InlineData(unchecked((int)0x800B0109), "Invalid")]    // CERT_E_UNTRUSTEDROOT
        [InlineData(unchecked((int)0x800B0101), "Invalid")]    // CERT_E_EXPIRED
        public void WhatTheSystemAnswers_IsToldAsValidUnsignedOrInvalid(int answer, string expected)
        {
            Assert.Equal(Enum.Parse<SignatureCheck>(expected), AuthenticodeVerifier.Classify(answer));
        }

        [Fact]
        public void ThePersonIsToldWhoSigned_AndWhetherTheSignatureHolds()
        {
            var signed = ExtensionPackageInstaller.ReadSigner(SignedProgram);
            Assert.NotNull(signed);
            Assert.False(string.IsNullOrWhiteSpace(signed!.Name));
            Assert.NotEqual("?", signed.Name);   // the name of the certificate was read
            Assert.True(signed.SignatureValid);

            var changed = ExtensionPackageInstaller.ReadSigner(ChangedCopyOfTheSignedProgram());
            Assert.NotNull(changed);   // it still names its signer
            Assert.Equal(signed.Name, changed!.Name);
            Assert.False(changed.SignatureValid);

            Assert.Null(ExtensionPackageInstaller.ReadSigner(UnsignedFile()));
        }

        [Fact]
        public void OnlyOneProcessTidiesTheFolder_UntilItLetsGo()
        {
            Directory.CreateDirectory(_folder.Path);
            var lockFile = Path.Combine(_folder.Path, ExtensionFolderGuard.LockFileName);
            using var first = new ExtensionFolderGuard(lockFile);
            using var second = new ExtensionFolderGuard(lockFile);   // what a second RFiDGear has

            Assert.True(first.TryTake());
            Assert.True(first.TryTake());    // again, in the same process: still its own
            Assert.False(second.TryTake());

            first.Dispose();
            Assert.True(second.TryTake());
        }
    }
}
