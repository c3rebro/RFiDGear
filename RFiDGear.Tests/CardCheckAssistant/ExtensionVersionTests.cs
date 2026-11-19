using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

using RFiDGear.ExtensionLibraries.CardCheck;
using RFiDGear.Extensions.CardCheckAssistant.Services;
using RFiDGear.Extensions.CardCheckAssistant.ViewModels;

using Xunit;

namespace RFiDGear.Tests.CardCheckAssistant
{
    /// <summary>The assistant is released on its own: its version is written in one file and read back from the files that are loaded.</summary>
    public class ExtensionVersionTests
    {
        private static readonly Assembly Extension = typeof(GuidedCheckViewModel).Assembly;
        private static readonly Assembly Library = typeof(CardCheckServices).Assembly;

        // The number that is written by hand, once, for the extension and its support library.
        private static string WrittenVersion()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "RFiDGear.sln")))
            {
                directory = directory.Parent;
            }

            Assert.NotNull(directory);
            var props = XDocument.Load(Path.Combine(directory!.FullName, "RFiDGear.ExtensionLibraries", "CardCheck", "CardCheckVersion.props"));
            var version = (string?)props.Descendants("CardCheckVersion").Single();
            Assert.False(string.IsNullOrWhiteSpace(version));
            return version!;
        }

        [Fact]
        public void TheExtensionAndItsLibrary_CarryTheNumberThatIsWrittenInTheVersionFile()
        {
            var written = WrittenVersion();

            Assert.Equal(written, ExtensionVersionInfo.VersionOf(Extension));
            Assert.Equal(written, ExtensionVersionInfo.VersionOf(Library));
            Assert.Equal(written + ".0", FileVersionInfo.GetVersionInfo(Extension.Location).FileVersion);
            Assert.Equal(written + ".0", FileVersionInfo.GetVersionInfo(Library.Location).FileVersion);
        }

        [Fact]
        public void TheAssemblyVersion_StaysWhereTheExtensionBindsToItsLibrary()
        {
            // The extension finds its library by this number; a library of another one next to it would not bind.
            Assert.Equal(new Version(1, 0, 0, 0), Extension.GetName().Version);
            Assert.Equal(new Version(1, 0, 0, 0), Library.GetName().Version);
        }

        [Fact]
        public void WhatIsRead_IsTheNumberAShortBuildAndTheFile()
        {
            var info = ExtensionVersionInfo.Read(Extension, Library, " 2.1.0 ");

            Assert.DoesNotContain("+", info.Version);
            Assert.True(info.Build.Length <= 7, "the commit is shortened");
            Assert.Equal("2.1.0", info.HostVersion);
            Assert.Equal(Extension.Location, info.Location);
            Assert.False(info.LibraryDiffers);
        }

        [Theory]
        [InlineData("1.1.0", "1.1.0", false)]
        [InlineData("1.1.0", "1.0.0", true)]
        [InlineData("1.1.0", "", false)]   // a library that is not known is not a difference
        [InlineData("", "1.1.0", false)]
        public void ALibraryOfAnotherNumber_IsAHalfFinishedUpdate(string extension, string library, bool differs)
        {
            Assert.Equal(differs, new ExtensionVersionInfo(extension, string.Empty, library, string.Empty, string.Empty).LibraryDiffers);
        }
    }
}
