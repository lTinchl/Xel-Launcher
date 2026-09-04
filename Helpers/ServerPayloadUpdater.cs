using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Hi3Helper.Hypergryph.Core.Utils;

namespace XelLauncher.Helpers
{
    public enum ServerPayloadUpdateStage
    {
        Checking,
        Comparing,
        Downloading,
        Verifying,
        Applying,
        Completed
    }

    public sealed class ServerPayloadProfile
    {
        internal ServerPayloadProfile(
            string iconName,
            string payloadDirectoryName,
            GameFamily gameFamily,
            string apiUrl,
            string appCode,
            string launcherAppCode,
            string channel,
            string subChannel,
            string sequence,
            IReadOnlyList<string> rootFiles,
            IReadOnlyList<string> directoryPrefixes,
            IReadOnlyList<string> requiredFiles,
            int maxFileCount,
            long maxTotalBytes,
            long maxSingleFileBytes)
        {
            IconName = iconName;
            PayloadDirectoryName = payloadDirectoryName;
            GameFamily = gameFamily;
            ApiUrl = apiUrl;
            AppCode = appCode;
            LauncherAppCode = launcherAppCode;
            Channel = channel;
            SubChannel = subChannel;
            Sequence = sequence;
            RootFiles = rootFiles;
            DirectoryPrefixes = directoryPrefixes;
            RequiredFiles = requiredFiles;
            MaxFileCount = maxFileCount;
            MaxTotalBytes = maxTotalBytes;
            MaxSingleFileBytes = maxSingleFileBytes;
        }

        public string IconName { get; }
        public string PayloadDirectoryName { get; }
        public GameFamily GameFamily { get; }
        internal string ApiUrl { get; }
        internal string AppCode { get; }
        internal string LauncherAppCode { get; }
        internal string Channel { get; }
        internal string SubChannel { get; }
        internal string Sequence { get; }
        public IReadOnlyList<string> RootFiles { get; }
        public IReadOnlyList<string> DirectoryPrefixes { get; }
        public IReadOnlyList<string> RequiredFiles { get; }
        internal int MaxFileCount { get; }
        internal long MaxTotalBytes { get; }
        internal long MaxSingleFileBytes { get; }
    }

    public sealed class ServerPayloadState
    {
        public string IconName { get; set; } = "";
        public string Version { get; set; } = "";
        public string ManifestSha256 { get; set; } = "";
        public DateTimeOffset UpdatedAtUtc { get; set; }
        public int FileCount { get; set; }
        public long TotalBytes { get; set; }
    }

    public sealed class ServerPayloadUpdateProgress
    {
        public ServerPayloadProfile Profile { get; init; }
        public ServerPayloadUpdateStage Stage { get; init; }
        public string Version { get; init; } = "";
        public string CurrentFile { get; init; } = "";
        public int FileIndex { get; init; }
        public int FileCount { get; init; }
        public long DownloadedBytes { get; init; }
        public long TotalBytes { get; init; }
    }

    public sealed class ServerPayloadUpdateResult
    {
        public ServerPayloadProfile Profile { get; init; }
        public string Version { get; init; } = "";
        public int FileCount { get; init; }
        public long DownloadedBytes { get; init; }
        public bool AlreadyCurrent { get; init; }
    }

    public sealed class ServerGameManifest
    {
        public ServerPayloadProfile Profile { get; init; }
        public string Version { get; init; } = "";
        public Uri ResourceBaseUri { get; init; }
        public string ManifestSha256 { get; init; } = "";
        public byte[] EncryptedManifest { get; init; } = Array.Empty<byte>();
        public IReadOnlyList<ServerGameManifestFile> Files { get; init; } =
            Array.Empty<ServerGameManifestFile>();
    }

    public sealed class ServerGameManifestFile
    {
        public string RelativePath { get; init; } = "";
        public string UrlPath { get; init; } = "";
        public string Md5 { get; init; } = "";
        public long Size { get; init; }
    }

    /// <summary>
    /// Updates the small, channel-specific payload used by server switching.
    /// The remote game manifest is only used as an authenticated file index;
    /// the existing payload layout defines the safe update scope.
    /// </summary>
    public static class ServerPayloadUpdater
    {
        private const long MiB = 1024L * 1024L;

        private static readonly ServerPayloadProfile[] ProfileList =
            GameChannelCatalog.ServerPayloadProfiles.ToArray();

        private static readonly ConcurrentDictionary<string, SemaphoreSlim> UpdateLocks =
            new(StringComparer.OrdinalIgnoreCase);

        private static readonly ConcurrentDictionary<string, SemaphoreSlim> PayloadAccessLocks =
            new(StringComparer.OrdinalIgnoreCase);

        private static readonly ConcurrentDictionary<string, CurrentVersionSnapshot> CurrentVersionStates =
            new(StringComparer.OrdinalIgnoreCase);

