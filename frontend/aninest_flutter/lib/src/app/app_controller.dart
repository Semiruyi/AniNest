import 'package:aninest_flutter/src/app/app_locale.dart';
import 'package:aninest_flutter/src/api/api_exception.dart';
import 'package:aninest_flutter/src/api/aninest_http_client.dart';
import 'package:aninest_flutter/src/features/library/application/library_controller.dart';
import 'package:aninest_flutter/src/features/metadata/application/metadata_controller.dart';
import 'package:aninest_flutter/src/features/player/application/player_controller.dart';
import 'package:aninest_flutter/src/features/settings/application/settings_controller.dart';
import 'package:aninest_flutter/src/models/library_models.dart';
import 'package:aninest_flutter/src/models/settings_models.dart';
import 'package:aninest_flutter/src/services/library_api.dart';
import 'package:aninest_flutter/src/core/storage/local_preferences.dart';
import 'package:aninest_flutter/src/services/metadata_api.dart';
import 'package:aninest_flutter/src/services/playlist_api.dart';
import 'package:aninest_flutter/src/services/session_api.dart';
import 'package:aninest_flutter/src/services/settings_api.dart';
import 'package:aninest_flutter/src/services/thumbnail_api.dart';
import 'package:flutter/foundation.dart';

class AppController extends ChangeNotifier {
  AppController({String baseUrl = 'http://localhost:5275'})
    : _client = AniNestHttpClient(baseUrl: baseUrl) {
    _wireServices();
    library = LibraryController(_libraryApi);
    player = PlayerController(_sessionApi, _playlistApi);
    settings = SettingsController(_settingsApi, LocalPreferences());
    metadata = MetadataController(_metadataApi, _thumbnailApi);
  }

  final AniNestHttpClient _client;

  late LibraryApi _libraryApi;
  late SessionApi _sessionApi;
  late PlaylistApi _playlistApi;
  late SettingsApi _settingsApi;
  late MetadataApi _metadataApi;
  late ThumbnailApi _thumbnailApi;

  late final LibraryController library;
  late final PlayerController player;
  late final SettingsController settings;
  late final MetadataController metadata;

  bool isLoading = false;
  String? lastError;

  String get baseUrl => _client.baseUrl;
  AppLocaleOption get locale => settings.locale;

  List<LibraryFolderDto> get folders => library.folders;

  String? get selectedFolderId =>
      player.selectedFolderId ??
      (library.folders.isNotEmpty ? library.folders.first.folderId : null);

  String? get selectedItemId => player.selectedItemId;

  AppSettingsDto? get appSettings => settings.appSettings;

  Future<void> bootstrap() async {
    await _run(() async {
      await settings.load();
      await library.refresh();
      await player.restore();
      await metadata.refresh(selectedFolderId);
    });
  }

  Future<AddLibraryFolderResultDto?> addFolder(String path) async {
    AddLibraryFolderResultDto? result;
    await _run(() async {
      result = await library.addFolder(path);
      if (result?.isAdded ?? false) {
        await metadata.refresh(selectedFolderId);
      }
    });
    return result;
  }

  Future<void> updateBaseUrl(String nextBaseUrl) async {
    _client.updateBaseUrl(nextBaseUrl);
    _wireServices();
    library.rebind(_libraryApi);
    player.rebind(_sessionApi, _playlistApi);
    settings.rebind(_settingsApi);
    metadata.rebind(_metadataApi, _thumbnailApi);
    await bootstrap();
  }

  Future<void> refreshLibrary() async {
    await _run(() async {
      await library.refresh();
      await metadata.refresh(selectedFolderId);
    }, showSpinner: false);
  }

  Future<void> openFolder(String folderId) async {
    await _run(() async {
      await player.openFolder(folderId);
      await metadata.refresh(selectedFolderId);
    });
  }

  Future<void> selectItem(String itemId) async {
    await _run(() async {
      await player.selectItem(itemId);
    }, showSpinner: false);
  }

  Future<void> moveNext() async {
    await _run(() async {
      await player.moveNext();
    }, showSpinner: false);
  }

  Future<void> movePrevious() async {
    await _run(() async {
      await player.movePrevious();
    }, showSpinner: false);
  }

  Future<void> closeSession() async {
    await _run(() async {
      await player.closeSession();
      await metadata.refresh(selectedFolderId);
    }, showSpinner: false);
  }

  Future<void> savePlayerSettings(PlayerSettingsDto settings) async {
    await _run(() async {
      await this.settings.savePlayerSettings(settings);
    }, showSpinner: false);
  }

  Future<void> saveLocale(AppLocaleOption locale) async {
    await _run(() async {
      await settings.saveLocale(locale);
    }, showSpinner: false);
  }

  Future<void> _run(
    Future<void> Function() operation, {
    bool showSpinner = true,
  }) async {
    if (showSpinner) {
      isLoading = true;
    }
    lastError = null;
    notifyListeners();

    try {
      await operation();
    } on ApiException catch (error) {
      lastError = '${error.code}: ${error.message}';
    } catch (error) {
      lastError = error.toString();
    } finally {
      isLoading = false;
      notifyListeners();
    }
  }

  void _wireServices() {
    _libraryApi = LibraryApi(_client);
    _sessionApi = SessionApi(_client);
    _playlistApi = PlaylistApi(_client);
    _settingsApi = SettingsApi(_client);
    _metadataApi = MetadataApi(_client);
    _thumbnailApi = ThumbnailApi(_client);
  }

  @override
  void dispose() {
    library.dispose();
    player.dispose();
    settings.dispose();
    metadata.dispose();
    super.dispose();
  }
}
