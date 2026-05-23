import 'package:flutter/widgets.dart';

enum AppLocaleOption {
  english('en', Locale('en')),
  simplifiedChinese('zh', Locale('zh'));

  const AppLocaleOption(this.code, this.locale);

  final String code;
  final Locale locale;

  static const AppLocaleOption fallback = AppLocaleOption.english;

  static List<Locale> get supportedLocales => AppLocaleOption.values
      .map((option) => option.locale)
      .toList(growable: false);

  static AppLocaleOption fromCode(String? code) {
    for (final option in AppLocaleOption.values) {
      if (option.code == code) {
        return option;
      }
    }
    return fallback;
  }

  static AppLocaleOption fromLocale(Locale? locale) {
    if (locale == null) {
      return fallback;
    }

    for (final option in AppLocaleOption.values) {
      if (option.locale.languageCode == locale.languageCode) {
        return option;
      }
    }
    return fallback;
  }
}
