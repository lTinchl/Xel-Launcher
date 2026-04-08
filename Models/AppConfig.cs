using System;
using System.Collections.Generic;

namespace XelLauncher.Models
{
    public class AppConfig
    {
        public List<GameEntry> Games { get; set; } = new List<GameEntry>
        {
            new GameEntry { Name = "明日方舟",       IconName = "Arknights" },
            new GameEntry { Name = "明日方舟(B服)",   IconName = "BiliArknights" },
            new GameEntry { Name = "终末地",          IconName = "Endfield" },
            new GameEntry { Name = "终末地(B服)",     IconName = "BiliEndfield" },
            new GameEntry { Name = "终末地(国际服)",  IconName = "GlobalEndfield" },
        };
        public List<MaaEntry> MaaList { get; set; } = new List<MaaEntry>();

        public Dictionary<string, string> Accounts { get; set; } = new Dictionary<string, string>();
        public List<string> AccountOrder { get; set; } = new List<string>();
        // 方舟官服账号管理
        public string DefaultAccount { get; set; } = "";                    // 默认账号 ID（如 "A1"）
        public HashSet<string> DisabledAccounts { get; set; } = new HashSet<string>(); // 禁用的账号 ID

        // 终末地官服账号管理
        public Dictionary<string, string> EndfieldAccounts { get; set; } = new Dictionary<string, string>();
        public List<string> EndfieldAccountOrder { get; set; } = new List<string>();
        public string EndfieldDefaultAccount { get; set; } = "";
        public HashSet<string> EndfieldDisabledAccounts { get; set; } = new HashSet<string>();
        public string LastNotifiedVersion { get; set; } = "";               // 上次通知的版本号
        public bool ShowTrayIcon { get; set; } = false;                     // 是否显示托盘图标
        public bool MinimizeToTray { get; set; } = false;                   // 关闭主窗口时是否最小化到托盘
        public bool AutoLaunchOfficial { get; set; } = false;               // 启动时自动打开官服
        public bool AutoLaunchBilibili { get; set; } = false;               // 启动时自动打开B服
        public string PrimaryColor { get; set; } = "#1677FF";               // 色板工具选择的主色
        public string BackgroundColor { get; set; } = "#FFFFFF";              // 窗口背景色
        public bool CloseAfterLaunch { get; set; } = false;               // 启动游戏后关闭软件
        public bool HideToTrayOnLaunch { get; set; } = false;             // 启动游戏后隐藏至托盘，游戏关闭后恢复
        public string MAA_Official { get; set; } = "";                      // MAA
        public string MAA_Bilibili { get; set; } = "";                      // MAA B 服
        public string Language { get; set; } = "";                          // 用户选择的语言 (zh-CN / en-US)

    }
}
