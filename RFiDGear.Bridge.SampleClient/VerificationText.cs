#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace RFiDGear.Bridge.SampleClient
{
    /// <summary>
    /// Writes the verification details of a task as lines an operator can read: what was compared, what was expected, what was read
    /// and how each setting ended. It reads numbers and fixed tokens and nothing else, and it copes with what it does not know: an
    /// unknown kind is described by its checks alone, and a token it has no word for is printed as it is.
    /// </summary>
    public static class VerificationText
    {
        /// <summary>Describes the details of one task.</summary>
        /// <param name="details">The details a task outcome carried.</param>
        /// <returns>
        /// A line for the whole and one for every setting that was compared (a setting that was not compared is left out, and counted
        /// in the first line).
        /// </returns>
        public static IReadOnlyList<string> Describe(VerificationDetailsDto details)
        {
            ArgumentNullException.ThrowIfNull(details);

            var lines = new List<string>();
            var compared = details.Checks.Where(check => check.Compared).ToList();
            var whole = details.OverallMatch switch
            {
                true => "matches",
                false => "DOES NOT MATCH",
                null => "no verdict",
            };
            var where = details.ApplicationId != null || details.FileNumber != null
                ? " (application " + Number(details.ApplicationId) + ", file " + Number(details.FileNumber) + ")"
                : string.Empty;

            lines.Add(details.Kind + where + ": " + whole
                + (details.CompareMode == null ? string.Empty : ", " + details.CompareMode + " of the compared settings")
                + ", " + compared.Count + " compared, " + (details.Checks.Count - compared.Count) + " not"
                + (details.ErrorCode == null ? string.Empty : ", " + details.ErrorCode));

            foreach (var check in compared)
            {
                lines.Add(check.Name + ": " + check.Result
                    + (check.Expected == null ? string.Empty : ", expected " + check.Expected.Value.ToString(CultureInfo.InvariantCulture))
                    + (check.Actual == null ? string.Empty : ", read " + check.Actual.Value.ToString(CultureInfo.InvariantCulture))
                    + (check.Rule == null ? string.Empty : " (" + check.Rule + ")"));
            }

            return lines;
        }

        private static string Number(int? value) => value == null ? "?" : value.Value.ToString(CultureInfo.InvariantCulture);
    }
}
