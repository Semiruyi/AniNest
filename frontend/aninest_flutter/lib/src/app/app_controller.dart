import 'dart:async';

import 'package:aninest_flutter/src/app/app_locale.dart';
import 'package:aninest_flutter/src/api/api_exception.dart';
import 'package:aninest_flutter/src/api/aninest_http_client.dart';
import 'package:aninest_flutter/src/core/logging/app_logger.dart';
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
import 'package:aninest_flutter/src/services/host_event_service.dart';
import 'package:aninest_flutter/src/services/thumbnail_api.dart';
import 'package:aninest_flutter/src/models/host_event_models.dart';
import 'package:flutter/foundation.dart';

class AppController extends ChangeNotifier {
  AppController({String baseUrl = 'http://localhost:5275'})
    : _client = AniNestHttpClient(baseUrl: baseUrl) {
    _wireServices();
    library = LibraryController(_libraryApi);
    player = PlayerController(_sessionApi, _playlistApi);
    settings = SettingsController(_settingsApi, LocalPreferences());
    metadata = MetadataController(_metadataApi, _thumbnailApi);
    _hostEventService = HostEventService(_client);
    library.addListener(_handleLibrarySelectionChanged);
  }

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

  bool isLoading = false;
  String? lastError;

  String get baseUrl => _client.baseUrl;
  AppLocaleOption get locale => settings.locale;

  List<LibraryFolderDto> get folders => library.folders;

  String? get selectedFolderId =>
      library.selectedFolderId ??
      player.selectedFolderId ??
      (library.folders.isNotEmpty ? library.folders.first.folderId : null);

  String? get selectedItemId => player.selectedItemId;

  AppSettingsDto? get appSettings => settings.appSettings;

  Future<void> bootstrap() async {
    _startHostEvents();
    await _run(() async {
      await settings.load();
      await _runWithSuspendedLibrarySelectionRefresh(library.refresh);
      await player.restore();
      await _refreshMetadataForSelectionAsync(force: true);
    });
  }

  Future<AddLibraryFolderResultDto?> addFolder(String path) async {
    AddLibraryFolderResultDto? result;
    AppLogger.info(
      'AppController.AddFolder',
      'Starting addFolder for path=$path',
    );
    await _run(() async {
      result = await _runWithSuspendedLibrarySelectionRefresh(
        () => library.addFolder(path),
      );
      AppLogger.info(
        'AppController.AddFolder',
        'library.addFolder completed. status=${result?.status}, selectedFolderId=${library.selectedFolderId}, currentFolderCount=${library.folders.length}',
      );
      if (result?.isAdded ?? false) {
        await _refreshMetadataForSelectionAsync(force: true);
        AppLogger.info(
          'AppController.AddFolder',
          'selection metadata refresh completed after addFolder. selectedFolderId=$selectedFolderId',
        );
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

  Future<void> updateBaseUrl(String nextBaseUrl) async {
    _client.updateBaseUrl(nextBaseUrl);
    _wireServices();
    library.rebind(_libraryApi);
    player.rebind(_sessionApi, _playlistApi);
    settings.rebind(_settingsApi);
    metadata.rebind(_metadataApi, _thumbnailApi);
    await _restartHostEvents();
    await bootstrap();
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
        AppLogger.info(
          'AppController.HostEvents',
          'Skipping stale host event type=${envelope.type}, sequence=$sequence, lastProcessed=$_lastProcessedHostEventSequence',
        );
        return;
      }
      if (sequence != null) {
        _lastProcessedHostEventSequence = sequence;
      }

      AppLogger.info(
        'AppController.HostEvents',
        'Received host event type=${envelope.type}, sequence=${envelope.sequence}, timestampUtc=${envelope.timestampUtc?.toIso8601String()}',
      );
      switch (envelope.type) {
        case 'library.folder_added':
          final payload = _coercePayloadMap(envelope.payload);
          if (payload == null) {
            return;
          }

          final added = LibraryFolderAddedEventDto.fromJson(payload);
          AppLogger.info(
            'AppController.HostEvents',
            'Processing library.folder_added. folderId=${added.folderId}, hasFolderSnapshot=${added.folder != null}',
          );
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
          AppLogger.info(
            'AppController.HostEvents',
            'Processing library.folder_removed. folderId=${removed.folderId}',
          );
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
          AppLogger.info(
            'AppController.HostEvents',
            'Processing library.folder_updated. folderId=${updated.folderId}, hasFolderSnapshot=${updated.folder != null}, isFavorite=${updated.isFavorite}, watchStatus=${updated.watchStatus?.name}',
          );
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
          AppLogger.info(
            'AppController.HostEvents',
            'Processing library.folder_reordered. folderId=${reordered.folderId}, position=${reordered.position}, hasFolderSnapshot=${reordered.folder != null}',
          );
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

          AppLogger.info(
            'AppController.HostEvents',
            'Processing metadata.folder_updated for folderId=${update.folderId}, selectedFolderId=$selectedFolderId, coverUrl=${update.coverUrl}, posterUrl=${update.posterUrl}, hasMetadata=${update.hasMetadata}',
          );
          library.applyMetadataFolderUpdate(update);
          metadata.applyFolderUpdate(update, selectedFolderId);
          if (selectedFolderId == update.folderId) {
            await metadata.refreshSelectedFolder(update.folderId);
            AppLogger.info(
              'AppController.HostEvents',
              'Refreshed selected metadata for folderId=${update.folderId}',
            );
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
