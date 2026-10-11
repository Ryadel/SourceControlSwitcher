using System;
using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using SourceControlSwitcher.Commands;
using SourceControlSwitcher.Diagnostics;
using SourceControlSwitcher.Options;
using SourceControlSwitcher.Providers;
using SourceControlSwitcher.Services;
using SolutionEvents = Microsoft.VisualStudio.Shell.Events.SolutionEvents;
using Task = System.Threading.Tasks.Task;

namespace SourceControlSwitcher
{
    /// <summary>
    /// Package entry point: registration, service wiring and event subscriptions only. The logic lives in
    /// <see cref="SourceControlSwitchService"/>.
    /// </summary>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("#110", "#112", "1.2.1")]
    [Guid(PackageGuids.PackageString)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideOptionPage(typeof(GeneralOptions), "Source Control Switcher", "General", 0, 0, true)]
    // Load as soon as a solution starts opening (or a folder is opened), in the background. A solution that is
    // already open when the package finishes loading is handled at the end of InitializeAsync.
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionOpening_string, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExists_string, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.FolderOpened_string, PackageAutoLoadFlags.BackgroundLoad)]
    public sealed class SourceControlSwitcherPackage : AsyncPackage
    {
        private SourceControlSwitchService _switcher;

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            var shell = await GetServiceAsync(typeof(SVsShell)) as IVsShell;
            var registerScc = await GetServiceAsync(typeof(IVsRegisterScciProvider)) as IVsRegisterScciProvider;
            var solution = await GetServiceAsync(typeof(SVsSolution)) as IVsSolution;
            var outputWindow = await GetServiceAsync(typeof(SVsOutputWindow)) as IVsOutputWindow;
            var statusBar = await GetServiceAsync(typeof(SVsStatusbar)) as IVsStatusbar;
            var commandService = await GetServiceAsync(typeof(IMenuCommandService)) as IMenuCommandService;
            Assumes.Present(shell);
            Assumes.Present(registerScc);
            Assumes.Present(solution);
            Assumes.Present(commandService);

            // The SCC registration service also exposes the active provider.
            var currentScc = registerScc as IVsGetScciProviderInterface;
            Assumes.Present(currentScc);

            var log = new Logger(outputWindow);
            var providers = new SccProviderService(shell, registerScc, currentScc);
            _switcher = new SourceControlSwitchService(this, providers, solution, statusBar, log,
                () => (GeneralOptions)GetDialogPage(typeof(GeneralOptions)));

            DetectProviderCommand.Register(commandService, _switcher);

            SolutionEvents.OnBeforeOpenSolution += OnBeforeOpenSolution;
            SolutionEvents.OnAfterOpenSolution += OnAfterOpenSolution;
            SolutionEvents.OnAfterOpenFolder += OnAfterOpenFolder;
            SolutionEvents.OnAfterCloseSolution += OnAfterClose;
            SolutionEvents.OnAfterCloseFolder += OnAfterCloseFolder;
            GeneralOptions.Saved += OnOptionsSaved;

            _switcher.OnSolutionOrFolderOpened();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                SolutionEvents.OnBeforeOpenSolution -= OnBeforeOpenSolution;
                SolutionEvents.OnAfterOpenSolution -= OnAfterOpenSolution;
                SolutionEvents.OnAfterOpenFolder -= OnAfterOpenFolder;
                SolutionEvents.OnAfterCloseSolution -= OnAfterClose;
                SolutionEvents.OnAfterCloseFolder -= OnAfterCloseFolder;
                GeneralOptions.Saved -= OnOptionsSaved;
            }
            base.Dispose(disposing);
        }

        private void OnBeforeOpenSolution(object sender, Microsoft.VisualStudio.Shell.Events.BeforeOpenSolutionEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _switcher?.OnSolutionOpening(e.SolutionFilename);
        }

        private void OnAfterOpenSolution(object sender, Microsoft.VisualStudio.Shell.Events.OpenSolutionEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _switcher?.OnSolutionOrFolderOpened();
        }

        private void OnAfterOpenFolder(object sender, Microsoft.VisualStudio.Shell.Events.FolderEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _switcher?.OnSolutionOrFolderOpened(e.FolderPath);
        }

        private void OnAfterClose(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _switcher?.OnSolutionOrFolderClosed();
        }

        private void OnAfterCloseFolder(object sender, Microsoft.VisualStudio.Shell.Events.FolderEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _switcher?.OnSolutionOrFolderClosed();
        }

        private void OnOptionsSaved(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _switcher?.OnOptionsChanged();
        }
    }
}
