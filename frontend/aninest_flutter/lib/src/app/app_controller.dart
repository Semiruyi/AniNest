import 'package:aninest_flutter/src/app/coordination/backend_connection_coordinator.dart';
import 'package:aninest_flutter/src/app/composition/app_dependencies.dart';
import 'package:aninest_flutter/src/app/composition/app_runtime.dart';
import 'package:aninest_flutter/src/app/composition/app_runtime_assembler.dart';
import 'package:aninest_flutter/src/app/app_locale.dart';
import 'package:aninest_flutter/src/api/aninest_http_client.dart';
import 'package:aninest_flutter/src/core/logging/app_logger.dart';
import 'package:aninest_flutter/src/core/storage/app_preferences.dart';
import 'package:aninest_flutter/src/features/library/application/library_batch_add_result.dart';
import 'package:aninest_flutter/src/features/library/application/library_controller.dart';
import 'package:aninest_flutter/src/features/library/application/library_view.dart';
import 'package:aninest_flutter/src/features/metadata/application/metadata_controller.dart';
import 'package:aninest_flutter/src/features/player/application/player_controller.dart';
import 'package:aninest_flutter/src/features/player/application/player_anime4k_mode.dart';
import 'package:aninest_flutter/src/features/player/application/player_runtime_state.dart';
import 'package:aninest_flutter/src/features/settings/application/settings_controller.dart';
import 'package:aninest_flutter/src/models/enums.dart';
import 'package:aninest_flutter/src/models/library_models.dart';
import 'package:aninest_flutter/src/models/metadata_models.dart';
import 'package:aninest_flutter/src/models/playlist_models.dart';
import 'package:aninest_flutter/src/models/settings_models.dart';
import 'package:aninest_flutter/src/models/session_models.dart';
import 'package:flutter/foundation.dart';
import 'package:media_kit_video/media_kit_video.dart';

class AppController extends ChangeNotifier {
  factory AppController({
    String? launchBaseUrl,
    AppPreferences? appPreferences,
  }) {
    final resolvedAppPreferences = appPreferences ?? AppPreferences();
    final dependencies = AppDependencies.create(
      appPreferences: resolvedAppPreferences,
      initialBaseUrl: _resolveInitialBaseUrl(launchBaseUrl),
    );
    return AppController._(
      dependencies: dependencies,
      runtime: const AppRuntimeAssembler().assemble(
        launchBaseUrl: launchBaseUrl,
        appPreferences: resolvedAppPreferences,
        dependencies: dependencies,
      ),
    );
  }

  AppController._({
    required AppDependencies dependencies,
    required AppRuntime runtime,
  }) : _dependencies = dependencies,
       _runtime = runtime {
    _runtime.actionState.addListener(notifyListeners);
    library.addListener(notifyListeners);
    player.addListener(notifyListeners);
    settings.addListener(notifyListeners);
    metadata.addListener(notifyListeners);
  }

  static const String defaultBaseUrl =
      BackendConnectionCoordinator.defaultBaseUrl;

  final AppDependencies _dependencies;
  final AppRuntime _runtime;

  bool get isLoading => _runtime.actionState.isLoading;
  String? get lastError => _runtime.actionState.lastError;

  String get baseUrl => _runtime.backendConnectionCoordinator.baseUrl;
  AppPreferences get appPreferences => _dependencies.appPreferences;
  AppLocaleOption get locale => settings.locale;
  LibraryController get library => _dependencies.library;
  PlayerController get player => _dependencies.player;
  SettingsController get settings => _dependencies.settings;
  MetadataController get metadata => _dependencies.metadata;

  List<LibraryFolderDto> get folders => library.folders;
  List<LibraryFolderDto> get visibleFolders => library.visibleFolders;
  LibraryView get libraryView => library.selectedView;

  String? get selectedFolderId =>
      _runtime.selectionResolver.resolveSelectedFolderId();

  String? get selectedItemId =>
      _runtime.selectionResolver.resolveSelectedItemId();

  LibraryFolderDto? get selectedFolder => library.selectedFolder;
  MetadataDto? get selectedMetadata => metadata.metadata;
  AppSettingsDto? get appSettings => settings.appSettings;
  PlaylistDto? get playlist => player.playlist;
  PlaybackTargetDto? get playbackTarget => player.playbackTarget;
  PlayerRuntimeState get playerRuntime => player.runtime;
  VideoController get playerVideoController => player.videoController;
  bool get canMovePrevious => player.canMovePrevious;
  bool get canMoveNext => player.canMoveNext;
  bool get canTogglePlayback => player.canTogglePlayback;
  double get playbackRate => player.playbackRate;
  double get playbackVolume => player.playbackVolume;

