using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Hi3Helper.Hypergryph.Core.Utils;
using Microsoft.Win32.SafeHandles;
using XelLauncher.Models;

namespace XelLauncher.Helpers
{
    public enum ArknightsLinkedClientStage
    {
        Validating,
        FetchingManifests,
        VerifyingSource,
        LinkingFiles,
        RepairingTarget,
        VerifyingTarget,
        Finalizing,
        Detaching,
        Completed
    }

    public sealed class ArknightsLinkedClientProgress
    {
        public ArknightsLinkedClientStage Stage { get; init; }
        public string CurrentFile { get; init; } = "";
        public int FileIndex { get; init; }
        public int FileCount { get; init; }
        public long ProcessedBytes { get; init; }
        public long TotalBytes { get; init; }
    }

    public sealed class ArknightsLinkedClientResult
    {
        public string SourcePath { get; init; } = "";
        public string TargetPath { get; init; } = "";
        public string TargetVersion { get; init; } = "";
        public string LinkedClientGroupId { get; init; } = "";
        public int LinkedFileCount { get; init; }
        public int CopiedFileCount { get; init; }
        public long SharedBytes { get; init; }
        public IReadOnlyList<string> LinkedFiles { get; init; } =
            Array.Empty<string>();
    }

    public sealed class ArknightsLinkedClientDetachResult
    {
        public int DetachedFileCount { get; init; }
        public long DetachedBytes { get; init; }
    }

    public static class ArknightsLinkedClientService
    {
        private const string OfficialIconName = "Arknights";
        private const string BilibiliIconName = "BiliArknights";
        private const string ExecutableName = "Arknights.exe";
        private const string DataDirectoryName = "Arknights_Data";
        private const string ManifestFileName = "game_files";
        private const string MarkerFileName = ".xel-linked-client.json";
        private const int MarkerSchemaVersion = 1;
        private const string PendingPhasePreparing = "Preparing";
        private const string PendingPhaseBuilding = "Building";
        private const string PendingPhaseReadyToCommit = "ReadyToCommit";
        private const string PendingPhaseRollingBack = "RollingBack";
        private const string DetachPhaseDetaching = "Detaching";
        private const string DetachPhaseFilesDetached = "FilesDetached";
        private static readonly SemaphoreSlim OperationGate = new(1, 1);
        private static readonly JsonSerializerOptions MarkerJsonOptions = new()
        {
            WriteIndented = true
        };

        public static bool IsOperationActive => OperationGate.CurrentCount == 0;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetVolumePathName(
            string fileName,
            StringBuilder volumePathName,
            uint bufferLength);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetVolumeNameForVolumeMountPoint(
            string volumeMountPoint,
            StringBuilder volumeName,
            uint bufferLength);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetVolumeInformation(
            string rootPathName,
            StringBuilder volumeNameBuffer,
            uint volumeNameSize,
            out uint volumeSerialNumber,
            out uint maximumComponentLength,
            out uint fileSystemFlags,
            StringBuilder fileSystemNameBuffer,
            uint fileSystemNameSize);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetDiskFreeSpaceEx(
            string directoryName,
            out ulong freeBytesAvailable,
            out ulong totalNumberOfBytes,
            out ulong totalNumberOfFreeBytes);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetFileInformationByHandle(
            SafeFileHandle file,
            out ByHandleFileInformation information);

