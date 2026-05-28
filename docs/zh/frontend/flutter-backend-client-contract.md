# Flutter 与后端对接契约

本文档面向当前仓库里的 Flutter 客户端，对齐 `AniNest.Host` 已落地的 REST API 与 SSE 事件流。

## 边界

后端负责：

- 媒体库
- 播放列表
- 会话状态
- 元数据
- 缩略图
- 设置
- 事件流

Flutter 负责：

- 窗口与页面 UI
- `media_kit` 播放器实例
- 本地播放控制
- 通过 API 上报进度与完成事件

一句话说清：

> 后端决定播什么，Flutter 决定怎么播。

## 默认连接地址

- Host：`http://localhost:5275`
- 调试页：`http://localhost:5275/debug/index.html`

当前 Flutter 代码里的默认地址也是 `http://localhost:5275`。

## 建议的客户端分层

当前仓库已经在沿这个方向组织：

```text
lib/
  src/
    api/
    models/
    services/
    features/
    presentation/
    app/
```

建议职责：

- `api/`：HTTP 与 SSE 基础能力
- `models/`：Dart DTO
- `services/`：按后端模块封装请求
- `features/`：状态与控制器
- `app/`：全局装配、事件分发、后端切换

## 当前 Host API

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

## 资源 URL 约定

前端应优先直接消费 DTO 中提供的 URL：

- `LibraryFolderDto.coverUrl`
- `LibraryMetadataSummaryDto.posterUrl`
- `PlaybackTargetDto.mediaUrl`
- `PlaybackTargetDto.subtitleUrl`

这些 URL 最终都落在：

```text
/api/resources/{kind}/{ownerId}
```

前端不应该自行推导资源路径。

## 播放对接

### 打开目录并开始播放

推荐流程：

1. `POST /api/session/open-folder`
2. 读取 `SessionOpenResultDto.playbackTarget`
3. 用 `playbackTarget.mediaUrl` 打开 `media_kit`
4. 如果 `startPositionMs > 0`，在播放器 ready 后 seek

### 切集

当前代码里有两条入口都能工作：

- 会话风格
  - `POST /api/session/next`
  - `POST /api/session/previous`
- 播放列表风格
  - `POST /api/playlist/current/items/{itemId}:select`
  - `POST /api/playlist/current:next`
  - `POST /api/playlist/current:previous`

当前 Flutter 已经在 `SessionApi.selectItem()` 中直接调用：

```text
POST /api/playlist/current/items/{itemId}:select
```

这没有问题，返回值同样是 `SessionOpenResultDto`。

### 进度上报

当前接口：

```text
POST /api/session/progress
```

请求字段：

```json
{
  "itemId": "ep-01",
  "positionMs": 120000,
  "durationMs": 1440000,
  "rate": 1.0,
  "volume": 80,
  "isPaused": false
}
```

建议节奏：

- 每 5 秒一次
- 暂停时补一次
- 切集前补一次
- 页面关闭前补一次

### 播放完成

当前接口：

```text
POST /api/session/complete
```

建议先通知后端，再决定是否自动下一集，不要在播放器本地直接推进业务状态。

## SSE 事件流

入口：

```text
GET /api/events
```

首条事件通常是：

- `host.connected`

当前 Host 会发出的事件：

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

- 事件名使用下划线风格
- `metadata.summary_changed` 的 payload 直接就是 summary DTO
- `settings.changed` 的 payload 当前形如 `{ "scope": "player" }`

### 前端处理建议

不要把 SSE payload 当唯一真相，推荐“事件触发，查询回填”。

当前最实用的刷新策略：

- 收到 `library.*`
  - 刷新 `GET /api/library/folders`
- 收到 `session.changed`
  - 刷新 `GET /api/session`
  - 刷新 `GET /api/playlist/current`
- 收到 `metadata.summary_changed`
  - 刷新 `GET /api/metadata/status-summary`
- 收到 `metadata.folder_updated`
  - 如果当前选中了对应 folder，刷新 `GET /api/metadata/folders/{folderId}`
- 收到 `thumbnail.folder_updated`
  - 如果当前选中了对应 folder，刷新：
    - `GET /api/thumbnails/folders/{folderId}`
    - `GET /api/thumbnails/folders/{folderId}/summary`

## 首屏加载建议

### Library 页

建议先拉：

1. `GET /api/library/folders`
2. `GET /api/metadata/status-summary`

### Player 页

建议先拉：

1. `GET /api/session`
2. 如果存在 session，再拉 `GET /api/playlist/current`

### 目录详情

建议先拉：

1. `GET /api/playlist/by-folder/{folderId}`
2. `GET /api/metadata/folders/{folderId}`
3. `GET /api/thumbnails/folders/{folderId}/summary`
4. `GET /api/thumbnails/folders/{folderId}`

## 当前 DTO 重点

### `PlaybackTargetDto`

当前字段是：

```text
itemId
title
mediaUrl
startPositionMs
subtitleUrl?
audioTrackHint?
```

不是早期草案里的 `filePath`。

### `LibraryFolderDto`

当前字段里是：

```text
coverUrl
addedAtUtc
metadataSummary
```

不是 `coverPath`。

### `MetadataFolderUpdatedEventDto`

当前 Flutter 模型已兼容这些字段：

```text
folderId
state
failureKind
hasMetadata
matchedTitle
originalTitle
posterUrl
coverUrl
updatedAtUtc
```

## 错误处理

统一错误模型：

```json
{
  "code": "resource.not_found",
  "message": "The target resource was not found.",
  "details": {
    "folderId": "missing-folder"
  }
}
```

建议前端至少保留：

- `statusCode`
- `code`
- `message`
- `details`

## 当前客户端实现状态

当前 Flutter 仓库里已经有这些 service：

- `LibraryApi`
- `SessionApi`
- `PlaylistApi`
- `MetadataApi`
- `ThumbnailApi`
- `SettingsApi`
- `HostEventService`

但它们并没有把 Host 的全部接口都包完，文档中的接口列表描述的是当前后端能力，不等于前端已经全部接入。
