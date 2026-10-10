using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using SourceControlSwitcher.Detection;
using SourceControlSwitcher.Diagnostics;
using SourceControlSwitcher.Options;
using SourceControlSwitcher.Providers;

namespace SourceControlSwitcher.Services
{
    /// <summary>
    /// Decides which source control provider the open solution or folder needs and activates it.
    /// All public members run on the UI thread; only the Perforce check runs in the background.
    /// </summary>
    internal sealed class SourceControlSwitchService
    {
        private const string StatusPrefix = "Source Control Switcher: ";

        private readonly AsyncPackage _package;
        private readonly SccProviderService _providers;
        private readonly IVsSolution _solution;
        private readonly IVsStatusbar _statusBar;
        private readonly Logger _log;
        private readonly Func<GeneralOptions> _getOptions;

        // Path evaluated last, and a counter that invalidates background checks started for an older one.
        private string _currentPath;
        private int _generation;

        // Settings that affect detection/selection at the last evaluation; see OnOptionsChanged.
        private string _lastSwitchingSignature;

        public SourceControlSwitchService(AsyncPackage package, SccProviderService providers, IVsSolution solution,
            IVsStatusbar statusBar, Logger log, Func<GeneralOptions> getOptions)
        {
            _package = package ?? throw new ArgumentNullException(nameof(package));
            _providers = providers ?? throw new ArgumentNullException(nameof(providers));
            _solution = solution ?? throw new ArgumentNullException(nameof(solution));
            _statusBar = statusBar;
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _getOptions = getOptions ?? throw new ArgumentNullException(nameof(getOptions));
        }

        /// <summary>A solution is about to open: switching now lets the provider see the solution load.</summary>
        public void OnSolutionOpening(string solutionPath)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Evaluate(solutionPath, force: false, userInitiated: false);
        }

