import 'package:aninest_flutter/src/api/api_exception.dart';
import 'package:aninest_flutter/src/api/aninest_http_client.dart';
import 'package:aninest_flutter/src/models/library_models.dart';
import 'package:aninest_flutter/src/models/metadata_models.dart';
import 'package:aninest_flutter/src/models/playlist_models.dart';
import 'package:aninest_flutter/src/models/session_models.dart';
import 'package:aninest_flutter/src/models/settings_models.dart';
import 'package:aninest_flutter/src/models/thumbnail_models.dart';
import 'package:aninest_flutter/src/services/library_api.dart';
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
  }

  final AniNestHttpClient _client;

  late LibraryApi _libraryApi;
  late SessionApi _sessionApi;
  late PlaylistApi _playlistApi;
  late SettingsApi _settingsApi;
  late MetadataApi _metadataApi;
  late ThumbnailApi _thumbnailApi;

  bool isLoading = false;
  String? lastError;

  List<LibraryFolderDto> folders = const [];
  SessionStateDto? session;
  PlaylistDto? playlist;
  PlaybackTargetDto? playbackTarget;
  AppSettingsDto? appSettings;
  MetadataStatusSummaryDto? metadataSummary;
  MetadataDto? metadata;
  ThumbnailFolderSummaryDto? thumbnailSummary;
  List<ThumbnailStatusDto> thumbnails = const [];

  String get baseUrl => _client.baseUrl;

  String? get selectedFolderId =>
      session?.folderId ?? (folders.isNotEmpty ? folders.first.folderId : null);

  String? get selectedItemId =>
      session?.currentItemId ?? playlist?.currentItemId;

  Future<void> bootstrap() async {
    await _run(() async {
      appSettings = await _settingsApi.getAll();
      folders = await _libraryApi.getFolders();
      try {
        session = await _sessionApi.getCurrent();
      } on ApiException {
        session = null;
      }

      if (session != null) {
        try {
          playlist = await _playlistApi.getCurrent();
        } on ApiException {
          playlist = null;
        }
      }

      await _refreshSelectedFolderDetails();
    });
  }

  Future<void> addFolder(String path) async {
    await _run(() async {
      await _libraryApi.addFolder(path);
      folders = await _libraryApi.getFolders();
      await _refreshSelectedFolderDetails();
    });
  }

  Future<void> updateBaseUrl(String nextBaseUrl) async {
    _client.updateBaseUrl(nextBaseUrl);
    _wireServices();
    await bootstrap();
  }

  Future<void> refreshLibrary() async {
    await _run(() async {
      folders = await _libraryApi.getFolders();
      await _refreshSelectedFolderDetails();
    }, showSpinner: false);
  }

  Future<void> openFolder(String folderId) async {
    await _run(() async {
      final result = await _sessionApi.openFolder(folderId);
      session = result.session;
      playbackTarget = result.playbackTarget;
      playlist = await _playlistApi.getCurrent();
      await _refreshSelectedFolderDetails();
    });
  }

  Future<void> selectItem(String itemId) async {
    await _run(() async {
      final result = await _sessionApi.selectItem(itemId);
      session = result.session;
      playbackTarget = result.playbackTarget;
      playlist = await _playlistApi.getCurrent();
    }, showSpinner: false);
  }

  Future<void> moveNext() async {
    await _run(() async {
      final result = await _sessionApi.moveNext();
      session = result.session;
      playbackTarget = result.playbackTarget;
      playlist = await _playlistApi.getCurrent();
    }, showSpinner: false);
  }

  Future<void> movePrevious() async {
    await _run(() async {
      final result = await _sessionApi.movePrevious();
      session = result.session;
      playbackTarget = result.playbackTarget;
      playlist = await _playlistApi.getCurrent();
    }, showSpinner: false);
  }

  Future<void> closeSession() async {
    await _run(() async {
      await _sessionApi.close();
      session = null;
      playlist = null;
      playbackTarget = null;
      await _refreshSelectedFolderDetails();
    }, showSpinner: false);
  }

  Future<void> savePlayerSettings(PlayerSettingsDto settings) async {
    await _run(() async {
      await _settingsApi.savePlayer(settings);
      final current = appSettings;
      if (current != null) {
        appSettings = AppSettingsDto(
          player: settings,
          metadata: current.metadata,
          thumbnails: current.thumbnails,
        );
      }
    }, showSpinner: false);
  }

  Future<void> _refreshSelectedFolderDetails() async {
    metadataSummary = await _metadataApi.getSummary();
    final folderId = selectedFolderId;
    if (folderId == null) {
      metadata = null;
      thumbnailSummary = null;
      thumbnails = const [];
      return;
    }

    try {
      metadata = await _metadataApi.getFolder(folderId);
    } on ApiException {
      metadata = null;
    }

    try {
      thumbnailSummary = await _thumbnailApi.getFolderSummary(folderId);
      thumbnails = await _thumbnailApi.getFolder(folderId);
    } on ApiException {
      thumbnailSummary = null;
      thumbnails = const [];
    }
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
}
