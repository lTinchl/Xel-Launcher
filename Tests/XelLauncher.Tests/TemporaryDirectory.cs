namespace XelLauncher.Tests;

internal sealed class TemporaryDirectory : IDisposable
{
    private static readonly string TestRoot = Path.Combine(
        Path.GetTempPath(), "XelLauncher.Tests");

    public TemporaryDirectory()
    {
        RootPath = Path.Combine(TestRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(RootPath);
    }

    public string RootPath { get; }

    public string CreateDirectory(string relativePath)
    {
        var path = GetPath(relativePath);
        Directory.CreateDirectory(path);
        return path;
    }

    public string WriteFile(string relativePath, string content)
    {
        var path = GetPath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    public string GetPath(string relativePath) =>
        Path.Combine(RootPath, relativePath);

    public void Dispose()
    {
        if (!Directory.Exists(RootPath)) return;

        var normalizedRoot = Path.GetFullPath(RootPath)
            .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var normalizedTestRoot = Path.GetFullPath(TestRoot)
            .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!normalizedRoot.StartsWith(
                normalizedTestRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Refusing to delete a directory outside the test root.");
        }

        Directory.Delete(RootPath, recursive: true);
    }
}
