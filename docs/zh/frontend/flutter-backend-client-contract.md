# Flutter 与后端对接契约

本文面向即将开始的 Flutter 前端实现，目标是把当前 AniNest 后端的 REST + SSE 能力收敛成一套稳定的客户端接入方式。

当前约定基于现有后端实现，而不是未来设想。

## 目标

Flutter 端负责：

- UI
- 路由与页面状态
- `media_kit` 播放器实例
- 本地播放控制
- 将播放进度、播放完成等事件上报给后端

后端负责：

- Library
- Playlist
- Session
- Metadata
- Thumbnail
- Settings
- 事件流

一句话说清边界：

> 后端决定“应该播放什么”；Flutter 决定“怎么播放这个目标”。

## 推荐的 Flutter 分层

推荐先按 4 层拆：

```text
lib/
  src/
    api/
      aninest_http_client.dart
      aninest_event_stream.dart
      dto/
    services/
      library_api.dart
      session_api.dart
      metadata_api.dart
      thumbnail_api.dart
      settings_api.dart
    features/
      library/
      player/
      metadata/
      settings/
    app/
      app_state.dart
      event_router.dart
```

职责建议：

- `api/`
  - 只处理 HTTP、JSON、SSE
  - 不承担业务拼装
- `services/`
  - 按后端模块封装调用
  - 例如 `SessionApi.openFolder()`、`ThumbnailApi.processFolder()`
- `features/`
  - 页面级状态
  - 控制器、view model、notifier、bloc 都放这里
- `app/event_router.dart`
  - 统一消费 SSE
  - 按事件类型刷新对应 feature 状态

## 后端基地址

开发期默认：

```text
http://localhost:5275
```

调试页地址：

```text
http://localhost:5275/debug/index.html
```

## 推荐的客户端对象

建议至少有这几个 service：

### `LibraryApi`

- `Future<List<LibraryFolderDto>> getFolders()`
- `Future<void> addFolder(String path)`
- `Future<void> addFolderBatch(String rootPath)`
- `Future<void> setFavorite(String folderId, bool isFavorite)`
- `Future<void> setWatchStatus(String folderId, String status)`
- `Future<void> moveToFront(String folderId)`
- `Future<void> deleteFolder(String folderId)`

### `SessionApi`

- `Future<SessionStateDto?> getCurrent()`
- `Future<SessionOpenResultDto> openFolder(String folderId)`
- `Future<SessionOpenResultDto> selectItem(String itemId)`
- `Future<SessionOpenResultDto> moveNext()`
- `Future<SessionOpenResultDto> movePrevious()`
- `Future<void> reportProgress(...)`
- `Future<void> complete(String itemId)`
- `Future<void> close()`

### `PlaylistApi`

- `Future<PlaylistDto?> getCurrent()`
- `Future<PlaylistDto> getByFolder(String folderId)`

### `MetadataApi`

- `Future<MetadataStatusSummaryDto> getSummary()`
- `Future<MetadataDto?> getFolder(String folderId)`
- `Future<void> refreshFolder(String folderId)`
- `Future<void> retryFolder(String folderId)`
- `Future<void> enqueueMissing()`
- `Future<void> retryFailed({required bool includeNoMatch})`
- `Future<MetadataProcessingResultDto> processQueue({int maxItems = 1})`

### `ThumbnailApi`

- `Future<List<ThumbnailStatusDto>> getFolder(String folderId)`
- `Future<ThumbnailFolderSummaryDto> getFolderSummary(String folderId)`
- `Future<ThumbnailStatusDto?> getVideo(String videoId)`
- `Future<void> prioritizeFolder(String folderId)`
- `Future<void> regenerateFolder(String folderId)`
- `Future<ThumbnailProcessingResultDto> processFolder(String folderId, {int maxItems = 1})`
- `Future<void> clearFolderCache(String folderId)`

### `SettingsApi`

