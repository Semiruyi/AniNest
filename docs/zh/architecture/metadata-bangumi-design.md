# AniNest Bangumi 元数据子系统设计

## 适用范围

- 面向当前动漫元数据主线
- 数据源限定为 `Bangumi`
- 目标实现位置以 `src/AniNest.Host/Modules/Metadata` 为准

## 相关代码

- `src/AniNest.Host/Modules/Metadata`
- `src/AniNest.Application/Metadata`
- `src/AniNest.Contracts/Metadata`

## 配套阅读

- [后端模块与 API](./backend-modules-and-api.md)
- [元数据抓取流水线](./metadata-fetch-pipeline-design.md)
- [元数据运行时结构](./metadata-runtime-structure.md)

## 1. 目标

本文档定义 AniNest 面向动漫库的元数据获取能力设计。该能力以 `Bangumi` 为唯一数据源，目标是在不阻塞 Library 正常加载的前提下，为本地目录持续补全标题、简介、海报、评分等信息，并在重启、失败、重试、目录变更场景下保持状态一致。

本文档聚焦以下问题：

- 架构如何分层
- 输入从哪里来
- 如何向 Bangumi 发起搜索与详情抓取
- 如何做严格匹配
- 如何输出到 Host API 与 Flutter
- 后台线程谁来起
- 任务如何调度与恢复

当前阶段明确约束：

- 仅支持动漫元数据
- 仅接入 Bangumi
- 以自动后台补全为主
- 优先避免误匹配，而不是追求极限命中率

---

## 2. 设计原则

1. 元数据是挂在 Library 上的后台子系统，不是临时抓一次的工具接口。
2. Library 加载绝不依赖元数据完成，元数据只能异步增强体验。
3. 自动匹配必须保守，错绑比缺失更糟。
4. 持久状态与运行时队列分离，重启后不能盲信上次的运行中状态。
5. Host 负责后台 worker、存储、Bangumi 通信与速率控制，Flutter 只消费结果和状态。

---

## 3. 总体架构

推荐分为五层：

```text
Flutter UI
  -> Library cards / inspector / settings metadata status

Host API Layer
  -> Metadata endpoints
  -> Event stream publishing

Application Layer
  -> Metadata command service
  -> Metadata query service
  -> Matching policy
  -> Scheduling abstractions

Host Runtime Layer
  -> Metadata background worker
  -> In-memory task scheduler
  -> Bangumi HTTP provider
  -> Poster downloader

Storage Layer
  -> Metadata index store
  -> Metadata payload repository
  -> Poster cache
```

职责边界：

- `Application` 只表达业务规则与抽象，不直接依赖 HTTP。
- `Host Runtime` 负责真实执行，包括队列、worker、Bangumi API 调用。
- `Storage` 负责持久化状态与缓存文件。
- `Flutter` 不直接调用 Bangumi。

---

## 3.1 期望运行主线

元数据子系统的主线不是“点一次接口就抓一次”，而是一个由 Host 托管、围绕 Library 快照持续工作的后台生命周期。

期望主线：

```text
App start
  -> Host boot
  -> Metadata background service starts
     -> load metadata records
     -> normalize stale Queued/Scraping to NeedsMetadata
     -> enter idle loop

  -> Library loads current folders
  -> Metadata lifecycle service receives library snapshot
     -> reconcile records with folders
     -> mark folder metadata states
     -> build task plans
     -> enqueue eligible jobs
     -> publish summary changed

  -> Metadata worker wakes
     -> dequeue next job
     -> mark Scraping
     -> execute placeholder or real scrape job
     -> persist state and payload
     -> update library-facing metadata summary
     -> publish folder_updated
     -> publish summary_changed
     -> continue
```

这条主线强调：

1. Host 负责拉起 metadata 后台线程
2. Library 负责提供“当前有哪些库”这一份事实快照
3. Metadata 负责对账、建计划、排队、调度、更新状态
4. 具体刮削细节只是 worker 的任务体，不是生命周期核心

---

## 3.2 生命周期职责拆分

为了让主线清晰，建议将 metadata 子系统拆为四个角色：

### `MetadataLifecycleService`

职责：

- 接收 library snapshot
- 对账 metadata records 与当前 folders
- 决定每个 folder 当前的 metadata 状态
- 生成任务计划
- 将任务交给 scheduler

### `MetadataTaskPlanner`

职责：

- 根据 folder 状态、失败类型、cooldown、手动操作原因生成任务计划
- 不直接执行 provider 调用

### `MetadataTaskScheduler`

职责：

