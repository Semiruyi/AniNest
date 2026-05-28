# AniNest 后端模块与 API

本文档描述当前仓库里已经落地的后端结构与接口，以 `src/AniNest.Host` 的实际实现为准。

## 当前项目结构

```text
src/
  AniNest.Core/
  AniNest.Contracts/
  AniNest.Application/
  AniNest.Host/
  Tests.Backend/
frontend/
  aninest_flutter/
```

其中：

- `AniNest.Core`：枚举与基础模型
- `AniNest.Contracts`：HTTP DTO、请求模型、事件信封
- `AniNest.Application`：模块接口与应用服务
- `AniNest.Host`：Minimal API、SSE、文件存储、运行时模块
- `Tests.Backend`：后端单测与 Host 集成测试

当前并没有单独的 `AniNest.Infrastructure` 项目；Host 内部的 `Modules/*` 与 `Modules/Storage/*` 承担了现阶段基础设施实现。

## 模块边界

### Library

负责：

- 媒体库目录增删改查
- 批量导入
- 服务端目录浏览
- 文件夹摘要投影

主要实现位置：

- `src/AniNest.Application/Library`
- `src/AniNest.Host/Modules/Library`
- `src/AniNest.Host/Endpoints/LibraryEndpoints.cs`

### Playlist / Session / Playback

当前 `Playlist` 与 `Session` 由同一个 `PlaybackModule` 实现。

负责：

- 按目录生成播放列表
- 激活目录并建立当前会话
- 切集、恢复进度、保存偏好
- 对外返回 `PlaybackTargetDto`

主要实现位置：

- `src/AniNest.Application/Playback`
- `src/AniNest.Application/Playlist`
- `src/AniNest.Host/Modules/Playback`
- `src/AniNest.Host/Endpoints/PlaylistEndpoints.cs`
- `src/AniNest.Host/Endpoints/SessionEndpoints.cs`

### Metadata

负责：

- 目录与元数据记录同步
- 元数据状态汇总
- 队列处理、重试、评审
- 对外提供目录元数据与评审视图

主要实现位置：

- `src/AniNest.Application/Metadata`
- `src/AniNest.Host/Modules/Metadata`
- `src/AniNest.Host/Endpoints/MetadataEndpoints.cs`

### Thumbnail

负责：

- 缩略图状态存储
- 目录摘要统计
- 优先化、重建、清理、处理

主要实现位置：

- `src/AniNest.Application/Thumbnail`
- `src/AniNest.Host/Modules/Thumbnail`
- `src/AniNest.Host/Endpoints/ThumbnailEndpoints.cs`

### Settings

负责：

- 全量设置读取与保存
- Player / Metadata / Thumbnail 分组设置

主要实现位置：

- `src/AniNest.Application/Settings`
- `src/AniNest.Host/Modules/Settings`
- `src/AniNest.Host/Endpoints/SettingsEndpoints.cs`

### Resources

负责把封面、海报、播放媒体等文件以统一资源路由暴露给前端。

主要实现位置：

- `src/AniNest.Application/Resources`
- `src/AniNest.Host/Modules/Resources`
- `src/AniNest.Host/Endpoints/ResourceEndpoints.cs`

## API 总览

### Library

- `GET /api/library/folders`
- `POST /api/library/folders`
- `POST /api/library/folders:batch-add`
- `GET /api/library/browser`
- `DELETE /api/library/folders/{folderId}`
- `POST /api/library/folders/{folderId}:favorite`
- `POST /api/library/folders/{folderId}:watch-status`
- `POST /api/library/folders/{folderId}:move-to-front`

### Playlist

- `GET /api/playlist/current`
- `GET /api/playlist/by-folder/{folderId}`
- `POST /api/playlist/by-folder/{folderId}:activate`
- `POST /api/playlist/current/items/{itemId}:select`
- `POST /api/playlist/current:next`
- `POST /api/playlist/current:previous`

### Session

- `GET /api/session`
- `POST /api/session/open-folder`
- `POST /api/session/select-item`
- `POST /api/session/next`
- `POST /api/session/previous`
- `POST /api/session/progress`
- `POST /api/session/complete`
- `POST /api/session/close`

### Metadata

