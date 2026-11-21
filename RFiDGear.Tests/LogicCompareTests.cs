using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

using RFiDGear.Infrastructure;
using RFiDGear.Infrastructure.Tasks;
using RFiDGear.Models;
using RFiDGear.ViewModel.TaskSetupViewModels;

using Xunit;

namespace RFiDGear.Tests
{
    /// <summary>
    /// The "Compare" function of a logic task. Every operator has to mean what it says for both operands: <c>%FREEMEM!=</c>
    /// used to test for equality, <c>%COUNTAPPS==</c> and <c>%COUNTAPPS!=</c> tested "at least", <c>%COUNTAPPS&lt;=</c> did
    /// nothing, and an expression that could not be evaluated ended without a result.
    /// </summary>
    public class LogicCompareTests
    {
        private const uint FreeMemory = 1000;
        private const int Applications = 2;

        // What each operator says when the value is smaller than, equal to, or greater than what the card has.
        // (">=" is true for "the card has at least the value".)
        private static readonly (string Operator, bool WhenValueIsSmaller, bool WhenEqual, bool WhenValueIsGreater)[] Table =
        {
            (">=", true, true, false),
            ("<=", false, true, true),
            ("==", false, true, false),
            ("!=", true, false, true)
        };

        public static TheoryData<string, string> EveryOperatorOnBothOperands()
        {
            var data = new TheoryData<string, string>();

            foreach (var (name, actual) in new[] { ("%FREEMEM", (long)FreeMemory), ("%COUNTAPPS", (long)Applications) })
            {
                foreach (var row in Table)
                {
                    data.Add($"{name}{row.Operator}{actual - 1}", Outcome(row.WhenValueIsSmaller));
                    data.Add($"{name}{row.Operator}{actual}", Outcome(row.WhenEqual));
                    data.Add($"{name}{row.Operator}{actual + 1}", Outcome(row.WhenValueIsGreater));
                }
            }

            return data;
        }

        // The outcome is passed by name: the type is internal to the application and a test method is public.
        private static string Outcome(bool value) => value ? nameof(LogicCompareOutcome.True) : nameof(LogicCompareOutcome.False);

        private const string Cannot = nameof(LogicCompareOutcome.NotEvaluable);

        private static string Evaluate(string? expression, IReadOnlyDictionary<string, string>? variables = null)
            => LogicCompare.Evaluate(expression, FreeMemory, Applications, variables).ToString();

        [Theory]
        [MemberData(nameof(EveryOperatorOnBothOperands))]
        public void EveryOperator_MeansWhatItSays_ForBothOperands(string expression, string expected)
        {
            Assert.Equal(expected, Evaluate(expression));
        }

        [Theory]
        [InlineData("%FREEMEM >= 1000")]
        [InlineData("  %FREEMEM>=  1000  ")]
        [InlineData("%FREEMEM>=1.000")]
        [InlineData("%FREEMEM>=1,000")]
        [InlineData("%FREEMEM>=1 000")]
        [InlineData("%FREEMEM>=0001000")]
        public void SpacesAndGroupSeparators_DoNotMatter(string expression)
        {
            Assert.Equal(nameof(LogicCompareOutcome.True), Evaluate(expression));
        }

        [Theory]
        [InlineData("%FREEMEM>=abc")]
        [InlineData("%FREEMEM>=12 bytes")]
        [InlineData("%FREEMEM>=-5")]
        [InlineData("%FREEMEM>=1.5e3")]
        [InlineData("%FREEMEM>=")]
        [InlineData("%FREEMEM>=99999999999")]
        [InlineData("%COUNTAPPS==two")]
        [InlineData("%COUNTAPPS!=")]
        public void AValueThatIsNoWholeNumber_CannotBeEvaluated_AndIsNeverRead_AsZero(string expression)
        {
            Assert.Equal(Cannot, Evaluate(expression));
        }

        [Theory]
        [InlineData(">=")]
        [InlineData("<=")]
        [InlineData("==")]
        [InlineData("!=")]
        public void WithoutADesfireCard_NoOperatorCanBeEvaluated(string op)
        {
            Assert.Equal(Cannot, LogicCompare.Evaluate("%FREEMEM" + op + "0", null, Applications, null).ToString());
            Assert.Equal(Cannot, LogicCompare.Evaluate("%COUNTAPPS" + op + "0", FreeMemory, null, null).ToString());
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("%FREEMEM")]
        [InlineData("%FREEMEM=1000")]
        [InlineData("%FREEMEM>1000")]
        [InlineData("%FOO>=1")]
        [InlineData("FREEMEM>=1")]
        [InlineData("%freemem>=1")]
        [InlineData(">=1")]
        public void AnExpressionThatIsNoComparison_CannotBeEvaluated(string? expression)
        {
            Assert.Equal(Cannot, Evaluate(expression));
        }

        [Fact]
        public void AVariable_IsComparedForEqualityAndDifference_AsText()
        {
            var variables = new Dictionary<string, string> { ["$JOB"] = "J-7" };

            Assert.Equal(nameof(LogicCompareOutcome.True), Evaluate("$JOB==J-7", variables));
            Assert.Equal(nameof(LogicCompareOutcome.False), Evaluate("$JOB==J-8", variables));
            Assert.Equal(nameof(LogicCompareOutcome.False), Evaluate("$JOB!=J-7", variables));
            Assert.Equal(nameof(LogicCompareOutcome.True), Evaluate("$JOB!=J-8", variables));
            Assert.Equal(nameof(LogicCompareOutcome.True), Evaluate("  $JOB  ==  J-7 ", variables));
            Assert.Equal(nameof(LogicCompareOutcome.False), Evaluate("$JOB==j-7", variables));
        }

