import 'dart:convert';

import 'package:aninest_flutter/src/core/window/window_state_snapshot.dart';
import 'package:shared_preferences/shared_preferences.dart';

class AppPreferences {
  static const String _localeCodeKey = 'app.localeCode';
  static const String _baseUrlKey = 'app.baseUrl';
  static const String _windowStateKey = 'window.state';

  SharedPreferences? _preferences;

  Future<String?> loadLocaleCode() async {
    final preferences = await _getPreferences();
    return preferences.getString(_localeCodeKey);
  }

  Future<void> saveLocaleCode(String code) async {
    final preferences = await _getPreferences();
    await preferences.setString(_localeCodeKey, code);
  }

  Future<String?> loadBaseUrl() async {
    final preferences = await _getPreferences();
    return preferences.getString(_baseUrlKey);
  }

  Future<void> saveBaseUrl(String baseUrl) async {
    final preferences = await _getPreferences();
    await preferences.setString(_baseUrlKey, baseUrl);
  }

  Future<WindowStateSnapshot?> loadWindowState() async {
    final preferences = await _getPreferences();
    final serialized = preferences.getString(_windowStateKey);
    if (serialized == null || serialized.isEmpty) {
      return null;
    }

    try {
      final decoded = jsonDecode(serialized);
      if (decoded is! Map<String, dynamic>) {
        return null;
      }

      return WindowStateSnapshot.fromJson(decoded);
    } catch (_) {
      return null;
    }
  }

  Future<void> saveWindowState(WindowStateSnapshot state) async {
    final preferences = await _getPreferences();
    await preferences.setString(_windowStateKey, jsonEncode(state.toJson()));
  }

  Future<SharedPreferences> _getPreferences() async {
    return _preferences ??= await SharedPreferences.getInstance();
  }
}
