using System;

namespace SourceControlSwitcher.Options
{
    /// <summary>
    /// Parsing of the values stored in the settings store, including those written by version 1.x.
    /// </summary>
    public static class SettingValues
    {
        /// <summary>
        /// Case-insensitive, so 1.x values ("AnkhSVN", "VisualSVN", "P4VS") map onto the current member names.
        /// Unknown or removed values return <paramref name="defaultValue"/>.
        /// </summary>
        public static T ParseEnum<T>(string value, T defaultValue) where T : struct
        {
            if (string.IsNullOrWhiteSpace(value))
                return defaultValue;
            return Enum.TryParse(value.Trim(), true, out T result) && Enum.IsDefined(typeof(T), result) ? result : defaultValue;
        }

        /// <summary>
        /// 1.x saved "VisualStudioToolsForGit" for the built-in provider and "EasyGitIntegrationTools" for a
        /// client that is no longer supported (falls back to Default).
        /// </summary>
        public static GitProviderChoice ParseGitProvider(string value)
        {
            if (string.Equals(value, "VisualStudioToolsForGit", StringComparison.OrdinalIgnoreCase))
                return GitProviderChoice.MicrosoftGit;
            return ParseEnum(value, GitProviderChoice.Default);
        }
    }
}
