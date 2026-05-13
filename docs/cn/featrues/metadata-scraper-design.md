# 元数据刮削功能设计文档

## 1. 背景与目标

### 1.1 背景
AniNest 当前 Library 页面以文件夹卡片形式展示用户的本地视频库。卡片封面依赖于本地 `cover.jpg`、`folder.jpg` 等文件，若用户未手动放置封面图，则展示默认样式。对于番剧/动画内容，标题、简介、评分、放送日期等元数据完全缺失，浏览体验较为简陋。

### 1.2 Phase 1 目标
**本阶段只实现后台自动刮削**，核心目标：
- 添加文件夹后，后台自动从 Bangumi 获取对应番剧元数据。
- **识别机制严格**：低置信度时宁可不刮削，也不错误匹配。
- Library 卡片自动刷新封面：本地 cover 优先，无本地 cover 时展示 Bangumi 海报。
- 支持将元数据**自动导出**到视频文件夹（`tvshow.nfo` + `poster.jpg`），由用户设置控制开关。
- 架构预留扩展点，后续可平滑增加手动搜索、单集 NFO、多数据源等功能。

### 1.3 范围限定
- **数据源**：仅 Bangumi。架构预留 `IMetadataProvider` 扩展。
- **交互方式**：仅后台自动刮削，**本阶段不做手动搜索弹窗**。
- **内容类型**：面向动画/番剧（Bangumi `type = 2`）。
- **数量级**：个人本地库，通常 50-200 部。

---

## 2. 总体方案

新增独立的 `Metadata` Feature，作为 `Library` 的**可选数据增强层**。两者通过 `folderPath` 弱关联：

```
Library (现有)
  └── 加载文件夹列表
  └── 扫描视频文件 / 本地封面
  └── [新增] 读取关联的 FolderMetadata
  └── 组装 DTO → 绑定 EffectiveCoverPath → UI

Metadata (新增)
  └── 清洗文件夹名 → 搜索关键词
  └── Bangumi API：搜索 → 严格匹配确认 → 获取详情
  └── 下载海报到本地缓存
  └── 保存元数据 JSON
  └── [可选] 自动导出 NFO + poster.jpg 到视频文件夹
```

**关键原则**：
- **本地封面优先**：用户手动放置的 `cover.jpg` 永远优先于 Bangumi 海报。
- **严格匹配**：自动刮削不满足高置信度条件时，静默跳过，宁可留白也不误匹配。
- **零侵入**：无元数据时，Library 的所有现有行为完全一致。

---

## 3. 架构设计

### 3.1 分层结构

```
┌─────────────────────────────────────────────────────────────┐
│  UI Layer (View / ViewModel)                                │
│  卡片封面绑定、设置项                                         │
├─────────────────────────────────────────────────────────────┤
│  App Service Layer                                          │
│  LibraryAppService：加载库时读取 Metadata；添加时注册任务    │
├─────────────────────────────────────────────────────────────┤
│  Metadata Domain Layer (Features/Metadata/Services)         │
│  ├─ IMetadataScraperService ── MetadataScraperCoordinator   │
│  │   ├─ MetadataTaskStore（待刮削队列）                      │
│  │   ├─ MetadataWorker（后台单线程消费）                     │
│  │   └─ MetadataScrapeIndex（持久化索引，防重复刮削）        │
│  ├─ IMetadataProvider ── BangumiMetadataProvider            │
│  ├─ IMetadataRepository ── MetadataRepository               │
│  ├─ MetadataImageCache                                      │
│  ├─ MetadataMatcher                                         │
│  └─ MetadataExporter                                        │
├─────────────────────────────────────────────────────────────┤
│  External Layer                                             │
│  Bangumi API (api.bgm.tv) / 本地文件系统                    │
└─────────────────────────────────────────────────────────────┘
```

### 3.2 核心抽象

