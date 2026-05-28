import 'package:aninest_flutter/src/app/app_locale.dart';
import 'package:aninest_flutter/src/features/settings/application/settings_controller.dart';
import 'package:aninest_flutter/src/models/settings_models.dart';

typedef RunSettingsOperation = Future<void> Function(
  Future<void> Function() operation, {
  bool showSpinner,
});

class SettingsWorkflow {
  SettingsWorkflow({
    required SettingsController settings,
    required RunSettingsOperation runOperation,
  }) : _settings = settings,
       _runOperation = runOperation;

  final SettingsController _settings;
  final RunSettingsOperation _runOperation;

  Future<void> savePlayerSettings(PlayerSettingsDto settings) async {
    await _runOperation(() async {
      await _settings.savePlayerSettings(settings);
    }, showSpinner: false);
  }

  Future<void> saveLocale(AppLocaleOption locale) async {
    await _runOperation(() async {
      await _settings.saveLocale(locale);
    }, showSpinner: false);
  }
}
