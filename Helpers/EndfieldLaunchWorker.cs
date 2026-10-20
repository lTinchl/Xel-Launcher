using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace XelLauncher.Helpers;

internal static class EndfieldLaunchWorker
{
    internal const string Command = "--endfield-launch-worker";

    internal static void Run(string[] args)
    {
        try
        {
            if (args.Length != 3)
                throw new ArgumentException("终末地启动辅助进程参数无效。");
            RunGameAsync(Path.GetFullPath(args[1]), args[2], game =>
            {
                Console.Out.WriteLine($"PID:{game.Id}");
                Console.Out.Flush();
            }).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            LogHelper.LogError(ex, "Endfield launch worker");
            Environment.ExitCode = 1;
            try
            {
                Console.Out.WriteLine("ERROR: " + ex.Message.Replace('\r', ' ').Replace('\n', ' '));
                Console.Out.Flush();
            }
            catch (IOException) { }
        }
    }

    internal static async Task RunGameAsync(string gamePath, string arguments,
        Action<Process> started, Func<string, GameDecoyParentBridge> createBridge = null)
    {
        using var bridge = (createBridge ?? GameDecoyParentBridge.TryCreate)(gamePath);
        Process game = null;
        if (bridge != null)
        {
            try
            {
                game = bridge.StartGame(Path.Combine(gamePath, "Endfield.exe"), arguments, gamePath);
            }
            catch (Exception ex)
            {
                LogHelper.LogError(ex, "Endfield parent bridge failed; using ordinary launch");
                bridge.Dispose();
            }
        }

        using (game ??= StartDirectly(gamePath, arguments))
        {
            // The UI can close its pipe at any time, including during startup.
            // A successfully created game must retain its parent in that case.
            try { started(game); }
            catch (IOException) { }
            await game.WaitForExitAsync().ConfigureAwait(false);
        }
    }

    internal static Process StartDirectly(string gamePath, string arguments)
    {
        LogHelper.Log("Endfield: starting through the ordinary launch path.");
        return Process.Start(new ProcessStartInfo
        {
            FileName = Path.Combine(gamePath, "Endfield.exe"),
            Arguments = arguments ?? "",
            WorkingDirectory = gamePath,
            UseShellExecute = true
        }) ?? throw new InvalidOperationException("无法创建终末地进程。");
    }
}
