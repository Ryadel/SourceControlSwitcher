using System;

namespace SourceControlSwitcher.Detection
{
    /// <summary>
    /// Outcome of a repository detection: what was found, where, and why.
    /// </summary>
    public sealed class DetectionResult
    {
        public static readonly DetectionResult None = new DetectionResult(RepositoryKind.None, null, Guid.Empty, "no repository marker found");

        public DetectionResult(RepositoryKind kind, string location, Guid customProviderGuid, string evidence)
        {
            Kind = kind;
            Location = location;
            CustomProviderGuid = customProviderGuid;
            Evidence = evidence;
        }

        public RepositoryKind Kind { get; }

        /// <summary>The folder (or solution file) where the evidence was found.</summary>
        public string Location { get; }

        /// <summary>The provider to activate when <see cref="Kind"/> is <see cref="RepositoryKind.Custom"/>.</summary>
        public Guid CustomProviderGuid { get; }

        /// <summary>Human-readable reason, used for logging.</summary>
        public string Evidence { get; }

        public bool IsDetected => Kind != RepositoryKind.None;
    }
}
