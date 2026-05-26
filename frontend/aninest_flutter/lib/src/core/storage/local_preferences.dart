import 'package:shared_preferences/shared_preferences.dart';

class LocalPreferences {
  static const String _localeCodeKey = 'app.localeCode';
  static const String _baseUrlKey = 'app.baseUrl';

  Future<String?> loadLocaleCode() async {
    final preferences = await SharedPreferences.getInstance();
    return preferences.getString(_localeCodeKey);
  }

  Future<void> saveLocaleCode(String code) async {
    final preferences = await SharedPreferences.getInstance();
    await preferences.setString(_localeCodeKey, code);
  }

  Future<String?> loadBaseUrl() async {
    final preferences = await SharedPreferences.getInstance();
    return preferences.getString(_baseUrlKey);
  }

  Future<void> saveBaseUrl(String baseUrl) async {
    final preferences = await SharedPreferences.getInstance();
    await preferences.setString(_baseUrlKey, baseUrl);
  }
}