- `Future<AppSettingsDto> getAll()`
- `Future<PlayerSettingsDto> getPlayer()`
- `Future<void> savePlayer(PlayerSettingsDto dto)`
- `Future<MetadataSettingsDto> getMetadata()`
- `Future<void> saveMetadata(MetadataSettingsDto dto)`
- `Future<ThumbnailSettingsDto> getThumbnails()`
- `Future<void> saveThumbnails(ThumbnailSettingsDto dto)`

## REST 接口如何使用

推荐把接口分成 3 类：

### 1. 查询接口

特点：

- `GET`
- 返回当前快照
- 适合页面首屏加载、手动刷新、重连后重建状态

典型接口：

- `GET /api/library/folders`
- `GET /api/playlist/current`
- `GET /api/session`
- `GET /api/metadata/status-summary`
- `GET /api/metadata/folders/{folderId}`
- `GET /api/thumbnails/folders/{folderId}`
- `GET /api/thumbnails/folders/{folderId}/summary`
- `GET /api/thumbnails/videos/{videoId}`
- `GET /api/settings/*`

### 2. 命令接口

特点：

- `POST` / `PUT` / `DELETE`
- 用于发起状态变更
- 成功后通常不要只信返回值，最好再等 SSE 或主动刷新一次相关查询

典型接口：

- `POST /api/session/open-folder`
- `POST /api/session/progress`
- `POST /api/metadata:process-queue`
- `POST /api/thumbnails/folders/{folderId}:process`

### 3. 事件接口

特点：

- `GET /api/events`
- SSE
- 用来驱动列表、详情、播放器状态联动刷新

## 播放器对接建议

### 打开播放

流程建议：

1. Flutter 调 `POST /api/session/open-folder`
2. 后端返回 `SessionOpenResultDto`
3. Flutter 取 `playbackTarget.filePath`
4. 交给 `media_kit` 打开
5. 如果 `startPositionMs > 0`，在播放器 ready 后 seek

### 切换集数

两种入口：

- 用户从播放列表点选某一集
- 当前播放结束后自动下一集

统一流程：

1. 先调后端：
   - `POST /api/playlist/current/items/{itemId}:select`
   - 或 `POST /api/session/next`
   - 或 `POST /api/session/previous`
2. 后端返回新的 `SessionOpenResultDto`
3. Flutter 用新的 `playbackTarget` 重开播放器目标

### 进度上报

建议播放器层定时上报，不要每一帧都打接口。

推荐节奏：

- 每 5 秒上报一次
- 暂停时立即上报一次
- 切换集数前上报一次
- 页面关闭或应用挂起前上报一次

当前接口：

`POST /api/session/progress`

请求 DTO 对应字段：

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

注意：

- 这里字段名是 `rate` 和 `volume`
- 不是 settings 里的 `preferredRate` / `preferredVolume`

### 播放完成

播放器判断“本集播放完成”后：

1. 调 `POST /api/session/complete`
2. 再决定是否自动调用 `POST /api/session/next`

建议不要让播放器本地私自推进业务状态，还是让后端记账。

## SSE 接入建议

后端事件入口：

```text
GET /api/events
```

当前第一页连接时会先收到：

- `host.connected`

当前事件类型包括：

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

### Flutter 端推荐策略

不要把 SSE payload 当成唯一真相。

推荐规则：

- 事件先做“轻量提示”
- 再按需刷新对应查询接口

例如：

- 收到 `library.*`
  - 刷新 `GET /api/library/folders`
- 收到 `session.changed`
  - 刷新 `GET /api/session`
  - 刷新 `GET /api/playlist/current`
- 收到 `metadata.summary_changed`
  - 刷新 `GET /api/metadata/status-summary`
- 收到 `metadata.folder_updated`
  - 如果当前正在看这个 folder，刷新 `GET /api/metadata/folders/{folderId}`
- 收到 `thumbnail.folder_updated`
  - 如果当前正在看这个 folder，刷新：
    - `GET /api/thumbnails/folders/{folderId}`
    - `GET /api/thumbnails/folders/{folderId}/summary`

这样做的好处：

- Flutter 端逻辑简单
- 不依赖事件 payload 的细节稳定性
- 后端后续扩充字段时前端更稳

