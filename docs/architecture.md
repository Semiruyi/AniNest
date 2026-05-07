# Architecture

## Overview

`AniNest` 是一个以本地番剧收藏和连续观看体验为中心的 WPF 应用。整体结构不是传统的三层 CRUD，而是更偏向 feature-first 的富客户端组织方式：

- 按业务特性拆分：`Shell`、`Library`、`Player`
- 按通用能力沉淀：`Infrastructure`、`Presentation`、`View`
- 用 `CompositionRoot` 统一注册依赖和装配对象

代码的关注点不是数据库事务，而是：

- 播放页进入/退出时序
- WPF UI 状态与播放器状态同步
- 本地文件系统扫描
- 后台缩略图队列
- 性能与稳定性诊断

## Top-Level Layout

```text
src/
├─ LocalPlayer/
│  ├─ CompositionRoot/
│  ├─ Core/
│  ├─ Data/
│  ├─ Features/
│  ├─ Infrastructure/
│  ├─ Presentation/
│  ├─ Resources/
│  └─ View/
├─ Launcher/
└─ Tests/
```

## Dependency Direction

核心依赖方向大致如下：

```text
View
  -> Features
  -> Presentation

Features
  -> Infrastructure

Presentation
  -> Infrastructure   (少量场景，主要是播放器或应用级能力接入)

CompositionRoot
  -> Features
  -> Infrastructure
  -> View
```

几个边界特征需要明确：

- `View` 是 WPF 宿主层，承接窗口生命周期和输入事件
- `Features` 里允许出现 ViewModel、Controller、AppService 混合组织
- `Infrastructure` 不是纯数据访问层，而是所有底层能力集合
- `Presentation` 是偏 WPF 的通用可复用组件，不承载业务流程

## Layer Responsibilities

### View

目录：`src/LocalPlayer/View`

职责：

- 应用入口
- 主窗口宿主
- 页面内容承载
- Win32 / WPF 生命周期接入
- 顶层输入分发

关键文件：

- `View/App.xaml.cs`
- `View/MainWindow.xaml`
- `View/MainWindow.xaml.cs`

这里的 `MainWindow` 不是业务中心，更像一个宿主容器，负责：

- 绑定 `ShellViewModel`
- 处理窗口几何恢复/保存
- 承接全屏切换
- 拦截预览输入再向下转发

### Features

目录：`src/LocalPlayer/Features`

`Features` 是项目的核心业务层，但不是纯粹的 domain layer，而是“功能组织层”。每个 feature 一般会同时包含：

- ViewModel
- Service / AppService
- Controller
- 该功能自己的页面或局部视图

#### Shell

目录：`src/LocalPlayer/Features/Shell`

职责：

- 应用级菜单
- 设置弹层
- 语言切换
- 媒体库页与播放页切换
- 播放页全屏状态对接

`ShellViewModel` 是最上层的页面编排器。它维护 `CurrentPage`，监听媒体库和播放页发出的事件，再决定何时进入播放器、何时返回主页。

#### Library

目录：`src/LocalPlayer/Features/Library`

职责：

- 媒体库加载
- 添加目录 / 批量添加目录
- 删除目录
- 展示目录封面和视频数
- 展示缩略图生成总体进度

关键对象：

- `MainPageViewModel`
- `LibraryAppService`

`LibraryAppService` 是一个典型用例编排服务。它把以下能力串起来：

- `ISettingsService`
- `IVideoScanner`
- `IThumbnailGenerator`

它不做底层扫描实现，而是负责“这个用例要先做什么、后做什么”。

#### Player

目录：`src/LocalPlayer/Features/Player`

职责：

- 播放页状态管理
- 播放会话管理
- 播放列表
- 输入绑定
- 控制栏交互
- 进度条缩略图预览

这一层是整个项目最复杂、也最有架构意味的部分。

关键对象：

- `PlayerViewModel`
- `PlayerAppService`
- `PlayerSessionController`
- `PlayerPlaybackStateController`
- `PlayerPlaybackFacade`
- `PlayerInputService`

其中的职责切分大概是：

- `PlayerViewModel`：页面状态入口，聚合控制栏、列表、输入宿主
- `PlayerAppService`：处理进入/退出播放页的时序
- `PlayerSessionController`：管理当前会话、当前项、播放列表同步
- `PlayerPlaybackStateController`：管理播放器 UI 关心的状态
- `PlayerPlaybackFacade`：给 UI 暴露统一播放操作接口
- `PlayerInputService`：把键盘/鼠标输入映射成播放器动作

## Infrastructure

目录：`src/LocalPlayer/Infrastructure`

这一层包含所有底层实现，不局限于“持久化”。

### Persistence

目录：`Infrastructure/Persistence`

`SettingsService` 是应用级状态仓库，负责：

- 加载/保存 `settings.json`
- 目录列表
- 播放进度
- 已播放状态
- 目录最后播放项
- 输入绑定相关配置
- 缩略图过期策略
- 窗口位置和尺寸

它既是配置服务，也是轻量本地状态存储。

### Media

目录：`Infrastructure/Media`

