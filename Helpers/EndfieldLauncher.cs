using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace XelLauncher.Helpers;

internal static class EndfieldLauncher
{
    internal static bool Supports(string iconName) =>
        iconName is "Endfield" or "BiliEndfield" or "GlobalEndfield" or "PlayEndfield";

    internal static async Task<Process> StartAsync(string gamePath, string arguments, string iconName)
    {
        // The legacy Games.exe download/cache route is disabled. The worker stages
        // a signed game file locally and owns its lifetime independently of the UI.
        var start = new ProcessStartInfo(Environment.ProcessPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true
        };
        if (string.Equals(Path.GetFileNameWithoutExtension(Environment.ProcessPath),
                "dotnet", StringComparison.OrdinalIgnoreCase))
            start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory,
                typeof(EndfieldLauncher).Assembly.GetName().Name + ".dll"));
        start.ArgumentList.Add(EndfieldLaunchWorker.Command);
        start.ArgumentList.Add(Path.GetFullPath(gamePath));
        start.ArgumentList.Add(arguments ?? "");

        Process worker;
        try
        {
            worker = Process.Start(start) ?? throw new InvalidOperationException("无法创建启动辅助进程。");
        }
        catch (Exception ex)
        {
            LogHelper.LogError(ex, "Endfield worker unavailable; using ordinary launch");
            return EndfieldLaunchWorker.StartDirectly(gamePath, arguments);
        }

        using (worker)
        {
            // Acknowledgement means the worker already owns both the game and
            // its parent. Closing the UI or this handle cannot terminate either.
            string reply = await worker.StandardOutput.ReadLineAsync().ConfigureAwait(false);
            if (reply == null || !reply.StartsWith("PID:", StringComparison.Ordinal) ||
                !int.TryParse(reply.AsSpan(4), out int processId))
                throw new InvalidOperationException(reply ?? "终末地启动辅助进程未返回游戏进程。");
            var process = Process.GetProcessById(processId);
            if (process.HasExited)
            {
                process.Dispose();
                throw new InvalidOperationException("终末地进程在启动过程中退出。");
            }
            LogHelper.Log($"Endfield launch: channel={iconName}, process={process.Id}");
            return process;
        }
    }
}
