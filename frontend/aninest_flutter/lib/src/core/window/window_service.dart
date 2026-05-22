import 'package:flutter/widgets.dart';
import 'package:window_manager/window_manager.dart';

class WindowService {
  const WindowService();

  Future<void> ensureInitialized() => windowManager.ensureInitialized();

  Future<void> waitUntilReadyToShow() async {
    const windowOptions = WindowOptions(
      size: Size(1280, 800),
      minimumSize: Size(960, 600),
      center: true,
      titleBarStyle: TitleBarStyle.hidden,
    );

    await windowManager.waitUntilReadyToShow(windowOptions, () async {
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

  Future<void> minimize() => windowManager.minimize();

  Future<void> maximize() => windowManager.maximize();

  Future<void> unmaximize() => windowManager.unmaximize();

  Future<void> close() => windowManager.close();
}
