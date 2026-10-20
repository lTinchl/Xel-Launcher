using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace XelLauncher.Helpers
{
    public static class Account
    {
        private static readonly SemaphoreSlim Operations = new(1, 1);
        private static readonly Dictionary<string, Detection> Active = new();
        private static readonly string[] LoginFiles = { "login_cache", "gf_login_cache" };

        public static bool Supports(string channel) =>
            channel is "Arknights" or "Endfield" or "GlobalEndfield";

        private static string Family(string channel) => channel == "Arknights" ? "Arknights" : "Endfield";

        private static string SdkRoot(string channel) => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "AppData", "LocalLow", "Hypergryph", Family(channel));

        private static string BackupRoot(string channel) => channel switch
        {
            "Arknights" => ConfigHelper.AccountBackupDir,
            "Endfield" => ConfigHelper.EndAccountBackupDir,
            "GlobalEndfield" => ConfigHelper.GlobalEndAccountBackupDir,
            _ => throw new ArgumentOutOfRangeException(nameof(channel))
        };

        private static Exception Error(string key) =>
            new InvalidOperationException(Localizer.GetRequiredString("App.Account." + key));

        private static bool IsSdkName(string name) =>
            name != null && Regex.IsMatch(name, @"\Asdk_data_[0-9a-f]{32}\z");

        private static void EnsureClosed(string channel)
        {
            if (CountProcesses(channel) != 0) throw Error("CloseGame");
        }

        private static int CountProcesses(string channel)
        {
            var processes = Process.GetProcessesByName(Family(channel));
            try { return processes.Length; }
            finally { foreach (var process in processes) process.Dispose(); }
        }

        private static string AccountPath(string channel, string accountId)
        {
            if (string.IsNullOrWhiteSpace(accountId) || accountId is "." or ".." ||
                accountId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new ArgumentException("Invalid account ID.", nameof(accountId));
            return Path.Combine(BackupRoot(channel), accountId);
        }

        private static string ResolveDirectory(string channel)
        {
            if (!Supports(channel)) throw new ArgumentOutOfRangeException(nameof(channel));
            lock (Active)
                if (Active.TryGetValue(Family(channel), out var active) &&
                    (active.Channel != channel || active.DirectoryName == null))
                    throw Error("IdentifyFirst");
            var mappings = ConfigHelper.Load().AccountSdkDirectories;
            if (!mappings.TryGetValue(channel, out var name) || !IsSdkName(name))
                throw Error("IdentifyFirst");
            return name;
        }

        public static Detection BeginDetection(string channel)
        {
            if (!Supports(channel)) return null;
            EnsureClosed(channel);
            var detection = new Detection(channel, SdkRoot(channel));
            lock (Active)
            {
                if (Active.ContainsKey(Family(channel)))
                    throw Error("IdentifyFirst");
                Active.Add(Family(channel), detection);
            }
            return detection;
        }

        public static async Task BackupAsync(string channel, string accountId)
        {
            await Operations.WaitAsync();
            try
            {
                var name = ResolveDirectory(channel);
                var source = Path.Combine(SdkRoot(channel), name);
                await Task.Run(() => SaveSnapshot(source, AccountPath(channel, accountId), channel, name));
            }
            finally { Operations.Release(); }
        }

        public static async Task RestoreAsync(string channel, string accountId)
        {
            await Operations.WaitAsync();
            try
            {
                EnsureClosed(channel);
                var name = ResolveDirectory(channel);
                await Task.Run(() => RestoreSnapshot(
                    AccountPath(channel, accountId), SdkRoot(channel), channel, name));
            }
            finally { Operations.Release(); }
        }

        // Hashes also detect memory-mapped MMKV writes whose file timestamps may
        // not advance until the game releases its handles. Never log their bytes.
        internal static string Fingerprint(string path)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            return Convert.ToHexString(SHA256.HashData(stream));
        }

        private static bool HasLoginPair(string directory) => LoginFiles.Any(name =>
            File.Exists(Path.Combine(directory, name)) &&
            new FileInfo(Path.Combine(directory, name)).Length > 0 &&
            File.Exists(Path.Combine(directory, name + ".crc")));

        internal static Dictionary<string, string> CaptureLogins(string root)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!Directory.Exists(root)) return result;
            foreach (var directory in Directory.GetDirectories(root, "sdk_data_*"))
            {
                if (!IsSdkName(Path.GetFileName(directory))) continue;
                foreach (var file in LoginFiles)
                {
                    var path = Path.Combine(directory, file);
                    if (!File.Exists(path) || new FileInfo(path).Length == 0 ||
                        !File.Exists(path + ".crc")) continue;
                    // Include timestamps to recognize a successful re-login that
                    // writes the same cache content.
                    result[Path.Combine(Path.GetFileName(directory), file)] =
                        Fingerprint(path) + ":" + File.GetLastWriteTimeUtc(path).Ticks + "|" +
                        Fingerprint(path + ".crc") + ":" +
                        File.GetLastWriteTimeUtc(path + ".crc").Ticks;
                }
            }
            return result;
        }

        internal sealed class LoginObservation
        {
            private readonly Dictionary<string, string> baseline;
            private Dictionary<string, string> previous;
            private readonly HashSet<string> candidates = new(StringComparer.OrdinalIgnoreCase);
            private int stableSamples;
            internal bool Ambiguous => candidates.Count > 1;

            internal LoginObservation(Dictionary<string, string> baseline) => this.baseline = baseline;

            internal string Observe(Dictionary<string, string> current)
            {
                foreach (var pair in current)
                    // CRC metadata alone is not evidence of a successful login.
                    if (!baseline.TryGetValue(pair.Key, out var old) ||
                        old.Split('|')[0] != pair.Value.Split('|')[0])
                        candidates.Add(Path.GetDirectoryName(pair.Key));
                stableSamples = previous != null && Equal(previous, current) ? stableSamples + 1 : 0;
                previous = current;
                if (candidates.Count != 1 || stableSamples < 2) return null;
                var name = candidates.Single();
                return current.Keys.Any(key => Path.GetDirectoryName(key) == name) ? name : null;
            }
        }

        private static bool Equal(Dictionary<string, string> left, Dictionary<string, string> right) =>
            left.Count == right.Count && left.All(pair =>
                right.TryGetValue(pair.Key, out var value) && value == pair.Value);

        public sealed class Detection : IDisposable
        {
            private readonly string root;
            private readonly LoginObservation observation;
            private readonly CancellationTokenSource cancellation = new();
            private readonly TaskCompletionSource completed = new(TaskCreationOptions.RunContinuationsAsynchronously);
            private bool invalidated;
            public string Channel { get; }
            public string DirectoryName { get; private set; }
            public Task Completion => completed.Task;
            public bool NeedsIdentification { get; }

            internal Detection(string channel, string root)
            {
                Channel = channel;
                this.root = root;
                observation = new LoginObservation(CaptureLogins(root));
                NeedsIdentification = !ConfigHelper.Load().AccountSdkDirectories.TryGetValue(channel, out var known) ||
                    !IsSdkName(known) || !Directory.Exists(Path.Combine(root, known));
            }

            public void Start(Process game)
            {
                // The launch UI owns and disposes the original process object.
                var ownedProcess = Process.GetProcessById(game.Id);
                _ = ObserveAsync(ownedProcess);
            }

            private async Task ObserveAsync(Process game)
            {
                using (game)
                {
                    try
                    {
                        while (!game.HasExited && !cancellation.IsCancellationRequested)
                        {
                            await Task.Delay(1000, cancellation.Token).ConfigureAwait(false);
                            await Operations.WaitAsync(cancellation.Token).ConfigureAwait(false);
                            try
                            {
                                // File changes cannot be attributed safely with two
                                // clients of the same game running.
                                if (CountProcesses(Channel) > 1) invalidated = true;
                                var name = invalidated ? null : observation.Observe(CaptureLogins(root));
                                if (observation.Ambiguous) invalidated = true;
                                if (name != DirectoryName || invalidated)
                                {
                                    DirectoryName = name;
                                    ConfigHelper.Update(cfg =>
                                    {
                                        if (name == null) cfg.AccountSdkDirectories.Remove(Channel);
                                        else cfg.AccountSdkDirectories[Channel] = name;
                                    });
                                }
                                if (name != null) completed.TrySetResult();
                                if (invalidated) break;
                            }
                            finally { Operations.Release(); }
                        }
                    }
                    catch (OperationCanceledException) { }
                    catch (Exception ex)
                    {
                        DirectoryName = null;
                        try { ConfigHelper.Update(cfg => cfg.AccountSdkDirectories.Remove(Channel)); }
                        catch (Exception saveError) { LogHelper.LogError(saveError, "AccountSdkDetectionReset"); }
                        LogHelper.LogError(ex, "AccountSdkDetection");
                    }
                    finally { Dispose(); }
                }
            }

            public void Dispose()
            {
                cancellation.Cancel();
                lock (Active)
                    if (Active.TryGetValue(Family(Channel), out var active) && ReferenceEquals(active, this))
                        Active.Remove(Family(Channel));
                completed.TrySetResult();
            }
        }

        private sealed class Snapshot
        {
            public int Version { get; set; } = 1;
            public string Channel { get; set; }
            public string DirectoryName { get; set; }
            public Dictionary<string, string> Files { get; set; }
        }

        private static Dictionary<string, string> CaptureFiles(string directory)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            CaptureDirectory(directory);
            return result;

            void CaptureDirectory(string current)
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw Error("InvalidBackup");
                foreach (var path in Directory.GetFiles(current))
                {
                    if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                        throw Error("InvalidBackup");
                    result.Add(Path.GetRelativePath(directory, path), Fingerprint(path));
                }
                foreach (var child in Directory.GetDirectories(current))
                    CaptureDirectory(child);
            }
        }

        private static void CopySnapshotFiles(string source, string destination, IEnumerable<string> files)
        {
            foreach (var file in files)
            {
                var target = Path.Combine(destination, file);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(Path.Combine(source, file), target);
            }
        }

        internal static void SaveSnapshot(string source, string destination, string channel, string name)
        {
            if (!IsSdkName(name) || !Directory.Exists(source) || !HasLoginPair(source))
                throw Error("IdentifyFirst");
            var staging = destination + ".staging-" + Guid.NewGuid().ToString("N");
            try
            {
                Directory.CreateDirectory(staging);
                var data = Path.Combine(staging, "data");
                Directory.CreateDirectory(data);
                var before = CaptureFiles(source);
                CopySnapshotFiles(source, data, before.Keys);
                var copied = CaptureFiles(data);
                if (!Equal(before, copied) || !Equal(before, CaptureFiles(source)))
                    throw Error("CacheChanging");
                File.WriteAllText(Path.Combine(staging, "snapshot.json"), JsonSerializer.Serialize(
                    new Snapshot { Channel = channel, DirectoryName = name, Files = copied }));
                ReplaceDirectory(staging, destination);
            }
            finally { if (Directory.Exists(staging)) Directory.Delete(staging, true); }
        }

        internal static void RestoreSnapshot(string source, string sdkRoot, string channel, string name)
        {
            var metadata = Path.Combine(source, "snapshot.json");
            if (!File.Exists(metadata)) throw Error("ResaveLegacy");
            var snapshot = JsonSerializer.Deserialize<Snapshot>(File.ReadAllText(metadata));
            var data = Path.Combine(source, "data");
            if (snapshot?.Version != 1 || snapshot.Channel != channel ||
                snapshot.DirectoryName != name || !IsSdkName(name) ||
                snapshot.Files == null || !Directory.Exists(data) ||
                !HasLoginPair(data) || !Equal(snapshot.Files, CaptureFiles(data)))
                throw Error("InvalidBackup");
            Directory.CreateDirectory(sdkRoot);
            var destination = Path.Combine(sdkRoot, name);
            var staging = destination + ".staging-" + Guid.NewGuid().ToString("N");
            try
            {
                Directory.CreateDirectory(staging);
                CopySnapshotFiles(data, staging, snapshot.Files.Keys);
                if (!Equal(snapshot.Files, CaptureFiles(staging))) throw Error("InvalidBackup");
                ReplaceDirectory(staging, destination);
            }
            finally { if (Directory.Exists(staging)) Directory.Delete(staging, true); }
        }

        private static void ReplaceDirectory(string staging, string destination)
        {
            var rollback = destination + ".previous-" + Guid.NewGuid().ToString("N");
            bool existed = Directory.Exists(destination);
            if (existed) Directory.Move(destination, rollback);
            try { Directory.Move(staging, destination); }
            catch
            {
                if (existed) Directory.Move(rollback, destination);
                throw;
            }
            // A failed cleanup must not turn a successful save into a reported failure.
            if (existed)
            {
                try { Directory.Delete(rollback, true); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }
}
