# 软件更新功能设计文档

**日期：** 2026-04-15  
**版本：** XelLauncher v0.1.5+  
**仓库：** https://github.com/lTinchl/Xel-Launcher

---

## 概述

在设置页面的"软件更新"面板中实现完整的更新检查与下载功能，支持安装版和便携版两种下载方式，分流逻辑确保网络不佳时可跳转网盘备用链接。

---

## 架构总览

### 新增 / 修改的文件

| 文件 | 操作 | 说明 |
|------|------|------|
| `Helpers/UpdateHelper.cs` | 新增 | 核心更新逻辑 |
| `Forms/Setting.Designer.cs` | 修改 | 填充 panelUpdate 的 UI 控件 |
| `Forms/Setting.cs` | 修改 | 绑定更新 UI 逻辑 |
| `Program.cs` | 修改 | 启动时异步静默检查更新 |
| `Models/AppConfig.cs` | 无需改动 | `LastNotifiedVersion` 字段已存在 |

---

## UI 布局（panelUpdate）

从上到下三个区域：

### ① 版本信息区
- 左侧：当前版本（从 `Assembly` 或 csproj `<Version>` 读取，格式 `v0.1.5`）
- 右侧：最新版本（检查后填入，初始显示"—"）
- 「检查更新」按钮（检查中时禁用并显示"检查中..."）

### ② 更新日志区
- 只读文本框展示 GitHub Release 的 `body` 内容
- 无新版本时显示"已是最新版本"
- 有新版本时显示 Changelog

### ③ 下载区（有新版本时才显示）
- 两个按钮：**[⬇ 下载安装版]** 和 **[⬇ 下载便携版]**
- 进度条 + 进度文字（如 `1.2 MB / 2.6 MB  45%`）
- 下载失败时按钮变为 **[打开网盘下载页]**

---

## 核心逻辑流程

### UpdateHelper.cs 职责
- `CheckAsync()` → 调用 GitHub API，返回 `UpdateInfo`（含最新版本号、Changelog、两个 Asset 的下载 URL）
- `DownloadAsync(url, progress, cancellationToken)` → 流式下载，回调进度
- 版本比较使用 `System.Version` 类型，稳健解析 `v0.1.5` 格式

### GitHub API
```
GET https://api.github.com/repos/lTinchl/Xel-Launcher/releases/latest
User-Agent: XelLauncher/{version}
```
解析字段：
- `tag_name` → 最新版本号
- `body` → 更新日志
- `assets[].name` + `assets[].browser_download_url` → 匹配安装版/便携版

Asset 命名规则：
- 安装版：`XelLauncher-{version}-Setup.exe`（匹配 `-Setup.exe` 后缀）
- 便携版：`XelLauncher.v{version}-Portable.zip`（匹配 `-Portable.zip` 后缀）

### 启动时静默检查（Program.cs）
1. 主窗口加载完成后 `Task.Run` 异步调用 `UpdateHelper.CheckAsync()`
2. 若发现新版本 且 版本号 ≠ `config.LastNotifiedVersion`
3. → 在主窗口通过 `AntdUI.Notification` 或托盘气泡提示"有新版本 vX.X.X"
4. → 更新 `LastNotifiedVersion` 并保存 config，避免重复提醒同一版本

### 手动检查更新（Setting.cs）
1. 点击「检查更新」→ 按钮禁用，显示"检查中..."
2. 调用 `UpdateHelper.CheckAsync()`
3. 无新版 → 提示"已是最新版本"
4. 有新版 → 展示 Changelog + 显示下载区

### 下载流程

**安装版（.exe）：**
1. 下载到 `%TEMP%\XelLauncher_Update\XelLauncher-{version}-Setup.exe`
2. 显示进度条
3. 下载完成 → 写临时 `update.bat`（内容：`TIMEOUT 2 && start "" "{installer_path}" && exit`）
4. `Process.Start` 启动 bat → `Application.Exit()` 退出软件
5. 安装程序接管后续

**便携版（.zip）：**
1. 显示 `SaveFileDialog`，默认文件名 `XelLauncher.v{version}-Portable.zip`，过滤器 `*.zip`
2. 用户取消 → 不做任何操作
3. 用户确认 → 下载到用户指定路径
4. 显示进度条
5. 下载完成 → 打开文件所在文件夹（`explorer.exe /select,{path}`）
6. 临时目录无残留文件

**分流逻辑（下载失败时）：**
- 捕获 `HttpRequestException` / 超时异常
- 清理临时文件
- 恢复下载按钮
- 显示「打开网盘下载页」按钮，点击后 `Process.Start` 打开浏览器
- 网盘链接：`TODO`（后续替换为真实链接）

---

## 错误处理 & 边界情况

| 情况 | 处理方式 |
|------|----------|
| 无网络 / GitHub API 超时 | 静默失败（启动检查）；手动检查时提示"检查失败，请检查网络" |
| GitHub API 限速（403） | 同上，提示"检查失败" |
| Asset 文件名在 Release 中找不到 | 跳转网盘备用链接 |
| 下载中途断开 | 清理临时文件，恢复按钮，提示"下载失败"并显示网盘按钮 |
| 版本号格式异常（解析失败） | 捕获异常，静默跳过，不崩溃 |
| 用户重复点击下载 | 下载进行中时禁用按钮 |
| 便携版用户在 SaveFileDialog 取消 | 不下载，不报错，静默返回 |

---

## 数据模型

```csharp
public class UpdateInfo
{
    public string LatestVersion { get; set; }      // 如 "0.1.6"
    public string Changelog { get; set; }           // Release body
    public string SetupDownloadUrl { get; set; }    // 安装版下载链接
    public string PortableDownloadUrl { get; set; } // 便携版下载链接
    public string ReleasePageUrl { get; set; }      // GitHub Release 页面链接
}
```

---

## 备注

- 网盘备用链接目前为 `TODO`，待确定后替换
- `User-Agent` header 为 GitHub API 必填项，否则返回 403
- bat 脚本用 `TIMEOUT /T 2 /NOBREAK` 等待主程序退出，窗口最小化运行