## 首屏加载建议

### Library 页

首次进入建议拉：

1. `GET /api/library/folders`
2. `GET /api/metadata/status-summary`

### Player 页

首次进入建议拉：

1. `GET /api/session`
2. 如果有 session，再拉 `GET /api/playlist/current`
3. 如果有 `currentItemId`，播放器再按需恢复

### Folder 详情页

进入某个 folder 时建议拉：

1. `GET /api/playlist/by-folder/{folderId}`
2. `GET /api/metadata/folders/{folderId}`
3. `GET /api/thumbnails/folders/{folderId}/summary`
4. `GET /api/thumbnails/folders/{folderId}`

## 错误处理约定

当前统一错误模型：

```json
{
  "code": "resource.not_found",
  "message": "The target resource was not found.",
  "details": {
    "folderId": "missing-folder"
  }
}
```

Flutter 端建议统一转成：

- `NotFound`
- `Conflict`
- `ValidationError`
- `UnknownApiError`

至少保留：

- `statusCode`
- `code`
- `message`
- `details`

## DTO 使用建议

当前后端已经有 `AniNest.Contracts` 的 C# DTO，但 Flutter 不应手写照抄业务逻辑。

建议做法：

1. 在 Dart 侧按当前 JSON 字段名定义模型
2. 所有字段名保持和后端 JSON 一致
3. 页面只依赖 Dart model，不直接依赖原始 map

建议优先建这些 Dart model：

- `LibraryFolderDto`
- `LibraryFolderListResponse`
- `PlaylistDto`
- `PlaylistItemDto`
- `SessionStateDto`
- `PlaybackTargetDto`
- `SessionOpenResultDto`
- `MetadataDto`
- `MetadataStatusSummaryDto`
- `MetadataProcessingResultDto`
- `ThumbnailStatusDto`
- `ThumbnailFolderSummaryDto`
- `ThumbnailProcessingResultDto`
- `PlayerSettingsDto`
- `MetadataSettingsDto`
- `ThumbnailSettingsDto`
- `AppSettingsDto`
- `ErrorResponse`
- `EventEnvelopeDto`

## 推荐的页面状态拆分

建议不要做一个巨大的全局 store。

先按 feature 拆：

### `LibraryState`

- `folders`
- `isLoading`
- `selectedFolderId`

### `PlayerState`

- `session`
- `playlist`
- `currentPlaybackTarget`
- `playerRuntimeState`

注意：

- `playerRuntimeState` 是 Flutter 本地状态
- `session` / `playlist` 是后端状态

### `MetadataState`

- `summary`
- `currentFolderMetadata`
- `isProcessing`

### `ThumbnailState`

- `folderSummary`
- `folderItems`
- `selectedVideoThumbnail`

### `SettingsState`

- `appSettings`
- `playerSettings`

## 第一版 Flutter 接入顺序

建议按这个顺序落地：

1. `SettingsApi`
2. `LibraryApi`
3. `SessionApi + PlaylistApi`
4. `media_kit` 打开 `PlaybackTargetDto`
5. `session/progress` 上报
6. `MetadataApi`
7. `ThumbnailApi`
8. `SSE event router`

理由很简单：

- 先把“能播”跑通
- 再把“信息补全”和“异步刷新”补上

## 当前最值得保持的约束

前端实现时尽量不要破坏这几条：

- 不让 Flutter 自己决定业务上的“下一集是谁”
- 不让 Flutter 自己持久化核心播放进度作为唯一来源
- 不让页面直接消费 SSE payload 去手改复杂状态
- 不把 `media_kit` 状态和后端 session 状态揉成一个对象

## 下一步建议

这份契约文档之后，最适合继续做的是：

1. 建一个最小 Dart client 目录结构
2. 先实现 `SessionApi + Player bridge`
3. 再补 SSE event router

如果你愿意下一步直接开 Flutter 脚手架，我建议先做“库页 + 播放页”两屏，不急着上 metadata 和 thumbnail 页面。
