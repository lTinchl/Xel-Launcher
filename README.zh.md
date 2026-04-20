<div align="center">
<img width="128" height="128" alt="logo" src="https://github.com/user-attachments/assets/448a8cf3-a270-4d82-810b-04119914a32a" />

# Xel Launcher

[English](./README.md) | 中文

![C#](https://img.shields.io/badge/C%23-13-square)
![Platform](https://img.shields.io/badge/platform-Windows-blue)
[![License](https://img.shields.io/badge/license-Apache%202.0-4EB1BA.svg?style=flat-square)](http://www.apache.org/licenses/LICENSE-2.0)
[![NuGet](https://img.shields.io/nuget/v/AntdUI.svg?style=flat-square&label=AntdUI&logo=nuget)](https://www.nuget.org/packages/AntdUI)<br>
![Stars](https://img.shields.io/github/stars/lTinchl/XelLauncher)
![Downloads](https://img.shields.io/github/downloads/lTinchl/XelLauncher/total)

基于 [AntdUI](https://github.com/AntdUI/AntdUI) 构建，支持明日方舟、终末地多服切换，提供账号管理等功能

</div>

## 文件结构
```
├── Forms/                   # 界面窗体
├── Helpers/                 # 工具类（配置、启动、日志等）
├── Models/                  # 数据模型
├── Properties/              # 资源与发布配置
├── Resources/               # 图标、图片等静态资源
├── load/                    # 切服用 load 文件
│   ├── ArkBilibili          # 方舟 Bili 服差异文件
│   ├── ArkOffiicial         # 方舟官服差异文件
│   ├── EndBilibili          # 终末地 Bili 服差异文件
│   ├── EndGlobal            # 终末地国际服差异文件
│   └── EndOfficial          # 终末地官服差异文件
│   └── EndPlay	             # 终末地GooglePlay服差异文件
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
- 服务器切换
	- [x] 明日方舟官服  
	- [x] 明日方舟Bilibili服
	- [x] 终末地官服 	
	- [x] 终末地Bilibili服
	- [x] 终末地国际服 
	- [x] 终末地国际服(GooglePlay)
- [x] 终末地、方舟多账号无感切换
- [x] 支持游戏下载、更新和版本检测
- [x] 联动软件启动
- [x] 自定义参数启动

## 截图
<img width="1280" height="760" alt="QQ_1775213815782" src="https://github.com/user-attachments/assets/b2f3b2e0-d0ac-480d-aaba-f7fee556baab" />

## 特别鸣谢

- UI 框架：[AntdUI](https://github.com/AntdUI/AntdUI)
- 思路来源:[▶️2分钟教会你明日方舟PC端官服-B服互转](https://www.bilibili.com/video/BV1VHFRzoE7T/?spm_id_from=333.337.search-card.all.click&vd_source=ec94b95a235413f9ad5d2ccb4597ac9f)
- 账号无感切换:[▶️明日方舟PC端多账号无感切换教程](https://www.bilibili.com/video/BV1dgcsz1EjM/?share_source=copy_web&vd_source=5d993adf522c16219536a4e8a61f8484)
- 游戏下载更新相关:[[Hi3Helper.Plugin.Arknights](https://github.com/misaka10843/Hi3Helper.Plugin.Arknights)] - [[Hi3Helper.Plugin.Endfield](https://github.com/misaka10843/Hi3Helper.Plugin.Endfield)]
- [Prts Wiki](https://prts.wiki/id/1) - [明日方舟工具箱](https://arkntools.app/) - [明日方舟一图流](https://ark.yituliu.cn/)
- [华法林 Wiki](https://endfieldtools.dev/) - [Endfield Tools](https://endfieldtools.dev/) - [终末地一图流](https://ef.yituliu.cn/)
## 免责声明
此工具不会将您的账号文件上传至互联网，仅用于本地替换。如担心账号安全请酌情使用，使用本工具造成的任何后果由使用者自行承担。
