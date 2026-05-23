import 'package:shared_preferences/shared_preferences.dart';

class LocalPreferences {
  static const String _localeCodeKey = 'app.localeCode';

  Future<String?> loadLocaleCode() async {
    final preferences = await SharedPreferences.getInstance();
    return preferences.getString(_localeCodeKey);
  }

  Future<void> saveLocaleCode(String code) async {
    final preferences = await SharedPreferences.getInstance();
    await preferences.setString(_localeCodeKey, code);
  }
}