        private static readonly JsonSerializerOptions StateJsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        private static readonly HttpClient ApiClient = CreateHttpClient(TimeSpan.FromSeconds(30));
        private static readonly HttpClient DownloadClient =
            CreateHttpClient(TimeSpan.FromMinutes(30));

        public static IReadOnlyList<ServerPayloadProfile> Profiles => ProfileList;

        public static event Action<string, bool> CurrentVersionStateChanged;

        public static string PayloadRoot =>
            Path.Combine(AppContext.BaseDirectory, "load");

        private static string StateRoot =>
            Path.Combine(PayloadRoot, ".state");

        public static ServerPayloadProfile GetProfile(string iconName)
        {
            return GameChannelCatalog.Get(iconName)?.PayloadProfile;
        }

        public static async Task<ServerGameManifest> GetGameManifestAsync(
            string iconName,
            CancellationToken cancellationToken = default)
        {
            var profile = GetProfile(iconName) ??
                          throw new ArgumentException(
                              $"Unknown server payload profile: {iconName}", nameof(iconName));
            var manifest = await GetRemoteManifestAsync(profile, cancellationToken)
                .ConfigureAwait(false);

            return new ServerGameManifest
            {
                Profile = profile,
                Version = manifest.Version,
                ResourceBaseUri = manifest.ResourceBaseUri,
                ManifestSha256 = manifest.ManifestSha256,
                EncryptedManifest = manifest.EncryptedManifest.ToArray(),
                Files = manifest.Files.Select(x => new ServerGameManifestFile
                {
                    RelativePath = x.RelativePath,
                    UrlPath = x.UrlPath,
                    Md5 = x.Md5,
                    Size = x.Size
                }).ToArray()
            };
        }

        public static async Task DownloadGameManifestFileAsync(
            ServerGameManifest manifest,
            ServerGameManifestFile file,
            string destinationPath,
            IProgress<long> progress = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(manifest);
            ArgumentNullException.ThrowIfNull(file);
            ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

            var parent = Path.GetDirectoryName(destinationPath) ??
                         throw new ArgumentException(
                             "The destination path has no parent directory.",
                             nameof(destinationPath));
            Directory.CreateDirectory(parent);

            var tempPath = destinationPath + ".download-" + Guid.NewGuid().ToString("N");
            try
            {
                await DownloadFileAsync(
                        BuildFileUri(manifest.ResourceBaseUri, file.UrlPath),
                        tempPath,
                        file.Size,
                        delta => progress?.Report(delta),
                        cancellationToken)
                    .ConfigureAwait(false);

                if (!await HasExpectedMd5Async(tempPath, file.Md5, cancellationToken)
                        .ConfigureAwait(false))
                {
                    throw new InvalidDataException(
                        $"Downloaded file hash mismatch: {file.RelativePath}");
                }

                File.Move(tempPath, destinationPath, overwrite: false);
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
        }

        public static string GetPayloadDirectory(string iconName)
        {
            var profile = GetProfile(iconName);
            return profile == null
                ? null
                : GetPayloadDirectory(profile);
        }

        public static async Task<TResult> UsePayloadDirectoryAsync<TResult>(
            string iconName,
            Func<string, Task<TResult>> action,
            CancellationToken cancellationToken = default)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            var profile = GetProfile(iconName);
            if (profile == null)
                return await action(null).ConfigureAwait(false);

            var gate = PayloadAccessLocks.GetOrAdd(
                profile.IconName, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await action(GetPayloadDirectory(iconName))
                    .ConfigureAwait(false);
            }
            finally
            {
                gate.Release();
            }
        }

        public static ServerPayloadState GetState(string iconName)
        {
            var profile = GetProfile(iconName);
            if (profile == null) return null;

            var path = GetStatePath(profile);
            if (!File.Exists(path)) return null;

            try
            {
                return JsonSerializer.Deserialize<ServerPayloadState>(
                    File.ReadAllText(path), StateJsonOptions);
            }
            catch (Exception ex)
            {
                LogHelper.LogError(ex, $"ServerPayloadState.Load({iconName})");
                return null;
            }
        }

        public static bool IsStateCurrentVersion(
            string iconName,
            ServerPayloadState state)
        {
            if (state == null ||
                !CurrentVersionStates.TryGetValue(iconName, out var current))
            {
                return false;
            }

            return string.Equals(
                       state.Version,
                       current.Version,
                       StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(
                       state.ManifestSha256,
                       current.ManifestSha256,
                       StringComparison.OrdinalIgnoreCase) &&
                   state.FileCount == current.FileCount &&
                   state.TotalBytes == current.TotalBytes;
        }

        public static bool IsDeploymentExcluded(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath)) return true;