- 维护内存工作队列
- 去重
- 优先级排序
- cooldown 判断
- 唤醒 worker

### `MetadataBackgroundWorker`

职责：

- 从 scheduler 取任务
- 执行任务体
- 更新 record / payload / poster cache
- 发布状态事件

一句话边界：

> Lifecycle 负责想明白该做什么，Scheduler 负责安排先做什么，Worker 负责真的去做。

---

## 4. 输入模型

元数据输入不依赖用户手动录入，而是从现有 Library 数据中提取。

推荐内部模型：

```csharp
public sealed record MetadataFolderRef(
    string FolderId,
    string FolderPath,
    string FolderName,
    string? ParentFolderName,
    IReadOnlyList<string> VideoFiles,
    int VideoCount);
```

输入来源优先级：

1. 目录名 `FolderName`
2. 父目录名 `ParentFolderName`
3. 视频文件名 `VideoFiles`
4. 视频数量 `VideoCount`

说明：

- 动漫目录名常常带有字幕组、分辨率、季度标记、剧场版标记。
- 真正的高价值关键词有时在视频文件名中，而不是目录名中。
- 后台 worker 运行时必须重新拿到真实 `VideoFiles`，不能只用 `FolderId` 或空文件列表。

---

## 5. 状态模型

继续沿用当前 AniNest 已有状态语义：

```csharp
public enum MetadataState
{
    NeedsMetadata,
    Queued,
    Scraping,
    Ready,
    NeedsReview,
    Disabled
}

public enum MetadataFailureKind
{
    None,
    NetworkError,
    NoMatch,
    ProviderError
}
```

约定：

- `NeedsMetadata`: 尚未抓取或需要重新抓取
- `Queued`: 已进入运行时队列
- `Scraping`: 正在由 worker 处理
- `Ready`: 已有本地元数据可供 UI 使用
- `NeedsReview`: 自动流程无法安全决定结果
- `Disabled`: 用户关闭自动元数据

重启恢复规则：

- 上次持久化为 `Queued` 或 `Scraping` 的记录，在启动恢复时统一回退为 `NeedsMetadata`
- `Ready`、`NeedsReview`、`Disabled` 可以直接保留

---

## 6. 持久化设计

元数据子系统需要三类存储：

### 6.1 索引文件

路径建议：

```text
{AppData}/metadata/index.json
```

用途：

- 元数据状态源头
- 存放任务状态、失败信息、时间戳、缓存文件定位信息

建议结构：

```csharp
public sealed class MetadataRecord
{
    public string FolderId { get; set; } = "";
    public string FolderPath { get; set; } = "";
    public string FolderName { get; set; } = "";
    public string FolderFingerprint { get; set; } = "";

    public MetadataState State { get; set; } = MetadataState.NeedsMetadata;
    public MetadataFailureKind FailureKind { get; set; } = MetadataFailureKind.None;

    public string? SourceId { get; set; }
    public DateTime? LastAttemptAtUtc { get; set; }
    public DateTime? LastSucceededAtUtc { get; set; }
    public DateTime? CooldownUntilUtc { get; set; }

    public string? MetadataFilePath { get; set; }
    public string? PosterFilePath { get; set; }
}
```

### 6.2 元数据正文

路径建议：

```text
{AppData}/metadata/payload/{folderHash}.json
```

用途：

- 保存展示用元数据正文
- 供 Library DTO 摘要组装与 Metadata 详情查询使用

建议结构：

```csharp
public sealed class FolderMetadataPayload
{
    public string FolderId { get; set; } = "";
    public string? SourceId { get; set; }
    public string? Title { get; set; }
    public string? OriginalTitle { get; set; }
    public string? Summary { get; set; }
    public string? PosterUrl { get; set; }
    public string? LocalPosterPath { get; set; }
    public string? AirDate { get; set; }
    public int? Year { get; set; }
    public double? Rating { get; set; }
    public int? EpisodeCount { get; set; }
    public IReadOnlyList<string> Tags { get; set; } = [];
    public string? Source { get; set; }
    public DateTime ScrapedAtUtc { get; set; }
}
```

### 6.3 海报缓存

路径建议：

```text
{AppData}/metadata/posters/{folderHash}.jpg
```

说明：

- 库页卡片优先使用本地 `cover` 文件
- 无本地封面时回退到 metadata poster
- 海报缓存由 Host 持久保存，目录删除时同步清理

### 6.4 写入策略

索引文件与正文文件都使用临时文件提升方式：

1. 写入 `*.tmp`
2. 刷盘
3. 原子替换正式文件

