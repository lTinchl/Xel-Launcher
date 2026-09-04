using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace XelLauncher.Helpers
{
    /// <summary>
    /// Windows hard-link primitives with extended-length path support.
    /// Linked Runtime staging adds enough path segments to exceed MAX_PATH even
    /// when the physical game path itself is short, so every native file path
    /// must use the Win32 extended path form.
    /// </summary>
    internal static class WindowsHardLink
    {
        private const string ExtendedPathPrefix = @"\\?\";
        private const string UncPrefix = @"\\";
        private const string ExtendedUncPrefix = @"\\?\UNC\";

        [DllImport(
            "kernel32.dll",
            EntryPoint = "CreateHardLinkW",
            CharSet = CharSet.Unicode,
            SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CreateHardLinkNative(
            string newFileName,
            string existingFileName,
            IntPtr securityAttributes);

        [DllImport(
            "kernel32.dll",
            EntryPoint = "CreateFileW",
            CharSet = CharSet.Unicode,
            SetLastError = true)]
        private static extern SafeFileHandle CreateFileNative(
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

        public static bool TryCreate(
            string newFileName,
            string existingFileName,
            out int errorCode)
        {
            var created = CreateHardLinkNative(
                ToExtendedLengthPath(newFileName),
                ToExtendedLengthPath(existingFileName),
                IntPtr.Zero);
            errorCode = created ? 0 : Marshal.GetLastWin32Error();
            return created;
        }

        public static bool AreSameFile(string left, string right)
        {
            try
            {
                using var leftHandle = CreateFileNative(
                    ToExtendedLengthPath(left),
                    0,
                    FileShare.ReadWrite | FileShare.Delete,
                    IntPtr.Zero,
                    FileMode.Open,
                    FileAttributes.Normal,
                    IntPtr.Zero);
                using var rightHandle = CreateFileNative(
                    ToExtendedLengthPath(right),
                    0,
                    FileShare.ReadWrite | FileShare.Delete,
                    IntPtr.Zero,
                    FileMode.Open,
                    FileAttributes.Normal,
                    IntPtr.Zero);
                if (leftHandle.IsInvalid || rightHandle.IsInvalid ||
                    !GetFileInformationByHandle(leftHandle, out var leftInfo) ||
                    !GetFileInformationByHandle(rightHandle, out var rightInfo))
                {
                    return false;
                }

                return leftInfo.VolumeSerialNumber == rightInfo.VolumeSerialNumber &&
                       leftInfo.FileIndexHigh == rightInfo.FileIndexHigh &&
                       leftInfo.FileIndexLow == rightInfo.FileIndexLow;
            }
            catch
            {
                return false;
            }
        }

        private static string ToExtendedLengthPath(string path)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            if (path.StartsWith(ExtendedPathPrefix, StringComparison.Ordinal))
                return path;

            var fullPath = Path.GetFullPath(path);
            return fullPath.StartsWith(UncPrefix, StringComparison.Ordinal)
                ? ExtendedUncPrefix + fullPath[UncPrefix.Length..]
                : ExtendedPathPrefix + fullPath;
        }

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
}
