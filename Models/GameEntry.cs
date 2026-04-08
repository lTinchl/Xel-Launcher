using System.Collections.Generic;

public class GameEntry
{
    public string Name { get; set; }
    public string IconName { get; set; }
    public string RootPath { get; set; }
    public bool SyncLaunchEnabled { get; set; } = false;          // Switch 状态
    public List<SyncApp> SyncApps { get; set; } = new();          // 联动列表

    /// <summary>Returns the localized display name based on current language.</summary>
    public string GetLocalizedName()
    {
        // Only translate when English is active
        if (!AntdUI.Localization.CurrentLanguage.StartsWith("en"))
            return Name;
        return IconName switch
        {
            "Arknights"      => "Arknights (Official)",
            "BiliArknights"  => "Arknights (Bilibili)",
            "Endfield"       => "Endfield (Official)",
            "BiliEndfield"   => "Endfield (Bilibili)",
            "GlobalEndfield" => "Endfield (Global)",
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