这样可以降低崩溃时写坏文件的概率。

---

## 7. Bangumi Provider 设计

Host 内部提供一个专门的 Bangumi provider，不让业务层直接依赖 HTTP 细节。

推荐接口：

```csharp
public interface IAnimeMetadataProvider
{
    Task<ProviderSearchResult> SearchBestMatchAsync(
        MetadataKeywordPlan plan,
        CancellationToken cancellationToken);

    Task<ProviderSubjectDetail> GetSubjectAsync(
        string sourceId,
        CancellationToken cancellationToken);

    Task<Stream> DownloadPosterAsync(
        string imageUrl,
        CancellationToken cancellationToken);
}
```

`BangumiMetadataProvider` 负责：

- Access Token 注入
- User-Agent
- 请求超时
- 速率控制
- 429 / 5xx 重试
- 搜索结果 DTO 映射
- subject detail DTO 映射
- provider 级错误分类

Bangumi 侧使用能力：

- `POST /v0/search/subjects`
- `GET /v0/subjects/{subject_id}`
- `GET /v0/subjects/{subject_id}/image`

当前阶段 Bangumi 是唯一 provider，因此业务记录中的 `Source` 固定为 `bangumi`。

---

## 8. 匹配与刮削流程

### 8.1 关键词计划

匹配器不直接输出一个字符串，而是输出一个搜索计划。

```csharp
public sealed record MetadataKeywordPlan(
    string PrimaryKeyword,
    string? SeasonAwareKeyword,
    string? SimplifiedKeyword,
    string BaseTitle,
    int? SeasonNumber,
    int? YearHint,
    bool IsAmbiguousShortKeyword,
    bool IsMovieLike);
```

计划来源：

1. 目录名清洗
2. 父目录名兜底
3. 文件名兜底

### 8.2 清洗步骤

清洗时去掉或抽取：

- 字幕组
- 分辨率、编码、音频标签
- 年份
- 季度标记，如 `S2`、`Season 2`、`第二季`
- 剧场版标记，如 `Movie`、`剧场版`
- 批次与集数范围

### 8.3 搜索尝试顺序

推荐固定顺序：

1. `PrimaryKeyword`
2. `SeasonAwareKeyword`
3. `BaseTitle + Movie`，仅限剧场版场景
4. `BaseTitle`
5. `SimplifiedKeyword`

### 8.4 候选评估

必须保守：

- 仅接受动漫条目
- 对 `name` 与 `name_cn` 都做相似度比较
- 季度一致性优先于模糊标题相似度
- 剧场版不能轻易回落到 TV 正片
- 短而歧义大的标题直接拒绝自动绑定

### 8.5 结果分类

抓取结果只能落入三类：

1. `Success`
   - 命中明确 subject
   - 拉取 detail
   - 下载海报
   - 保存 payload
   - 状态置为 `Ready`

2. `NoMatch`
   - 搜不到
   - 候选冲突大
   - 置信度不足
   - 状态置为 `NeedsReview`

3. `Transient / Provider Failure`
   - 网络超时、连接失败、5xx
   - Bangumi 响应异常或 detail 缺失
   - 记录 `FailureKind`
   - 进入 cooldown

---

## 9. 命令与查询接口

建议将元数据模块分为 command 与 query 两类能力。

### 9.1 Command

```csharp
public interface IMetadataCommandService
{
    Task SyncLibrarySnapshotAsync(
        IReadOnlyList<MetadataFolderRef> folders,
        CancellationToken cancellationToken = default);

    Task RegisterFolderAsync(
        MetadataFolderRef folder,
        CancellationToken cancellationToken = default);

    Task DeleteFolderAsync(
        string folderId,
        CancellationToken cancellationToken = default);

    Task RefreshFolderAsync(
        string folderId,
        CancellationToken cancellationToken = default);

    Task RetryFolderAsync(
        string folderId,
        CancellationToken cancellationToken = default);

    Task EnqueueMissingAsync(
        CancellationToken cancellationToken = default);

    Task RetryFailedAsync(
        bool includeNoMatch,
        CancellationToken cancellationToken = default);
}
```

### 9.2 Query

```csharp
public interface IMetadataQueryService
{
    Task<MetadataDto?> GetByFolderAsync(
        string folderId,
        CancellationToken cancellationToken = default);

    Task<MetadataStatusSummaryDto> GetSummaryAsync(
        CancellationToken cancellationToken = default);
}
```

### 9.3 当前 `ProcessQueueAsync` 的去向

当前仓库中的 `ProcessQueueAsync(...)` 更像是模拟入口。接入真实 worker 后应逐步降级为：

