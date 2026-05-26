import 'dart:async';

import 'package:aninest_flutter/src/app/app_locale.dart';
import 'package:aninest_flutter/src/api/api_exception.dart';
import 'package:aninest_flutter/src/api/aninest_http_client.dart';
import 'package:aninest_flutter/src/core/logging/app_logger.dart';
import 'package:aninest_flutter/src/features/library/application/library_controller.dart';
import 'package:aninest_flutter/src/features/metadata/application/metadata_controller.dart';
import 'package:aninest_flutter/src/features/player/application/player_controller.dart';
import 'package:aninest_flutter/src/features/settings/application/settings_controller.dart';
import 'package:aninest_flutter/src/models/enums.dart';
import 'package:aninest_flutter/src/models/library_models.dart';
import 'package:aninest_flutter/src/models/settings_models.dart';
import 'package:aninest_flutter/src/services/library_api.dart';
import 'package:aninest_flutter/src/core/storage/app_preferences.dart';
import 'package:aninest_flutter/src/services/metadata_api.dart';
import 'package:aninest_flutter/src/services/playlist_api.dart';
import 'package:aninest_flutter/src/services/session_api.dart';
import 'package:aninest_flutter/src/services/settings_api.dart';
import 'package:aninest_flutter/src/services/host_event_service.dart';
import 'package:aninest_flutter/src/services/thumbnail_api.dart';
import 'package:aninest_flutter/src/models/host_event_models.dart';
import 'package:flutter/foundation.dart';

class AppController extends ChangeNotifier {
  AppController({String? launchBaseUrl, AppPreferences? appPreferences})
    : _launchBaseUrl = launchBaseUrl?.trim(),
      _appPreferences = appPreferences ?? AppPreferences(),
      _client = AniNestHttpClient(
        baseUrl: _resolveInitialBaseUrl(launchBaseUrl),
      ) {
    _wireServices();
    library = LibraryController(_libraryApi);
    player = PlayerController(_sessionApi, _playlistApi);
    settings = SettingsController(_settingsApi, _appPreferences);
    metadata = MetadataController(_metadataApi, _thumbnailApi);
    _hostEventService = HostEventService(_client);
    library.addListener(_handleLibrarySelectionChanged);
  }

  static const String defaultBaseUrl = 'http://localhost:5275';

  final String? _launchBaseUrl;
  final AppPreferences _appPreferences;
  final AniNestHttpClient _client;

  late LibraryApi _libraryApi;
  late SessionApi _sessionApi;
  late PlaylistApi _playlistApi;
  late SettingsApi _settingsApi;
  late MetadataApi _metadataApi;
  late ThumbnailApi _thumbnailApi;
  late HostEventService _hostEventService;

  late final LibraryController library;
  late final PlayerController player;
  late final SettingsController settings;
  late final MetadataController metadata;

  StreamSubscription<HostEventEnvelopeDto>? _hostEventSubscription;
  String? _lastMetadataFolderId;
  int _selectionRefreshSuspensionCount = 0;
  int? _lastProcessedHostEventSequence;
  bool _didHydrateBaseUrl = false;

  bool isLoading = false;
  String? lastError;

  String get baseUrl => _client.baseUrl;
  AppPreferences get appPreferences => _appPreferences;
  AppLocaleOption get locale => settings.locale;

  List<LibraryFolderDto> get folders => library.folders;

  String? get selectedFolderId =>
      library.selectedFolderId ??
      player.selectedFolderId ??
      (library.folders.isNotEmpty ? library.folders.first.folderId : null);

  String? get selectedItemId => player.selectedItemId;

  AppSettingsDto? get appSettings => settings.appSettings;

  Future<void> bootstrap() async {
    await _hydrateBaseUrl();
    await _reloadFromBackend();
  }

  Future<AddLibraryFolderResultDto?> addFolder(String path) async {
    AddLibraryFolderResultDto? result;
    await _run(() async {
      result = await _runWithSuspendedLibrarySelectionRefresh(
        () => library.addFolder(path),
      );
      if (result?.isAdded ?? false) {
        await _refreshMetadataForSelectionAsync(force: true);
      }
    });
    if (result == null && lastError != null) {
      result = _buildAddFolderFailureResult(path, lastError!);
      AppLogger.warning(
        'AppController.AddFolder',
        'Converted operation failure into result status=${result?.status}, reason=${result?.reasonCode}',
      );
    }
    return result;
  }

