using System;

namespace XelLauncher.Forms
{
    internal static class StartupAnnouncementContent
    {
        public static string GetVersionTitle(string version) => $"v{NormalizeVersion(version)}";

        public static string DetailIntro => "以下是详细内容：";

        public static string FullChangelogTitle => "Full Changelog:";

        public static string Tip =>
            "提示：如果不想让启动器主动检查游戏更新，可以到 设置 > 软件 > 检查游戏更新 关闭";

        public static AnnouncementSection[] Highlights =>
        [
            new(
                "终末地更新器升级",
                "终末地更新插件切换到 Hi3Helper.Plugin.Hypergryph 1.0.3，适配新版增量更新流程，减少异常增量包反复更新的风险。"),
            new(
                "游戏更新检查开关",
                "设置页新增“检查游戏更新”开关。关闭后启动器不会主动检查游戏更新，也不会因为游戏版本状态反复提示更新。"),
            new(
                "插件依赖与构建修正",
                "修正 Hypergryph.Core、Endfield、Arknights、SevenZipExtractor 和 SharpHDiffPatch.Core 的项目引用与输出，确保调试构建可以生成所需 DLL。"),
            new(
                "启动公告",
                "重要变更会在启动时以版本信息弹窗展示。用户需要滚动阅读到末尾后，才可以关闭公告。"),
        ];

        public static string GetDetailHeader(string version) => $"{GetVersionTitle(version)}  (2026-06-05)";

        public static string[] ChangelogItems =>
        [
            "Hi3Helper.Plugin.Hypergryph 更新到 1.0.3",
            "新增设置项：检查游戏更新",
            "启动公告支持强制阅读完毕后关闭",
            "修复插件项目引用后默认 Debug 输出缺失的问题",
        ];

        private static string NormalizeVersion(string version)
        {
            if (string.IsNullOrWhiteSpace(version)) return "0.0.0";
            return version.Trim().TrimStart('v', 'V');
        }

        internal sealed record AnnouncementSection(string Title, string Body);
    }
}
