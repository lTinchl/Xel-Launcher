using System.Collections.Generic;

public class GameEntry
{
    public string Name { get; set; }
    public string IconName { get; set; }
    public string RootPath { get; set; }
    public bool SyncLaunchEnabled { get; set; } = false;          // Switch 状态
    public List<SyncApp> SyncApps { get; set; } = new();          // 联动列表
}

public class SyncApp
{
    public string Name { get; set; }       // 显示名
    public string Path { get; set; }       // exe 完整路径
    public string Args { get; set; } = ""; // 启动参数（可选）
}