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
