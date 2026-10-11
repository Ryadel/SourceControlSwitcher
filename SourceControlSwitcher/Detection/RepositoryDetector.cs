// Copyright (c) 2013-2014 Francesco Pretto
// This file is subject to the MIT license

using System;
using System.IO;
using System.Security;
using System.Xml;

namespace SourceControlSwitcher.Detection
{
    /// <summary>
    /// Detects the repository kind of a solution or folder from markers on disk. Cheap enough to run on the UI
    /// thread before a solution opens: it only checks for the existence of a few names per folder level.
    /// Perforce has no marker on disk and is handled separately by <see cref="PerforceDetector"/>.
    /// </summary>
    public static class RepositoryDetector
    {
        public const string GitMarker = ".git";
        public const string SubversionMarker = ".svn";

        // Hidden folder TFVC creates at the root of a local workspace.
        public const string TeamFoundationLocalWorkspaceMarker = "$tf";

        // A solution bound to TFVC has this key, with the server URL, in its TeamFoundationVersionControl section.
        public const string TeamFoundationSection = "TeamFoundationVersionControl";
        public const string TeamFoundationSolutionBinding = "SccTeamFoundationServer";

        /// <summary>
        /// Returns the folder detection starts from: the path itself for a folder (Open Folder mode), otherwise
        /// the folder containing the solution file. Null when the path is empty or invalid.
        /// </summary>
        public static string GetStartDirectory(string solutionOrFolderPath)
        {
            if (string.IsNullOrWhiteSpace(solutionOrFolderPath))
                return null;

            try
            {
                if (Directory.Exists(solutionOrFolderPath))
                    return Path.GetFullPath(solutionOrFolderPath);

                string directory = Path.GetDirectoryName(solutionOrFolderPath);
                return string.IsNullOrEmpty(directory) ? null : Path.GetFullPath(directory);
            }
            catch (Exception ex) when (IsPathException(ex))
            {
                return null;
            }
        }

        public static DetectionResult Detect(string solutionOrFolderPath, DetectionSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            string startDirectory = GetStartDirectory(solutionOrFolderPath);
            if (startDirectory == null)
                return DetectionResult.None;

            try
            {
                // Nearest folder first. The solution file is checked once, at its own folder level.
                string solutionFile = solutionOrFolderPath;
                for (var directory = new DirectoryInfo(startDirectory); directory != null; directory = settings.SearchParentFolders ? directory.Parent : null)
                {
                    var result = DetectInDirectory(directory.FullName, solutionFile, settings);
                    if (result.IsDetected)
                        return result;
                    solutionFile = null;
                }
            }
            catch (Exception ex) when (IsPathException(ex))
            {
                // An unreadable parent folder ends the search; whatever was below it has already been checked.
            }

            return DetectionResult.None;
        }

        private static DetectionResult DetectInDirectory(string directory, string solutionFile, DetectionSettings settings)
        {
            foreach (var rule in settings.CustomProviders)
            {
                if (EntryExists(directory, rule.Marker))
                    return new DetectionResult(RepositoryKind.Custom, directory, rule.ProviderGuid, "custom marker '" + rule.Marker + "'");
            }

            // Explicit TFVC bindings in the solution file win over the folder markers next to it.
            if (solutionFile != null && settings.DetectTeamFoundationFromSolution && HasTeamFoundationBindings(solutionFile))
                return new DetectionResult(RepositoryKind.TeamFoundation, solutionFile, Guid.Empty, "TFVC bindings in the solution file");

            // .git is a folder in a regular clone and a file in worktrees and submodules.
            if (EntryExists(directory, GitMarker))
                return new DetectionResult(RepositoryKind.Git, directory, Guid.Empty, "'" + GitMarker + "' found");

            if (Directory.Exists(Path.Combine(directory, SubversionMarker)))
                return new DetectionResult(RepositoryKind.Subversion, directory, Guid.Empty, "'" + SubversionMarker + "' folder found");

            if (Directory.Exists(Path.Combine(directory, TeamFoundationLocalWorkspaceMarker)))
                return new DetectionResult(RepositoryKind.TeamFoundation, directory, Guid.Empty, "TFVC local workspace ('" + TeamFoundationLocalWorkspaceMarker + "') found");

            return DetectionResult.None;
        }

        private static bool EntryExists(string directory, string name)
        {
            string path = Path.Combine(directory, name);
            return Directory.Exists(path) || File.Exists(path);
        }

        public static bool HasTeamFoundationBindings(string solutionPath)
        {
            if (string.IsNullOrWhiteSpace(solutionPath))
                return false;

            string extension = Path.GetExtension(solutionPath);
            bool isSln = string.Equals(extension, ".sln", StringComparison.OrdinalIgnoreCase);
            bool isSlnx = string.Equals(extension, ".slnx", StringComparison.OrdinalIgnoreCase);
            if (!(isSln || isSlnx) || !File.Exists(solutionPath))
                return false;

            try
            {
                return isSln ? HasSlnBindings(solutionPath) : HasSlnxBindings(solutionPath);
            }
            catch (Exception ex) when (IsPathException(ex) || ex is XmlException)
            {
                return false;
            }
        }

        // The key only counts inside its own section: the same text in a comment or a project name is not a binding.
        private static bool HasSlnBindings(string solutionPath)
        {
            bool inSection = false;
            foreach (var rawLine in File.ReadLines(solutionPath))
            {
                string line = rawLine.Trim();
                if (!inSection)
                    inSection = line.StartsWith("GlobalSection(" + TeamFoundationSection + ")", StringComparison.OrdinalIgnoreCase);
                else if (line.StartsWith("EndGlobalSection", StringComparison.OrdinalIgnoreCase))
                    inSection = false;
                else if (IsServerBinding(line))
                    return true;
            }
            return false;
        }

        private static bool IsServerBinding(string line)
        {
            int separator = line.IndexOf('=');
            return separator > 0
                && string.Equals(line.Substring(0, separator).Trim(), TeamFoundationSolutionBinding, StringComparison.OrdinalIgnoreCase)
                && line.Substring(separator + 1).Trim().Length > 0;
        }

        // .slnx keeps the same section as <Properties Name="TeamFoundationVersionControl"><Property Name=... Value=... />.
        private static bool HasSlnxBindings(string solutionPath)
        {
            var readerSettings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, IgnoreComments = true };
            // A stream, not the path: XmlReader would read the path as a URI ('#' and '%' are legal in folder names).
            using (var stream = File.OpenRead(solutionPath))
            using (var reader = XmlReader.Create(stream, readerSettings))
            {
                while (reader.ReadToFollowing("Properties"))
                {
                    if (!string.Equals(reader.GetAttribute("Name"), TeamFoundationSection, StringComparison.OrdinalIgnoreCase))
                        continue;

                    using (var section = reader.ReadSubtree())
                    {
                        while (section.ReadToFollowing("Property"))
                        {
                            if (string.Equals(section.GetAttribute("Name"), TeamFoundationSolutionBinding, StringComparison.OrdinalIgnoreCase)
                                && !string.IsNullOrWhiteSpace(section.GetAttribute("Value")))
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            return false;
        }

        private static bool IsPathException(Exception ex)
        {
            return ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException
                || ex is NotSupportedException || ex is SecurityException;
        }
    }
}
