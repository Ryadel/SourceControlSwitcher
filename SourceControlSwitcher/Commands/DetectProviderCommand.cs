using System;
using System.ComponentModel.Design;
using Microsoft.VisualStudio.Shell;
using SourceControlSwitcher.Services;

namespace SourceControlSwitcher.Commands
{
    /// <summary>
    /// Tools > Detect Source Control Provider: re-runs detection for the open solution or folder and activates the
    /// matching provider, also after the provider was changed by hand or when automatic switching is off.
    /// </summary>
    internal static class DetectProviderCommand
    {
        public static void Register(IMenuCommandService commandService, SourceControlSwitchService switcher)
        {
            if (commandService == null)
                throw new ArgumentNullException(nameof(commandService));
            if (switcher == null)
                throw new ArgumentNullException(nameof(switcher));

            var commandId = new CommandID(PackageGuids.CommandSet, PackageIds.DetectProviderCommand);
            var command = new MenuCommand((sender, e) =>
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                switcher.DetectNow();
            }, commandId);
            commandService.AddCommand(command);
        }
    }
}
