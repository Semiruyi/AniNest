import 'package:aninest_flutter/src/core/window/window_state_snapshot.dart';
import 'package:flutter/widgets.dart';
import 'package:window_manager/window_manager.dart';

class WindowService {
  const WindowService();

  static const Size defaultSize = Size(1280, 800);
  static const Size minimumSize = Size(960, 600);

  Future<void> ensureInitialized() => windowManager.ensureInitialized();

  Future<void> waitUntilReadyToShow({WindowStateSnapshot? initialState}) async {
    final windowOptions = WindowOptions(
      size: _resolveInitialSize(initialState),
      minimumSize: minimumSize,
      center: _resolveInitialBounds(initialState) == null,
      titleBarStyle: TitleBarStyle.hidden,
    );

    await windowManager.waitUntilReadyToShow(windowOptions, () async {
      final bounds = _resolveInitialBounds(initialState);
      if (bounds != null) {
        await windowManager.setBounds(bounds);
      }
      if (initialState?.isMaximized ?? false) {
        await windowManager.maximize();
      }
      await windowManager.show();
      await windowManager.focus();
    });
  }

  void addListener(WindowListener listener) {
    windowManager.addListener(listener);
  }

  void removeListener(WindowListener listener) {
    windowManager.removeListener(listener);
  }

  Future<bool> isMaximized() => windowManager.isMaximized();

  Future<Rect> getBounds() => windowManager.getBounds();

  Future<void> minimize() => windowManager.minimize();

  Future<void> maximize() => windowManager.maximize();

  Future<void> unmaximize() => windowManager.unmaximize();

  Future<void> close() => windowManager.close();

  Size _resolveInitialSize(WindowStateSnapshot? initialState) {
    final size = initialState?.size;
    if (size == null) {
      return defaultSize;
    }

    if (size.width < minimumSize.width || size.height < minimumSize.height) {
      return defaultSize;
    }

    return size;
  }

  Rect? _resolveInitialBounds(WindowStateSnapshot? initialState) {
    final bounds = initialState?.bounds;
    if (bounds == null) {
      return null;
    }

    if (bounds.width < minimumSize.width ||
        bounds.height < minimumSize.height) {
      return null;
    }

    return bounds;
  }
}
