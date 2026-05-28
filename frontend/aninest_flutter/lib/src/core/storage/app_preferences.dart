import 'dart:convert';

import 'package:aninest_flutter/src/features/player/application/player_anime4k_mode.dart';
import 'package:aninest_flutter/src/core/storage/library_page_preferences.dart';
import 'package:aninest_flutter/src/core/window/window_state_snapshot.dart';
import 'package:shared_preferences/shared_preferences.dart';

class AppPreferences {
  static const String _localeCodeKey = 'app.localeCode';
  static const String _baseUrlKey = 'app.baseUrl';
  static const String _windowStateKey = 'window.state';
  static const String _libraryPagePreferencesKey = 'library.pagePreferences';
  static const String _playerAnime4kModeKey = 'player.anime4kMode';

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

  Future<LibraryPagePreferences?> loadLibraryPagePreferences() async {
    final preferences = await _getPreferences();
    final serialized = preferences.getString(_libraryPagePreferencesKey);
    if (serialized == null || serialized.isEmpty) {
      return null;
    }

    try {
      final decoded = jsonDecode(serialized);
      if (decoded is! Map<String, dynamic>) {
        return null;
      }

      return LibraryPagePreferences.fromJson(decoded);
    } catch (_) {
      return null;
    }
  }

  Future<void> saveLibraryPagePreferences(LibraryPagePreferences value) async {
    final preferences = await _getPreferences();
    await preferences.setString(
      _libraryPagePreferencesKey,
      jsonEncode(value.toJson()),
    );
  }

  Future<PlayerAnime4kMode> loadPlayerAnime4kMode() async {
    final preferences = await _getPreferences();
    return PlayerAnime4kMode.fromId(
      preferences.getString(_playerAnime4kModeKey),
    );
  }

  Future<void> savePlayerAnime4kMode(PlayerAnime4kMode mode) async {
    final preferences = await _getPreferences();
    await preferences.setString(_playerAnime4kModeKey, mode.id);
  }

  Future<SharedPreferences> _getPreferences() async {
    return _preferences ??= await SharedPreferences.getInstance();
  }
}
