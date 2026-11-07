using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RFiDGear.ExtensionLibraries.CardCheck
{
    /// <summary>
    /// The only place where a name from the settings becomes part of SQL text. A table name is up to three parts
    /// (<c>server-less</c>: database, schema, table) separated by dots; each part is a bare word or is written in square
    /// brackets (<c>]]</c> stands for a <c>]</c>). Anything else is refused, and what is accepted is written out again
    /// with every part in brackets, so nothing the operator typed can end a name early or add a statement.
    /// </summary>
    internal static class SqlIdentifier
    {
        private const int MaxParts = 3;
        private const int MaxPartLength = 128;

        /// <summary>Checks a table name and writes it out quoted.</summary>
        /// <param name="name">The name as typed, for example <c>T_CardCheck</c> or <c>dbo.[T Card Check]</c>.</param>
        /// <returns>The quoted name, for example <c>[dbo].[T Card Check]</c>, or an <see cref="FailureKind.InvalidConfiguration"/> failure.</returns>
        public static Result<string> Quote(string? name)
        {
            var text = name?.Trim();

            if (string.IsNullOrEmpty(text))
            {
                return Invalid();
            }

            var parts = new List<string>();
            var position = 0;

            while (true)
            {
                var (part, next) = ReadPart(text, position);

                if (part == null)
                {
                    return Invalid();
                }

                parts.Add(part);
                position = next;

                if (position == text.Length)
                {
                    break;
                }

                if (text[position] != '.' || position + 1 == text.Length)
                {
                    return Invalid();
                }

                position++;
            }

            return parts.Count > MaxParts
                ? Invalid()
                : Result<string>.Ok(string.Join(".", parts.Select(part => "[" + part.Replace("]", "]]") + "]")));
        }

        private static Result<string> Invalid() =>
            Result<string>.Fail(new Failure(FailureKind.InvalidConfiguration, "orders.table.invalid", "The name of the order table is not valid."));

        // Reads one part starting at the given position; the part is null if it is not valid.
        private static (string? Part, int Next) ReadPart(string text, int start)
        {
            return text[start] == '['
                ? ReadBracketed(text, start + 1)
                : ReadBare(text, start);
        }

        private static (string? Part, int Next) ReadBare(string text, int start)
        {
            var end = start;

            while (end < text.Length && text[end] != '.')
            {
                var c = text[end];
                var allowed = char.IsLetter(c) || c == '_' || (end > start && (char.IsDigit(c) || c == '$' || c == '#'));

                if (!allowed)
                {
                    return (null, end);
                }

                end++;
            }

            return end == start || end - start > MaxPartLength ? (null, end) : (text.Substring(start, end - start), end);
        }

        private static (string? Part, int Next) ReadBracketed(string text, int start)
        {
            var part = new StringBuilder();
            var index = start;

            while (index < text.Length)
            {
                var c = text[index];

                if (char.IsControl(c))
                {
                    return (null, index);
                }

                if (c != ']')
                {
                    part.Append(c);
                    index++;
                }
                else if (index + 1 < text.Length && text[index + 1] == ']')
                {
                    part.Append(']');
                    index += 2;
                }
                else
                {
                    return part.Length == 0 || part.Length > MaxPartLength ? (null, index) : (part.ToString(), index + 1);
                }
            }

            return (null, index);
        }
    }
}
