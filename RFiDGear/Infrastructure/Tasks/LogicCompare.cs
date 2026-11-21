#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace RFiDGear.Infrastructure.Tasks
{
    /// <summary>What the "Compare" function of a logic task found out about its expression.</summary>
    internal enum LogicCompareOutcome
    {
        /// <summary>The expression is true.</summary>
        True,

        /// <summary>The expression is false.</summary>
        False,

        /// <summary>
        /// The expression cannot be evaluated: no operator, an operand or an operator the function does not know, a value that is
        /// no number, a variable that has no value, or a card whose data is not known. It is no answer, and a task ends as
        /// <see cref="ERROR.IsNotTrue"/> on it, as it does on <see cref="False"/>.
        /// </summary>
        NotEvaluable
    }

    /// <summary>
    /// The "Compare" function of a logic task: <c>operand operator value</c>, for example <c>%FREEMEM&gt;=4096</c>,
    /// <c>%COUNTAPPS==2</c> or <c>$JOB!=J-7</c>.
    /// <list type="bullet">
    /// <item><description><c>%FREEMEM</c> (the free memory of the DESFire card) and <c>%COUNTAPPS</c> (the number of its applications)
    /// take <c>&gt;=</c>, <c>&lt;=</c>, <c>==</c> and <c>!=</c>, each meaning what it says, and a whole number as the value.</description></item>
    /// <item><description>A variable of the run, <c>$NAME</c> (any name with a dollar sign in it, as the start-up arguments accept
    /// it), takes <c>==</c> and <c>!=</c> and a text as the value. A variable that has no value is neither equal to anything nor
    /// different from anything: it cannot be evaluated.</description></item>
    /// </list>
    /// Spaces around the operand, the operator and the value do not matter.
    /// </summary>
    internal static class LogicCompare
    {
        private static readonly string[] Operators = { ">=", "<=", "==", "!=" };

        /// <summary>Evaluates an expression.</summary>
        /// <param name="expression">What was typed into the task.</param>
        /// <param name="freeMemory">The free memory of the DESFire card the run read, or <see langword="null"/> when there is none.</param>
        /// <param name="applicationCount">The number of applications of that card, or <see langword="null"/> when there is none.</param>
        /// <param name="variables">The variables of the run, or <see langword="null"/> for a check outside a run.</param>
        /// <returns>What the expression came to.</returns>
        internal static LogicCompareOutcome Evaluate(
            string? expression, uint? freeMemory, int? applicationCount, IReadOnlyDictionary<string, string>? variables)
        {
            if (!TrySplit(expression, out var operand, out var op, out var value))
            {
                return LogicCompareOutcome.NotEvaluable;
            }

            switch (operand)
            {
                case "%FREEMEM":
                    return CompareNumber(freeMemory, op, value);

                case "%COUNTAPPS":
                    return CompareNumber(applicationCount is >= 0 ? (uint)applicationCount.Value : null, op, value);

                default:
                    return operand.Contains('$') ? CompareVariable(operand, op, value, variables) : LogicCompareOutcome.NotEvaluable;
            }
        }

        // The operator is the first one in the text, so that a value may hold the characters of another (a variable "a>=b").
        private static bool TrySplit(string? expression, out string operand, out string op, out string value)
        {
            operand = op = value = string.Empty;

            if (string.IsNullOrWhiteSpace(expression))
            {
                return false;
            }

            var at = -1;

            foreach (var candidate in Operators)
            {
                var index = expression.IndexOf(candidate, StringComparison.Ordinal);

                if (index >= 0 && (at < 0 || index < at))
                {
                    at = index;
                    op = candidate;
                }
            }

            if (at < 0)
            {
                return false;
            }

            operand = expression.Substring(0, at).Trim();
            value = expression.Substring(at + op.Length).Trim();

            // A text may be compared with an empty value ("$NAME!=" says that the variable is not empty); a number may not.
            return operand.Length > 0;
        }

        private static LogicCompareOutcome CompareNumber(uint? actual, string op, string value)
        {
            if (actual == null || !TryParseNumber(value, out var expected))
            {
                return LogicCompareOutcome.NotEvaluable;
            }

            var result = op switch
            {
                ">=" => actual.Value >= expected,
                "<=" => actual.Value <= expected,
                "==" => actual.Value == expected,
                _ => actual.Value != expected
            };

            return result ? LogicCompareOutcome.True : LogicCompareOutcome.False;
        }

        private static LogicCompareOutcome CompareVariable(
            string name, string op, string value, IReadOnlyDictionary<string, string>? variables)
        {
            if ((op != "==" && op != "!=") || variables == null || !variables.TryGetValue(name, out var actual) || actual == null)
            {
                return LogicCompareOutcome.NotEvaluable;
            }

            var equal = string.Equals(actual, value, StringComparison.Ordinal);

            return equal == (op == "==") ? LogicCompareOutcome.True : LogicCompareOutcome.False;
        }

        // Digits only (4096), or groups of three digits after one separator that is used throughout (4.096, 4,096, 4 096; 1 to 3
        // digits come first). Nothing else: "abc", "12 bytes", "1,5" (a decimal), ".1000", "1000," and "1,,000" are not numbers,
        // and a text that is no number is never read as 0.
        private static bool TryParseNumber(string value, out uint number)
        {
            number = 0;

            if (value.Length == 0)
            {
                return false;
            }

            var separator = value.FirstOrDefault(c => !IsDigit(c));
            var groups = separator == default ? new[] { value } : value.Split(separator);

            var wellFormed = separator == default
                || (separator is '.' or ',' or ' ' or ' '
                    && groups[0].Length is >= 1 and <= 3
                    && groups.Skip(1).All(group => group.Length == 3));

            return wellFormed
                && groups.All(group => group.Length > 0 && group.All(IsDigit))
                && uint.TryParse(string.Concat(groups), NumberStyles.None, CultureInfo.InvariantCulture, out number);
        }

        private static bool IsDigit(char c) => c is >= '0' and <= '9';
    }
}