| 抽象 | 类型 | 职责 |
|------|------|------|
| `IMetadataScraperService` | 接口 | 元数据刮削协调器。Library 通过它注册/删除文件夹任务，内部自己调度执行。 |
| `MetadataScraperCoordinator` | 类 | 协调器实现：维护队列、单线程 Worker、限流、状态跟踪、跨线程 UI 通知。 |
| `MetadataScrapeIndex` | 类 | 轻量索引（JSON）：记录每个 folderPath 的刮削状态与最后尝试时间，防止跨会话重复刮削。 |
| `IMetadataProvider` | 接口 | 数据源抽象：搜索 + 获取详情。本阶段仅有 Bangumi 实现。 |
| `IMetadataRepository` | 接口 | 本地元数据读写（JSON）。 |
| `MetadataImageCache` | 类 | 海报下载与本地缓存。 |
| `MetadataMatcher` | 静态类 | 文件夹名清洗，输出搜索关键词。详见[识别策略文档](metadata-matching-strategy.md)。 |

### 3.3 扩展性

后续增加数据源或功能时，改动范围：
- 新增数据源：实现 `IMetadataProvider`。
- 手动搜索弹窗：在 UI 层新增窗口，`MetadataScraperCoordinator` 中暴露 `SearchCandidatesAsync`。
- 单集 NFO：`IMetadataProvider` 新增 `GetEpisodesAsync`，`MetadataExporter` 扩展单集导出。
- 多尺寸图片：`MetadataImageCache` 扩展缓存策略。

Library 层（`LibraryAppService`、`FolderListItem`、卡片模板）**无需改动**。

---

## 4. 数据链路

### 4.1 库加载链路（读）

```
用户打开 Library
  → MainPageViewModel.LoadDataAsync()
    → LibraryAppService.LoadLibraryAsync()
      ├─ VideoScanner.ScanFolderAsync(path)
      │   返回：videoCount, localCoverPath, videoFiles
      ├─ MetadataRepository.Get(path)
      │   返回：FolderMetadata?（纯本地文件读取，毫秒级）
      └─ 组装 LibraryFolderDto(含 Metadata)
    → CreateFolderItem(dto)
      → FolderListItem.EffectiveCoverPath
         优先级：localCoverPath > Metadata.LocalPosterPath > 默认封面
  → 绑定到卡片模板
```

**设计决策**：`LoadLibraryAsync` **只读取**已有元数据，**不入队刮削**。避免每次打开 Library 都重复注册任务。

### 4.2 自动刮削链路（写，后台异步）

模仿缩略图系统的协调器模式。`LibraryAppService` 在**添加新文件夹时**注册任务，实际执行由 `MetadataScraperCoordinator` 内部调度：

```
LibraryAppService.AddFolderAsync / AddFolderBatchAsync 成功
  → _metadataScraperService.RegisterFolder(path, folderName)
    ├─ MetadataScrapeIndex.Get(path)
    │   ├─ status == Completed  → 跳过（已有数据）
    │   ├─ status == Failed && 尝试时间在 7 天内 → 跳过（避免反复重试）
    │   └─ 其他 → 继续判断
    ├─ (若 AutoScrapeMetadata == true)
    │   → 加入内部待刮削队列（MetadataTaskStore）
    │   → MetadataScrapeIndex.Update(path, status: Pending)
    │   → 唤醒后台 Worker（若未运行）
    └─ (若 AutoScrapeMetadata == false)
        → MetadataScrapeIndex.Update(path, status: Skipped)

MetadataScraperCoordinator（后台单线程 Worker）
  → 从队列取出 Pending 任务
  → MetadataScrapeIndex.Update(path, status: Scraping)
  → 调用 BangumiMetadataProvider.Search(keyword)
  → 严格匹配验证（详见 4.4）
  │   ├─ 全部通过 → 继续
  │   └─ 任一失败 → MetadataScrapeIndex.Update(path, status: Failed)
  │                 → 记录日志 → 结束本轮
  → BangumiMetadataProvider.GetDetail(sourceId)
  → MetadataImageCache.Download(posterUrl, hash)
  → MetadataRepository.Save(metadata)
  → MetadataScrapeIndex.Update(path, status: Completed)
  → (若 AutoExportMetadata == true)
  │   → MetadataExporter.ExportAsync(path)
  → 触发 FolderMetadataRefreshed 事件（通过 Dispatcher 回到 UI 线程）
  → 等待 1000ms（限流）
  → 消费下一个任务
```

