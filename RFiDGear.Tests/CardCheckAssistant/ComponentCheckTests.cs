using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Controls;

using RFiDGear.Extensions.CardCheckAssistant;
using RFiDGear.Extensions.CardCheckAssistant.Services;
using RFiDGear.Extensions.CardCheckAssistant.Views;

using Xunit;

namespace RFiDGear.Tests.CardCheckAssistant
{
    /// <summary>
    /// The extension's support library may be missing or may not fit (it is a separate file beside the extension, and
    /// MEF cannot see that). The page must then say so, create nothing else, and never let the exception out.
    /// </summary>
    public class ComponentCheckTests
    {
        private const string LibraryName = "RFiDGear.ExtensionLibraries.CardCheck";

        [Theory]
        [InlineData(typeof(FileNotFoundException))]
        [InlineData(typeof(FileLoadException))]
        [InlineData(typeof(BadImageFormatException))]
        [InlineData(typeof(TypeLoadException))]
        [InlineData(typeof(MissingMethodException))]
        public void TheLibraryThatCannotBeUsed_IsNotAvailable(Type failure)
        {
            Assert.False(ComponentCheck.IsAvailable(() => throw (Exception)Activator.CreateInstance(failure)!));
        }

        [Fact]
        public void AnyOtherFault_IsNotHidden_AndTheRealLibraryIsAvailable()
        {
            Assert.Throws<InvalidOperationException>(() => ComponentCheck.IsAvailable(() => throw new InvalidOperationException()));
            Assert.True(ComponentCheck.IsAvailable(ComponentCheck.TouchLibrary));
        }

        [Fact]
        public async Task WithoutTheLibrary_TheContentIsAPageThatSaysSo_InTheHostsLanguage_AndNothingElseIsCreated()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var german = new FakeExtensionHost("de-DE");
                var english = new FakeExtensionHost("en-US");

                var inGerman = Assert.IsType<TextBlock>(CardCheckAssistantExtension.CreateContent(german, () => throw new FileNotFoundException()));
                var inEnglish = Assert.IsType<TextBlock>(CardCheckAssistantExtension.CreateContent(english, () => throw new BadImageFormatException()));

                Assert.Contains("fehlt", inGerman.Text, StringComparison.Ordinal);
                Assert.Contains("was not found", inEnglish.Text, StringComparison.Ordinal);
                Assert.All(new[] { inGerman.Text, inEnglish.Text }, text =>
                {
                    Assert.Contains(ComponentCheck.LibraryFileName, text, StringComparison.Ordinal);
                    Assert.Contains(typeof(CardCheckAssistantExtension).Assembly.GetName().Name + ".dll", text, StringComparison.Ordinal);
                });
                Assert.Equal(0, german.PageSubscriberCount + english.PageSubscriberCount);   // no view model, nothing subscribed
            });
        }

        [Fact]
        public async Task WithTheLibrary_TheContentIsTheNormalPage()
        {
            await StaTestRunner.RunOnStaThreadAsync(() =>
            {
                var host = new FakeExtensionHost();

                var content = CardCheckAssistantExtension.CreateContent(host, ComponentCheck.TouchLibrary);

                try
                {
                    Assert.IsType<CardCheckAssistantView>(content);
                }
                finally
                {
                    (content as IDisposable)?.Dispose();
                }
            });
        }

        [Fact]
        public void TheExportedClassAndTheCheck_MentionNoLibraryTypeInAnyMember()
        {
            // MEF creates the exported class without the library; a library type in its fields, signatures or
            // constructor would fail when the class is created, before the check can run.
            const BindingFlags all = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            var types = new[] { typeof(CardCheckAssistantExtension), typeof(ComponentCheck), typeof(MissingComponentPage) }
                .SelectMany(owner => owner.GetMembers(all).SelectMany(MemberTypes))
                .ToList();

            Assert.NotEmpty(types);
            Assert.DoesNotContain(types, type => type.Assembly.GetName().Name == LibraryName);
        }

        private static Type[] MemberTypes(MemberInfo member)
        {
            switch (member)
            {
                case FieldInfo field:
                    return new[] { field.FieldType };
                case PropertyInfo property:
                    return new[] { property.PropertyType };
                case MethodBase method:
                    var parameters = method.GetParameters().Select(parameter => parameter.ParameterType);
                    return (method is MethodInfo info ? parameters.Append(info.ReturnType) : parameters).ToArray();
                default:
                    return Type.EmptyTypes;
            }
        }
    }
}
