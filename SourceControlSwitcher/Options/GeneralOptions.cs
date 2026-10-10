using System;
using System.ComponentModel;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Settings;
using SourceControlSwitcher.Detection;
using SourceControlSwitcher.Providers;

namespace SourceControlSwitcher.Options
{
    /// <summary>
    /// Tools > Options > Source Control Switcher > General.
    /// Values are stored in the user settings store, collection <see cref="CollectionPath"/>, which is where
    /// version 1.x saved them: existing provider choices are picked up on upgrade.
    /// </summary>
    public sealed class GeneralOptions : DialogPage
    {
        internal const string CollectionPath = "SourceControlSwitcher";

        private const string GeneralCategory = "General";
        private const string ProvidersCategory = "Providers";
        private const string DetectionCategory = "Detection";
        private const string CustomCategory = "Custom providers";

        private int _perforceTimeoutSeconds = 5;

        /// <summary>Raised on the UI thread after the user applies changes in the options dialog.</summary>
        public static event EventHandler Saved;

        [Category(GeneralCategory)]
        [DisplayName("Switch automatically")]
        [Description("Activate the matching source control provider when a solution or folder is opened. " +
            "When off, use Tools > Detect Source Control Provider to switch on demand.")]
        [DefaultValue(true)]
        public bool AutoSwitch { get; set; } = true;

        [Category(GeneralCategory)]
        [DisplayName("Show status bar notifications")]
        [Description("Show a message in the status bar when the source control provider is switched.")]
        [DefaultValue(true)]
        public bool ShowStatusBarNotifications { get; set; } = true;

        [Category(GeneralCategory)]
        [DisplayName("Logging")]
        [Description("Detail of the messages written to the 'Source Control Switcher' pane of the Output window.")]
        [DefaultValue(LogLevel.Normal)]
        public LogLevel LogLevel { get; set; } = LogLevel.Normal;

        [Category(ProvidersCategory)]
        [DisplayName("Git")]
        [Description("Provider to activate for Git repositories.")]
        [DefaultValue(GitProviderChoice.Default)]
        public GitProviderChoice GitProvider { get; set; }

        [Category(ProvidersCategory)]
        [DisplayName("Subversion")]
        [Description("Provider to activate for Subversion working copies. Automatic prefers VisualSVN, then AnkhSVN.")]
        [DefaultValue(SubversionProviderChoice.Default)]
        public SubversionProviderChoice SubversionProvider { get; set; }

        [Category(ProvidersCategory)]
        [DisplayName("Team Foundation (TFVC)")]
        [Description("Provider to activate for Team Foundation Version Control workspaces and TFVC-bound solutions.")]
        [DefaultValue(TeamFoundationProviderChoice.Default)]
        public TeamFoundationProviderChoice TeamFoundationProvider { get; set; }

        [Category(ProvidersCategory)]
        [DisplayName("Perforce")]
        [Description("Provider to activate for Helix/Perforce workspaces.")]
        [DefaultValue(PerforceProviderChoice.Default)]
        public PerforceProviderChoice PerforceProvider { get; set; }

        [Category(DetectionCategory)]
        [DisplayName("Search parent folders")]
        [Description("Look for repository markers (.git, .svn, $tf, custom markers) in the solution folder and all its " +
            "parents. When off, only the solution folder itself is checked.")]
        [DefaultValue(true)]
        public bool SearchParentFolders { get; set; } = true;

        [Category(DetectionCategory)]
        [DisplayName("Detect TFVC bindings in .sln files")]
        [Description("Treat a solution whose .sln/.slnx file contains Team Foundation bindings as a TFVC solution, even " +
            "when no local workspace marker is found. Turn off for repositories migrated from TFVC that still carry " +
            "old bindings.")]
        [DefaultValue(true)]
        public bool DetectTeamFoundationFromSolution { get; set; } = true;

        [Category(DetectionCategory)]
        [DisplayName("Detect Perforce")]
        [Description("When no marker is found, ask the Perforce client (p4 where) whether the solution is in a " +
            "workspace. Runs in the background and only when p4.exe is on PATH.")]
        [DefaultValue(true)]
        public bool DetectPerforce { get; set; } = true;

        [Category(DetectionCategory)]
        [DisplayName("Perforce timeout (seconds)")]
        [Description("Maximum time to wait for 'p4 where' (1-60 seconds).")]
        [DefaultValue(5)]
        public int PerforceTimeoutSeconds
        {
            get { return _perforceTimeoutSeconds; }
            set { _perforceTimeoutSeconds = Math.Max(1, Math.Min(60, value)); }
        }

        [Category(CustomCategory)]
        [DisplayName("Custom providers")]
        [Description("Additional rules as marker=providerGuid, separated by ';'. The marker is a file or folder name " +
            "looked up like the built-in ones and checked before them; when found, the source control provider with " +
            "that GUID is activated. Example: .vault=01234567-89ab-cdef-0123-456789abcdef")]
        [DefaultValue("")]
        public string CustomProviders { get; set; } = string.Empty;

