using System;

namespace SourceControlSwitcher
{
    /// <summary>GUIDs shared with SourceControlSwitcher.vsct and the package registration.</summary>
    internal static class PackageGuids
    {
        // Unchanged since 1.0: the package identity Visual Studio already knows.
        public const string PackageString = "1c0ec7cd-f652-42c4-a6ad-02b2b3653a1e";
        public const string CommandSetString = "4b8714ec-a9e9-4e3d-9fa1-e3548ae80475";

        public static readonly Guid Package = new Guid(PackageString);
        public static readonly Guid CommandSet = new Guid(CommandSetString);
    }

    /// <summary>Command ids, matching the IDSymbol values in SourceControlSwitcher.vsct.</summary>
    internal static class PackageIds
    {
        public const int DetectProviderCommand = 0x0100;
    }
}
