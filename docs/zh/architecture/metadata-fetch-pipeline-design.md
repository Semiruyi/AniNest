# AniNest 元数据抓取流水线设计

本文档补充 AniNest 动漫元数据子系统中的“实际抓取执行流水线”职责划分。

目标不是把所有逻辑塞进一个 worker，而是把“清洗本地输入”、“向 Bangumi 获取候选”、“做保守可信判断”、“把判断结果落成系统状态”拆成四段，便于调试、演进和回放日志。

---

## 1. 总体结构

Runtime 内部抓取链路建议固定为：

```text
MetadataRecord
  -> Preparation
  -> Acquisition
  -> Confidence
  -> Resolution
  -> RuntimeStateService.Save / Publish
```

四段职责分别是：

1. `Preparation / 清洗`
2. `Acquisition / 获取`
3. `Confidence / 可信判断`
4. `Resolution / 决策落库`

---

## 2. Preparation / 清洗

### 输入

- `MetadataRecord`
- `FolderPath`
- `FolderName`
- `ParentFolderName`
- `VideoFiles`
- `VideoCount`

### 职责

- 重新构建 worker 执行阶段所需的 `MetadataFolderRef`
- 清理目录名、父目录名、视频文件名中的噪声
- 识别季数、年份、剧场版/OVA 倾向等结构化线索
- 生成后续搜索使用的关键词计划

### 输出

- `MetadataFolderRef`
- `MetadataKeywordPlan`
- 规范化标题
- 别名列表
- 季数提示、年份提示、格式提示

### 边界

> Preparation 只负责把本地输入整理干净，不直接访问 Bangumi，也不决定哪个候选可信。

### 风险点

- 如果 worker 只拿 `FolderId` 或旧 `MetadataRecord`，而不重新拿 `VideoFiles`，清洗质量会明显下降
- 文件名 fallback 是动漫目录匹配质量的重要来源，不能在后台执行阶段丢掉

---

## 3. Acquisition / 获取

### 输入

- `MetadataPreparedContext`

### 职责

- 使用清洗后的关键词计划向 Bangumi 发起搜索
- 拉取候选条目的详情
- 统一封装 provider 返回结果
- 区分搜索失败、无候选、详情失败等不同失败类别

### 输出

- `MetadataAcquisitionResult`
- `MetadataAcquisitionCandidate[]`

### 边界

> Acquisition 只负责尽可能拿到候选和详情，不负责拍板哪个结果最终采用。

### 风险点

- 不能只拿搜索第一条就直接落库
- 不能把网络失败和“无匹配结果”混成同一种失败

---

## 4. Confidence / 可信判断

### 输入

- `MetadataPreparedContext`
- `MetadataAcquisitionResult`

### 职责

- 对候选执行保守评分
- 综合标题、别名、季数、年份、剧场版/TV 倾向、分集数等信号
- 给出明确理由，而不是只给一个黑盒总分
- 支持硬性否决条件，例如季数冲突、格式冲突、年份差距过大

### 输出

- `MetadataConfidenceResult`
- `BestCandidate`
- `ConfidenceLevel`
- `Reasons`

### 边界

> Confidence 负责评估可信度，但不直接写 record，也不直接推进状态机。

### 风险点

- “标题有点像”不能自动进入 `Ready`
- `NoMatch` 通常优于错误自动匹配

---

## 5. Resolution / 决策落库

### 输入

- `MetadataRecord`
- `MetadataPreparedContext`
- `MetadataAcquisitionResult`
- `MetadataConfidenceResult`

### 职责

- 把评分结果转换成系统状态决策
- 决定是进入 `Ready`、`NeedsReview`、`NoMatch` 还是继续保留 `NeedsMetadata`
- 生成最终 payload / poster 下载计划 / sourceId 绑定结果
- 将后续状态推进与副作用交给 `MetadataRuntimeStateService` 执行

### 输出

- `MetadataResolutionResult`
- `NextState`
- `FailureKind`
- `SelectedPayload`

### 边界

> Resolution 是拍板层，负责把分析结果变成可落库的系统动作。

### 为什么要单独保留 Resolution

如果只保留“清洗 / 获取 / 判断”三块，后面很容易把：

- 置信度规则
- 状态机推进
- 失败分类
- payload 落库

混在一个服务里。单独保留 `Resolution` 的意义是：

- 让评分规则和状态规则分开
- 让 `NeedsReview` / `NoMatch` / `Ready` 的行为可独立演进
- 让 RuntimeStateService 保持执行者角色，而不是变成策略中心

---

## 6. 当前落地策略

当前阶段先做这四段的骨架和编排服务，先不直接替换已有 placeholder 执行路径。

这样做的好处是：

- 不破坏当前已验证通过的后台生命周期
- 可以先把职责边界、日志点和中间模型搭稳
- 后续再把真实 Bangumi 抓取逻辑逐步接入 pipeline

下一步接线顺序建议是：

1. 用 `Preparation` 替换掉 worker 中对 folder 上下文的临时推断
2. 用 `Acquisition` 接入真实 Bangumi 搜索与详情拉取
3. 用 `Confidence` 做保守匹配
4. 用 `Resolution` 决定 `Ready / NeedsReview / NoMatch`
