using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SourceControlSwitcher.Detection;

namespace SourceControlSwitcher.Tests
{
    [TestClass]
    public sealed class RepositoryDetectorTests
    {
        // Parent folders of the temp tree are outside the test's control: tests that expect "nothing found"
        // disable the upward search so a marker above the temp folder cannot interfere.
        private static readonly DetectionSettings Defaults = new DetectionSettings();
        private static readonly DetectionSettings NoParents = new DetectionSettings { SearchParentFolders = false };

        private const string SlnWithBindings =
            "Global\r\n\tGlobalSection(TeamFoundationVersionControl) = preSolution\r\n" +
            "\t\tSccTeamFoundationServer = https://tfs.example.com/tfs/DefaultCollection\r\n\tEndGlobalSection\r\nEndGlobal\r\n";

        private const string SlnxWithBindings =
            "<Solution>\r\n  <Properties Name=\"TeamFoundationVersionControl\">\r\n" +
            "    <Property Name=\"SccTeamFoundationServer\" Value=\"https://tfs.example.com/tfs\" />\r\n  </Properties>\r\n</Solution>\r\n";

        [TestMethod]
        public void GitFolder_IsDetected()
        {
            using (var tree = new TempTree())
            {
                tree.Dir("repo/.git");
                string sln = tree.File("repo/App.sln");

                var result = RepositoryDetector.Detect(sln, Defaults);

                Assert.AreEqual(RepositoryKind.Git, result.Kind);
                Assert.AreEqual(tree.Dir("repo"), result.Location);
            }
        }

        [TestMethod]
        public void GitFile_WorktreeOrSubmodule_IsDetected()
        {
            using (var tree = new TempTree())
            {
                tree.File("worktree/.git", "gitdir: ../main/.git/worktrees/feature");
                string sln = tree.File("worktree/App.slnx");

                Assert.AreEqual(RepositoryKind.Git, RepositoryDetector.Detect(sln, Defaults).Kind);
            }
        }

        [TestMethod]
        public void Subversion_IsDetected_FromParentFolder()
        {
            using (var tree = new TempTree())
            {
                tree.Dir("wc/.svn");
                string sln = tree.File("wc/src/deep/App.sln");

                var result = RepositoryDetector.Detect(sln, Defaults);

                Assert.AreEqual(RepositoryKind.Subversion, result.Kind);
                Assert.AreEqual(tree.Dir("wc"), result.Location);
            }
        }

        [TestMethod]
        public void ParentSearchDisabled_IgnoresMarkersAbove()
        {
            using (var tree = new TempTree())
            {
                tree.Dir("wc/.svn");
                string sln = tree.File("wc/src/App.sln");

                Assert.AreEqual(RepositoryKind.None, RepositoryDetector.Detect(sln, NoParents).Kind);
            }
        }

        [TestMethod]
        public void NearestMarker_Wins()
        {
            using (var tree = new TempTree())
            {
                tree.Dir("outer/.svn");
                tree.Dir("outer/inner/.git");
                string sln = tree.File("outer/inner/App.sln");

                Assert.AreEqual(RepositoryKind.Git, RepositoryDetector.Detect(sln, Defaults).Kind);
            }
        }

        [TestMethod]
        public void TeamFoundation_LocalWorkspace_IsDetected()
        {
            using (var tree = new TempTree())
            {
                tree.Dir("ws/$tf");
                string sln = tree.File("ws/App/App.sln");

                Assert.AreEqual(RepositoryKind.TeamFoundation, RepositoryDetector.Detect(sln, Defaults).Kind);
            }
        }

        [TestMethod]
        public void TeamFoundation_SolutionBindings_WinOverFolderMarkers()
        {
            using (var tree = new TempTree())
            {
                tree.Dir("repo/.git");
                string sln = tree.File("repo/App.sln", SlnWithBindings);

                Assert.AreEqual(RepositoryKind.TeamFoundation, RepositoryDetector.Detect(sln, Defaults).Kind);
            }
        }

        [TestMethod]
        public void TeamFoundation_SlnxBindings_AreDetected()
        {
            using (var tree = new TempTree())
            {
                string slnx = tree.File("repo/App.slnx", SlnxWithBindings);

                Assert.AreEqual(RepositoryKind.TeamFoundation, RepositoryDetector.Detect(slnx, NoParents).Kind);
            }
        }

        [TestMethod]
        public void TeamFoundation_SlnxBindings_InFolderWithUriCharacters_AreDetected()
        {
            using (var tree = new TempTree())
            {
                string slnx = tree.File("C# 100%/App.slnx", SlnxWithBindings);

                Assert.AreEqual(RepositoryKind.TeamFoundation, RepositoryDetector.Detect(slnx, NoParents).Kind);
            }
        }