            var normalized = relativePath.Replace('\\', '/').TrimStart('/');
            var fileName = Path.GetFileName(normalized);
            return normalized.Equals("config.ini", StringComparison.OrdinalIgnoreCase) ||
                   normalized.StartsWith("Arknights_Data/", StringComparison.OrdinalIgnoreCase) ||
                   normalized.StartsWith("Endfield_Data/", StringComparison.OrdinalIgnoreCase) ||
                   fileName.Equals("Arknights.exe", StringComparison.OrdinalIgnoreCase) ||
                   fileName.Equals("Endfield.exe", StringComparison.OrdinalIgnoreCase) ||
                   fileName.Equals("GameAssembly.dll", StringComparison.OrdinalIgnoreCase) ||
                   fileName.Equals("baselib.dll", StringComparison.OrdinalIgnoreCase) ||
                   fileName.StartsWith("UnityPlayer", StringComparison.OrdinalIgnoreCase) ||
                   fileName.StartsWith("game_files", StringComparison.OrdinalIgnoreCase) ||
                   fileName.Equals("payload-state.json", StringComparison.OrdinalIgnoreCase);
        }

        public static async Task<ServerPayloadUpdateResult> UpdateAsync(
            ServerPayloadProfile profile,
            bool force,
            IProgress<ServerPayloadUpdateProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));

            var gate = UpdateLocks.GetOrAdd(profile.IconName, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                SetCurrentVersionState(profile.IconName, null);

                var result = await UpdateCoreAsync(
                        profile, force, progress, cancellationToken)
                    .ConfigureAwait(false);

                RememberCurrentVersionState(profile, result);
                return result;
            }
            finally
            {
                gate.Release();
            }
        }

        public static async Task<ServerPayloadUpdateResult> UpdateIfOutdatedAsync(
            ServerPayloadProfile profile,
            IProgress<ServerPayloadUpdateProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));

            var gate = UpdateLocks.GetOrAdd(profile.IconName, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                SetCurrentVersionState(profile.IconName, null);

                var result = await UpdateCoreAsync(
                        profile,
                        force: false,
                        progress,
                        cancellationToken)
                    .ConfigureAwait(false);
                RememberCurrentVersionState(profile, result);
                return result;
            }
            finally
            {
                gate.Release();
            }
        }

        private static void RememberCurrentVersionState(
            ServerPayloadProfile profile,
            ServerPayloadUpdateResult result)
        {
            var state = GetState(profile.IconName);
            if (state == null ||
                !string.Equals(
                    state.Version,
                    result.Version,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            SetCurrentVersionState(
                profile.IconName,
                new CurrentVersionSnapshot(
                    state.Version,
                    state.ManifestSha256,
                    state.FileCount,
                    state.TotalBytes));
        }

        private static void SetCurrentVersionState(
            string iconName,
            CurrentVersionSnapshot state)
        {
            if (state == null)
                CurrentVersionStates.TryRemove(iconName, out _);
            else
                CurrentVersionStates[iconName] = state;

            var handlers = CurrentVersionStateChanged;
            if (handlers == null) return;

            foreach (Action<string, bool> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(iconName, state != null);
                }
                catch (Exception ex)
                {
                    LogHelper.LogError(
                        ex,
                        $"Server payload version state notification failed: {iconName}");
                }
            }
        }

        private static async Task<ServerPayloadUpdateResult> UpdateCoreAsync(
            ServerPayloadProfile profile,
            bool force,
            IProgress<ServerPayloadUpdateProgress> progress,
            CancellationToken cancellationToken,
            RemotePayloadPackage latestPackage = null)
        {
            Report(progress, profile, ServerPayloadUpdateStage.Checking);
            var remote = await GetRemoteManifestAsync(
                    profile, cancellationToken, latestPackage)
                .ConfigureAwait(false);

            var rules = BuildSeedRules(profile);
            var selectedFiles = remote.Files
                .Where(x => IsAllowedPayloadFile(x.RelativePath, rules))
                .OrderBy(x => x.RelativePath, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (selectedFiles.Count == 0)
                throw new InvalidDataException(
                    $"No channel payload files were found in the {profile.IconName} manifest.");

            ValidateSelectedPayload(profile, selectedFiles);

            var payloadDirectory = GetPayloadDirectory(profile);
            var previousState = GetState(profile.IconName);
            if (!force &&
                previousState != null &&
                previousState.FileCount == selectedFiles.Count &&
                previousState.TotalBytes == selectedFiles.Sum(x => x.Size) &&
                Directory.Exists(payloadDirectory) &&
                string.Equals(previousState.IconName, profile.IconName,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(previousState.Version, remote.Version,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(previousState.ManifestSha256, remote.ManifestSha256,
                    StringComparison.OrdinalIgnoreCase) &&
                await IsPayloadDirectoryCurrentAsync(
                        profile, payloadDirectory, selectedFiles, progress,
                        remote.Version, cancellationToken)
                    .ConfigureAwait(false))
            {
                Report(progress, profile, ServerPayloadUpdateStage.Completed,
                    remote.Version, fileCount: previousState.FileCount);
                return new ServerPayloadUpdateResult
                {
                    Profile = profile,
                    Version = remote.Version,
                    FileCount = previousState.FileCount,
                    AlreadyCurrent = true
                };
            }

            var candidates = GetSourceDirectories(profile).ToArray();
            var plan = new List<PayloadFilePlan>(selectedFiles.Count);

            for (var i = 0; i < selectedFiles.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var node = selectedFiles[i];
                Report(progress, profile, ServerPayloadUpdateStage.Comparing,
                    remote.Version, node.RelativePath, i + 1, selectedFiles.Count);

                string matchingSource = null;
                foreach (var sourceDirectory in candidates)
                {
                    var sourcePath = SafeCombine(sourceDirectory, node.RelativePath);
                    if (!File.Exists(sourcePath)) continue;

                    var info = new FileInfo(sourcePath);
                    if (info.Attributes.HasFlag(FileAttributes.ReparsePoint)) continue;
                    if (info.Length != node.Size) continue;
                    if (await HasExpectedMd5Async(sourcePath, node.Md5, cancellationToken)
                            .ConfigureAwait(false))
                    {
                        matchingSource = sourcePath;
                        break;
                    }
                }

                plan.Add(new PayloadFilePlan(node, matchingSource));
            }

            var totalDownloadBytes = plan.Where(x => x.SourcePath == null).Sum(x => x.Node.Size);
            var downloadedBytes = 0L;
            var lastDownloadReportAt = Environment.TickCount64;
            var lastDownloadReportBytes = 0L;
            var stagingDirectory = Path.Combine(
                PayloadRoot, ".staging", profile.PayloadDirectoryName + "-" + Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(stagingDirectory);

            try
            {
                for (var i = 0; i < plan.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var item = plan[i];
                    var destination = SafeCombine(stagingDirectory, item.Node.RelativePath);
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

                    if (item.SourcePath != null)
                    {
                        File.Copy(item.SourcePath, destination, true);
                        continue;
                    }

                    Report(progress, profile, ServerPayloadUpdateStage.Downloading,
                        remote.Version, item.Node.RelativePath, i + 1, plan.Count,
                        downloadedBytes, totalDownloadBytes);

                    var fileStart = downloadedBytes;
                    await DownloadFileAsync(
                            BuildFileUri(remote.ResourceBaseUri, item.Node.UrlPath),
                            destination + ".download",
                            item.Node.Size,
                            delta =>
                            {
                                downloadedBytes += delta;
                                var now = Environment.TickCount64;
                                if (now - lastDownloadReportAt >= 100 ||
                                    Math.Abs(downloadedBytes - lastDownloadReportBytes) >= MiB ||
                                    downloadedBytes >= totalDownloadBytes)
                                {
                                    Report(progress, profile,
                                        ServerPayloadUpdateStage.Downloading,
                                        remote.Version, item.Node.RelativePath,
                                        i + 1, plan.Count,
                                        downloadedBytes, totalDownloadBytes);
                                    lastDownloadReportAt = now;
                                    lastDownloadReportBytes = downloadedBytes;
                                }
                            },
                            cancellationToken)
                        .ConfigureAwait(false);

                    var tempPath = destination + ".download";
                    if (!await HasExpectedMd5Async(tempPath, item.Node.Md5, cancellationToken)
                            .ConfigureAwait(false))
                    {
                        throw new InvalidDataException(
                            $"MD5 mismatch after downloading {item.Node.RelativePath}.");
                    }

                    if (downloadedBytes < fileStart + item.Node.Size)
                        downloadedBytes = fileStart + item.Node.Size;

                    File.Move(tempPath, destination, true);
                }

                for (var i = 0; i < selectedFiles.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var node = selectedFiles[i];
                    Report(progress, profile, ServerPayloadUpdateStage.Verifying,
                        remote.Version, node.RelativePath, i + 1, selectedFiles.Count,
                        downloadedBytes, totalDownloadBytes);

                    var path = SafeCombine(stagingDirectory, node.RelativePath);
                    var info = new FileInfo(path);
                    if (!info.Exists || info.Length != node.Size ||
                        !await HasExpectedMd5Async(path, node.Md5, cancellationToken)
                            .ConfigureAwait(false))
                    {
                        throw new InvalidDataException(
                            $"Payload verification failed for {node.RelativePath}.");
                    }
                }

                Report(progress, profile, ServerPayloadUpdateStage.Applying,
                    remote.Version, fileCount: selectedFiles.Count,
                    downloadedBytes: downloadedBytes, totalBytes: totalDownloadBytes);

                var state = new ServerPayloadState
                {
                    IconName = profile.IconName,
                    Version = remote.Version,
                    ManifestSha256 = remote.ManifestSha256,
                    UpdatedAtUtc = DateTimeOffset.UtcNow,
                    FileCount = selectedFiles.Count,
                    TotalBytes = selectedFiles.Sum(x => x.Size)
                };

                var accessGate = PayloadAccessLocks.GetOrAdd(
                    profile.IconName, _ => new SemaphoreSlim(1, 1));
                await accessGate.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    ApplyStagedPayload(profile, stagingDirectory, state);
                }
                finally
                {
                    accessGate.Release();
                }
                stagingDirectory = null;

                Report(progress, profile, ServerPayloadUpdateStage.Completed,
                    remote.Version, fileCount: selectedFiles.Count,
                    downloadedBytes: downloadedBytes, totalBytes: totalDownloadBytes);

                return new ServerPayloadUpdateResult
                {
                    Profile = profile,
                    Version = remote.Version,
                    FileCount = selectedFiles.Count,
                    DownloadedBytes = downloadedBytes,
                    AlreadyCurrent = false
                };
            }
            finally
            {
                if (!string.IsNullOrEmpty(stagingDirectory) && Directory.Exists(stagingDirectory))
                    DeleteControlledDirectory(stagingDirectory);
            }
        }

        private static async Task<RemotePayloadManifest> GetRemoteManifestAsync(
            ServerPayloadProfile profile,
            CancellationToken cancellationToken,
            RemotePayloadPackage latestPackage = null)
        {
            var latest = latestPackage ??
                await GetLatestPackageAsync(profile, cancellationToken)
                    .ConfigureAwait(false);
            var manifestUri = new Uri(
                latest.ResourceBaseUri.AbsoluteUri.TrimEnd('/') + "/game_files",
                UriKind.Absolute);
            var encryptedManifest = await ApiClient.GetByteArrayAsync(
                    manifestUri, cancellationToken)
                .ConfigureAwait(false);
            var decryptedManifest = HgCrypto.DecryptBytesToString(encryptedManifest);
            if (string.IsNullOrWhiteSpace(decryptedManifest))
                throw new InvalidDataException(
                    $"Unable to decrypt the {profile.IconName} game_files manifest.");

            var nodes = ParseManifest(decryptedManifest);
            if (nodes.Count == 0)
                throw new InvalidDataException(
                    $"The {profile.IconName} game_files manifest is empty.");

            return new RemotePayloadManifest(
                latest.Version,
                latest.ResourceBaseUri,
                Convert.ToHexString(SHA256.HashData(encryptedManifest)),
                encryptedManifest,
                nodes);
        }

        private static async Task<RemotePayloadPackage> GetLatestPackageAsync(
            ServerPayloadProfile profile,
            CancellationToken cancellationToken)
        {
            var requestBody = new
            {
                seq = profile.Sequence,
                proxy_reqs = new object[]
                {
                    new
                    {
                        kind = "get_latest_game",
                        get_latest_game_req = new
                        {
                            appcode = profile.AppCode,
                            launcher_appcode = profile.LauncherAppCode,
                            channel = profile.Channel,
                            sub_channel = profile.SubChannel,
                            version = ""
                        }
                    }
                }
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, profile.ApiUrl)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json")
            };
            using var response = await ApiClient.SendAsync(
                    request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using var responseStream =
                await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(
                    responseStream, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (!TryReadLatestPackage(document.RootElement, out var version, out var resourceBaseUrl))
                throw new InvalidDataException(
                    $"The {profile.IconName} launcher API did not return a full package.");

            if (!Uri.TryCreate(resourceBaseUrl, UriKind.Absolute, out var resourceBaseUri) ||
                resourceBaseUri.Scheme != Uri.UriSchemeHttps)
            {
                throw new InvalidDataException(
                    $"The {profile.IconName} launcher API returned an invalid resource URL.");
            }

            return new RemotePayloadPackage(version, resourceBaseUri);
        }

        private static bool TryReadLatestPackage(
            JsonElement root,
            out string version,
            out string resourceBaseUrl)
        {
            version = null;
            resourceBaseUrl = null;

            if (!root.TryGetProperty("proxy_rsps", out var proxyResponses) ||
                proxyResponses.ValueKind != JsonValueKind.Array)
                return false;

            foreach (var proxyResponse in proxyResponses.EnumerateArray())
            {
                if (!proxyResponse.TryGetProperty("kind", out var kind) ||
                    !string.Equals(kind.GetString(), "get_latest_game",
                        StringComparison.OrdinalIgnoreCase) ||
                    !proxyResponse.TryGetProperty("get_latest_game_rsp", out var latest) ||
                    latest.ValueKind != JsonValueKind.Object)
                    continue;

                if (latest.TryGetProperty("version", out var versionElement))
                    version = versionElement.GetString();

                if (latest.TryGetProperty("pkg", out var package) &&
                    package.ValueKind == JsonValueKind.Object &&
                    package.TryGetProperty("file_path", out var filePath))
                {
                    resourceBaseUrl = filePath.GetString();
                }

                return !string.IsNullOrWhiteSpace(version) &&
                       !string.IsNullOrWhiteSpace(resourceBaseUrl);
            }

            return false;
        }

        private static List<RemotePayloadFile> ParseManifest(string decryptedManifest)
        {
            var result = new Dictionary<string, RemotePayloadFile>(StringComparer.OrdinalIgnoreCase);
            using var reader = new StringReader(decryptedManifest);
            string line;
            var lineNumber = 0;

            while ((line = reader.ReadLine()) != null)
            {
                lineNumber++;
                if (string.IsNullOrWhiteSpace(line)) continue;

                PayloadManifestNode node;
                try
                {
                    node = JsonSerializer.Deserialize<PayloadManifestNode>(line);
                }
                catch (JsonException ex)
                {
                    throw new InvalidDataException(
                        $"Invalid game_files entry at line {lineNumber}.", ex);
                }

                if (node == null || string.IsNullOrWhiteSpace(node.Path))
                    throw new InvalidDataException(
                        $"Missing game_files path at line {lineNumber}.");
                if (node.Size < 0)
                    throw new InvalidDataException(
                        $"Invalid size for {node.Path}.");
                if (!IsValidMd5(node.Md5))
                    throw new InvalidDataException(
                        $"Invalid MD5 for {node.Path}.");

                var relativePath = NormalizeRelativePath(node.Path);
                var normalizedUrlPath = relativePath.Replace('\\', '/');
                var item = new RemotePayloadFile(
                    relativePath, normalizedUrlPath, node.Md5.ToLowerInvariant(), node.Size);

                if (result.TryGetValue(relativePath, out var existing) &&
                    (!string.Equals(existing.Md5, item.Md5, StringComparison.OrdinalIgnoreCase) ||
                     existing.Size != item.Size))
                {
                    throw new InvalidDataException(
                        $"Conflicting duplicate manifest entry: {relativePath}.");
                }

                result[relativePath] = item;
                if (result.Count > 100_000)
                    throw new InvalidDataException("The game_files manifest contains too many entries.");
            }

            return result.Values.ToList();
        }

        private static PayloadSeedRules BuildSeedRules(ServerPayloadProfile profile)
        {
            var rootFiles = new HashSet<string>(
                profile.RootFiles,
                StringComparer.OrdinalIgnoreCase);
            var directoryPrefixes = new HashSet<string>(
                profile.DirectoryPrefixes.Select(directory =>
                    directory.Replace('\\', '/').TrimEnd('/')),
                StringComparer.OrdinalIgnoreCase);

            return new PayloadSeedRules(rootFiles, directoryPrefixes);
        }

        private static bool IsAllowedPayloadFile(
            string relativePath,
            PayloadSeedRules rules)
        {
            if (IsDeploymentExcluded(relativePath)) return false;

            var normalized = relativePath.Replace('\\', '/');
            var separator = normalized.IndexOf('/');
            if (separator < 0)
                return rules.RootFiles.Contains(normalized);

            return rules.DirectoryPrefixes.Any(prefix =>
                normalized.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase));
        }

        private static void ValidateSelectedPayload(
            ServerPayloadProfile profile,
            IReadOnlyList<RemotePayloadFile> files)
        {
            if (files.Count > profile.MaxFileCount)
                throw new InvalidDataException(
                    $"The {profile.IconName} payload contains too many files ({files.Count}).");

            var totalBytes = files.Sum(x => x.Size);
            if (totalBytes > profile.MaxTotalBytes)
                throw new InvalidDataException(
                    $"The {profile.IconName} payload is unexpectedly large ({totalBytes} bytes).");

            var oversized = files.FirstOrDefault(x => x.Size > profile.MaxSingleFileBytes);
            if (oversized != null)
                throw new InvalidDataException(
                    $"The payload file {oversized.RelativePath} is unexpectedly large.");

            var paths = new HashSet<string>(
                files.Select(x => x.RelativePath.Replace('\\', '/')),
                StringComparer.OrdinalIgnoreCase);
            var missing = profile.RequiredFiles
                .Where(x => !paths.Contains(x))
                .ToArray();
            if (missing.Length > 0)
                throw new InvalidDataException(
                    $"The {profile.IconName} payload is missing required files: {string.Join(", ", missing)}.");
        }

        private static IEnumerable<string> GetSourceDirectories(ServerPayloadProfile profile)
        {
            var payloadDirectory = GetPayloadDirectory(profile);
            if (Directory.Exists(payloadDirectory))
                yield return payloadDirectory;
        }

        private static async Task DownloadFileAsync(
            Uri uri,
            string tempPath,
            long expectedSize,
            Action<long> reportDelta,
            CancellationToken cancellationToken)
        {
            const int maxAttempts = 3;
            long reportedBytes = 0;

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var existingLength = File.Exists(tempPath)
                        ? new FileInfo(tempPath).Length
                        : 0;

                    if (existingLength > expectedSize)
                    {
                        File.Delete(tempPath);
                        existingLength = 0;
                    }

                    var correction = existingLength - reportedBytes;
                    if (correction != 0)
                    {
                        reportDelta(correction);
                        reportedBytes += correction;
                    }

                    if (existingLength == expectedSize) return;

                    using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                    if (existingLength > 0)
                        request.Headers.Range = new RangeHeaderValue(existingLength, null);

                    using var response = await DownloadClient.SendAsync(
                            request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                        .ConfigureAwait(false);

                    if (existingLength > 0 && response.StatusCode != HttpStatusCode.PartialContent)
                    {
                        File.Delete(tempPath);
                        existingLength = 0;
                        if (reportedBytes != 0)
                        {
                            reportDelta(-reportedBytes);
                            reportedBytes = 0;
                        }
                    }

                    response.EnsureSuccessStatusCode();

                    await using var input =
                        await response.Content.ReadAsStreamAsync(cancellationToken)
                            .ConfigureAwait(false);
                    await using var output = new FileStream(
                        tempPath,
                        existingLength > 0 ? FileMode.Append : FileMode.Create,
                        FileAccess.Write,
                        FileShare.None,
                        81920,
                        true);

                    var buffer = new byte[81920];
                    int read;
                    while ((read = await input.ReadAsync(
                               buffer.AsMemory(0, buffer.Length), cancellationToken)
                               .ConfigureAwait(false)) > 0)
                    {
                        await output.WriteAsync(
                                buffer.AsMemory(0, read), cancellationToken)
                            .ConfigureAwait(false);
                        reportDelta(read);
                        reportedBytes += read;
                    }

                    await output.FlushAsync(cancellationToken).ConfigureAwait(false);
                    if (output.Length != expectedSize)
                        throw new InvalidDataException(
                            $"Size mismatch for {uri}. Expected {expectedSize}, got {output.Length}.");

                    return;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch when (attempt < maxAttempts)
                {
                    await Task.Delay(TimeSpan.FromSeconds(attempt), cancellationToken)
                        .ConfigureAwait(false);
                }
            }

            throw new IOException($"Download failed after {maxAttempts} attempts: {uri}");
        }

        private static async Task<bool> HasExpectedMd5Async(
            string filePath,
            string expectedMd5,
            CancellationToken cancellationToken)
        {
            using var md5 = MD5.Create();
            await using var stream = new FileStream(
                filePath, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete, 81920, true);
            var hash = await md5.ComputeHashAsync(stream, cancellationToken)
                .ConfigureAwait(false);
            return Convert.ToHexString(hash)
                .Equals(expectedMd5, StringComparison.OrdinalIgnoreCase);
        }

        private static async Task<bool> IsPayloadDirectoryCurrentAsync(
            ServerPayloadProfile profile,
            string directory,
            IReadOnlyList<RemotePayloadFile> files,
            IProgress<ServerPayloadUpdateProgress> progress,
            string version,
            CancellationToken cancellationToken)
        {
            for (var i = 0; i < files.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var node = files[i];
                Report(progress, profile, ServerPayloadUpdateStage.Comparing,
                    version, node.RelativePath, i + 1, files.Count);

                var path = SafeCombine(directory, node.RelativePath);
                if (!File.Exists(path)) return false;

                var info = new FileInfo(path);
                if (info.Attributes.HasFlag(FileAttributes.ReparsePoint) ||
                    info.Length != node.Size ||
                    !await HasExpectedMd5Async(path, node.Md5, cancellationToken)
                        .ConfigureAwait(false))
                {
                    return false;
                }
            }

            return true;
        }

        private static void ApplyStagedPayload(
            ServerPayloadProfile profile,
            string stagingDirectory,
            ServerPayloadState state)
        {
            Directory.CreateDirectory(PayloadRoot);
            Directory.CreateDirectory(StateRoot);

            var targetDirectory = GetPayloadDirectory(profile);
            var backupDirectory = targetDirectory + ".backup";
            var statePath = GetStatePath(profile);
            var targetMovedToBackup = false;
            var stagingMovedToTarget = false;

            if (Directory.Exists(backupDirectory))
                DeleteControlledDirectory(backupDirectory);

            try
            {
                if (Directory.Exists(targetDirectory))
                {
                    Directory.Move(targetDirectory, backupDirectory);
                    targetMovedToBackup = true;
                }

                Directory.Move(stagingDirectory, targetDirectory);
                stagingMovedToTarget = true;
                WriteStateAtomic(statePath, state);
            }
            catch
            {
                if (stagingMovedToTarget && Directory.Exists(targetDirectory))
                    DeleteControlledDirectory(targetDirectory);

                if (targetMovedToBackup && Directory.Exists(backupDirectory))
                    Directory.Move(backupDirectory, targetDirectory);

                throw;
            }

            if (targetMovedToBackup && Directory.Exists(backupDirectory))
            {
                try
                {
                    DeleteControlledDirectory(backupDirectory);
                }
                catch (Exception ex)
                {
                    LogHelper.LogError(
                        ex, $"ServerPayloadBackupCleanup({profile.IconName})");
                }
            }
        }

        private static void WriteStateAtomic(string path, ServerPayloadState state)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var tempPath = path + ".tmp";
            File.WriteAllText(tempPath, JsonSerializer.Serialize(state, StateJsonOptions));
            File.Move(tempPath, path, true);
        }

        private static string GetPayloadDirectory(ServerPayloadProfile profile)
        {
            return Path.Combine(PayloadRoot, profile.PayloadDirectoryName);
        }

        private static string GetStatePath(ServerPayloadProfile profile)
        {
            return Path.Combine(StateRoot, profile.PayloadDirectoryName + ".json");
        }

        private static string NormalizeRelativePath(string path)
        {
            var normalized = path.Replace('\\', '/').Trim();
            if (normalized.StartsWith("/", StringComparison.Ordinal) ||
                normalized.StartsWith("//", StringComparison.Ordinal) ||
                normalized.Contains(':'))
                throw new InvalidDataException($"Unsafe manifest path: {path}");

            var segments = normalized.Split(
                '/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0 ||
                segments.Any(IsUnsafePathSegment))
                throw new InvalidDataException($"Unsafe manifest path: {path}");

            return string.Join(Path.DirectorySeparatorChar, segments);
        }

        private static bool IsUnsafePathSegment(string segment)
        {
            if (segment is "." or ".." ||
                segment.Length == 0 ||
                !string.Equals(segment, segment.Trim(), StringComparison.Ordinal) ||
                segment.EndsWith(".", StringComparison.Ordinal))
                return true;

            var nameWithoutExtension = segment.Split('.')[0];
            if (nameWithoutExtension.Equals("CON", StringComparison.OrdinalIgnoreCase) ||
                nameWithoutExtension.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
                nameWithoutExtension.Equals("AUX", StringComparison.OrdinalIgnoreCase) ||
                nameWithoutExtension.Equals("NUL", StringComparison.OrdinalIgnoreCase))
                return true;

            if (nameWithoutExtension.Length == 4 &&
                (nameWithoutExtension.StartsWith("COM", StringComparison.OrdinalIgnoreCase) ||
                 nameWithoutExtension.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) &&
                nameWithoutExtension[3] is >= '1' and <= '9')
                return true;

            return false;
        }

        private static string SafeCombine(string root, string relativePath)
        {
            var fullRoot = Path.GetFullPath(root)
                .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var combined = Path.GetFullPath(Path.Combine(root, relativePath));
            if (!combined.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Path escapes payload directory: {relativePath}");
            return combined;
        }

        private static Uri BuildFileUri(Uri baseUri, string relativeUrlPath)
        {
            var escapedPath = string.Join(
                "/", relativeUrlPath.Split('/').Select(Uri.EscapeDataString));
            var uri = new Uri(baseUri.AbsoluteUri.TrimEnd('/') + "/" + escapedPath);
            if (uri.Scheme != Uri.UriSchemeHttps ||
                !string.Equals(uri.Host, baseUri.Host, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Unsafe payload URL: {uri}");
            return uri;
        }

        private static bool IsValidMd5(string value)
        {
            return value?.Length == 32 && value.All(Uri.IsHexDigit);
        }

        private static void DeleteControlledDirectory(string path)
        {
            var root = Path.GetFullPath(PayloadRoot)
                .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var target = Path.GetFullPath(path)
                .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Refusing to delete outside payload root: {path}");

            Directory.Delete(path, true);
        }

        private static HttpClient CreateHttpClient(TimeSpan timeout)
        {
            var client = new HttpClient { Timeout = timeout };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("XelLauncher/0.2.5");
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
            return client;
        }

        private static void Report(
            IProgress<ServerPayloadUpdateProgress> progress,
            ServerPayloadProfile profile,
            ServerPayloadUpdateStage stage,
            string version = "",
            string currentFile = "",
            int fileIndex = 0,
            int fileCount = 0,
            long downloadedBytes = 0,
            long totalBytes = 0)
        {
            progress?.Report(new ServerPayloadUpdateProgress
            {
                Profile = profile,
                Stage = stage,
                Version = version,
                CurrentFile = currentFile,
                FileIndex = fileIndex,
                FileCount = fileCount,
                DownloadedBytes = downloadedBytes,
                TotalBytes = totalBytes
            });
        }

        private sealed record RemotePayloadManifest(
            string Version,
            Uri ResourceBaseUri,
            string ManifestSha256,
            byte[] EncryptedManifest,
            IReadOnlyList<RemotePayloadFile> Files);

        private sealed record RemotePayloadPackage(
            string Version,
            Uri ResourceBaseUri);

        private sealed record RemotePayloadFile(
            string RelativePath,
            string UrlPath,
            string Md5,
            long Size);

        private sealed record PayloadFilePlan(
            RemotePayloadFile Node,
            string SourcePath);

        private sealed record CurrentVersionSnapshot(
            string Version,
            string ManifestSha256,
            int FileCount,
            long TotalBytes);

        private sealed record PayloadSeedRules(
            HashSet<string> RootFiles,
            HashSet<string> DirectoryPrefixes);

        private sealed class PayloadManifestNode
        {
            [JsonPropertyName("path")]
            public string Path { get; set; }

            [JsonPropertyName("md5")]
            public string Md5 { get; set; }

            [JsonPropertyName("size")]
            public long Size { get; set; }
        }
    }
}
