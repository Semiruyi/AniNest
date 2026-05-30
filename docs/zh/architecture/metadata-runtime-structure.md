# AniNest Metadata 模块结构说明

本文档说明当前 AniNest 元数据模块在 `AniNest.Host` 内部的目录结构、职责分层和运行主线。

## 适用范围

- 面向当前 `src/AniNest.Host/Modules/Metadata` 目录结构
- 关注代码应该放在哪里，以及运行时任务如何穿过这些层
- 不替代抓取策略与业务目标文档

## 相关代码

- `src/AniNest.Host/Modules/Metadata`
- `src/AniNest.Application/Metadata`
- `src/AniNest.Contracts/Metadata`

## 配套阅读

- [Bangumi 元数据设计](./metadata-bangumi-design.md)
- [元数据抓取流水线](./metadata-fetch-pipeline-design.md)

这份文档重点不是介绍 Bangumi 抓取策略本身，而是回答下面几个问题：

- 元数据模块现在有哪些层
- 每一层负责什么
- 运行时一次任务是怎么流动的
- 代码应该优先放到哪个目录

相关抓取流程设计可配合阅读：

- [metadata-fetch-pipeline-design.md](./metadata-fetch-pipeline-design.md)

---

## 1. 顶层目录

当前 `src/AniNest.Host/Modules/Metadata` 目录按职责分为：

```text
Metadata/
  Acquisition/
  Confidence/
  Configuration/
  Preparation/
  Providers/
  Resolution/
  Runtime/
  Storage/
  MetadataModule.cs
```

各目录职责如下：

### `Preparation/`

负责本地输入清洗与规范化。

典型职责：

- 清理目录名、文件名噪声
- 提取季数、年份、剧场版/OVA 等提示
- 生成搜索关键词计划

### `Acquisition/`

负责向 Bangumi 发起搜索与详情获取。

典型职责：

- 多关键词搜索
- 候选去重
- 候选详情拉取
- 统一封装 provider 返回结果

### `Confidence/`

负责候选评分与可信判断。

典型职责：

- 标题/别名匹配
- 季数与年份信号判断
- TV / OVA / 剧场版冲突判断
- 给出候选评分与理由

### `Resolution/`

负责把评分结果转换成系统决策。

典型职责：

- 决定 `Ready / NeedsReview / NeedsMetadata`
- 生成最终 payload 候选
- 生成 review record
- 统一失败语义

### `Providers/`

负责外部元数据源接入。

当前主要是：

- `BangumiMetadataProvider`

### `Storage/`

负责 Metadata 模块自己的持久化实现。

当前主要是文件存储实现：

- metadata 主存储
- record 存储
- review 存储
- payload 存储
- poster 缓存

### `Runtime/`

负责运行期编排、状态推进、任务调度和 review 操作。

这是当前模块里最核心的执行层。

### `Configuration/`

负责模块默认值与存储初始化默认数据。

---

## 2. Runtime 子目录

当前 `Runtime/` 内部已经继续按职责拆分：

```text
Runtime/
  Bootstrap/
  Orchestration/
  Pipeline/
  Planning/
  Projection/
  Queue/
  Review/
  State/
  Storage/
```

### `Bootstrap/`

负责运行时启动准备。

当前职责：

- `EnsureInitialized()`
  - 当新的 runtime record store 为空时，从 legacy metadata 导入
- `NormalizeTransientStates()`
  - 将中断残留的 `Queued / Scraping` 状态归一为稳定状态

代表文件：

- `IMetadataRuntimeBootstrapService`
- `MetadataRuntimeBootstrapService`

### `Orchestration/`

负责运行时对外编排与任务调度。

当前职责：

- library snapshot 同步
- refresh / retry
- enqueue missing
- retry failed
- manual process queue
- background worker

代表文件：

- `MetadataLifecycleService`
- `MetadataOrchestrationService`
- `MetadataBackgroundService`
- `MetadataTaskScheduler`

这里要区分两个角色：

1. `MetadataLifecycleService`
   - 对外门面
   - 面向 module / endpoint 暴露统一调用入口
   - 当前已经尽量保持“薄门面”

2. `MetadataOrchestrationService`
   - 真正做 snapshot、refresh、enqueue、process queue 的编排逻辑

### `Pipeline/`

负责串起一次抓取执行链。

当前主线：

```text
Preparation
  -> Acquisition
  -> Confidence
  -> Resolution
```

代表文件：

- `IMetadataFetchPipeline`
- `MetadataFetchPipeline`

### `Planning/`

负责把“应该做什么”转换成任务计划。

典型职责：