        /// <summary>A solution or folder finished opening, or was already open when the package loaded.</summary>
        public void OnSolutionOrFolderOpened(string path = null)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Evaluate(path ?? GetOpenSolutionOrFolderPath(), force: false, userInitiated: false);
        }

        public void OnSolutionOrFolderClosed()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _currentPath = null;
            _generation++;
        }

        /// <summary>
        /// Options were applied: re-evaluate the open solution, but only when a setting that affects the outcome
        /// changed (so applying e.g. a new logging level does not override a provider chosen by hand), and only
        /// once the Options dialog has closed and VS is idle.
        /// </summary>
        public void OnOptionsChanged()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var options = _getOptions();
            _log.Level = options.LogLevel;

            string signature = options.GetSwitchingSignature();
            if (signature == _lastSwitchingSignature)
                return;
            _lastSwitchingSignature = signature;

            _package.JoinableTaskFactory.StartOnIdle(() =>
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                string path = _currentPath ?? GetOpenSolutionOrFolderPath();
                if (path != null)
                    Evaluate(path, force: true, userInitiated: false);
            }).FileAndForget("SourceControlSwitcher/OptionsChanged");
        }

        /// <summary>Tools > Detect Source Control Provider: always runs, even when automatic switching is off.</summary>
        public void DetectNow()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            string path = GetOpenSolutionOrFolderPath() ?? _currentPath;
            if (path == null)
            {
                SetStatus("no solution or folder is open.");
                return;
            }
            Evaluate(path, force: true, userInitiated: true);
        }

        private void Evaluate(string path, bool force, bool userInitiated)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            path = NormalizePath(path);
            if (path == null)
                return;

            var options = _getOptions();
            _log.Level = options.LogLevel;
            _lastSwitchingSignature = options.GetSwitchingSignature();

            if (!userInitiated && !options.AutoSwitch)
            {
                _generation++; // Also discards a Perforce check still running for an earlier evaluation.
                _log.Verbose("Automatic switching is off; ignoring " + path);
                return;
            }

            // OnBeforeOpenSolution and OnAfterOpenSolution both report the same solution: evaluate it once.
            if (!force && _currentPath != null && string.Equals(_currentPath, path, StringComparison.OrdinalIgnoreCase))
                return;

            _currentPath = path;
            int generation = ++_generation;

            var settings = options.ToDetectionSettings(out IReadOnlyList<string> invalidCustomEntries);
            foreach (var entry in invalidCustomEntries)
                _log.Info("Ignoring invalid custom provider entry '" + entry + "' (expected marker=providerGuid).");

            _log.Verbose("Detecting repository for " + path);
            var result = RepositoryDetector.Detect(path, settings);
            if (result.IsDetected)
            {
                Apply(result, options, userInitiated);
                return;
            }

            if (options.DetectPerforce)
            {
                StartPerforceDetection(path, TimeSpan.FromSeconds(options.PerforceTimeoutSeconds), generation, userInitiated);
                return;
            }

            ReportNothingDetected(path, userInitiated);
        }

        private void StartPerforceDetection(string path, TimeSpan timeout, int generation, bool userInitiated)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            string directory = RepositoryDetector.GetStartDirectory(path);
            _log.Verbose("No marker found; checking Perforce in the background.");

            _package.JoinableTaskFactory.RunAsync(async () =>
            {
                var token = _package.DisposalToken;

                // Both the PATH lookup (File.Exists on every entry, possibly on network drives) and p4 itself can
                // block: keep them off the UI thread.
                string client = null;
                bool inWorkspace = await Task.Run(() =>
                {
                    client = PerforceDetector.FindClient();
                    return client != null && PerforceDetector.IsInWorkspace(client, directory, timeout, token);
                }, token);
                await _package.JoinableTaskFactory.SwitchToMainThreadAsync(token);

                if (generation != _generation)
                    return; // Another solution was opened meanwhile.

                if (client == null)
                    _log.Verbose(PerforceDetector.ClientExecutable + " not found on PATH; Perforce detection skipped.");

                if (inWorkspace)
                {
                    var result = new DetectionResult(RepositoryKind.Perforce, directory, Guid.Empty, "'p4 where' succeeded");
                    Apply(result, _getOptions(), userInitiated);
                }
                else
                {
                    ReportNothingDetected(path, userInitiated);
                }
            }).FileAndForget("SourceControlSwitcher/PerforceDetection");
        }

        private void Apply(DetectionResult result, GeneralOptions options, bool userInitiated)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _log.Verbose(string.Format("Detected {0}: {1} ({2})", result.Kind, result.Evidence, result.Location));

            Guid providerGuid;
            string providerName;
            if (result.Kind == RepositoryKind.Custom)
            {
                providerGuid = result.CustomProviderGuid;
                providerName = SccProviderService.Describe(providerGuid);
            }
            else
            {
                var provider = ProviderSelector.Select(result.Kind, options.ToProviderPreferences(), _providers.IsInstalled);
                if (provider == null)
                {
                    string reason = result.Kind + " repository detected, but switching is disabled for it or no supported provider is installed.";
                    _log.Info(reason);
                    if (userInitiated)
                        SetStatus(reason);
                    return;
                }
                if (!_providers.IsInstalled(provider))
                {
                    string reason = result.Kind + " repository detected, but the selected provider (" + provider.Name + ") is not installed.";
                    _log.Info(reason);
                    if (userInitiated)
                        SetStatus(reason);
                    return;
                }
                providerGuid = provider.ProviderGuid;
                providerName = provider.Name;
            }

            switch (_providers.Activate(providerGuid, out int hr))
            {
                case ActivationOutcome.AlreadyActive:
                    _log.Verbose(providerName + " is already the active provider.");
                    if (userInitiated)
                        SetStatus(providerName + " is already active (" + result.Kind + ").");
                    break;

                case ActivationOutcome.Activated:
                    _log.Info(string.Format("Activated {0} for {1} ({2}).", providerName, result.Location, result.Evidence));
                    if (userInitiated || options.ShowStatusBarNotifications)
                        SetStatus("activated " + providerName + ".");
                    break;

                default:
                    _log.Error(string.Format("Could not activate {0} (HRESULT 0x{1:X8}).", providerName, hr));
                    if (userInitiated)
                        SetStatus("could not activate " + providerName + ".");
                    break;
            }
        }

        private void ReportNothingDetected(string path, bool userInitiated)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            string message = "No repository detected for " + Path.GetFileName(path.TrimEnd('\\', '/')) + "; provider left unchanged ("
                + SccProviderService.Describe(_providers.GetActiveProviderGuid()) + ").";
            _log.Verbose(message);
            if (userInitiated)
                SetStatus(message);
        }

        private void SetStatus(string message)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (_statusBar == null)
                return;

            if (ErrorHandler.Succeeded(_statusBar.IsFrozen(out int frozen)) && frozen != 0)
                return;

            _statusBar.SetText(StatusPrefix + message);
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;
            try
            {
                string fullPath = Path.GetFullPath(path);
                // Keep "C:\" as is: "C:" would mean the current directory on drive C.
                return fullPath.Length > (Path.GetPathRoot(fullPath)?.Length ?? 0) ? fullPath.TrimEnd('\\', '/') : fullPath;
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is IOException || ex is System.Security.SecurityException)
            {
                return null;
            }
        }

        /// <summary>The open solution file, or the folder in Open Folder mode; null when nothing is open.</summary>
        private string GetOpenSolutionOrFolderPath()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            bool isOpen = ErrorHandler.Succeeded(_solution.GetProperty((int)__VSPROPID.VSPROPID_IsSolutionOpen, out object open)) && open is bool b && b;
            bool isFolder = ErrorHandler.Succeeded(_solution.GetProperty((int)__VSPROPID7.VSPROPID_IsInOpenFolderMode, out object folder)) && folder is bool f && f;
            if (!isOpen && !isFolder)
                return null;

            if (ErrorHandler.Failed(_solution.GetSolutionInfo(out string directory, out string file, out _)))
                return null;

            if (!string.IsNullOrWhiteSpace(file) && !isFolder)
                return file;
            return string.IsNullOrWhiteSpace(directory) ? null : directory;
        }
    }
}
