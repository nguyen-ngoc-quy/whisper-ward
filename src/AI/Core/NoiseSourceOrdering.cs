using System;
using System.Globalization;

namespace WhisperWard.AI.Core
{
    /// <summary>
    /// Compares serialized source identities without allowing numeric identifiers
    /// to fall back to lexical ordering. Numeric suffixes are canonical decimal
    /// text without leading zeroes; opaque suffixes remain valid for compatibility
    /// fixtures and use ordinal comparison.
    /// </summary>
    public static class NoiseSourceOrdering
    {
        /// <summary>
        /// Validates the source-ID namespace and suffix policy for an authoritative
        /// Movement or Burst source. Malformed IDs are rejected at ingress rather
        /// than being silently assigned an ordering position.
        /// </summary>
        public static bool TryValidateSourceEventId(string sourceEventId,
            NoiseSourceKind sourceKind, out string errorCode)
        {
            errorCode = string.Empty;
            if (string.IsNullOrWhiteSpace(sourceEventId))
            {
                errorCode = "noise-source-id-empty";
                return false;
            }

            string expectedPrefix = sourceKind == NoiseSourceKind.Movement
                ? "step" : sourceKind == NoiseSourceKind.Burst
                    ? "flight" : string.Empty;
            int separator = sourceEventId.IndexOf(':');
            if (string.IsNullOrEmpty(expectedPrefix) || separator <= 0
                || separator != sourceEventId.LastIndexOf(':')
                || separator == sourceEventId.Length - 1
                || !string.Equals(sourceEventId.Substring(0, separator),
                    expectedPrefix, StringComparison.Ordinal))
            {
                errorCode = "noise-source-id-namespace-invalid";
                return false;
            }

            string suffix = sourceEventId.Substring(separator + 1);
            for (int i = 0; i < suffix.Length; i++)
            {
                char character = suffix[i];
                bool allowed = (character >= 'a' && character <= 'z')
                    || (character >= 'A' && character <= 'Z')
                    || (character >= '0' && character <= '9')
                    || character == '-' || character == '_' || character == '.';
                if (!allowed)
                {
                    errorCode = "noise-source-id-suffix-invalid";
                    return false;
                }
            }

            ulong numericValue;
            if (ulong.TryParse(suffix, NumberStyles.None,
                CultureInfo.InvariantCulture, out numericValue))
            {
                if (suffix.Length > 1 && suffix[0] == '0')
                {
                    errorCode = "noise-source-id-leading-zero";
                    return false;
                }
                if (sourceKind == NoiseSourceKind.Burst && numericValue == 0)
                {
                    errorCode = "noise-source-id-zero";
                    return false;
                }
            }
            else if (sourceKind == NoiseSourceKind.Burst)
            {
                // Burst handles are numeric and must remain parseable so the
                // typed flight identity cannot diverge from its transport ID.
                errorCode = "noise-source-id-flight-not-numeric";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Compares canonical source identities such as step:9 and step:10.
        /// Numeric suffixes are compared numerically when both identities use the
        /// same namespace; opaque suffixes use ordinal comparison.
        /// </summary>
        public static int CompareSourceEventIds(string left, string right)
        {
            string leftPrefix;
            string rightPrefix;
            ulong leftNumber;
            ulong rightNumber;
            bool leftNumeric = TryParseNumericIdentity(left, out leftPrefix,
                out leftNumber);
            bool rightNumeric = TryParseNumericIdentity(right, out rightPrefix,
                out rightNumber);
            if (leftNumeric && rightNumeric
                && string.Equals(leftPrefix, rightPrefix,
                    StringComparison.Ordinal))
            {
                return leftNumber.CompareTo(rightNumber);
            }
            return string.Compare(left ?? string.Empty, right ?? string.Empty,
                StringComparison.Ordinal);
        }

        private static bool TryParseNumericIdentity(string value,
            out string prefix, out ulong number)
        {
            prefix = string.Empty;
            number = 0;
            if (string.IsNullOrWhiteSpace(value)) return false;
            int separator = value.IndexOf(':');
            if (separator <= 0 || separator == value.Length - 1
                || separator != value.LastIndexOf(':')) return false;
            prefix = value.Substring(0, separator);
            string suffix = value.Substring(separator + 1);
            if (suffix.Length > 1 && suffix[0] == '0') return false;
            return ulong.TryParse(suffix, NumberStyles.None,
                CultureInfo.InvariantCulture, out number);
        }
    }
}
