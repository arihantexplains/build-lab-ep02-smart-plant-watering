using System;
using System.Collections.Generic;
using System.Text;

namespace SmartWatering.Plugins.Logic
{
    /// <summary>The result of parsing a list of mobile numbers.</summary>
    public sealed class RecipientList
    {
        /// <summary>Numbers that passed the checks, digits only with the country code, no duplicates, in the original order.</summary>
        public List<string> Valid { get; } = new List<string>();

        /// <summary>One line per refused entry: the masked entry and the reason.</summary>
        public List<string> Rejected { get; } = new List<string>();
    }

    /// <summary>
    /// Parses and checks mobile numbers for the WhatsApp Cloud API, which wants digits only,
    /// country code first, no plus sign and no leading zero (for example 91XXXXXXXXXX).
    /// </summary>
    public static class PhoneNumbers
    {
        /// <summary>
        /// Shortest accepted number, country code included. 11 fits India (91 + 10 digits) and most countries.
        /// A plain 10 digit number is refused because it has no country code. Lower this if your country's numbers are shorter.
        /// </summary>
        public const int MinDigits = 11;

        /// <summary>Longest number the international format (E.164) allows.</summary>
        public const int MaxDigits = 15;

        /// <summary>
        /// Cleans one number: drops spaces, dashes, dots, brackets, a leading plus and a leading 00,
        /// then checks that only digits are left, that it does not start with 0 and that the length is right.
        /// </summary>
        /// <param name="raw">The number as typed, for example "+91 98XXX XXXXX".</param>
        /// <param name="digits">The cleaned number, digits only, when the result is true.</param>
        /// <param name="reason">Why the number was refused, when the result is false.</param>
        /// <returns>True when the number can be used.</returns>
        public static bool TryNormalise(string raw, out string digits, out string reason)
        {
            digits = null;
            reason = null;
            if (string.IsNullOrWhiteSpace(raw))
            {
                reason = "is empty";
                return false;
            }

            var sb = new StringBuilder();
            string trimmed = raw.Trim();
            for (int i = 0; i < trimmed.Length; i++)
            {
                char c = trimmed[i];
                if (c == ' ' || c == '-' || c == '.' || c == '(' || c == ')') continue;
                if (c == '+' && sb.Length == 0 && i == 0) continue;
                if (c < '0' || c > '9')
                {
                    reason = "has characters other than digits";
                    return false;
                }
                sb.Append(c);
            }

            string cleaned = sb.ToString();
            if (cleaned.StartsWith("00", StringComparison.Ordinal)) cleaned = cleaned.Substring(2);

            if (cleaned.Length == 0)
            {
                reason = "is empty";
                return false;
            }
            if (cleaned[0] == '0')
            {
                reason = "starts with 0, use the country code instead";
                return false;
            }
            if (cleaned.Length < MinDigits)
            {
                reason = "is too short, include the country code (for example 91 for India)";
                return false;
            }
            if (cleaned.Length > MaxDigits)
            {
                reason = "is longer than 15 digits";
                return false;
            }

            digits = cleaned;
            return true;
        }

        /// <summary>
        /// Parses a list such as the admin numbers environment variable.
        /// Entries may be separated by commas, semicolons or new lines. Duplicates are dropped.
        /// </summary>
        /// <param name="list">For example "91XXXXXXXXXX, 91XXXXXXXXXX".</param>
        public static RecipientList ParseList(string list)
        {
            var result = new RecipientList();
            if (string.IsNullOrWhiteSpace(list)) return result;

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string entry in list.Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (string.IsNullOrWhiteSpace(entry)) continue;
                if (TryNormalise(entry, out string digits, out string reason))
                {
                    if (seen.Add(digits)) result.Valid.Add(digits);
                }
                else
                {
                    result.Rejected.Add(Mask(entry.Trim()) + " " + reason);
                }
            }

            return result;
        }

        /// <summary>
        /// Hides the middle of a number for logs and the result column: "91XXXXXXXXXX" becomes "91******XXXX".
        /// Short values are hidden completely.
        /// </summary>
        /// <param name="value">A number or any short text.</param>
        public static string Mask(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= 6) return "***";
            return value.Substring(0, 2) + new string('*', value.Length - 6) + value.Substring(value.Length - 4);
        }
    }
}
