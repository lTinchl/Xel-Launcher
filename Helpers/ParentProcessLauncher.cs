using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace XelLauncher.Helpers;

internal static class ParentProcessLauncher
{
    internal static SafeProcessHandle StartSuspended(string executable)
    {
        if (executable.IndexOfAny(new[] { '"', '\0' }) >= 0)
            throw new ArgumentException("Games.exe 路径包含无效字符。", nameof(executable));

        var startup = new StartupInfo { Size = Marshal.SizeOf<StartupInfo>() };
        var command = new StringBuilder($"\"{executable}\"");
        if (!CreateSuspendedProcessW(executable, command, IntPtr.Zero, IntPtr.Zero,
                false, 0x00000004 | 0x08000000, IntPtr.Zero, System.IO.Path.GetDirectoryName(executable),
                ref startup, out var info))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "无法创建暂停的 Games.exe 父进程。");

        CloseHandle(info.Thread);
        return new SafeProcessHandle(info.Process, ownsHandle: true);
    }

    internal static void Terminate(SafeProcessHandle process)
    {
        if (process == null || process.IsClosed || process.IsInvalid) return;
        // The parent is never resumed; if it already exited there is nothing to clean up.
        TerminateProcess(process, 0);
        // Wait for the image mapping to be released before deleting its staged file.
        WaitForSingleObject(process, 2000);
    }

    internal static Process Start(SafeProcessHandle parent, string executable, string arguments, string directory)
    {
        if (executable.IndexOfAny(new[] { '"', '\0' }) >= 0 || arguments?.Contains('\0') == true)
            throw new ArgumentException("游戏路径或启动参数含有无效字符。");
        var command = new StringBuilder($"\"{executable}\"" +
            (string.IsNullOrWhiteSpace(arguments) ? "" : " " + arguments));
        if (command.Length >= 32767) throw new ArgumentException("游戏启动参数过长。");

        IntPtr list = IntPtr.Zero, value = IntPtr.Zero;
        bool initialized = false;
        bool parentReferenced = false;
        var info = new ProcessInformation();
        try
        {
            parent.DangerousAddRef(ref parentReferenced);
            nuint size = 0;
            InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref size);
            if (size == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
            list = Marshal.AllocHGlobal(checked((int)size));
            if (!InitializeProcThreadAttributeList(list, 1, 0, ref size))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            initialized = true;
            value = Marshal.AllocHGlobal(IntPtr.Size);
            Marshal.WriteIntPtr(value, parent.DangerousGetHandle());
            if (!UpdateProcThreadAttribute(list, 0, 0x00020000, value,
                    (nuint)IntPtr.Size, IntPtr.Zero, IntPtr.Zero))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            var startup = new StartupInfoEx
            {
                StartupInfo = new StartupInfo { Size = Marshal.SizeOf<StartupInfoEx>() },
                AttributeList = list
            };
            if (!CreateProcessW(executable, command, IntPtr.Zero, IntPtr.Zero, false,
                    0x00080000 | 0x08000000, IntPtr.Zero, directory, ref startup, out info))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "创建游戏进程失败。");

            // Return the exact new process so callers need not inspect protected
            // game modules to rediscover it after the anti-cheat starts.
            return Process.GetProcessById(info.ProcessId);
        }
        finally
        {
            if (info.Thread != IntPtr.Zero) CloseHandle(info.Thread);
            if (info.Process != IntPtr.Zero) CloseHandle(info.Process);
            if (initialized) DeleteProcThreadAttributeList(list);
            if (list != IntPtr.Zero) Marshal.FreeHGlobal(list);
            if (value != IntPtr.Zero) Marshal.FreeHGlobal(value);
            if (parentReferenced) parent.DangerousRelease();
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct StartupInfo
    {
        public int Size;
        public IntPtr Reserved, Desktop, Title;
        public uint X, Y, XSize, YSize, XCountChars, YCountChars, FillAttribute, Flags;
        public ushort ShowWindow, ReservedSize;
        public IntPtr ReservedBytes, StdInput, StdOutput, StdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct StartupInfoEx { public StartupInfo StartupInfo; public IntPtr AttributeList; }
    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation { public IntPtr Process, Thread; public int ProcessId, ThreadId; }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool InitializeProcThreadAttributeList(IntPtr list, int count, uint flags, ref nuint size);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UpdateProcThreadAttribute(IntPtr list, uint flags, nuint attribute, IntPtr value, nuint size, IntPtr previous, IntPtr returnedSize);
    [DllImport("kernel32.dll")]
    private static extern void DeleteProcThreadAttributeList(IntPtr list);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateProcessW(string application, StringBuilder command, IntPtr processAttributes,
        IntPtr threadAttributes, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint flags, IntPtr environment,
        string directory, ref StartupInfoEx startup, out ProcessInformation information);
    [DllImport("kernel32.dll", EntryPoint = "CreateProcessW", CharSet = CharSet.Unicode,
        ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateSuspendedProcessW(string application, StringBuilder command,
        IntPtr processAttributes, IntPtr threadAttributes,
        [MarshalAs(UnmanagedType.Bool)] bool inherit, uint flags, IntPtr environment,
        string directory, ref StartupInfo startup, out ProcessInformation information);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TerminateProcess(SafeProcessHandle process, uint exitCode);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(SafeProcessHandle process, uint milliseconds);
    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}
