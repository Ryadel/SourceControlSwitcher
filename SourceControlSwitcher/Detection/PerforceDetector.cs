using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace SourceControlSwitcher.Detection
{
    /// <summary>
    /// Perforce leaves no marker on disk, so the only reliable check is asking the client (<c>p4 where</c>).
    /// That starts a process and may contact the server: never call <see cref="IsInWorkspace"/> on the UI thread.
    /// </summary>
    public static class PerforceDetector
    {
        public const string ClientExecutable = "p4.exe";

        /// <summary>Returns the full path of p4.exe found on PATH, or null when Perforce is not installed.</summary>
        public static string FindClient()
        {
            string path = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(path))
                return null;

            foreach (var entry in path.Split(Path.PathSeparator))
            {
                string directory = entry.Trim().Trim('"');
                if (directory.Length == 0)
                    continue;

                try
                {
                    string candidate = Path.Combine(directory, ClientExecutable);
                    if (File.Exists(candidate))
                        return candidate;
                }
                catch (ArgumentException)
                {
                    // Malformed PATH entry.
                }
            }
            return null;
        }

        /// <summary>
        /// True when <paramref name="directory"/> is mapped in the current Perforce client workspace.
        /// </summary>
        public static bool IsInWorkspace(string clientPath, string directory, TimeSpan timeout, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(clientPath) || string.IsNullOrEmpty(directory))
                return false;

            var startInfo = new ProcessStartInfo(clientPath, "where")
            {
                WorkingDirectory = directory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            // p4 resolves relative paths against PWD when it is set; make it agree with the working directory.
            startInfo.EnvironmentVariables["PWD"] = directory;

            try
            {
                using (var process = new Process { StartInfo = startInfo })
                using (cancellationToken.Register(() => TryKill(process)))
                {
                    if (!process.Start())
                        return false;

                    // Drain both streams so a verbose client cannot block on a full pipe.
                    process.OutputDataReceived += (s, e) => { };
                    process.ErrorDataReceived += (s, e) => { };
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    if (!process.WaitForExit((int)timeout.TotalMilliseconds))
                    {
                        TryKill(process);
                        return false;
                    }
                    return !cancellationToken.IsCancellationRequested && process.ExitCode == 0;
                }
            }
            catch (Exception ex) when (ex is Win32Exception || ex is InvalidOperationException || ex is IOException)
            {
                return false;
            }
        }

        private static void TryKill(Process process)
        {
            try
            {
                if (!process.HasExited)
                    process.Kill();
            }
            catch (Exception ex) when (ex is Win32Exception || ex is InvalidOperationException)
            {
            }
        }
    }
}
