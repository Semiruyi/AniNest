# 文档导航

当前文档只保留仍然服务于 `AniNest.Host + Flutter` 主线的内容。

旧的 WPF / LibVLC 设计稿、迁移讨论和已失效方案已经移除，避免和现状混淆。

## 推荐阅读路径

- 想快速上手项目：先看仓库根目录的 [README.md](../README.md)，再看 [架构文档索引](zh/architecture/README.md)
- 想改后端功能：先看 [架构文档索引](zh/architecture/README.md)，再按需要进入元数据或测试文档
- 想改 Flutter 前端：先看 [frontend/aninest_flutter/README.md](../frontend/aninest_flutter/README.md)，再看 [Flutter 与后端对接契约](zh/frontend/flutter-backend-client-contract.md)
- 想理解元数据链路：按 [Bangumi 元数据设计](zh/architecture/metadata-bangumi-design.md) -> [元数据抓取流水线](zh/architecture/metadata-fetch-pipeline-design.md) -> [元数据运行时结构](zh/architecture/metadata-runtime-structure.md) 的顺序阅读

## 文档索引

### 架构

- [架构文档索引](zh/architecture/README.md)：架构文档总入口与推荐阅读路径
- [后端模块与 API](zh/architecture/backend-modules-and-api.md)：当前后端结构、模块职责、接口与调试页说明
- [Bangumi 元数据设计](zh/architecture/metadata-bangumi-design.md)：元数据能力的整体目标、状态语义与运行规则
- [元数据抓取流水线](zh/architecture/metadata-fetch-pipeline-design.md)：抓取执行链路、候选处理与任务推进方式
- [元数据运行时结构](zh/architecture/metadata-runtime-structure.md)：`AniNest.Host/Modules/Metadata` 目录分层与职责分布

### 前端

- [Flutter 与后端对接契约](zh/frontend/flutter-backend-client-contract.md)：REST API / SSE 边界与客户端职责

### 测试

- [后端测试工作流](zh/testing/backend-test-workflow.md)：常用测试命令、提交流程与当前验收基线

## 维护约定

- 根目录 `README.md` 只放项目概览、快速开始和主要入口
- `docs/README.md` 作为文档导航页，负责串联所有专题文档
- 具体专题文档尽量围绕“当前已落地实现”编写，避免混入旧方案讨论

## 术语约定

- `Host` 默认指 `src/AniNest.Host`
- `Flutter` 默认指 `frontend/aninest_flutter`
- “元数据”默认指当前基于 `Bangumi` 的动漫元数据子系统，除非文档另有说明
