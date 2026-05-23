import 'package:aninest_flutter/src/app/app_controller.dart';
import 'package:aninest_flutter/src/app/app_theme.dart';
import 'package:aninest_flutter/src/presentation/app_window.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart' as s;

class AniNestApp extends s.StatefulWidget {
  const AniNestApp({super.key});

  @override
  s.State<AniNestApp> createState() => _AniNestAppState();
}

class _AniNestAppState extends s.State<AniNestApp> {
  late final AppController _controller;

  @override
  void initState() {
    super.initState();
    _controller = AppController();
    _controller.bootstrap();
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  s.Widget build(s.BuildContext context) {
    return s.ShadcnApp(
      title: 'AniNest',
      debugShowCheckedModeBanner: false,
      theme: AppTheme.shadcnDark,
      home: AppWindow(controller: _controller),
    );
  }
}
