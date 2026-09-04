using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using XelLauncher.Helpers;

namespace XelLauncher.Tests;

public sealed class HardLinkDeploymentTests
{
    [Fact]
    public async Task PreferredHardLinks_CreateSharedFileIdentity_AndSkipUnsafeFiles()
    {
        Assert.True(OperatingSystem.IsWindows());
        using var temp = new TemporaryDirectory();
        var source = temp.CreateDirectory("source");
        var target = temp.CreateDirectory("target");
        if (!IsNtfs(source)) return;

        var sourceFile = temp.WriteFile("source/payload.dll", "shared payload");
        var nestedSource = temp.WriteFile("source/nested/asset.bin", "nested payload");
        temp.WriteFile("source/config.ini", "must not deploy");
        temp.WriteFile("source/Arknights_Data/large.bin", "must not deploy");
        temp.WriteFile("source/game_files_official", "must not deploy");
        var existingManifest = temp.WriteFile("target/game_files", "keep manifest");

        var result = await GameLauncher.HardLinkOrCopyDirectory(
            source, target, preferHardLink: true);

        Assert.True(result);
        AssertSameFile(sourceFile, Path.Combine(target, "payload.dll"));
        AssertSameFile(nestedSource, Path.Combine(target, "nested", "asset.bin"));
        Assert.False(File.Exists(Path.Combine(target, "config.ini")));
        Assert.False(File.Exists(Path.Combine(target, "Arknights_Data", "large.bin")));
        Assert.False(File.Exists(Path.Combine(target, "game_files_official")));
        Assert.Equal("keep manifest", File.ReadAllText(existingManifest));

        File.WriteAllText(Path.Combine(target, "payload.dll"), "changed through link");
        Assert.Equal("changed through link", File.ReadAllText(sourceFile));

        var secondResult = await GameLauncher.HardLinkOrCopyDirectory(
            source, target, preferHardLink: true);
        Assert.True(secondResult);
        AssertSameFile(sourceFile, Path.Combine(target, "payload.dll"));
    }

    [Fact]
    public async Task HardLinksDisabled_CreateIndependentCopies()
    {
        using var temp = new TemporaryDirectory();
        var source = temp.CreateDirectory("source");
        var target = temp.CreateDirectory("target");
        var sourceFile = temp.WriteFile("source/payload.dll", "source content");

        var result = await GameLauncher.HardLinkOrCopyDirectory(
            source, target, preferHardLink: false);

        var targetFile = Path.Combine(target, "payload.dll");
        Assert.False(result);
        Assert.Equal("source content", File.ReadAllText(targetFile));
        AssertNotSameFile(sourceFile, targetFile);

        File.WriteAllText(targetFile, "target changed");
        Assert.Equal("source content", File.ReadAllText(sourceFile));
    }

    [Fact]
    public async Task PreferredHardLinks_SupportDestinationPathsBeyondMaxPath()
    {
        Assert.True(OperatingSystem.IsWindows());
        using var temp = new TemporaryDirectory();
        var source = temp.CreateDirectory("source");
        if (!IsNtfs(source)) return;

        var sourceFile = temp.WriteFile("source/asset.bin", "shared payload");
        var longTargetRelativePath = string.Join(
            '/',
            Enumerable.Repeat("runtime-segment-0123456789abcdef", 7));
        var target = temp.GetPath(longTargetRelativePath);
        var destinationFile = Path.Combine(target, "asset.bin");
        Assert.True(destinationFile.Length >= 260);

        var result = await GameLauncher.HardLinkOrCopyDirectory(
            source, target, preferHardLink: true);

        Assert.True(result);
        Assert.True(File.Exists(destinationFile));
        AssertSameFile(sourceFile, destinationFile);
    }