- 测试辅助接口，或
- 彻底删除，由后台 worker 常驻消费替代

---

## 10. 后台线程与 Worker

### 10.1 谁来起线程

由 `AniNest.Host` 启动后台 worker，而不是 Flutter，也不是某次 API 请求临时创建线程。

推荐形态：

- 新增 `MetadataBackgroundWorker : BackgroundService`
- 在 Host 启动时随 DI 一起注册
- 全生命周期由 Host 托管

原因：

- 前端关闭后任务仍可继续
- 自动扫描与失败重试不依赖用户手动触发
- 生命周期清晰，便于恢复与测试

### 10.2 Worker 单并发

Phase 1 采用单线程、单并发消费：

- 降低 Bangumi 速率控制复杂度
- 降低状态一致性风险
- 足够覆盖当前动漫库规模

Worker 循环：

```text
wait signal
  -> dequeue next folder
  -> mark Scraping
  -> rehydrate folder video files
  -> build keyword plan
  -> search Bangumi
  -> evaluate candidates
  -> fetch subject detail
  -> download poster
  -> save payload + index
  -> publish folder_updated
  -> publish summary_changed
  -> continue
```

---

## 11. 调度设计

### 11.1 调度器职责

推荐新增 `MetadataTaskScheduler`，职责包括：

- 管理内存队列
- 去重
- 唤醒 worker
- 判断 cooldown
- 处理优先级

### 11.2 入队来源

1. Library 启动后的 `SyncLibrarySnapshotAsync`
2. 新增目录 `RegisterFolderAsync`
3. 用户手动 `RefreshFolderAsync`
4. 用户手动 `RetryFolderAsync`
5. 设置页 `EnqueueMissingAsync`
6. 设置页 `RetryFailedAsync`

说明：

- 程序启动后的第一次入队来源于 `SyncLibrarySnapshotAsync(...)`
- Metadata 自己根据当前 Library 快照生成 plan 并入队
- Library 不直接操作工作队列

### 11.3 优先级建议

从高到低：

1. 用户手动刷新单目录
2. 新增目录
3. 自动补全 `NeedsMetadata`
4. 重试失败项

建议用任务计划而不是裸 `folderId` 入队，例如：

```csharp
public sealed record MetadataTaskPlan(
    string FolderId,
    MetadataTaskReason Reason,
    int Priority,
    bool BypassCooldown);
```

其中 `Reason` 可以区分：

- `MissingMetadata`
- `LibraryImport`
- `LibraryReconcile`
- `ManualRefresh`
- `RetryFailed`

### 11.4 Cooldown 策略

- `NetworkError`: 30 分钟
- `ProviderError`: 1 天
- `NoMatch`: 7 天

规则：

- 自动流程尊重 cooldown
- 用户手动 `RefreshFolderAsync` 可绕过 cooldown
- `RetryFailedAsync(includeNoMatch: false)` 只重试瞬时错误

---

## 12. 启动恢复与 Reconcile

元数据子系统的中心不是单个 folder 的刷新，而是对当前 Library 快照做对账。

### 12.1 启动流程

```text
Host start
  -> load metadata index
  -> normalize stale Queued/Scraping to NeedsMetadata
  -> load library snapshot
  -> reconcile records with current folders
  -> enqueue eligible items if auto metadata enabled
  -> start worker loop
```

注意：

- Metadata 后台服务可以先启动
- 但真正决定要处理哪些任务，依赖 Library 侧提供的当前 folder snapshot
- Metadata 不负责自己扫描磁盘找库

### 12.2 Reconcile 步骤

1. 读取全部 metadata records
2. 回滚陈旧运行时状态
3. 构建当前 library folder 集合
4. 删除已经不在库中的 orphan records
5. 为新的 folder 建立 record
6. 为已有 folder 修正 `FolderName`、`FolderPath`
7. 对可自动抓取的 `NeedsMetadata` 项入队
8. 发布 summary 变化

这里的重点不是立即抓取，而是先让 Metadata 状态与 Library 事实对齐。

### 12.3 Library 可见状态

Library 不应只知道 metadata 的标题摘要，还应知道当前 folder 的 metadata 生命周期位置。

建议至少暴露以下信息给库页与 inspector：

- `HasMetadata`
- `MetadataState`
- `Title`
- `PosterUrl`

这样库页可以区分：

- 还没有 metadata
- 已入队
- 正在抓取
- 已完成
- 需要人工关注

也就是说，Library 对 metadata 的接入不是“拿一份 title”，而是“拿一份最小生命周期摘要”。

