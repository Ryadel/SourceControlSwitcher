namespace SourceControlSwitcher.Detection
{
    /// <summary>
    /// The kind of repository a solution or folder belongs to.
    /// </summary>
    public enum RepositoryKind
    {
        None = 0,
        Git,
        Subversion,
        TeamFoundation,
        Perforce,

        /// <summary>A user-defined marker matched (see <see cref="CustomProviderRule"/>).</summary>
        Custom
    }
}
