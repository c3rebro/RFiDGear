using System;
using System.IO;
using System.Reflection;

using RFiDGear.Infrastructure.Editions;

using Xunit;

namespace RFiDGear.Tests
{
    /// <summary>
    /// The Store edition loads the extensions that its package carries and nothing that was added later: not the machine-wide
    /// folder under ProgramData, not the packages that a user installed. The standard edition keeps both.
    /// </summary>
    public sealed class PackagedExtensionsTests : IDisposable
    {
        private readonly string _root = Directory.CreateTempSubdirectory("RFiDGearStoreExt").FullName;

        private string ProgramData => Path.Combine(_root, "ProgramData");

        private string BaseDirectory => Path.Combine(_root, "WindowsApps", "RFiDGear");

        private string UserRoot => Path.Combine(_root, "UserExtensions");

        public void Dispose() => Directory.Delete(_root, recursive: true);

        private static string UserFolderOf(MefHelper helper) =>
            (string)typeof(MefHelper).GetField("_userExtensionsPath", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(helper)!;

        // MefHelper implements IDisposable explicitly; an implicit conversion to the interface needs no cast.
        private static void Release(MefHelper helper)
        {
            IDisposable disposable = helper;
            disposable.Dispose();
        }

        [Fact]
        public void TheStandardEdition_ScansTheMachineWideFolder_CreatesIt_AndTheUsersFolder()
        {
            var helper = new MefHelper(EditionPolicy.Standard, BaseDirectory, ProgramData, UserRoot);

            try
            {
                Assert.Equal(MefHelper.BuildExtensionsPath(ProgramData, "RFiDGear"), helper.ExtensionsPath);
                Assert.True(Directory.Exists(helper.ExtensionsPath));
                Assert.Equal(UserRoot, UserFolderOf(helper));
            }
            finally
            {
                Release(helper);
            }
        }

        [Fact]
        public void TheStoreEdition_ScansOnlyThePackageFolder_AndCreatesNothing()
        {
            var helper = new MefHelper(EditionPolicy.Store, BaseDirectory, ProgramData, UserRoot);

            try
            {
                Assert.Equal(Path.Combine(BaseDirectory, "Extensions"), helper.ExtensionsPath);
                Assert.Equal(string.Empty, UserFolderOf(helper));
                Assert.False(Directory.Exists(ProgramData));
                Assert.False(Directory.Exists(helper.ExtensionsPath));
            }
            finally
            {
                Release(helper);
            }
        }

        [Fact]
        public void TheStoreEdition_FindsTheExtensionsInsideItsPackage_AndNotThoseOutside()
        {
            var packaged = Directory.CreateDirectory(Path.Combine(BaseDirectory, "Extensions")).FullName;
            var outside = Directory.CreateDirectory(MefHelper.BuildExtensionsPath(ProgramData, "RFiDGear")).FullName;
            File.WriteAllBytes(Path.Combine(packaged, "RFiDGear.Extensions.Inside.dll"), Array.Empty<byte>());
            File.WriteAllBytes(Path.Combine(outside, "RFiDGear.Extensions.Outside.dll"), Array.Empty<byte>());

            var helper = new MefHelper(EditionPolicy.Store, BaseDirectory, ProgramData, UserRoot);

            try
            {
                var found = MefHelper.GetExtensionAssemblyPaths(helper.ExtensionsPath);

                Assert.Equal(new[] { Path.Combine(packaged, "RFiDGear.Extensions.Inside.dll") }, found);
            }
            finally
            {
                Release(helper);
            }
        }
    }
}