**与缩略图系统的对齐点**：
- `RegisterFolder` ≈ `RegisterCollection`：Library 只注册，不直接执行。
- `DeleteFolder` ≈ `DeleteCollection`：删除时同步清理队列、状态、缓存。
- 后台 Worker 单线程串行消费 ≈ `ThumbnailWorkerPool`（元数据因限流只需单线程）。
- 状态跟踪（Pending / Scraping / Completed / Failed / Skipped）≈ 缩略图的 `ThumbnailState`。
- 是否已完成以磁盘文件 + 索引为准 ≈ 缩略图以索引文件/缩略图文件存在性为准。
- 跨线程进度通知 ≈ `ThumbnailProgressChanged`。

### 4.3 限流与容错

- **全局限流**：`BangumiMetadataProvider` 内部使用 `SemaphoreSlim(1)`，所有搜索/详情请求串行执行，间隔 **1000ms**。
- **重试策略**：网络超时/异常时，指数退避重试最多 2 次（2s → 4s）。HTTP 404 不重试，直接标记失败。
- **失败处理**：任何原因导致刮削失败（无结果、匹配不通过、网络故障、图片下载失败），均**静默跳过**，记录日志，不弹窗、不阻断 UI。

### 4.4 严格匹配策略

自动刮削必须同时满足以下全部条件，否则放弃：

| 条件 | 阈值 | 说明 |
|------|------|------|
| 搜索返回非空 | `candidates.Count > 0` | Bangumi 至少返回一条结果。 |
| 类型为动画 | `type == 2` | 请求时已固定 filter。 |
| 标题相似度 | `>= 0.5` | 清洗后的关键词与 Bangumi `name_cn`（或 `name`）的相似度。中文基于 LCS，英文基于 Levenshtein。 |
| 非短词歧义 | 中文 `>= 3` 字 / 英文 `>= 6` 字符 | "Fate"、"AB" 等易歧义短词拒绝自动匹配。 |
| 年份匹配（可选） | 若关键词含 4 位年份，Top1 年份误差 `<= 1` | 增强精确度，非强制。 |

### 4.5 跨线程 UI 刷新

`MetadataWorker` 运行在后台线程，完成刮削后不可直接操作 `ObservableCollection`。通知链路：

```
MetadataScraperCoordinator（后台线程）
  → FolderMetadataRefreshed?.Invoke(folderPath)
    → Application.Current.Dispatcher.BeginInvoke(...)
      → MainPageViewModel.OnFolderMetadataRefreshed(folderPath)
        → 在 FolderItems 中找到对应 FolderListItem
        → 更新 item.Metadata = 新数据
        → EffectiveCoverPath 自动变更 → 卡片刷新封面
```

`FolderListItem` 的 `Metadata` 属性使用 `ObservableObject` 的 `[ObservableProperty]`，变更时自动触发 `PropertyChanged`，卡片模板绑定响应。

---

## 5. UI 与设置

### 5.1 卡片展示

卡片模板使用 `FolderListItem.EffectiveCoverPath`：

1. **有本地 cover** → 展示用户本地封面图（尊重用户选择）。
2. **无本地 cover，有 Bangumi 海报** → 展示 Bangumi 海报。
3. **什么都没有** → 默认占位图。

**本阶段不做**：卡片上的标题/评分文字叠加、未刮削角标、手动搜索弹窗、右键菜单扩展。

### 5.2 设置项

`AppSettings` 新增三个字段：

```csharp
public bool AutoScrapeMetadata { get; set; } = true;      // 添加文件夹后自动后台刮削
public bool AutoExportMetadata { get; set; } = false;     // 刮削成功后自动导出 NFO + poster 到文件夹
public string? BangumiAccessToken { get; set; }           // 留空则匿名调用
```

- `AutoScrapeMetadata` 默认 `true`：新用户开箱即用。
- `AutoExportMetadata` 默认 `false`：导出会修改用户文件夹内容，默认关闭，由用户主动开启。
- 设置页面预留这三个输入控件，本阶段可先以简单的 `InputBox` 或配置项形式存在。

---

## 6. 数据持久化

### 6.1 元数据主体