  Future<LibraryBrowserResponse> browseLibraryDirectory(String? path) {
    return _libraryApi.browse(path);
  }

  Future<String?> testBaseUrl(String nextBaseUrl) async {
    final validationError = _validateBaseUrl(nextBaseUrl);
    if (validationError != null) {
      return validationError;
    }

    final normalizedBaseUrl = AniNestHttpClient.normalizeBaseUrl(nextBaseUrl);
    final probeClient = AniNestHttpClient(baseUrl: normalizedBaseUrl);
    try {
      await probeClient.getObject('/api/settings');
      return null;
    } on ApiException catch (error) {
      return '${error.code}: ${error.message}';
    } catch (error) {
      return _buildConnectionFailureMessage(normalizedBaseUrl, error);
    } finally {
      probeClient.close();
    }
  }

  Future<String?> updateBaseUrl(String nextBaseUrl) async {
    final validationError = _validateBaseUrl(nextBaseUrl);
    if (validationError != null) {
      return validationError;
    }

    final normalizedBaseUrl = AniNestHttpClient.normalizeBaseUrl(nextBaseUrl);
    if (normalizedBaseUrl == baseUrl) {
      await _appPreferences.saveBaseUrl(normalizedBaseUrl);
      return null;
    }

    final previousBaseUrl = baseUrl;
    _client.updateBaseUrl(normalizedBaseUrl);
    await _reloadFromBackend(restartHostEvents: true);

    if (lastError == null) {
      await _appPreferences.saveBaseUrl(normalizedBaseUrl);
      return null;
    }

    final failureMessage = lastError!;
    AppLogger.warning(
      'AppController.UpdateBaseUrl',
      'Failed to switch backend to $normalizedBaseUrl. Restoring $previousBaseUrl.',
    );

    _client.updateBaseUrl(previousBaseUrl);
    await _reloadFromBackend(restartHostEvents: true);
    return failureMessage;
  }

  Future<void> refreshLibrary() async {
    await _run(() async {
      await _runWithSuspendedLibrarySelectionRefresh(library.refresh);
      await _refreshMetadataForSelectionAsync(force: true);
    }, showSpinner: false);
  }

  Future<void> openFolder(String folderId) async {
    await _run(() async {
      await player.openFolder(folderId);
      await _refreshMetadataForSelectionAsync(force: true);
    });
  }

