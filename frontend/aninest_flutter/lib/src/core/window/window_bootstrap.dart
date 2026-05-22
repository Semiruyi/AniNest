import 'package:aninest_flutter/src/core/platform/app_platform.dart';
import 'package:aninest_flutter/src/core/window/window_service.dart';

Future<void> initializeDesktopWindow({
  WindowService windowService = const WindowService(),
}) async {
  if (!AppPlatform.isDesktop) {
    return;
  }

  await windowService.ensureInitialized();
  await windowService.waitUntilReadyToShow();
}