    [Fact]
    public async Task ProfileDeployment_RemovesPreviousChannelFiles_AndKeepsGameFiles()
    {
        using var temp = new TemporaryDirectory();
        var source = temp.CreateDirectory("source");
        var target = temp.CreateDirectory("target");
        var profile = Assert.IsType<ServerPayloadProfile>(
            ServerPayloadUpdater.GetProfile("Arknights"));

        foreach (var relativePath in profile.RequiredFiles)
            temp.WriteFile($"source/{relativePath}", $"official:{relativePath}");

        temp.WriteFile("target/PCGameSDK.dll", "stale bilibili sdk");
        temp.WriteFile("target/BLPlatform64/PCGamePlatform.exe", "stale bilibili platform");
        temp.WriteFile("target/sdkdata/obsolete.bin", "stale official data");
        temp.WriteFile("target/U8Data/config/obsolete.bin", "stale config");
        var gameManifest = temp.WriteFile("target/game_files", "keep manifest");
        var executable = temp.WriteFile("target/Arknights.exe", "keep executable");
        var unrelatedFamilyFile = temp.WriteFile(
            "target/eld_Endfield.db", "keep unrelated family file");

        var result = await ServerPayloadDeployment.DeployProfileAsync(
            profile, source, target, preferHardLink: false);

        Assert.False(result);
        Assert.False(File.Exists(Path.Combine(target, "PCGameSDK.dll")));
        Assert.False(Directory.Exists(Path.Combine(target, "BLPlatform64")));
        Assert.False(File.Exists(Path.Combine(target, "sdkdata", "obsolete.bin")));
        Assert.False(File.Exists(Path.Combine(
            target, "U8Data", "config", "obsolete.bin")));
        Assert.Equal("keep manifest", File.ReadAllText(gameManifest));
        Assert.Equal("keep executable", File.ReadAllText(executable));
        Assert.Equal("keep unrelated family file", File.ReadAllText(unrelatedFamilyFile));

        foreach (var relativePath in profile.RequiredFiles)
        {
            Assert.Equal(
                $"official:{relativePath}",
                File.ReadAllText(Path.Combine(
                    target,
                    relativePath.Replace('/', Path.DirectorySeparatorChar))));
        }

        Assert.Empty(Directory.EnumerateFileSystemEntries(
            target, ".xel-payload-*", SearchOption.TopDirectoryOnly));
    }

    [Fact]
    public async Task ProfileDeployment_RejectsIncompletePayload_BeforeChangingTarget()
    {
        using var temp = new TemporaryDirectory();
        var source = temp.CreateDirectory("source");
        var target = temp.CreateDirectory("target");
        var profile = Assert.IsType<ServerPayloadProfile>(
            ServerPayloadUpdater.GetProfile("Arknights"));
        temp.WriteFile("source/U8CoreUI.dll", "incomplete payload");
        var oldSdk = temp.WriteFile("target/PCGameSDK.dll", "existing sdk");
        var oldManifest = temp.WriteFile("target/game_files", "existing manifest");

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            ServerPayloadDeployment.DeployProfileAsync(
                profile, source, target, preferHardLink: false));

