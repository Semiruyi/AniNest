import 'package:aninest_flutter/src/app/app_locale.dart';
import 'package:aninest_flutter/src/core/logging/app_performance_logger.dart';
import 'package:aninest_flutter/src/core/storage/app_preferences.dart';
import 'package:aninest_flutter/src/models/settings_models.dart';
import 'package:aninest_flutter/src/services/settings_api.dart';
import 'package:flutter/foundation.dart';

class SettingsController extends ChangeNotifier {
  SettingsController(this._settingsApi, this._appPreferences);

  SettingsApi _settingsApi;
  final AppPreferences _appPreferences;

  AppSettingsDto? _appSettings;
  AppLocaleOption _locale = AppLocaleOption.fallback;

  AppSettingsDto? get appSettings => _appSettings;
  AppLocaleOption get locale => _locale;

  void rebind(SettingsApi settingsApi) {
    _settingsApi = settingsApi;
  }

  Future<void> load() async {
    final localeCode = await AppPerformanceLogger.measure(
      'Startup.Settings',
      'appPreferences.loadLocaleCode',
      _appPreferences.loadLocaleCode,
    );
    _locale = AppLocaleOption.fromCode(localeCode);
    _appSettings = await AppPerformanceLogger.measure(
      'Startup.Settings',
      'settingsApi.getAll',
      _settingsApi.getAll,
    );
    notifyListeners();
  }

  Future<void> saveLocale(AppLocaleOption locale) async {
    if (_locale == locale) {
      return;
    }

    _locale = locale;
    await _appPreferences.saveLocaleCode(locale.code);
    notifyListeners();
  }

  Future<void> savePlayerSettings(PlayerSettingsDto settings) async {
    await _settingsApi.savePlayer(settings);
    final current = _appSettings;
    if (current == null) {
      return;
    }

    _appSettings = AppSettingsDto(
      player: settings,
      metadata: current.metadata,
      thumbnails: current.thumbnails,
    );
    notifyListeners();
  }

  Future<void> saveMetadataSettings(MetadataSettingsDto settings) async {
    await _settingsApi.saveMetadata(settings);
    final current = _appSettings;
    if (current == null) {
      return;
    }

    _appSettings = AppSettingsDto(
      player: current.player,
      metadata: settings,
      thumbnails: current.thumbnails,
    );
    notifyListeners();
  }

  void clear() {
    if (_appSettings == null) {
      return;
    }

    _appSettings = null;
    notifyListeners();
  }
}
