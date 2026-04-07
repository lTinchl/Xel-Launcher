<div align="center">
<img width="128" height="128" alt="logo" src="https://github.com/user-attachments/assets/448a8cf3-a270-4d82-810b-04119914a32a" />

# Xel Launcher

English | [中文](./README.zh.md)

![C#](https://img.shields.io/badge/C%23-13-square)
![Platform](https://img.shields.io/badge/platform-Windows-blue)
[![License](https://img.shields.io/badge/license-Apache%202.0-4EB1BA.svg?style=flat-square)](http://www.apache.org/licenses/LICENSE-2.0)
[![NuGet](https://img.shields.io/nuget/v/AntdUI.svg?style=flat-square&label=AntdUI&logo=nuget)](https://www.nuget.org/packages/AntdUI)<br>
![Stars](https://img.shields.io/github/stars/lTinchl/XelLauncher)
![Downloads](https://img.shields.io/github/downloads/lTinchl/XelLauncher/total)

Built on [AntdUI](https://github.com/AntdUI/AntdUI), supporting multi-server switching for Arknights and Arknights: Endfield, with account management, MAA integration, and more.

</div>

## File Structure
```
├── Forms/                   # UI windows/forms
├── Helpers/                 # Utilities (config, launch, logging, etc.)
├── Models/                  # Data models
├── Properties/              # Resources and publish configuration
├── Resources/               # Icons, images, and other static assets
├── load/                    # Payload archives for server switching
│   ├── ArkBilibili.zip      # Arknights Bilibili server
│   ├── ArkOfficial.zip      # Arknights official server
│   ├── EndBilibili.zip      # Endfield Bilibili server
│   ├── EndGlobal.zip        # Endfield global server
│   └── EndOfficial.zip      # Endfield official server
├── Program.cs               # Entry point
└── XelLauncher.csproj
```

## Important Notes
- ⚠️ Do **not** place the game's root directory on the same disk as Tencent ACE Anti-Cheat. Doing so will prevent the launcher from having read access to the game directory, making server switching impossible.
- Configuration is cached at `C:\Users\<username>\AppData\Local\XelLauncher\config.json`. Delete this file to reset the application.
- Account backups are saved by default to `C:\Users\<username>\AppData\Local\XelLauncher\AccountBackups`. Delete this folder to reset all accounts.

## How to Use
Download the latest installer from [Releases](https://github.com/lTinchl/Xel-Launcher/releases), install and open the application, then follow the on-screen prompts to add your game path. Server switching will then be available.

## Supported Features
- [x] Arknights Official / Bilibili server switching
- [x] Arknights: Endfield Official / Bilibili / Global server switching
- [x] Seamless multi-account switching for Endfield and Arknights official servers
- [x] Companion app launching
- [x] Arknights Wiki integration
- [x] Arknights: Endfield Wiki integration

## Screenshots
<img width="1280" height="760" alt="QQ_1775213815782" src="https://github.com/user-attachments/assets/b2f3b2e0-d0ac-480d-aaba-f7fee556baab" />

## Special Thanks

- UI Framework: [AntdUI](https://github.com/AntdUI/AntdUI)
- Concept reference: [▶️2分钟教会你明日方舟PC端官服-B服互转](https://www.bilibili.com/video/BV1VHFRzoE7T/?spm_id_from=333.337.search-card.all.click&vd_source=ec94b95a235413f9ad5d2ccb4597ac9f)
- Seamless account switching: [▶️明日方舟PC端多账号无感切换教程](https://www.bilibili.com/video/BV1dgcsz1EjM/?share_source=copy_web&vd_source=5d993adf522c16219536a4e8a61f8484)
- [Prts Wiki](https://prts.wiki/id/1) · [明日方舟工具箱](https://arkntools.app/) · [明日方舟一图流](https://ark.yituliu.cn/)
- [Warfarin Wiki](https://endfieldtools.dev/) · [Endfield Tools](https://endfieldtools.dev/) · [终末地一图流](https://ef.yituliu.cn/)

## Disclaimer
This tool does not upload your account files to the internet — it is used solely for local file replacement. Please use at your own discretion if you have concerns about account security. The developer bears no responsibility for any consequences resulting from the use of this tool.
