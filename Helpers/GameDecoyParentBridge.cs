using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace XelLauncher.Helpers;

// Adapted to Xel's process launcher from the Endfield@v1.2.0 design:
// https://github.com/misaka10843/Hi3Helper.Plugin.Hypergryph/blob/45eb8dc/Hi3Helper.Hypergryph.Core/Management/GameDecoyParentBridge.cs
internal sealed class GameDecoyParentBridge : IDisposable
{
    private static readonly string[] CandidateNames =
        { "PlatformProcess.exe", "UnityCrashHandler64.exe", "Endfield.exe" };
    private readonly string _directory;
    private readonly FileStream _imageLock;
    private SafeProcessHandle _parent;

    internal string StagedPath => Path.Combine(_directory, "Games.exe");

    private GameDecoyParentBridge(string directory, FileStream imageLock, SafeProcessHandle parent)
    {
        _directory = directory;
        _imageLock = imageLock;
        _parent = parent;
    }

    internal static GameDecoyParentBridge TryCreate(string gameDirectory) =>
        TryCreate(gameDirectory, OfficialLauncherTrust.IsTrustedGameFile);

    // The verifier seam allows native process/cleanup regression tests without
    // requiring an installed game or replacing the production trust policy.
    internal static GameDecoyParentBridge TryCreate(string gameDirectory, Func<string, bool> verify)
    {
        foreach (string source in EnumerateCandidates(gameDirectory))
        {
            string directory = null;
            FileStream imageLock = null;
            SafeProcessHandle parent = null;
            try
            {
                if (File.GetAttributes(source).HasFlag(FileAttributes.ReparsePoint)) continue;
                using var sourceLock = File.Open(source, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (!verify(source))
                {
                    LogHelper.Log($"Endfield parent: untrusted candidate skipped: {source}");
                    continue;
                }

                directory = Directory.CreateTempSubdirectory("Xel-Endfield-").FullName;
                string staged = Path.Combine(directory, "Games.exe");
                using (var output = new FileStream(staged, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    sourceLock.CopyTo(output);
                imageLock = File.Open(staged, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (!verify(staged)) throw new InvalidDataException("Staged parent signature verification failed.");
                parent = ParentProcessLauncher.StartSuspended(staged);
                LogHelper.Log($"Endfield parent staged: {source} -> {staged}");
                var bridge = new GameDecoyParentBridge(directory, imageLock, parent);
                directory = null;
                imageLock = null;
                parent = null;
                return bridge;
            }
            catch (Exception ex)
            {
                LogHelper.LogError(ex, $"Endfield parent candidate failed: {source}");
            }
            finally
            {
                if (parent != null)
                {
                    ParentProcessLauncher.Terminate(parent);
                    parent.Dispose();
                }
                imageLock?.Dispose();
                if (directory != null) CleanDirectory(directory);
            }
        }
        LogHelper.Log($"Endfield: no usable signed parent in {gameDirectory}; ordinary launch will be used.");
        return null;
    }

    internal static IEnumerable<string> EnumerateCandidates(string gameDirectory)
    {
        if (string.IsNullOrWhiteSpace(gameDirectory) || !Directory.Exists(gameDirectory)) yield break;
        foreach (string name in CandidateNames)
        {
            var directories = new Queue<(string Path, int Depth)>();
            directories.Enqueue((gameDirectory, 0));
            while (directories.TryDequeue(out var current))
            {
                string file = Path.Combine(current.Path, name);
                if (File.Exists(file)) yield return file;
                if (current.Depth >= 3) continue;
                foreach (string child in GetChildDirectories(current.Path))
                    directories.Enqueue((child, current.Depth + 1));
            }
        }
    }

    private static string[] GetChildDirectories(string directory)
    {
        try
        {
            var children = new List<string>();
            foreach (string child in Directory.GetDirectories(directory))
                if (!File.GetAttributes(child).HasFlag(FileAttributes.ReparsePoint)) children.Add(child);
            children.Sort(StringComparer.OrdinalIgnoreCase);
            return children.ToArray();
        }
        catch (Exception ex)
        {
            LogHelper.LogError(ex, $"Endfield parent directory search: {directory}");
            return Array.Empty<string>();
        }
    }

    internal Process StartGame(string executable, string arguments, string directory)
    {
        ObjectDisposedException.ThrowIf(_parent == null, this);
        return ParentProcessLauncher.Start(_parent, executable, arguments, directory);
    }

    public void Dispose()
    {
        var parent = Interlocked.Exchange(ref _parent, null);
        if (parent == null) return;
        try { ParentProcessLauncher.Terminate(parent); }
        finally
        {
            parent.Dispose();
            _imageLock.Dispose();
            CleanDirectory(_directory);
        }
    }

    private static void CleanDirectory(string directory)
    {
        // Only our private image and empty per-launch directory are removed.
        for (int attempt = 0; attempt < 4; attempt++)
        {
            try
            {
                File.Delete(Path.Combine(directory, "Games.exe"));
                Directory.Delete(directory);
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (attempt == 3) LogHelper.LogError(ex, $"Endfield parent cleanup: {directory}");
                else Thread.Sleep(150);
            }
        }
    }
}