`MediaPlayerController` 是底层播放器封装，负责：

- LibVLC 初始化与复用
- 播放 / 暂停 / 停止 / seek
- 当前媒体切换
- 帧输出为 `WriteableBitmap`
- 播放进度事件
- 部分性能埋点

这一层屏蔽了 LibVLC 的生命周期细节，让上层更像在操作一个应用内播放器服务。

### Thumbnails

目录：`Infrastructure/Thumbnails`

这里包含两类能力：

- `VideoScanner`：目录扫描、视频文件识别、封面识别、批量查找视频目录
- `ThumbnailGenerator`：后台缩略图任务队列、任务索引、优先级、过期清理

`ThumbnailGenerator` 不是简单的工具函数，而是一个长期存活的后台组件：

- 应用启动后异步初始化
- 从索引恢复任务状态
- 后台串行处理生成任务
- 对外发布总体进度和单视频进度事件

### Diagnostics / Logging / Interop / Localization

- `Diagnostics`：性能埋点、帧统计、内存快照
- `Logging`：应用日志和异常处理
- `Interop`：任务栏、窗口、系统级行为适配
- `Localization`：语言资源加载和切换

这些模块共同说明，这个项目把“桌面端稳定性和可观测性”当成正式能力在维护。

## Presentation

目录：`src/LocalPlayer/Presentation`

这一层是 WPF 侧通用部件集合，主要包括：

- `Animations/`
- `Behaviors/`
- `Converters/`
- `Diagnostics/`
- `Interop/`
- `Primitives/`

它们服务于界面表达，但不主导业务流程。比较典型的对象包括：

- 自定义动画器
- 输入 behavior
- 缩略图/封面转换器
- `TransitioningContentControl`
- `SeekBar`
- `AnimatedPopup`

可以把这一层理解成“本项目内部的小型 UI 基础设施”。

## Composition Root

目录：`src/LocalPlayer/CompositionRoot`

`ServiceRegistration` 统一注册依赖。项目使用 `Microsoft.Extensions.DependencyInjection` 做装配，生命周期目前以 `Singleton` 为主。

这意味着：

- 应用整体偏单进程、单用户会话模型
- 很多状态对象会长期驻留
- service 间事件订阅和清理必须谨慎处理

## Runtime Flow

### Startup

1. `App.xaml.cs` 创建 `ServiceCollection`
2. `ServiceRegistration` 注册所有服务、ViewModel 和主窗口
3. 启动时加载设置
4. 根据设置应用语言
5. 预热 `IMediaPlayerController`
6. 配置全局异常处理
7. 创建并显示 `MainWindow`

### Open Library

1. `MainPageViewModel` 调用 `LibraryAppService.LoadLibraryAsync`
2. `LibraryAppService` 从 `SettingsService` 读取目录列表
3. 对每个目录调用 `VideoScanner`
4. 为每个目录建立 `LibraryFolderDto`
5. 将目录内视频送入 `ThumbnailGenerator` 队列
6. ViewModel 更新界面集合和缩略图进度显示

### Enter Player

1. 用户在媒体库中选择目录
2. `ShellViewModel` 切换 `CurrentPage` 到 `PlayerViewModel`
3. `ShellViewModel` 调用 `PlayerAppService.EnterPlayerAsync`
4. `PlayerAppService` 先加载播放列表骨架
5. 再加载播放列表完整数据
6. 等待页面转场完成
7. 激活当前视频并开始真实播放链路

这套时序的目的，是避免页面还没 ready 时就提前激活播放器，减少首帧显示和页面切换之间的竞争。

### Playback State Sync

播放器底层状态不会直接绑定到所有 ViewModel，而是通过同步服务桥接：

- `MediaPlayerController` 发出播放/暂停/停止/进度事件
- `PlayerPlaybackStateSyncService` 负责线程切换和状态同步
- `PlayerPlaybackStateController` 维护供 UI 绑定的状态

这能把“底层播放器事件”和“UI 层状态响应”解耦开。

## Why This Shape

这套结构适合当前项目，原因主要有三点：

1. 业务不是表单录入，而是高交互、高状态变化的桌面体验
2. 媒体播放、缩略图生成、输入绑定、动画切换都需要显式时序控制
3. WPF 富客户端里，完全追求分层纯度的收益不如把职责切清楚、把生命周期处理对

所以这个项目更像：

- MVVM 作为 UI 绑定基础
- AppService / Controller 负责业务时序
- Infrastructure 承担所有底层实现

而不是一个严格套模板的 Clean Architecture 示例。

## Known Tradeoffs

当前结构也有一些明显取舍：

- 某些 ViewModel 仍直接接触 `MessageBox`、`OpenFolderDialog` 等 UI 细节
- `SettingsService` 承担的职责较多，是比较重的中心依赖
- `Features` 层内部混合了 ViewModel、Controller、Service，不是纯粹的单一风格
- 部分 `Presentation` 组件会接入业务相关能力，边界不是绝对严格

这些取舍并不罕见，关键在于它们目前仍然可控，而且符合项目规模。

## Related Docs

- [offline-update.md](offline-update.md)
