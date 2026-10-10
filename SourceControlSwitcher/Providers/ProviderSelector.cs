using System;
using SourceControlSwitcher.Detection;
using SourceControlSwitcher.Options;

namespace SourceControlSwitcher.Providers
{
    /// <summary>
    /// The provider choice for each repository kind, as configured in the options page.
    /// </summary>
    public sealed class ProviderPreferences
    {
        public GitProviderChoice Git { get; set; }

        public SubversionProviderChoice Subversion { get; set; }

        public TeamFoundationProviderChoice TeamFoundation { get; set; }

        public PerforceProviderChoice Perforce { get; set; }
    }

    /// <summary>
    /// Picks the provider to activate for a repository kind.
    /// </summary>
    public static class ProviderSelector
    {
        /// <summary>
        /// Returns the provider to activate, or null when switching is disabled for <paramref name="kind"/> or no
        /// suitable provider is installed. An explicitly chosen provider is returned even when it is not
        /// installed, so the caller can report it.
        /// </summary>
        public static SccProviderInfo Select(RepositoryKind kind, ProviderPreferences preferences, Func<SccProviderInfo, bool> isInstalled)
        {
            if (preferences == null)
                throw new ArgumentNullException(nameof(preferences));
            if (isInstalled == null)
                throw new ArgumentNullException(nameof(isInstalled));

            switch (kind)
            {
                case RepositoryKind.Git:
                    switch (preferences.Git)
                    {
                        case GitProviderChoice.MicrosoftGit: return KnownSccProviders.MicrosoftGit;
                        case GitProviderChoice.Default: return FirstInstalled(isInstalled, KnownSccProviders.MicrosoftGit);
                        default: return null;
                    }

                case RepositoryKind.Subversion:
                    switch (preferences.Subversion)
                    {
                        case SubversionProviderChoice.VisualSvn: return KnownSccProviders.VisualSvn;
                        case SubversionProviderChoice.AnkhSvn: return KnownSccProviders.AnkhSvn;
                        case SubversionProviderChoice.Default: return FirstInstalled(isInstalled, KnownSccProviders.VisualSvn, KnownSccProviders.AnkhSvn);
                        default: return null;
                    }

                case RepositoryKind.TeamFoundation:
                    switch (preferences.TeamFoundation)
                    {
                        case TeamFoundationProviderChoice.TeamFoundationServer: return KnownSccProviders.TeamFoundation;
                        case TeamFoundationProviderChoice.Default: return FirstInstalled(isInstalled, KnownSccProviders.TeamFoundation);
                        default: return null;
                    }

                case RepositoryKind.Perforce:
                    switch (preferences.Perforce)
                    {
                        case PerforceProviderChoice.P4Vs: return KnownSccProviders.P4Vs;
                        case PerforceProviderChoice.Default: return FirstInstalled(isInstalled, KnownSccProviders.P4Vs);
                        default: return null;
                    }

                default:
                    return null;
            }
        }

        private static SccProviderInfo FirstInstalled(Func<SccProviderInfo, bool> isInstalled, params SccProviderInfo[] candidates)
        {
            foreach (var candidate in candidates)
            {
                if (isInstalled(candidate))
                    return candidate;
            }
            return null;
        }
    }
}
