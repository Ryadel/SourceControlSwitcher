using System;
using System.IO;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SourceControlSwitcher.Detection;

namespace SourceControlSwitcher.Tests
{
    [TestClass]
    [DoNotParallelize] // Modifies the process PATH.
    public sealed class PerforceDetectorTests
    {
        [TestMethod]
        public void FindClient_LooksUpPath()
        {
            string originalPath = Environment.GetEnvironmentVariable("PATH");
            using (var tree = new TempTree())
            {
                try
                {
                    string bin = tree.Dir("bin");
                    Environment.SetEnvironmentVariable("PATH", "\"" + tree.Dir("empty") + "\";;bad|entry");
                    Assert.IsNull(PerforceDetector.FindClient());

                    tree.File("bin/" + PerforceDetector.ClientExecutable);
                    Environment.SetEnvironmentVariable("PATH", tree.Dir("empty") + Path.PathSeparator + bin);
                    Assert.AreEqual(Path.Combine(bin, PerforceDetector.ClientExecutable), PerforceDetector.FindClient());
                }
                finally
                {
                    Environment.SetEnvironmentVariable("PATH", originalPath);
                }
            }
        }

        [TestMethod]
        public void WhereOutput_MappedFolder_IsWorkspace()
        {
            var lines = new[] { "info: //depot/app/... //ws/app/... C:\\ws\\app\\...", "exit: 0" };
            Assert.IsTrue(PerforceDetector.IsWorkspaceOutput(lines, 0));
        }

        [TestMethod]
        [DataRow(0, "warning: C:\\other\\... - file(s) not in client view.", "exit: 0")]
        [DataRow(1, "error: Path 'C:\\other\\...' is not under client's root 'C:\\ws'.", "exit: 1")]
        [DataRow(0, "info: //depot/app/... //ws/app/... C:\\ws\\app\\...", "exit: 1")]
        [DataRow(1, "info: //depot/app/... //ws/app/... C:\\ws\\app\\...", "exit: 0")]
        [DataRow(0, "text: nothing useful", "exit: 0")]
        public void WhereOutput_WarningsErrorsOrNoMapping_AreNotWorkspace(int exitCode, string line1, string line2)
        {
            Assert.IsFalse(PerforceDetector.IsWorkspaceOutput(new[] { line1, line2 }, exitCode));
        }

        [TestMethod]
        public void IsInWorkspace_MissingClient_ReturnsFalse()
        {
            using (var tree = new TempTree())
            {
                string missing = Path.Combine(tree.Root, "nope", PerforceDetector.ClientExecutable);
                Assert.IsFalse(PerforceDetector.IsInWorkspace(missing, tree.Root, TimeSpan.FromSeconds(1), CancellationToken.None));
                Assert.IsFalse(PerforceDetector.IsInWorkspace(null, tree.Root, TimeSpan.FromSeconds(1), CancellationToken.None));
            }
        }
    }
}