  Future<String?> openLibraryFolder(String folderId) async {
    await _run(() async {
      await player.openFolder(folderId);
      await _refreshMetadataForSelectionAsync(force: true);
    }, showSpinner: false);
    return lastError;
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
      await _refreshMetadataForSelectionAsync(force: true);
    }, showSpinner: false);
  }

  Future<String?> moveFolderToFront(String folderId) async {
    await _run(() async {
      await library.moveToFront(folderId);
      await _refreshMetadataForSelectionAsync(force: true);
    }, showSpinner: false);
    return lastError;
  }

  Future<String?> deleteFolder(String folderId) async {
    await _run(() async {
      await library.deleteFolder(folderId);
      await _refreshMetadataForSelectionAsync(force: true);
    }, showSpinner: false);
    return lastError;
  }

  Future<String?> toggleFolderFavorite(String folderId, bool isFavorite) async {
    await _run(() async {
      await library.toggleFavorite(folderId, isFavorite);
      await _refreshMetadataForSelectionAsync(force: true);
    }, showSpinner: false);
    return lastError;
  }

  Future<String?> setFolderWatchStatus(
    String folderId,
    WatchStatus status,
  ) async {
    await _run(() async {
      await library.setWatchStatus(folderId, status);
      await _refreshMetadataForSelectionAsync(force: true);
    }, showSpinner: false);
    return lastError;
  }

  void selectFolder(String? folderId) {
    library.selectFolder(folderId);
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
      AppLogger.error(
        'AppController.Run',
        'ApiException during operation.',
        error: error,
        stackTrace: StackTrace.current,
      );
      lastError = '${error.code}: ${error.message}';
    } catch (error) {
      AppLogger.error(
        'AppController.Run',
        'Unhandled exception during operation.',
        error: error,
        stackTrace: StackTrace.current,
      );
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

  void _startHostEvents() {
    _hostEventSubscription ??= _hostEventService.events.listen(
      _handleHostEvent,
    );
    _hostEventService.start();
  }

  Future<void> _restartHostEvents() async {
    await _hostEventSubscription?.cancel();
    _hostEventSubscription = null;
    await _hostEventService.restart();
  }

  Future<void> _handleHostEvent(HostEventEnvelopeDto envelope) async {
    try {
      final sequence = envelope.sequence;
      if (sequence != null &&
          _lastProcessedHostEventSequence != null &&
          sequence <= _lastProcessedHostEventSequence!) {
        return;
      }
      if (sequence != null) {
        _lastProcessedHostEventSequence = sequence;
      }
      switch (envelope.type) {
        case 'library.folder_added':
          final payload = _coercePayloadMap(envelope.payload);
          if (payload == null) {
            return;
          }

          final added = LibraryFolderAddedEventDto.fromJson(payload);
          if (added.folderId.isEmpty) {
            return;
          }
          if (added.folder != null) {
            library.applyFolderAdded(added.folder!);
          } else {
            await refreshLibrary();
          }
          break;

        case 'library.folder_removed':
          final payload = _coercePayloadMap(envelope.payload);
          if (payload == null) {
            return;
          }

          final removed = LibraryFolderRemovedEventDto.fromJson(payload);
          if (removed.folderId.isEmpty) {
            return;
          }
          library.applyFolderRemoved(removed.folderId);
          if (selectedFolderId == null) {
            await _refreshMetadataForSelectionAsync(force: true);
          }
          break;

        case 'library.folder_updated':
          final payload = _coercePayloadMap(envelope.payload);
          if (payload == null) {
            return;
          }

          final updated = LibraryFolderUpdatedEventDto.fromJson(payload);
          if (updated.folderId.isEmpty) {
            return;
          }
          if (updated.folder != null) {
            library.applyFolderUpdated(updated.folder!);
          } else {
            await refreshLibrary();
          }
          break;

        case 'library.folder_reordered':
          final payload = _coercePayloadMap(envelope.payload);
          if (payload == null) {
            return;
          }

          final reordered = LibraryFolderReorderedEventDto.fromJson(payload);
          if (reordered.folderId.isEmpty) {
            return;
          }
          library.applyFolderReordered(
            reordered.folderId,
            reordered.position ?? 0,
            folder: reordered.folder,
          );
          break;

        case 'metadata.folder_updated':
          final payload = _coercePayloadMap(envelope.payload);
          if (payload == null) {
            AppLogger.warning(
              'AppController.HostEvents',
              'Skipping metadata.folder_updated due to unsupported payload type=${envelope.payload.runtimeType}',
            );
            return;
          }

          final update = MetadataFolderUpdatedEventDto.fromJson(payload);
          if (update.folderId.isEmpty) {
            return;
          }

          library.applyMetadataFolderUpdate(update);
          metadata.applyFolderUpdate(update, selectedFolderId);
          if (selectedFolderId == update.folderId) {
            await metadata.refreshSelectedFolder(update.folderId);
          }
          break;

        case 'metadata.summary_changed':
          final payload = _coercePayloadMap(envelope.payload);
          if (payload == null) {
            AppLogger.warning(
              'AppController.HostEvents',
              'Skipping metadata.summary_changed due to unsupported payload type=${envelope.payload.runtimeType}',
            );
            return;
          }

          final update = MetadataSummaryChangedEventDto.fromJson(payload);
          metadata.applySummary(update.summary);
          break;
      }
    } catch (error, stackTrace) {
      AppLogger.error(
        'AppController.HostEvents',
        'Failed to process host event ${envelope.type}.',
        error: error,
        stackTrace: stackTrace,
      );
    }
  }

  void _handleLibrarySelectionChanged() {
    if (_selectionRefreshSuspensionCount > 0) {
      return;
    }

    final currentFolderId = library.selectedFolderId;
    if (currentFolderId == _lastMetadataFolderId) {
      return;
    }

    unawaited(_refreshMetadataForSelectionAsync());
  }

  Future<void> _refreshMetadataForSelectionAsync({bool force = false}) async {
    final folderId = library.selectedFolderId;
    if (!force && folderId == _lastMetadataFolderId) {
      return;
    }

    _lastMetadataFolderId = folderId;
    await metadata.refresh(folderId);
  }

  Future<T> _runWithSuspendedLibrarySelectionRefresh<T>(
    Future<T> Function() action,
  ) async {
    _selectionRefreshSuspensionCount++;
    try {
      return await action();
    } finally {
      _selectionRefreshSuspensionCount--;
    }
  }

  AddLibraryFolderResultDto _buildAddFolderFailureResult(
    String path,
    String errorMessage,
  ) {
    final normalized = errorMessage.toLowerCase();
    if (normalized.contains('socketexception') ||
        normalized.contains('clientexception')) {
      return AddLibraryFolderResultDto(
        status: 'failed',
        message:
            'Unable to connect to the backend at $baseUrl. Please make sure AniNest.Host is running, then try again.',
        reasonCode: 'network_error',
        folder: null,
      );
    }

    return AddLibraryFolderResultDto(
      status: 'failed',
      message: 'Unable to add folder "$path". $errorMessage',
      reasonCode: 'unexpected_error',
      folder: null,
    );
  }

  Future<void> _hydrateBaseUrl() async {
    if (_didHydrateBaseUrl) {
      return;
    }

    _didHydrateBaseUrl = true;
    final resolvedBaseUrl = await _resolveStartupBaseUrl();
    if (resolvedBaseUrl == null || resolvedBaseUrl == baseUrl) {
      return;
    }

    _client.updateBaseUrl(resolvedBaseUrl);
  }

  Future<String?> _resolveStartupBaseUrl() async {
    final launchBaseUrl = _launchBaseUrl;
    if (launchBaseUrl != null && launchBaseUrl.isNotEmpty) {
      return _normalizeStartupBaseUrl(launchBaseUrl, source: 'launch override');
    }

    final storedBaseUrl = await _appPreferences.loadBaseUrl();
    if (storedBaseUrl == null || storedBaseUrl.trim().isEmpty) {
      return null;
    }

    return _normalizeStartupBaseUrl(storedBaseUrl, source: 'saved preference');
  }

  String? _normalizeStartupBaseUrl(String candidate, {required String source}) {
    if (!AniNestHttpClient.isValidBaseUrl(candidate)) {
      AppLogger.warning(
        'AppController.StartupBaseUrl',
        'Ignoring invalid $source: $candidate',
      );
      return null;
    }

    return AniNestHttpClient.normalizeBaseUrl(candidate);
  }

  Future<void> _reloadFromBackend({bool restartHostEvents = false}) async {
    if (restartHostEvents) {
      await _restartHostEvents();
    }
    _startHostEvents();

    await _run(() async {
      await settings.load();
      await _runWithSuspendedLibrarySelectionRefresh(library.refresh);
      await player.restore();
      await _refreshMetadataForSelectionAsync(force: true);
    });
  }

  String? _validateBaseUrl(String candidate) {
    if (!AniNestHttpClient.isValidBaseUrl(candidate)) {
      return 'Please enter a full http:// or https:// backend address.';
    }

    return null;
  }

  String _buildConnectionFailureMessage(String targetBaseUrl, Object error) {
    final normalized = error.toString().toLowerCase();
    if (normalized.contains('socketexception') ||
        normalized.contains('clientexception')) {
      return 'Unable to connect to the backend at $targetBaseUrl.';
    }

    return 'Unable to connect to the backend at $targetBaseUrl. $error';
  }

  static String _resolveInitialBaseUrl(String? launchBaseUrl) {
    final trimmed = launchBaseUrl?.trim();
    if (trimmed != null &&
        trimmed.isNotEmpty &&
        AniNestHttpClient.isValidBaseUrl(trimmed)) {
      return trimmed;
    }

    return defaultBaseUrl;
  }

  Map<String, dynamic>? _coercePayloadMap(Object? payload) {
    if (payload is Map<String, dynamic>) {
      return payload;
    }
    if (payload is Map) {
      return payload.map((key, value) => MapEntry(key.toString(), value));
    }
    return null;
  }

  @override
  void dispose() {
    library.removeListener(_handleLibrarySelectionChanged);
    _hostEventSubscription?.cancel();
    _hostEventService.dispose();
    library.dispose();
    player.dispose();
    settings.dispose();
    metadata.dispose();
    _client.close();
    super.dispose();
  }
}
