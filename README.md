# AniNest

AniNest 是一个基于 WPF + LibVLC 的 Windows 本地动漫播放器，面向本地番剧收藏和连续观看体验。项目的核心亮点是库记忆、丝滑动画，以及基于 ffmpeg 的缩略图预览。

![.NET](https://img.shields.io/badge/.NET-9.0-512BD4)
![WPF](https://img.shields.io/badge/WPF-Windows-0078D4)
![License](https://img.shields.io/badge/License-MIT-green)

## 功能

- 海报墙式媒体库浏览，支持封面自动识别和目录拖拽排序
- 库记忆，自动保存单集进度、目录顺序、已看状态和最后播放项
- 丝滑动画，包括页面转场、全屏切换和控件显隐
- 缩略图预览，支持后台预生成、缓存和进度条悬停预览
- 倍速播放与右键长按临时 3x 加速
- 可自定义键盘/鼠标输入绑定，支持冲突检测和恢复默认
- 便携式数据布局，设置、进度和缩略图都保存在应用目录
- 离线补丁启动器，支持应用更新包后再启动主程序

## 支持格式

| 类型 | 格式 |
|---|---|
| 视频 | MP4, MKV, AVI, MOV, WMV, FLV, WEBM, M4V, MPG, MPEG, TS, M2TS, RMVB |
| 封面 | JPG, JPEG, PNG, BMP, GIF |

## 默认输入绑定

| 输入 | 功能 |
|---|---|
| `Space` | 播放 / 暂停 |
| `Left` / `Right` | 后退 / 前进 5 秒 |
| `J` / `L` | 后退 / 前进 5 秒 |
| `F` | 切换全屏 |
| `Esc` | 退出全屏 / 返回媒体库 |
| `N` | 下一项 |
| `P` | 上一项 |
| 鼠标后退键 | 返回媒体库 |
| 双击视频区域 | 切换全屏 |
| 右键长按 | 临时 3x 加速 |

## 快速开始

环境要求：

- Windows 10/11 x64
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- 可选：`ffmpeg.exe`，用于缩略图预生成与进度条预览

构建与运行：

```powershell
dotnet build AniNest.sln
dotnet run --project .\src\AniNest\AniNest.csproj
```

发布：

```powershell
dotnet publish .\src\AniNest\AniNest.csproj -c Release -r win-x64 --self-contained true -o .\artifacts\publish
```

或使用脚本：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\publish.ps1 -Runtime win-x64 -Configuration Release -Zip
```

## 项目结构

```text
AniNest/
├─ src/
│  ├─ AniNest/
│  │  ├─ CompositionRoot/
│  │  ├─ Core/
│  │  ├─ Data/
│  │  ├─ Features/
│  │  ├─ Infrastructure/
│  │  ├─ Presentation/
│  │  ├─ Resources/
│  │  └─ View/
│  ├─ Launcher/
│  └─ Tests/
├─ docs/
├─ tools/
└─ AniNest.sln
```

## 数据目录

应用按便携模式运行，主要数据位于程序目录下：

- `Data/settings.json`：目录列表、窗口状态、播放进度、输入绑定、语言等配置
- `Data/Languages/*.json`：界面语言资源
- `thumbnails/`：按视频路径哈希组织的缩略图缓存和索引
- `player.log`：应用日志

## 测试

```powershell
dotnet test .\src\Tests\AniNest.Tests.csproj
```

## 文档

- [docs/architecture.md](docs/architecture.md)：架构、依赖方向、运行时主链路和关键模块
- [docs/offline-update.md](docs/offline-update.md)：离线更新与补丁流程

## 技术栈

- .NET 9
- WPF
- CommunityToolkit.Mvvm
- LibVLCSharp
- ffmpeg

## License

MIT
