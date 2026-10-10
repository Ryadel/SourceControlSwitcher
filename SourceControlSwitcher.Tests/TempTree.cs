using System;
using System.IO;

namespace SourceControlSwitcher.Tests
{
    /// <summary>A throwaway folder tree under the system temp folder, deleted on dispose.</summary>
    internal sealed class TempTree : IDisposable
    {
        public TempTree()
        {
            Root = Path.Combine(Path.GetTempPath(), "SourceControlSwitcher.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public string Dir(string relativePath)
        {
            string path = Path.Combine(Root, relativePath);
            Directory.CreateDirectory(path);
            return path;
        }

        public string File(string relativePath, string content = "")
        {
            string path = Path.Combine(Root, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            System.IO.File.WriteAllText(path, content);
            return path;
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
