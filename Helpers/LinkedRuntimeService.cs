using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using XelLauncher.Models;

namespace XelLauncher.Helpers
{
    public enum LinkedRuntimeHealth
    {
        Healthy,
        Degraded,
        Invalid,
        Stale
    }

    public enum SharedRootMutationKind
    {
        InstallOrUpdate,
        Repair,
        Preload
    }

    public enum LinkedRuntimeStorageKind
    {
        HardLink,
        Independent
    }

    public sealed class LinkedRuntimePlanEntry
    {
        public ServerGameManifestFile TargetFile { get; init; }
        public ServerGameManifestFile BaseFile { get; init; }
        public LinkedRuntimeStorageKind StorageKind { get; init; }
        public bool IsAuxiliary { get; init; }
        public bool IsLocalOnly { get; init; }
    }

    public sealed class LinkedRuntimePlan
    {
        public IReadOnlyList<LinkedRuntimePlanEntry> Files { get; init; } =
            Array.Empty<LinkedRuntimePlanEntry>();
        public int LinkedFileCount => Files.Count(file =>
            file.StorageKind == LinkedRuntimeStorageKind.HardLink);
        public int IndependentFileCount => Files.Count - LinkedFileCount;
    }

    public sealed class LinkedRuntimeResult
    {
        public string RuntimePath { get; init; } = "";
        public LinkedRuntimeHealth Health { get; init; }
        public int LinkedFileCount { get; init; }
        public int IndependentFileCount { get; init; }
        public int SkippedFileCount { get; init; }
        public bool Reconciled { get; init; }
    }

    /// <summary>
    /// Maintains disposable channel runtimes beside (but never inside) a user's
    /// physical game directory. Runtime files are derived from trusted manifests;
    /// the user's configured RootPath is never changed.
    /// </summary>
    public static class LinkedRuntimeService
    {
        private const int MarkerSchemaVersion = 1;
        private const string RuntimeContainerName = ".xel-linked-runtime";
        private const string MarkerFileName = ".xel-runtime.json";
        private const string StagingDirectoryName = ".staging";
        private const string PreviousDirectoryName = ".previous";
        private const string ArknightsPersistentBundlesPath =
            "Arknights_Data/PersistentData/Bundles";
        private const string ArknightsHotUpdateListName =
            "hot_update_list.json";
        private const string ArknightsPersistentResourceListName =
            "persistent_res_list.json";

        private static readonly JsonSerializerOptions MarkerJsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        private static readonly JsonSerializerOptions InternalManifestJsonOptions =
            new()
            {
                PropertyNameCaseInsensitive = true
            };

        public static string GetRuntimePath(
            string gameId,
            string sharedRootPath,
            string targetChannel)
        {
            var normalizedRoot = SharedRootManager.NormalizeRootPath(sharedRootPath);
            var parent = Directory.GetParent(normalizedRoot)?.FullName;
            if (string.IsNullOrWhiteSpace(parent))
                throw new NotSupportedException(
                    "磁盘根目录不能直接作为 Linked Runtime 的 Shared Root。");

            var sharedRootId = SharedRootManager.GetSharedRootId(
                gameId, normalizedRoot);
            return Path.Combine(
                parent,
                RuntimeContainerName,
                SanitizePathSegment(gameId),
                sharedRootId,
                SanitizePathSegment(targetChannel));
        }

        public static bool CanCreateLinkedRuntime(
            SharedRootResolution resolution,
            out string reason)
        {
            reason = "";
            if (resolution?.Mode != SharedRootMode.Shared ||
                resolution.Base == null || resolution.Target == null)
            {
                reason = "Shared Root 或 BaseChannel 不可用";
                return false;
            }

            if (!GameChannelCatalog.CanCreateLinkedRuntime(
                    resolution.Base.IconName, resolution.Target.IconName))
            {
                reason = "渠道不属于同一兼容组或缺少可比较 Manifest";
                return false;
            }

            string runtimePath;
            try
            {
                runtimePath = GetRuntimePath(
                    resolution.GameId,
                    resolution.RootPath,
                    resolution.Target.Channel);
            }
            catch (Exception ex)
            {
                reason = ex.Message;
                return false;
            }

            if (!ServerPayloadDeployment.CanUseHardLinks(
                    resolution.RootPath, runtimePath))
            {
                reason = "Shared Root 与 Runtime 不在同一磁盘分区";
                return false;
            }

            try
            {
                var root = Path.GetPathRoot(resolution.RootPath);
                if (string.IsNullOrWhiteSpace(root) ||
                    !string.Equals(new DriveInfo(root).DriveFormat, "NTFS",
                        StringComparison.OrdinalIgnoreCase))
                {
                    reason = "Linked Runtime 需要 NTFS 文件系统";
                    return false;
                }

                var attributes = File.GetAttributes(resolution.RootPath);
                if (attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    reason = "Shared Root 不能是符号链接或目录联接";
                    return false;
                }
            }
            catch (Exception ex)
            {
                reason = "无法检查 Shared Root 文件系统：" + ex.Message;
                return false;
            }

            return true;
        }