        public static async Task<ArknightsLinkedClientResult> CreateBilibiliClientAsync(
            string officialPath,
            string bilibiliPath,
            IProgress<ArknightsLinkedClientProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            IDisposable operationLease = null;
            await OperationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                Report(progress, ArknightsLinkedClientStage.Validating);
                var sourcePath = NormalizeDirectoryPath(officialPath);
                var targetPath = NormalizeDirectoryPath(bilibiliPath);
                ValidatePaths(sourcePath, targetPath);
                var initialConfig = ConfigHelper.Load();
                var expectedBilibiliPath = CaptureCreationState(
                    initialConfig, sourcePath);
                var operationPaths = new List<string> { sourcePath, targetPath };
                if (!string.IsNullOrWhiteSpace(expectedBilibiliPath))
                    operationPaths.Add(expectedBilibiliPath);
                if (!LinkedClientOperationCoordinator.TryAcquirePaths(
                        operationPaths, out operationLease))
                {
                    throw new InvalidOperationException(
                        Localize("App.LinkedClient.Error.GroupBusy",
                            "关联客户端正在执行更新、修复或共享操作，请稍后重试"));
                }
                ValidateCreationState(
                    ConfigHelper.Load(), sourcePath, expectedBilibiliPath);
                EnsureNoManagedMutation(sourcePath);
                if (!string.IsNullOrWhiteSpace(expectedBilibiliPath) &&
                    !LinkedClientPolicy.AreSamePath(
                        expectedBilibiliPath, sourcePath))
                {
                    EnsureNoManagedMutation(expectedBilibiliPath);
                }
                await GameLauncher.KillArknightsProcesses(false)
                    .ConfigureAwait(false);

                cancellationToken.ThrowIfCancellationRequested();
                Report(progress, ArknightsLinkedClientStage.FetchingManifests);
                var officialManifestTask = ServerPayloadUpdater.GetGameManifestAsync(
                    OfficialIconName, cancellationToken);
                var bilibiliManifestTask = ServerPayloadUpdater.GetGameManifestAsync(
                    BilibiliIconName, cancellationToken);
                await Task.WhenAll(officialManifestTask, bilibiliManifestTask)
                    .ConfigureAwait(false);

                var officialManifest = await officialManifestTask.ConfigureAwait(false);
                var bilibiliManifest = await bilibiliManifestTask.ConfigureAwait(false);
                ValidateSourceVersion(sourcePath, officialManifest.Version);

                var targetFiles = bilibiliManifest.Files.ToDictionary(
                    x => x.RelativePath, StringComparer.OrdinalIgnoreCase);
                var sourceFiles = officialManifest.Files
                    .Where(ShouldVerifySourceFile)
                    .Where(x => IsSameManifestFile(x, targetFiles))
                    .OrderBy(x => x.RelativePath, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                var totalVerifyBytes = sourceFiles.Sum(x => x.Size);

                foreach (var manifestFile in sourceFiles)
                {
                    ValidateRegularSourceFile(
                        SafeCombine(sourcePath, manifestFile.RelativePath),
                        manifestFile);
                }

                var hardLinkCandidates = sourceFiles
                    .Where(file => CanHardLinkSource(
                        sourcePath, file, targetFiles))
                    .Select(file => file.RelativePath)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (hardLinkCandidates.Count == 0)
                {
                    throw new InvalidDataException(
                        Localize("App.LinkedClient.Error.NoSharedFiles",
                            "官服与B服清单中没有可安全共享的相同资源文件"));
                }

                var potentialSharedBytes = sourceFiles
                    .Where(file => hardLinkCandidates.Contains(file.RelativePath))
                    .Sum(file => file.Size);
                var targetManifestBytes = bilibiliManifest.Files.Sum(file => file.Size);
                var requiredCreationBytes = Math.Max(
                    0, targetManifestBytes - potentialSharedBytes) +
                    bilibiliManifest.EncryptedManifest.LongLength;
                EnsureFreeSpace(targetPath, requiredCreationBytes, detaching: false);

                var targetParent = Path.GetDirectoryName(targetPath) ??
                                   throw new InvalidOperationException(
                                       Localize("App.LinkedClient.Error.TargetRoot",
                                           "不能将磁盘根目录用作硬链接客户端目录"));
                var stagingPath = Path.Combine(
                    targetParent,
                    $".{Path.GetFileName(targetPath)}.xel-linking-{Guid.NewGuid():N}");

                var linkedFileCount = 0;
                var copiedFileCount = 0;
                long sharedBytes = 0;
                long processedBytes = 0;
                var linkedRelativePaths = new List<string>();
                var stagingOwned = false;
                var pendingRegistrationStarted = false;
                var pendingGroupId = "";
                var sourceMarkerWritten = false;
                var stagingMarkerWritten = false;
                var targetCommitted = false;
                LinkedClientPendingOperation pendingOperation = null;
                var transactionGroupId = Guid.NewGuid().ToString("N");

                try
                {
                    var buildingResult = new ArknightsLinkedClientResult
                    {
                        SourcePath = sourcePath,
                        TargetPath = targetPath,
                        TargetVersion = bilibiliManifest.Version,
                        LinkedClientGroupId = transactionGroupId,
                        LinkedFiles = hardLinkCandidates
                            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                            .ToArray()
                    };
                    pendingOperation = BeginLinkedClientRegistration(
                        buildingResult,
                        stagingPath,
                        expectedBilibiliPath,
                        PendingPhasePreparing);
                    pendingRegistrationStarted = true;
                    pendingGroupId = transactionGroupId;

                    Directory.CreateDirectory(stagingPath);
                    stagingOwned = true;
                    WriteLinkedClientMarker(
                        stagingPath,
                        buildingResult,
                        pendingOperation,
                        role: BilibiliIconName,
                        phase: PendingPhasePreparing,
                        allowSameTransactionOverwrite: false);
                    stagingMarkerWritten = true;
                    WriteLinkedClientMarker(
                        sourcePath,
                        buildingResult,
                        pendingOperation,
                        role: OfficialIconName,
                        phase: PendingPhasePreparing,
                        allowSameTransactionOverwrite: false);
                    sourceMarkerWritten = true;
                    pendingOperation = MarkPendingPhase(
                        pendingOperation,
                        PendingPhaseBuilding);

                    for (var index = 0; index < sourceFiles.Length; index++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var manifestFile = sourceFiles[index];
                        var sourceFile = SafeCombine(sourcePath, manifestFile.RelativePath);
                        ValidateRegularSourceFile(sourceFile, manifestFile);

                        var actualMd5 = await ComputeMd5Async(sourceFile, cancellationToken)
                            .ConfigureAwait(false);
                        if (!string.Equals(actualMd5, manifestFile.Md5,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            throw new InvalidDataException(string.Format(
                                Localize("App.LinkedClient.Error.SourceHash",
                                    "官服文件校验失败：{0}。请先修复官服客户端"),
                                manifestFile.RelativePath));
                        }

                        processedBytes += manifestFile.Size;
                        Report(progress, ArknightsLinkedClientStage.VerifyingSource,
                            manifestFile.RelativePath, index + 1, sourceFiles.Length,
                            processedBytes, totalVerifyBytes);

                        var stagedPath = SafeCombine(stagingPath, manifestFile.RelativePath);
                        Directory.CreateDirectory(Path.GetDirectoryName(stagedPath)!);
                        if (hardLinkCandidates.Contains(manifestFile.RelativePath) &&
                            CanHardLinkSource(sourcePath, manifestFile, targetFiles))
                        {
                            if (!WindowsHardLink.TryCreate(
                                    stagedPath, sourceFile, out var errorCode))
                                throw new Win32Exception(errorCode);

                            linkedFileCount++;
                            linkedRelativePaths.Add(manifestFile.RelativePath);
                            sharedBytes += manifestFile.Size;
                        }
                        else
                        {
                            File.Copy(sourceFile, stagedPath, overwrite: false);
                            ClearDeletionBlockingAttributes(stagedPath);
                            copiedFileCount++;
                        }

                        Report(progress, ArknightsLinkedClientStage.LinkingFiles,
                            manifestFile.RelativePath, index + 1, sourceFiles.Length,
                            processedBytes, totalVerifyBytes);
                    }

                    if (linkedFileCount == 0)
                    {
                        throw new InvalidDataException(
                            Localize("App.LinkedClient.Error.NoSharedFiles",
                                "官服与B服清单中没有可安全共享的相同资源文件"));
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    await CreateBootstrapFilesAsync(
                            stagingPath, bilibiliManifest, cancellationToken)
                        .ConfigureAwait(false);

                    Report(progress, ArknightsLinkedClientStage.RepairingTarget);
                    using (var repairService = new EndfieldService(BilibiliIconName))
                    {
                        await repairService.RepairAsync(
                                stagingPath,
                                (_, downloaded, total) =>
                                    Report(progress,
                                        ArknightsLinkedClientStage.RepairingTarget,
                                        processedBytes: downloaded,
                                        totalBytes: total),
                                cancellationToken)
                            .ConfigureAwait(false);
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    await VerifyTargetAsync(
                            stagingPath, bilibiliManifest, progress, cancellationToken)
                        .ConfigureAwait(false);
                    ValidateSharedLinks(
                        sourcePath, stagingPath, linkedRelativePaths);
                    using (var statusService = new EndfieldService(BilibiliIconName))
                    {
                        var status = await statusService.CheckStatusAsync(
                                stagingPath, cancellationToken)
                            .ConfigureAwait(false);
                        if (status == null || !status.IsInstalled || status.HasUpdate ||
                            !string.Equals(status.LocalVersion, bilibiliManifest.Version,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            throw new InvalidDataException(
                                Localize("App.LinkedClient.Error.TargetVerify",
                                    "B服客户端最终校验失败，未写入游戏设置"));
                        }
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    Report(progress, ArknightsLinkedClientStage.Finalizing);
                    var result = new ArknightsLinkedClientResult
                    {
                        SourcePath = sourcePath,
                        TargetPath = targetPath,
                        TargetVersion = bilibiliManifest.Version,
                        LinkedClientGroupId = transactionGroupId,
                        LinkedFileCount = linkedFileCount,
                        CopiedFileCount = copiedFileCount,
                        SharedBytes = sharedBytes,
                        LinkedFiles = linkedRelativePaths.ToArray()
                    };
                    WriteLinkedClientMarker(
                        sourcePath,
                        result,
                        pendingOperation,
                        role: OfficialIconName,
                        phase: PendingPhaseReadyToCommit,
                        allowSameTransactionOverwrite: true);
                    WriteLinkedClientMarker(
                        stagingPath,
                        result,
                        pendingOperation,
                        role: BilibiliIconName,
                        phase: PendingPhaseReadyToCommit,
                        allowSameTransactionOverwrite: true);
                    pendingOperation = MarkPendingPhase(
                        pendingOperation,
                        PendingPhaseReadyToCommit);

                    EnsureTargetStillEmpty(targetPath);
                    if (Directory.Exists(targetPath))
                        Directory.Delete(targetPath, recursive: false);
                    Directory.Move(stagingPath, targetPath);
                    stagingOwned = false;
                    targetCommitted = true;
                    try
                    {
                        RegisterLinkedClient(result, expectedBilibiliPath);
                    }
                    catch (Exception registrationError)
                    {
                        try
                        {
                            EnsureRollbackTargetOwned(
                                targetPath,
                                pendingOperation,
                                bilibiliManifest,
                                result.LinkedFiles);
                            Directory.Move(targetPath, stagingPath);
                            stagingOwned = true;
                            targetCommitted = false;
                        }
                        catch (Exception rollbackError)
                        {
                            throw new AggregateException(
                                "Linked client registration and rollback both failed.",
                                registrationError,
                                rollbackError);
                        }

                        throw;
                    }

                    Report(progress, ArknightsLinkedClientStage.Completed);
                    return result;
                }
                finally
                {
                    var rollbackJournaled = false;
                    if (pendingRegistrationStarted && !targetCommitted)
                    {
                        try
                        {
                            pendingOperation = MarkPendingRollback(
                                pendingOperation);
                            rollbackJournaled = true;
                        }
                        catch (Exception ex)
                        {
                            LogHelper.LogError(ex,
                                "Journal linked-client rollback");
                        }
                    }

                    var rollbackOwnedStaging = rollbackJournaled && stagingOwned;
                    var stagingRemoved = false;
                    if (rollbackOwnedStaging)
                    {
                        stagingRemoved = stagingMarkerWritten
                            ? TryDeleteOwnedStagingDirectory(pendingOperation)
                            : TryDeleteOwnedMarkerTemps(
                                  stagingPath,
                                  pendingOperation,
                                  BilibiliIconName) &&
                              TryDeleteEmptyUnmarkedStagingDirectory(
                                  stagingPath, pendingOperation);
                    }
                    else if (rollbackJournaled &&
                             !sourceMarkerWritten &&
                             !Directory.Exists(stagingPath))
                    {
                        stagingRemoved = true;
                    }

                    if (rollbackJournaled &&
                        stagingRemoved &&
                        (!sourceMarkerWritten ||
                         TryDeleteLinkedClientMarker(
                             sourcePath, pendingGroupId)) &&
                        TryDeleteOwnedMarkerTemps(
                            sourcePath,
                            pendingOperation,
                            OfficialIconName))
                    {
                        ClearPendingRegistration(pendingGroupId);
                    }
                }
            }
            finally
            {
                operationLease?.Dispose();
                OperationGate.Release();
            }
        }

        public static async Task<ArknightsLinkedClientDetachResult> DetachSharedFilesAsync(
            string clientPath,
            IProgress<ArknightsLinkedClientProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            IDisposable operationLease = null;
            await OperationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var rootPath = NormalizeDirectoryPath(clientPath);
                if (!Directory.Exists(rootPath))
                    throw new DirectoryNotFoundException(rootPath);

                EnsureNoReparsePointAncestors(rootPath);
                var operationPaths = new List<string> { rootPath };
                var initialConfig = ConfigHelper.Load();
                var pendingDetach = initialConfig.PendingLinkedClientDetach;
                if (pendingDetach != null &&
                    LinkedClientPolicy.AreSamePath(
                        pendingDetach.TargetPath, rootPath))
                {
                    operationPaths.Add(pendingDetach.SourcePath);
                }
                else if (TryReadLinkedClientMarker(
                             rootPath, out var initialMarker) &&
                         !string.IsNullOrWhiteSpace(initialMarker.PeerPath))
                {
                    operationPaths.Add(initialMarker.PeerPath);
                }

                if (!LinkedClientOperationCoordinator.TryAcquirePaths(
                        operationPaths, out operationLease))
                {
                    throw new InvalidOperationException(
                        Localize("App.LinkedClient.Error.GroupBusy",
                            "关联客户端正在执行更新、修复或共享操作，请稍后重试"));
                }
                EnsureNoManagedMutation(rootPath);
                var detachOperation = BeginOrResumeDetach(rootPath);
                EnsureNoManagedMutation(detachOperation.SourcePath);
                await GameLauncher.KillArknightsProcesses(false)
                    .ConfigureAwait(false);
                var files = detachOperation.LinkedFiles
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Select(relativePath =>
                    {
                        var targetFile = SafeCombine(rootPath, relativePath);
                        var sourceFile = SafeCombine(
                            detachOperation.SourcePath, relativePath);
                        var targetInfo = new FileInfo(targetFile);
                        var sourceInfo = new FileInfo(sourceFile);
                        if (targetInfo.Exists &&
                            targetInfo.Attributes.HasFlag(
                                FileAttributes.ReparsePoint) ||
                            sourceInfo.Exists &&
                            sourceInfo.Attributes.HasFlag(
                                FileAttributes.ReparsePoint))
                        {
                            throw new InvalidDataException(
                                $"A linked-client path is a reparse point: {relativePath}");
                        }

                        if (!targetInfo.Exists || !sourceInfo.Exists)
                            return null;

                        var targetIdentity = GetFileIdentity(targetFile);
                        var sourceIdentity = GetFileIdentity(sourceFile);
                        if (!AreSameFileIdentity(
                                sourceIdentity, targetIdentity))
                        {
                            return null;
                        }
                        if (targetIdentity.NumberOfLinks <= 1)
                            return null;

                        return new DetachFile(
                            relativePath,
                            sourceFile,
                            targetFile,
                            targetInfo.Length,
                            targetInfo.Attributes);
                    })
                    .Where(file => file != null)
                    .OrderBy(file => file.TargetPath,
                        StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                var requiredBytes = files.Sum(x => x.Length);
                EnsureFreeSpace(rootPath, requiredBytes, detaching: true);

                long detachedBytes = 0;
                for (var index = 0; index < files.Length; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var file = files[index];
                    var tempPath = Path.Combine(
                        Path.GetDirectoryName(file.TargetPath)!,
                        $".{Path.GetFileName(file.TargetPath)}.xel-detach-{Guid.NewGuid():N}");
                    try
                    {
                        File.Copy(file.TargetPath, tempPath, overwrite: false);
                        ClearDeletionBlockingAttributes(tempPath);
                        var copiedInfo = new FileInfo(tempPath);
                        if (copiedInfo.Length != file.Length ||
                            !string.Equals(
                                await ComputeMd5Async(file.TargetPath, cancellationToken)
                                    .ConfigureAwait(false),
                                await ComputeMd5Async(tempPath, cancellationToken)
                                    .ConfigureAwait(false),
                                StringComparison.OrdinalIgnoreCase))
                        {
                            throw new InvalidDataException(
                                $"Detached copy verification failed: {file.TargetPath}");
                        }

                        var safeAttributes = file.OriginalAttributes &
                                             ~(FileAttributes.ReadOnly |
                                               FileAttributes.System);
                        if (safeAttributes != file.OriginalAttributes)
                            File.SetAttributes(file.TargetPath, safeAttributes);
                        try
                        {
                            File.Replace(
                                tempPath,
                                file.TargetPath,
                                null,
                                ignoreMetadataErrors: true);
                        }
                        finally
                        {
                            if (File.Exists(file.SourcePath))
                            {
                                File.SetAttributes(
                                    file.SourcePath,
                                    file.OriginalAttributes);
                            }
                            if (File.Exists(file.TargetPath))
                            {
                                File.SetAttributes(
                                    file.TargetPath,
                                    file.OriginalAttributes);
                            }
                        }

                        if (GetHardLinkCount(file.TargetPath) > 1 &&
                            AreSameFileIdentity(
                                GetFileIdentity(file.SourcePath),
                                GetFileIdentity(file.TargetPath)))
                        {
                            throw new IOException(
                                $"The file is still shared after detaching: {file.TargetPath}");
                        }

                        detachedBytes += file.Length;
                        Report(progress, ArknightsLinkedClientStage.Detaching,
                            file.RelativePath,
                            index + 1, files.Length, detachedBytes, requiredBytes);
                    }
                    finally
                    {
                        if (File.Exists(tempPath)) File.Delete(tempPath);
                    }
                }

                ValidateNoSharedLinks(detachOperation);
                detachOperation = MarkDetachFilesDetached(detachOperation);
                ValidateNoSharedLinks(detachOperation);
                if (!TryDeleteLinkedClientMarker(
                        detachOperation.SourcePath,
                        detachOperation.GroupId) ||
                    !TryDeleteLinkedClientMarker(
                        detachOperation.TargetPath,
                        detachOperation.GroupId))
                {
                    throw new IOException(
                        "Unable to remove the linked-client markers after detaching.");
                }
                ClearPendingDetach(detachOperation);

                Report(progress, ArknightsLinkedClientStage.Completed,
                    fileIndex: files.Length, fileCount: files.Length,
                    processedBytes: detachedBytes, totalBytes: requiredBytes);
                return new ArknightsLinkedClientDetachResult
                {
                    DetachedFileCount = files.Length,
                    DetachedBytes = detachedBytes
                };
            }
            finally
            {
                operationLease?.Dispose();
                OperationGate.Release();
            }
        }

        private static void ValidatePaths(string sourcePath, string targetPath)
        {
            if (sourcePath.StartsWith(@"\\", StringComparison.Ordinal) ||
                targetPath.StartsWith(@"\\", StringComparison.Ordinal))
            {
                throw new IOException(
                    Localize("App.LinkedClient.Error.Volume",
                        "硬链接客户端不支持网络路径"));
            }

            if (!Directory.Exists(sourcePath) ||
                !File.Exists(Path.Combine(sourcePath, ExecutableName)) ||
                !File.Exists(Path.Combine(sourcePath, "config.ini")) ||
                !Directory.Exists(Path.Combine(sourcePath, DataDirectoryName)))
            {
                throw new DirectoryNotFoundException(
                    Localize("App.LinkedClient.Error.SourceMissing",
                        "未找到完整的明日方舟官服客户端"));
            }

            if (IsReparsePoint(sourcePath) ||
                IsReparsePoint(Path.Combine(sourcePath, DataDirectoryName)))
            {
                throw new IOException(
                    Localize("App.LinkedClient.Error.SourceLink",
                        "官服目录不能是符号链接、目录联接或其他重解析点"));
            }
            EnsureNoReparsePointAncestors(sourcePath);

            if (string.Equals(sourcePath, targetPath,
                    StringComparison.OrdinalIgnoreCase) ||
                IsPathInside(sourcePath, targetPath) ||
                IsPathInside(targetPath, sourcePath))
            {
                throw new IOException(
                    Localize("App.LinkedClient.Error.PathOverlap",
                        "官服与B服目录必须是互不包含的两个目录"));
            }

            if (string.Equals(
                    Path.GetPathRoot(targetPath)?.TrimEnd(
                        Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    targetPath.TrimEnd(
                        Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException(
                    Localize("App.LinkedClient.Error.TargetRoot",
                        "不能将磁盘根目录用作硬链接客户端目录"));
            }

            EnsureTargetStillEmpty(targetPath);
            if (Directory.Exists(targetPath) && IsReparsePoint(targetPath))
            {
                throw new IOException(
                    Localize("App.LinkedClient.Error.TargetLink",
                        "B服目标目录不能是符号链接、目录联接或其他重解析点"));
            }

            var targetProbe = Directory.Exists(targetPath)
                ? targetPath
                : FindExistingParent(targetPath);
            EnsureNoReparsePointAncestors(targetProbe);
            var sourceVolume = GetVolumeIdentity(sourcePath);
            var targetVolume = GetVolumeIdentity(targetProbe);
            if (!string.Equals(sourceVolume.FileSystem, "NTFS",
                    StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(targetVolume.FileSystem, "NTFS",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException(
                    Localize("App.LinkedClient.Error.Ntfs",
                        "硬链接客户端要求官服与B服目录都位于 NTFS 卷"));
            }

            if (!string.Equals(sourceVolume.VolumeName, targetVolume.VolumeName,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException(
                    Localize("App.LinkedClient.Error.Volume",
                        "官服与B服目录必须位于同一个磁盘分区"));
            }
        }

        private static void EnsureNoManagedMutation(string path)
        {
            if (GameUpdateManager.Find(path) == null &&
                !GameRepairManager.IsRepairing(path))
            {
                return;
            }

            throw new InvalidOperationException(
                Localize("App.LinkedClient.Error.GroupBusy",
                    "关联客户端正在执行更新、修复或共享操作，请稍后重试"));
        }

        public static bool HasLinkedClientMarker(string installPath)
        {
            if (string.IsNullOrWhiteSpace(installPath)) return false;
            try
            {
                var rootPath = NormalizeDirectoryPath(installPath);
                return File.Exists(Path.Combine(rootPath, MarkerFileName)) ||
                       Directory.Exists(rootPath) &&
                       Directory.EnumerateFiles(
                               rootPath,
                               MarkerFileName + ".tmp-*",
                               SearchOption.TopDirectoryOnly)
                           .Any();
            }
            catch
            {
                return false;
            }
        }

        public static bool IsPendingClient(
            AppConfig config,
            string iconName,
            string installPath)
        {
            var pending = config?.PendingLinkedClient;
            if (pending == null || string.IsNullOrWhiteSpace(installPath))
                return false;

            if (string.Equals(iconName, OfficialIconName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return LinkedClientPolicy.AreSamePath(
                    installPath, pending.SourcePath);
            }

            return string.Equals(iconName, BilibiliIconName,
                       StringComparison.OrdinalIgnoreCase) &&
                   LinkedClientPolicy.AreSamePath(
                       installPath, pending.TargetPath);
        }

        public static bool IsPendingDetachClient(
            AppConfig config,
            string iconName,
            string installPath)
        {
            var pending = config?.PendingLinkedClientDetach;
            if (pending == null || string.IsNullOrWhiteSpace(installPath))
                return false;

            if (string.Equals(iconName, OfficialIconName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return LinkedClientPolicy.AreSamePath(
                    installPath, pending.SourcePath);
            }

            return string.Equals(iconName, BilibiliIconName,
                       StringComparison.OrdinalIgnoreCase) &&
                   LinkedClientPolicy.AreSamePath(
                       installPath, pending.TargetPath);
        }

        public static AppConfig TryRecoverPendingRegistration(AppConfig config)
        {
            var pending = config?.PendingLinkedClient;
            if (pending == null || string.IsNullOrWhiteSpace(pending.GroupId))
                return config ?? new AppConfig();

            IDisposable recoveryLease = null;
            try
            {
                var paths = new List<string>
                {
                    pending.SourcePath,
                    pending.TargetPath
                };
                if (!string.IsNullOrWhiteSpace(pending.ExpectedBilibiliPath))
                    paths.Add(pending.ExpectedBilibiliPath);
                if (!LinkedClientOperationCoordinator.TryAcquirePaths(
                        paths, out recoveryLease))
                {
                    return config;
                }

                if (TryBuildRecoveredResult(pending, out var result))
                {
                    var recoveredConfig = ConfigHelper.Update(latest =>
                    {
                        if (!PendingOperationsMatch(
                                latest.PendingLinkedClient, pending))
                        {
                            throw CreationStateChanged();
                        }
                        ValidateRecoveryState(latest, pending);
                        ApplyLinkedClientRegistration(latest, result);
                        latest.PendingLinkedClient = null;
                    }, allowLinkedClientStateChange: true);
                    LogHelper.Log(
                        $"Recovered linked-client registration {pending.GroupId}.");
                    return recoveredConfig;
                }

                var targetMarkerPath = Path.Combine(
                    pending.TargetPath, MarkerFileName);
                if (File.Exists(targetMarkerPath))
                    return config;

                if (Directory.Exists(pending.TargetPath) &&
                    Directory.EnumerateFileSystemEntries(
                        pending.TargetPath).Any())
                {
                    return config;
                }

                var originalPhase = pending.Phase;
                var stagingExists = Directory.Exists(pending.StagingPath);
                var sourceMarkerExists = File.Exists(Path.Combine(
                    pending.SourcePath, MarkerFileName));
                LinkedClientMarker sourceMarker = null;
                var sourceMarkerValid = sourceMarkerExists &&
                                        TryReadLinkedClientMarker(
                                            pending.SourcePath,
                                            out sourceMarker) &&
                                        MarkerOwnsTransaction(
                                            sourceMarker,
                                            pending,
                                            OfficialIconName) &&
                                        sourceMarker.LinkedFiles.Count > 0;

                if (!stagingExists &&
                    (sourceMarkerExists && !sourceMarkerValid ||
                     sourceMarkerValid &&
                     HasSourceFilesWithExtraLinks(
                         pending.SourcePath,
                         sourceMarker.LinkedFiles) ||
                     !sourceMarkerExists &&
                     !string.Equals(originalPhase, PendingPhasePreparing,
                         StringComparison.Ordinal) &&
                     !string.Equals(originalPhase, PendingPhaseRollingBack,
                         StringComparison.Ordinal)))
                {
                    return config;
                }

                if (!string.Equals(
                        pending.Phase,
                        PendingPhaseRollingBack,
                        StringComparison.Ordinal))
                {
                    pending = MarkPendingRollback(pending);
                }

                var stagingRemoved = !stagingExists;
                if (stagingExists)
                {
                    var stagingMarkerPath = Path.Combine(
                        pending.StagingPath, MarkerFileName);
                    if (File.Exists(stagingMarkerPath))
                    {
                        stagingRemoved = TryReadMarkerFile(
                                             stagingMarkerPath,
                                             out var stagingMarker) &&
                                         MarkerOwnsTransaction(
                                             stagingMarker,
                                             pending,
                                             BilibiliIconName) &&
                                         TryDeleteOwnedStagingDirectory(
                                             pending);
                    }
                    else
                    {
                        stagingRemoved = TryDeleteOwnedMarkerTemps(
                                             pending.StagingPath,
                                             pending,
                                             BilibiliIconName) &&
                                         TryDeleteEmptyUnmarkedStagingDirectory(
                                             pending.StagingPath, pending);
                    }
                }

                if (!stagingRemoved ||
                    sourceMarkerExists && !sourceMarkerValid ||
                    sourceMarkerValid &&
                    !TryDeleteLinkedClientMarker(
                        pending.SourcePath, pending.GroupId) ||
                    !TryDeleteOwnedMarkerTemps(
                        pending.SourcePath,
                        pending,
                        OfficialIconName))
                {
                    return config;
                }

                var rolledBackConfig = ConfigHelper.Update(latest =>
                {
                    if (!PendingOperationsMatch(
                            latest.PendingLinkedClient, pending))
                    {
                        throw CreationStateChanged();
                    }
                    latest.PendingLinkedClient = null;
                }, allowLinkedClientStateChange: true);
                LogHelper.Log(
                    $"Rolled back incomplete linked-client registration {pending.GroupId}.");
                return rolledBackConfig;
            }
            catch (Exception ex)
            {
                LogHelper.LogError(ex,
                    $"Recover linked-client registration {pending.GroupId}");
                return config;
            }
            finally
            {
                recoveryLease?.Dispose();
            }
        }

        private static string CaptureCreationState(
            AppConfig config,
            string sourcePath)
        {
            var official = FindChannelEntry(config, OfficialIconName);
            var bilibili = FindChannelEntry(config, BilibiliIconName);
            if (official == null || bilibili == null ||
                !LinkedClientPolicy.AreSamePath(official.RootPath, sourcePath) ||
                !string.IsNullOrWhiteSpace(official.LinkedClientGroupId) ||
                !string.IsNullOrWhiteSpace(bilibili.LinkedClientGroupId) ||
                config.PendingLinkedClient != null ||
                config.PendingLinkedClientDetach != null ||
                HasLinkedClientMarker(sourcePath) ||
                (!string.IsNullOrWhiteSpace(bilibili.RootPath) &&
                 HasLinkedClientMarker(bilibili.RootPath)))
            {
                throw CreationStateChanged();
            }

            return bilibili.RootPath ?? "";
        }

        private static void ValidateCreationState(
            AppConfig config,
            string sourcePath,
            string expectedBilibiliPath)
        {
            var actualBilibiliPath = CaptureCreationState(config, sourcePath);
            if (!AreSameConfiguredPath(
                    actualBilibiliPath, expectedBilibiliPath))
            {
                throw CreationStateChanged();
            }

        }

        private static void ValidateCommittedMarkers(
            ArknightsLinkedClientResult result,
            LinkedClientPendingOperation pending)
        {
            if (!TryReadLinkedClientMarker(
                    result.SourcePath, out var sourceMarker) ||
                !TryReadLinkedClientMarker(
                    result.TargetPath, out var targetMarker) ||
                !MarkerMatches(sourceMarker, pending, OfficialIconName) ||
                !MarkerMatches(targetMarker, pending, BilibiliIconName) ||
                sourceMarker.LinkedFiles.Count == 0 ||
                !sourceMarker.LinkedFiles.SequenceEqual(
                    targetMarker.LinkedFiles,
                    StringComparer.OrdinalIgnoreCase) ||
                !sourceMarker.LinkedFiles.SequenceEqual(
                    result.LinkedFiles,
                    StringComparer.OrdinalIgnoreCase))
            {
                throw CreationStateChanged();
            }

            ValidateSharedLinks(
                result.SourcePath,
                result.TargetPath,
                sourceMarker.LinkedFiles);
        }

        private static LinkedClientPendingOperation BeginLinkedClientRegistration(
            ArknightsLinkedClientResult result,
            string stagingPath,
            string expectedBilibiliPath,
            string phase)
        {
            LinkedClientPendingOperation pending = null;
            ConfigHelper.Update(config =>
            {
                ValidateCreationState(
                    config, result.SourcePath, expectedBilibiliPath);
                pending = new LinkedClientPendingOperation
                {
                    GroupId = result.LinkedClientGroupId,
                    SourcePath = result.SourcePath,
                    TargetPath = result.TargetPath,
                    StagingPath = stagingPath,
                    TargetVersion = result.TargetVersion,
                    ExpectedBilibiliPath = expectedBilibiliPath ?? "",
                    Phase = phase
                };
                config.PendingLinkedClient = pending;
            }, allowLinkedClientStateChange: true);
            return pending;
        }

        private static LinkedClientPendingOperation MarkPendingPhase(
            LinkedClientPendingOperation expected,
            string nextPhase)
        {
            LinkedClientPendingOperation updatedPending = null;
            ConfigHelper.Update(config =>
            {
                if (!PendingOperationsMatch(
                        config.PendingLinkedClient, expected) ||
                    !IsValidPendingPhaseTransition(
                        expected.Phase, nextPhase))
                {
                    throw CreationStateChanged();
                }

                config.PendingLinkedClient.Phase = nextPhase;
                updatedPending = config.PendingLinkedClient;
            }, allowLinkedClientStateChange: true);
            return updatedPending;
        }

        private static LinkedClientPendingOperation MarkPendingRollback(
            LinkedClientPendingOperation expected)
        {
            if (expected == null) throw CreationStateChanged();
            if (string.Equals(expected.Phase, PendingPhaseRollingBack,
                    StringComparison.Ordinal))
            {
                return expected;
            }

            LinkedClientPendingOperation updatedPending = null;
            ConfigHelper.Update(config =>
            {
                if (!PendingOperationsMatch(
                        config.PendingLinkedClient, expected) ||
                    !string.Equals(expected.Phase, PendingPhasePreparing,
                        StringComparison.Ordinal) &&
                    !string.Equals(expected.Phase, PendingPhaseBuilding,
                        StringComparison.Ordinal) &&
                    !string.Equals(expected.Phase, PendingPhaseReadyToCommit,
                        StringComparison.Ordinal))
                {
                    throw CreationStateChanged();
                }

                config.PendingLinkedClient.Phase = PendingPhaseRollingBack;
                updatedPending = config.PendingLinkedClient;
            }, allowLinkedClientStateChange: true);
            return updatedPending;
        }

        private static bool IsValidPendingPhaseTransition(
            string currentPhase,
            string nextPhase) =>
            string.Equals(currentPhase, PendingPhasePreparing,
                StringComparison.Ordinal) &&
            string.Equals(nextPhase, PendingPhaseBuilding,
                StringComparison.Ordinal) ||
            string.Equals(currentPhase, PendingPhaseBuilding,
                StringComparison.Ordinal) &&
            string.Equals(nextPhase, PendingPhaseReadyToCommit,
                StringComparison.Ordinal);

        private static void RegisterLinkedClient(
            ArknightsLinkedClientResult result,
            string expectedBilibiliPath)
        {
            ConfigHelper.Update(config =>
            {
                ValidateRegistrationState(
                    config, result, expectedBilibiliPath);
                ApplyLinkedClientRegistration(config, result);
                config.PendingLinkedClient = null;
            }, allowLinkedClientStateChange: true);
        }

        private static void ValidateRegistrationState(
            AppConfig config,
            ArknightsLinkedClientResult result,
            string expectedBilibiliPath)
        {
            var official = FindChannelEntry(config, OfficialIconName);
            var bilibili = FindChannelEntry(config, BilibiliIconName);
            var pending = config.PendingLinkedClient;
            if (official == null || bilibili == null || pending == null ||
                !LinkedClientPolicy.AreSamePath(
                    official.RootPath, result.SourcePath) ||
                !AreSameConfiguredPath(
                    bilibili.RootPath, expectedBilibiliPath) ||
                !string.IsNullOrWhiteSpace(official.LinkedClientGroupId) ||
                !string.IsNullOrWhiteSpace(bilibili.LinkedClientGroupId) ||
                !PendingMatches(pending, result))
            {
                throw CreationStateChanged();
            }

            ValidateCommittedMarkers(result, pending);
        }

        private static void ValidateRecoveryState(
            AppConfig config,
            LinkedClientPendingOperation pending)
        {
            var official = FindChannelEntry(config, OfficialIconName);
            var bilibili = FindChannelEntry(config, BilibiliIconName);
            if (official == null || bilibili == null ||
                !LinkedClientPolicy.AreSamePath(
                    official.RootPath, pending.SourcePath) ||
                !AreSameConfiguredPath(
                    bilibili.RootPath, pending.ExpectedBilibiliPath) ||
                !IsEmptyOrGroup(official.LinkedClientGroupId, pending.GroupId) ||
                !IsEmptyOrGroup(bilibili.LinkedClientGroupId, pending.GroupId))
            {
                throw CreationStateChanged();
            }
        }

        private static void ApplyLinkedClientRegistration(
            AppConfig config,
            ArknightsLinkedClientResult result)
        {
            var official = FindChannelEntry(config, OfficialIconName) ??
                           throw CreationStateChanged();
            var bilibili = FindChannelEntry(config, BilibiliIconName) ??
                           throw CreationStateChanged();
            official.IndependentChannelClient = true;
            official.LinkedClientGroupId = result.LinkedClientGroupId;
            bilibili.RootPath = result.TargetPath;
            bilibili.LocalVersion = result.TargetVersion;
            bilibili.IndependentChannelClient = true;
            bilibili.LinkedClientGroupId = result.LinkedClientGroupId;
            config.GameStatusCache[BilibiliIconName] = new CachedGameStatus
            {
                IsInstalled = true,
                HasUpdate = false,
                HasPreload = false,
                PreloadCompleted = false,
                LocalVersion = result.TargetVersion,
                RemoteVersion = result.TargetVersion,
                PreloadVersion = "",
                InstallPath = result.TargetPath,
            };
        }

        private static GameEntry FindChannelEntry(
            AppConfig config,
            string iconName) => config.Games.Find(g =>
            string.Equals(g.IconName, iconName,
                StringComparison.OrdinalIgnoreCase));

        private static InvalidOperationException CreationStateChanged() =>
            new(Localize("App.LinkedClient.Error.ConfigChanged",
                "创建期间游戏设置已发生变化，请重新开始"));

        private static bool AreSameConfiguredPath(string left, string right) =>
            string.IsNullOrWhiteSpace(left) && string.IsNullOrWhiteSpace(right) ||
            LinkedClientPolicy.AreSamePath(left, right);

        private static bool IsEmptyOrGroup(string value, string groupId) =>
            string.IsNullOrWhiteSpace(value) ||
            string.Equals(value, groupId, StringComparison.OrdinalIgnoreCase);

        private static bool PendingMatches(
            LinkedClientPendingOperation pending,
            ArknightsLinkedClientResult result) =>
            string.Equals(pending.GroupId, result.LinkedClientGroupId,
                StringComparison.OrdinalIgnoreCase) &&
            LinkedClientPolicy.AreSamePath(
                pending.SourcePath, result.SourcePath) &&
            LinkedClientPolicy.AreSamePath(
                pending.TargetPath, result.TargetPath) &&
            string.Equals(pending.Phase, PendingPhaseReadyToCommit,
                StringComparison.Ordinal) &&
            string.Equals(pending.TargetVersion, result.TargetVersion,
                StringComparison.OrdinalIgnoreCase);

        private static bool PendingOperationsMatch(
            LinkedClientPendingOperation left,
            LinkedClientPendingOperation right) =>
            left != null && right != null &&
            string.Equals(left.GroupId, right.GroupId,
                StringComparison.OrdinalIgnoreCase) &&
            LinkedClientPolicy.AreSamePath(left.SourcePath, right.SourcePath) &&
            LinkedClientPolicy.AreSamePath(left.TargetPath, right.TargetPath) &&
            LinkedClientPolicy.AreSamePath(left.StagingPath, right.StagingPath) &&
            AreSameConfiguredPath(
                left.ExpectedBilibiliPath, right.ExpectedBilibiliPath) &&
            string.Equals(left.Phase, right.Phase,
                StringComparison.Ordinal) &&
            string.Equals(left.TargetVersion, right.TargetVersion,
                StringComparison.OrdinalIgnoreCase);

        private static void ClearPendingRegistration(string groupId)
        {
            try
            {
                ConfigHelper.Update(config =>
                {
                    if (string.Equals(config.PendingLinkedClient?.GroupId, groupId,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        config.PendingLinkedClient = null;
                    }
                }, allowLinkedClientStateChange: true);
            }
            catch (Exception ex)
            {
                LogHelper.LogError(ex,
                    $"Clear pending linked-client registration {groupId}");
            }
        }

        private static void WriteLinkedClientMarker(
            string directoryPath,
            ArknightsLinkedClientResult result,
            LinkedClientPendingOperation pending,
            string role,
            string phase,
            bool allowSameTransactionOverwrite)
        {
            var marker = new LinkedClientMarker
            {
                SchemaVersion = MarkerSchemaVersion,
                GroupId = result.LinkedClientGroupId,
                Role = role,
                ThisPath = string.Equals(role, OfficialIconName,
                    StringComparison.OrdinalIgnoreCase)
                    ? result.SourcePath
                    : result.TargetPath,
                PeerPath = string.Equals(role, OfficialIconName,
                    StringComparison.OrdinalIgnoreCase)
                    ? result.TargetPath
                    : result.SourcePath,
                TargetVersion = result.TargetVersion,
                StagingPath = pending.StagingPath,
                Phase = phase,
                LinkedFiles = result.LinkedFiles.ToList()
            };
            var markerPath = Path.Combine(directoryPath, MarkerFileName);
            var tempPath = markerPath + ".tmp-" + pending.GroupId + "-" +
                           Guid.NewGuid().ToString("N");
            try
            {
                if (File.Exists(markerPath) &&
                    (!allowSameTransactionOverwrite ||
                     !TryReadMarkerFile(markerPath, out var existing) ||
                     !MarkerOwnsTransaction(existing, pending, role)))
                {
                    throw new InvalidOperationException(
                        "A different linked-client marker already exists.");
                }

                WriteTextDurable(
                    tempPath,
                    JsonSerializer.Serialize(marker, MarkerJsonOptions),
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                File.Move(
                    tempPath,
                    markerPath,
                    overwrite: allowSameTransactionOverwrite);
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
        }

        private static void WriteTextDurable(
            string path,
            string content,
            Encoding encoding)
        {
            var bytes = encoding.GetBytes(content);
            using var stream = new FileStream(
                path,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                64 * 1024,
                FileOptions.WriteThrough);
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush(flushToDisk: true);
        }

        private static bool TryReadLinkedClientMarker(
            string rootPath,
            out LinkedClientMarker marker)
        {
            marker = null;
            rootPath = NormalizeDirectoryPath(rootPath);
            if (!TryReadMarkerFile(
                    Path.Combine(rootPath, MarkerFileName), out var candidate))
            {
                return false;
            }

            if (candidate.SchemaVersion != MarkerSchemaVersion ||
                string.IsNullOrWhiteSpace(candidate.GroupId) ||
                !LinkedClientPolicy.AreSamePath(candidate.ThisPath, rootPath))
            {
                return false;
            }

            marker = candidate;
            return true;
        }

        private static bool TryReadMarkerFile(
            string markerPath,
            out LinkedClientMarker marker)
        {
            marker = null;
            try
            {
                if (!File.Exists(markerPath)) return false;
                marker = JsonSerializer.Deserialize<LinkedClientMarker>(
                    File.ReadAllText(markerPath));
                if (marker != null) marker.LinkedFiles ??= new List<string>();
                return marker != null;
            }
            catch (Exception ex)
            {
                LogHelper.LogError(ex, $"Read linked-client marker {markerPath}");
                marker = null;
                return false;
            }
        }

        private static bool TryDeleteLinkedClientMarker(
            string rootPath,
            string expectedGroupId)
        {
            try
            {
                rootPath = NormalizeDirectoryPath(rootPath);
                var markerPath = Path.Combine(rootPath, MarkerFileName);
                if (!File.Exists(markerPath)) return true;
                if (!TryReadMarkerFile(markerPath, out var marker) ||
                    !string.Equals(marker.GroupId, expectedGroupId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                File.Delete(markerPath);
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.LogError(ex, $"Delete linked-client marker {rootPath}");
                return false;
            }
        }

        private static bool TryDeleteOwnedMarkerTemps(
            string rootPath,
            LinkedClientPendingOperation pending,
            string role)
        {
            try
            {
                if (pending == null || string.IsNullOrWhiteSpace(rootPath) ||
                    string.IsNullOrWhiteSpace(pending.GroupId) ||
                    !Directory.Exists(rootPath))
                {
                    return pending != null;
                }

                rootPath = NormalizeDirectoryPath(rootPath);
                var prefix = MarkerFileName + ".tmp-" + pending.GroupId + "-";
                foreach (var path in Directory.EnumerateFiles(
                             rootPath, MarkerFileName + ".tmp-*",
                             SearchOption.TopDirectoryOnly))
                {
                    var name = Path.GetFileName(path);
                    if (!name.StartsWith(prefix, StringComparison.Ordinal) ||
                        name.Length != prefix.Length + 32 ||
                        !name.AsSpan(prefix.Length).ToString()
                            .All(Uri.IsHexDigit))
                    {
                        continue;
                    }

                    if (File.GetAttributes(path)
                            .HasFlag(FileAttributes.ReparsePoint))
                    {
                        return false;
                    }

                    if (TryReadMarkerFile(path, out var marker) &&
                        !MarkerOwnsTransaction(marker, pending, role))
                    {
                        return false;
                    }
                    File.Delete(path);
                }

                return true;
            }
            catch (Exception ex)
            {
                LogHelper.LogError(ex,
                    $"Delete linked-client marker temp files {rootPath}");
                return false;
            }
        }

        private static LinkedClientMarker CaptureDetachMarker(string rootPath)
        {
            if (!TryReadLinkedClientMarker(rootPath, out var targetMarker) ||
                !string.Equals(targetMarker.Role, BilibiliIconName,
                    StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(targetMarker.Phase, PendingPhaseReadyToCommit,
                    StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(targetMarker.PeerPath) ||
                targetMarker.LinkedFiles.Count == 0)
            {
                throw new InvalidDataException(
                    "The Bilibili linked-client marker is missing or invalid.");
            }

            var sourcePath = NormalizeDirectoryPath(targetMarker.PeerPath);
            if (!Directory.Exists(sourcePath) ||
                IsReparsePoint(sourcePath) ||
                !TryReadLinkedClientMarker(sourcePath, out var sourceMarker) ||
                !string.Equals(sourceMarker.Role, OfficialIconName,
                    StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(sourceMarker.Phase, PendingPhaseReadyToCommit,
                    StringComparison.Ordinal) ||
                !string.Equals(sourceMarker.GroupId, targetMarker.GroupId,
                    StringComparison.OrdinalIgnoreCase) ||
                !LinkedClientPolicy.AreSamePath(
                    sourceMarker.PeerPath, rootPath) ||
                !string.Equals(
                    sourceMarker.TargetVersion,
                    targetMarker.TargetVersion,
                    StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(
                    sourceMarker.StagingPath,
                    targetMarker.StagingPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "The official linked-client marker is missing or inconsistent.");
            }
            EnsureNoReparsePointAncestors(sourcePath);

            var targetFiles = NormalizeRelativePaths(
                rootPath, targetMarker.LinkedFiles);
            var sourceFiles = NormalizeRelativePaths(
                sourcePath, sourceMarker.LinkedFiles);
            if (targetFiles.Count == 0 ||
                !targetFiles.SetEquals(sourceFiles))
            {
                throw new InvalidDataException(
                    "The linked-client marker file lists are inconsistent.");
            }

            var config = ConfigHelper.Load();
            var official = FindChannelEntry(config, OfficialIconName);
            var bilibili = FindChannelEntry(config, BilibiliIconName);
            if (official == null || bilibili == null ||
                !LinkedClientPolicy.AreSamePath(
                    official.RootPath, sourcePath) ||
                !LinkedClientPolicy.AreSamePath(
                    bilibili.RootPath, rootPath) ||
                !string.Equals(
                    official.LinkedClientGroupId,
                    targetMarker.GroupId,
                    StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(
                    bilibili.LinkedClientGroupId,
                    targetMarker.GroupId,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "The linked-client markers do not match the saved client group.");
            }

            return targetMarker;
        }

        private static LinkedClientPendingDetachOperation BeginOrResumeDetach(
            string rootPath)
        {
            var config = ConfigHelper.Load();
            if (config.PendingLinkedClientDetach != null)
            {
                var resumed = ClonePendingDetach(
                    config.PendingLinkedClientDetach);
                ValidatePendingDetach(config, resumed, rootPath);
                return resumed;
            }

            var marker = CaptureDetachMarker(rootPath);
            var operation = new LinkedClientPendingDetachOperation
            {
                GroupId = marker.GroupId,
                SourcePath = NormalizeDirectoryPath(marker.PeerPath),
                TargetPath = NormalizeDirectoryPath(rootPath),
                LinkedFiles = NormalizeRelativePaths(
                        rootPath, marker.LinkedFiles)
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                Phase = DetachPhaseDetaching,
            };

            ConfigHelper.Update(latest =>
            {
                if (latest.PendingLinkedClient != null ||
                    latest.PendingLinkedClientDetach != null)
                {
                    throw CreationStateChanged();
                }
                ValidatePendingDetach(latest, operation, rootPath);
                latest.PendingLinkedClientDetach =
                    ClonePendingDetach(operation);
            }, allowLinkedClientStateChange: true);
            return operation;
        }

        private static LinkedClientPendingDetachOperation MarkDetachFilesDetached(
            LinkedClientPendingDetachOperation expected)
        {
            if (string.Equals(expected.Phase, DetachPhaseFilesDetached,
                    StringComparison.Ordinal))
            {
                return expected;
            }

            LinkedClientPendingDetachOperation updated = null;
            ConfigHelper.Update(config =>
            {
                if (!PendingDetachOperationsMatch(
                        config.PendingLinkedClientDetach, expected) ||
                    !string.Equals(expected.Phase, DetachPhaseDetaching,
                        StringComparison.Ordinal))
                {
                    throw CreationStateChanged();
                }

                var official = FindChannelEntry(config, OfficialIconName);
                var bilibili = FindChannelEntry(config, BilibiliIconName);
                if (official == null || bilibili == null ||
                    !LinkedClientPolicy.AreSamePath(
                        official.RootPath, expected.SourcePath) ||
                    !LinkedClientPolicy.AreSamePath(
                        bilibili.RootPath, expected.TargetPath) ||
                    !string.Equals(
                        official.LinkedClientGroupId,
                        expected.GroupId,
                        StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(
                        bilibili.LinkedClientGroupId,
                        expected.GroupId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw CreationStateChanged();
                }

                official.LinkedClientGroupId = "";
                bilibili.LinkedClientGroupId = "";
                config.PendingLinkedClientDetach.Phase =
                    DetachPhaseFilesDetached;
                updated = ClonePendingDetach(
                    config.PendingLinkedClientDetach);
            }, allowLinkedClientStateChange: true);
            return updated;
        }

        private static void ClearPendingDetach(
            LinkedClientPendingDetachOperation expected)
        {
            ConfigHelper.Update(config =>
            {
                if (!PendingDetachOperationsMatch(
                        config.PendingLinkedClientDetach, expected) ||
                    !string.Equals(expected.Phase, DetachPhaseFilesDetached,
                        StringComparison.Ordinal) ||
                    HasLinkedClientMarker(expected.SourcePath) ||
                    HasLinkedClientMarker(expected.TargetPath))
                {
                    throw CreationStateChanged();
                }
                config.PendingLinkedClientDetach = null;
            }, allowLinkedClientStateChange: true);
        }

        private static void ValidatePendingDetach(
            AppConfig config,
            LinkedClientPendingDetachOperation operation,
            string rootPath)
        {
            if (operation == null ||
                string.IsNullOrWhiteSpace(operation.GroupId) ||
                string.IsNullOrWhiteSpace(operation.SourcePath) ||
                string.IsNullOrWhiteSpace(operation.TargetPath) ||
                operation.LinkedFiles == null ||
                operation.LinkedFiles.Count == 0 ||
                !LinkedClientPolicy.AreSamePath(
                    operation.TargetPath, rootPath) ||
                !Directory.Exists(operation.SourcePath) ||
                !Directory.Exists(operation.TargetPath) ||
                IsReparsePoint(operation.SourcePath) ||
                IsReparsePoint(operation.TargetPath) ||
                !string.Equals(operation.Phase, DetachPhaseDetaching,
                    StringComparison.Ordinal) &&
                !string.Equals(operation.Phase, DetachPhaseFilesDetached,
                    StringComparison.Ordinal))
            {
                throw CreationStateChanged();
            }
            EnsureNoReparsePointAncestors(operation.SourcePath);
            EnsureNoReparsePointAncestors(operation.TargetPath);

            var normalizedFiles = NormalizeRelativePaths(
                operation.TargetPath, operation.LinkedFiles);
            if (normalizedFiles.Count != operation.LinkedFiles.Count)
                throw CreationStateChanged();

            var official = FindChannelEntry(config, OfficialIconName);
            var bilibili = FindChannelEntry(config, BilibiliIconName);
            if (official == null || bilibili == null ||
                !LinkedClientPolicy.AreSamePath(
                    official.RootPath, operation.SourcePath) ||
                !LinkedClientPolicy.AreSamePath(
                    bilibili.RootPath, operation.TargetPath))
            {
                throw CreationStateChanged();
            }

            if (string.Equals(operation.Phase, DetachPhaseDetaching,
                    StringComparison.Ordinal))
            {
                if (!string.Equals(
                        official.LinkedClientGroupId,
                        operation.GroupId,
                        StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(
                        bilibili.LinkedClientGroupId,
                        operation.GroupId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw CreationStateChanged();
                }

                ValidateDetachMarker(
                    operation.SourcePath,
                    operation,
                    OfficialIconName,
                    markerRequired: true);
                ValidateDetachMarker(
                    operation.TargetPath,
                    operation,
                    BilibiliIconName,
                    markerRequired: true);
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(
                        official.LinkedClientGroupId) ||
                    !string.IsNullOrWhiteSpace(
                        bilibili.LinkedClientGroupId))
                {
                    throw CreationStateChanged();
                }

                ValidateDetachMarker(
                    operation.SourcePath,
                    operation,
                    OfficialIconName,
                    markerRequired: false);
                ValidateDetachMarker(
                    operation.TargetPath,
                    operation,
                    BilibiliIconName,
                    markerRequired: false);
            }
        }

        private static void ValidateDetachMarker(
            string rootPath,
            LinkedClientPendingDetachOperation operation,
            string role,
            bool markerRequired)
        {
            var markerPath = Path.Combine(rootPath, MarkerFileName);
            if (!File.Exists(markerPath))
            {
                if (markerRequired) throw CreationStateChanged();
                return;
            }

            if (!TryReadLinkedClientMarker(rootPath, out var marker) ||
                !string.Equals(marker.GroupId, operation.GroupId,
                    StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(marker.Role, role,
                    StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(marker.Phase, PendingPhaseReadyToCommit,
                    StringComparison.Ordinal) ||
                !LinkedClientPolicy.AreSamePath(
                    marker.PeerPath,
                    string.Equals(role, OfficialIconName,
                        StringComparison.OrdinalIgnoreCase)
                        ? operation.TargetPath
                        : operation.SourcePath) ||
                !NormalizeRelativePaths(rootPath, marker.LinkedFiles)
                    .SetEquals(operation.LinkedFiles))
            {
                throw CreationStateChanged();
            }
        }

        private static void ValidateNoSharedLinks(
            LinkedClientPendingDetachOperation operation)
        {
            foreach (var relativePath in operation.LinkedFiles)
            {
                var sourcePath = SafeCombine(
                    operation.SourcePath, relativePath);
                var targetPath = SafeCombine(
                    operation.TargetPath, relativePath);
                if (!File.Exists(sourcePath) || !File.Exists(targetPath))
                    continue;
                if (File.GetAttributes(sourcePath)
                        .HasFlag(FileAttributes.ReparsePoint) ||
                    File.GetAttributes(targetPath)
                        .HasFlag(FileAttributes.ReparsePoint))
                {
                    throw CreationStateChanged();
                }

                if (AreSameFileIdentity(
                        GetFileIdentity(sourcePath),
                        GetFileIdentity(targetPath)))
                {
                    throw new IOException(
                        $"The file is still shared after detaching: {relativePath}");
                }
            }
        }

        private static bool PendingDetachOperationsMatch(
            LinkedClientPendingDetachOperation left,
            LinkedClientPendingDetachOperation right) =>
            left != null && right != null &&
            string.Equals(left.GroupId, right.GroupId,
                StringComparison.OrdinalIgnoreCase) &&
            LinkedClientPolicy.AreSamePath(
                left.SourcePath, right.SourcePath) &&
            LinkedClientPolicy.AreSamePath(
                left.TargetPath, right.TargetPath) &&
            string.Equals(left.Phase, right.Phase,
                StringComparison.Ordinal) &&
            new HashSet<string>(left.LinkedFiles ?? new(),
                    StringComparer.OrdinalIgnoreCase)
                .SetEquals(right.LinkedFiles ?? new());

        private static LinkedClientPendingDetachOperation ClonePendingDetach(
            LinkedClientPendingDetachOperation operation) => new()
        {
            GroupId = operation.GroupId,
            SourcePath = operation.SourcePath,
            TargetPath = operation.TargetPath,
            LinkedFiles = operation.LinkedFiles.ToList(),
            Phase = operation.Phase,
        };

        private static void RemoveLinkedClientMarkersAfterDetach(string rootPath)
        {
            if (!TryReadLinkedClientMarker(rootPath, out var marker)) return;

            var config = ConfigHelper.Load();
            var current = config.Games.FirstOrDefault(g =>
                LinkedClientPolicy.IsArknightsChannel(g.IconName) &&
                LinkedClientPolicy.AreSamePath(g.RootPath, rootPath));
            var peer = config.Games.FirstOrDefault(g =>
                !ReferenceEquals(g, current) &&
                string.Equals(g.LinkedClientGroupId, marker.GroupId,
                    StringComparison.OrdinalIgnoreCase));
            if (current == null || peer == null ||
                !string.Equals(current.LinkedClientGroupId, marker.GroupId,
                    StringComparison.OrdinalIgnoreCase) ||
                !LinkedClientPolicy.AreSamePath(
                    peer.RootPath, marker.PeerPath))
            {
                throw new InvalidDataException(
                    "The linked-client marker does not match the saved client group.");
            }

            if (!string.IsNullOrWhiteSpace(marker.PeerPath))
            {
                var peerPath = NormalizeDirectoryPath(marker.PeerPath);
                var peerMarkerPath = Path.Combine(
                    peerPath, MarkerFileName);
                if (File.Exists(peerMarkerPath) &&
                    (!TryReadMarkerFile(peerMarkerPath, out var peerMarker) ||
                     !string.Equals(peerMarker.GroupId, marker.GroupId,
                         StringComparison.OrdinalIgnoreCase)))
                {
                    throw new InvalidDataException(
                        "The linked-client peer marker is inconsistent.");
                }

                if (!TryDeleteLinkedClientMarker(
                        peerPath, marker.GroupId))
                {
                    throw new IOException(
                        "Unable to remove the linked-client peer marker.");
                }
            }

            if (!TryDeleteLinkedClientMarker(rootPath, marker.GroupId))
            {
                throw new IOException(
                    "Unable to remove the linked-client marker.");
            }
        }

        private static bool TryBuildRecoveredResult(
            LinkedClientPendingOperation pending,
            out ArknightsLinkedClientResult result)
        {
            result = null;
            if (!string.Equals(pending.Phase, PendingPhaseReadyToCommit,
                    StringComparison.Ordinal))
            {
                return false;
            }

            if (!TryReadLinkedClientMarker(
                    pending.SourcePath, out var sourceMarker) ||
                !TryReadLinkedClientMarker(
                    pending.TargetPath, out var targetMarker) ||
                !MarkerMatches(sourceMarker, pending, OfficialIconName) ||
                !MarkerMatches(targetMarker, pending, BilibiliIconName))
            {
                return false;
            }

            var sourceFiles = NormalizeRelativePaths(
                pending.SourcePath, sourceMarker.LinkedFiles);
            var targetFiles = NormalizeRelativePaths(
                pending.TargetPath, targetMarker.LinkedFiles);
            if (sourceFiles.Count == 0 ||
                sourceFiles.Count != sourceMarker.LinkedFiles.Count ||
                targetFiles.Count != targetMarker.LinkedFiles.Count ||
                !sourceFiles.SetEquals(targetFiles))
            {
                return false;
            }

            result = new ArknightsLinkedClientResult
            {
                SourcePath = pending.SourcePath,
                TargetPath = pending.TargetPath,
                TargetVersion = pending.TargetVersion,
                LinkedClientGroupId = pending.GroupId,
                LinkedFileCount = sourceFiles.Count,
                LinkedFiles = sourceFiles
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToArray()
            };
            return true;
        }

        private static bool MarkerMatches(
            LinkedClientMarker marker,
            LinkedClientPendingOperation pending,
            string role)
        {
            return MarkerOwnsTransaction(marker, pending, role) &&
                   string.Equals(marker.Phase, pending.Phase,
                       StringComparison.Ordinal);
        }

        private static bool MarkerOwnsTransaction(
            LinkedClientMarker marker,
            LinkedClientPendingOperation pending,
            string role)
        {
            var official = string.Equals(role, OfficialIconName,
                StringComparison.OrdinalIgnoreCase);
            return marker != null && pending != null &&
                   marker.SchemaVersion == MarkerSchemaVersion &&
                   string.Equals(marker.GroupId, pending.GroupId,
                       StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(marker.Role, role,
                       StringComparison.OrdinalIgnoreCase) &&
                   LinkedClientPolicy.AreSamePath(
                       marker.ThisPath,
                       official ? pending.SourcePath : pending.TargetPath) &&
                   LinkedClientPolicy.AreSamePath(
                       marker.PeerPath,
                       official ? pending.TargetPath : pending.SourcePath) &&
                    string.Equals(marker.TargetVersion, pending.TargetVersion,
                        StringComparison.OrdinalIgnoreCase) &&
                   LinkedClientPolicy.AreSamePath(
                       marker.StagingPath, pending.StagingPath);
        }

        private static void ValidateSourceVersion(
            string sourcePath,
            string expectedVersion)
        {
            var configPath = Path.Combine(sourcePath, "config.ini");
            var content = ConfigTool.ReadConfig(configPath);
            var version = ConfigTool.ParseVersion(content);
            if (string.IsNullOrWhiteSpace(version) ||
                !string.Equals(version, expectedVersion,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(string.Format(
                    Localize("App.LinkedClient.Error.SourceVersion",
                        "官服客户端不是最新版本（需要 {0}），请先更新并修复官服客户端"),
                    expectedVersion));
            }
        }

        private static bool ShouldVerifySourceFile(ServerGameManifestFile file)
        {
            var normalized = file.RelativePath.Replace('\\', '/');
            var fileName = Path.GetFileName(normalized);
            return !normalized.Equals("config.ini", StringComparison.OrdinalIgnoreCase) &&
                   !fileName.Equals("game_files", StringComparison.OrdinalIgnoreCase) &&
                   !fileName.StartsWith("game_files_", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSameManifestFile(
            ServerGameManifestFile source,
            IReadOnlyDictionary<string, ServerGameManifestFile> targetFiles) =>
            targetFiles.TryGetValue(source.RelativePath, out var target) &&
            target.Size == source.Size &&
            string.Equals(target.Md5, source.Md5, StringComparison.OrdinalIgnoreCase);

        private static bool CanShareWithTarget(
            ServerGameManifestFile source,
            IReadOnlyDictionary<string, ServerGameManifestFile> targetFiles)
        {
            var normalized = source.RelativePath.Replace('\\', '/');
            if (!normalized.StartsWith(DataDirectoryName + "/",
                    StringComparison.OrdinalIgnoreCase) ||
                IsMutableDataPath(normalized))
            {
                return false;
            }

            return targetFiles.TryGetValue(source.RelativePath, out var target) &&
                   target.Size == source.Size &&
                   string.Equals(target.Md5, source.Md5,
                        StringComparison.OrdinalIgnoreCase);
        }

        private static bool CanHardLinkSource(
            string sourceRoot,
            ServerGameManifestFile source,
            IReadOnlyDictionary<string, ServerGameManifestFile> targetFiles)
        {
            if (!CanShareWithTarget(source, targetFiles)) return false;

            var sourcePath = SafeCombine(sourceRoot, source.RelativePath);
            var attributes = File.GetAttributes(sourcePath);
            return !attributes.HasFlag(FileAttributes.ReadOnly) &&
                   !attributes.HasFlag(FileAttributes.System) &&
                   !attributes.HasFlag(FileAttributes.ReparsePoint) &&
                   GetHardLinkCount(sourcePath) == 1;
        }

        private static void ClearDeletionBlockingAttributes(string path)
        {
            var attributes = File.GetAttributes(path);
            var safeAttributes = attributes &
                                 ~(FileAttributes.ReadOnly | FileAttributes.System);
            if (safeAttributes != attributes)
                File.SetAttributes(path, safeAttributes);
        }

        private static bool IsMutableDataPath(string normalizedPath)
        {
            var segments = normalizedPath.Split('/',
                StringSplitOptions.RemoveEmptyEntries);
            if (segments.Any(x => x.Equals("Cache", StringComparison.OrdinalIgnoreCase) ||
                                  x.Equals("Caches", StringComparison.OrdinalIgnoreCase) ||
                                  x.Equals("Logs", StringComparison.OrdinalIgnoreCase) ||
                                  x.Equals("Temp", StringComparison.OrdinalIgnoreCase) ||
                                  x.Equals("Temporary", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            var extension = Path.GetExtension(normalizedPath);
            return extension.Equals(".log", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".tmp", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".lock", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".db", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".sqlite", StringComparison.OrdinalIgnoreCase);
        }

        private static void ValidateRegularSourceFile(
            string sourceFile,
            ServerGameManifestFile manifestFile)
        {
            var info = new FileInfo(sourceFile);
            if (!info.Exists || info.Length != manifestFile.Size ||
                info.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                throw new InvalidDataException(string.Format(
                    Localize("App.LinkedClient.Error.SourceFile",
                        "官服文件缺失或大小不正确：{0}。请先修复官服客户端"),
                    manifestFile.RelativePath));
            }
        }

        private static async Task<string> ComputeMd5Async(
            string path,
            CancellationToken cancellationToken)
        {
            await using var stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.Read,
                1024 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            var hash = await MD5.HashDataAsync(stream, cancellationToken)
                .ConfigureAwait(false);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private static async Task CreateBootstrapFilesAsync(
            string stagingPath,
            ServerGameManifest targetManifest,
            CancellationToken cancellationToken)
        {
            var executablePath = Path.Combine(stagingPath, ExecutableName);
            if (!File.Exists(executablePath))
            {
                var executable = FindManifestFile(targetManifest, ExecutableName);
                await ServerPayloadUpdater.DownloadGameManifestFileAsync(
                        targetManifest, executable, executablePath,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }

            var config = FindManifestFile(targetManifest, "config.ini");
            await ServerPayloadUpdater.DownloadGameManifestFileAsync(
                    targetManifest,
                    config,
                    Path.Combine(stagingPath, "config.ini"),
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (targetManifest.EncryptedManifest.Length == 0)
            {
                throw new InvalidDataException(
                    Localize("App.LinkedClient.Error.Config",
                        "无法读取B服游戏清单"));
            }

            await File.WriteAllBytesAsync(
                    Path.Combine(stagingPath, ManifestFileName),
                    targetManifest.EncryptedManifest,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        private static ServerGameManifestFile FindManifestFile(
            ServerGameManifest manifest,
            string relativePath) =>
            manifest.Files.FirstOrDefault(x =>
                string.Equals(
                    x.RelativePath.Replace('\\', '/'),
                    relativePath.Replace('\\', '/'),
                    StringComparison.OrdinalIgnoreCase)) ??
            throw new InvalidDataException(string.Format(
                Localize("App.LinkedClient.Error.SourceFile",
                    "渠道清单缺少必要文件：{0}"),
                relativePath));

        private static async Task VerifyTargetAsync(
            string stagingPath,
            ServerGameManifest manifest,
            IProgress<ArknightsLinkedClientProgress> progress,
            CancellationToken cancellationToken)
        {
            var files = manifest.Files
                .OrderBy(x => x.RelativePath, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var totalBytes = files.Sum(x => x.Size);
            long processedBytes = 0;

            for (var index = 0; index < files.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var manifestFile = files[index];
                var targetFile = SafeCombine(stagingPath, manifestFile.RelativePath);
                var info = new FileInfo(targetFile);
                if (!info.Exists || info.Length != manifestFile.Size ||
                    info.Attributes.HasFlag(FileAttributes.ReparsePoint) ||
                    !string.Equals(
                        await ComputeMd5Async(targetFile, cancellationToken)
                            .ConfigureAwait(false),
                        manifestFile.Md5,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(string.Format(
                        Localize("App.LinkedClient.Error.TargetFileVerify",
                            "B服客户端最终校验失败：{0}"),
                        manifestFile.RelativePath));
                }

                processedBytes += manifestFile.Size;
                Report(progress, ArknightsLinkedClientStage.VerifyingTarget,
                    manifestFile.RelativePath, index + 1, files.Length,
                    processedBytes, totalBytes);
            }
        }

        private static string SafeCombine(string root, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath) ||
                Path.IsPathRooted(relativePath) ||
                relativePath.Contains(':', StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Unsafe manifest path: {relativePath}");
            }

            var fullRoot = NormalizeDirectoryPath(root);
            var combined = Path.GetFullPath(Path.Combine(fullRoot, relativePath));
            if (!IsPathInside(fullRoot, combined))
                throw new InvalidDataException($"Unsafe manifest path: {relativePath}");
            EnsureNoReparsePointsBetween(fullRoot, combined);
            return combined;
        }

        private static void EnsureTargetStillEmpty(string targetPath)
        {
            if (!Directory.Exists(targetPath)) return;
            if (Directory.EnumerateFileSystemEntries(targetPath).Any())
            {
                throw new IOException(
                    Localize("App.LinkedClient.Error.TargetNotEmpty",
                        "B服目标目录必须为空，以免覆盖已有文件"));
            }
        }

        private static string FindExistingParent(string path)
        {
            var current = Path.GetDirectoryName(path);
            while (!string.IsNullOrWhiteSpace(current) && !Directory.Exists(current))
                current = Path.GetDirectoryName(current);

            return current ?? throw new DirectoryNotFoundException(
                Localize("App.LinkedClient.Error.TargetParent",
                    "B服目标目录的父目录不存在"));
        }

        private static VolumeIdentity GetVolumeIdentity(string path)
        {
            var volumePath = new StringBuilder(512);
            if (!GetVolumePathName(path, volumePath, (uint)volumePath.Capacity))
                throw new Win32Exception(Marshal.GetLastWin32Error());

            var fileSystem = new StringBuilder(64);
            if (!GetVolumeInformation(
                    volumePath.ToString(), null, 0,
                    out var serialNumber, out _, out _,
                    fileSystem, (uint)fileSystem.Capacity))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            var volumeName = new StringBuilder(512);
            if (!GetVolumeNameForVolumeMountPoint(
                    volumePath.ToString(), volumeName, (uint)volumeName.Capacity))
            {
                volumeName.Append(volumePath);
            }

            return new VolumeIdentity(
                volumeName.ToString() + "|" + serialNumber,
                fileSystem.ToString());
        }

        private static bool IsReparsePoint(string path) =>
            File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint);

        private static bool IsPathInside(string root, string candidate)
        {
            var normalizedRoot = NormalizeDirectoryPath(root)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            var normalizedCandidate = Path.GetFullPath(candidate);
            return normalizedCandidate.StartsWith(
                normalizedRoot, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeDirectoryPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException(
                    Localize("App.LinkedClient.Error.PathMissing", "路径不能为空"),
                    nameof(path));

            var fullPath = Path.GetFullPath(path);
            var rootPath = Path.GetPathRoot(fullPath);
            if (!string.IsNullOrEmpty(rootPath) &&
                string.Equals(
                    fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase))
            {
                return rootPath;
            }

            return fullPath.TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static void EnsureNoReparsePointAncestors(string path)
        {
            for (var current = new DirectoryInfo(path); current != null; current = current.Parent)
            {
                if (current.Exists &&
                    current.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    throw new IOException(
                        Localize("App.LinkedClient.Error.TargetLink",
                            "硬链接客户端路径不能经过符号链接、目录联接或其他重解析点"));
                }
            }
        }

        private static void EnsureNoReparsePointsBetween(string root, string candidate)
        {
            if (!Directory.Exists(root)) return;

            var relative = Path.GetRelativePath(root, candidate);
            var segments = relative.Split(
                new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                StringSplitOptions.RemoveEmptyEntries);
            var current = root;
            for (var index = 0; index < segments.Length - 1; index++)
            {
                current = Path.Combine(current, segments[index]);
                if (Directory.Exists(current) && IsReparsePoint(current))
                    throw new IOException($"Manifest path crosses a reparse point: {relative}");
            }
        }

        private static IEnumerable<string> EnumerateRegularFiles(string rootPath)
        {
            var pending = new Stack<string>();
            pending.Push(rootPath);
            while (pending.Count > 0)
            {
                var directory = pending.Pop();
                if (IsReparsePoint(directory))
                    throw new IOException($"Directory is a reparse point: {directory}");

                foreach (var childDirectory in Directory.EnumerateDirectories(directory))
                    pending.Push(childDirectory);

                foreach (var file in Directory.EnumerateFiles(directory))
                {
                    if (File.GetAttributes(file).HasFlag(FileAttributes.ReparsePoint))
                        throw new IOException($"File is a reparse point: {file}");
                    yield return file;
                }
            }
        }

        private static uint GetHardLinkCount(string path)
        {
            return GetFileIdentity(path).NumberOfLinks;
        }

        private static bool HasSourceFilesWithExtraLinks(
            string sourceRoot,
            IEnumerable<string> relativePaths)
        {
            foreach (var relativePath in relativePaths)
            {
                var sourcePath = SafeCombine(sourceRoot, relativePath);
                if (!File.Exists(sourcePath)) continue;
                if (File.GetAttributes(sourcePath)
                        .HasFlag(FileAttributes.ReparsePoint) ||
                    GetHardLinkCount(sourcePath) > 1)
                {
                    return true;
                }
            }
            return false;
        }

        private static void ValidateSharedLinks(
            string sourceRoot,
            string targetRoot,
            IReadOnlyCollection<string> relativePaths)
        {
            if (relativePaths == null || relativePaths.Count == 0)
            {
                throw new InvalidDataException(
                    Localize("App.LinkedClient.Error.NoSharedFiles",
                        "官服与B服清单中没有可安全共享的相同资源文件"));
            }

            foreach (var relativePath in relativePaths)
            {
                var source = GetFileIdentity(
                    SafeCombine(sourceRoot, relativePath));
                var target = GetFileIdentity(
                    SafeCombine(targetRoot, relativePath));
                if (source.NumberOfLinks <= 1 ||
                    target.NumberOfLinks <= 1 ||
                    source.VolumeSerialNumber != target.VolumeSerialNumber ||
                    source.FileIndexHigh != target.FileIndexHigh ||
                    source.FileIndexLow != target.FileIndexLow)
                {
                    throw new InvalidDataException(string.Format(
                        Localize("App.LinkedClient.Error.LinkIntegrity",
                            "共享硬链接校验失败：{0}"),
                        relativePath));
                }
            }
        }

        private static void EnsureRollbackTargetOwned(
            string targetPath,
            LinkedClientPendingOperation pending,
            ServerGameManifest manifest,
            IReadOnlyCollection<string> linkedFiles)
        {
            if (pending == null || manifest == null || linkedFiles == null ||
                linkedFiles.Count == 0)
            {
                throw new InvalidDataException(
                    "The linked-client rollback transaction is incomplete.");
            }

            var normalizedTarget = NormalizeDirectoryPath(targetPath);
            if (!Directory.Exists(normalizedTarget) ||
                !LinkedClientPolicy.AreSamePath(
                    normalizedTarget, pending.TargetPath) ||
                IsReparsePoint(normalizedTarget))
            {
                throw new InvalidDataException(
                    "The linked-client rollback target is not owned by this transaction.");
            }
            EnsureNoReparsePointAncestors(normalizedTarget);

            if (!TryReadLinkedClientMarker(
                    normalizedTarget, out var targetMarker) ||
                !MarkerMatches(targetMarker, pending, BilibiliIconName))
            {
                throw new InvalidDataException(
                    "The linked-client rollback marker is missing or inconsistent.");
            }

            var expectedLinkedFiles = NormalizeRelativePaths(
                normalizedTarget, linkedFiles);
            var markerLinkedFiles = NormalizeRelativePaths(
                normalizedTarget, targetMarker.LinkedFiles);
            if (!expectedLinkedFiles.SetEquals(markerLinkedFiles))
            {
                throw new InvalidDataException(
                    "The linked-client rollback file list is inconsistent.");
            }

            ValidateSharedLinks(
                pending.SourcePath, normalizedTarget, linkedFiles);

            var allowedFiles = NormalizeRelativePaths(
                normalizedTarget,
                manifest.Files.Select(file => file.RelativePath));
            allowedFiles.Add(ManifestFileName);
            allowedFiles.Add(MarkerFileName);

            var allowedDirectories = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            foreach (var allowedFile in allowedFiles)
            {
                var separatorIndex = allowedFile.LastIndexOf('/');
                while (separatorIndex > 0)
                {
                    var directory = allowedFile[..separatorIndex];
                    allowedDirectories.Add(directory);
                    separatorIndex = directory.LastIndexOf('/');
                }
            }

            foreach (var file in EnumerateRegularFiles(normalizedTarget))
            {
                var relativePath = NormalizeRelativePath(
                    normalizedTarget, file);
                if (!allowedFiles.Contains(relativePath))
                {
                    throw new InvalidDataException(
                        $"The rollback target contains an unowned file: {relativePath}");
                }
            }

            var directories = new Stack<string>();
            directories.Push(normalizedTarget);
            while (directories.Count > 0)
            {
                var directory = directories.Pop();
                if (IsReparsePoint(directory))
                {
                    throw new IOException(
                        $"Directory is a reparse point: {directory}");
                }

                foreach (var child in Directory.EnumerateDirectories(directory))
                {
                    var relativePath = NormalizeRelativePath(
                        normalizedTarget, child);
                    if (!allowedDirectories.Contains(relativePath))
                    {
                        throw new InvalidDataException(
                            $"The rollback target contains an unowned directory: {relativePath}");
                    }
                    directories.Push(child);
                }
            }
        }

        private static HashSet<string> NormalizeRelativePaths(
            string rootPath,
            IEnumerable<string> relativePaths)
        {
            var normalized = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            foreach (var relativePath in relativePaths ?? Array.Empty<string>())
            {
                normalized.Add(NormalizeRelativePath(
                    rootPath, SafeCombine(rootPath, relativePath)));
            }
            return normalized;
        }

        private static string NormalizeRelativePath(
            string rootPath,
            string fullPath) =>
            Path.GetRelativePath(rootPath, fullPath).Replace('\\', '/');

        private static ByHandleFileInformation GetFileIdentity(string path)
        {
            using var handle = File.OpenHandle(
                path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            if (!GetFileInformationByHandle(handle, out var information))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            return information;
        }

        private static bool AreSameFileIdentity(
            ByHandleFileInformation left,
            ByHandleFileInformation right) =>
            left.VolumeSerialNumber == right.VolumeSerialNumber &&
            left.FileIndexHigh == right.FileIndexHigh &&
            left.FileIndexLow == right.FileIndexLow;

        private static void EnsureFreeSpace(
            string path,
            long requiredBytes,
            bool detaching)
        {
            if (requiredBytes <= 0) return;
            var probePath = Directory.Exists(path)
                ? path
                : FindExistingParent(path);
            var volumePath = new StringBuilder(512);
            if (!GetVolumePathName(
                    probePath, volumePath, (uint)volumePath.Capacity) ||
                !GetDiskFreeSpaceEx(
                    volumePath.ToString(),
                    out var availableBytes,
                    out _,
                    out _))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            var freeBytes = availableBytes > long.MaxValue
                ? long.MaxValue
                : (long)availableBytes;
            var reserve = detaching
                ? Math.Max(64L * 1024 * 1024, requiredBytes / 100)
                : Math.Max(512L * 1024 * 1024, requiredBytes / 20);
            if (freeBytes < requiredBytes + reserve)
            {
                throw new IOException(string.Format(
                    Localize("App.LinkedClient.Error.FreeSpace",
                        "磁盘空间不足。至少需要 {0:N0} 字节，当前可用 {1:N0} 字节"),
                    requiredBytes + reserve,
                    freeBytes));
            }
        }

        private static bool TryDeleteOwnedStagingDirectory(
            LinkedClientPendingOperation pending)
        {
            try
            {
                if (!TryValidateStagingPath(pending, out var fullStaging) ||
                    !Directory.Exists(fullStaging))
                    return false;

                var markerPath = Path.Combine(fullStaging, MarkerFileName);
                if (!TryReadMarkerFile(markerPath, out var marker) ||
                    !MarkerOwnsTransaction(
                        marker, pending, BilibiliIconName))
                    return false;

                foreach (var file in EnumerateRegularFiles(fullStaging)
                             .Where(path => !LinkedClientPolicy.AreSamePath(
                                 path, markerPath))
                             .ToArray())
                {
                    var attributes = File.GetAttributes(file);
                    if ((attributes.HasFlag(FileAttributes.ReadOnly) ||
                         attributes.HasFlag(FileAttributes.System)) &&
                        GetHardLinkCount(file) <= 1)
                    {
                        ClearDeletionBlockingAttributes(file);
                    }
                    File.Delete(file);
                }

                var directories = new List<string>();
                var pendingDirectories = new Stack<string>();
                pendingDirectories.Push(fullStaging);
                while (pendingDirectories.Count > 0)
                {
                    var directory = pendingDirectories.Pop();
                    if (IsReparsePoint(directory))
                        throw new IOException(
                            $"Directory is a reparse point: {directory}");
                    foreach (var child in Directory.EnumerateDirectories(directory))
                    {
                        directories.Add(child);
                        pendingDirectories.Push(child);
                    }
                }
                foreach (var directory in directories
                             .OrderByDescending(path => path.Length))
                {
                    Directory.Delete(directory, recursive: false);
                }

                if (Directory.EnumerateFileSystemEntries(fullStaging)
                    .Any(path => !LinkedClientPolicy.AreSamePath(
                        path, markerPath)))
                {
                    return false;
                }
                File.Delete(markerPath);
                Directory.Delete(fullStaging, recursive: false);
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.LogError(ex,
                    $"Linked client staging cleanup failed: {pending?.StagingPath}");
                return false;
            }
        }

        private static bool TryDeleteEmptyUnmarkedStagingDirectory(
            string stagingPath,
            LinkedClientPendingOperation pending)
        {
            try
            {
                if (!TryValidateStagingPath(pending, out var expectedStaging) ||
                    !LinkedClientPolicy.AreSamePath(
                        stagingPath, expectedStaging) ||
                    !Directory.Exists(expectedStaging) ||
                    Directory.EnumerateFileSystemEntries(expectedStaging).Any())
                {
                    return false;
                }

                Directory.Delete(expectedStaging, recursive: false);
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.LogError(ex,
                    $"Empty linked-client staging cleanup failed: {stagingPath}");
                return false;
            }
        }

        private static bool TryValidateStagingPath(
            LinkedClientPendingOperation pending,
            out string fullStaging)
        {
            fullStaging = "";
            if (pending == null ||
                string.IsNullOrWhiteSpace(pending.TargetPath) ||
                string.IsNullOrWhiteSpace(pending.StagingPath))
            {
                return false;
            }

            var target = NormalizeDirectoryPath(pending.TargetPath);
            fullStaging = NormalizeDirectoryPath(pending.StagingPath);
            var expectedParent = Path.GetDirectoryName(target);
            if (string.IsNullOrWhiteSpace(expectedParent) ||
                !string.Equals(
                    NormalizeDirectoryPath(expectedParent),
                    NormalizeDirectoryPath(Path.GetDirectoryName(fullStaging)),
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var prefix = "." + Path.GetFileName(target) + ".xel-linking-";
            var name = Path.GetFileName(fullStaging);
            if (!name.StartsWith(prefix, StringComparison.Ordinal) ||
                name.Length != prefix.Length + 32)
            {
                return false;
            }

            return name.AsSpan(prefix.Length).ToString().All(Uri.IsHexDigit);
        }

        private static void Report(
            IProgress<ArknightsLinkedClientProgress> progress,
            ArknightsLinkedClientStage stage,
            string currentFile = "",
            int fileIndex = 0,
            int fileCount = 0,
            long processedBytes = 0,
            long totalBytes = 0)
        {
            progress?.Report(new ArknightsLinkedClientProgress
            {
                Stage = stage,
                CurrentFile = currentFile,
                FileIndex = fileIndex,
                FileCount = fileCount,
                ProcessedBytes = processedBytes,
                TotalBytes = totalBytes
            });
        }

        private static string Localize(string key, string fallback) =>
            AntdUI.Localization.Get(key, fallback);

        [StructLayout(LayoutKind.Sequential)]
        private struct ByHandleFileInformation
        {
            public uint FileAttributes;
            public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
            public uint VolumeSerialNumber;
            public uint FileSizeHigh;
            public uint FileSizeLow;
            public uint NumberOfLinks;
            public uint FileIndexHigh;
            public uint FileIndexLow;
        }

        private sealed record VolumeIdentity(string VolumeName, string FileSystem);

        private sealed record DetachFile(
            string RelativePath,
            string SourcePath,
            string TargetPath,
            long Length,
            FileAttributes OriginalAttributes);

        private sealed class LinkedClientMarker
        {
            public int SchemaVersion { get; set; }
            public string GroupId { get; set; } = "";
            public string Role { get; set; } = "";
            public string ThisPath { get; set; } = "";
            public string PeerPath { get; set; } = "";
            public string TargetVersion { get; set; } = "";
            public string StagingPath { get; set; } = "";
            public string Phase { get; set; } = "";
            public List<string> LinkedFiles { get; set; } = new();
        }
    }
}