        public DetectionSettings ToDetectionSettings(out System.Collections.Generic.IReadOnlyList<string> invalidCustomEntries)
        {
            return new DetectionSettings
            {
                SearchParentFolders = SearchParentFolders,
                DetectTeamFoundationFromSolution = DetectTeamFoundationFromSolution,
                CustomProviders = CustomProviderRule.ParseList(CustomProviders, out invalidCustomEntries),
            };
        }

        /// <summary>
        /// The settings that change which provider gets activated (not logging or notifications), as one string:
        /// used to skip re-evaluation when unrelated options are applied.
        /// </summary>
        public string GetSwitchingSignature()
        {
            return string.Join("|", AutoSwitch, GitProvider, SubversionProvider, TeamFoundationProvider, PerforceProvider,
                SearchParentFolders, DetectTeamFoundationFromSolution, DetectPerforce, PerforceTimeoutSeconds,
                (CustomProviders ?? string.Empty).Trim());
        }

        public ProviderPreferences ToProviderPreferences()
        {
            return new ProviderPreferences
            {
                Git = GitProvider,
                Subversion = SubversionProvider,
                TeamFoundation = TeamFoundationProvider,
                Perforce = PerforceProvider,
            };
        }

        public override void LoadSettingsFromStorage()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            // No early return when the collection does not exist yet: the defaults below are what a reload
            // (Cancel, Reset) must restore.
            var store = GetStore();

            AutoSwitch = store.GetBoolean(CollectionPath, nameof(AutoSwitch), true);
            ShowStatusBarNotifications = store.GetBoolean(CollectionPath, nameof(ShowStatusBarNotifications), true);
            LogLevel = ReadEnum(store, nameof(LogLevel), LogLevel.Normal);

            GitProvider = SettingValues.ParseGitProvider(store.GetString(CollectionPath, nameof(GitProvider), null));
            SubversionProvider = ReadEnum(store, nameof(SubversionProvider), SubversionProviderChoice.Default);
            TeamFoundationProvider = ReadEnum(store, nameof(TeamFoundationProvider), TeamFoundationProviderChoice.Default);
            PerforceProvider = ReadEnum(store, nameof(PerforceProvider), PerforceProviderChoice.Default);

            SearchParentFolders = store.GetBoolean(CollectionPath, nameof(SearchParentFolders), true);
            DetectTeamFoundationFromSolution = store.GetBoolean(CollectionPath, nameof(DetectTeamFoundationFromSolution), true);
            DetectPerforce = store.GetBoolean(CollectionPath, nameof(DetectPerforce), true);
            PerforceTimeoutSeconds = store.GetInt32(CollectionPath, nameof(PerforceTimeoutSeconds), 5);

            CustomProviders = store.GetString(CollectionPath, nameof(CustomProviders), string.Empty);
        }

        public override void SaveSettingsToStorage()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var store = GetStore();
            store.CreateCollection(CollectionPath);

            store.SetBoolean(CollectionPath, nameof(AutoSwitch), AutoSwitch);
            store.SetBoolean(CollectionPath, nameof(ShowStatusBarNotifications), ShowStatusBarNotifications);
            store.SetString(CollectionPath, nameof(LogLevel), LogLevel.ToString());

            store.SetString(CollectionPath, nameof(GitProvider), GitProvider.ToString());
            store.SetString(CollectionPath, nameof(SubversionProvider), SubversionProvider.ToString());
            store.SetString(CollectionPath, nameof(TeamFoundationProvider), TeamFoundationProvider.ToString());
            store.SetString(CollectionPath, nameof(PerforceProvider), PerforceProvider.ToString());

            store.SetBoolean(CollectionPath, nameof(SearchParentFolders), SearchParentFolders);
            store.SetBoolean(CollectionPath, nameof(DetectTeamFoundationFromSolution), DetectTeamFoundationFromSolution);
            store.SetBoolean(CollectionPath, nameof(DetectPerforce), DetectPerforce);
            store.SetInt32(CollectionPath, nameof(PerforceTimeoutSeconds), PerforceTimeoutSeconds);

            store.SetString(CollectionPath, nameof(CustomProviders), (CustomProviders ?? string.Empty).Trim());
        }

        protected override void OnApply(PageApplyEventArgs e)
        {
            base.OnApply(e);
            if (e.ApplyBehavior == ApplyKind.Apply)
                Saved?.Invoke(this, EventArgs.Empty);
        }

        private static WritableSettingsStore GetStore()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            return new ShellSettingsManager(ServiceProvider.GlobalProvider).GetWritableSettingsStore(SettingsScope.UserSettings);
        }

        private static T ReadEnum<T>(SettingsStore store, string property, T defaultValue) where T : struct
        {
            return SettingValues.ParseEnum(store.GetString(CollectionPath, property, null), defaultValue);
        }
    }
}
