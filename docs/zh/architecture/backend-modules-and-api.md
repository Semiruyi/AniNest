# AniNest 后端模块与接口设计

## 目标

本文档用于定义 AniNest 新后端的模块边界、职责划分、统一 API、关键 DTO，以及前后端之间的协作方式。

设计前提：

- WPF 不再作为目标前端
- 后端重构不以 WPF 可运行作为验证标准
- 正确性以单元测试、契约测试、集成测试为准
- 新前端计划使用 Flutter
- 新播放器计划使用 Flutter `media_kit`

核心原则：

> 后端决定“播什么、从哪播、播完怎么记账”；前端决定“怎么播、怎么暂停、怎么 seek、怎么渲染”。

这意味着后端不再持有 WPF 控件、视频表面或具体桌面 UI 状态，也不直接承担 Flutter 播放器实例管理。

## 当前实现进度

截至当前版本，新后端骨架已经完成以下落地：

- 已建立项目分层：
  - `AniNest.Core`
  - `AniNest.Contracts`
  - `AniNest.Application`
  - `AniNest.Host`
  - `AniNest.Backend.Tests`
- 已建立统一错误响应模型与 Host 全局异常映射
- 已为以下模块建立 `Application service + Host file store + API + tests` 的基础形态：
  - `Library`
  - `Playlist`
  - `Session / Playback`
  - `Settings`
  - `Metadata`
  - `Thumbnail`
- `Settings`、`Library`、`PlaybackProgress`、`Metadata`、`Thumbnail` 已具备文件持久化能力
- `Playlist` 已可以基于真实目录文件生成
- `Session` 已具备：
  - 按目录恢复上次播放项
  - 按视频进度恢复起播位置
  - 播放超过 90% 后从头播
  - 关闭会话时保存当前项
- Host 集成测试已覆盖主要 API 骨架
- 当前后端测试总数已达到 40 条量级，并以新接口行为作为重构基线

当前尚未完成的部分主要是“增强实现”，而不是“继续搭骨架”：

- `Library` 仍未接入完整真实扫描策略
- `Metadata` 尚未接入真实抓取 worker / provider 调度
- `Thumbnail` 尚未接入真实缩略图生成任务
- `Session / Playlist` 还未完整迁入旧工程中的所有排序与恢复细节

## 总体结构

推荐项目结构：

```text
src/
  AniNest.Core/
  AniNest.Application/
  AniNest.Contracts/
  AniNest.Infrastructure/
  AniNest.Host/
frontend/
  aninest_flutter/
tests/
  AniNest.Tests/
```

各层职责：

- `AniNest.Core`
  - 纯领域模型、值对象、枚举、状态定义
- `AniNest.Application`
  - 用例编排、业务服务、模块协调
- `AniNest.Contracts`
  - 对外 DTO、请求响应模型、事件模型、错误模型
- `AniNest.Infrastructure`
  - 文件扫描、设置存储、元数据存储、缩略图执行等实现
- `AniNest.Host`
  - HTTP API、事件流、依赖注入、宿主生命周期
- `frontend/aninest_flutter`
  - Flutter UI 与 `media_kit` 播放器实现

## 模块划分

后端分为 7 个核心模块。

### 1. Library

职责：

- 管理媒体库目录
- 扫描目录并识别视频文件
- 返回文件夹摘要、排序、收藏、观看状态
- 触发新增目录后的元数据与缩略图初始化

不负责：

- 实时播放器控制
- 视频渲染
- 页面层 UI 状态

核心能力：

- 获取媒体库列表
- 添加目录
- 批量导入目录
- 删除目录
- 修改目录排序
- 设置收藏与观看状态

### 2. Playlist

职责：

- 根据文件夹生成播放列表
- 决定当前集、上一集、下一集
- 恢复上次播放的视频与位置
- 保存目录级和文件级进度

不负责：

- 调用底层播放器播放
- 维护 Flutter 控件状态

核心能力：

- 构建播放列表
- 选集
- 上一集/下一集
- 计算默认播放目标
- 持久化进度

