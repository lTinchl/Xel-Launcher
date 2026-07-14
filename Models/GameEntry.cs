using System.Collections.Generic;

namespace XelLauncher.Models;

public sealed class GameEntry
{
    public string Name { get; set; } = "";
    public string IconName { get; set; } = "";
    public string RootPath { get; set; } = "";
    public bool SyncLaunchEnabled { get; set; }
    public List<SyncApp> SyncApps { get; set; } = [];
    public string SessionToken { get; set; } = "";
    public bool AccountSwitchEnabled { get; set; }
    public bool CustomLaunchArgsEnabled { get; set; }
    public string CustomLaunchArgs { get; set; } = "";
    public string LocalVersion { get; set; } = "";
}

public sealed class SyncApp
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public string Args { get; set; } = "";
}
