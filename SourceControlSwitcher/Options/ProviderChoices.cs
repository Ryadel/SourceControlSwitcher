using System.ComponentModel;

namespace SourceControlSwitcher.Options
{
    // Member names are persisted in the settings store: rename only with a mapping in GeneralOptions.

    [TypeConverter(typeof(EnumDescriptionConverter))]
    public enum GitProviderChoice
    {
        [Description("Automatic (first installed)")]
        Default = 0,

        [Description("Git (Microsoft Git provider)")]
        MicrosoftGit,

        [Description("Disabled (do not switch)")]
        Disabled
    }

    [TypeConverter(typeof(EnumDescriptionConverter))]
    public enum SubversionProviderChoice
    {
        [Description("Automatic (first installed)")]
        Default = 0,

        [Description("VisualSVN")]
        VisualSvn,

        [Description("AnkhSVN")]
        AnkhSvn,

        [Description("Disabled (do not switch)")]
        Disabled
    }

    [TypeConverter(typeof(EnumDescriptionConverter))]
    public enum TeamFoundationProviderChoice
    {
        [Description("Automatic (first installed)")]
        Default = 0,

        [Description("Visual Studio Team Foundation Server")]
        TeamFoundationServer,

        [Description("Disabled (do not switch)")]
        Disabled
    }

    [TypeConverter(typeof(EnumDescriptionConverter))]
    public enum PerforceProviderChoice
    {
        [Description("Automatic (first installed)")]
        Default = 0,

        [Description("P4VS")]
        P4Vs,

        [Description("Disabled (do not switch)")]
        Disabled
    }

    [TypeConverter(typeof(EnumDescriptionConverter))]
    public enum LogLevel
    {
        [Description("Off")]
        Off = 0,

        [Description("Normal (switches and problems)")]
        Normal,

        [Description("Verbose (every detection step)")]
        Verbose
    }
}
