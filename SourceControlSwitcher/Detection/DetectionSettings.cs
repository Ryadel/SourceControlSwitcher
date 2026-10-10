using System;
using System.Collections.Generic;

namespace SourceControlSwitcher.Detection
{
    /// <summary>
    /// Inputs of <see cref="RepositoryDetector"/>, taken from the options page.
    /// </summary>
    public sealed class DetectionSettings
    {
        /// <summary>Walk up from the solution folder to the drive root looking for markers.</summary>
        public bool SearchParentFolders { get; set; } = true;

        /// <summary>Treat a .sln file containing TFVC bindings as a TFVC solution.</summary>
        public bool DetectTeamFoundationFromSolution { get; set; } = true;

        /// <summary>User-defined markers, checked before the built-in ones.</summary>
        public IReadOnlyList<CustomProviderRule> CustomProviders { get; set; } = Array.Empty<CustomProviderRule>();
    }
}
