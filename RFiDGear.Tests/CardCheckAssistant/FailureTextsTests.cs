using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using RFiDGear.ExtensionLibraries.CardCheck;
using RFiDGear.Extensions.CardCheckAssistant.Localization;
using RFiDGear.Extensions.CardCheckAssistant.Workflow;

using Xunit;

using Keys = RFiDGear.Extensions.CardCheckAssistant.Localization.ExtensionStrings.Keys;

namespace RFiDGear.Tests.CardCheckAssistant
{
    public sealed class FailureTextsTests
    {
        // Codes that can reach the page but have nothing to tell the operator beyond what the generic text says: the
        // request was malformed or a template step that this version does not run. A new code lands here or gets a text.
        private static readonly string[] GenericOnly =
        {
            CheckFailureCodes.RequestInvalid,
            CheckFailureCodes.TemplateMissing,
            CheckFailureCodes.LoadUnknown,
        };

        private static IEnumerable<string> CodesOf(Type codes) => codes
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!);

        [Fact]
        public void EveryCodeOfTheWorkflow_TheRunAndTheContext_HasATextOfItsOwn_OrIsNamedAsGenericHere()
        {
            var codes = CodesOf(typeof(WorkflowFailureCodes))
                .Concat(CodesOf(typeof(CheckFailureCodes)))
                .Concat(CodesOf(typeof(ContextFailureCodes)))
                .ToList();

            Assert.NotEmpty(codes);
            Assert.All(codes, code => Assert.True(FailureTexts.HasExactText(code) || GenericOnly.Contains(code), "No text for " + code));
            Assert.All(GenericOnly, code => Assert.False(FailureTexts.HasExactText(code), code + " has a text; take it off the list."));
        }

        [Theory]
        [InlineData("en-US")]
        [InlineData("de-DE")]
        public void ACodeWithATextOfItsOwn_GetsItInTheLanguageOfThePage(string culture)
        {
            var strings = new LocalizedStrings(culture);
            var failure = new Failure(FailureKind.NotFound, WorkflowFailureCodes.OrderInUse, "English safe message");

            var text = FailureTexts.Describe(failure, strings);

            Assert.Equal(ExtensionStrings.Get(Keys.FailOrderInUse, culture), text);
            Assert.DoesNotContain("English safe message", text, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("report.lock.denied", Keys.FailReportDenied)]
        [InlineData("report.analyze.denied", Keys.FailReportDenied)]
        [InlineData("report.write.io", Keys.FailReportIo)]
        [InlineData("report.stale.io", Keys.FailReportIo)]
        [InlineData("orders.finalize.unexpected", Keys.FailUnexpected)]
        [InlineData("report.analyze.unexpected", Keys.FailUnexpected)]
        public void ACodeWithoutATextOfItsOwn_GetsTheTextOfItsFamily(string code, string key)
        {
            var strings = new LocalizedStrings("en-US");

            Assert.Equal(strings[key], FailureTexts.Describe(new Failure(FailureKind.Io, code, "ignored"), strings));
        }

        [Theory]
        [InlineData("en-US")]
        [InlineData("de-DE")]
        public void ACodeNobodyKnows_GetsTheGenericText_WithTheSafeMessage(string culture)
        {
            var strings = new LocalizedStrings(culture);

            var text = FailureTexts.Describe(new Failure(FailureKind.Unexpected, "something.new", "the safe message"), strings);

            Assert.Contains("the safe message", text, StringComparison.Ordinal);
            Assert.StartsWith(ExtensionStrings.Get(Keys.FailGeneric, culture).Split('{')[0], text, StringComparison.Ordinal);
        }

        [Fact]
        public void TheKindOfAFailure_DoesNotChooseTheText()
        {
            var strings = new LocalizedStrings("en-US");
            var reader = new Failure(FailureKind.Unreachable, WorkflowFailureCodes.CardUnclear, "x");
            var host = new Failure(FailureKind.Unreachable, CheckFailureCodes.LoadBusy, "x");
            var orders = new Failure(FailureKind.Unreachable, "orders.unreachable", "x");

            var texts = new[] { reader, host, orders }.Select(failure => FailureTexts.Describe(failure, strings)).Distinct().ToList();

            Assert.Equal(3, texts.Count);
        }

        [Fact]
        public void Arguments_AreChecked()
        {
            Assert.Throws<ArgumentNullException>(() => FailureTexts.Describe(null!, new LocalizedStrings("en-US")));
            Assert.Throws<ArgumentNullException>(() => FailureTexts.Describe(new Failure(FailureKind.Unexpected, "x", "y"), null!));
        }
    }
}