        public static async Task<LinkedRuntimeResult> EnsureLinkedRuntimeAsync(
            SharedRootResolution resolution,
            Action<string> onProgress = null,
            bool operationAlreadyCoordinated = false,
            CancellationToken cancellationToken = default)
        {
            if (resolution?.Target == null ||
                string.IsNullOrWhiteSpace(resolution.RootPath))
            {
                throw new ArgumentException(
                    "Linked Runtime 缺少目标渠道或 Shared Root。",
                    nameof(resolution));
            }

            var runtimePath = GetRuntimePath(
                resolution.Target.GameId,
                resolution.RootPath,
                resolution.Target.Channel);
            IDisposable operationLease = null;
            if (!operationAlreadyCoordinated &&
                !LinkedClientOperationCoordinator.TryAcquirePaths(
                    new[] { resolution.RootPath, runtimePath },
                    out operationLease))
            {
                throw new InvalidOperationException(
                    "Shared Root 正在执行更新、校验、切服或 Runtime 维护。");
            }

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                // The caller normally holds this lease already, but resolving
                // again here also makes the public API safe when used directly.
                resolution = SharedRootManager.ResolveAndPersist(
                    resolution.Target.IconName,
                    resolution.RootPath,
                    detectBaseChannel: true);
                if (!CanCreateLinkedRuntime(resolution, out var reason))
                    throw new NotSupportedException(reason);
                runtimePath = GetRuntimePath(
                    resolution.GameId,
                    resolution.RootPath,
                    resolution.Target.Channel);
                CleanupOrphanedTransactionDirectories(runtimePath);

                onProgress?.Invoke("正在检查共享运行环境...");

                var marker = TryReadMarker(runtimePath);
                if (marker != null &&
                    !HasValidMarkerFileList(marker, runtimePath))
                {
                    LogHelper.Log(
                        $"Linked Runtime marker is invalid and will be rebuilt: " +
                        $"GameId={resolution.GameId} | Runtime={runtimePath}");
                    marker = null;
                }
                var metadata = resolution.State.LinkedRuntimes.FirstOrDefault(item =>
                    string.Equals(item.Channel, resolution.Target.Channel,
                        StringComparison.OrdinalIgnoreCase));
                var payloadState = ServerPayloadUpdater.GetState(
                    resolution.Target.IconName);
                var localBaseVersion = SharedRootManager.ReadInstalledVersion(
                    resolution.RootPath);

                if (CanUseMarkerWithoutRemoteManifest(
                        marker, metadata, resolution, payloadState,
                        localBaseVersion))
                {
                    var auxiliaryCounters = await
                        ReconcileArknightsPersistentResourcesAsync(
                                resolution, runtimePath, marker,
                                cancellationToken)
                            .ConfigureAwait(false);
                    var auxiliaryReconciled =
                        auxiliaryCounters.Changed > 0 ||
                        auxiliaryCounters.Removed > 0;
                    if (auxiliaryReconciled)
                    {
                        marker.UpdatedAtUtc = DateTimeOffset.UtcNow;
                        WriteMarkerAtomic(runtimePath, marker);
                        LogPersistentResourceResult(
                            resolution, auxiliaryCounters);
                    }

                    var health = await InspectHealthAsync(
                            runtimePath, resolution.RootPath, marker,
                            cancellationToken)
                        .ConfigureAwait(false);
                    if (health != LinkedRuntimeHealth.Invalid)
                    {
                        var fastResult = CreateResult(
                            runtimePath, health, marker,
                            skippedFileCount: marker.Files.Count(file =>
                                !file.IsAuxiliary) +
                                auxiliaryCounters.AuxiliarySkipped,
                            reconciled: auxiliaryReconciled);
                        PersistRuntimeMetadata(
                            resolution, marker, fastResult, payloadState,
                            isStale: false);
                        LogRuntimeResult(resolution, fastResult, "reused");
                        return fastResult;
                    }
                }

                onProgress?.Invoke("正在获取渠道文件清单...");
                var baseManifestTask = ServerPayloadUpdater.GetGameManifestAsync(
                    resolution.Base.IconName, cancellationToken);
                var targetManifestTask = ServerPayloadUpdater.GetGameManifestAsync(
                    resolution.Target.IconName, cancellationToken);
                await Task.WhenAll(baseManifestTask, targetManifestTask)
                    .ConfigureAwait(false);
                var baseManifest = await baseManifestTask.ConfigureAwait(false);
                var targetManifest = await targetManifestTask.ConfigureAwait(false);

                if (string.IsNullOrWhiteSpace(localBaseVersion) ||
                    !string.Equals(localBaseVersion, baseManifest.Version,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(
                        $"Shared Root 的 BaseChannel 版本不是最新版本：" +
                        $"local={localBaseVersion}, manifest={baseManifest.Version}");
                }

                onProgress?.Invoke("正在同步渠道差异文件...");
                await ServerPayloadUpdater.UpdateIfOutdatedAsync(
                        resolution.Target.PayloadProfile,
                        progress: null,
                        cancellationToken)
                    .ConfigureAwait(false);
                payloadState = ServerPayloadUpdater.GetState(
                    resolution.Target.IconName);
                if (payloadState == null ||
                    !string.Equals(
                        payloadState.ManifestSha256,
                        targetManifest.ManifestSha256,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(
                        "目标渠道 Manifest 在 Runtime 构建期间发生变化，请重试。");
                }

                var markerMatchesIdentity = MarkerMatchesRuntimeIdentity(
                    marker, resolution);
                RuntimeMarker updatedMarker;
                ReconcileCounters counters;
                if (markerMatchesIdentity && Directory.Exists(runtimePath))
                {
                    onProgress?.Invoke("正在增量更新共享运行环境...");
                    (updatedMarker, counters) = await ReconcileAsync(
                            resolution,
                            runtimePath,
                            marker,
                            baseManifest,
                            targetManifest,
                            payloadState,
                            cancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    onProgress?.Invoke("正在创建共享运行环境...");
                    (updatedMarker, counters) = await BuildFreshAsync(
                            resolution,
                            runtimePath,
                            baseManifest,
                            targetManifest,
                            payloadState,
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                var finalHealth = await InspectHealthAsync(
                        runtimePath, resolution.RootPath, updatedMarker,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (finalHealth == LinkedRuntimeHealth.Invalid)
                    throw new InvalidDataException(
                        "Linked Runtime 创建后未通过必要文件检查。");

                var result = new LinkedRuntimeResult
                {
                    RuntimePath = runtimePath,
                    Health = finalHealth,
                    LinkedFileCount = updatedMarker.Files.Count(file =>
                        file.StorageKind == LinkedRuntimeStorageKind.HardLink),
                    IndependentFileCount = updatedMarker.Files.Count(file =>
                        file.StorageKind == LinkedRuntimeStorageKind.Independent),
                    SkippedFileCount = counters.Skipped,
                    Reconciled = true
                };
                PersistRuntimeMetadata(
                    resolution, updatedMarker, result, payloadState,
                    isStale: false);
                LogHelper.Log(
                    $"Linked Runtime manifest diff: GameId={resolution.GameId} | " +
                    $"SharedRoot={resolution.RootPath} | BaseChannel={resolution.Base.Channel} | " +
                    $"TargetChannel={resolution.Target.Channel} | " +
                    $"CompatibilityGroup={resolution.Target.ClientCompatibilityGroup} | " +
                    $"Changed={counters.Changed} | Removed={counters.Removed} | " +
                    $"Skipped={counters.Skipped} | " +
                    $"SourceFallbackDownloads=" +
                    $"{counters.SourceFallbackDownloads} | " +
                    $"PersistentLinked={counters.AuxiliaryLinked} | " +
                    $"PersistentSkipped={counters.AuxiliarySkipped} | " +
                    $"PersistentMetadataSeeded=" +
                    $"{counters.PersistentMetadataSeeded}");
                LogRuntimeResult(resolution, result, "reconciled");
                return result;
            }
            catch (Exception ex)
            {
                MarkRuntimeFailure(resolution, runtimePath);
                LogHelper.LogError(ex,
                    $"Linked Runtime failed; fallback required | " +
                    $"GameId={resolution?.GameId} | SharedRoot={resolution?.RootPath} | " +
                    $"BaseChannel={resolution?.Base?.Channel} | " +
                    $"TargetChannel={resolution?.Target?.Channel} | " +
                    $"CompatibilityGroup={resolution?.Target?.ClientCompatibilityGroup}");
                throw;
            }
            finally
            {
                operationLease?.Dispose();
            }
        }

        public static Task PrepareSharedRootForMutationAsync(
            string effectiveIconName,
            string rootPath,
            SharedRootMutationKind mutationKind,
            CancellationToken cancellationToken = default)
        {
            var resolution = SharedRootManager.ResolveAndPersist(
                effectiveIconName, rootPath, detectBaseChannel: true);
            if (resolution.Mode != SharedRootMode.Shared ||
                resolution.Base == null)
            {
                return Task.CompletedTask;
            }

            if (mutationKind == SharedRootMutationKind.Preload)
            {
                // Preload files are not part of the installed full-package
                // manifest and mutable/temp paths are never hard-linked.
                return Task.CompletedTask;
            }

            var detached = 0;
            var invalidRuntimesRemoved = 0;
            var runtimeChannels = resolution.ConfiguredChannels
                .Select(channel => channel.Channel)
                .Concat(resolution.State?.LinkedRuntimes?
                            .Select(runtime => runtime.Channel) ??
                        Array.Empty<string>())
                .Where(channel => !string.IsNullOrWhiteSpace(channel))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            foreach (var runtimeChannel in runtimeChannels)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var definition = GameChannelCatalog.GetByGameAndChannel(
                    resolution.GameId, runtimeChannel);
                if (definition == null) continue;

                var runtimePath = GetRuntimePath(
                    resolution.GameId,
                    resolution.RootPath,
                    definition.Channel);
                if (!Directory.Exists(runtimePath)) continue;

                var marker = TryReadMarker(runtimePath);
                if (!MarkerMatchesSharedRoot(marker, resolution) ||
                    !HasValidMarkerFileList(marker, runtimePath))
                {
                    // A corrupt/missing marker cannot tell us which entries are
                    // shared. Removing this deterministic internal runtime is a
                    // metadata-safe way to guarantee the formal updater cannot
                    // write through an undiscovered hard link.
                    DeleteInternalDirectory(
                        runtimePath, Path.GetDirectoryName(runtimePath)!);
                    invalidRuntimesRemoved++;
                    continue;
                }

                foreach (var file in marker.Files.Where(file =>
                             file.StorageKind == LinkedRuntimeStorageKind.HardLink))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    // The current plugin uses delete+replace for delta files and
                    // repair downloads, but a full-package extraction can still
                    // open an existing destination for overwrite. Removing every
                    // shared directory entry is metadata-only (no 40 GB copy) and
                    // guarantees the formal installer never writes through a
                    // hard link into a runtime.
                    var mustDetach = mutationKind is
                        SharedRootMutationKind.Repair or
                        SharedRootMutationKind.InstallOrUpdate;
                    if (!mustDetach) continue;

                    var sourcePath = SafeCombine(
                        resolution.RootPath, file.RelativePath);
                    var runtimeFile = SafeCombine(
                        runtimePath, file.RelativePath);
                    if (!File.Exists(runtimeFile) ||
                        !File.Exists(sourcePath) ||
                        !IsSameFile(sourcePath, runtimeFile))
                    {
                        continue;
                    }

                    File.Delete(runtimeFile);
                    detached++;
                }
            }

            ConfigHelper.Update(config =>
            {
                var state = SharedRootManager.FindState(
                    config, resolution.GameId, resolution.RootPath);
                SharedRootManager.MarkRuntimesStale(
                    state, mutationKind.ToString());
            });
            LogHelper.Log(
                $"Linked Runtime prepared for mutation: GameId={resolution.GameId} | " +
                $"SharedRoot={resolution.RootPath} | BaseChannel={resolution.Base.Channel} | " +
                $"Mutation={mutationKind} | DetachedLinks={detached} | " +
                $"InvalidRuntimesRemoved={invalidRuntimesRemoved}");
            return Task.CompletedTask;
        }

        public static LinkedRuntimePlan BuildPlan(
            GameChannelDefinition targetChannel,
            ServerGameManifest baseManifest,
            ServerGameManifest targetManifest)
        {
            ArgumentNullException.ThrowIfNull(targetChannel);
            ArgumentNullException.ThrowIfNull(baseManifest);
            ArgumentNullException.ThrowIfNull(targetManifest);

            var baseFiles = baseManifest.Files.ToDictionary(
                file => NormalizeRelativePath(file.RelativePath),
                StringComparer.OrdinalIgnoreCase);
            var files = targetManifest.Files.Select(targetFile =>
            {
                baseFiles.TryGetValue(
                    NormalizeRelativePath(targetFile.RelativePath),
                    out var baseFile);
                var canLink = baseFile != null &&
                              ManifestFilesEqual(baseFile, targetFile) &&
                              IsSafeSharedPath(
                                  targetChannel,
                                  targetFile.RelativePath);
                return new LinkedRuntimePlanEntry
                {
                    TargetFile = targetFile,
                    BaseFile = baseFile,
                    StorageKind = canLink
                        ? LinkedRuntimeStorageKind.HardLink
                        : LinkedRuntimeStorageKind.Independent
                };
            }).ToArray();

            return new LinkedRuntimePlan { Files = files };
        }

        internal static IReadOnlyList<LinkedRuntimePlanEntry>
            BuildArknightsPersistentResourcePlan(
                GameChannelDefinition channel,
                string sharedRootPath)
        {
            if (channel?.Family != GameFamily.Arknights ||
                string.IsNullOrWhiteSpace(sharedRootPath))
            {
                return Array.Empty<LinkedRuntimePlanEntry>();
            }

            try
            {
                var bundlesRoot = SafeCombine(
                    sharedRootPath, ArknightsPersistentBundlesPath);
                var hotUpdatePath = SafeCombine(
                    bundlesRoot, ArknightsHotUpdateListName);
                var persistentListPath = SafeCombine(
                    bundlesRoot, ArknightsPersistentResourceListName);
                var hotUpdate = ReadArknightsResourceManifest(
                    hotUpdatePath, maxEntries: 50_000);
                var persistent = ReadArknightsResourceManifest(
                    persistentListPath, maxEntries: 10_000);
                if (hotUpdate == null || persistent == null ||
                    persistent.AbInfos.Count == 0)
                {
                    return Array.Empty<LinkedRuntimePlanEntry>();
                }

                var hotFiles = new Dictionary<string, ArknightsResourceEntry>(
                    StringComparer.OrdinalIgnoreCase);
                foreach (var entry in hotUpdate.AbInfos)
                {
                    var name = NormalizeInternalResourceName(entry.Name);
                    if (string.IsNullOrWhiteSpace(name) ||
                        !IsValidInternalResourceEntry(entry))
                    {
                        continue;
                    }

                    hotFiles.TryAdd(name, entry);
                }

                var selected = new Dictionary<string, ArknightsResourceEntry>(
                    StringComparer.OrdinalIgnoreCase);
                foreach (var entry in persistent.AbInfos)
                {
                    var name = NormalizeInternalResourceName(entry.Name);
                    if (string.IsNullOrWhiteSpace(name) ||
                        !IsValidInternalResourceEntry(entry) ||
                        !hotFiles.TryGetValue(name, out var current) ||
                        current.AbSize != entry.AbSize ||
                        !string.Equals(current.Md5, entry.Md5,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    selected.TryAdd(name, current);
                }

                var manifestName = NormalizeInternalResourceName(
                    persistent.ManifestName);
                if (!string.IsNullOrWhiteSpace(manifestName) &&
                    hotFiles.TryGetValue(manifestName, out var manifestEntry))
                {
                    selected.TryAdd(manifestName, manifestEntry);
                }

                var result = new List<LinkedRuntimePlanEntry>(selected.Count);
                long totalBytes = 0;
                foreach (var pair in selected.OrderBy(
                             item => item.Key,
                             StringComparer.OrdinalIgnoreCase))
                {
                    var relativePath = NormalizeRelativePath(
                        $"{ArknightsPersistentBundlesPath}/{pair.Key}");
                    var manifestFile = new ServerGameManifestFile
                    {
                        RelativePath = relativePath,
                        UrlPath = relativePath,
                        Size = pair.Value.AbSize,
                        Md5 = pair.Value.Md5
                    };
                    var sourcePath = SafeCombine(
                        sharedRootPath, relativePath);
                    if (!IsUsableManifestSource(sourcePath, manifestFile))
                        continue;

                    totalBytes = checked(totalBytes + manifestFile.Size);
                    if (totalBytes > 100L * 1024 * 1024 * 1024)
                        throw new InvalidDataException(
                            "Arknights PersistentData resource list is unexpectedly large.");

                    result.Add(new LinkedRuntimePlanEntry
                    {
                        BaseFile = manifestFile,
                        TargetFile = manifestFile,
                        StorageKind = LinkedRuntimeStorageKind.HardLink,
                        IsAuxiliary = true,
                        IsLocalOnly = true
                    });
                }

                return result;
            }
            catch (Exception ex)
            {
                LogHelper.LogError(
                    ex, "Arknights persistent resource manifest parsing");
                return Array.Empty<LinkedRuntimePlanEntry>();
            }
        }

        private static ArknightsResourceManifest ReadArknightsResourceManifest(
            string path,
            int maxEntries)
        {
            if (!File.Exists(path)) return null;
            var info = new FileInfo(path);
            if (info.Length <= 0 || info.Length > 16L * 1024 * 1024)
                return null;

            var manifest = JsonSerializer.Deserialize<ArknightsResourceManifest>(
                File.ReadAllText(path), InternalManifestJsonOptions);
            if (manifest?.AbInfos == null ||
                manifest.AbInfos.Count > maxEntries)
            {
                return null;
            }

            return manifest;
        }

        private static bool IsValidInternalResourceEntry(
            ArknightsResourceEntry entry) =>
            entry != null &&
            entry.AbSize >= 0 &&
            entry.AbSize <= 8L * 1024 * 1024 * 1024 &&
            IsHexHash(entry.Md5, 32);

        private static string NormalizeInternalResourceName(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || Path.IsPathRooted(name))
                return "";

            var normalized = NormalizeRelativePath(name).Trim('/');
            var segments = normalized.Split(
                '/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0 ||
                segments.Any(segment =>
                    segment == "." || segment == ".."))
            {
                return "";
            }

            return string.Join('/', segments);
        }

        public static bool IsSafeSharedPath(
            GameChannelDefinition channel,
            string relativePath)
        {
            if (channel == null || string.IsNullOrWhiteSpace(relativePath))
                return false;
            var normalized = NormalizeRelativePath(relativePath);
            var dataDirectory = channel.Family == GameFamily.Endfield
                ? "Endfield_Data/"
                : "Arknights_Data/";
            if (!normalized.StartsWith(dataDirectory,
                    StringComparison.OrdinalIgnoreCase) ||
                ServerPayloadDeployment.IsManagedPath(
                    channel.Family, normalized))
            {
                return false;
            }

            var segments = normalized.Split('/',
                StringSplitOptions.RemoveEmptyEntries);
            if (segments.Any(segment => MutableDirectoryNames.Contains(segment)))
                return false;

            var fileName = Path.GetFileNameWithoutExtension(normalized);
            if (MutableNameFragments.Any(fragment =>
                    fileName.Contains(fragment,
                        StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            return !MutableExtensions.Contains(Path.GetExtension(normalized));
        }

        private static readonly HashSet<string> MutableDirectoryNames = new(
            new[]
            {
                "Cache", "Caches", "Logs", "Log", "Temp", "Temporary",
                "Save", "Saves", "Saved", "UserData", "PersistentData",
                "Crash", "Crashes"
            },
            StringComparer.OrdinalIgnoreCase);

        private static readonly string[] MutableNameFragments =
        {
            "config", "setting", "account", "login", "session", "save",
            "preference", "userdata"
        };

        private static readonly HashSet<string> MutableExtensions = new(
            new[]
            {
                ".ini", ".cfg", ".config", ".json", ".xml", ".log", ".tmp",
                ".lock", ".db", ".sqlite", ".sqlite3", ".info"
            },
            StringComparer.OrdinalIgnoreCase);

        private static async Task<(RuntimeMarker Marker, ReconcileCounters Counters)>
            BuildFreshAsync(
                SharedRootResolution resolution,
                string runtimePath,
                ServerGameManifest baseManifest,
                ServerGameManifest targetManifest,
                ServerPayloadState payloadState,
                CancellationToken cancellationToken)
        {
            var parent = Path.GetDirectoryName(runtimePath)!;
            Directory.CreateDirectory(parent);
            // Operations for one Shared Root are serialized, so deterministic
            // transaction directory names are safe and keep deep game paths
            // well below the legacy Win32 MAX_PATH boundary where possible.
            var staging = Path.Combine(parent, StagingDirectoryName);
            var backup = Path.Combine(parent, PreviousDirectoryName);
            try
            {
                Directory.CreateDirectory(staging);
                var result = await ReconcileAsync(
                        resolution, staging, oldMarker: null,
                        baseManifest, targetManifest, payloadState,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (Directory.Exists(runtimePath))
                    Directory.Move(runtimePath, backup);
                Directory.Move(staging, runtimePath);
                return result;
            }
            catch
            {
                if (Directory.Exists(staging))
                    DeleteInternalDirectory(staging, parent);
                if (!Directory.Exists(runtimePath) && Directory.Exists(backup))
                    Directory.Move(backup, runtimePath);
                throw;
            }
            finally
            {
                if (Directory.Exists(backup))
                {
                    try { DeleteInternalDirectory(backup, parent); }
                    catch (Exception ex)
                    {
                        LogHelper.LogError(ex,
                            "Linked Runtime invalid backup cleanup");
                    }
                }
            }
        }

        private static async Task<(RuntimeMarker Marker, ReconcileCounters Counters)>
            ReconcileAsync(
                SharedRootResolution resolution,
                string runtimePath,
                RuntimeMarker oldMarker,
                ServerGameManifest baseManifest,
                ServerGameManifest targetManifest,
                ServerPayloadState payloadState,
                CancellationToken cancellationToken)
        {
            Directory.CreateDirectory(runtimePath);
            var plan = BuildPlan(
                resolution.Target, baseManifest, targetManifest);
            if (plan.LinkedFileCount == 0)
                throw new InvalidDataException(
                    "两个渠道 Manifest 中没有可安全复用的公共资源。");

            var primaryPaths = new HashSet<string>(
                plan.Files.Select(file => NormalizeRelativePath(
                    file.TargetFile.RelativePath)),
                StringComparer.OrdinalIgnoreCase);
            var auxiliaryPlan = BuildArknightsPersistentResourcePlan(
                    resolution.Target, resolution.RootPath)
                .Where(file => primaryPaths.Add(NormalizeRelativePath(
                    file.TargetFile.RelativePath)));
            var plannedFiles = plan.Files.Concat(auxiliaryPlan).ToArray();

            var oldFiles = (oldMarker?.Files ?? new List<RuntimeFileRecord>())
                .ToDictionary(
                    file => NormalizeRelativePath(file.RelativePath),
                    StringComparer.OrdinalIgnoreCase);
            var newPaths = new HashSet<string>(
                plannedFiles.Select(file => NormalizeRelativePath(
                    file.TargetFile.RelativePath)),
                StringComparer.OrdinalIgnoreCase);
            var counters = new ReconcileCounters();

            foreach (var removed in oldFiles.Values.Where(file =>
                         !newPaths.Contains(NormalizeRelativePath(
                             file.RelativePath))))
            {
                cancellationToken.ThrowIfCancellationRequested();
                DeleteFileIfExists(SafeCombine(runtimePath, removed.RelativePath));
                counters.Removed++;
            }

            var records = new List<RuntimeFileRecord>(plannedFiles.Length);
            foreach (var planned in plannedFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var targetFile = planned.TargetFile;
                var relativePath = NormalizeRelativePath(targetFile.RelativePath);
                var runtimeFile = SafeCombine(runtimePath, relativePath);
                oldFiles.TryGetValue(relativePath, out var oldRecord);
                var actualStorageKind = ResolveStorageKindForSource(
                    resolution.RootPath, planned);
                if (planned.IsLocalOnly &&
                    actualStorageKind != planned.StorageKind)
                {
                    DeleteFileIfExists(runtimeFile);
                    counters.Removed++;
                    continue;
                }

                if (actualStorageKind == LinkedRuntimeStorageKind.HardLink)
                {
                    var sourceFile = SafeCombine(
                        resolution.RootPath, planned.BaseFile.RelativePath);
                    ValidateSourceFile(sourceFile, planned.BaseFile);
                    if (File.Exists(runtimeFile) &&
                        IsSameFile(sourceFile, runtimeFile))
                    {
                        counters.Skipped++;
                        if (planned.IsAuxiliary)
                            counters.AuxiliarySkipped++;
                    }
                    else
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(runtimeFile)!);
                        DeleteFileIfExists(runtimeFile);
                        if (!WindowsHardLink.TryCreate(
                                runtimeFile, sourceFile, out var errorCode))
                            throw new Win32Exception(errorCode,
                                $"CreateHardLink failed: {relativePath}");
                        counters.Changed++;
                        if (planned.IsAuxiliary)
                            counters.AuxiliaryLinked++;
                    }
                }
                else if (ServerPayloadDeployment.IsManagedPath(
                             resolution.Target.Family, relativePath))
                {
                    // UpdateIfOutdatedAsync already downloaded and verified the
                    // authoritative channel payload. DeployProfileAsync below
                    // replaces the complete managed set transactionally, so
                    // downloading the same SDK/config file here would duplicate
                    // network and disk I/O.
                    counters.Changed++;
                }
                else if (CanReuseIndependentFile(
                             runtimeFile, oldRecord, targetFile))
                {
                    counters.Skipped++;
                }
                else
                {
                    var copiedFromSharedRoot = false;
                    if (planned.BaseFile != null &&
                        ManifestFilesEqual(planned.BaseFile, targetFile))
                    {
                        var sourceFile = SafeCombine(
                            resolution.RootPath, planned.BaseFile.RelativePath);
                        if (IsUsableManifestSource(
                                sourceFile, planned.BaseFile))
                        {
                            await CopyFileAtomicAsync(
                                    sourceFile, runtimeFile, cancellationToken)
                                .ConfigureAwait(false);
                            copiedFromSharedRoot = true;
                        }
                        else
                        {
                            // This file is deliberately independent (for
                            // example a root-level DLL). A traditional server
                            // switch may leave a channel-specific version in
                            // Shared Root even when both game manifests list
                            // the same target content. Do not abort the whole
                            // runtime: obtain the authoritative target object
                            // from its manifest instead.
                            counters.SourceFallbackDownloads++;
                        }
                    }

                    if (!copiedFromSharedRoot)
                    {
                        await DownloadFileAtomicAsync(
                                targetManifest, targetFile, runtimeFile,
                                cancellationToken)
                            .ConfigureAwait(false);
                    }
                    counters.Changed++;
                }

                records.Add(CreateRecord(
                    planned, runtimeFile, actualStorageKind));
            }

            await SeedArknightsPersistentMetadataAsync(
                    resolution.Target, resolution.RootPath, runtimePath,
                    counters, cancellationToken)
                .ConfigureAwait(false);

            WriteBytesAtomic(
                Path.Combine(runtimePath, "game_files"),
                targetManifest.EncryptedManifest);
            await ServerPayloadUpdater.UsePayloadDirectoryAsync(
                    resolution.Target.IconName,
                    async payloadDirectory =>
                    {
                        if (string.IsNullOrWhiteSpace(payloadDirectory) ||
                            !Directory.Exists(payloadDirectory))
                        {
                            throw new FileNotFoundException(
                                "Linked Runtime 缺少目标渠道切服差异文件。");
                        }

                        await ServerPayloadDeployment.DeployProfileAsync(
                                resolution.Target.PayloadProfile,
                                payloadDirectory,
                                runtimePath,
                                preferHardLink: false)
                            .ConfigureAwait(false);
                        return true;
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            // Payload deployment may replace independent files after their
            // records were captured. Refresh cheap file metadata before commit.
            foreach (var record in records)
            {
                var path = SafeCombine(runtimePath, record.RelativePath);
                if (File.Exists(path))
                    record.LastWriteTimeUtcTicks = File.GetLastWriteTimeUtc(path).Ticks;
            }

            var marker = new RuntimeMarker
            {
                SchemaVersion = MarkerSchemaVersion,
                GameId = resolution.GameId,
                SharedRootPath = resolution.RootPath,
                SharedRootId = SharedRootManager.GetSharedRootId(
                    resolution.GameId, resolution.RootPath),
                CompatibilityGroup = resolution.Target.ClientCompatibilityGroup,
                BaseChannel = resolution.Base.Channel,
                TargetChannel = resolution.Target.Channel,
                BaseVersion = baseManifest.Version,
                TargetVersion = targetManifest.Version,
                BaseManifestSha256 = baseManifest.ManifestSha256,
                TargetManifestSha256 = targetManifest.ManifestSha256,
                PayloadManifestSha256 = payloadState?.ManifestSha256 ?? "",
                UpdatedAtUtc = DateTimeOffset.UtcNow,
                Files = records
            };
            WriteMarkerAtomic(runtimePath, marker);
            return (marker, counters);
        }

        private static async Task<ReconcileCounters>
            ReconcileArknightsPersistentResourcesAsync(
                SharedRootResolution resolution,
                string runtimePath,
                RuntimeMarker marker,
                CancellationToken cancellationToken)
        {
            var counters = new ReconcileCounters();
            if (resolution?.Target?.Family != GameFamily.Arknights ||
                marker?.Files == null)
            {
                return counters;
            }

            var plan = BuildArknightsPersistentResourcePlan(
                resolution.Target, resolution.RootPath);
            var desiredPaths = new HashSet<string>(
                plan.Select(file => NormalizeRelativePath(
                    file.TargetFile.RelativePath)),
                StringComparer.OrdinalIgnoreCase);

            foreach (var obsolete in marker.Files.Where(file =>
                         file.IsAuxiliary &&
                         !desiredPaths.Contains(NormalizeRelativePath(
                             file.RelativePath))).ToArray())
            {
                cancellationToken.ThrowIfCancellationRequested();
                DeleteFileIfExists(SafeCombine(
                    runtimePath, obsolete.RelativePath));
                marker.Files.Remove(obsolete);
                counters.Removed++;
            }

            foreach (var planned in plan)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relativePath = NormalizeRelativePath(
                    planned.TargetFile.RelativePath);
                var sourceFile = SafeCombine(
                    resolution.RootPath, planned.BaseFile.RelativePath);
                var runtimeFile = SafeCombine(runtimePath, relativePath);
                if (!IsUsableManifestSource(
                        sourceFile, planned.BaseFile))
                {
                    continue;
                }

                var oldRecord = marker.Files.FirstOrDefault(file =>
                    string.Equals(
                        NormalizeRelativePath(file.RelativePath),
                        relativePath,
                        StringComparison.OrdinalIgnoreCase));
                var isLinked = File.Exists(runtimeFile) &&
                               IsSameFile(sourceFile, runtimeFile);
                var recordMatches = AuxiliaryRecordMatches(
                    oldRecord, planned);
                if (isLinked && recordMatches)
                {
                    counters.Skipped++;
                    counters.AuxiliarySkipped++;
                    continue;
                }

                if (!isLinked)
                {
                    Directory.CreateDirectory(
                        Path.GetDirectoryName(runtimeFile)!);
                    DeleteFileIfExists(runtimeFile);
                    if (!WindowsHardLink.TryCreate(
                            runtimeFile, sourceFile, out var errorCode))
                    {
                        throw new Win32Exception(
                            errorCode,
                            $"CreateHardLink failed: {relativePath}");
                    }

                    counters.AuxiliaryLinked++;
                }

                if (oldRecord != null)
                    marker.Files.Remove(oldRecord);
                marker.Files.Add(CreateRecord(
                    planned,
                    runtimeFile,
                    LinkedRuntimeStorageKind.HardLink));
                counters.Changed++;
            }

            await SeedArknightsPersistentMetadataAsync(
                    resolution.Target, resolution.RootPath, runtimePath,
                    counters, cancellationToken)
                .ConfigureAwait(false);
            return counters;
        }

        private static bool AuxiliaryRecordMatches(
            RuntimeFileRecord record,
            LinkedRuntimePlanEntry planned) =>
            record != null &&
            record.IsAuxiliary &&
            record.StorageKind == LinkedRuntimeStorageKind.HardLink &&
            record.Size == planned.TargetFile.Size &&
            record.SourceSize == planned.BaseFile.Size &&
            string.Equals(record.Md5, planned.TargetFile.Md5,
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(record.SourceMd5, planned.BaseFile.Md5,
                StringComparison.OrdinalIgnoreCase);

        private static async Task SeedArknightsPersistentMetadataAsync(
            GameChannelDefinition channel,
            string sharedRootPath,
            string runtimePath,
            ReconcileCounters counters,
            CancellationToken cancellationToken)
        {
            if (channel?.Family != GameFamily.Arknights)
                return;

            var sourceBundles = SafeCombine(
                sharedRootPath, ArknightsPersistentBundlesPath);
            var targetBundles = SafeCombine(
                runtimePath, ArknightsPersistentBundlesPath);
            var sourcePersistentList = SafeCombine(
                sourceBundles, ArknightsPersistentResourceListName);
            var sourcePersistent = ReadArknightsResourceManifest(
                sourcePersistentList, maxEntries: 10_000);
            if (sourcePersistent?.AbInfos == null ||
                sourcePersistent.AbInfos.Count == 0)
            {
                return;
            }

            var targetPersistentList = SafeCombine(
                targetBundles, ArknightsPersistentResourceListName);
            var targetPersistent = ReadArknightsResourceManifest(
                targetPersistentList, maxEntries: 10_000);
            var seededPersistentList = targetPersistent?.AbInfos == null ||
                                       targetPersistent.AbInfos.Count == 0;
            if (seededPersistentList)
            {
                await CopyFileAtomicAsync(
                        sourcePersistentList,
                        targetPersistentList,
                        cancellationToken)
                    .ConfigureAwait(false);
                counters.Changed++;
                counters.PersistentMetadataSeeded++;
            }

            var sourceHotUpdate = SafeCombine(
                sourceBundles, ArknightsHotUpdateListName);
            var targetHotUpdate = SafeCombine(
                targetBundles, ArknightsHotUpdateListName);
            if (!File.Exists(targetHotUpdate) &&
                File.Exists(sourceHotUpdate))
            {
                await CopyFileAtomicAsync(
                        sourceHotUpdate,
                        targetHotUpdate,
                        cancellationToken)
                    .ConfigureAwait(false);
                counters.Changed++;
                counters.PersistentMetadataSeeded++;
            }

            if (!seededPersistentList)
                return;

            var persistentDataRoot = SafeCombine(
                runtimePath, "Arknights_Data/PersistentData");
            var downloadDirectory = SafeCombine(
                persistentDataRoot, "HGDownload");
            if (!Directory.Exists(downloadDirectory))
                return;

            try
            {
                DeleteInternalDirectory(
                    downloadDirectory, persistentDataRoot);
                counters.Removed++;
            }
            catch (Exception ex)
            {
                LogHelper.LogError(
                    ex, "Arknights persistent download cache cleanup");
            }
        }

        private static void LogPersistentResourceResult(
            SharedRootResolution resolution,
            ReconcileCounters counters)
        {
            LogHelper.Log(
                $"Linked Runtime persistent resources synchronized: " +
                $"GameId={resolution.GameId} | " +
                $"SharedRoot={resolution.RootPath} | " +
                $"BaseChannel={resolution.Base.Channel} | " +
                $"TargetChannel={resolution.Target.Channel} | " +
                $"Linked={counters.AuxiliaryLinked} | " +
                $"Skipped={counters.AuxiliarySkipped} | " +
                $"Removed={counters.Removed} | " +
                $"MetadataSeeded={counters.PersistentMetadataSeeded}");
        }

        private static RuntimeFileRecord CreateRecord(
            LinkedRuntimePlanEntry planned,
            string runtimeFile,
            LinkedRuntimeStorageKind storageKind)
        {
            return new RuntimeFileRecord
            {
                RelativePath = NormalizeRelativePath(
                    planned.TargetFile.RelativePath),
                Size = planned.TargetFile.Size,
                Md5 = planned.TargetFile.Md5,
                SourceSize = planned.BaseFile?.Size ?? -1,
                SourceMd5 = planned.BaseFile?.Md5 ?? "",
                StorageKind = storageKind,
                IsAuxiliary = planned.IsAuxiliary,
                LastWriteTimeUtcTicks = File.Exists(runtimeFile)
                    ? File.GetLastWriteTimeUtc(runtimeFile).Ticks
                    : 0
            };
        }

        private static async Task<LinkedRuntimeHealth> InspectHealthAsync(
            string runtimePath,
            string sharedRootPath,
            RuntimeMarker marker,
            CancellationToken cancellationToken)
        {
            var gameFilesPath = Path.Combine(runtimePath, "game_files");
            if (marker == null || !Directory.Exists(runtimePath) ||
                !File.Exists(gameFilesPath) ||
                !await HasExpectedSha256Async(
                        gameFilesPath,
                        marker.TargetManifestSha256,
                        cancellationToken)
                    .ConfigureAwait(false))
            {
                return LinkedRuntimeHealth.Invalid;
            }

            var definition = GameChannelCatalog.GetByGameAndChannel(
                marker.GameId, marker.TargetChannel);
            var executableName = definition?.Family == GameFamily.Endfield
                ? "Endfield.exe"
                : "Arknights.exe";
            if (definition == null ||
                !File.Exists(Path.Combine(runtimePath, executableName)))
            {
                return LinkedRuntimeHealth.Invalid;
            }

            var requiredContentPaths = new HashSet<string>(
                definition.PayloadProfile?.RequiredFiles
                    .Select(NormalizeRelativePath) ??
                Array.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);
            requiredContentPaths.Add(NormalizeRelativePath(executableName));
            requiredContentPaths.Add("config.ini");
            var recordedPaths = new HashSet<string>(
                marker.Files.Select(file =>
                    NormalizeRelativePath(file.RelativePath)),
                StringComparer.OrdinalIgnoreCase);
            if (requiredContentPaths.Any(path => !recordedPaths.Contains(path)))
                return LinkedRuntimeHealth.Invalid;

            var degraded = false;
            foreach (var record in marker.Files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var runtimeFile = SafeCombine(runtimePath, record.RelativePath);
                if (!File.Exists(runtimeFile) ||
                    new FileInfo(runtimeFile).Length != record.Size)
                {
                    return LinkedRuntimeHealth.Invalid;
                }

                if (record.StorageKind == LinkedRuntimeStorageKind.HardLink)
                {
                    var sourceFile = SafeCombine(
                        sharedRootPath, record.RelativePath);
                    var sourceStillMatches = File.Exists(sourceFile) &&
                                             new FileInfo(sourceFile).Length ==
                                             record.SourceSize;
                    var isStillLinked = sourceStillMatches &&
                                        IsSameFile(sourceFile, runtimeFile);
                    if (!isStillLinked &&
                        !await HasExpectedMd5Async(
                                runtimeFile, record.Md5, cancellationToken)
                            .ConfigureAwait(false))
                    {
                        return LinkedRuntimeHealth.Invalid;
                    }
                    if (!isStillLinked) degraded = true;
                    continue;
                }

                var writeTicks = File.GetLastWriteTimeUtc(runtimeFile).Ticks;
                var mustVerifyContent = requiredContentPaths.Contains(
                                            NormalizeRelativePath(
                                                record.RelativePath)) ||
                                        (record.LastWriteTimeUtcTicks != 0 &&
                                         writeTicks !=
                                         record.LastWriteTimeUtcTicks);
                if (mustVerifyContent &&
                    !await HasExpectedMd5Async(
                            runtimeFile, record.Md5, cancellationToken)
                        .ConfigureAwait(false))
                {
                    return LinkedRuntimeHealth.Invalid;
                }
            }

            return degraded
                ? LinkedRuntimeHealth.Degraded
                : LinkedRuntimeHealth.Healthy;
        }

        private static bool CanUseMarkerWithoutRemoteManifest(
            RuntimeMarker marker,
            LinkedRuntimeMetadata metadata,
            SharedRootResolution resolution,
            ServerPayloadState payloadState,
            string localBaseVersion)
        {
            if (!MarkerMatchesIdentity(marker, resolution) ||
                !Directory.Exists(GetRuntimePath(
                    resolution.GameId,
                    resolution.RootPath,
                    resolution.Target.Channel)) ||
                metadata?.IsStale == true)
            {
                return false;
            }

            return string.Equals(marker.BaseVersion, localBaseVersion,
                       StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(marker.PayloadManifestSha256,
                       payloadState?.ManifestSha256 ?? "",
                       StringComparison.OrdinalIgnoreCase);
        }

        private static bool MarkerMatchesIdentity(
            RuntimeMarker marker,
            SharedRootResolution resolution) =>
            MarkerMatchesRuntimeIdentity(marker, resolution) &&
            string.Equals(marker.BaseChannel, resolution.Base?.Channel,
                StringComparison.OrdinalIgnoreCase);

        private static bool MarkerMatchesRuntimeIdentity(
            RuntimeMarker marker,
            SharedRootResolution resolution) =>
            MarkerMatchesSharedRoot(marker, resolution) &&
            string.Equals(marker.TargetChannel, resolution.Target?.Channel,
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(marker.CompatibilityGroup,
                resolution.Target?.ClientCompatibilityGroup,
                StringComparison.OrdinalIgnoreCase);

        private static bool MarkerMatchesSharedRoot(
            RuntimeMarker marker,
            SharedRootResolution resolution) =>
            marker != null && marker.SchemaVersion == MarkerSchemaVersion &&
            string.Equals(marker.GameId, resolution.GameId,
                StringComparison.OrdinalIgnoreCase) &&
            SharedRootManager.PathsEqual(
                marker.SharedRootPath, resolution.RootPath) &&
            string.Equals(marker.SharedRootId,
                SharedRootManager.GetSharedRootId(
                    resolution.GameId, resolution.RootPath),
                StringComparison.OrdinalIgnoreCase);

        private static RuntimeMarker TryReadMarker(string runtimePath)
        {
            try
            {
                var markerPath = Path.Combine(runtimePath, MarkerFileName);
                if (!File.Exists(markerPath)) return null;
                var marker = JsonSerializer.Deserialize<RuntimeMarker>(
                    File.ReadAllText(markerPath), MarkerJsonOptions);
                if (marker != null) marker.Files ??= new();
                return marker;
            }
            catch (Exception ex)
            {
                LogHelper.LogError(ex, "Linked Runtime marker read");
                return null;
            }
        }

        private static bool HasValidMarkerFileList(
            RuntimeMarker marker,
            string runtimePath)
        {
            if (marker?.Files == null || marker.Files.Count == 0 ||
                marker.Files.Count > 100_000 ||
                !IsHexHash(marker.BaseManifestSha256, 64) ||
                !IsHexHash(marker.TargetManifestSha256, 64))
            {
                return false;
            }

            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in marker.Files)
            {
                if (file == null || file.Size < 0 ||
                    !IsHexHash(file.Md5, 32) ||
                    !Enum.IsDefined(
                        typeof(LinkedRuntimeStorageKind), file.StorageKind))
                {
                    return false;
                }
                if (file.StorageKind == LinkedRuntimeStorageKind.HardLink &&
                    (file.SourceSize != file.Size ||
                     !IsHexHash(file.SourceMd5, 32)))
                {
                    return false;
                }

                var normalized = NormalizeRelativePath(file.RelativePath);
                if (string.IsNullOrWhiteSpace(normalized) ||
                    !paths.Add(normalized))
                {
                    return false;
                }

                try
                {
                    _ = SafeCombine(runtimePath, normalized);
                }
                catch
                {
                    return false;
                }
            }

            return true;
        }

        private static void WriteMarkerAtomic(
            string runtimePath,
            RuntimeMarker marker)
        {
            var markerPath = Path.Combine(runtimePath, MarkerFileName);
            var tempPath = markerPath + ".tmp-" + Guid.NewGuid().ToString("N");
            File.WriteAllText(
                tempPath,
                JsonSerializer.Serialize(marker, MarkerJsonOptions));
            File.Move(tempPath, markerPath, overwrite: true);
        }

        private static void PersistRuntimeMetadata(
            SharedRootResolution resolution,
            RuntimeMarker marker,
            LinkedRuntimeResult result,
            ServerPayloadState payloadState,
            bool isStale)
        {
            ConfigHelper.Update(config =>
            {
                var state = SharedRootManager.FindState(
                    config, resolution.GameId, resolution.RootPath);
                if (state == null) return;
                state.LinkedRuntimes ??= new();
                var metadata = state.LinkedRuntimes.FirstOrDefault(item =>
                    string.Equals(item.Channel, resolution.Target.Channel,
                        StringComparison.OrdinalIgnoreCase));
                if (metadata == null)
                {
                    metadata = new LinkedRuntimeMetadata
                    {
                        Channel = resolution.Target.Channel
                    };
                    state.LinkedRuntimes.Add(metadata);
                }

                metadata.RuntimePath = result.RuntimePath;
                metadata.CompatibilityGroup =
                    resolution.Target.ClientCompatibilityGroup;
                metadata.BaseChannel = marker.BaseChannel;
                metadata.BaseManifestSha256 = marker.BaseManifestSha256;
                metadata.TargetManifestSha256 = marker.TargetManifestSha256;
                metadata.BaseVersion = marker.BaseVersion;
                metadata.TargetVersion = marker.TargetVersion;
                metadata.PayloadManifestSha256 =
                    payloadState?.ManifestSha256 ??
                    marker.PayloadManifestSha256;
                metadata.Health = result.Health.ToString();
                metadata.IsStale = isStale;
                metadata.LinkedFileCount = result.LinkedFileCount;
                metadata.IndependentFileCount = result.IndependentFileCount;
                metadata.SkippedFileCount = result.SkippedFileCount;
                metadata.UpdatedAtUtc = DateTimeOffset.UtcNow;
            });
        }

        private static void MarkRuntimeFailure(
            SharedRootResolution resolution,
            string runtimePath)
        {
            if (resolution?.State == null) return;
            try
            {
                ConfigHelper.Update(config =>
                {
                    var state = SharedRootManager.FindState(
                        config, resolution.GameId, resolution.RootPath);
                    if (state == null) return;
                    state.LinkedRuntimes ??= new();
                    var metadata = state.LinkedRuntimes.FirstOrDefault(item =>
                        string.Equals(item.Channel, resolution.Target.Channel,
                            StringComparison.OrdinalIgnoreCase));
                    if (metadata == null)
                    {
                        metadata = new LinkedRuntimeMetadata
                        {
                            Channel = resolution.Target.Channel,
                            RuntimePath = runtimePath
                        };
                        state.LinkedRuntimes.Add(metadata);
                    }
                    metadata.Health = "Invalid";
                    metadata.IsStale = true;
                    metadata.UpdatedAtUtc = DateTimeOffset.UtcNow;
                });
            }
            catch (Exception ex)
            {
                LogHelper.LogError(ex, "Linked Runtime failure metadata");
            }
        }

        private static LinkedRuntimeResult CreateResult(
            string runtimePath,
            LinkedRuntimeHealth health,
            RuntimeMarker marker,
            int skippedFileCount,
            bool reconciled) => new()
        {
            RuntimePath = runtimePath,
            Health = health,
            LinkedFileCount = marker.Files.Count(file =>
                file.StorageKind == LinkedRuntimeStorageKind.HardLink),
            IndependentFileCount = marker.Files.Count(file =>
                file.StorageKind == LinkedRuntimeStorageKind.Independent),
            SkippedFileCount = skippedFileCount,
            Reconciled = reconciled
        };

        private static void LogRuntimeResult(
            SharedRootResolution resolution,
            LinkedRuntimeResult result,
            string action)
        {
            LogHelper.Log(
                $"Linked Runtime {action}: GameId={resolution.GameId} | " +
                $"SharedRoot={resolution.RootPath} | BaseChannel={resolution.Base.Channel} | " +
                $"TargetChannel={resolution.Target.Channel} | " +
                $"CompatibilityGroup={resolution.Target.ClientCompatibilityGroup} | " +
                $"Runtime={result.RuntimePath} | Status={result.Health} | " +
                $"HardLinks={result.LinkedFileCount} | " +
                $"Independent={result.IndependentFileCount} | " +
                $"Skipped={result.SkippedFileCount}");
        }

        private static bool ManifestFilesEqual(
            ServerGameManifestFile left,
            ServerGameManifestFile right) =>
            left != null && right != null &&
            left.Size == right.Size &&
            string.Equals(left.Md5, right.Md5,
                StringComparison.OrdinalIgnoreCase);

        private static bool CanReuseIndependentFile(
            string runtimeFile,
            RuntimeFileRecord oldRecord,
            ServerGameManifestFile targetFile)
        {
            if (oldRecord == null ||
                oldRecord.StorageKind != LinkedRuntimeStorageKind.Independent ||
                oldRecord.Size != targetFile.Size ||
                !string.Equals(oldRecord.Md5, targetFile.Md5,
                    StringComparison.OrdinalIgnoreCase) ||
                !File.Exists(runtimeFile))
            {
                return false;
            }

            var info = new FileInfo(runtimeFile);
            return info.Length == targetFile.Size &&
                   (oldRecord.LastWriteTimeUtcTicks == 0 ||
                    info.LastWriteTimeUtc.Ticks ==
                    oldRecord.LastWriteTimeUtcTicks);
        }

        private static void ValidateSourceFile(
            string sourcePath,
            ServerGameManifestFile manifestFile)
        {
            if (!IsUsableManifestSource(sourcePath, manifestFile))
            {
                throw new InvalidDataException(
                    $"Shared Root 文件缺失、大小错误或为重解析点：" +
                    manifestFile.RelativePath);
            }
        }

        internal static bool IsUsableManifestSource(
            string sourcePath,
            ServerGameManifestFile manifestFile)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || manifestFile == null)
                return false;

            try
            {
                var info = new FileInfo(sourcePath);
                return info.Exists &&
                       info.Length == manifestFile.Size &&
                       !info.Attributes.HasFlag(FileAttributes.ReparsePoint);
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        internal static LinkedRuntimeStorageKind ResolveStorageKindForSource(
            string sharedRootPath,
            LinkedRuntimePlanEntry planned)
        {
            ArgumentNullException.ThrowIfNull(planned);
            if (planned.StorageKind != LinkedRuntimeStorageKind.HardLink)
                return planned.StorageKind;

            if (planned.BaseFile == null ||
                string.IsNullOrWhiteSpace(sharedRootPath))
            {
                return LinkedRuntimeStorageKind.Independent;
            }

            var sourceFile = SafeCombine(
                sharedRootPath, planned.BaseFile.RelativePath);
            return IsUsableManifestSource(sourceFile, planned.BaseFile)
                ? LinkedRuntimeStorageKind.HardLink
                : LinkedRuntimeStorageKind.Independent;
        }

        private static async Task CopyFileAtomicAsync(
            string sourcePath,
            string destinationPath,
            CancellationToken cancellationToken)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            var tempPath = destinationPath + ".copy-" + Guid.NewGuid().ToString("N");
            try
            {
                await using (var source = new FileStream(
                                 sourcePath, FileMode.Open, FileAccess.Read,
                                 FileShare.Read, 1024 * 1024,
                                 FileOptions.Asynchronous |
                                 FileOptions.SequentialScan))
                await using (var target = new FileStream(
                                 tempPath, FileMode.CreateNew, FileAccess.Write,
                                 FileShare.None, 1024 * 1024,
                                 FileOptions.Asynchronous |
                                 FileOptions.SequentialScan))
                {
                    await source.CopyToAsync(target, cancellationToken)
                        .ConfigureAwait(false);
                    await target.FlushAsync(cancellationToken)
                        .ConfigureAwait(false);
                }
                File.Move(tempPath, destinationPath, overwrite: true);
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
        }

        private static async Task DownloadFileAtomicAsync(
            ServerGameManifest manifest,
            ServerGameManifestFile file,
            string destinationPath,
            CancellationToken cancellationToken)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            var tempPath = destinationPath + ".new-" + Guid.NewGuid().ToString("N");
            try
            {
                await ServerPayloadUpdater.DownloadGameManifestFileAsync(
                        manifest, file, tempPath,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                File.Move(tempPath, destinationPath, overwrite: true);
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
        }

        private static async Task<bool> HasExpectedMd5Async(
            string path,
            string expectedMd5,
            CancellationToken cancellationToken)
        {
            await using var stream = new FileStream(
                path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                1024 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            var hash = await MD5.HashDataAsync(stream, cancellationToken)
                .ConfigureAwait(false);
            return Convert.ToHexString(hash).Equals(
                expectedMd5, StringComparison.OrdinalIgnoreCase);
        }

        private static async Task<bool> HasExpectedSha256Async(
            string path,
            string expectedSha256,
            CancellationToken cancellationToken)
        {
            if (!IsHexHash(expectedSha256, 64)) return false;
            await using var stream = new FileStream(
                path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                1024 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            var hash = await SHA256.HashDataAsync(stream, cancellationToken)
                .ConfigureAwait(false);
            return Convert.ToHexString(hash).Equals(
                expectedSha256, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsHexHash(string value, int length) =>
            !string.IsNullOrWhiteSpace(value) && value.Length == length &&
            value.All(Uri.IsHexDigit);

        private static bool IsSameFile(string left, string right) =>
            WindowsHardLink.AreSameFile(left, right);

        private static void WriteBytesAtomic(string destinationPath, byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                throw new InvalidDataException("目标渠道 Manifest 为空。");
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            var tempPath = destinationPath + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllBytes(tempPath, bytes);
                File.Move(tempPath, destinationPath, overwrite: true);
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
        }

        private static void DeleteFileIfExists(string path)
        {
            if (!File.Exists(path)) return;
            File.Delete(path);
        }

        private static void DeleteInternalDirectory(
            string path,
            string expectedParent)
        {
            var normalizedParent = SharedRootManager.NormalizeRootPath(
                expectedParent);
            var normalizedPath = SharedRootManager.NormalizeRootPath(path);
            var prefix = normalizedParent + Path.DirectorySeparatorChar;
            if (!normalizedPath.StartsWith(prefix,
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalizedPath, normalizedParent,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Refusing to delete a directory outside Linked Runtime storage.");
            }

            DeleteDirectoryTree(normalizedPath);
        }

        private static void CleanupOrphanedTransactionDirectories(
            string runtimePath)
        {
            var runtimeParent = Path.GetDirectoryName(runtimePath);
            if (string.IsNullOrWhiteSpace(runtimeParent) ||
                !Directory.Exists(runtimeParent))
            {
                return;
            }

            string[] directories;
            try
            {
                directories = Directory.GetDirectories(
                    runtimeParent, "*", SearchOption.TopDirectoryOnly);
            }
            catch (Exception ex)
            {
                LogHelper.LogError(
                    ex, $"Linked Runtime orphan enumeration: {runtimeParent}");
                return;
            }

            foreach (var directory in directories)
            {
                var name = Path.GetFileName(directory);
                var isCurrentTransactionDirectory =
                    string.Equals(name, StagingDirectoryName,
                        StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, PreviousDirectoryName,
                        StringComparison.OrdinalIgnoreCase);
                var isLegacyTransactionDirectory =
                    name.StartsWith(".staging-", StringComparison.OrdinalIgnoreCase) ||
                    name.StartsWith(".invalid-", StringComparison.OrdinalIgnoreCase);
                if (!isCurrentTransactionDirectory &&
                    !isLegacyTransactionDirectory)
                {
                    continue;
                }

                try
                {
                    DeleteInternalDirectory(directory, runtimeParent);
                    LogHelper.Log(
                        $"Removed orphaned Linked Runtime transaction directory: {directory}");
                }
                catch (Exception ex)
                {
                    // Orphan cleanup is maintenance only. An undeletable stale
                    // directory must not prevent a valid runtime from launching
                    // or force the traditional-switch fallback.
                    LogHelper.LogError(
                        ex, $"Linked Runtime orphan cleanup: {directory}");
                }
            }
        }

        private static void DeleteDirectoryTree(string path)
        {
            if (!Directory.Exists(path)) return;
            var attributes = File.GetAttributes(path);
            if (attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                Directory.Delete(path);
                return;
            }

            foreach (var file in Directory.EnumerateFiles(path))
                DeleteFileIfExists(file);
            foreach (var directory in Directory.EnumerateDirectories(path))
                DeleteDirectoryTree(directory);
            Directory.Delete(path);
        }

        private static string SafeCombine(string rootPath, string relativePath)
        {
            if (Path.IsPathRooted(relativePath))
                throw new InvalidDataException(
                    $"Runtime manifest contains rooted path: {relativePath}");
            var root = SharedRootManager.NormalizeRootPath(rootPath);
            var candidate = Path.GetFullPath(Path.Combine(
                root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            var prefix = root.EndsWith(Path.DirectorySeparatorChar)
                ? root
                : root + Path.DirectorySeparatorChar;
            if (!candidate.StartsWith(prefix,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Runtime manifest path escapes root: {relativePath}");
            }
            return candidate;
        }

        private static string NormalizeRelativePath(string path) =>
            (path ?? "").Replace('\\', '/').TrimStart('/');

        private static string SanitizePathSegment(string value)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var chars = (value ?? "Unknown")
                .Select(character => invalid.Contains(character)
                    ? '_'
                    : character)
                .ToArray();
            var result = new string(chars).Trim();
            return string.IsNullOrWhiteSpace(result) ? "Unknown" : result;
        }

        private sealed class RuntimeMarker
        {
            public int SchemaVersion { get; set; }
            public string GameId { get; set; } = "";
            public string SharedRootPath { get; set; } = "";
            public string SharedRootId { get; set; } = "";
            public string CompatibilityGroup { get; set; } = "";
            public string BaseChannel { get; set; } = "";
            public string TargetChannel { get; set; } = "";
            public string BaseVersion { get; set; } = "";
            public string TargetVersion { get; set; } = "";
            public string BaseManifestSha256 { get; set; } = "";
            public string TargetManifestSha256 { get; set; } = "";
            public string PayloadManifestSha256 { get; set; } = "";
            public DateTimeOffset UpdatedAtUtc { get; set; }
            public List<RuntimeFileRecord> Files { get; set; } = new();
        }

        private sealed class RuntimeFileRecord
        {
            public string RelativePath { get; set; } = "";
            public long Size { get; set; }
            public string Md5 { get; set; } = "";
            public long SourceSize { get; set; }
            public string SourceMd5 { get; set; } = "";
            public LinkedRuntimeStorageKind StorageKind { get; set; }
            public bool IsAuxiliary { get; set; }
            public long LastWriteTimeUtcTicks { get; set; }
        }

        private sealed class ArknightsResourceManifest
        {
            public string ManifestName { get; set; } = "";
            public List<ArknightsResourceEntry> AbInfos { get; set; } = new();
        }

        private sealed class ArknightsResourceEntry
        {
            public string Name { get; set; } = "";
            public string Md5 { get; set; } = "";
            public long AbSize { get; set; }
        }

        private sealed class ReconcileCounters
        {
            public int Changed { get; set; }
            public int Removed { get; set; }
            public int Skipped { get; set; }
            public int SourceFallbackDownloads { get; set; }
            public int AuxiliaryLinked { get; set; }
            public int AuxiliarySkipped { get; set; }
            public int PersistentMetadataSeeded { get; set; }
        }
    }
}
