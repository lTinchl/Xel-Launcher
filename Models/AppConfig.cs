using System;
using System.Collections.Generic;

namespace XelLauncher.Models;

public sealed class CachedGameStatus
{
    public bool IsInstalled { get; set; }
    public bool HasUpdate { get; set; }
    public string LocalVersion { get; set; } = "";
    public string RemoteVersion { get; set; } = "";
    public string InstallPath { get; set; } = "";
}

public sealed class AppUpdateState
{
    public string LastCheckedAtUtc { get; set; } = "";
    public bool HasUpdate { get; set; }
    public string LatestVersion { get; set; } = "";
    public string Changelog { get; set; } = "";
    public DateTimeOffset? PublishedAt { get; set; }
    public string SetupDownloadUrl { get; set; } = "";
    public long? SetupSizeBytes { get; set; }
    public string PortableDownloadUrl { get; set; } = "";
    public long? PortableSizeBytes { get; set; }
    public string ReleasePageUrl { get; set; } = "";
    public bool DisableReminder { get; set; }
    public string SkippedVersion { get; set; } = "";
}

public sealed class CachedLauncherBanner
{
    public string ImageUrl { get; set; } = "";
    public string JumpUrl { get; set; } = "";
}

public sealed class CachedLauncherNotice
{
    public string Category { get; set; } = "";
    public string Title { get; set; } = "";
    public string Date { get; set; } = "";
    public string JumpUrl { get; set; } = "";
}

public sealed class CachedLauncherNoticeContent
{
    public List<CachedLauncherBanner> Banners { get; set; } = [];
    public List<CachedLauncherNotice> Notices { get; set; } = [];
    public string CachedAtUtc { get; set; } = "";
}

public sealed class AppConfig
{
    public List<GameEntry> Games { get; set; } =
    [
        new() { Name = "明日方舟", IconName = "Arknights" },
        new() { Name = "明日方舟 B服", IconName = "BiliArknights" },
        new() { Name = "明日方舟：终末地", IconName = "Endfield" },
        new() { Name = "明日方舟：终末地 B服", IconName = "BiliEndfield" },
        new() { Name = "明日方舟：终末地国际服", IconName = "GlobalEndfield" },
        new() { Name = "明日方舟：终末地 Google Play", IconName = "PlayEndfield" },
    ];

    public Dictionary<string, string> Accounts { get; set; } = [];
    public List<string> AccountOrder { get; set; } = [];
    public string DefaultAccount { get; set; } = "";
    public HashSet<string> DisabledAccounts { get; set; } = [];
    public Dictionary<string, string> EndfieldAccounts { get; set; } = [];
    public List<string> EndfieldAccountOrder { get; set; } = [];
    public string EndfieldDefaultAccount { get; set; } = "";
    public HashSet<string> EndfieldDisabledAccounts { get; set; } = [];
    public Dictionary<string, string> GlobalEndfieldAccounts { get; set; } = [];
    public List<string> GlobalEndfieldAccountOrder { get; set; } = [];
    public string GlobalEndfieldDefaultAccount { get; set; } = "";
    public HashSet<string> GlobalEndfieldDisabledAccounts { get; set; } = [];
    public string LastNotifiedVersion { get; set; } = "";
    public string LastReadStartupAnnouncementVersion { get; set; } = "";
    public bool SkylandSignEnabled { get; set; }
    public bool SkylandStartupSignEnabled { get; set; }
    public string SkylandLastAutoSignDate { get; set; } = "";
    public string SkylandTokensEncrypted { get; set; } = "";
    public List<string> SkylandTokens { get; set; } = [];
    public bool SkportSignEnabled { get; set; }
    public bool SkportStartupSignEnabled { get; set; }
    public string SkportLastAutoSignDate { get; set; } = "";
    public string SkportTokensEncrypted { get; set; } = "";
    public List<string> SkportTokens { get; set; } = [];
    public bool ShowTrayIcon { get; set; }
    public bool MinimizeToTray { get; set; }
    public bool AutoLaunchOfficial { get; set; }
    public bool AutoLaunchBilibili { get; set; }
    public string PrimaryColor { get; set; } = "#1677FF";
    public string BackgroundColor { get; set; } = "#FFFFFF";
    public bool CloseAfterLaunch { get; set; }
    public bool HideToTrayOnLaunch { get; set; }
    public string Language { get; set; } = "";
    public bool UseExternalBrowser { get; set; }
    public bool CheckGameUpdates { get; set; }
    public bool ArchiveLauncherImages { get; set; }
    public string ThemeMode { get; set; } = "dark";
    public bool UseHardLink { get; set; } = true;
    public Dictionary<string, CachedGameStatus> GameStatusCache { get; set; } = [];
    public Dictionary<string, CachedLauncherNoticeContent> LauncherNoticeCache { get; set; } = [];
    public AppUpdateState UpdateState { get; set; } = new();
}
