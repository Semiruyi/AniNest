import 'package:aninest_flutter/src/app/aninest_app.dart';
import 'package:aninest_flutter/src/core/logging/app_logger.dart';
import 'package:aninest_flutter/src/core/window/window_bootstrap.dart';
import 'package:flutter/widgets.dart';
import 'package:media_kit/media_kit.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  await AppLogger.initialize();
  MediaKit.ensureInitialized();
  await initializeDesktopWindow();
  runApp(const AniNestApp());
}