        [TestMethod]
        public void TeamFoundation_KeyOutsideBindingSection_IsIgnored()
        {
            using (var tree = new TempTree())
            {
                tree.Dir("repo/.git");
                string sln = tree.File("repo/App.sln",
                    "# SccTeamFoundationServer = https://tfs.example.com/tfs\r\n" +
                    "Project(\"{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}\") = \"SccTeamFoundationServer\", \"A\\A.csproj\", \"{1}\"\r\nEndProject\r\n" +
                    "Global\r\n\tGlobalSection(SolutionProperties) = preSolution\r\n\t\tSccTeamFoundationServer = https://tfs.example.com/tfs\r\n" +
                    "\tEndGlobalSection\r\n\tGlobalSection(TeamFoundationVersionControl) = preSolution\r\n\t\tSccTeamFoundationServer =\r\n" +
                    "\tEndGlobalSection\r\nEndGlobal\r\n");

                Assert.AreEqual(RepositoryKind.Git, RepositoryDetector.Detect(sln, Defaults).Kind);
            }
        }

        [TestMethod]
        public void TeamFoundation_SlnxKeyOutsideBindingSection_IsIgnored()
        {
            using (var tree = new TempTree())
            {
                string slnx = tree.File("repo/App.slnx",
                    "<Solution>\r\n  <!-- <Property Name=\"SccTeamFoundationServer\" Value=\"https://tfs.example.com/tfs\" /> -->\r\n" +
                    "  <Properties Name=\"Other\">\r\n    <Property Name=\"SccTeamFoundationServer\" Value=\"https://tfs.example.com/tfs\" />\r\n" +
                    "  </Properties>\r\n  <Project Path=\"SccTeamFoundationServer/A.csproj\" />\r\n</Solution>\r\n");

                Assert.AreEqual(RepositoryKind.None, RepositoryDetector.Detect(slnx, NoParents).Kind);
            }
        }

        [TestMethod]
        public void MalformedSlnx_IsNotTeamFoundation()
        {
            using (var tree = new TempTree())
            {
                tree.Dir("repo/.git");
                string slnx = tree.File("repo/App.slnx", "<Solution><Properties Name=\"TeamFoundationVersionControl\">");

                Assert.AreEqual(RepositoryKind.Git, RepositoryDetector.Detect(slnx, Defaults).Kind);
            }
        }

        [TestMethod]
        public void CustomMarker_WinsOverSolutionBindings()
        {
            using (var tree = new TempTree())
            {
                var providerGuid = Guid.NewGuid();
                tree.File("repo/.vault", "");
                string sln = tree.File("repo/App.sln", SlnWithBindings);
                var settings = new DetectionSettings { CustomProviders = new[] { new CustomProviderRule(".vault", providerGuid) } };

                Assert.AreEqual(RepositoryKind.Custom, RepositoryDetector.Detect(sln, settings).Kind);
            }
        }

        [TestMethod]
        public void TeamFoundation_SolutionBindings_CanBeDisabled()
        {
            using (var tree = new TempTree())
            {
                tree.Dir("repo/.git");
                string sln = tree.File("repo/App.sln", SlnWithBindings);
                var settings = new DetectionSettings { DetectTeamFoundationFromSolution = false };

                Assert.AreEqual(RepositoryKind.Git, RepositoryDetector.Detect(sln, settings).Kind);
            }
        }

        [TestMethod]
        public void CustomMarker_IsCheckedBeforeBuiltIns()
        {
            using (var tree = new TempTree())
            {
                var providerGuid = Guid.NewGuid();
                tree.Dir("repo/.git");
                tree.File("repo/.vault", "");
                string sln = tree.File("repo/App.sln");
                var settings = new DetectionSettings { CustomProviders = new[] { new CustomProviderRule(".vault", providerGuid) } };

                var result = RepositoryDetector.Detect(sln, settings);

                Assert.AreEqual(RepositoryKind.Custom, result.Kind);
                Assert.AreEqual(providerGuid, result.CustomProviderGuid);
            }
        }

        [TestMethod]
        public void OpenFolderPath_IsSearchedFromTheFolderItself()
        {
            using (var tree = new TempTree())
            {
                tree.Dir("folder/.git");

                Assert.AreEqual(RepositoryKind.Git, RepositoryDetector.Detect(tree.Dir("folder"), NoParents).Kind);
            }
        }

        [TestMethod]
        public void NoMarker_ReturnsNone()
        {
            using (var tree = new TempTree())
            {
                string sln = tree.File("plain/App.sln");

                var result = RepositoryDetector.Detect(sln, NoParents);

                Assert.IsFalse(result.IsDetected);
            }
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("   ")]
        [DataRow("C:\\invalid|path\\App.sln")]
        public void InvalidPath_ReturnsNone(string path)
        {
            Assert.AreEqual(RepositoryKind.None, RepositoryDetector.Detect(path, Defaults).Kind);
        }
    }
}
