using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace XelLauncher.Helpers;

internal static class ParentProcessLauncher
{
    internal const uint CreateProcessAccess = 0x0080;
    internal const uint QueryLimitedInformation = 0x1000;

    internal static SafeProcessHandle OpenParent(int processId)
        => Open(processId, CreateProcessAccess | QueryLimitedInformation);

    internal static SafeProcessHandle OpenQuery(int processId)
        => Open(processId, QueryLimitedInformation);

    private static SafeProcessHandle Open(int processId, uint access)
    {
        var handle = OpenProcess(access, false, processId);
        if (!handle.IsInvalid) return handle;
        int error = Marshal.GetLastWin32Error();
        handle.Dispose();
        throw new Win32Exception(error, error == 5
            ? "无法访问官方启动器进程。请以与官方启动器相同的权限运行 Xel Launcher（必要时以管理员身份运行）。"
            : "无法打开官方启动器进程，请重新启动官方启动器后重试。");
    }

    internal static string GetImagePath(SafeProcessHandle handle)
    {
        var path = new StringBuilder(32768);
        int size = path.Capacity;
        if (!QueryFullProcessImageNameW(handle, 0, path, ref size))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        return path.ToString();
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
    private static extern SafeProcessHandle OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, int id);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageNameW(SafeProcessHandle process, uint flags, StringBuilder path, ref int size);
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
    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}
