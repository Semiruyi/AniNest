import 'package:aninest_flutter/src/models/settings_models.dart';
import 'package:aninest_flutter/src/services/settings_api.dart';
import 'package:flutter/foundation.dart';

class SettingsController extends ChangeNotifier {
  SettingsController(this._settingsApi);

  SettingsApi _settingsApi;

  AppSettingsDto? _appSettings;

  AppSettingsDto? get appSettings => _appSettings;

  void rebind(SettingsApi settingsApi) {
    _settingsApi = settingsApi;
  }

  Future<void> load() async {
    _appSettings = await _settingsApi.getAll();
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

  void clear() {
    if (_appSettings == null) {
      return;
    }

    _appSettings = null;
    notifyListeners();
  }
}
