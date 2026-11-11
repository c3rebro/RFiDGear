using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

using RFiDGear.ExtensionLibraries.CardCheck;
using RFiDGear.Extensions.CardCheckAssistant.Services;
using RFiDGear.Tests.CardCheck;

using Xunit;

namespace RFiDGear.Tests.CardCheckAssistant
{
    public sealed class ShellOpenerTests : IDisposable
    {
        private readonly TempFolder _folder = new TempFolder();
        private readonly List<ProcessStartInfo> _started = new List<ProcessStartInfo>();
        private Failure? _startFails;

        public void Dispose() => _folder.Dispose();

        private ShellOpener Opener() => new ShellOpener(info =>
        {
            _started.Add(info);

            return _startFails;
        });

        private string Make(string name)
        {
            var path = Path.Combine(_folder.Path, name);
            File.WriteAllText(path, "content");

            return path;
        }

        [Fact]
        public void AnExistingPdf_IsOpenedAsTheDocumentItself_WithNoProgramAndNoArguments()
        {
            var path = Make("job-1.step1.pdf");

            var result = Opener().Open(path);

            Assert.True(result.IsSuccess);
            var info = Assert.Single(_started);
            Assert.Equal(Path.GetFullPath(path), info.FileName);
            Assert.True(info.UseShellExecute);
            Assert.Equal(string.Empty, info.Arguments);
            Assert.Equal(string.Empty, info.Verb);
            Assert.Equal(string.Empty, info.WorkingDirectory);
            Assert.Empty(info.ArgumentList);
        }

        [Fact]
        public void WhatIsStarted_IsThePathAsTheSystemSpellsIt_NotAsItWasGiven()
        {
            Make("a.pdf");
            var asGiven = Path.Combine(_folder.Path, "sub", "..", "a.pdf");
            Directory.CreateDirectory(Path.Combine(_folder.Path, "sub"));

            Assert.True(Opener().Open(asGiven).IsSuccess);

            var started = Assert.Single(_started).FileName;
            Assert.Equal(Path.Combine(_folder.Path, "a.pdf"), started);
            Assert.DoesNotContain("..", started, StringComparison.Ordinal);
        }

        [Fact]
        public void Describe_IsTheSameForEveryPath_AndNeverNamesAProgram()
        {
            var info = ShellOpener.Describe(@"C:\reports\a.pdf");

            Assert.Equal(@"C:\reports\a.pdf", info.FileName);
            Assert.True(info.UseShellExecute);
            Assert.Equal(string.Empty, info.Arguments);
            Assert.Equal(string.Empty, info.Verb);
            Assert.Empty(info.ArgumentList);
            Assert.False(info.RedirectStandardOutput);
        }

        [Fact]
        public void TheExtensionIsNotCaseSensitive()
        {
            Assert.True(Opener().Open(Make("REPORT.PDF")).IsSuccess);
        }

        [Fact]
        public void AnExistingFolder_IsOpened()
        {
            var result = Opener().Open(_folder.Path);

            Assert.True(result.IsSuccess);
            Assert.Equal(Path.GetFullPath(_folder.Path), Assert.Single(_started).FileName);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("report.pdf")]
        [InlineData("..\\report.pdf")]
        [InlineData("C:report.pdf")]
        public void APathThatIsNotAFullPath_IsRefused_AndNothingIsStarted(string? path)
        {
            var result = Opener().Open(path!);

            Assert.False(result.IsSuccess);
            Assert.Equal(OpenerFailureCodes.PathInvalid, result.Failure!.Code);
            Assert.Empty(_started);
        }

        [Theory]
        [InlineData("tool.exe")]
        [InlineData("run.bat")]
        [InlineData("run.cmd")]
        [InlineData("script.ps1")]
        [InlineData("link.lnk")]
        [InlineData("link.url")]
        [InlineData("page.html")]
        [InlineData("library.dll")]
        [InlineData("setup.msi")]
        [InlineData("notes.txt")]
        [InlineData("report.pdf.exe")]
        [InlineData("report")]
        public void AFileThatIsNotAPdf_IsRefused_AndNothingIsStarted(string name)
        {
            var result = Opener().Open(Make(name));

            Assert.False(result.IsSuccess);
            Assert.Equal(OpenerFailureCodes.NotAllowed, result.Failure!.Code);
            Assert.Empty(_started);
        }

        [Fact]
        public void APdfThatIsNotThere_IsRefused_AsNotFound()
        {
            var result = Opener().Open(Path.Combine(_folder.Path, "missing.pdf"));

            Assert.False(result.IsSuccess);
            Assert.Equal(OpenerFailureCodes.NotFound, result.Failure!.Code);
            Assert.Empty(_started);
        }

        [Theory]
        [InlineData(@"\\.\C:")]
        [InlineData(@"\\?\C:\Windows\notepad.pdf")]
        [InlineData(@"\\.\pipe\something")]
        [InlineData(@"\??\C:\Windows\notepad.pdf")]
        [InlineData(@"\??\UNC\server\share\a.pdf")]
        [InlineData(@"C:\Temp\report.pdf:stream.exe")]
        [InlineData(@"C:\Temp\report.exe:stream.pdf")]
        public void ADevicePathOrAStream_IsRefused_AndNothingIsStarted(string path)
        {
            var result = Opener().Open(path);

            Assert.False(result.IsSuccess);
            Assert.Equal(OpenerFailureCodes.NotAllowed, result.Failure!.Code);
            Assert.Empty(_started);
        }

        [Fact]
        public void AStartThatFails_IsAFailureWithItsCode_NotAnException()
        {
            _startFails = new Failure(FailureKind.NotFound, OpenerFailureCodes.NoViewer, "No program.");

            var result = Opener().Open(Make("a.pdf"));

            Assert.False(result.IsSuccess);
            Assert.Equal(OpenerFailureCodes.NoViewer, result.Failure!.Code);
            Assert.Single(_started);
        }

        [Theory]
        [InlineData(31, OpenerFailureCodes.NoViewer)]
        [InlineData(1155, OpenerFailureCodes.NoViewer)]
        [InlineData(2, OpenerFailureCodes.NotFound)]
        [InlineData(3, OpenerFailureCodes.NotFound)]
        [InlineData(5, OpenerFailureCodes.StartFailed)]
        [InlineData(1223, OpenerFailureCodes.StartFailed)]
        public void WhatTheShellSaysNo_IsToldByItsError_NotAlwaysAsNoViewer(int nativeErrorCode, string code)
        {
            Assert.Equal(code, ShellOpener.FailureOf(nativeErrorCode).Code);
        }

        [Fact]
        public void TheOpener_NeedsAWayToStart()
        {
            Assert.Throws<ArgumentNullException>(() => new ShellOpener(null!));
        }
    }
}
