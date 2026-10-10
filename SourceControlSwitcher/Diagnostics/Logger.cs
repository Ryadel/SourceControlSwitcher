using System;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using SourceControlSwitcher.Options;

namespace SourceControlSwitcher.Diagnostics
{
    /// <summary>
    /// Writes to the "Source Control Switcher" Output window pane (created on first use) and to the Visual Studio
    /// activity log (recorded only when VS runs with /log). UI thread only.
    /// </summary>
    internal sealed class Logger
    {
        private const string Source = "Source Control Switcher";
        private static readonly Guid PaneGuid = new Guid("f83a7498-b575-46b9-906d-d28a26fc263e");

        private readonly IVsOutputWindow _outputWindow;
        private IVsOutputWindowPane _pane;

        public Logger(IVsOutputWindow outputWindow)
        {
            _outputWindow = outputWindow;
        }

        public LogLevel Level { get; set; } = LogLevel.Normal;

        public void Info(string message)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Write(LogLevel.Normal, message);
        }

        public void Verbose(string message)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Write(LogLevel.Verbose, message);
        }

        public void Error(string message, Exception exception = null)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            string text = exception == null ? message : message + ": " + exception.Message;
            ActivityLog.TryLogError(Source, exception == null ? text : text + Environment.NewLine + exception);
            WriteToPane(LogLevel.Normal, "ERROR " + text);
        }

        private void Write(LogLevel level, string message)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            ActivityLog.TryLogInformation(Source, message);
            WriteToPane(level, message);
        }

        private void WriteToPane(LogLevel level, string message)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (Level == LogLevel.Off || level > Level)
                return;

            var pane = GetPane();
            pane?.OutputStringThreadSafe(string.Format("[{0:HH:mm:ss}] {1}{2}", DateTime.Now, message, Environment.NewLine));
        }

        private IVsOutputWindowPane GetPane()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (_pane != null || _outputWindow == null)
                return _pane;

            Guid paneGuid = PaneGuid;
            if (ErrorHandler.Failed(_outputWindow.GetPane(ref paneGuid, out _pane)) || _pane == null)
            {
                if (ErrorHandler.Succeeded(_outputWindow.CreatePane(ref paneGuid, Source, fInitVisible: 1, fClearWithSolution: 0)))
                    _outputWindow.GetPane(ref paneGuid, out _pane);
            }
            return _pane;
        }
    }
}
