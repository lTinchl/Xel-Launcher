using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Hi3Helper.Hypergryph.Core.Utils;
using XelLauncher.Models;

namespace XelLauncher.Helpers
{
    public enum SharedRootMode
    {
        Independent,
        Shared,
        Conflict
    }

    public sealed class SharedRootResolution
    {
        public SharedRootMode Mode { get; init; }
        public string GameId { get; init; } = "";
        public string RootPath { get; init; } = "";
        public GameChannelDefinition Target { get; init; }
        public GameChannelDefinition Base { get; init; }
        public SharedRootState State { get; init; }
        public IReadOnlyList<GameChannelDefinition> ConfiguredChannels { get; init; } =
            Array.Empty<GameChannelDefinition>();
    }

    public sealed class GameOperationTarget
    {
        public string RequestedIconName { get; init; } = "";
        public string EffectiveIconName { get; init; } = "";
        public string RootPath { get; init; } = "";
        public bool IsSharedRoot { get; init; }
        public SharedRootResolution SharedRoot { get; init; }
    }

    /// <summary>
    /// Resolves physical shared installations. Compatibility is deliberately
    /// evaluated separately from identity: a SharedRootState key contains only
    /// GameId and the normalized physical RootPath.
    /// </summary>
    public static class SharedRootManager
    {
        public const string UnknownChannel = "Unknown";

