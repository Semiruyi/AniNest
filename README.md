# LocalPlayer

一款基于 WPF + LibVLC 的本地视频播放器，采用深色主题设计，提供海报墙式的文件夹浏览体验和流畅的播放控制。

![.NET](https://img.shields.io/badge/.NET-9.0-512BD4)
![WPF](https://img.shields.io/badge/WPF-Windows-0078D4)
![License](https://img.shields.io/badge/License-MIT-green)

## 功能特性

- **海报墙浏览** — 自动扫描文件夹中的视频文件，以卡片形式展示，支持封面图自动识别
- **智能封面识别** — 自动查找 `folder`、`cover`、`poster`、`front`、`thumbnail` 等命名的图片，或自动选用文件夹内任意图片作为封面
- **播放进度记忆** — 自动保存每个视频的播放进度，下次打开时从断点续播
- **选集面板** — 右侧集数网格，快速切换视频，支持集数提示 Tooltip
- **全屏播放** — 一键全屏，控制栏自动隐藏，沉浸式观影体验
- **丰富的快捷键** — 支持播放控制、进度跳转、集数切换、全屏等键盘操作
- **流畅动画** — 卡片悬浮、按钮交互、页面切换均带有缓动动画效果

## 支持格式

| 视频格式 |
|----------|
| MP4, MKV, AVI, MOV, WMV, FLV, WEBM, M4V, MPG, MPEG, TS, M2TS, RMVB |

| 封面图片格式 |
|-------------|
| JPG, JPEG, PNG, BMP, GIF |

## 快捷键

| 快捷键 | 功能 |
|--------|------|
| `Space` | 播放 / 暂停 |
| `←` / `→` | 后退 / 前进 5 秒 |
| `J` / `L` | 后退 / 前进 5 秒 |
| `F` | 全屏切换 |
| `Esc` | 退出全屏 / 返回主页 |
| `N` / `PageDown` | 下一集 |
| `P` / `PageUp` | 上一集 |
| `鼠标后退键` / `Mouse Back Button` | 返回上一级 |
| `左键双击视频` / `Double-click (Left) video` | 播放 / 暂停 |
| `右键双击视频` / `Double-click (Right) video` | 全屏切换 |


## 项目结构

```
LocalPlayer/
├── Models/              # 数据模型
│   ├── AppSettings.cs   # 应用设置与播放进度
│   ├── VideoFolder.cs   # 视频文件夹
│   ├── VideoItem.cs     # 视频条目
│   └── PlayerHistory.cs # 播放历史
├── Services/            # 业务服务
│   ├── MediaPlayerController.cs  # VLC 播放控制
│   ├── PlayerInputHandler.cs     # 键盘输入处理
│   ├── SettingsService.cs        # 设置读写
│   └── VideoScanner.cs           # 视频/封面扫描
├── Views/               # 视图页面
│   ├── MainPage.xaml    # 主页（海报墙）
│   ├── PlayerPage.xaml  # 播放页
│   └── AnimatedWrapPanel.cs      # 动画 WrapPanel
├── Resources/Icons/     # 图标资源
├── App.xaml             # 全局样式与资源
└── MainWindow.xaml      # 主窗口
```

## 环境要求

- **操作系统**: Windows 10/11 (x64)
- **运行时**: [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- **依赖**: LibVLCSharp.WPF + VideoLAN.LibVLC.Windows（通过 NuGet 自动还原）

## 构建与运行

### 调试运行

```bash
dotnet build
dotnet run
```

### 发布（独立部署）

```bash
# 使用脚本发布到 ./publish 目录
release.bat

# 或手动执行
dotnet publish LocalPlayer.csproj -c Release --self-contained true -r win-x64 -o ./publish
```

发布后可直接将 `publish` 文件夹复制到其他 Windows 电脑运行，无需安装 .NET 运行时。

## 使用说明

1. 点击主页右下角 **"添加文件夹"** 按钮，选择存放视频文件的文件夹
2. 程序自动扫描文件夹中的视频并生成封面卡片
3. 双击卡片进入播放页，从上次离开的位置继续播放
4. 播放时可通过右侧选集面板切换视频，或使用键盘快捷键控制播放

## 技术栈

- [.NET 9 WPF](https://learn.microsoft.com/zh-cn/dotnet/desktop/wpf/)
- [LibVLCSharp](https://code.videolan.org/videolan/LibVLCSharp) — VideoLAN 官方 .NET 绑定
- [VideoLAN.LibVLC.Windows](https://www.nuget.org/packages/VideoLAN.LibVLC.Windows/) — Windows 版 LibVLC 引擎

## 开源协议

MIT License
