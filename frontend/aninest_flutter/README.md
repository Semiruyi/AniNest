# aninest_flutter

A new Flutter project.

## Getting Started

This project is a starting point for a Flutter application.

A few resources to get you started if this is your first Flutter project:

- [Learn Flutter](https://docs.flutter.dev/get-started/learn-flutter)
- [Write your first Flutter app](https://docs.flutter.dev/get-started/codelab)
- [Flutter learning resources](https://docs.flutter.dev/reference/learning-resources)

For help getting started with Flutter development, view the
[online documentation](https://docs.flutter.dev/), which offers tutorials,
samples, guidance on mobile development, and a full API reference.

## Windows media_kit fix

If Windows builds start failing again after switching commits or refreshing the
pub cache, rerun:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\fix-media-kit-windows-cache.ps1 -SeedCache -Proxy http://127.0.0.1:7890
```

`-Proxy` is optional. The script reapplies the local `media_kit` Windows CMake
patch and preloads the stable native cache under `%LOCALAPPDATA%\media_kit_libs_windows_video`.
