using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace SourceControlSwitcher.Detection
{
    /// <summary>
    /// Perforce leaves no marker on disk, so the only reliable check is asking the client (<c>p4 where</c>).
    /// That starts a process and may contact the server: never call <see cref="IsInWorkspace"/> (or
    /// <see cref="FindClient"/>, which probes every PATH entry) on the UI thread.
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

            // -s prefixes every output line with its severity (info:, warning:, error:, exit:), so the outcome does
            // not depend on the exit code alone: p4 can exit 0 after only printing a warning.
            var startInfo = new ProcessStartInfo(clientPath, "-s where")
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
                var output = new ConcurrentQueue<string>();
                using (var process = new Process { StartInfo = startInfo })
                using (cancellationToken.Register(() => TryKill(process)))
                {
                    // Read both streams so a verbose client cannot block on a full pipe.
                    process.OutputDataReceived += (s, e) => { if (e.Data != null) output.Enqueue(e.Data); };
                    process.ErrorDataReceived += (s, e) => { if (e.Data != null) output.Enqueue(e.Data); };

                    if (!process.Start())
                        return false;

                    // A token cancelled before Start() fired its callback while there was nothing to kill.
                    if (cancellationToken.IsCancellationRequested)
                    {
                        TryKill(process);
                        return false;
                    }

                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    if (!process.WaitForExit((int)timeout.TotalMilliseconds))
                    {
                        TryKill(process);
                        return false;
                    }
                    process.WaitForExit(); // Flushes the asynchronous output handlers.

                    return !cancellationToken.IsCancellationRequested && IsWorkspaceOutput(output, process.ExitCode);
                }
            }
            catch (Exception ex) when (ex is Win32Exception || ex is InvalidOperationException || ex is IOException)
            {
                return false;
            }
        }

        /// <summary>
        /// Interprets the output of <c>p4 -s where</c>: the folder is in the workspace when p4 printed at least one
        /// info line (the depot/client/local mapping) and no warning or error, and exited with 0.
        /// </summary>
        public static bool IsWorkspaceOutput(IEnumerable<string> lines, int exitCode)
        {
            if (exitCode != 0 || lines == null)
                return false;

            bool mapped = false;
            foreach (var rawLine in lines)
            {
                string line = rawLine.TrimStart();
                if (line.StartsWith("error", StringComparison.OrdinalIgnoreCase) || line.StartsWith("warning", StringComparison.OrdinalIgnoreCase))
                    return false;

                if (line.StartsWith("exit:", StringComparison.OrdinalIgnoreCase))
                {
                    if (line.Substring("exit:".Length).Trim() != "0")
                        return false;
                }
                else if (line.StartsWith("info", StringComparison.OrdinalIgnoreCase))
                {
                    mapped = true; // "info:" or "info1:" (nested) lines carry the mapping.
                }
            }
            return mapped;
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
