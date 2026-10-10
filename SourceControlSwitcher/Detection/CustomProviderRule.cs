using System;
using System.Collections.Generic;
using System.IO;

namespace SourceControlSwitcher.Detection
{
    /// <summary>
    /// A user-defined detection rule: when <see cref="Marker"/> (a file or folder name) exists in the solution
    /// folder or one of its parents, the source control provider <see cref="ProviderGuid"/> is activated.
    /// </summary>
    public sealed class CustomProviderRule
    {
        public CustomProviderRule(string marker, Guid providerGuid)
        {
            Marker = marker;
            ProviderGuid = providerGuid;
        }

        public string Marker { get; }

        public Guid ProviderGuid { get; }

        /// <summary>
        /// Parses "marker=providerGuid" entries separated by ';' or new lines. Entries that cannot be parsed are
        /// returned in <paramref name="invalidEntries"/> and otherwise ignored.
        /// </summary>
        public static IReadOnlyList<CustomProviderRule> ParseList(string value, out IReadOnlyList<string> invalidEntries)
        {
            var rules = new List<CustomProviderRule>();
            var invalid = new List<string>();
            invalidEntries = invalid;

            if (string.IsNullOrWhiteSpace(value))
                return rules;

            foreach (var rawEntry in value.Split(new[] { ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string entry = rawEntry.Trim();
                if (entry.Length == 0)
                    continue;

                if (TryParse(entry, out CustomProviderRule rule))
                    rules.Add(rule);
                else
                    invalid.Add(entry);
            }
            return rules;
        }

        public static bool TryParse(string entry, out CustomProviderRule rule)
        {
            rule = null;
            int separator = entry.IndexOf('=');
            if (separator <= 0)
                return false;

            string marker = entry.Substring(0, separator).Trim();
            if (marker.Length == 0 || marker.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                return false;

            if (!Guid.TryParse(entry.Substring(separator + 1).Trim(), out Guid providerGuid) || providerGuid == Guid.Empty)
                return false;

            rule = new CustomProviderRule(marker, providerGuid);
            return true;
        }

        public override string ToString() => Marker + "=" + ProviderGuid.ToString("D");
    }
}
