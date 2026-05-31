<div align="center">
<img width="128" height="128" alt="logo" src="https://github.com/user-attachments/assets/448a8cf3-a270-4d82-810b-04119914a32a" />

# Xel Launcher

English | [中文](./README.zh.md)

[English Wiki](./docs/wiki.en-US.md) | [中文 Wiki](./docs/wiki.zh-CN.md)

![C#](https://img.shields.io/badge/C%23-13-square)
![Platform](https://img.shields.io/badge/platform-Windows-blue)
[![License](https://img.shields.io/badge/license-Apache%202.0-4EB1BA.svg?style=flat-square)](http://www.apache.org/licenses/LICENSE-2.0)
[![NuGet](https://img.shields.io/nuget/v/AntdUI.svg?style=flat-square&label=AntdUI&logo=nuget)](https://www.nuget.org/packages/AntdUI)<br>
![Stars](https://img.shields.io/github/stars/lTinchl/Xel-Launcher)
![Downloads](https://img.shields.io/github/downloads/lTinchl/Xel-Launcher/total)

Built on [AntdUI](https://github.com/AntdUI/AntdUI), supporting multi-server switching for Arknights and Arknights: Endfield, with account management and more.


</div>

## File Structure
```
├── Forms/                   # UI windows/forms
├── Helpers/                 # Utilities (config, launch, logging, etc.)
├── Models/                  # Data models
├── Properties/              # Resources and publish configuration
├── Resources/               # Icons, images, and other static assets
├── load/                    # Payload archives for server switching
│   ├── ArkBilibili          # Arknights Bilibili server
│   ├── ArkOfficial          # Arknights official server
│   ├── EndBilibili          # Endfield Bilibili server
│   ├── EndGlobal            # Endfield global server
│   └── EndOfficial          # Endfield official server
│   └── EndPlay              # Endfield Google Play server
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
- Server switching
  - [x] Arknights official server
  - [x] Arknights Bilibili server
  - [x] Arknights: Endfield official server
  - [x] Arknights: Endfield Bilibili server
  - [x] Arknights: Endfield global server
  - [x] Arknights: Endfield global server (Google Play)
- [x] Seamless multi-account switching for Endfield and Arknights
- [x] Game download, update, and version detection
- [x] Companion app launching
- [x] Custom launch parameters
- [x] Skyland Sign

## Special Thanks

- UI Framework: [AntdUI](https://github.com/AntdUI/AntdUI)
- Concept reference: [▶️2分钟教会你明日方舟PC端官服-B服互转](https://www.bilibili.com/video/BV1VHFRzoE7T/?spm_id_from=333.337.search-card.all.click&vd_source=ec94b95a235413f9ad5d2ccb4597ac9f)
- Seamless account switching: [▶️明日方舟PC端多账号无感切换教程](https://www.bilibili.com/video/BV1dgcsz1EjM/?share_source=copy_web&vd_source=5d993adf522c16219536a4e8a61f8484)
- Game download & update: [[Hi3Helper.Plugin.Arknights](https://github.com/misaka10843/Hi3Helper.Plugin.Arknights)] · [[Hi3Helper.Plugin.Endfield](https://github.com/misaka10843/Hi3Helper.Plugin.Endfield)]
- [Prts Wiki](https://prts.wiki/id/1) · [明日方舟工具箱](https://arkntools.app/) · [明日方舟一图流](https://ark.yituliu.cn/)
- [Warfarin Wiki](https://endfieldtools.dev/) · [Endfield Tools](https://endfieldtools.dev/) · [终末地一图流](https://ef.yituliu.cn/)

## Disclaimer
- This project is an unofficial tool and has no affiliation with Hypergryph or any of its affiliated organizations/groups/studios. Game images and data copyrights belong to their respective owners.
- This tool will not upload your account files or login credentials to the internet; it only works on the local client. Please use it with caution if you are concerned about account security. Users are solely responsible for any consequences arising from the use of this tool.
