# LocalPlayer

基于 WPF + LibVLC 的 Windows 本地视频播放器。深色主题，海报墙式文件夹浏览，支持断点续播、全屏动画、键盘快捷键、后台缩略图生成。

![.NET](https://img.shields.io/badge/.NET-9.0-512BD4)
![WPF](https://img.shields.io/badge/WPF-Windows-0078D4)
![License](https://img.shields.io/badge/License-MIT-green)

## 功能

- **海报墙浏览** — 文件夹卡片展示，支持封面图自动识别、拖拽排序、流畅入场动画
- **断点续播** — 自动保存播放进度，下次打开从上次位置继续（跳过 90% 后的结尾）
- **选集面板** — 右侧集数网格，带缩略图生成进度饼图和已看对勾标记
- **全屏播放** — 独立全屏窗口 + 缩放过渡动画，控制栏/选集自动隐藏
- **进度条缩略图预览** — 鼠标悬停时显示对应时间点的帧截图（ffmpeg 预生成）
- **倍速播放** — 0.5x ~ 3x，带延迟弹出动画的速度选择器
- **右键长按加速** — 按住右键 350ms 自动切 3 倍速，松手恢复
- **可自定义快捷键** — 9 个操作支持自定义绑定，冲突检测，重置默认
- **便携模式** — 所有数据保存在 exe 同级目录，绿色免安装

## 支持的格式

| 视频 | 封面 |
|------|------|
| MP4, MKV, AVI, MOV, WMV, FLV, WEBM, M4V, MPG, MPEG, TS, M2TS, RMVB | JPG, JPEG, PNG, BMP, GIF |

## 快捷键（默认）

| 按键 | 功能 |
|------|------|
| `Space` | 播放 / 暂停 |
| `←` `→` | 后退 / 前进 5 秒 |
| `J` `L` | 后退 / 前进 5 秒（备用） |
| `F` | 全屏 |
| `Esc` | 退出全屏 / 返回主页 |
| `N` | 下一集 |
| `P` | 上一集 |
| `鼠标后退键` | 返回主页 |
| `左键双击视频` | 全屏切换 |

快捷键可通过播放页控制栏的设置按钮自定义。

## 项目结构

```
LocalPlayer/
│
├── Model/                       # 数据 + 后台服务，零 UI 依赖
│   ├── AppSettings.cs           # 应用设置
│   ├── FolderListItem.cs        # 文件夹卡片
│   ├── PlaylistItem.cs          # 选集条目
│   ├── SettingsService.cs       # JSON 配置读写
│   ├── ThumbnailGenerator.cs    # ffmpeg 后台缩略图队列
│   ├── VideoScanner.cs          # 视频/封面文件扫描
│   └── AppLog.cs                # 文件日志
│
├── Media/                       # 播放引擎，依赖 LibVLCSharp
│   ├── MediaPlayerController.cs # LibVLC 封装
│   ├── VideoFrameProvider.cs    # 双缓冲 BGRA → WriteableBitmap
│   └── PlayerInputHandler.cs    # 键盘快捷键映射
│
├── Controls/                    # 共享播放 UI 控件与交互行为
│   ├── ControlBarView.xaml/.cs  # 播放控制栏
│   ├── PlaylistPanelView.xaml/.cs # 选集侧面板
│   ├── SpeedPopupController.cs  # 倍速弹出菜单
│   ├── ThumbnailPreviewController.cs # 进度条缩略图预览
│   ├── ClickRouter.cs           # 单击/双击分发
│   ├── PauseOverlayController.cs # 暂停图标动画
│   └── RightHoldSpeedController.cs # 右键长按加速
│
├── View/                        # 页面、窗口、UI 工具
│   ├── App.xaml/.cs             # 应用入口
│   ├── MainWindow.xaml/.cs      # 主窗口，页面导航
│   ├── Primitives/              # 通用 WPF 工具（与业务无关）
│   │   ├── AnimationHelper.cs
│   │   ├── CubicBezierEase.cs
│   │   ├── ThumbnailConverters.cs
│   │   ├── AnimatedWrapPanel.cs
│   │   └── InsertionAdorner.cs
│   ├── Library/
│   │   ├── MainPage.xaml/.cs        # 文件夹卡片浏览
│   │   ├── MainPage.Animations.cs   # 卡片入场/重排动画
│   │   └── MainPage.DragDrop.cs     # 拖拽排序
│   ├── Player/
│   │   ├── PlayerPage.xaml/.cs      # 视频播放页
│   │   ├── PlayerPage.Animations.cs # 页面淡出动画
│   │   └── FullscreenWindow.xaml/.cs # 全屏窗口
│   └── Settings/
│       └── KeyBindingsWindow.xaml/.cs # 键盘快捷键编辑
│
└── Resources/Icons/             # 按钮图标
```

## 架构

依赖方向：

```
View ──→ Controls ──→ Media ──→ Model
 │         │            │
 └─────────┴────────────┴──→ Primitives
```

- **Model** — 数据类、文件 IO、JSON 序列化、ffmpeg 进程管理。不碰 UI 线程，不引用 WPF 类型
- **Media** — LibVLC 封装、视频帧回调、按键映射。纯引擎逻辑，不持有 XAML 控件引用
- **Controls** — 可在 PlayerPage / FullscreenWindow 间复用的 UI 控件和交互行为（控制栏、选集面板、倍速弹窗、鼠标行为等）
- **View** — 页面组装、窗口管理、入场/退出动画。Primitives 为通用 WPF 工具子目录
- **Resources** — 静态资源

无 DI 容器，通过单例（SettingsService、ThumbnailGenerator）和事件在组件间通信。

## 环境要求

- Windows 10/11 (x64)
- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- LibVLCSharp.WPF + VideoLAN.LibVLC.Windows（NuGet 自动还原）
- ffmpeg.exe + ffprobe.exe（可选，放在 exe 同级目录即可启用缩略图功能）

## 构建与运行

```bash
# 调试运行
dotnet build
dotnet run

# 独立部署（免安装 .NET 运行时）
dotnet publish -c Release --self-contained true -r win-x64 -o ./publish
```

## 数据存储

便携模式，所有数据在 exe 同级目录：

- `Data/settings.json` — 文件夹列表、播放进度、快捷键配置
- `thumbnails/` — ffmpeg 生成的缩略图缓存（按 MD5 目录组织）
- `player.log` — 运行日志

## 技术栈

- [.NET 9 WPF](https://learn.microsoft.com/zh-cn/dotnet/desktop/wpf/)
- [LibVLCSharp](https://code.videolan.org/videolan/LibVLCSharp)
- [VideoLAN.LibVLC.Windows](https://www.nuget.org/packages/VideoLAN.LibVLC.Windows/)

## License

MIT
