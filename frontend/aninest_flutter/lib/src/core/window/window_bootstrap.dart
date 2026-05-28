import 'package:aninest_flutter/src/core/logging/app_performance_logger.dart';
import 'package:aninest_flutter/src/core/storage/app_preferences.dart';
import 'package:aninest_flutter/src/core/platform/app_platform.dart';
import 'package:aninest_flutter/src/core/window/window_service.dart';

Future<void> initializeDesktopWindow({
  required AppPreferences appPreferences,
  WindowService windowService = const WindowService(),
}) async {
  if (!AppPlatform.isDesktop) {
    return;
  }

  await AppPerformanceLogger.measure(
    'Startup.Window',
    'windowService.ensureInitialized',
    windowService.ensureInitialized,
  );
  final initialWindowState = await AppPerformanceLogger.measure(
    'Startup.Window',
    'appPreferences.loadWindowState',
    appPreferences.loadWindowState,
  );
  await AppPerformanceLogger.measure(
    'Startup.Window',
    'windowService.waitUntilReadyToShow',
    () => windowService.waitUntilReadyToShow(initialState: initialWindowState),
  );
}
