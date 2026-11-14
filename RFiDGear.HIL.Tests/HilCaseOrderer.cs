using System;
using System.Collections.Generic;
using System.Linq;

using Xunit.Sdk;
using Xunit.v3;

namespace RFiDGear.HIL.Tests
{
    /// <summary>
    /// Puts the cases that do not move cards before the ones that do, and orders each group by name. xunit orders the cases
    /// by a hash otherwise, and the cases share one reader and one card: a case that needs the card that was on the reader at
    /// the start would run after a case that ended with the card taken off, and fail without anyone being asked.
    /// </summary>
    public sealed class HilCaseOrderer : ITestCaseOrderer
    {
        /// <inheritdoc/>
        public IReadOnlyCollection<TTestCase> OrderTestCases<TTestCase>(IReadOnlyCollection<TTestCase> testCases)
            where TTestCase : notnull, ITestCase =>
            testCases
                .OrderBy(testCase => AsksForCards(testCase) ? 1 : 0)
                .ThenBy(testCase => testCase.TestMethodName, StringComparer.Ordinal)
                .ToList();

        private static bool AsksForCards(ITestCase testCase) =>
            testCase is IXunitTestCase xunitCase
            && xunitCase.TestMethod.Method.GetCustomAttributes(typeof(HilInteractiveFactAttribute), inherit: false).Length > 0;
    }
}
