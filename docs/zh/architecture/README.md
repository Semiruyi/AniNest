# 架构文档索引

本目录收纳当前 `AniNest.Host + Flutter` 主线相关的架构文档。

如果你只是想快速理解项目结构，建议从 [后端模块与 API](./backend-modules-and-api.md) 开始；如果你要进入元数据链路，再按下面的顺序继续读。

## 推荐阅读路径

- 想快速建立全局认识：先看 [后端模块与 API](./backend-modules-and-api.md)
- 想理解元数据目标与状态语义：看 [Bangumi 元数据设计](./metadata-bangumi-design.md)
- 想理解元数据执行链路：接着看 [元数据抓取流水线](./metadata-fetch-pipeline-design.md)
- 想落代码或调整目录分层：最后看 [元数据运行时结构](./metadata-runtime-structure.md)

## 文档索引

### 全局结构

- [后端模块与 API](./backend-modules-and-api.md)：后端结构、模块边界、接口与调试页说明

### 元数据

- [Bangumi 元数据设计](./metadata-bangumi-design.md)：元数据子系统的目标、约束、状态语义与整体规则
- [元数据抓取流水线](./metadata-fetch-pipeline-design.md)：从输入清洗到决策落库的运行链路
- [元数据运行时结构](./metadata-runtime-structure.md)：`AniNest.Host/Modules/Metadata` 目录分层与职责分布

## 相关入口

- [文档导航页](../../README.md)
- [Flutter 与后端对接契约](../frontend/flutter-backend-client-contract.md)
- [后端测试工作流](../testing/backend-test-workflow.md)
