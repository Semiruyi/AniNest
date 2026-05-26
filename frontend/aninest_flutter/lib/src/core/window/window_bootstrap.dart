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

  await windowService.ensureInitialized();
  final initialWindowState = await appPreferences.loadWindowState();
  await windowService.waitUntilReadyToShow(initialState: initialWindowState);
}