### 3. Session

职责：

- 管理当前播放会话
- 将 Library、Playlist、Settings 串成一个完整播放流程
- 维护当前会话状态与“应播放目标”
- 接收前端上报的播放进度并回写

不负责：

- 创建 `media_kit` 播放器实例
- 持有播放器渲染表面

核心能力：

- 打开文件夹开始会话
- 切换当前播放项
- 下一集/上一集
- 保存播放进度
- 标记播放完成
- 关闭会话

### 4. Metadata

职责：

- 管理目录与元数据记录的关联
- 处理匹配、抓取、失败重试、冷却时间
- 返回海报、标题、简介、标签等信息
- 维护元数据任务队列与汇总状态

不负责：

- 页面展示格式
- 视频播放控制

核心能力：

- 同步库快照
- 注册/删除目录元数据记录
- 刷新缺失项
- 重试失败项
- 查询目录元数据

### 5. Thumbnail

职责：

- 管理缩略图索引、生成任务、缓存与清理
- 返回缩略图状态与资源位置
- 为播放列表和库页面提供缩略图数据

不负责：

- 决定页面如何展示缩略图
- 前端 hover 预览交互

核心能力：

- 注册目录缩略图任务
- 优先化当前目录或播放窗口附近的视频
- 重建目录缩略图
- 清理目录缓存
- 查询缩略图状态

### 6. Settings

职责：

- 管理应用设置与持久化
- 提供播放偏好、元数据策略、缩略图策略、库目录配置
- 为 Session、Library、Metadata、Thumbnail 提供策略输入

不负责：

- 前端设置面板状态

核心能力：

- 读取全部设置
- 更新分组设置
- 提供默认值
- 保存与校验

### 7. Host / Platform

职责：

- 暴露 HTTP API
- 暴露事件流
- 负责依赖注入、宿主启动、后台任务协调
- 连接底层基础设施实现

不负责：

- 承载业务规则本身

## 模块之间的依赖关系

推荐依赖方向：

```text
Library   -> Settings, Metadata, Thumbnail
Playlist  -> Settings
Session   -> Playlist, Settings
Metadata  -> Settings
Thumbnail -> Settings
Host      -> Application modules
```

约束：

- 模块之间通过应用服务和契约模型交互
- 不允许模块直接依赖前端对象
- 不允许 `Application` 层依赖 Flutter 或 WPF 类型

## 前后端边界

Flutter 负责：

- 页面 UI
- `media_kit` 播放器实例
- 播放/暂停/seek/音量/倍速等本地控制
- 当前 position、duration、播放结束等事件上报

后端负责：

- 媒体库
- 播放列表
- 播放会话规则
- 播放进度保存
- 元数据
- 缩略图
- 设置

关键设计决定：

- 后端 API 返回“播放目标”，而不是直接驱动播放器
- Flutter 拿到播放目标后，交给 `media_kit` 执行
- Flutter 通过上报接口告知后端进度、结束、失败等事件

## API 总览

统一路由前缀：

```text
/api/library/*
/api/playlist/*
/api/session/*
/api/metadata/*
/api/thumbnails/*
/api/settings/*
/api/events
```

推荐协议：

- 命令/查询：HTTP + JSON
- 事件流：SSE，必要时可替换为 WebSocket

## API 详细设计

### Library API

#### `GET /api/library/folders`

返回媒体库文件夹列表。

响应：

```json
{
  "items": [
    {
      "folderId": "clannad",
      "name": "CLANNAD",
      "path": "D:/Anime/CLANNAD",
      "videoCount": 24,
      "coverPath": "D:/AniNest/cache/posters/clannad.jpg",
      "playedCount": 12,
      "watchStatus": "watching",
      "isFavorite": true,
      "metadataSummary": {
        "title": "CLANNAD",
        "posterPath": "D:/AniNest/cache/posters/clannad.jpg"
      }
    }
  ]
}
```

#### `POST /api/library/folders`

添加单个目录。

