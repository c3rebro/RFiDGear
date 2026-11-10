using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Controls;

using RFiDGear.ExtensionLibraries.CardCheck;
using RFiDGear.Extensions.CardCheckAssistant;
using RFiDGear.Extensions.CardCheckAssistant.Services;
using RFiDGear.Extensions.CardCheckAssistant.ViewModels;
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
                    Assert.IsType<GuidedCheckView>(content);
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
            var owners = new[] { typeof(CardCheckAssistantExtension), typeof(ComponentCheck), typeof(MissingComponentPage) };
            var types = owners
                .SelectMany(owner => owner.GetMembers(all).SelectMany(MemberTypes)
                    .Concat(owner.GetInterfaces())
                    .Append(owner.BaseType!))
                .SelectMany(Flatten)
                .ToList();

            Assert.NotEmpty(types);
            Assert.DoesNotContain(types, type => type.Assembly.GetName().Name == LibraryName);
        }

        [Fact]
        public void ThePageTypes_HoldNoLibraryEnumOrStructInAField()
        {
            // The page may name library classes (it is only created after the check), but an enum or a struct in a field
            // makes the runtime load the library when the type is loaded, which can be before the check has run.
            const BindingFlags fields = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            var pageTypes = new[] { typeof(GuidedCheckView), typeof(GuidedCheckViewModel), typeof(AssistantServices) };
            var offending = pageTypes
                .SelectMany(type => type.GetFields(fields))
                .Where(field => field.FieldType.IsValueType && field.FieldType.Assembly.GetName().Name == LibraryName)
                .Select(field => field.DeclaringType!.Name + "." + field.Name)
                .ToList();

            Assert.Empty(offending);
        }

        [Fact]
        public void TheTypeWalk_SeesALibraryTypeInsideGenericsArraysAndReferences()
        {
            // Without this the check above would let `Func<ICardCheckLog>` or `List<ICardCheckLog>` through: the
            // outer type is a framework type, the dependency is in its argument.
            foreach (var type in new[] { typeof(Func<ICardCheckLog>), typeof(List<ICardCheckLog>), typeof(ICardCheckLog[]), typeof(Func<List<ICardCheckLog[]>>) })
            {
                Assert.Contains(Flatten(type), inner => inner == typeof(ICardCheckLog));
            }
        }

        // The type and everything it is made of: element types and generic arguments, all the way down.
        private static IEnumerable<Type> Flatten(Type type)
        {
            yield return type;

            if (type.HasElementType)
            {
                foreach (var inner in Flatten(type.GetElementType()!))
                {
                    yield return inner;
                }
            }

            if (type.IsGenericType)
            {
                foreach (var inner in type.GetGenericArguments().SelectMany(Flatten))
                {
                    yield return inner;
                }
            }
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
