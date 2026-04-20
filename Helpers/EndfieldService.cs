using System;
using System.Threading;
using System.Threading.Tasks;
using Hi3Helper.Plugin.Arknights.Management;
using Hi3Helper.Plugin.Arknights.Management.PresetConfig;
using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.Plugin.Core.Management.PresetConfig;
using Hi3Helper.Plugin.Endfield.Management;
using Hi3Helper.Plugin.Endfield.Management.PresetConfig;

namespace XelLauncher.Helpers
{
    public record GameStatus(bool IsInstalled, bool HasUpdate, string LocalVersion, string RemoteVersion);

    public class EndfieldService : IDisposable
    {
        private readonly PluginPresetConfigBase _preset;

        public EndfieldService(string iconName)
        {
            _preset = iconName switch
            {
                "Arknights"      => new ArknightsCnPresetConfig(),
                "BiliArknights"  => new ArknightsBiliPresetConfig(),
                "Endfield"       => new EndfieldCnPresetConfig(),
                "BiliEndfield"   => new EndfieldBiliPresetConfig(),
                "GlobalEndfield" => new EndfieldGlobalPresetConfig(),
                _ => throw new ArgumentException($"未知游戏类型：{iconName}", nameof(iconName))
            };
        }

        /// <summary>
        /// 检查游戏状态：是否已安装、是否有更新、本地/远端版本号。
        /// API 请求失败时返回 null。
        /// </summary>
        public async Task<GameStatus?> CheckStatusAsync(string installPath, CancellationToken ct = default)
        {
            int result = _preset.GameManager switch
            {
                EndfieldGameManager  em => await em.CheckWithPathAsync(installPath, ct).ConfigureAwait(false),
                ArknightsGameManager am => await am.CheckWithPathAsync(installPath, ct).ConfigureAwait(false),
                _ => -1
            };

            if (result != 0) return null;

            var manager = _preset.GameManager!;
            manager.IsGameInstalled(out bool isInstalled);
            manager.IsGameHasUpdate(out bool hasUpdate);
            manager.GetCurrentGameVersion(out GameVersion localVer);
            manager.GetApiGameVersion(out GameVersion remoteVer);

            return new GameStatus(isInstalled, hasUpdate, localVer.ToString(), remoteVer.ToString());
        }

        /// <summary>
        /// 下载并安装/更新游戏。
        /// onProgress 参数：(状态, 已下载字节数, 总字节数)
        /// </summary>
        public async Task InstallOrUpdateAsync(
            string installPath,
            Action<InstallProgressState, long, long> onProgress,
            CancellationToken ct = default)
        {
            var manager   = _preset.GameManager  ?? throw new InvalidOperationException("GameManager 未初始化");
            var installer = _preset.GameInstaller ?? throw new InvalidOperationException("GameInstaller 未初始化");

            manager.SetGamePath(installPath);
            manager.IsGameInstalled(out bool isInstalled);

            var currentState = InstallProgressState.Preparing;

            InstallProgressDelegate progressDelegate = (in InstallProgress p) =>
                onProgress(currentState, p.DownloadedBytes, p.TotalBytesToDownload);

            InstallProgressStateDelegate stateDelegate = state =>
            {
                currentState = state;
                onProgress(state, 0, 0);
            };

            Task installTask = (isInstalled, installer) switch
            {
                (true,  EndfieldGameInstaller  ei) => ei.RunUpdateAsync(progressDelegate,  stateDelegate, ct),
                (false, EndfieldGameInstaller  ei) => ei.RunInstallAsync(progressDelegate, stateDelegate, ct),
                (true,  ArknightsGameInstaller ai) => ai.RunUpdateAsync(progressDelegate,  stateDelegate, ct),
                (false, ArknightsGameInstaller ai) => ai.RunInstallAsync(progressDelegate, stateDelegate, ct),
                _ => throw new InvalidOperationException($"不支持的 Installer 类型：{installer.GetType().Name}")
            };

            await installTask.ConfigureAwait(false);
        }

        public void Dispose()
        {
            _preset.GameInstaller?.Free();
            _preset.Dispose();
        }
    }
}