请求：

```json
{
  "path": "D:/Anime/CLANNAD"
}
```

#### `POST /api/library/folders:batch-add`

批量导入某根目录下的有效视频文件夹。

请求：

```json
{
  "rootPath": "D:/Anime"
}
```

#### `DELETE /api/library/folders/{folderId}`

删除库目录，同时触发对应元数据和缩略图清理。

#### `POST /api/library/folders/{folderId}:favorite`

请求：

```json
{
  "isFavorite": true
}
```

#### `POST /api/library/folders/{folderId}:watch-status`

请求：

```json
{
  "status": "completed"
}
```

#### `POST /api/library/folders/{folderId}:move-to-front`

将目录移动到列表首位。

### Playlist API

#### `GET /api/playlist/by-folder/{folderId}`

返回指定目录的播放列表。

#### `GET /api/playlist/current`

返回当前会话对应的播放列表；无会话时返回空。

#### `POST /api/playlist/by-folder/{folderId}:activate`

基于指定目录创建或切换当前播放列表，并返回默认播放目标。

#### `POST /api/playlist/current/items/{itemId}:select`

选中某一集作为当前播放项，并返回新的播放目标。

#### `POST /api/playlist/current:next`

跳到下一集，并返回新的播放目标。

#### `POST /api/playlist/current:previous`

跳到上一集，并返回新的播放目标。

### Session API

Session 模块是 Flutter 与后端交互的核心入口。

#### `GET /api/session`

返回当前会话状态。

#### `POST /api/session/open-folder`

打开某个目录并创建会话。

请求：

```json
{
  "folderId": "clannad"
}
```

响应：

```json
{
  "session": {
    "sessionId": "session-001",
    "folderId": "clannad",
    "folderName": "CLANNAD",
    "currentItemId": "ep-01",
    "currentIndex": 0,
    "playlistCount": 24,
    "hasPrevious": false,
    "hasNext": true,
    "savedProgressMs": 0
  },
  "playbackTarget": {
    "itemId": "ep-01",
    "title": "Episode 1",
    "filePath": "D:/Anime/CLANNAD/01.mp4",
    "startPositionMs": 0
  }
}
```

#### `POST /api/session/select-item`

请求：

```json
{
  "itemId": "ep-03"
}
```

响应返回新的 `session` 与 `playbackTarget`。

#### `POST /api/session/next`

切换到下一集。

#### `POST /api/session/previous`

切换到上一集。

#### `POST /api/session/progress`

Flutter 上报当前播放进度。

请求：

```json
{
  "itemId": "ep-03",
  "positionMs": 182000,
  "durationMs": 1440000,
  "rate": 1.0,
  "volume": 80,
  "isPaused": false
}
```

#### `POST /api/session/complete`

当 Flutter 判定播放完成时调用，用于标记已看、推进下一集候选。

请求：

```json
{
  "itemId": "ep-03"
}
```

#### `POST /api/session/close`

关闭当前会话并持久化最后一次播放信息。

### Metadata API

#### `GET /api/metadata/folders/{folderId}`

返回目录元数据详情。

#### `POST /api/metadata/folders/{folderId}:refresh`

强制刷新单个目录元数据。

#### `POST /api/metadata/folders/{folderId}:retry`

重试单个目录失败的元数据任务。

#### `POST /api/metadata:enqueue-missing`

将所有缺失元数据的目录重新加入任务队列。

#### `POST /api/metadata:retry-failed`

请求：

```json
{
  "includeNoMatch": false
}
```

#### `GET /api/metadata/status-summary`

返回元数据总览状态。

### Thumbnail API

#### `GET /api/thumbnails/folders/{folderId}`

返回目录级缩略图信息。

#### `GET /api/thumbnails/videos/{videoId}`

返回单个视频的缩略图状态、路径、进度信息。

#### `POST /api/thumbnails/folders/{folderId}:prioritize`

优先生成某目录缩略图。

#### `POST /api/thumbnails/folders/{folderId}:regenerate`