- `GET /api/metadata/status-summary`
- `GET /api/metadata/folders/{folderId}`
- `GET /api/metadata/reviews`
- `GET /api/metadata/reviews/{folderId}`
- `POST /api/metadata/reviews/{folderId}:confirm`
- `POST /api/metadata/reviews/{folderId}:reject-candidate`
- `POST /api/metadata/folders/{folderId}:refresh`
- `POST /api/metadata/folders/{folderId}:retry`
- `POST /api/metadata:enqueue-missing`
- `POST /api/metadata:retry-failed`
- `POST /api/metadata:process-queue`

### Thumbnail

- `GET /api/thumbnails/folders/{folderId}`
- `GET /api/thumbnails/folders/{folderId}/summary`
- `GET /api/thumbnails/videos/{videoId}`
- `POST /api/thumbnails/folders/{folderId}:prioritize`
- `POST /api/thumbnails/folders/{folderId}:regenerate`
- `POST /api/thumbnails/folders/{folderId}:process`
- `DELETE /api/thumbnails/folders/{folderId}/cache`

### Settings

- `GET /api/settings`
- `PUT /api/settings`
- `GET /api/settings/player`
- `PUT /api/settings/player`
- `GET /api/settings/metadata`
- `PUT /api/settings/metadata`
- `GET /api/settings/thumbnails`
- `PUT /api/settings/thumbnails`

### Resources

- `GET /api/resources/{kind}/{ownerId}`

当前 `kind` 由 Host 内部编码，前端通常直接消费 DTO 中给出的 URL，而不是手拼。

## 关键 DTO

### `LibraryFolderDto`

```text
folderId
name
path
videoCount
coverUrl
playedCount
watchStatus
isFavorite
addedAtUtc
metadataSummary?
```

`metadataSummary` 当前字段：

```text
matchedTitle
originalTitle
posterUrl
state
hasMetadata
```

### `PlaylistDto`

```text
folderId
folderName
currentItemId?
currentIndex
items[]
```

### `PlaylistItemDto`

```text
itemId
index
title
filePath
isPlayed
hasSavedProgress
savedProgressMs
durationMs
thumbnailState
```

### `SessionOpenResultDto`

```text
session
playbackTarget
```

`PlaybackTargetDto` 当前字段：

```text
itemId
title
mediaUrl
startPositionMs
subtitleUrl?
audioTrackHint?
```

### `MetadataDto`

```text
folderId
title
originalTitle
summary
tags[]
posterPath
season
episodeCount
source
state
failureKind
airDate?
year?
rating?
```

### `MetadataReviewDto`

```text
folderId
folderName
state
failureKind
suggestedSourceId
suggestedTitle
reason
candidates[]
rejectedSourceIds[]
updatedAtUtc
```

### `ThumbnailFolderSummaryDto`

```text
folderId
total
pending
generating
ready
failed
completionPercent
updatedAtUtc?
```

### `AppSettingsDto`

```text
library
player
metadata
thumbnails
```

## 事件流

入口：

```text
GET /api/events
```

返回标准 SSE，每条消息的数据体都是 `EventEnvelopeDto`：

```json
{
  "type": "library.folder_updated",
  "timestampUtc": "2026-05-28T06:00:00Z",
  "sequence": 12,
  "payload": {}
}
```

当前实际事件类型：

- `host.connected`
- `settings.changed`
- `library.folder_added`
- `library.folder_removed`
- `library.folder_updated`
- `library.folder_reordered`
- `session.changed`
- `session.progress_saved`
- `session.completed`
- `session.closed`
- `metadata.folder_updated`
- `metadata.summary_changed`
- `thumbnail.folder_updated`

注意：

- 当前事件名使用下划线风格，而不是驼峰
- `metadata.summary_changed` 的 payload 直接就是 `MetadataStatusSummaryDto`
- `settings.changed` 的 payload 当前只有 `scope`

## 错误模型

统一错误响应为：

```json
{
  "code": "resource.not_found",
  "message": "The target resource was not found.",
  "details": {
    "folderId": "missing-folder"
  }
}
```

对应 `AniNest.Contracts.Common.ErrorResponse`：

```text
code
message
details?: IReadOnlyDictionary<string, string>
```

## 当前实现备注

- 默认 Host 地址是 `http://localhost:5275`
- 根路径 `/` 会重定向到 `/api/settings`
- `src/AniNest.Host/wwwroot/debug/index.html` 是本地调试页
- 当前缩略图与 metadata 处理流程是可运行的 Host 接口骨架，但仍然偏轻量实现
- 当前文档描述的是“已存在接口”，不是早期迁移计划