        public static SharedRootResolution Resolve(
            AppConfig config,
            string targetIconName,
            string rootPath,
            bool detectBaseChannel,
            out bool configChanged)
        {
            ArgumentNullException.ThrowIfNull(config);
            configChanged = false;

            var target = GameChannelCatalog.Get(targetIconName);
            if (target == null || string.IsNullOrWhiteSpace(rootPath))
            {
                return new SharedRootResolution
                {
                    Mode = SharedRootMode.Independent,
                    Target = target,
                    RootPath = rootPath ?? "",
                    GameId = target?.GameId ?? ""
                };
            }

            var normalizedRoot = NormalizeRootPath(rootPath);
            var configured = config.Games
                .Where(entry => !string.IsNullOrWhiteSpace(entry?.RootPath))
                .Select(entry => new
                {
                    Entry = entry,
                    Definition = GameChannelCatalog.Get(entry.IconName)
                })
                .Where(item => item.Definition != null &&
                               string.Equals(item.Definition.GameId, target.GameId,
                                   StringComparison.OrdinalIgnoreCase) &&
                               PathsEqual(item.Entry.RootPath, normalizedRoot))
                .GroupBy(item => item.Definition.IconName,
                    StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First().Definition)
                .ToArray();

            if (configured.Length < 2)
            {
                return new SharedRootResolution
                {
                    Mode = SharedRootMode.Independent,
                    GameId = target.GameId,
                    RootPath = normalizedRoot,
                    Target = target,
                    ConfiguredChannels = configured
                };
            }

            var hasConflict = string.IsNullOrWhiteSpace(
                                  target.ClientCompatibilityGroup) ||
                              configured.Any(channel =>
                                  string.IsNullOrWhiteSpace(
                                      channel.ClientCompatibilityGroup) ||
                                  !string.Equals(
                                      channel.ClientCompatibilityGroup,
                                      target.ClientCompatibilityGroup,
                                      StringComparison.OrdinalIgnoreCase));
            if (hasConflict)
            {
                return new SharedRootResolution
                {
                    Mode = SharedRootMode.Conflict,
                    GameId = target.GameId,
                    RootPath = normalizedRoot,
                    Target = target,
                    ConfiguredChannels = configured
                };
            }

            config.SharedRoots ??= new();
            var state = FindState(config, target.GameId, normalizedRoot);
            if (state == null)
            {
                state = new SharedRootState
                {
                    GameId = target.GameId,
                    RootPath = normalizedRoot,
                    BaseChannel = UnknownChannel,
                    UpdatedAtUtc = DateTimeOffset.UtcNow
                };
                config.SharedRoots.Add(state);
                configChanged = true;
            }
            else if (!string.Equals(state.RootPath, normalizedRoot,
                         StringComparison.Ordinal))
            {
                state.RootPath = normalizedRoot;
                configChanged = true;
            }

            state.LinkedRuntimes ??= new();
            if (detectBaseChannel && Directory.Exists(normalizedRoot))
            {
                var detected = DetectBaseChannel(
                    target.GameId,
                    target.ClientCompatibilityGroup,
                    normalizedRoot);
                var detectedVersion = ReadInstalledVersion(normalizedRoot);
                if (!string.Equals(detected, UnknownChannel,
                        StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(state.BaseChannel, detected,
                        StringComparison.OrdinalIgnoreCase))
                {
                    var previous = state.BaseChannel;
                    state.BaseChannel = detected;
                    state.BaseVersion = detectedVersion;
                    state.UpdatedAtUtc = DateTimeOffset.UtcNow;
                    MarkRuntimesStale(state, "base-channel-detected-change");
                    configChanged = true;
                    LogHelper.Log(
                        $"Shared root BaseChannel detected: GameId={state.GameId} | " +
                        $"SharedRoot={state.RootPath} | Previous={previous} | " +
                        $"BaseChannel={state.BaseChannel}");
                }
                else if (!string.IsNullOrWhiteSpace(detectedVersion) &&
                         !string.Equals(state.BaseVersion, detectedVersion,
                             StringComparison.OrdinalIgnoreCase))
                {
                    var previousVersion = state.BaseVersion;
                    state.BaseVersion = detectedVersion;
                    state.UpdatedAtUtc = DateTimeOffset.UtcNow;
                    if (!string.IsNullOrWhiteSpace(previousVersion))
                        MarkRuntimesStale(state, "base-version-detected-change");
                    configChanged = true;
                }
            }

            var baseDefinition = GameChannelCatalog.GetByGameAndChannel(
                target.GameId, state.BaseChannel);
            if (baseDefinition?.PayloadProfile == null ||
                !string.Equals(
                    baseDefinition.ClientCompatibilityGroup,
                    target.ClientCompatibilityGroup,
                    StringComparison.OrdinalIgnoreCase))
            {
                baseDefinition = null;
            }
            return new SharedRootResolution
            {
                Mode = SharedRootMode.Shared,
                GameId = target.GameId,
                RootPath = normalizedRoot,
                Target = target,
                Base = baseDefinition,
                State = state,
                ConfiguredChannels = configured
            };
        }

        public static SharedRootResolution ResolveAndPersist(
            string targetIconName,
            string rootPath,
            bool detectBaseChannel = true)
        {
            var config = ConfigHelper.Load();
            var result = Resolve(
                config, targetIconName, rootPath, detectBaseChannel,
                out var changed);
            if (!changed) return result;

            // Re-run the resolution inside ConfigHelper's cross-process
            // read/modify/write transaction. Saving the snapshot loaded above
            // could otherwise overwrite a BaseChannel or Stale flag recorded by
            // another page while channel detection was running.
            SharedRootResolution persisted = null;
            ConfigHelper.Update(current =>
            {
                persisted = Resolve(
                    current, targetIconName, rootPath, detectBaseChannel,
                    out _);
            });
            return persisted ?? result;
        }

        public static GameOperationTarget ResolveMaintenanceTarget(
            string requestedIconName,
            string rootPath,
            bool allowUninstalledTarget = true)
        {
            var resolution = ResolveAndPersist(
                requestedIconName, rootPath, detectBaseChannel: true);
            if (resolution.Mode == SharedRootMode.Conflict)
            {
                throw new InvalidOperationException(
                    "同一游戏目录配置了不兼容的渠道，无法建立共享运行环境或执行渠道切换。请为这些渠道设置不同目录。");
            }

            if (resolution.Mode != SharedRootMode.Shared)
            {
                return new GameOperationTarget
                {
                    RequestedIconName = requestedIconName,
                    EffectiveIconName = requestedIconName,
                    RootPath = NormalizeRootPath(rootPath),
                    IsSharedRoot = false,
                    SharedRoot = resolution
                };
            }

            if (resolution.Base == null)
            {
                if (allowUninstalledTarget &&
                    !File.Exists(Path.Combine(
                        resolution.RootPath,
                        GetExecutableName(resolution.Target.Family))))
                {
                    return new GameOperationTarget
                    {
                        RequestedIconName = requestedIconName,
                        EffectiveIconName = requestedIconName,
                        RootPath = resolution.RootPath,
                        IsSharedRoot = true,
                        SharedRoot = resolution
                    };
                }

                throw new InvalidOperationException(
                    "无法识别共享游戏目录当前的真实渠道。为避免使用错误渠道清单修改客户端，已停止本次安装、更新或校验。");
            }

            return new GameOperationTarget
            {
                RequestedIconName = requestedIconName,
                EffectiveIconName = resolution.Base.IconName,
                RootPath = resolution.RootPath,
                IsSharedRoot = true,
                SharedRoot = resolution
            };
        }

        public static string DetectBaseChannel(
            string gameId,
            string compatibilityGroup,
            string rootPath)
        {
            if (string.IsNullOrWhiteSpace(gameId) ||
                string.IsNullOrWhiteSpace(compatibilityGroup) ||
                string.IsNullOrWhiteSpace(rootPath) ||
                !Directory.Exists(rootPath))
            {
                return UnknownChannel;
            }

            var candidates = GameChannelCatalog.Channels
                .Where(channel => channel.PayloadProfile != null &&
                                  string.Equals(channel.GameId, gameId,
                                      StringComparison.OrdinalIgnoreCase) &&
                                  string.Equals(channel.ClientCompatibilityGroup,
                                      compatibilityGroup,
                                      StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (candidates.Length == 0) return UnknownChannel;

            var requiredSets = candidates.ToDictionary(
                channel => channel.IconName,
                channel => new HashSet<string>(
                    channel.PayloadProfile.RequiredFiles.Select(NormalizeRelativePath),
                    StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);

            var scored = candidates.Select(candidate =>
                new
                {
                    Candidate = candidate,
                    Score = ScoreChannelSignature(
                        candidate, candidates, requiredSets, rootPath)
                })
                .OrderByDescending(item => item.Score)
                .ToArray();
            if (scored.Length == 0 || scored[0].Score <= 0)
                return UnknownChannel;
            if (scored.Length > 1 && scored[0].Score == scored[1].Score)
                return UnknownChannel;

            return scored[0].Candidate.Channel;
        }

        public static void RecordBaseChannel(
            string targetIconName,
            string rootPath,
            string reason,
            string version = null)
        {
            var target = GameChannelCatalog.Get(targetIconName);
            if (target == null || string.IsNullOrWhiteSpace(rootPath)) return;
            var normalized = NormalizeRootPath(rootPath);

            ConfigHelper.Update(config =>
            {
                var resolution = Resolve(
                    config, targetIconName, normalized,
                    detectBaseChannel: false, out _);
                var state = resolution.State ??
                            FindState(config, target.GameId, normalized);
                if (resolution.Mode != SharedRootMode.Shared && state == null)
                    return;
                if (resolution.Mode == SharedRootMode.Conflict)
                    throw new InvalidOperationException(
                        "同一游戏目录包含不兼容渠道，不能记录切服结果。");

                state ??= new SharedRootState
                {
                    GameId = target.GameId,
                    RootPath = normalized
                };
                if (!config.SharedRoots.Contains(state))
                    config.SharedRoots.Add(state);

                var previous = state.BaseChannel;
                var changed = !string.Equals(previous, target.Channel,
                    StringComparison.OrdinalIgnoreCase);
                state.BaseChannel = target.Channel;
                state.BaseVersion = !string.IsNullOrWhiteSpace(version)
                    ? version
                    : ReadInstalledVersion(normalized);
                state.UpdatedAtUtc = DateTimeOffset.UtcNow;
                if (changed) MarkRuntimesStale(state, "base-channel-changed");

                LogHelper.Log(
                    $"Shared root BaseChannel changed: GameId={target.GameId} | " +
                    $"SharedRoot={normalized} | Previous={previous} | " +
                    $"BaseChannel={target.Channel} | Reason={reason}");
            });
        }

        public static SharedRootState FindState(
            AppConfig config,
            string gameId,
            string rootPath)
        {
            if (config?.SharedRoots == null ||
                string.IsNullOrWhiteSpace(gameId) ||
                string.IsNullOrWhiteSpace(rootPath))
            {
                return null;
            }

            return config.SharedRoots.FirstOrDefault(state =>
                string.Equals(state.GameId, gameId,
                    StringComparison.OrdinalIgnoreCase) &&
                PathsEqual(state.RootPath, rootPath));
        }

        public static void MarkRuntimesStale(
            SharedRootState state,
            string reason)
        {
            if (state?.LinkedRuntimes == null) return;
            foreach (var runtime in state.LinkedRuntimes)
            {
                runtime.IsStale = true;
                runtime.Health = "Stale";
            }

            if (state.LinkedRuntimes.Count > 0)
            {
                LogHelper.Log(
                    $"Linked runtimes marked stale: GameId={state.GameId} | " +
                    $"SharedRoot={state.RootPath} | Count={state.LinkedRuntimes.Count} | " +
                    $"Reason={reason}");
            }
        }

        public static string GetSharedRootId(string gameId, string rootPath)
        {
            var identity = (gameId ?? "").Trim().ToUpperInvariant() + "\n" +
                           NormalizeRootPath(rootPath).ToUpperInvariant();
            return Convert.ToHexString(SHA256.HashData(
                    Encoding.UTF8.GetBytes(identity)))
                .ToLowerInvariant()[..24];
        }

        public static string NormalizeRootPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return "";
            var fullPath = Path.GetFullPath(path);
            var root = Path.GetPathRoot(fullPath);
            if (!string.IsNullOrEmpty(root) &&
                string.Equals(
                    fullPath.TrimEnd(Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar),
                    root.TrimEnd(Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase))
            {
                return root;
            }

            return fullPath.TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        public static bool PathsEqual(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left) ||
                string.IsNullOrWhiteSpace(right)) return false;
            try
            {
                return string.Equals(
                    NormalizeRootPath(left), NormalizeRootPath(right),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        public static string ReadInstalledVersion(string rootPath)
        {
            try
            {
                var configPath = Path.Combine(rootPath, "config.ini");
                if (!File.Exists(configPath)) return "";
                return ConfigTool.ParseVersion(ConfigTool.ReadConfig(configPath)) ?? "";
            }
            catch
            {
                return "";
            }
        }

        private static int ScoreChannelSignature(
            GameChannelDefinition candidate,
            IReadOnlyList<GameChannelDefinition> candidates,
            IReadOnlyDictionary<string, HashSet<string>> requiredSets,
            string rootPath)
        {
            var required = requiredSets[candidate.IconName];
            // A server-payload transaction that was interrupted by a process
            // crash can leave only part of the target channel on disk. Unique
            // SDK files carry a high discrimination score, but they must never
            // make an incomplete client look like a valid BaseChannel. Keeping
            // the result Unknown forces launch to use the transactional copy
            // switch fallback, which can safely finish the deployment.
            if (required.Any(path => !File.Exists(SafeCombine(rootPath, path))))
                return int.MinValue;

            var otherRequired = new HashSet<string>(
                candidates
                    .Where(other => !string.Equals(other.IconName,
                        candidate.IconName, StringComparison.OrdinalIgnoreCase))
                    .SelectMany(other => requiredSets[other.IconName]),
                StringComparer.OrdinalIgnoreCase);
            var exclusive = required.Where(path => !otherRequired.Contains(path))
                .ToArray();

            var score = 0;
            foreach (var path in required)
            {
                score++;
            }
            foreach (var path in exclusive)
            {
                if (File.Exists(SafeCombine(rootPath, path))) score += 12;
            }

            foreach (var other in candidates.Where(other =>
                         !string.Equals(other.IconName, candidate.IconName,
                             StringComparison.OrdinalIgnoreCase)))
            {
                var foreignExclusive = requiredSets[other.IconName]
                    .Where(path => !required.Contains(path));
                foreach (var path in foreignExclusive)
                {
                    if (File.Exists(SafeCombine(rootPath, path))) score -= 5;
                }
            }

            var payloadRoot = ServerPayloadUpdater.GetPayloadDirectory(
                candidate.IconName);
            if (Directory.Exists(payloadRoot))
            {
                foreach (var path in required)
                {
                    var installed = SafeCombine(rootPath, path);
                    var payload = SafeCombine(payloadRoot, path);
                    if (!File.Exists(installed) || !File.Exists(payload)) continue;
                    try
                    {
                        if (new FileInfo(installed).Length ==
                            new FileInfo(payload).Length)
                        {
                            score += 2;
                        }
                    }
                    catch
                    {
                        // Signature detection is best effort.
                    }
                }
            }

            return score;
        }

        private static string SafeCombine(string rootPath, string relativePath)
        {
            var root = NormalizeRootPath(rootPath);
            var candidate = Path.GetFullPath(Path.Combine(
                root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            var prefix = root.EndsWith(Path.DirectorySeparatorChar)
                ? root
                : root + Path.DirectorySeparatorChar;
            if (!candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(
                    $"Channel signature path escapes root: {relativePath}");
            return candidate;
        }

        private static string NormalizeRelativePath(string path) =>
            path.Replace('\\', '/').TrimStart('/');

        private static string GetExecutableName(GameFamily family) =>
            family == GameFamily.Endfield ? "Endfield.exe" : "Arknights.exe";
    }
}
