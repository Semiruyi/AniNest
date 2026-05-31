import 'package:aninest_flutter/src/app/app_controller.dart';
import 'package:aninest_flutter/src/app/app_theme.dart';
import 'package:aninest_flutter/src/core/storage/app_preferences.dart';
import 'package:aninest_flutter/src/l10n/generated/app_localizations.dart';
import 'package:aninest_flutter/src/presentation/app_window.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter/widgets.dart' as f;
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
    const rootLocale = f.Locale('en');
    return s.AnimatedBuilder(
      animation: _controller,
      builder: (context, _) => s.ShadcnApp(
        title: 'AniNest',
        locale: rootLocale,
        supportedLocales: const [rootLocale],
        localizationsDelegates: const [
          GlobalMaterialLocalizations.delegate,
          GlobalCupertinoLocalizations.delegate,
          GlobalWidgetsLocalizations.delegate,
        ],
        debugShowCheckedModeBanner: false,
        theme: AppTheme.shadcnDark,
        builder: (context, child) => f.Localizations.override(
          context: context,
          locale: _controller.locale.locale,
          delegates: AppLocalizations.localizationsDelegates,
          child: child,
        ),
        home: AppWindow(controller: _controller),
      ),
    );
  }
}
