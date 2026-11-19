#nullable enable
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace RFiDGear.Infrastructure.Tasks
{
    /// <summary>
    /// What a Check UID task compares the card with: a UID typed into the task, or a placeholder such as <c>$EXPECTEDUID</c> that
    /// names a variable of the run. A variable is what the caller of the run supplies (a start-up argument, <c>startRun</c> of the
    /// external control bridge, an extension), so the project does not change from card to card, and it says which card the
    /// caller expects for this run.
    /// <para>
    /// The two are different questions. A typed UID asks "is this card that card?" and answers no with
    /// <see cref="ERROR.IsNotTrue"/>, a logic answer that a project may branch on and that a run may end with as one it
    /// "asked for". A variable is the caller's statement of which card to process: "not this card" and "no card was named" are both a refusal
    /// (<see cref="ERROR.CardNotExpected"/>), which no project branch for another card takes and which never ends a run as a
    /// success. A placeholder without a value is never compared as text, and never matches.
    /// </para>
    /// </summary>
    internal static class ExpectedUid
    {
        // A variable is spelled as the caller spells it, with the dollar sign: $NAME. A UID never starts with one.
        private static readonly Regex Reference = new Regex(@"^\$[A-Za-z0-9_]{1,64}\z", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

        /// <summary>Says whether the text of a task names a variable instead of holding a UID.</summary>
        /// <param name="text">What was typed into the task.</param>
        /// <returns><see langword="true"/> for a dollar sign and 1 to 64 letters, digits or underscores.</returns>
        internal static bool IsVariableReference(string? text) => text != null && Reference.IsMatch(text.Trim());

        /// <summary>Compares the card with what the task expects.</summary>
        /// <param name="expected">What was typed into the task: a UID, or a placeholder.</param>
        /// <param name="cardUid">The UID of the card the run read, or <see langword="null"/> when it has none.</param>
        /// <param name="variables">The variables of the run, or <see langword="null"/> for a check outside a run.</param>
        /// <returns>
        /// <see cref="ERROR.NoError"/> for the card that was meant; for a typed UID that is another one,
        /// <see cref="ERROR.IsNotTrue"/>; for a placeholder whose value is missing, empty or another UID,
        /// <see cref="ERROR.CardNotExpected"/>.
        /// </returns>
        internal static ERROR Check(string? expected, string? cardUid, IReadOnlyDictionary<string, string>? variables)
        {
            if (!IsVariableReference(expected))
            {
                return Same(expected, cardUid) ? ERROR.NoError : ERROR.IsNotTrue;
            }

            string? named = null;

            return variables != null && variables.TryGetValue(expected!.Trim(), out named) && Same(named, cardUid)
                ? ERROR.NoError
                : ERROR.CardNotExpected;
        }

        // A UID is hexadecimal digits, so the case does not matter; the text of a person or of a program may have white space around it.
        // Nothing is empty-equals-empty: a card without a UID is no card that was meant.
        private static bool Same(string? wanted, string? cardUid) =>
            !string.IsNullOrWhiteSpace(wanted)
            && !string.IsNullOrWhiteSpace(cardUid)
            && string.Equals(wanted.Trim(), cardUid.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
