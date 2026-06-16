<h1 align="center">
  <br>
  <img width="128" height="128" alt="Xel Launcher logo" src="https://github.com/user-attachments/assets/448a8cf3-a270-4d82-810b-04119914a32a">
  <br>
  Xel Launcher
  <br>
</h1>

<h4 align="center">
  基于 <a href="https://github.com/AntdUI/AntdUI">AntdUI</a> 构建的游戏启动器，支持明日方舟、终末地多服切换与账号管理。
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
  <a href="#已支持功能">功能</a>
  <span> | </span>
  <a href="#如何使用">如何使用</a>
  <span> | </span>
  <a href="./docs/wiki.zh-CN.md">文档</a>
  <span> | </span>
  <a href="#项目结构">项目结构</a>
  <span> | </span>
  <a href="#特别鸣谢">特别鸣谢</a>
</p>

## 已支持功能

- 服务器切换
  - [x] 明日方舟官服
  - [x] 明日方舟 Bilibili 服
  - [x] 终末地官服
  - [x] 终末地 Bilibili 服
  - [x] 终末地国际服
  - [x] 终末地国际服（Google Play）
- [x] 终末地、方舟多账号无感切换
- [x] 支持游戏下载、更新和版本检测
- [x] 联动软件启动
- [x] 自定义参数启动
- [x] 森空岛签到

## 如何使用

在 [Releases](https://github.com/lTinchl/Xel-Launcher/releases) 下载最新版本安装包，安装后打开软件，按照提示添加游戏路径，即可使用切服功能。


## 项目结构

```text
XelLauncher/
├── docs/                         # 中英文 Wiki 文档
├── Forms/                        # WinForms 页面、弹窗与 UI 控件
├── Helpers/                      # 应用服务、启动/更新逻辑、存储、本地化与通用工具
├── load/                         # 切服用差异文件
│   ├── ArkBilibili/              # 明日方舟 Bilibili 服
│   ├── ArkOfficial/              # 明日方舟官服
│   ├── EndBilibili/              # 终末地 Bilibili 服
│   ├── EndGlobal/                # 终末地国际服
│   ├── EndOfficial/              # 终末地官服
│   └── EndPlay/                  # 终末地 Google Play 服
├── Models/                       # 配置、游戏、账号与更新数据模型
├── Properties/                   # .resx 资源与发布配置
├── redist/                       # 可再发行运行库/安装资源
├── Resources/
│   ├── i18n/                     # 本地化资源
│   └── Icon/                     # 应用与游戏图标
├── Program.cs                    # 程序入口
├── XelLauncher.csproj            # .NET 项目文件
├── XelLauncher.sln               # Visual Studio 解决方案
└── XelLauncher.iss               # Inno Setup 安装脚本
```

> [!CAUTION]
> 请勿将游戏根目录与某讯 ACE 反作弊放在同一磁盘下。此操作可能导致启动器无权读取游戏目录，无法切服。
>
> 配置文件缓存在 `C:\Users\用户名\AppData\Local\XelLauncher\config.json`，如需重置软件请删除此文件。
>
> 账号备份默认保存在 `C:\Users\用户名\AppData\Local\XelLauncher\AccountBackups`，如需重置账号请删除此目录。

## 特别鸣谢

- UI 框架：[AntdUI](https://github.com/AntdUI/AntdUI)
- 思路来源：[2 分钟教会你明日方舟 PC 端官服-B 服互转](https://www.bilibili.com/video/BV1VHFRzoE7T/?spm_id_from=333.337.search-card.all.click&vd_source=ec94b95a235413f9ad5d2ccb4597ac9f)
- 账号无感切换：[明日方舟 PC 端多账号无感切换教程](https://www.bilibili.com/video/BV1dgcsz1EjM/?share_source=copy_web&vd_source=5d993adf522c16219536a4e8a61f8484)
- 游戏下载更新相关：[Hi3Helper.Plugin.Arknights](https://github.com/misaka10843/Hi3Helper.Plugin.Arknights) · [Hi3Helper.Plugin.Endfield](https://github.com/misaka10843/Hi3Helper.Plugin.Endfield)

## 免责声明

- 本项目为非官方工具，与鹰角网络 (Hypergryph) 及其旗下组织/团体/工作室没有任何关联。游戏图片与数据版权归各自权利人所有。
- 此工具不会将您的账号文件与登录凭证上传至互联网，仅用于本地客户端。如担心账号安全请酌情使用，使用本工具造成的任何后果由使用者自行承担。
