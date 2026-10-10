// Copyright (c) 2013-2014 Francesco Pretto
// This file is subject to the MIT license

using System;
using System.IO;
using System.Security;

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

        // Present in the GlobalSection(TeamFoundationVersionControl) of a solution bound to TFVC.
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

            // Explicit TFVC bindings in the solution file win over the folder layout.
            if (settings.DetectTeamFoundationFromSolution && HasTeamFoundationBindings(solutionOrFolderPath))
                return new DetectionResult(RepositoryKind.TeamFoundation, solutionOrFolderPath, Guid.Empty, "TFVC bindings in the solution file");

            try
            {
                for (var directory = new DirectoryInfo(startDirectory); directory != null; directory = settings.SearchParentFolders ? directory.Parent : null)
                {
                    var result = DetectInDirectory(directory.FullName, settings);
                    if (result.IsDetected)
                        return result;
                }
            }
            catch (Exception ex) when (IsPathException(ex))
            {
                // An unreadable parent folder ends the search; whatever was below it has already been checked.
            }

            return DetectionResult.None;
        }

        private static DetectionResult DetectInDirectory(string directory, DetectionSettings settings)
        {
            foreach (var rule in settings.CustomProviders)
            {
                if (EntryExists(directory, rule.Marker))
                    return new DetectionResult(RepositoryKind.Custom, directory, rule.ProviderGuid, "custom marker '" + rule.Marker + "'");
            }

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
            if (string.IsNullOrWhiteSpace(solutionPath)
                || !string.Equals(Path.GetExtension(solutionPath), ".sln", StringComparison.OrdinalIgnoreCase)
                || !File.Exists(solutionPath))
            {
                return false;
            }

            try
            {
                foreach (var line in File.ReadLines(solutionPath))
                {
                    if (line.IndexOf(TeamFoundationSolutionBinding, StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                }
            }
            catch (Exception ex) when (IsPathException(ex))
            {
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