- **位置**：`{AppPaths.CacheDirectory}/metadata/{folderPathHash}.json`
- **格式**：独立 JSON，每文件夹一份。
- **hash**：`MD5(folderPath)` 前 16 位。
- **内容**：`FolderMetadata` 序列化。

```csharp
public class FolderMetadata
{
    public string FolderPath { get; set; } = "";
    public string? Title { get; set; }           // name_cn fallback name
    public string? OriginalTitle { get; set; }   // name
    public string? Summary { get; set; }
    public string? PosterUrl { get; set; }
    public string? LocalPosterPath { get; set; }
    public string? Date { get; set; }
    public double? Rating { get; set; }
    public int? Episodes { get; set; }
    public string? Platform { get; set; }
    public List<string> Tags { get; set; } = new();
    public string? SourceId { get; set; }
    public DateTime ScrapedAt { get; set; }
}
```

### 6.2 海报图片

- **位置**：`{AppPaths.CacheDirectory}/metadata/posters/{folderPathHash}.jpg`
- **来源**：Bangumi `images.common`（fallback `images.medium`）。
- **策略**：永久缓存，Bangumi 数据静态不变。

### 6.3 刮削索引（防重复）

- **位置**：`{AppPaths.CacheDirectory}/metadata/index.json`
- **作用**：记录每个文件夹的刮削状态与最后尝试时间，防止跨会话重复刮削和反复重试已知失败项。
- **格式**：

```json
{
  "C:\\Anime\\进击的巨人": {
    "status": "Completed",
    "sourceId": "55770",
    "lastAttemptAt": "2026-05-13T10:00:00Z"
  },
  "C:\\Anime\\Season 1": {
    "status": "Failed",
    "lastAttemptAt": "2026-05-13T10:05:00Z"
  }
}
```

- **重试冷却**：`Failed` 状态的条目，7 天内不再入队重试。超过 7 天可在后续版本中允许重试（如 Bangumi 数据已补充）。
- **删除时清理**：`DeleteFolder` 时同步从索引中移除对应条目。

### 6.4 自动导出到文件夹

由 `AutoExportMetadata` 开关控制。刮削成功后，若开关开启，`MetadataExporter` 自动在视频文件夹内生成：

| 文件 | 说明 |
|------|------|
| `tvshow.nfo` | Kodi/XBMC 标准 XML。包含 title、originaltitle、plot、genre、studio、year、rating、episode、uniqueid(bangumi) 等。 |
| `poster.jpg` | 复制自本地缓存的海报。 |

**覆盖策略**：若文件已存在，静默覆盖（应用主动生成的元数据）。
**失败处理**：文件夹只读/无权限/路径不在线（NAS 断开）时记录日志，不弹窗阻断。

#### NFO 示例

```xml
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<tvshow>
    <title>进击的巨人</title>
    <originaltitle>進撃の巨人</originaltitle>
    <showtitle>进击的巨人</showtitle>
    <season>1</season>
    <episode>25</episode>
    <plot>107年前...（Bangumi summary）</plot>
    <genre>动画</genre>
    <genre>动作</genre>
    <studio>WIT STUDIO</studio>
    <premiered>2013-04-07</premiered>
    <year>2013</year>
    <rating>8.4</rating>
    <votes>25000</votes>
    <uniqueid type="bangumi" default="true">55770</uniqueid>
</tvshow>
```

### 6.5 生命周期管理

- **添加文件夹**：`AddFolderAsync` / `AddFolderBatchAsync` 中调用 `RegisterFolder`，由协调器判断是否入队。
- **删除文件夹**：`LibraryAppService.DeleteFolderAsync` 调用 `_metadataScraperService.DeleteFolder`，同步清理：
  - `MetadataRepository.Delete`
  - `MetadataImageCache.Delete`
  - `MetadataScrapeIndex.Delete`
- **修改文件夹名**：不触发重新刮削。Bangumi 数据静态，无需因重命名而刷新。

---

## 7. 职责分离

