using System;
using System.ComponentModel;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SourceControlSwitcher.Detection;
using SourceControlSwitcher.Options;
using SourceControlSwitcher.Providers;

namespace SourceControlSwitcher.Tests
{
    [TestClass]
    public sealed class ConfigurationTests
    {
        [TestMethod]
        public void CustomProviders_ParsesValidEntries_AndReportsInvalidOnes()
        {
            var guid = new Guid("01234567-89ab-cdef-0123-456789abcdef");
            var rules = CustomProviderRule.ParseList(
                " .vault = " + guid + " ; broken ; =" + guid + "\n.x=not-a-guid\r\n.zero=" + Guid.Empty + ";a|b=" + guid,
                out var invalid);

            Assert.AreEqual(1, rules.Count);
            Assert.AreEqual(".vault", rules[0].Marker);
            Assert.AreEqual(guid, rules[0].ProviderGuid);
            CollectionAssert.AreEqual(new[] { "broken", "=" + guid, ".x=not-a-guid", ".zero=" + Guid.Empty, "a|b=" + guid }, invalid.ToArray());
        }

        [TestMethod]
        public void CustomProviders_EmptyValue_HasNoRules()
        {
            Assert.AreEqual(0, CustomProviderRule.ParseList(null, out var invalid).Count);
            Assert.AreEqual(0, invalid.Count);
        }

        [TestMethod]
        [DataRow("VisualStudioToolsForGit", GitProviderChoice.MicrosoftGit)]
        [DataRow("EasyGitIntegrationTools", GitProviderChoice.Default)]
        [DataRow("Disabled", GitProviderChoice.Disabled)]
        [DataRow("MicrosoftGit", GitProviderChoice.MicrosoftGit)]
        [DataRow(null, GitProviderChoice.Default)]
        public void GitProvider_LegacyValues_AreMapped(string stored, GitProviderChoice expected)
        {
            Assert.AreEqual(expected, SettingValues.ParseGitProvider(stored));
        }

        [TestMethod]
        public void LegacyValues_OfOtherProviders_AreMappedCaseInsensitively()
        {
            Assert.AreEqual(SubversionProviderChoice.AnkhSvn, SettingValues.ParseEnum("AnkhSVN", SubversionProviderChoice.Default));
            Assert.AreEqual(SubversionProviderChoice.VisualSvn, SettingValues.ParseEnum("VisualSVN", SubversionProviderChoice.Default));
            Assert.AreEqual(PerforceProviderChoice.P4Vs, SettingValues.ParseEnum("P4VS", PerforceProviderChoice.Default));
            Assert.AreEqual(PerforceProviderChoice.Default, SettingValues.ParseEnum("42", PerforceProviderChoice.Default));
        }

        [TestMethod]
        public void Selector_Automatic_PicksFirstInstalled_InPreferenceOrder()
        {
            var preferences = new ProviderPreferences();

            Assert.AreSame(KnownSccProviders.VisualSvn, ProviderSelector.Select(RepositoryKind.Subversion, preferences, p => true));
            Assert.AreSame(KnownSccProviders.AnkhSvn, ProviderSelector.Select(RepositoryKind.Subversion, preferences, p => p == KnownSccProviders.AnkhSvn));
            Assert.IsNull(ProviderSelector.Select(RepositoryKind.Subversion, preferences, p => false));
            Assert.AreSame(KnownSccProviders.MicrosoftGit, ProviderSelector.Select(RepositoryKind.Git, preferences, p => true));
            Assert.AreSame(KnownSccProviders.TeamFoundation, ProviderSelector.Select(RepositoryKind.TeamFoundation, preferences, p => true));
            Assert.AreSame(KnownSccProviders.P4Vs, ProviderSelector.Select(RepositoryKind.Perforce, preferences, p => true));
        }

        [TestMethod]
        public void Selector_ExplicitChoice_IsReturnedEvenWhenNotInstalled()
        {
            var preferences = new ProviderPreferences { Subversion = SubversionProviderChoice.AnkhSvn };

            Assert.AreSame(KnownSccProviders.AnkhSvn, ProviderSelector.Select(RepositoryKind.Subversion, preferences, p => false));
        }

        [TestMethod]
        public void Selector_Disabled_ReturnsNull()
        {
            var preferences = new ProviderPreferences { Git = GitProviderChoice.Disabled, TeamFoundation = TeamFoundationProviderChoice.Disabled };

            Assert.IsNull(ProviderSelector.Select(RepositoryKind.Git, preferences, p => true));
            Assert.IsNull(ProviderSelector.Select(RepositoryKind.TeamFoundation, preferences, p => true));
            Assert.IsNull(ProviderSelector.Select(RepositoryKind.None, new ProviderPreferences(), p => true));
        }

        [TestMethod]
        public void KnownProviders_HaveUniqueProviderGuids_AndCanBeFound()
        {
            Assert.AreEqual(KnownSccProviders.All.Count, KnownSccProviders.All.Select(p => p.ProviderGuid).Distinct().Count());
            foreach (var provider in KnownSccProviders.All)
                Assert.AreSame(provider, KnownSccProviders.FindByProviderGuid(provider.ProviderGuid));
            Assert.IsNull(KnownSccProviders.FindByProviderGuid(Guid.NewGuid()));
        }

        [TestMethod]
        public void EnumConverter_ShowsDescriptions_AndParsesThemBack()
        {
            var converter = TypeDescriptor.GetConverter(typeof(GitProviderChoice));

            Assert.IsInstanceOfType(converter, typeof(EnumDescriptionConverter));
            Assert.AreEqual("Automatic (first installed)", converter.ConvertToString(GitProviderChoice.Default));
            Assert.AreEqual(GitProviderChoice.Disabled, converter.ConvertFromString("Disabled (do not switch)"));
            Assert.AreEqual(GitProviderChoice.MicrosoftGit, converter.ConvertFromString("MicrosoftGit"));
        }
    }
}