  String? resolveMediaUrl(String? path) => library.resolveMediaUrl(path);

  Future<void> bootstrap() async {
    await _runtime.backendConnectionCoordinator.hydrateBaseUrl();
    await _runtime.appBootstrapWorkflow.reloadFromBackend();
  }

  Future<AddLibraryFolderResultDto?> addFolder(String path) async {
    final workflowResult = await _runtime.libraryWorkflow.addFolder(path);
    final result = workflowResult.result;
    if (workflowResult.usedFailureFallback && result != null) {
      AppLogger.warning(
        'AppController.AddFolder',
        'Converted operation failure into result status=${result.status}, reason=${result.reasonCode}',
      );
    }
    return result;
  }

  Future<LibraryBrowserResponse> browseLibraryDirectory(String? path) {
    return _runtime.libraryWorkflow.browseDirectory(path);
  }

  Future<LibraryBatchAddResult?> scanFolder(String rootPath) async {
    return _runtime.libraryWorkflow.scanFolder(rootPath);
  }

  Future<String?> testBaseUrl(String nextBaseUrl) async {
    return _runtime.backendConnectionCoordinator.testBaseUrl(nextBaseUrl);
  }

  Future<String?> updateBaseUrl(String nextBaseUrl) async {
    return _runtime.backendConnectionCoordinator.updateBaseUrl(nextBaseUrl);
  }

  Future<void> refreshLibrary() async {
    await _runtime.libraryWorkflow.refreshLibrary();
  }

  Future<void> openFolder(String folderId) async {
    await _runtime.playerWorkflow.openFolder(folderId);
  }

  Future<String?> openLibraryFolder(String folderId) async {
    return _runtime.playerWorkflow.openLibraryFolder(folderId);
  }

  Future<void> selectItem(String itemId) async {
    await _runtime.playerWorkflow.selectItem(itemId);
  }

  Future<void> moveNext() async {
    await _runtime.playerWorkflow.moveNext();
  }

  Future<void> movePrevious() async {
    await _runtime.playerWorkflow.movePrevious();
  }

  Future<void> closeSession() async {
    await _runtime.playerWorkflow.closeSession();
  }

  Future<String?> moveFolderToFront(String folderId) async {
    return _runtime.libraryWorkflow.moveFolderToFront(folderId);
  }

  Future<String?> deleteFolder(String folderId) async {
    return _runtime.libraryWorkflow.deleteFolder(folderId);
  }

  Future<String?> toggleFolderFavorite(String folderId, bool isFavorite) async {
    return _runtime.libraryWorkflow.toggleFolderFavorite(folderId, isFavorite);
  }

  Future<String?> setFolderWatchStatus(
    String folderId,
    WatchStatus status,
  ) async {
    return _runtime.libraryWorkflow.setFolderWatchStatus(folderId, status);
  }

  void selectFolder(String? folderId) {
    _runtime.libraryWorkflow.selectFolder(folderId);
  }

  void selectLibraryView(LibraryView view) {
    library.selectView(view);
  }

  Future<void> savePlayerSettings(PlayerSettingsDto settings) async {
    await _runtime.settingsWorkflow.savePlayerSettings(settings);
  }

  Future<void> saveLocale(AppLocaleOption locale) async {
    await _runtime.settingsWorkflow.saveLocale(locale);
  }

  Future<void> play() async {
    await player.play();
  }

  Future<void> pause() async {
    await player.pause();
  }

  Future<void> togglePlayPause() async {
    await player.togglePlayPause();
  }

  Future<void> seekToFraction(double fraction) async {
    await player.seekToFraction(fraction);
  }

  Future<void> moveNextAndPlay() async {
    await player.moveNextAndPlay();
  }

  Future<void> movePreviousAndPlay() async {
    await player.movePreviousAndPlay();
  }

  Future<void> cyclePlaybackRate() async {
    await player.cyclePlaybackRate();
  }

  Future<void> toggleMute() async {
    await player.toggleMute();
  }

  Future<void> selectSubtitleTrack(String trackId) async {
    await player.selectSubtitleTrack(trackId);
  }

  Future<void> setAnime4kMode(PlayerAnime4kMode mode) async {
    await player.setAnime4kMode(mode);
  }

  Future<void> selectItemAndPlay(String itemId) async {
    await player.selectItemAndPlay(itemId);
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

  @override
  void dispose() {
    _runtime.actionState.removeListener(notifyListeners);
    library.removeListener(notifyListeners);
    player.removeListener(notifyListeners);
    settings.removeListener(notifyListeners);
    metadata.removeListener(notifyListeners);
    _runtime.dispose();
    _dependencies.dispose();
    super.dispose();
  }
}
