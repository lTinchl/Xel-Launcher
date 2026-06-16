<h1 align="center">
  <br>
  <img width="128" height="128" alt="Xel Launcher logo" src="https://github.com/user-attachments/assets/448a8cf3-a270-4d82-810b-04119914a32a">
  <br>
  Xel Launcher
  <br>
</h1>

<h4 align="center">
  A Windows launcher for Arknights and Arknights: Endfield, built on top of <a href="https://github.com/AntdUI/AntdUI">AntdUI</a>.
</h4>

<p align="center">
  <a href="./README.md">English</a>
  <span> | </span>
  <a href="./README.zh.md">中文</a>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/C%23-13-square" alt="C#">
  <img src="https://img.shields.io/badge/platform-Windows-blue" alt="Platform">
  <a href="http://www.apache.org/licenses/LICENSE-2.0"><img src="https://img.shields.io/badge/license-Apache%202.0-4EB1BA.svg?style=flat-square" alt="License"></a>
  <a href="https://www.nuget.org/packages/AntdUI"><img src="https://img.shields.io/nuget/v/AntdUI.svg?style=flat-square&label=AntdUI&logo=nuget" alt="AntdUI NuGet"></a>
  <br>
  <img src="https://img.shields.io/github/stars/lTinchl/Xel-Launcher" alt="Stars">
  <img src="https://img.shields.io/github/downloads/lTinchl/Xel-Launcher/total" alt="Downloads">
</p>

<p align="center">
  <a href="#supported-features">Features</a>
  <span> | </span>
  <a href="#how-to-use">How To Use</a>
  <span> | </span>
  <a href="./docs/wiki.en-US.md">Document</a>
  <span> | </span>
  <a href="#project-structure">Project Structure</a>
  <span> | </span>
  <a href="#special-thanks">Special Thanks</a>
</p>

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

## How to Use

Download the latest installer from [Releases](https://github.com/lTinchl/Xel-Launcher/releases), install and open the application, then follow the on-screen prompts to add your game path. Server switching will then be available.

## Project Structure

```text
XelLauncher/
├── docs/                         # English and Chinese wiki documents
├── Forms/                        # WinForms pages, dialogs, and UI controls
├── Helpers/                      # App services, launch/update logic, storage, localization, and utilities
├── load/                         # Server-switching payloads
│   ├── ArkBilibili/              # Arknights Bilibili server
│   ├── ArkOfficial/              # Arknights official server
│   ├── EndBilibili/              # Endfield Bilibili server
│   ├── EndGlobal/                # Endfield global server
│   ├── EndOfficial/              # Endfield official server
│   └── EndPlay/                  # Endfield Google Play server
├── Models/                       # Configuration, game, account, and update models
├── Properties/                   # .resx resources and publish profiles
├── redist/                       # Redistributable runtime/install assets
├── Resources/
│   ├── i18n/                     # Localization resources
│   └── Icon/                     # Application and game icons
├── Program.cs                    # Application entry point
├── XelLauncher.csproj            # .NET project file
├── XelLauncher.sln               # Visual Studio solution
└── XelLauncher.iss               # Inno Setup installer script
```

> [!CAUTION]
> Do **not** place the game's root directory on the same disk as Tencent ACE Anti-Cheat. Doing so can prevent the launcher from reading the game directory, making server switching unavailable.
>
> Configuration is cached at `C:\Users\<username>\AppData\Local\XelLauncher\config.json`. Delete this file to reset the application.
>
> Account backups are saved by default to `C:\Users\<username>\AppData\Local\XelLauncher\AccountBackups`. Delete this folder to reset all accounts.

## Special Thanks

- UI Framework: [AntdUI](https://github.com/AntdUI/AntdUI)
- Concept reference: [Arknights PC server switching guide](https://www.bilibili.com/video/BV1VHFRzoE7T/?spm_id_from=333.337.search-card.all.click&vd_source=ec94b95a235413f9ad5d2ccb4597ac9f)
- Seamless account switching: [Arknights PC multi-account switching guide](https://www.bilibili.com/video/BV1dgcsz1EjM/?share_source=copy_web&vd_source=5d993adf522c16219536a4e8a61f8484)
- Game download & update: [Hi3Helper.Plugin.Arknights](https://github.com/misaka10843/Hi3Helper.Plugin.Arknights) · [Hi3Helper.Plugin.Endfield](https://github.com/misaka10843/Hi3Helper.Plugin.Endfield)
## Disclaimer

- This project is an unofficial tool and has no affiliation with Hypergryph or any of its affiliated organizations/groups/studios. Game images and data copyrights belong to their respective owners.
- This tool will not upload your account files or login credentials to the internet; it only works on the local client. Please use it with caution if you are concerned about account security. Users are solely responsible for any consequences arising from the use of this tool.
