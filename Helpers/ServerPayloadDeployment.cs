using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace XelLauncher.Helpers
{
    /// <summary>
    /// Deploys a validated server payload and owns reconciliation of all
    /// channel-specific files for the corresponding game family.
    /// </summary>
    public static class ServerPayloadDeployment
    {
        private static readonly IReadOnlyDictionary<GameFamily, IReadOnlyList<string>>
            ManagedEntriesByFamily = Enum.GetValues<GameFamily>()
                .ToDictionary(
                    family => family,
                    BuildManagedEntries);

        public static bool CanUseHardLinks(string sourcePath, string targetPath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) ||
                string.IsNullOrWhiteSpace(targetPath))
            {
                return false;
            }

            var sourceRoot = Path.GetPathRoot(Path.GetFullPath(sourcePath));
            var targetRoot = Path.GetPathRoot(Path.GetFullPath(targetPath));
            return string.Equals(
                sourceRoot, targetRoot, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Returns true when the traditional server switch owns this path for
        /// the game family. Linked Runtime must never hard-link such files.
        /// </summary>
        public static bool IsManagedPath(GameFamily family, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath)) return true;
            var normalized = NormalizeRelativePath(relativePath).TrimEnd('/');
            foreach (var managed in GetManagedEntries(family))
            {
                var candidate = NormalizeRelativePath(managed).TrimEnd('/');
                if (string.Equals(normalized, candidate,
                        StringComparison.OrdinalIgnoreCase) ||
                    normalized.StartsWith(candidate + "/",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public static IReadOnlyList<string> GetManagedPaths(GameFamily family) =>
            GetManagedEntries(family);

        public static Task<bool> DeployDirectoryAsync(
            string sourceDirectory,
            string targetDirectory,
            bool preferHardLink,
            int maxRetries = 5)
        {
            var paths = NormalizeAndValidateDirectories(
                sourceDirectory, targetDirectory, maxRetries);
            return DeployDirectoryCoreAsync(
                paths.Source,
                paths.Target,
                preferHardLink,
                maxRetries,
                _ => true);
        }

        public static async Task<bool> DeployProfileAsync(
            ServerPayloadProfile profile,
            string sourceDirectory,
            string targetDirectory,
            bool preferHardLink,
            int maxRetries = 5)
        {
            ArgumentNullException.ThrowIfNull(profile);
            var paths = NormalizeAndValidateDirectories(
                sourceDirectory, targetDirectory, maxRetries);

            ValidateRequiredFiles(profile, paths.Source);
            Directory.CreateDirectory(paths.Target);

            var operationId = Guid.NewGuid().ToString("N");
            var stagingDirectory = GetChildPath(
                paths.Target, $".xel-payload-staging-{operationId}");
            var backupDirectory = GetChildPath(
                paths.Target, $".xel-payload-backup-{operationId}");
            var preserveBackup = false;

            try
            {
                var allHardLinked = await DeployDirectoryCoreAsync(
                        paths.Source,
                        stagingDirectory,
                        // A channel profile owns SDK/login/config files that may
                        // be changed by the game. They must never share a file
                        // object with the payload cache, even when a legacy
                        // caller still asks for hard links.
                        preferHardLink: false,
                        maxRetries,
                        relativePath => IsOwnedByProfile(profile, relativePath))
                    .ConfigureAwait(false);
                ValidateRequiredFiles(profile, stagingDirectory);

                try
                {
                    MoveManagedItemsToBackup(
                        profile.GameFamily, paths.Target, backupDirectory);
                }
                catch (Exception snapshotError)
                {
                    try
                    {
                        RestoreManagedItems(
                            profile.GameFamily, paths.Target, backupDirectory);
                    }
                    catch (Exception restoreError)
                    {
                        preserveBackup = true;
                        throw CreateRollbackException(
                            snapshotError, restoreError, backupDirectory);
                    }

                    throw;
                }

                try
                {
                    MoveStagedFiles(stagingDirectory, paths.Target);
                }
                catch (Exception deploymentError)
                {
                    try
                    {
                        DeleteManagedItems(profile.GameFamily, paths.Target);
                        RestoreManagedItems(
                            profile.GameFamily, paths.Target, backupDirectory);
                    }
                    catch (Exception restoreError)
                    {
                        preserveBackup = true;
                        throw CreateRollbackException(
                            deploymentError, restoreError, backupDirectory);
                    }

                    throw;
                }

                return allHardLinked;
            }
            finally
            {
                TryDeletePath(stagingDirectory);
                if (!preserveBackup)
                    TryDeletePath(backupDirectory);
            }
        }

        private static Task<bool> DeployDirectoryCoreAsync(
            string sourceDirectory,
            string targetDirectory,
            bool preferHardLink,
            int maxRetries,
            Func<string, bool> includeFile)
        {
            return Task.Run(() =>
            {
                Directory.CreateDirectory(targetDirectory);
                var useHardLink = preferHardLink &&
                                  CanUseHardLinks(sourceDirectory, targetDirectory);
                var allHardLinked = useHardLink;

                foreach (var sourceFile in Directory.EnumerateFiles(
                             sourceDirectory, "*", SearchOption.AllDirectories))
                {
                    var relativePath = Path.GetRelativePath(sourceDirectory, sourceFile);
                    if (ServerPayloadUpdater.IsDeploymentExcluded(relativePath) ||
                        !includeFile(relativePath))
                    {
                        continue;
                    }

                    var destinationFile = GetChildPath(targetDirectory, relativePath);
                    Directory.CreateDirectory(Path.GetDirectoryName(destinationFile)!);

                    for (var attempt = 1; attempt <= maxRetries; attempt++)
                    {
                        try
                        {
                            if (!HardLinkOrCopyFile(
                                    sourceFile, destinationFile, useHardLink))
                            {
                                allHardLinked = false;
                            }

                            break;
                        }
                        catch (IOException) when (attempt < maxRetries)
                        {
                            Thread.Sleep(1000);
                        }
                    }
                }

                return allHardLinked;
            });
        }

        private static (string Source, string Target) NormalizeAndValidateDirectories(
            string sourceDirectory,
            string targetDirectory,
            int maxRetries)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory);
            ArgumentException.ThrowIfNullOrWhiteSpace(targetDirectory);
            if (maxRetries < 1)
                throw new ArgumentOutOfRangeException(nameof(maxRetries));

            var source = NormalizeDirectory(sourceDirectory);
            var target = NormalizeDirectory(targetDirectory);
            if (!Directory.Exists(source))
                throw new DirectoryNotFoundException(
                    $"Server payload directory not found: {source}");
            if (IsVolumeRoot(target))
                throw new InvalidOperationException(
                    "A volume root cannot be used as a server payload target.");
            if (PathsOverlap(source, target))
                throw new InvalidOperationException(
                    "The payload source and deployment target must not overlap.");

            return (source, target);
        }

        private static void ValidateRequiredFiles(
            ServerPayloadProfile profile,
            string directory)
        {
            var missing = profile.RequiredFiles
                .Where(relativePath => !File.Exists(
                    GetChildPath(directory, relativePath)))
                .ToArray();
            if (missing.Length > 0)
            {
                throw new InvalidDataException(
                    $"The {profile.IconName} payload is incomplete. Missing: " +
                    string.Join(", ", missing));
            }
        }

        private static bool IsOwnedByProfile(
            ServerPayloadProfile profile,
            string relativePath)
        {
            var normalized = NormalizeRelativePath(relativePath);
            if (!normalized.Contains('/'))
            {
                return profile.RootFiles.Contains(
                    normalized, StringComparer.OrdinalIgnoreCase);
            }

            return profile.DirectoryPrefixes.Any(prefix =>
            {
                var normalizedPrefix = NormalizeRelativePath(prefix).TrimEnd('/');
                return normalized.StartsWith(
                    normalizedPrefix + "/", StringComparison.OrdinalIgnoreCase);
            });
        }

        private static void MoveManagedItemsToBackup(
            GameFamily family,
            string targetDirectory,
            string backupDirectory)
        {
            foreach (var relativePath in GetManagedEntries(family))
            {
                var sourcePath = GetChildPath(targetDirectory, relativePath);
                if (!PathExists(sourcePath)) continue;

                var backupPath = GetChildPath(backupDirectory, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);
                MovePath(sourcePath, backupPath);
            }
        }

        private static void RestoreManagedItems(
            GameFamily family,
            string targetDirectory,
            string backupDirectory)
        {
            foreach (var relativePath in GetManagedEntries(family))
            {
                var backupPath = GetChildPath(backupDirectory, relativePath);
                if (!PathExists(backupPath)) continue;

                var targetPath = GetChildPath(targetDirectory, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                MovePath(backupPath, targetPath);
            }
        }

        private static void DeleteManagedItems(
            GameFamily family,
            string targetDirectory)
        {
            foreach (var relativePath in GetManagedEntries(family))
                DeletePath(GetChildPath(targetDirectory, relativePath));
        }

        private static IReadOnlyList<string> GetManagedEntries(GameFamily family)
        {
            return ManagedEntriesByFamily.TryGetValue(family, out var entries)
                ? entries
                : Array.Empty<string>();
        }

        private static IReadOnlyList<string> BuildManagedEntries(
            GameFamily family)
        {
            var profiles = ServerPayloadUpdater.Profiles
                .Where(profile => profile.GameFamily == family)
                .ToArray();
            var directories = profiles
                .SelectMany(profile => profile.DirectoryPrefixes)
                .Select(NormalizeRelativePath)
                .Select(path => path.TrimEnd('/'))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(path => path.Length)
                .ToArray();
            var rootFiles = profiles
                .SelectMany(profile => profile.RootFiles)
                .Select(NormalizeRelativePath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);

            return directories.Concat(rootFiles).ToArray();
        }

        private static void MoveStagedFiles(
            string stagingDirectory,
            string targetDirectory)
        {
            foreach (var stagedFile in Directory.EnumerateFiles(
                         stagingDirectory, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(stagingDirectory, stagedFile);
                var targetFile = GetChildPath(targetDirectory, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
                File.Move(stagedFile, targetFile, true);
            }
        }

        private static bool HardLinkOrCopyFile(
            string sourceFile,
            string destinationFile,
            bool useHardLink)
        {
            if (useHardLink &&
                File.Exists(destinationFile) &&
                IsSameFile(sourceFile, destinationFile))
            {
                return true;
            }

            DeletePath(destinationFile);
            if (useHardLink &&
                WindowsHardLink.TryCreate(
                    destinationFile, sourceFile, out _))
            {
                return true;
            }

            File.Copy(sourceFile, destinationFile, true);
            return false;
        }

        private static bool IsSameFile(string pathA, string pathB) =>
            WindowsHardLink.AreSameFile(pathA, pathB);

        private static void MovePath(string sourcePath, string destinationPath)
        {
            var attributes = File.GetAttributes(sourcePath);
            if ((attributes & FileAttributes.Directory) != 0)
                Directory.Move(sourcePath, destinationPath);
            else
                File.Move(sourcePath, destinationPath);
        }

        private static void DeletePath(string path)
        {
            if (!PathExists(path)) return;

            var attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.Directory) == 0)
            {
                File.SetAttributes(path, FileAttributes.Normal);
                File.Delete(path);
                return;
            }

            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                Directory.Delete(path);
                return;
            }

            foreach (var child in Directory.EnumerateFileSystemEntries(path))
                DeletePath(child);
            Directory.Delete(path);
        }

        private static void TryDeletePath(string path)
        {
            try
            {
                DeletePath(path);
            }
            catch (Exception ex)
            {
                LogHelper.LogError(ex, $"Clean server payload transaction directory: {path}");
            }
        }

        private static bool PathExists(string path)
        {
            if (File.Exists(path) || Directory.Exists(path)) return true;
            try
            {
                _ = File.GetAttributes(path);
                return true;
            }
            catch (FileNotFoundException)
            {
                return false;
            }
            catch (DirectoryNotFoundException)
            {
                return false;
            }
        }

        private static string GetChildPath(string rootDirectory, string relativePath)
        {
            if (Path.IsPathRooted(relativePath))
                throw new InvalidDataException($"Rooted payload path: {relativePath}");

            var root = NormalizeDirectory(rootDirectory);
            var candidate = Path.GetFullPath(Path.Combine(
                root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            var rootPrefix = AppendDirectorySeparator(root);
            if (!candidate.StartsWith(
                    rootPrefix,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Payload path escapes the target directory: {relativePath}");
            }

            return candidate;
        }

        private static string NormalizeDirectory(string path)
        {
            var fullPath = Path.GetFullPath(path);
            return IsVolumeRoot(fullPath)
                ? Path.GetPathRoot(fullPath)!
                : fullPath.TrimEnd(
                    Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static string NormalizeRelativePath(string path) =>
            path.Replace('\\', '/').TrimStart('/');

        private static bool PathsOverlap(string left, string right)
        {
            if (string.Equals(left, right, StringComparison.OrdinalIgnoreCase))
                return true;

            return left.StartsWith(
                       AppendDirectorySeparator(right),
                       StringComparison.OrdinalIgnoreCase) ||
                   right.StartsWith(
                       AppendDirectorySeparator(left),
                       StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsVolumeRoot(string path)
        {
            var fullPath = Path.GetFullPath(path);
            var root = Path.GetPathRoot(fullPath);
            return !string.IsNullOrEmpty(root) &&
                   string.Equals(
                       fullPath.TrimEnd(
                           Path.DirectorySeparatorChar,
                           Path.AltDirectorySeparatorChar),
                       root.TrimEnd(
                           Path.DirectorySeparatorChar,
                           Path.AltDirectorySeparatorChar),
                       StringComparison.OrdinalIgnoreCase);
        }

        private static string AppendDirectorySeparator(string path) =>
            path.EndsWith(Path.DirectorySeparatorChar) ||
            path.EndsWith(Path.AltDirectorySeparatorChar)
                ? path
                : path + Path.DirectorySeparatorChar;

        private static Exception CreateRollbackException(
            Exception operationError,
            Exception restoreError,
            string backupDirectory)
        {
            return new AggregateException(
                $"Server payload deployment failed and rollback was incomplete. " +
                $"Recovery files were kept at {backupDirectory}.",
                operationError,
                restoreError);
        }
    }
}
