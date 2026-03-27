<div align="center">
<img width="128" height="128" alt="logo" src="https://github.com/user-attachments/assets/448a8cf3-a270-4d82-810b-04119914a32a" />

# Xel Launcher

![C#](https://img.shields.io/badge/C%23-13-square)
![Platform](https://img.shields.io/badge/platform-Windows-blue)
[![License](https://img.shields.io/badge/license-Apache%202.0-4EB1BA.svg?style=flat-square)](http://www.apache.org/licenses/LICENSE-2.0)
[![NuGet](https://img.shields.io/nuget/v/AntdUI.svg?style=flat-square&label=AntdUI&logo=nuget)](https://www.nuget.org/packages/AntdUI)<br>
![Stars](https://img.shields.io/github/stars/lTinchl/XelLauncher)
![Downloads](https://img.shields.io/github/downloads/lTinchl/XelLauncher/total)

基于 [AntdUI](https://github.com/AntdUI/AntdUI) 构建，支持明日方舟、终末地多服切换，提供账号管理、MAA 集成等功能。

</div>

## 文件结构
```
├── Forms/                   # 界面窗体
├── Helpers/                 # 工具类（配置、启动、日志等）
├── Models/                  # 数据模型
├── Properties/              # 资源与发布配置
├── Resources/               # 图标、图片等静态资源
├── load/                    # 切服用 payload 压缩包
│   ├── ArkBilibili.zip      # 方舟 B 服
│   ├── ArkOffiicial.zip     # 方舟官服
│   ├── EndBilibili.zip      # 终末地 B 服
│   ├── EndGlobal.zip        # 终末地国际服
│   └── EndOfficial.zip      # 终末地官服
├── Program.cs               # 程序入口
└── XelLauncher.csproj
```

## 注意事项
- ⚠️请勿将游戏根目录与某讯ACE反作弊放在同一磁盘下,此操作将导致启动器无权读取游戏目录,无法切服
- 配置文件缓存在 `C:\Users\用户名\AppData\Local\XelLauncher\config.json`，如需重置软件请删除此文件
- 账号备份默认保存在 `C:\Users\用户名\AppData\Local\XelLauncher\AccountBackups`，如需重置账号请删除此目录

## 如何使用
在 [Releases](https://github.com/lTinchl/Xel-Launcher/releases) 下载最新版本安装包，安装后打开软件，按照提示添加游戏路径，即可使用切服功能。

## 已支持功能
- [x] 明日方舟官服 / B 服双服切换
- [x] 终末地官服 / B 服 / 国际服三服切换
- [x] 方舟官服多账号无感切换
- [ ] 启动MAA
- [ ] 明日方舟Wiki
- [ ] 终末地Wiki

## 特别鸣谢

- UI 框架：[AntdUI](https://github.com/AntdUI/AntdUI)
- 思路来源:[▶️2分钟教会你明日方舟PC端官服-B服互转](https://www.bilibili.com/video/BV1VHFRzoE7T/?spm_id_from=333.337.search-card.all.click&vd_source=ec94b95a235413f9ad5d2ccb4597ac9f)
- 账号无感切换:[▶️明日方舟PC端多账号无感切换教程](https://www.bilibili.com/video/BV1dgcsz1EjM/?share_source=copy_web&vd_source=5d993adf522c16219536a4e8a61f8484)
- [明日方舟WiKi](https://prts.wiki/id/1) - [明日方舟工具箱](https://arkntools.app/) - [明日方舟一图流](https://ark.yituliu.cn/)

## 免责声明
此工具不会将您的账号文件上传至互联网，仅用于本地替换。如担心账号安全请酌情使用，使用本工具造成的任何后果由使用者自行承担。
