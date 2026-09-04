using System;
using System.Collections.Generic;

namespace XelLauncher.Models
{
    /// <summary>
    /// Identifies one physical installation. The identity is always
    /// GameId + normalized RootPath; compatibility groups belong to channels,
    /// not to the installation itself.
    /// </summary>
    public sealed class SharedRootState
    {
        public string GameId { get; set; } = "";
        public string RootPath { get; set; } = "";
        public string BaseChannel { get; set; } = "Unknown";
        public string BaseVersion { get; set; } = "";
        public DateTimeOffset UpdatedAtUtc { get; set; }
        public List<LinkedRuntimeMetadata> LinkedRuntimes { get; set; } = new();
    }

    /// <summary>
    /// Lightweight config metadata. The per-file manifest is stored beside the
    /// derived runtime so the main config does not grow with the game manifest.
    /// </summary>
    public sealed class LinkedRuntimeMetadata
    {
        public string Channel { get; set; } = "";
        public string RuntimePath { get; set; } = "";
        public string CompatibilityGroup { get; set; } = "";
        public string BaseChannel { get; set; } = "";
        public string BaseManifestSha256 { get; set; } = "";
        public string TargetManifestSha256 { get; set; } = "";
        public string BaseVersion { get; set; } = "";
        public string TargetVersion { get; set; } = "";
        public string PayloadManifestSha256 { get; set; } = "";
        public string Health { get; set; } = "Invalid";
        public bool IsStale { get; set; } = true;
        public int LinkedFileCount { get; set; }
        public int IndependentFileCount { get; set; }
        public int SkippedFileCount { get; set; }
        public DateTimeOffset UpdatedAtUtc { get; set; }
    }
}
