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
            path,
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
