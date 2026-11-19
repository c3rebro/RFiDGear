using System.Collections.Generic;

using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.Tasks;

using Xunit;

namespace RFiDGear.Tests
{
    /// <summary>
    /// What a Check UID task decides: a typed UID is a question (the answer "no" is <see cref="ERROR.IsNotTrue"/>), a placeholder
    /// such as <c>$EXPECTEDUID</c> is the caller's statement of which card to process (anything but the UID of this card is
    /// <see cref="ERROR.CardNotExpected"/>), and nothing that is not a UID of this card ever passes.
    /// </summary>
    public sealed class ExpectedUidTests
    {
        private const string Card = "0123456789AB";

        private static IReadOnlyDictionary<string, string> Variables(string name, string value) => new Dictionary<string, string> { [name] = value };

        [Theory]
        [InlineData("$EXPECTEDUID", true)]
        [InlineData("  $EXPECTEDUID ", true)]
        [InlineData("$a", true)]
        [InlineData("$A_1", true)]
        [InlineData("0123456789AB", false)]
        [InlineData("$", false)]
        [InlineData("$EXPECTED UID", false)]
        [InlineData("$EXPECTED-UID", false)]
        [InlineData("EXPECTEDUID$", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void AVariableIsADollarSignAndAName_AndNothingElse(string text, bool isVariable)
        {
            Assert.Equal(isVariable, ExpectedUid.IsVariableReference(text));
        }

        [Theory]
        [InlineData("0123456789AB")]
        [InlineData("0123456789ab")]
        [InlineData(" 0123456789AB ")]
        public void ATypedUidOfTheCard_Passes(string typed)
        {
            Assert.Equal(ERROR.NoError, ExpectedUid.Check(typed, Card, null));
        }

        [Theory]
        [InlineData("FFFFFFFFFFFF")]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void ATypedUidThatIsNotTheCards_IsTheAnswerNo(string typed)
        {
            Assert.Equal(ERROR.IsNotTrue, ExpectedUid.Check(typed, Card, null));
        }

        [Fact]
        public void ANoCardWithoutAUid_IsNeverTheCardThatWasMeant()
        {
            Assert.Equal(ERROR.IsNotTrue, ExpectedUid.Check("0123456789AB", null, null));
            Assert.Equal(ERROR.IsNotTrue, ExpectedUid.Check(string.Empty, string.Empty, null));
            Assert.Equal(ERROR.CardNotExpected, ExpectedUid.Check("$EXPECTEDUID", null, Variables("$EXPECTEDUID", "0123456789AB")));
            Assert.Equal(ERROR.CardNotExpected, ExpectedUid.Check("$EXPECTEDUID", "  ", Variables("$EXPECTEDUID", "  ")));
        }

        [Theory]
        [InlineData("0123456789AB")]
        [InlineData("0123456789ab")]
        [InlineData("\t0123456789AB\r\n")]
        public void AVariableWithTheUidOfTheCard_Passes(string named)
        {
            Assert.Equal(ERROR.NoError, ExpectedUid.Check("$EXPECTEDUID", Card, Variables("$EXPECTEDUID", named)));
        }

        [Theory]
        [InlineData("FFFFFFFFFFFF")]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("$EXPECTEDUID")]
        [InlineData("0123456789")]        // a part of the UID is no UID
        [InlineData("0123456789ABCD")]
        public void AVariableWithAnythingButTheUidOfTheCard_IsARefusal(string named)
        {
            Assert.Equal(ERROR.CardNotExpected, ExpectedUid.Check("$EXPECTEDUID", Card, Variables("$EXPECTEDUID", named)));
        }

        [Fact]
        public void AVariableThatWasNotGiven_IsARefusal_AndIsNotComparedAsText()
        {
            // Outside a run, in a run without variables, and in a run with other ones: the placeholder is not a UID that a card has.
            Assert.Equal(ERROR.CardNotExpected, ExpectedUid.Check("$EXPECTEDUID", Card, null));
            Assert.Equal(ERROR.CardNotExpected, ExpectedUid.Check("$EXPECTEDUID", Card, new Dictionary<string, string>()));
            Assert.Equal(ERROR.CardNotExpected, ExpectedUid.Check("$EXPECTEDUID", Card, Variables("$JOBNUMBER", Card)));
            Assert.Equal(ERROR.CardNotExpected, ExpectedUid.Check("$EXPECTEDUID", "$EXPECTEDUID", null));
        }

        [Fact]
        public void TheNameOfAVariable_IsLookedUpAsTheCallerSpellsIt()
        {
            // The variables of a run keep the spelling of their names (a start-up argument and startRun alike), and the dictionary of
            // the run decides about the case: a case-sensitive one does not find $expecteduid, one that ignores the case does.
            Assert.Equal(ERROR.CardNotExpected, ExpectedUid.Check("$EXPECTEDUID", Card, Variables("$expecteduid", Card)));

            var ignoringCase = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase) { ["$expecteduid"] = Card };
            Assert.Equal(ERROR.NoError, ExpectedUid.Check("$EXPECTEDUID", Card, ignoringCase));
        }
    }
}
