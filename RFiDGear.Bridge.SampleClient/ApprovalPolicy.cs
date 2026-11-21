#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RFiDGear.Bridge.SampleClient
{
    /// <summary>
    /// The options of the command <c>auto</c> that make it wait for approval: one fixed rule by which it decides about every card, to
    /// try the flow by hand. A program that writes cards decides in its own code instead (see <see cref="ApprovalModeLoop"/>).
    /// <list type="bullet">
    /// <item><c>--approve-with=$NAME</c> approves EVERY card, with the variable set to its UID and the project held to the state the
    /// card was shown with (<c>--no-pin</c> leaves that out); <c>--wait=&lt;seconds&gt;</c> thinks that long first (time to change the
    /// project, take the card away or kill the program; RFiDGear waits 15 seconds for an answer); <c>--set=NAME=value</c> adds a
    /// variable to every approval (a report path).</item>
    /// <item><c>--decline</c> declines every card.</item>
    /// <item><c>--no-answer</c> never answers, so that the time limit of RFiDGear ends the wait.</item>
    /// </list>
    /// Anything else is refused: an option that is not understood must never end up as the mode that runs every card.
    /// </summary>
    public static class ApprovalPolicy
    {
        /// <summary>The option that approves every card with a variable set to its UID.</summary>
        public const string ApproveWithOption = "--approve-with=";

        /// <summary>The option that declines every card.</summary>
        public const string DeclineOption = "--decline";

        /// <summary>The option that never answers.</summary>
        public const string NoAnswerOption = "--no-answer";

        /// <summary>The option that leaves out the state of the project from an approval.</summary>
        public const string NoPinOption = "--no-pin";

        /// <summary>The option that adds a variable to every approval.</summary>
        public const string SetOption = "--set=";

        /// <summary>The option that thinks for some seconds before it approves.</summary>
        public const string WaitOption = "--wait=";

        /// <summary>The longest <c>--wait</c> that is taken, in seconds.</summary>
        public const int LongestWaitSeconds = 600;

        /// <summary>
        /// Tells whether <c>auto</c> is the plain mode, which runs every card: only a bare <c>auto</c> is. Any argument after it, a
        /// misspelled option included, means that the person wanted the mode that waits for approval, and gets that or an error.
        /// </summary>
        /// <param name="options">The arguments after the command.</param>
        /// <returns><see langword="true"/> when there are none.</returns>
        public static bool IsPlainMode(IReadOnlyList<string> options)
        {
            ArgumentNullException.ThrowIfNull(options);

            return options.Count == 0;
        }

        /// <summary>Tells whether an argument of <c>auto</c> is an option of the rules that wait for approval.</summary>
        /// <param name="option">The argument.</param>
        /// <returns><see langword="true"/> when the argument is one of the options of this class.</returns>
        public static bool IsOption(string option) =>
            option.StartsWith(ApproveWithOption, StringComparison.Ordinal) || option == DeclineOption || option == NoAnswerOption
            || option == NoPinOption || option.StartsWith(SetOption, StringComparison.Ordinal) || option.StartsWith(WaitOption, StringComparison.Ordinal);

        /// <summary>Reads the options that follow <c>auto</c> and makes the rule by which the command decides about every card.</summary>
        /// <param name="options">The arguments after the command: exactly one of <c>--approve-with=$NAME</c>, <c>--decline</c> and <c>--no-answer</c>, and with the first one any of <c>--no-pin</c>, <c>--wait=</c> and <c>--set=</c>.</param>
        /// <param name="decide">The decision about a card; <see langword="null"/> when the options are refused.</param>
        /// <param name="description">What the rule does, for the person at the command line.</param>
        /// <param name="error">Why the options are refused; empty when they are not.</param>
        /// <returns><see langword="true"/> when the options make one rule.</returns>
        public static bool TryRead(
            IReadOnlyList<string> options,
            out Func<CardPresentedEvent, CancellationToken, Task<CardDecision>>? decide,
            out string description,
            out string error)
        {
            ArgumentNullException.ThrowIfNull(options);

            decide = null;
            description = string.Empty;
            error = string.Empty;

            var unknown = options.FirstOrDefault(option => !IsOption(option));

            if (unknown != null)
            {
                error = "Unknown option for auto: " + unknown + ". Use exactly one of " + ApproveWithOption + "$NAME (with an equals sign), " + DeclineOption + " and " + NoAnswerOption
                    + ", and with the first one " + NoPinOption + ", " + WaitOption + "<seconds> and " + SetOption + "NAME=value.";

                return false;
            }

            var rules = options.Where(option => option.StartsWith(ApproveWithOption, StringComparison.Ordinal) || option == DeclineOption || option == NoAnswerOption).ToList();

            if (rules.Count != 1)
            {
                error = "Give exactly one of " + ApproveWithOption + "$NAME, " + DeclineOption + " and " + NoAnswerOption + ".";

                return false;
            }

            var rule = rules[0];

            if ((rule == DeclineOption || rule == NoAnswerOption) && options.Count > 1)
            {
                error = NoPinOption + ", " + WaitOption + " and " + SetOption + " go with " + ApproveWithOption + " only.";

                return false;
            }

            if (rule == DeclineOption)
            {
                decide = (_, _) => Task.FromResult(CardDecision.Decline);
                description = "every card is declined";

                return true;
            }

            if (rule == NoAnswerOption)
            {
                decide = async (_, token) =>
                {
                    await Task.Delay(Timeout.Infinite, token).ConfigureAwait(false);

                    return CardDecision.Decline;
                };
                description = "no card is answered, so that the time limit of RFiDGear ends the wait";

                return true;
            }

            return TryReadApproval(options, rule.Substring(ApproveWithOption.Length), out decide, out description, out error);
        }

        private static bool TryReadApproval(
            IReadOnlyList<string> options,
            string variable,
            out Func<CardPresentedEvent, CancellationToken, Task<CardDecision>>? decide,
            out string description,
            out string error)
        {
            decide = null;
            description = string.Empty;
            error = string.Empty;

            if (!variable.StartsWith('$') || variable.Length < 2)
            {
                error = "The variable is a dollar sign and a name, such as $EXPECTEDUID.";

                return false;
            }

            var pinRevision = !options.Contains(NoPinOption);
            var wait = TimeSpan.Zero;
            var fixedVariables = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var option in options.Where(option => option.StartsWith(WaitOption, StringComparison.Ordinal)))
            {
                if (!double.TryParse(option.Substring(WaitOption.Length), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) || seconds < 0 || seconds > LongestWaitSeconds)
                {
                    error = "--wait takes a number of seconds from 0 to " + LongestWaitSeconds.ToString(CultureInfo.InvariantCulture) + ".";

                    return false;
                }

                wait = TimeSpan.FromSeconds(seconds);
            }

            foreach (var option in options.Where(option => option.StartsWith(SetOption, StringComparison.Ordinal)))
            {
                var pair = option.Substring(SetOption.Length);
                var at = pair.IndexOf('=', StringComparison.Ordinal);

                if (at < 1)
                {
                    error = "--set takes NAME=value.";

                    return false;
                }

                fixedVariables[pair.Substring(0, at)] = pair.Substring(at + 1);
            }

            decide = async (shown, token) =>
            {
                if (wait > TimeSpan.Zero)
                {
                    await Task.Delay(wait, token).ConfigureAwait(false);
                }

                var variables = new Dictionary<string, string>(fixedVariables, StringComparer.Ordinal) { [variable] = shown.Uid };

                return CardDecision.ApproveWith(variables, pinRevision);
            };
            description = "every card is approved with " + variable + " = its UID" + (pinRevision ? " and the state of the project" : string.Empty)
                + (wait > TimeSpan.Zero ? ", after " + wait.TotalSeconds.ToString(CultureInfo.InvariantCulture) + " seconds" : string.Empty);

            return true;
        }
    }
}