| 组件 | 负责 | 明确不负责 |
|------|------|-----------|
| `BangumiMetadataProvider` | Bangumi HTTP 请求、JSON 映射、全局限流。 | 本地存储、UI、匹配决策。 |
| `MetadataRepository` | 本地元数据 JSON 读写删。 | 网络、图片、业务判断。 |
| `MetadataScrapeIndex` | 索引 JSON 读写删（状态/时间）。 | API 调用、图片。 |
| `MetadataImageCache` | 海报下载、本地缓存路径管理。 | 元数据解析。 |
| `MetadataMatcher` | 文件夹名清洗。 | I/O。 |
| `MetadataScraperCoordinator` | 编排自动刮削全流程：注册 → 索引检查 → 入队 → 单线程消费 → 限流 → 严格匹配 → 详情 → 缓存 → 保存 → 索引更新 → 可选导出 → UI 通知。 | 不直接操作 UI 控件，只通过 Dispatcher 事件通知。 |
| `MetadataExporter` | NFO 序列化、海报复制到视频文件夹。 | API 调用。 |
| `LibraryAppService` | 加载时读取 Metadata 注入 DTO；添加时 RegisterFolder；删除时 DeleteFolder。 | Bangumi API。 |
| `MainPageViewModel` | 订阅 `FolderMetadataRefreshed` 事件，在 UI 线程更新对应 FolderListItem。 | 不直接读写文件或发起网络请求。 |

---

## 8. 接口草案

### 8.1 Provider

```csharp
public interface IMetadataProvider
{
    Task<IReadOnlyList<MetadataSearchCandidate>> SearchAsync(string keyword, CancellationToken ct = default);
    Task<FolderMetadata?> GetDetailAsync(string sourceId, CancellationToken ct = default);
}

public record MetadataSearchCandidate(
    string SourceId,
    string Title,
    string? OriginalTitle,
    string? Year,
    double? Rating,
    string? PosterUrl,
    string? Summary);
```

### 8.2 Repository

```csharp
public interface IMetadataRepository
{
    FolderMetadata? Get(string folderPath);
    void Save(FolderMetadata metadata);
    void Delete(string folderPath);
}
```

### 8.3 Scraper Service（协调器接口）

```csharp
public interface IMetadataScraperService
{
    /// <summary>
    /// 注册文件夹到协调器。协调器内部查索引、依据 AutoScrapeMetadata 判断是否入队。
    /// 仅在添加新文件夹时调用，LoadLibraryAsync 中不调用。
    /// </summary>
    void RegisterFolder(string folderPath, string folderName);

    /// <summary>
    /// 删除文件夹时同步清理：队列、缓存、索引、元数据 JSON。
    /// </summary>
    void DeleteFolder(string folderPath);

    MetadataScrapeState GetFolderState(string folderPath);

    event EventHandler<FolderMetadataRefreshedEventArgs>? FolderMetadataRefreshed;
}

public enum MetadataScrapeState
{
    Completed,   // 索引或元数据 JSON 存在
    Pending,     // 已入队，等待执行
    Scraping,    // 正在刮削中
    Failed,      // 已尝试但失败（7 天内不再重试）
    Skipped      // AutoScrapeMetadata 关闭，未入队
}

public class FolderMetadataRefreshedEventArgs : EventArgs
{
    public string FolderPath { get; }
}
```

`MetadataScraperCoordinator` 内部职责：
1. `MetadataTaskStore`：内存队列，记录 Pending 任务。
2. `MetadataWorker`：后台单线程，串行消费队列，执行 Search → Match → Detail → Cache → Save → Export。
3. 每次 API 请求后休眠 1000ms，实现 Bangumi 限流。
4. `MetadataScrapeIndex`：跨会话持久化，Failed 项 7 天内不入队重试。
5. 完成后通过 `Dispatcher.BeginInvoke` 触发 `FolderMetadataRefreshed`。

### 8.4 Exporter

```csharp
public class MetadataExporter
{
    /// <summary>
    /// 将已缓存的元数据导出为 tvshow.nfo + poster.jpg 到 folderPath。
    /// 若文件夹只读/路径不存在则抛出 IOException，由协调器记录日志。
    /// </summary>
    public Task ExportAsync(string folderPath, CancellationToken ct = default);
}
```

---

## 9. Phase 1 实施步骤

