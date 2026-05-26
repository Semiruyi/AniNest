import 'package:aninest_flutter/src/app/app_locale.dart';
import 'package:aninest_flutter/src/app/app_controller.dart';
import 'package:aninest_flutter/src/app/app_theme.dart';
import 'package:aninest_flutter/src/core/storage/app_preferences.dart';
import 'package:aninest_flutter/src/l10n/generated/app_localizations.dart';
import 'package:aninest_flutter/src/presentation/app_window.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart' as s;

class AniNestApp extends s.StatefulWidget {
  const AniNestApp({super.key, required this.appPreferences});

  final AppPreferences appPreferences;

  @override
  s.State<AniNestApp> createState() => _AniNestAppState();
}

class _AniNestAppState extends s.State<AniNestApp> {
  static const String _launchBaseUrl = String.fromEnvironment(
    'ANINEST_BASE_URL',
  );

  late final AppController _controller;

  @override
  void initState() {
    super.initState();
    _controller = AppController(
      launchBaseUrl: _launchBaseUrl.isEmpty ? null : _launchBaseUrl,
      appPreferences: widget.appPreferences,
    );
    _controller.bootstrap();
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  s.Widget build(s.BuildContext context) {
    return s.AnimatedBuilder(
      animation: _controller,
      builder: (context, _) => s.ShadcnApp(
        title: 'AniNest',
        locale: _controller.locale.locale,
        supportedLocales: AppLocaleOption.supportedLocales,
        localizationsDelegates: const [
          AppLocalizations.delegate,
          GlobalMaterialLocalizations.delegate,
          GlobalCupertinoLocalizations.delegate,
          GlobalWidgetsLocalizations.delegate,
        ],
        debugShowCheckedModeBanner: false,
        theme: AppTheme.shadcnDark,
        home: AppWindow(controller: _controller),
      ),
    );
  }
}
