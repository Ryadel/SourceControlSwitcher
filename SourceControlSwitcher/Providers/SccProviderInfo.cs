using System;
using System.Collections.Generic;
using SourceControlSwitcher.Detection;

namespace SourceControlSwitcher.Providers
{
    /// <summary>
    /// A Visual Studio source control (SCC) provider: the GUID passed to
    /// <c>IVsRegisterScciProvider.RegisterSourceControlProvider</c> and the package GUIDs whose installation
    /// means the provider is available.
    /// </summary>
    public sealed class SccProviderInfo
    {
        public SccProviderInfo(string name, RepositoryKind kind, Guid providerGuid, params Guid[] packageGuids)
        {
            Name = name;
            Kind = kind;
            ProviderGuid = providerGuid;
            PackageGuids = packageGuids;
        }

        public string Name { get; }

        public RepositoryKind Kind { get; }

        public Guid ProviderGuid { get; }

        public IReadOnlyList<Guid> PackageGuids { get; }

        public override string ToString() => Name;
    }
}