重新生成某目录缩略图。

#### `DELETE /api/thumbnails/folders/{folderId}/cache`

清理某目录缩略图缓存。

### Settings API

#### `GET /api/settings`

返回完整设置。

#### `PUT /api/settings`

更新完整设置。

#### `GET /api/settings/player`

返回播放器偏好，例如默认倍速、音量、自动续播策略。

#### `PUT /api/settings/player`

更新播放器偏好。

#### `GET /api/settings/metadata`

返回元数据相关策略。

#### `PUT /api/settings/metadata`

更新元数据相关策略。

#### `GET /api/settings/thumbnails`

返回缩略图相关策略。

#### `PUT /api/settings/thumbnails`

更新缩略图相关策略。

## 事件流设计

统一事件入口：

```text
GET /api/events
```

推荐使用 SSE。

事件格式：

```json
{
  "type": "session.changed",
  "timestampUtc": "2026-05-22T10:00:00Z",
  "payload": {
    "sessionId": "session-001",
    "currentItemId": "ep-04"
  }
}
```

建议事件类型：

- `library.folderAdded`
- `library.folderRemoved`
- `library.folderUpdated`
- `session.changed`
- `session.progressSaved`
- `metadata.summaryChanged`
- `metadata.folderUpdated`
- `thumbnail.progressChanged`
- `thumbnail.folderCompleted`
- `settings.changed`

事件用途：

- Flutter 监听后台异步任务
- 避免轮询元数据与缩略图进度
- 统一刷新列表、详情页和播放器状态

## DTO 设计

以下 DTO 应定义在 `AniNest.Contracts`。

### `LibraryFolderDto`

```text
LibraryFolderDto
- folderId
- name
- path
- videoCount
- coverPath
- playedCount
- watchStatus
- isFavorite
- metadataSummary
```

### `PlaylistDto`

```text
PlaylistDto
- folderId
- folderName
- currentItemId
- currentIndex
- items[]
```

### `PlaylistItemDto`

```text
PlaylistItemDto
- itemId
- index
- title
- filePath
- isPlayed
- hasSavedProgress
- savedProgressMs
- durationMs
- thumbnailStatus
```

### `SessionStateDto`

```text
SessionStateDto
- sessionId
- folderId
- folderName
- currentItemId
- currentIndex
- playlistCount
- hasPrevious
- hasNext
- savedProgressMs
- preferredRate
- preferredVolume
```

### `PlaybackTargetDto`

```text
PlaybackTargetDto
- itemId
- title
- filePath
- startPositionMs
- subtitlePath
- audioTrackHint
```

### `MetadataDto`

```text
MetadataDto
- folderId
- title
- originalTitle
- summary
- tags[]
- posterPath
- season
- episodeCount
- source
- state
- failureKind
```

### `ThumbnailStatusDto`

```text
ThumbnailStatusDto
- targetId
- state
- progressPercent
- imagePath
- updatedAtUtc
```

### `AppSettingsDto`

```text
AppSettingsDto
- library
- player
- metadata
- thumbnails
```

## 后端内部服务接口建议

这些接口用于 `Application` 层，不等同于 HTTP API。

### `ILibraryModule`

```text
Task<IReadOnlyList<LibraryFolderDto>> GetFoldersAsync(...)
Task<AddFolderResultDto> AddFolderAsync(...)
Task<BatchAddFoldersResultDto> AddFolderBatchAsync(...)
Task DeleteFolderAsync(...)
Task SetFavoriteAsync(...)
Task SetWatchStatusAsync(...)
Task MoveFolderToFrontAsync(...)
```

### `IPlaylistModule`

```text
Task<PlaylistDto> GetByFolderAsync(...)
Task<PlaylistSelectionResultDto> ActivateFolderAsync(...)
Task<PlaylistSelectionResultDto> SelectItemAsync(...)
Task<PlaylistSelectionResultDto> MoveNextAsync(...)
Task<PlaylistSelectionResultDto> MovePreviousAsync(...)
Task SaveProgressAsync(...)
```

