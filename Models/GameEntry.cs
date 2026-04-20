using System.Collections.Generic;

public class GameEntry
{
    public string Name { get; set; }
    public string IconName { get; set; }
    public string RootPath { get; set; }
    public bool SyncLaunchEnabled { get; set; } = false;          // Switch 状态
    public List<SyncApp> SyncApps { get; set; } = new();          // 联动列表
    public bool AccountSwitchEnabled { get; set; } = false;       // 账号切换开关
    public bool CustomLaunchArgsEnabled { get; set; } = false;    // 自定义启动参数开关
    public string CustomLaunchArgs { get; set; } = "";             // 自定义启动参数

    /// <summary>Returns the localized display name based on current language.</summary>
    public string GetLocalizedName()
    {
        bool isEnglish = AntdUI.Localization.CurrentLanguage.StartsWith("en");
        return IconName switch
        {
            "Arknights"      => isEnglish ? "Arknights (Official)" : "明日方舟（官服）",
            "BiliArknights"  => isEnglish ? "Arknights (Bilibili)" : "明日方舟（B服）",
            "Endfield"       => isEnglish ? "Endfield (Official)"  : "明日方舟：终末地（官服）",
            "BiliEndfield"   => isEnglish ? "Endfield (Bilibili)"  : "明日方舟：终末地（B服）",
            "GlobalEndfield" => isEnglish ? "Endfield (Global)"    : "明日方舟：终末地（国际服）",
            _                => Name,
        };
    }
}

public class SyncApp
{
    public string Name { get; set; }       // 显示名
    public string Path { get; set; }       // exe 完整路径
    public string Args { get; set; } = ""; // 启动参数（可选）
}