### 12.3 可选增强

后续可以加入基于 `FolderFingerprint` 的 rename / move 迁移逻辑，避免目录改名后丢失已抓取结果。Phase 1 可以先不做复杂迁移，只保留 fingerprint 字段。

---

## 13. 输出设计

输出分三类。

### 13.1 Metadata 详情输出

用于 `GET /api/metadata/folders/{folderId}`：

- `title`
- `originalTitle`
- `summary`
- `tags`
- `posterPath`
- `season`
- `episodeCount`
- `source`
- `state`
- `failureKind`

建议后续补充：

- `sourceId`
- `rating`
- `year`
- `airDate`

### 13.2 Library 摘要输出

用于 `LibraryFolderDto.metadataSummary`：

- `title`
- `posterUrl`
- `state`
- `rating`
- `year`

原则：

- 卡片不需要 Metadata 全量正文
- 列表只拿最小摘要，避免 DTO 过胖

### 13.3 事件输出

继续使用 SSE，建议保持：

- `metadata.folder_updated`
- `metadata.summary_changed`

`metadata.folder_updated` 用于：

- 更新卡片海报
- 更新 inspector

`metadata.summary_changed` 用于：

- 更新设置页状态统计
- 必要时更新库页顶部状态

---

## 14. Library 与 Flutter 集成

### 14.1 Library 侧

`Library` 的职责：

1. 正常加载文件夹与本地封面
2. 从 metadata query service 读取已有摘要
3. 立即返回 DTO 给前端
4. 后台触发 `SyncLibrarySnapshotAsync(...)`

这样可以保证：

- 首屏快
- 元数据不阻塞列表
- 已有缓存可以立即显示

### 14.2 Flutter 侧

Flutter 只做消费：

1. 初始读取
   - `GET /api/library/folders`
   - `GET /api/metadata/status-summary`
2. 监听 SSE
   - `metadata.folder_updated`
   - `metadata.summary_changed`
3. 收到事件后刷新当前 folder 或状态摘要

Flutter 不负责：

- provider 调用
- 匹配决策
- poster 下载
- 后台任务调度

---

## 15. 配置项

建议补充以下配置：

```csharp
public sealed record MetadataSettingsDto(
    bool AutoScrapeMetadata,
    string? BangumiAccessToken);
```

后续可扩展：

- `RequestTimeoutSeconds`
- `RetryBackoffSeconds`
- `NoMatchCooldownDays`
- `NetworkCooldownMinutes`
- `ProviderCooldownHours`

Phase 1 先保留最少配置，避免设置面板膨胀。

---

## 16. 实施顺序

建议按以下顺序落地。

### Step 1: 存储与状态机

- 新增 `MetadataRecord`
- 新增 `MetadataIndexStore`
- 新增 `MetadataPayloadRepository`
- 新增 `MetadataPosterCache`
- 实现启动恢复逻辑

### Step 2: Bangumi Provider

- 新增 `BangumiMetadataProvider`
- 新增 provider DTO
- 实现 search / subject / image
- 加入认证、超时、错误分类

### Step 3: 匹配器

- 新增 `MetadataKeywordPlan`
- 新增 `MetadataMatcher`
- 实现目录名、父目录、文件名三层输入
- 实现严格评分策略

### Step 4: 调度与 Worker

- 新增 `MetadataTaskScheduler`
- 新增 `MetadataBackgroundWorker`
- 接入 command service
- 打通队列、状态切换、事件发布

### Step 5: Library 接入

- Library load 时读取 metadata 摘要
- Add/Delete folder 时通知 metadata command service
- DTO 中填充 `metadataSummary`

### Step 6: Flutter 接入

- 补完 metadata summary 字段使用
- 监听 SSE 刷新 inspector 与卡片
- 设置页展示 metadata 状态与动作

---

## 17. 第一阶段明确不做

- 手动搜索弹窗
- 多 provider 聚合
- 导出 `nfo`
- 分集级元数据
- 高并发抓取
- 前端直接编辑 metadata

这些都可以等基础系统稳定后再加。

---

## 18. 结论

AniNest 的元数据能力应该实现为：

- 由 Host 托管的后台子系统
- 以 Library 快照为输入
- 以 Bangumi 为唯一数据源
- 以严格匹配和本地缓存为核心
- 以单 worker、可恢复调度为第一阶段执行模型

一句话概括：

> Library 负责提供目录事实，Metadata 子系统负责对账、排队、抓取、缓存和广播更新，Flutter 只负责展示结果。