        [Fact]
        public void AVariableWithoutAValue_IsNeitherEqualNorDifferent()
        {
            var variables = new Dictionary<string, string> { ["$JOB"] = "J-7" };

            Assert.Equal(Cannot, Evaluate("$OTHER==J-7", variables));
            Assert.Equal(Cannot, Evaluate("$OTHER!=J-7", variables));
            Assert.Equal(Cannot, Evaluate("$JOB==J-7", null));
            Assert.Equal(Cannot, Evaluate("$JOB!=J-7", new Dictionary<string, string>()));
        }

        [Fact]
        public void AVariable_TakesNoOrderingOperator()
        {
            var variables = new Dictionary<string, string> { ["$JOB"] = "5" };

            Assert.Equal(Cannot, Evaluate("$JOB>=1", variables));
            Assert.Equal(Cannot, Evaluate("$JOB<=9", variables));
        }

        [Fact]
        public void AVariableMayBeComparedWithAnEmptyValue()
        {
            Assert.Equal(nameof(LogicCompareOutcome.True), Evaluate("$A!=", new Dictionary<string, string> { ["$A"] = "x" }));
            Assert.Equal(nameof(LogicCompareOutcome.False), Evaluate("$A!=", new Dictionary<string, string> { ["$A"] = "" }));
            Assert.Equal(nameof(LogicCompareOutcome.True), Evaluate("$A==", new Dictionary<string, string> { ["$A"] = "" }));
        }

        [Fact]
        public void TheFirstOperator_IsTheOneThatCounts_SoAValueMayHoldTheCharactersOfAnother()
        {
            var variables = new Dictionary<string, string> { ["$A"] = "x>=y" };

            Assert.Equal(nameof(LogicCompareOutcome.True), Evaluate("$A==x>=y", variables));
            Assert.Equal(nameof(LogicCompareOutcome.False), Evaluate("$A!=x>=y", variables));
        }

        // The task itself: the logic function with the expression, a DESFire card of 3 applications and 1000 bytes free.
        private static async Task<ERROR> RunAsync(string expression, bool withCard = true, Dictionary<string, string>? variables = null)
        {
            ERROR result = ERROR.Empty;

            await StaTestRunner.RunOnStaThreadAsync(async () =>
            {
                var chip = new MifareDesfireChipModel("04000001", CARD_TYPE.DESFire) { FreeMemory = FreeMemory };
                chip.AppList.Add(new MifareDesfireAppModel(1));
                chip.AppList.Add(new MifareDesfireAppModel(2));
                chip.AppList.Add(new MifareDesfireAppModel(3));

                var task = new CommonTaskViewModel
                {
                    SelectedTaskType = TaskType_CommonTask.CheckLogicCondition,
                    SelectedLogicCondition = LOGIC_STATE.COMPARE,
                    CompareValue = expression,
                    DesfireChip = withCard ? chip : null,
                    Args = variables ?? new Dictionary<string, string>()
                };

                await task.CheckLogicCondition.ExecuteAsync(new ObservableCollection<object>());

                result = task.CurrentTaskErrorLevel;
                Assert.Equal(result == ERROR.NoError, task.IsTaskCompletedSuccessfully);
            });

            return result;
        }

        [Theory]
        [InlineData("%COUNTAPPS==3", ERROR.NoError)]
        [InlineData("%COUNTAPPS==2", ERROR.IsNotTrue)]
        [InlineData("%COUNTAPPS!=3", ERROR.IsNotTrue)]
        [InlineData("%COUNTAPPS!=2", ERROR.NoError)]
        [InlineData("%COUNTAPPS<=3", ERROR.NoError)]
        [InlineData("%COUNTAPPS<=2", ERROR.IsNotTrue)]
        [InlineData("%COUNTAPPS>=3", ERROR.NoError)]
        [InlineData("%FREEMEM!=1000", ERROR.IsNotTrue)]
        [InlineData("%FREEMEM!=999", ERROR.NoError)]
        public async Task TheTask_EndsAsTheExpressionSays(string expression, ERROR expected)
        {
            Assert.Equal(expected, await RunAsync(expression));
        }

        [Theory]
        [InlineData("%FREEMEM>=1", false)]
        [InlineData("%COUNTAPPS==3", false)]
        [InlineData("nonsense", true)]
        [InlineData("", true)]
        public async Task AnExpressionThatCannotBeEvaluated_EndsAsNotTrue_NotWithoutAResult(string expression, bool withCard)
        {
            Assert.Equal(ERROR.IsNotTrue, await RunAsync(expression, withCard));
        }

        [Fact]
        public async Task TheTask_ComparesTheVariablesOfTheRun()
        {
            var variables = new Dictionary<string, string> { ["$JOB"] = "J-7" };

            Assert.Equal(ERROR.NoError, await RunAsync("$JOB==J-7", variables: variables));
            Assert.Equal(ERROR.IsNotTrue, await RunAsync("$JOB!=J-7", variables: variables));
            Assert.Equal(ERROR.IsNotTrue, await RunAsync("$MISSING==J-7", variables: variables));
        }
    }
}
