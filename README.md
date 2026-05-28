# AniNest

AniNest 现在的主线是一个以后端为中心的本地番剧库项目：

- `src/AniNest.Host`：ASP.NET Core Host，负责 API、SSE 事件流、文件存储与模块装配
- `src/AniNest.Application`：应用服务与模块边界
- `src/AniNest.Contracts`：前后端共享 DTO
- `src/AniNest.Core`：核心枚举与基础模型
- `frontend/aninest_flutter`：Flutter 桌面端壳层，消费 Host API
- `src/Tests.Backend`：后端测试

旧的 `WPF + LibVLC` 方案文档已经移除；当前仓库以 `Host + Flutter` 结构为准。

## 当前能力

- 媒体库目录管理、批量导入、服务端目录浏览
- 播放列表与播放会话恢复
- 设置读写
- 元数据状态、评审队列与刷新流程
- 缩略图状态、摘要、重建与清理
- SSE 事件流，用于驱动前端增量刷新

## 快速开始

### 运行后端

```powershell
dotnet run --project .\src\AniNest.Host\AniNest.Host.csproj
```

默认地址：

- `http://localhost:5275`
- 调试页：`http://localhost:5275/debug/index.html`

### 运行后端测试

```powershell
dotnet test .\src\Tests.Backend\AniNest.Backend.Tests.csproj -m:1
```

### 运行 Flutter 前端

```powershell
cd .\frontend\aninest_flutter
flutter pub get
flutter run -d windows
```

Flutter 默认连接 `http://localhost:5275`，也可以在应用内切换后端地址。

## 项目结构

```text
AniNest/
├─ docs/
├─ frontend/
│  └─ aninest_flutter/
└─ src/
   ├─ AniNest.Application/
   ├─ AniNest.Contracts/
   ├─ AniNest.Core/
   ├─ AniNest.Host/
   └─ Tests.Backend/
```

## 文档

- [docs/README.md](docs/README.md)：当前文档索引
- [docs/zh/architecture/backend-modules-and-api.md](docs/zh/architecture/backend-modules-and-api.md)：后端模块与 API 总览
- [docs/zh/architecture/metadata-bangumi-design.md](docs/zh/architecture/metadata-bangumi-design.md)：Bangumi 元数据设计
- [docs/zh/frontend/flutter-backend-client-contract.md](docs/zh/frontend/flutter-backend-client-contract.md)：Flutter 对接契约
- [docs/zh/testing/backend-test-workflow.md](docs/zh/testing/backend-test-workflow.md)：后端测试工作流

## 技术栈

- .NET 9
- ASP.NET Core Minimal API
- Flutter
- `media_kit`

## License

MIT