- snapshot sync 时生成 upsert/delete/task plan
- refresh 生成单 folder task plan
- retry failed / enqueue missing 生成批量计划

### `Projection/`

负责把内部存储状态投影成对外可读 DTO。

当前职责：

- `MetadataRecord + FolderMetadataPayload -> MetadataDto`
- 构建 summary
- 构建 folder state summary

这层不负责状态推进，只负责“怎么看”。

### `Queue/`

负责元数据任务排队。

当前职责：

- 入队
- 出队
- worker 消费

### `Review/`

负责人工复核操作。

当前职责：

- 读取 review 队列
- 按 folderId 读取 review
- confirm 某个 sourceId
- reject 某个 candidate

这层不负责 library snapshot，也不负责 pipeline 自身。

### `State/`

负责状态推进，是 runtime 执行器。

当前职责：

- 维护 `MetadataRecord`
- 调用 pipeline 执行一次任务
- 应用 resolution 结果
- 保存最终状态
- 发布 runtime 事件
- placeholder fallback

这个目录中的 `MetadataRuntimeStateService` 应被理解为：

> “执行状态决策”的服务，而不是“负责所有元数据相关事情”的总管。

### `Storage/`

负责 runtime 内部使用的资产落盘辅助，而不是底层文件存储实现本身。

当前职责：

- payload 写入
- poster 下载与缓存
- legacy payload 初始化
- 资产删除

这里的 `Runtime/Storage` 和外层 `Metadata/Storage` 的区别是：

- `Metadata/Storage`
  - 底层存储实现
  - 关注“怎么存”
- `Runtime/Storage`
  - 运行期资产操作
  - 关注“什么时候保存、替换、删除这些资产”

---

## 3. 当前主线

现在一次 metadata 任务的主线大致如下：

```text
LibraryModule
  -> MetadataLifecycleService
  -> MetadataOrchestrationService
  -> MetadataTaskQueue
  -> MetadataBackgroundService
  -> MetadataRuntimeStateService.ExecuteAsync
  -> MetadataFetchPipeline
     -> Preparation
     -> Acquisition
     -> Confidence
     -> Resolution
  -> MetadataRuntimeStateService.ApplyResolution
  -> MetadataAssetService
  -> Publish events / update summary
```

如果是人工 review：

```text
Endpoint / Module
  -> MetadataLifecycleService
  -> MetadataReviewService
  -> MetadataAssetService
  -> MetadataRuntimeStateService.SaveRecord / PublishFolderState
```

---

## 4. 当前职责边界

为了后续继续演进，当前建议遵守这些边界：

### 可以放在 `Preparation / Acquisition / Confidence / Resolution`

- 与一次抓取分析直接相关的逻辑
- 与 Bangumi 候选匹配相关的逻辑
- 与候选评分相关的逻辑

### 可以放在 `Runtime/State`

- 状态推进
- 执行一次 pipeline
- 应用 resolution
- 事件发布

### 可以放在 `Runtime/Orchestration`

- snapshot sync
- refresh / retry / enqueue
- queue 驱动

### 可以放在 `Runtime/Review`

- 人工确认
- 人工拒绝候选
- review record 维护

### 可以放在 `Runtime/Storage`

- payload/poster 资产写入或删除策略
- 运行时资产替换逻辑

### 不建议继续塞进 `MetadataRuntimeStateService`

- review 业务
- snapshot 规划
- DTO 投影
- payload/poster 细节处理
- 启动导入与脏状态修复

这些都已经拆出去了，后续应保持这个边界，不要再回流。

---

## 5. 当前目录地图的意义

这次整理后的目标不是“目录看起来更漂亮”，而是为了让后续开发更稳定：

1. 看文件位置就能大致判断职责
2. 修改 review 时不用碰 runtime state
3. 修改资产缓存时不用碰 orchestration
4. 修改 pipeline 评分时不用碰生命周期编排
5. 后续加第二数据源时，主要影响 `Providers / Acquisition / Confidence / Resolution`

---

## 6. 后续建议

在当前结构基础上，下一步更值得做的是：

1. 给 runtime 结构补少量单元测试
   - 尤其是 review、resolution 应用、asset 保存替换

2. 逐步减少 legacy metadata 同步路径的存在感
   - 最终让 runtime record/payload 成为主数据

3. 如果后续支持多数据源
   - 在 `Providers/` 下扩 provider
   - 在 `Acquisition/` 增加 provider 聚合策略
   - 在 `Confidence/` 增加跨源候选比较规则

4. 如果后续支持更强的人审
   - 在 `Review/` 内部继续拆出 command/query
   - 增加“候选耗尽后自动 replan”