### Step 1：基础设施（1 天）
1. 定义 `FolderMetadata` 模型、`IMetadataProvider`、`IMetadataRepository`。
2. 实现 `MetadataRepository`（JSON 读写）。
3. 实现 `MetadataScrapeIndex`（索引 JSON 读写，含 7 天冷却逻辑）。
4. 在 `AppPaths` 新增 `MetadataDirectory`、`MetadataPosterDirectory`。
5. `AppSettings` 新增 `AutoScrapeMetadata`、`AutoExportMetadata`、`BangumiAccessToken`。
6. `ServiceRegistration` 注册新服务。

### Step 2：Bangumi 接入与协调器（1 天）
1. 实现 `BangumiMetadataProvider`（Search + GetDetail + 全局限流 + 重试）。
2. 实现 `MetadataImageCache`（海报下载）。
3. 实现 `MetadataMatcher`（详见[识别策略文档](metadata-matching-strategy.md)）。
4. 实现 `MetadataScraperCoordinator`：
   - `RegisterFolder` / `DeleteFolder`
   - `MetadataTaskStore` + `MetadataWorker`（单线程队列消费）
   - 严格匹配验证 + 限流
   - `FolderMetadataRefreshed` 事件（含 Dispatcher）
5. 实现 `MetadataExporter`（NFO XML + 海报复制）。
6. 单元测试：Bangumi API DTO 映射、Matcher 清洗用例、相似度计算、索引冷却逻辑。

### Step 3：Library 集成（0.5 天）
1. `LibraryFolderDto` / `FolderListItem` 新增 `Metadata` 与 `EffectiveCoverPath`。
2. `LibraryAppService.LoadLibraryAsync` 加载时读取 `MetadataRepository`（只读，不入队）。
3. `LibraryAppService.AddFolderAsync` / `AddFolderBatchAsync` 中调用 `_metadataScraperService.RegisterFolder`。
4. `LibraryAppService.DeleteFolderAsync` 中调用 `_metadataScraperService.DeleteFolder`。
5. `MainPageViewModel` 订阅 `FolderMetadataRefreshed`，在 UI 线程更新对应卡片。
6. 卡片模板绑定 `EffectiveCoverPath`。

### Step 4：设置与端到端测试（0.5 天）
1. 集成 `AutoScrapeMetadata` / `AutoExportMetadata` / `BangumiAccessToken` 到设置页面。
2. 端到端测试：添加 20-50 个文件夹，验证：
   - 索引正确（Completed / Failed / Skipped）
   - 限流（1s 间隔）
   - 封面刷新
   - NFO 导出
   - 重启后 Failed 项不再重试

---

## 10. 后续演进方向

本阶段不做，但架构已预留扩展：

| 功能 | 演进说明 | 所需扩展 |
|------|---------|---------|
| **手动搜索弹窗** | 用户右键卡片，弹出候选列表手动选择匹配条目。 | UI：新增 `MetadataSearchDialog`；Coordinator：暴露 `SearchCandidatesAsync` 供弹窗调用。 |
| **多数据源** | 支持 TMDb、豆瓣等。 | 新增 `TmdbMetadataProvider` : `IMetadataProvider`。 |
| **单集 NFO** | 为每个视频文件生成对应集数的 `.nfo`。 | Provider：新增 `GetEpisodesAsync`；Exporter：扩展单集导出；需解析文件名中的集数。 |
| **多尺寸图片** | 缓存并导出 `fanart.jpg`、`banner.jpg` 等。 | `MetadataImageCache` 扩展多尺寸；`MetadataExporter` 增加文件输出。 |
| **刷新元数据** | 强制重新拉取已匹配条目的最新数据。 | `MetadataScraperCoordinator` 增加 `RefreshAsync`，跳过搜索直接按已有 `sourceId` 重新获取详情；同时重置索引中的冷却时间。 |
| **未刮削过滤** | Library 过滤栏增加"未刮削"选项。 | `LibraryFilter` 新增枚举；`MatchesSelectedFilter` 增加判断逻辑。 |
| **Bangumi 用户数据同步** | 登录 Bangumi 后同步"在看/看过"状态到 WatchStatus。 | OAuth 登录；调用 Bangumi 收藏 API。 |
