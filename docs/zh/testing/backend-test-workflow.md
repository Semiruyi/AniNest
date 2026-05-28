# Backend Test Workflow

当前后端工作的验证基线主要是：

- `src/AniNest.Host`
- `src/AniNest.Application`
- `src/AniNest.Contracts`
- `src/Tests.Backend`

旧 UI 工程已经不再是后端重构的验收标准。

## 常用命令

在仓库根目录执行。

### 1. 跑完整后端测试

```powershell
dotnet test src/Tests.Backend/AniNest.Backend.Tests.csproj -m:1
```

说明：

- `-m:1` 用来避开 Windows 下偶发的测试产物文件锁问题
- 这是默认的提交前检查

### 2. 只跑某一类测试

例如只跑 Library：

```powershell
dotnet test src/Tests.Backend/AniNest.Backend.Tests.csproj --filter Library -m:1
```

例如只跑 Metadata：

```powershell
dotnet test src/Tests.Backend/AniNest.Backend.Tests.csproj --filter Metadata -m:1
```

例如只跑 Thumbnail：

```powershell
dotnet test src/Tests.Backend/AniNest.Backend.Tests.csproj --filter Thumbnail -m:1
```

### 3. 跑单个测试类

```powershell
dotnet test src/Tests.Backend/AniNest.Backend.Tests.csproj --filter "FullyQualifiedName~HostScaffoldTests" -m:1
```

或者：

```powershell
dotnet test src/Tests.Backend/AniNest.Backend.Tests.csproj --filter "FullyQualifiedName~LibraryCatalogServiceTests" -m:1
```

### 4. 单独构建 Host

```powershell
dotnet build src/AniNest.Host/AniNest.Host.csproj
```

适用于改了这些内容之后做一遍快速确认：

- DI 注册
- API endpoint
- Host 模块装配
- 配置路径解析

## 当前覆盖重点

### 应用层与存储

现有测试覆盖：

- `LibraryCatalogService`
- `PlaybackSessionEngine`
- `SettingsService`
- 文件存储：Library / PlaybackProgress / Settings / Thumbnail
- `ResourceLocator`

### Host 集成

`HostScaffoldTests` 当前已经覆盖：

- `GET /api/settings`
- `GET /api/library/folders`
- `GET /api/playlist/current`
- `POST /api/session/open-folder`
- `POST /api/session/progress`
- `POST /api/session/complete`
- Metadata 查询、重试、入队、处理
- Thumbnail 摘要、优先化、清理、处理
- `GET /api/events` 与关键事件发布

## 推荐工作流

### 改应用服务或单模块逻辑时

1. 先跑对应测试类
2. 再跑完整后端测试

### 改 endpoint、DI 或配置接线时

1. 跑对应测试类
2. 跑 `HostScaffoldTests`
3. 跑完整后端测试
4. 补一遍 `dotnet build src/AniNest.Host/AniNest.Host.csproj`

## 不再作为当前验收基线的内容

这些路径如果还存在，也不是后端重构的主要回归目标：

- 旧 WPF UI 工程
- 旧播放壳层工程
- 任何依赖旧 UI 行为的人工回归说明
