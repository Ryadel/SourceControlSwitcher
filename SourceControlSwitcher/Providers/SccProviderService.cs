using System;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace SourceControlSwitcher.Providers
{
    internal enum ActivationOutcome
    {
        AlreadyActive,
        Activated,
        Failed
    }

    /// <summary>
    /// Thin wrapper over the Visual Studio services that list, query and activate SCC providers. UI thread only.
    /// </summary>
    internal sealed class SccProviderService
    {
        private readonly IVsShell _shell;
        private readonly IVsRegisterScciProvider _register;
        private readonly IVsGetScciProviderInterface _current;

        public SccProviderService(IVsShell shell, IVsRegisterScciProvider register, IVsGetScciProviderInterface current)
        {
            _shell = shell ?? throw new ArgumentNullException(nameof(shell));
            _register = register ?? throw new ArgumentNullException(nameof(register));
            _current = current ?? throw new ArgumentNullException(nameof(current));
        }

        public bool IsInstalled(SccProviderInfo provider)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            foreach (var packageGuid in provider.PackageGuids)
            {
                Guid guid = packageGuid;
                if (ErrorHandler.Succeeded(_shell.IsPackageInstalled(ref guid, out int installed)) && installed != 0)
                    return true;
            }
            return false;
        }

        public Guid GetActiveProviderGuid()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _current.GetSourceControlProviderID(out Guid providerGuid);
            return providerGuid;
        }

        public ActivationOutcome Activate(Guid providerGuid, out int hresult)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            hresult = VSConstants.S_OK;
            if (GetActiveProviderGuid() == providerGuid)
                return ActivationOutcome.AlreadyActive;

            hresult = _register.RegisterSourceControlProvider(providerGuid);
            return ErrorHandler.Succeeded(hresult) ? ActivationOutcome.Activated : ActivationOutcome.Failed;
        }

        public static string Describe(Guid providerGuid)
        {
            if (providerGuid == Guid.Empty)
                return "none";
            return KnownSccProviders.FindByProviderGuid(providerGuid)?.Name ?? providerGuid.ToString("B");
        }
    }
}
