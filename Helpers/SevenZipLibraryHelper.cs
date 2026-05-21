using System;
using System.IO;
using System.Reflection;

namespace XelLauncher.Helpers
{
    public static class SevenZipLibraryHelper
    {
        private const string ResourceName = "XelLauncher.Lib.7z.dll";

        public static void EnsureAvailable()
        {
            try
            {
                string appDir = Path.GetDirectoryName(Environment.ProcessPath ?? AppContext.BaseDirectory)
                    ?? AppContext.BaseDirectory;
                string libDir = Path.Combine(appDir, "Lib");
                string targetPath = Path.Combine(libDir, "7z.dll");

                if (File.Exists(targetPath) && new FileInfo(targetPath).Length > 0)
                    return;

                using Stream resourceStream = Assembly.GetExecutingAssembly()
                    .GetManifestResourceStream(ResourceName);
                if (resourceStream == null)
                {
                    LogHelper.Log($"7z.dll resource not found: {ResourceName}");
                    return;
                }

                Directory.CreateDirectory(libDir);
                using var fileStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None);
                resourceStream.CopyTo(fileStream);
                LogHelper.Log($"Restored 7z.dll to {targetPath}");
            }
            catch (Exception ex)
            {
                LogHelper.LogError(ex, "Ensure 7z.dll");
            }
        }
    }
}
