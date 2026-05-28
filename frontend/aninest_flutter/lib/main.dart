import 'package:aninest_flutter/src/app/aninest_app.dart';
import 'package:aninest_flutter/src/core/logging/app_logger.dart';
import 'package:aninest_flutter/src/core/logging/app_performance_logger.dart';
import 'package:aninest_flutter/src/core/storage/app_preferences.dart';
import 'package:aninest_flutter/src/core/window/window_bootstrap.dart';
import 'package:flutter/widgets.dart';
import 'package:media_kit/media_kit.dart';

Future<void> main() async {
  final startupStopwatch = Stopwatch()..start();
  WidgetsFlutterBinding.ensureInitialized();
  await AppPerformanceLogger.measure(
    'Startup',
    'AppLogger.initialize',
    AppLogger.initialize,
  );
  AppPerformanceLogger.instant('Startup', 'MediaKit.ensureInitialized', () {
    MediaKit.ensureInitialized();
  });
  final appPreferences = AppPreferences();
  await AppPerformanceLogger.measure(
    'Startup',
    'initializeDesktopWindow',
    () => initializeDesktopWindow(appPreferences: appPreferences),
  );
  AppLogger.info(
    'Startup',
    'Pre-runApp startup completed in ${startupStopwatch.elapsedMilliseconds}ms',
  );
  runApp(AniNestApp(appPreferences: appPreferences));
}