        Assert.Equal("existing sdk", File.ReadAllText(oldSdk));
        Assert.Equal("existing manifest", File.ReadAllText(oldManifest));
        Assert.Empty(Directory.EnumerateFileSystemEntries(
            target, ".xel-payload-*", SearchOption.TopDirectoryOnly));
    }

    [Fact]
    public async Task ProfileDeployment_KeepsMutablePayloadIndependent_WhenHardLinksAreRequested()
    {
        using var temp = new TemporaryDirectory();
        var source = temp.CreateDirectory("source");
        var target = temp.CreateDirectory("target");
        var profile = Assert.IsType<ServerPayloadProfile>(
            ServerPayloadUpdater.GetProfile("Arknights"));

        foreach (var relativePath in profile.RequiredFiles)
            temp.WriteFile($"source/{relativePath}", $"official:{relativePath}");

        var sourceSdk = Path.Combine(source, "U8CoreUI.dll");
        var allHardLinked = await ServerPayloadDeployment.DeployProfileAsync(
            profile, source, target, preferHardLink: true);
        var deployedSdk = Path.Combine(target, "U8CoreUI.dll");

        Assert.False(allHardLinked);
        Assert.Equal(File.ReadAllText(sourceSdk), File.ReadAllText(deployedSdk));
        AssertNotSameFile(sourceSdk, deployedSdk);

        File.WriteAllText(deployedSdk, "runtime mutation");
        Assert.NotEqual("runtime mutation", File.ReadAllText(sourceSdk));
    }

    [Fact]
    public async Task EndfieldProfileDeployment_RemovesOtherEndfieldChannelPayloads()
    {
        using var temp = new TemporaryDirectory();
        var source = temp.CreateDirectory("source");
        var target = temp.CreateDirectory("target");
        var profile = Assert.IsType<ServerPayloadProfile>(
            ServerPayloadUpdater.GetProfile("BiliEndfield"));

        foreach (var relativePath in profile.RequiredFiles)
            temp.WriteFile($"source/{relativePath}", $"bilibili:{relativePath}");

        foreach (var stalePath in new[]
                 {
                     "gfsdk.dll",
                     "glextra.dll",
                     "glfoundation.dll",
                     "manifest.xml",
                     "play_pc_sdk.dll",
                     "hgsdk.dll",
                     "sdkdata/stale.bin"
                 })
        {
            temp.WriteFile($"target/{stalePath}", "stale channel payload");
        }

        var unrelatedFamilyFile = temp.WriteFile(
            "target/PlatformProcess.dll", "keep Arknights payload");
        var executable = temp.WriteFile("target/Endfield.exe", "keep executable");

        await ServerPayloadDeployment.DeployProfileAsync(
            profile, source, target, preferHardLink: false);

        Assert.False(File.Exists(Path.Combine(target, "gfsdk.dll")));
        Assert.False(File.Exists(Path.Combine(target, "glextra.dll")));
        Assert.False(File.Exists(Path.Combine(target, "glfoundation.dll")));
        Assert.False(File.Exists(Path.Combine(target, "manifest.xml")));
        Assert.False(File.Exists(Path.Combine(target, "play_pc_sdk.dll")));
        Assert.False(File.Exists(Path.Combine(target, "hgsdk.dll")));
        Assert.False(Directory.Exists(Path.Combine(target, "sdkdata")));
        Assert.Equal("keep Arknights payload", File.ReadAllText(unrelatedFamilyFile));
        Assert.Equal("keep executable", File.ReadAllText(executable));
    }

    [Theory]
    [InlineData("")]
    [InlineData("config.ini")]
    [InlineData("Arknights_Data/data.bin")]
    [InlineData("Endfield_Data/data.bin")]
    [InlineData("bin/Arknights.exe")]
    [InlineData("bin/Endfield.exe")]
    [InlineData("GameAssembly.dll")]
    [InlineData("baselib.dll")]
    [InlineData("UnityPlayer.dll")]
    [InlineData("game_files_bilibili")]
    [InlineData("payload-state.json")]
    public void DeploymentExclusions_BlockProtectedPaths(string relativePath)
    {
        Assert.True(ServerPayloadUpdater.IsDeploymentExcluded(relativePath));
    }

    [Theory]
    [InlineData("payload.dll")]
    [InlineData("sdk/channel.dat")]
    [InlineData("nested/asset.bin")]
    public void DeploymentExclusions_AllowPayloadFiles(string relativePath)
    {
        Assert.False(ServerPayloadUpdater.IsDeploymentExcluded(relativePath));
    }

    private static bool IsNtfs(string path)
    {
        var root = Path.GetPathRoot(Path.GetFullPath(path));
        return string.Equals(
            new DriveInfo(root!).DriveFormat,
            "NTFS",
            StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertSameFile(string left, string right)
    {
        var leftInfo = GetFileIdentity(left);
        var rightInfo = GetFileIdentity(right);
        Assert.Equal(leftInfo.VolumeSerialNumber, rightInfo.VolumeSerialNumber);
        Assert.Equal(leftInfo.FileIndexHigh, rightInfo.FileIndexHigh);
        Assert.Equal(leftInfo.FileIndexLow, rightInfo.FileIndexLow);
        Assert.True(leftInfo.NumberOfLinks > 1);
        Assert.True(rightInfo.NumberOfLinks > 1);
    }

    private static void AssertNotSameFile(string left, string right)
    {
        var leftInfo = GetFileIdentity(left);
        var rightInfo = GetFileIdentity(right);
        Assert.False(
            leftInfo.VolumeSerialNumber == rightInfo.VolumeSerialNumber &&
            leftInfo.FileIndexHigh == rightInfo.FileIndexHigh &&
            leftInfo.FileIndexLow == rightInfo.FileIndexLow);
    }

    private static ByHandleFileInformation GetFileIdentity(string path)
    {
        using var handle = CreateFile(
            ToExtendedLengthPath(path),
            0,
            FileShare.ReadWrite | FileShare.Delete,
            IntPtr.Zero,
            FileMode.Open,
            FileAttributes.Normal,
            IntPtr.Zero);
        Assert.False(handle.IsInvalid);
        Assert.True(GetFileInformationByHandle(handle, out var info));
        return info;
    }

    private static string ToExtendedLengthPath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (fullPath.StartsWith(@"\\?\", StringComparison.Ordinal))
            return fullPath;
        return fullPath.StartsWith(@"\\", StringComparison.Ordinal)
            ? @"\\?\UNC\" + fullPath[2..]
            : @"\\?\" + fullPath;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(
        string fileName,
        uint desiredAccess,
        FileShare shareMode,
        IntPtr securityAttributes,
        FileMode creationDisposition,
        FileAttributes flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(
        SafeFileHandle file,
        out ByHandleFileInformation information);

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
}
