using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace XelLauncher.Helpers;

internal static class EndfieldLauncher
{
    internal static bool Supports(string iconName) =>
        iconName is "Endfield" or "BiliEndfield" or "GlobalEndfield";

    internal static async Task<Process> StartAsync(string gamePath, string arguments, string iconName)
    {
        bool global = iconName == "GlobalEndfield";
        // Authenticode verification may access certificate revocation services.
        // Run discovery and verification off the UI thread.
        return await Task.Run(async () =>
        {
            var candidates = GetCandidates(global).ToArray();
            if (candidates.Length == 0)
                throw new InvalidOperationException("未找到此渠道已安装的官方启动器。请先安装并运行一次鹰角或 GRYPHLINK 官方启动器。");

            using var parent = await GetParentAsync(candidates, global);
            string parentPath = ParentProcessLauncher.GetImagePath(parent);
            // Hold the image against replacement between trust verification and
            // child creation, and recheck the exact process selected above.
            using var imageLock = File.Open(parentPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (!OfficialLauncherTrust.IsTrusted(parentPath, global))
                throw new InvalidOperationException("官方启动器签名验证失败，请修复官方启动器后重试。");
            var process = ParentProcessLauncher.Start(parent,
                Path.GetFullPath(Path.Combine(gamePath, "Endfield.exe")), arguments, Path.GetFullPath(gamePath));
            LogHelper.Log($"Endfield launch: channel={iconName}, process={process.Id}");
            return process;
        });
    }

    internal static string ParseRegisteredExecutable(string command)
    {
        if (string.IsNullOrWhiteSpace(command)) return null;
        command = Environment.ExpandEnvironmentVariables(command.Trim());
        // Official protocol registrations quote their absolute executable path.
        if (command[0] != '"') return null;
        int end = command.IndexOf('"', 1);
        if (end <= 1) return null;
        string path = command.Substring(1, end - 1);
        return Path.IsPathFullyQualified(path) &&
               Path.GetFileName(path).Equals("Launcher.exe", StringComparison.OrdinalIgnoreCase)
            ? path : null;
    }

    private static IEnumerable<string> GetCandidates(bool global)
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            using var classes = RegistryKey.OpenBaseKey(RegistryHive.ClassesRoot, view);
            // Both the consolidated and older international launcher are supported.
            var protocols = global
                ? new[] { "gryphlinklauncher-TiaytKBUIEdoEwRT", "gryphlinelauncher-YDUTE5gscDZ229CW" }
                : new[] { "hypergryphlauncher-abYeZZ16BPluCFyT" };
            foreach (var protocol in protocols)
            {
                using var key = classes.OpenSubKey(protocol + @"\shell\open\command");
                var executable = ParseRegisteredExecutable(key?.GetValue("") as string);
                if (executable != null && File.Exists(executable)) roots.Add(Path.GetDirectoryName(executable));
            }
        }
        foreach (string root in roots)
        {
            // Version directories are numerically ordered so 1.10 wins over 1.9.
            var versions = Directory.EnumerateDirectories(root)
                .Select(path => (Path: path, Version: Version.TryParse(Path.GetFileName(path), out var version) ? version : null))
                .Where(item => item.Version != null).OrderByDescending(item => item.Version);
            foreach (var version in versions)
            {
                string candidate = Path.Combine(version.Path, "Games.exe");
                if (File.Exists(candidate)) yield return candidate;
            }
            string unversioned = Path.Combine(root, "Games.exe");
            if (File.Exists(unversioned)) yield return unversioned;
        }
    }

    private static async Task<SafeProcessHandle> GetParentAsync(string[] candidates, bool global)
    {
        var trusted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string candidate in candidates)
            if (OfficialLauncherTrust.IsTrusted(candidate, global)) trusted.Add(Path.GetFullPath(candidate));
        if (trusted.Count == 0)
            throw new InvalidOperationException("官方 Games.exe 数字签名或发行者验证失败，请通过官方安装包修复启动器后重试。");

        var parent = FindParent(trusted, out var accessError);
        if (parent != null) return parent;
        if (accessError != null) throw accessError;

        string path = candidates.First(candidate => trusted.Contains(Path.GetFullPath(candidate)));
        // Keep the actual installed executable running normally. Do not suspend,
        // inject into, terminate, or modify the official launcher.
        using var started = Process.Start(new ProcessStartInfo(path)
        {
            WorkingDirectory = Path.GetDirectoryName(path),
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Minimized
        });
        for (int attempt = 0; attempt < 60; attempt++)
        {
            await Task.Delay(250).ConfigureAwait(false);
            parent = FindParent(trusted, out accessError);
            if (parent != null) return parent;
            if (accessError != null) throw accessError;
        }
        throw new TimeoutException("官方启动器启动超时，请手动打开对应官方启动器后重试。");
    }

    private static SafeProcessHandle FindParent(HashSet<string> trusted, out Win32Exception accessError)
    {
        accessError = null;
        using var current = Process.GetCurrentProcess();
        var processes = Process.GetProcessesByName("Games");
        try
        {
            foreach (var process in processes)
            {
                SafeProcessHandle handle = null;
                try
                {
                    if (process.SessionId != current.SessionId) continue;
                    using var query = ParentProcessLauncher.OpenQuery(process.Id);
                    if (!trusted.Contains(Path.GetFullPath(ParentProcessLauncher.GetImagePath(query)))) continue;
                    handle = ParentProcessLauncher.OpenParent(process.Id);
                    string path = ParentProcessLauncher.GetImagePath(handle);
                    if (!trusted.Contains(Path.GetFullPath(path))) continue;
                    var result = handle;
                    handle = null;
                    return result;
                }
                catch (Win32Exception ex) { if (ex.NativeErrorCode == 5) accessError = ex; }
                catch (InvalidOperationException) { /* Process exited during enumeration. */ }
                finally { handle?.Dispose(); }
            }
        }
        finally { foreach (var process in processes) process.Dispose(); }
        return null;
    }
}
