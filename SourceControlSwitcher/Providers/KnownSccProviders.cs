using System;
using System.Collections.Generic;
using SourceControlSwitcher.Detection;

namespace SourceControlSwitcher.Providers
{
    /// <summary>
    /// The source control providers the extension knows how to activate.
    /// </summary>
    public static class KnownSccProviders
    {
        // Built into Visual Studio. VS 2022 and VS 2026 register both packages; the second one is the package
        // named by the provider's own registration (TeamExplorer.pkgdef / GitProvider.pkgdef).
        public static readonly SccProviderInfo MicrosoftGit = new SccProviderInfo(
            "Git (Microsoft Git provider)", RepositoryKind.Git,
            new Guid("11b8e6d7-c08b-4385-b321-321078cdd1f8"),
            new Guid("7fe30a77-37f9-4cf2-83dd-96b207028e1b"), new Guid("7fe5c29f-58ec-45f0-af68-ec00d9ecd138"));

        public static readonly SccProviderInfo VisualSvn = new SccProviderInfo(
            "VisualSVN", RepositoryKind.Subversion,
            new Guid("937cffd6-105a-4c00-a044-33ffb48a3b8f"),
            new Guid("133240d5-fafa-4868-8fd7-5190a259e676"), new Guid("83f1e506-04bc-4694-9c7d-c55b120e11f0"));

        // The second package GUID is AnkhSVN 2019+ (issue #3).
        public static readonly SccProviderInfo AnkhSvn = new SccProviderInfo(
            "AnkhSVN", RepositoryKind.Subversion,
            new Guid("8770915b-b235-42ec-bbc6-8e93286e59b5"),
            new Guid("604ad610-5cf9-4bd5-8acc-f49810e2efd4"), new Guid("321833bc-d00d-4076-9fdd-ac485efdaeb2"));

        // Built into Visual Studio (Team Explorer); provider and package share the GUID.
        public static readonly SccProviderInfo TeamFoundation = new SccProviderInfo(
            "Visual Studio Team Foundation Server (TFVC)", RepositoryKind.TeamFoundation,
            new Guid("4ca58ab2-18fa-4f8d-95d4-32ddf27d184c"),
            new Guid("4ca58ab2-18fa-4f8d-95d4-32ddf27d184c"));

        public static readonly SccProviderInfo P4Vs = new SccProviderInfo(
            "P4VS (Helix/Perforce)", RepositoryKind.Perforce,
            new Guid("fda934f4-0492-4f67-a6eb-cbe0953649f0"),
            new Guid("8d316614-311a-48f4-85f7-df7020f62357"));

        public static IReadOnlyList<SccProviderInfo> All { get; } = new[] { MicrosoftGit, VisualSvn, AnkhSvn, TeamFoundation, P4Vs };

        /// <summary>Returns the known provider with this provider GUID, or null.</summary>
        public static SccProviderInfo FindByProviderGuid(Guid providerGuid)
        {
            foreach (var provider in All)
            {
                if (provider.ProviderGuid == providerGuid)
                    return provider;
            }
            return null;
        }
    }
}
