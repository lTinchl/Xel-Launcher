using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace XelLauncher.Helpers
{
    public static class GameLauncher
    {
        public static string GetPayloadZipPath(string iconName)
        {
            string exeDir = AppContext.BaseDirectory;
            string zipName = iconName switch
            {
                "BiliArknights"  => "ArkBilibili.zip",
                "Arknights"      => "ArkOffiicial.zip",
                "BiliEndfield"   => "EndBilibili.zip",
                "GlobalEndfield" => "EndGlobal.zip",
                "Endfield"       => "EndOfficial.zip",
                _ => null
            };
            if (zipName == null) return null;
            return Path.Combine(exeDir, "load", zipName);
        }

        public static async Task ExtractAndReplace(string rootPath, string zipPath, Action<string> onProgress, bool isEndfield = false)
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "XelLauncher_payload_" + Guid.NewGuid().ToString("N"));
            try
            {
                onProgress("解压文件中...");
                await Task.Run(() => ZipFile.ExtractToDirectory(zipPath, tempDir, true));

                onProgress("结束游戏进程...");
                await KillArknightsProcesses(isEndfield);

                onProgress("文件替换中...");
                await CopyDirectory(tempDir, rootPath);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    await Task.Run(() => Directory.Delete(tempDir, true));
            }
        }

        public static void StartArknights(string rootPath, bool isEndfield = false)
        {
            string exeName = isEndfield ? "Endfield.exe" : "Arknights.exe";
            string exePath = Path.Combine(rootPath, exeName);
            if (!File.Exists(exePath)) throw new Exception($"未找到 {exeName}");

            Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = rootPath,
                UseShellExecute = true
            });
        }

        public static void StartMAA(string exePath)
        {
            if (!File.Exists(exePath))
                throw new Exception("未找到 MAA.exe");

            Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = Path.GetDirectoryName(exePath)!,
                UseShellExecute = true
            });
        }

        // 判断 pid 的祖先链（向上追溯）中是否包含 ancestorPids 中的任意一个
        private static bool IsDescendantOf(int pid, System.Collections.Generic.HashSet<int> ancestorPids)
        {
            int current = pid;
            // 最多向上追溯 10 层，防止进程树出现环路死循环
            for (int depth = 0; depth < 10; depth++)
            {
                int parent = GetParentProcessId(current);
                if (parent <= 0) return false;
                if (ancestorPids.Contains(parent)) return true;
                current = parent;
            }
            return false;
        }

        public static async Task KillArknightsProcesses(bool isEndfield = false)
        {
            string mainName = isEndfield ? "Endfield" : "Arknights";
            var mainProcs = Process.GetProcessesByName(mainName);
            var mainPids = new System.Collections.Generic.HashSet<int>(mainProcs.Select(p => p.Id));

            // 先杀祖先链中包含主游戏进程的 PlatformProcess（兼容中间进程层级）
            foreach (var proc in Process.GetProcessesByName("PlatformProcess"))
            {
                using (proc)
                {
                    try
                    {
                        if (IsDescendantOf(proc.Id, mainPids))
                        {
                            proc.Kill();
                            proc.WaitForExit();
                        }
                    }
                    catch { }
                }
            }

            // 再杀主游戏进程
            foreach (var proc in mainProcs)
            {
                using (proc)
                {
                    try { proc.Kill(); proc.WaitForExit(); } catch { }
                }
            }

            // 等待 Windows 完全释放文件句柄
            await Task.Delay(1500);
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct PROCESS_BASIC_INFORMATION
        {
            public IntPtr Reserved1;
            public IntPtr PebBaseAddress;
            public IntPtr Reserved2_0;
            public IntPtr Reserved2_1;
            public IntPtr UniqueProcessId;
            public IntPtr InheritedFromUniqueProcessId;
        }

        [System.Runtime.InteropServices.DllImport("ntdll.dll")]
        private static extern int NtQueryInformationProcess(IntPtr hProcess, int processInformationClass, ref PROCESS_BASIC_INFORMATION processInformation, int processInformationLength, out int returnLength);

        private static int GetParentProcessId(int pid)
        {
            try
            {
                using var proc = Process.GetProcessById(pid);
                var pbi = new PROCESS_BASIC_INFORMATION();
                int status = NtQueryInformationProcess(proc.Handle, 0, ref pbi, System.Runtime.InteropServices.Marshal.SizeOf(pbi), out _);
                if (status != 0) return -1;
                return pbi.InheritedFromUniqueProcessId.ToInt32();
            }
            catch { return -1; }
        }

        public static async Task BackupAccount(string accountId)
        {
            string sdkPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "AppData", "LocalLow", "Hypergryph", "Arknights"
            );

            string target = Path.Combine(ConfigHelper.AccountBackupDir, accountId);

            var sdkDir = Directory.GetDirectories(sdkPath, "sdk_data_*").FirstOrDefault();
            if (sdkDir == null) return;

            if (Directory.Exists(target)) Directory.Delete(target, true);
            await CopyDirectory(sdkDir, target);
        }

        public static async Task RestoreAccount(string accountId)
        {
            string backupDir = Path.Combine(ConfigHelper.AccountBackupDir, accountId);
            if (!Directory.Exists(backupDir))
                throw new Exception($"账号备份不存在，请先点击「保存账号」记录该账号。");

            string sdkPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "AppData", "LocalLow", "Hypergryph", "Arknights"
            );

            var sdkDir = Directory.GetDirectories(sdkPath, "sdk_data_*").FirstOrDefault();
            if (sdkDir == null)
                throw new Exception("未找到 sdk_data_* 目录，请先启动一次游戏。");

            if (Directory.Exists(sdkDir)) Directory.Delete(sdkDir, true);
            await CopyDirectory(backupDir, sdkDir);
        }

        public static async Task BackupEndfieldAccount(string accountId)
        {
            string sdkPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "AppData", "LocalLow", "Hypergryph", "Endfield"
            );

            string target = Path.Combine(ConfigHelper.EndAccountBackupDir, accountId);

            var sdkDir = Directory.GetDirectories(sdkPath, "sdk_data_*").FirstOrDefault();
            if (sdkDir == null) return;

            if (Directory.Exists(target)) Directory.Delete(target, true);
            await CopyDirectory(sdkDir, target);
        }

        public static async Task RestoreEndfieldAccount(string accountId)
        {
            string backupDir = Path.Combine(ConfigHelper.EndAccountBackupDir, accountId);
            if (!Directory.Exists(backupDir))
                throw new Exception($"账号备份不存在，请先点击「保存账号」记录该账号。");

            string sdkPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "AppData", "LocalLow", "Hypergryph", "Endfield"
            );

            var sdkDir = Directory.GetDirectories(sdkPath, "sdk_data_*").FirstOrDefault();
            if (sdkDir == null)
                throw new Exception("未找到 sdk_data_* 目录，请先启动一次游戏。");

            if (Directory.Exists(sdkDir)) Directory.Delete(sdkDir, true);
            await CopyDirectory(backupDir, sdkDir);
        }

        public static async Task CopyDirectory(string sourceDir, string targetDir, int maxRetries = 5)
        {
            sourceDir = Path.GetFullPath(sourceDir).TrimEnd(Path.DirectorySeparatorChar);
            targetDir = Path.GetFullPath(targetDir).TrimEnd(Path.DirectorySeparatorChar);

            Directory.CreateDirectory(targetDir);

            foreach (var file in Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories))
            {
                string relativePath = file.Substring(sourceDir.Length + 1);
                string destFile = Path.Combine(targetDir, relativePath);

                Directory.CreateDirectory(Path.GetDirectoryName(destFile)!);

                for (int i = 0; i < maxRetries; i++)
                {
                    try
                    {
                        File.Copy(file, destFile, true);
                        break;
                    }
                    catch (IOException) when (i < maxRetries - 1)
                    {
                        await Task.Delay(1000);
                    }
                }
            }
        }
    }
}
