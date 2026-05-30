# AniNest Flutter

AniNest 的 Flutter 桌面端壳层，负责消费 `AniNest.Host` 暴露的 REST API 与 SSE 事件流。

当前前端主线包括：

- 媒体库页
- 播放页
- 后端连接切换
- 设置加载与保存
- 元数据与缩略图信息展示

## 运行方式

先启动后端：

```powershell
dotnet run --project .\src\AniNest.Host\AniNest.Host.csproj
```

然后在当前目录运行：

```powershell
flutter pub get
flutter run -d windows
```

默认后端地址是 `http://localhost:5275`。

## 相关文档

- [仓库文档导航](../../docs/README.md)
- [Flutter 与后端对接契约](../../docs/zh/frontend/flutter-backend-client-contract.md)
- [后端模块与 API](../../docs/zh/architecture/backend-modules-and-api.md)

## Windows media_kit 修复

如果 Windows 构建在切换提交或刷新 pub cache 后再次失败，可重新执行：

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\fix-media-kit-windows-cache.ps1 -SeedCache -Proxy http://127.0.0.1:7890
```

`-Proxy` 可选。脚本会重新应用本地 `media_kit` Windows CMake 补丁，并预热 `%LOCALAPPDATA%\media_kit_libs_windows_video` 缓存。
