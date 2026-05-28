import 'package:aninest_flutter/src/api/aninest_http_client.dart';
import 'package:aninest_flutter/src/app/app_locale.dart';
import 'package:aninest_flutter/src/app/workflows/settings_workflow.dart';
import 'package:aninest_flutter/src/core/storage/app_preferences.dart';
import 'package:aninest_flutter/src/features/settings/application/settings_controller.dart';
import 'package:aninest_flutter/src/models/settings_models.dart';
import 'package:aninest_flutter/src/services/settings_api.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('savePlayerSettings delegates without spinner', () async {
    final settings = _FakeSettingsController();
    final runOperation = _RunOperationSpy();
    final workflow = SettingsWorkflow(
      settings: settings,
      runOperation: runOperation.call,
    );
    const playerSettings = PlayerSettingsDto(
      preferredRate: 1.25,
      preferredVolume: 65,
      resumePlayback: true,
    );

    await workflow.savePlayerSettings(playerSettings);

    expect(settings.savedPlayerSettings, same(playerSettings));
    expect(runOperation.showSpinners, <bool>[false]);
  });

  test('saveLocale delegates without spinner', () async {
    final settings = _FakeSettingsController();
    final runOperation = _RunOperationSpy();
    final workflow = SettingsWorkflow(
      settings: settings,
      runOperation: runOperation.call,
    );

    await workflow.saveLocale(AppLocaleOption.simplifiedChinese);

    expect(settings.savedLocale, AppLocaleOption.simplifiedChinese);
    expect(runOperation.showSpinners, <bool>[false]);
  });
}

class _RunOperationSpy {
  final showSpinners = <bool>[];

  Future<void> call(
    Future<void> Function() operation, {
    bool showSpinner = true,
  }) async {
    showSpinners.add(showSpinner);
    await operation();
  }
}

class _FakeSettingsController extends SettingsController {
  _FakeSettingsController() : super(_NoopSettingsApi(), _NoopAppPreferences());

  PlayerSettingsDto? savedPlayerSettings;
  AppLocaleOption? savedLocale;

  @override
  Future<void> savePlayerSettings(PlayerSettingsDto settings) async {
    savedPlayerSettings = settings;
  }

  @override
  Future<void> saveLocale(AppLocaleOption locale) async {
    savedLocale = locale;
  }
}

class _NoopSettingsApi extends SettingsApi {
  _NoopSettingsApi()
    : super(AniNestHttpClient(baseUrl: 'http://localhost:5275'));
}

class _NoopAppPreferences extends AppPreferences {
  @override
  Future<String?> loadLocaleCode() async {
    return null;
  }

  @override
  Future<void> saveLocaleCode(String code) async {}
}