### `ISessionModule`

```text
Task<SessionOpenResultDto> OpenFolderAsync(...)
Task<SessionOpenResultDto> SelectItemAsync(...)
Task<SessionOpenResultDto> MoveNextAsync(...)
Task<SessionOpenResultDto> MovePreviousAsync(...)
Task ReportProgressAsync(...)
Task CompleteAsync(...)
Task CloseAsync(...)
Task<SessionStateDto?> GetCurrentAsync(...)
```

### `IMetadataModule`

```text
Task<MetadataDto?> GetByFolderAsync(...)
Task RefreshFolderAsync(...)
Task RetryFolderAsync(...)
Task EnqueueMissingAsync(...)
Task RetryFailedAsync(...)
Task<MetadataStatusSummaryDto> GetSummaryAsync(...)
```

### `IThumbnailModule`

```text
Task<ThumbnailStatusDto> GetByVideoAsync(...)
Task<IReadOnlyList<ThumbnailStatusDto>> GetByFolderAsync(...)
Task PrioritizeFolderAsync(...)
Task RegenerateFolderAsync(...)
Task ClearFolderCacheAsync(...)
```

### `ISettingsModule`

```text
Task<AppSettingsDto> GetAsync(...)
Task SaveAsync(AppSettingsDto settings, ...)
Task<PlayerSettingsDto> GetPlayerAsync(...)
Task SavePlayerAsync(PlayerSettingsDto settings, ...)
Task<MetadataSettingsDto> GetMetadataAsync(...)
Task SaveMetadataAsync(MetadataSettingsDto settings, ...)
Task<ThumbnailSettingsDto> GetThumbnailsAsync(...)
Task SaveThumbnailsAsync(ThumbnailSettingsDto settings, ...)
```

## 错误模型

建议统一错误响应：

```json
{
  "code": "library.folder_not_found",
  "message": "The target folder does not exist.",
  "details": {
    "folderId": "clannad"
  }
}
```

建议错误码前缀：

- `library.*`
- `playlist.*`
- `session.*`
- `metadata.*`
- `thumbnail.*`
- `settings.*`

## 测试策略

本次重构不以 WPF 行为作为回归基线，而以测试作为基线。

建议测试层次：

### 1. 模块单元测试

覆盖：

- Library 目录增删改查
- Playlist 选集与进度恢复
- Session 会话切换与完成逻辑
- Metadata 队列与状态汇总
- Thumbnail 状态与缓存管理
- Settings 校验与保存

### 2. 契约测试

覆盖：

- API 请求响应结构
- DTO 字段完整性
- 错误码与状态码
- SSE 事件格式

### 3. 集成测试

覆盖：

- 使用真实测试目录扫描视频文件
- 会话打开、切换、保存进度
- 元数据快照同步与重试
- 缩略图任务调度

## 第一阶段落地建议

优先顺序：

1. 建立 `AniNest.Contracts`
2. 定义核心 DTO 与错误模型
3. 定义 `Library`、`Playlist`、`Session`、`Metadata`、`Thumbnail`、`Settings` 模块接口
4. 在 `AniNest.Host` 中实现最小 API
5. 为模块与 API 补齐测试

第一阶段先跑通：

- `Library`
- `Session`
- `Settings`

第二阶段接入：

- `Metadata`
- `Thumbnail`

第三阶段由 Flutter 对接：

- 库页
- 播放页
- 设置页

## 与旧代码的关系

以下方向建议保留并迁移其业务语义：

- `LibraryAppService`
- `MetadataSyncCoordinator`
- `PlaylistManager`
- 设置、扫描、元数据、缩略图相关接口与实现

以下对象不应直接迁入新后端：

- `PlayerViewModel`
- `PlayerDisplayStateViewModel`
- `PlaylistViewModel`
- `ControlBarViewModel`
- 所有 XAML、Behavior、Animation、WPF Presentation 类

新后端应只保留其中真正属于业务逻辑的部分，并重新整理为模块服务与契约模型。
