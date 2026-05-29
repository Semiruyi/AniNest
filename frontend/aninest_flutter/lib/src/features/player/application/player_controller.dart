import 'dart:async';

import 'package:aninest_flutter/src/api/api_exception.dart';
import 'package:aninest_flutter/src/core/logging/app_logger.dart';
import 'package:aninest_flutter/src/core/logging/app_performance_logger.dart';
import 'package:aninest_flutter/src/core/storage/app_preferences.dart';
import 'package:aninest_flutter/src/features/player/application/player_anime4k_mode.dart';
import 'package:aninest_flutter/src/features/player/application/player_playback_engine.dart';
import 'package:aninest_flutter/src/features/player/application/player_progress_synchronizer.dart';
import 'package:aninest_flutter/src/features/player/application/player_runtime_state.dart';
import 'package:aninest_flutter/src/features/player/application/player_state.dart';
import 'package:aninest_flutter/src/models/playlist_models.dart';
import 'package:aninest_flutter/src/models/session_models.dart';
import 'package:aninest_flutter/src/services/playlist_api.dart';
import 'package:aninest_flutter/src/services/session_api.dart';
import 'package:flutter/foundation.dart';
import 'package:media_kit_video/media_kit_video.dart';

class PlayerController extends ChangeNotifier {
  PlayerController(this._sessionApi, this._playlistApi, this._appPreferences) {
    _progressSynchronizer = PlayerProgressSynchronizer(_sessionApi);
    _playbackEngine.addListener(_handlePlaybackChanged);
    _syncRuntimeState();
  }

  static const List<double> _supportedRates = <double>[
    0.5,
    0.75,
    1.0,
    1.25,
    1.5,
    2.0,
  ];

  SessionApi _sessionApi;
  PlaylistApi _playlistApi;
  final AppPreferences _appPreferences;
  late final PlayerProgressSynchronizer _progressSynchronizer;
  final PlayerPlaybackEngine _playbackEngine = PlayerPlaybackEngine();

  PlayerState _state = const PlayerState.initial();
  String? _completingItemId;
  bool _isDisposed = false;
  bool _anime4kModeHydrated = false;

  PlayerState get state => _state;
  SessionStateDto? get session => _state.session;
  PlaylistDto? get playlist => _state.playlist;
  PlaybackTargetDto? get playbackTarget => _state.playbackTarget;
  PlayerRuntimeState get runtime => _state.runtime;
  VideoController get videoController => _playbackEngine.videoController;

  String? get selectedFolderId => _state.selectedFolderId;
  String? get selectedItemId => _state.selectedItemId;

  bool get canMovePrevious => _state.canMovePrevious;
  bool get canMoveNext => _state.canMoveNext;
  bool get canTogglePlayback => _state.canTogglePlayback;
  double get playbackRate => _state.playbackRate;
  double get playbackVolume => _state.playbackVolume;

  void rebind(SessionApi sessionApi, PlaylistApi playlistApi) {
    _sessionApi = sessionApi;
    _playlistApi = playlistApi;
    _progressSynchronizer.rebind(sessionApi);
  }

  Future<void> restore() async {
    await AppPerformanceLogger.measure(
      'Startup.Player',
      'hydrateAnime4kMode',
      _hydrateAnime4kMode,
    );
    SessionStateDto? restoredSession;
    try {
      restoredSession = await AppPerformanceLogger.measure(
        'Startup.Player',
        'sessionApi.getCurrent',
        _sessionApi.getCurrent,
      );
    } on ApiException {
      restoredSession = null;
    }

    if (restoredSession == null) {
      _updateState(session: null, playlist: null, playbackTarget: null);
      await _syncPlayback();
      notifyListeners();
      return;
    }

    try {
      final result = await AppPerformanceLogger.measure(
        'Startup.Player',
        'sessionApi.openFolder',
        () => _sessionApi.openFolder(restoredSession!.folderId),
      );
      _updateState(
        session: result.session,
        playbackTarget: result.playbackTarget,
      );
    } on ApiException {
      _updateState(session: restoredSession, playbackTarget: null);
    }

    await AppPerformanceLogger.measure(
      'Startup.Player',
      'refreshPlaylist',
      _refreshPlaylist,
    );
    await AppPerformanceLogger.measure(
      'Startup.Player',
      'syncPlayback',
      _syncPlayback,
    );
    notifyListeners();
  }

  Future<void> openFolder(String folderId) async {
    await _flushProgress();
    final result = await _sessionApi.openFolder(folderId);
    _updateState(
      session: result.session,
      playbackTarget: result.playbackTarget,
    );
    await _refreshPlaylist();
    await _syncPlayback();
    notifyListeners();
  }

  Future<void> selectItem(String itemId) async {
    await _flushProgress();
    final result = await _sessionApi.selectItem(itemId);
    _updateState(
      session: result.session,
      playbackTarget: result.playbackTarget,
    );
    await _refreshPlaylist();
    await _syncPlayback();
    notifyListeners();
  }

  Future<void> selectItemAndPlay(String itemId) async {
    await selectItem(itemId);
    await play();
  }

  Future<void> moveNext() async {
    await _flushProgress();
    final result = await _sessionApi.moveNext();
    _updateState(
      session: result.session,
      playbackTarget: result.playbackTarget,
    );
    await _refreshPlaylist();
    await _syncPlayback();
    notifyListeners();
  }

  Future<void> moveNextAndPlay() async {
    await moveNext();
    await play();
  }

  Future<void> movePrevious() async {
    await _flushProgress();
    final result = await _sessionApi.movePrevious();
    _updateState(
      session: result.session,
      playbackTarget: result.playbackTarget,
    );
    await _refreshPlaylist();
    await _syncPlayback();
    notifyListeners();
  }

  Future<void> movePreviousAndPlay() async {
    await movePrevious();
    await play();
  }

  Future<void> togglePlayPause() async {
    if (!canTogglePlayback) {
      return;
    }

    await _playbackEngine.togglePlayPause();
    await _flushProgress();
  }

  Future<void> play() async {
    if (!canTogglePlayback) {
      return;
    }

    await _playbackEngine.play();
  }

  Future<void> pause() async {
    await _playbackEngine.pause();
    await _flushProgress();
  }

  Future<void> seekTo(Duration position) async {
    final maxPosition = runtime.duration;
    if (maxPosition <= Duration.zero) {
      return;
    }

    final clamped = position > maxPosition ? maxPosition : position;
    await _playbackEngine.seek(clamped);
    await _flushProgress();
  }

  Future<void> seekToFraction(double fraction) async {
    final duration = runtime.duration;
    if (duration <= Duration.zero) {
      return;
    }

    final target = Duration(
      microseconds: (duration.inMicroseconds * fraction.clamp(0.0, 1.0))
          .round(),
    );
    await seekTo(target);
  }

  Future<void> seekBy(Duration delta) async {
    final target = runtime.position + delta;
    final clamped = target < Duration.zero ? Duration.zero : target;
    await seekTo(clamped);
  }

  Future<void> cyclePlaybackRate() async {
    final currentRate = playbackRate;
    final currentIndex = _supportedRates.indexWhere(
      (double value) => (value - currentRate).abs() < 0.001,
    );
    final nextIndex =
        currentIndex < 0 || currentIndex == _supportedRates.length - 1
        ? 0
        : currentIndex + 1;
    await setPlaybackRate(_supportedRates[nextIndex]);
  }

  Future<void> setPlaybackRate(double rate) async {
    await _playbackEngine.setRate(rate);
    if (session != null) {
      _updateState(session: session!.copyWith(preferredRate: rate));
      notifyListeners();
    }
    await _flushProgress();
  }

  Future<void> toggleMute() async {
    await _playbackEngine.toggleMute();
    if (session != null) {
      _updateState(
        session: session!.copyWith(preferredVolume: runtime.volume.round()),
      );
      notifyListeners();
    }
    await _flushProgress();
  }

  Future<void> setPlaybackVolume(double volume) async {
    await _playbackEngine.setVolume(volume);
    if (session != null) {
      _updateState(session: session!.copyWith(preferredVolume: volume.round()));
      notifyListeners();
    }
    await _flushProgress();
  }

  Future<void> adjustVolumeBy(double delta) async {
    final nextVolume = (runtime.volume + delta).clamp(0.0, 100.0).toDouble();
    await setPlaybackVolume(nextVolume);
  }

  Future<void> selectSubtitleTrack(String trackId) async {
    await _playbackEngine.setSubtitleTrack(trackId);
  }

  Future<void> setAnime4kMode(PlayerAnime4kMode mode) async {
    _anime4kModeHydrated = true;
    await _playbackEngine.setAnime4kMode(mode);
    await _appPreferences.savePlayerAnime4kMode(mode);
  }

  Future<void> closeSession() async {
    await _flushProgress();
    await _sessionApi.close();
    clear(flushProgress: false);
  }

  void clear({bool flushProgress = true}) {
    if (flushProgress) {
      final itemId = _currentProgressItemId;
      final runtimeSnapshot = runtime;
      unawaited(_flushProgress(itemId: itemId, runtimeState: runtimeSnapshot));
    }
    final hadState =
        session != null || playlist != null || playbackTarget != null;
    _updateState(session: null, playlist: null, playbackTarget: null);
    unawaited(_syncPlayback());
    if (hadState) {
      notifyListeners();
    }
  }

  Future<void> _refreshPlaylist() async {
    if (session == null) {
      _updateState(playlist: null);
      return;
    }

    try {
      _updateState(playlist: await _playlistApi.getCurrent());
    } on ApiException {
      _updateState(playlist: null);
    }
  }

  Future<void> _syncPlayback() {
    _progressSynchronizer.resetForItem(playbackTarget?.itemId);
    return _playbackEngine.load(target: playbackTarget, session: session);
  }

  Future<void> _hydrateAnime4kMode() async {
    if (_anime4kModeHydrated) {
      return;
    }

    _anime4kModeHydrated = true;
    await _playbackEngine.setAnime4kMode(
      await _appPreferences.loadPlayerAnime4kMode(),
    );
  }

  void _handlePlaybackChanged() {
    _syncRuntimeState();
    if (_isDisposed) {
      return;
    }

    if (runtime.isCompleted) {
      unawaited(_completeCurrentItem());
    } else {
      unawaited(_reportProgressIfDue());
    }
    notifyListeners();
  }

  Future<void> _reportProgressIfDue() async {
    try {
      final snapshot = await _progressSynchronizer.reportIfDue(
        itemId: _currentProgressItemId,
        runtime: runtime,
      );
      if (snapshot == null) {
        return;
      }

      _applyProgressSnapshot(snapshot);
      if (!_isDisposed) {
        notifyListeners();
      }
    } catch (error, stackTrace) {
      AppLogger.error(
        'PlayerController.Progress',
        'Failed to report playback progress.',
        error: error,
        stackTrace: stackTrace,
      );
    }
  }

  Future<void> _flushProgress({
    String? itemId,
    PlayerRuntimeState? runtimeState,
    bool notify = true,
  }) async {
    try {
      final snapshot = await _progressSynchronizer.reportNow(
        itemId: itemId ?? _currentProgressItemId,
        runtime: runtimeState ?? runtime,
      );
      if (snapshot == null) {
        return;
      }

      _applyProgressSnapshot(snapshot);
      if (notify && !_isDisposed) {
        notifyListeners();
      }
    } catch (error, stackTrace) {
      AppLogger.error(
        'PlayerController.Progress',
        'Failed to flush playback progress.',
        error: error,
        stackTrace: stackTrace,
      );
    }
  }

  Future<void> _completeCurrentItem() async {
    final itemId = _currentProgressItemId;
    if (itemId == null || _completingItemId == itemId) {
      return;
    }

    _completingItemId = itemId;
    try {
      final completed = await _progressSynchronizer.completeIfNeeded(itemId);
      if (!completed) {
        return;
      }

      _applyCompletion(itemId);
      await _refreshPlaylist();
      if (!_isDisposed) {
        notifyListeners();
      }
    } catch (error, stackTrace) {
      AppLogger.error(
        'PlayerController.Progress',
        'Failed to complete playback item.',
        error: error,
        stackTrace: stackTrace,
      );
    } finally {
      if (_completingItemId == itemId) {
        _completingItemId = null;
      }
    }
  }

  void _applyProgressSnapshot(PlayerProgressSnapshot snapshot) {
    if (session?.currentItemId == snapshot.itemId) {
      _updateState(
        session: session!.copyWith(
          savedProgressMs: snapshot.positionMs,
          preferredRate: snapshot.rate,
          preferredVolume: snapshot.volume,
        ),
      );
    }

    final playlist = this.playlist;
    if (playlist == null) {
      return;
    }

    final itemIndex = playlist.items.indexWhere(
      (PlaylistItemDto item) => item.itemId == snapshot.itemId,
    );
    if (itemIndex < 0) {
      return;
    }

    final items = List<PlaylistItemDto>.of(playlist.items);
    final item = items[itemIndex];
    items[itemIndex] = item.copyWith(
      hasSavedProgress: snapshot.positionMs > 0,
      savedProgressMs: snapshot.positionMs,
      durationMs: snapshot.durationMs > 0
          ? snapshot.durationMs
          : item.durationMs,
    );
    _updateState(playlist: playlist.copyWith(items: items));
  }

  void _applyCompletion(String itemId) {
    if (session?.currentItemId == itemId) {
      _updateState(session: session!.copyWith(savedProgressMs: 0));
    }

    final playlist = this.playlist;
    if (playlist == null) {
      return;
    }

    final itemIndex = playlist.items.indexWhere(
      (PlaylistItemDto item) => item.itemId == itemId,
    );
    if (itemIndex < 0) {
      return;
    }

    final items = List<PlaylistItemDto>.of(playlist.items);
    items[itemIndex] = items[itemIndex].copyWith(
      isPlayed: true,
      hasSavedProgress: false,
      savedProgressMs: 0,
    );
    _updateState(playlist: playlist.copyWith(items: items));
  }

  String? get _currentProgressItemId =>
      playbackTarget?.itemId ?? selectedItemId;

  void _syncRuntimeState() {
    final nextRuntime = _playbackEngine.runtimeState;
    if (!identical(nextRuntime, _state.runtime)) {
      _state = _state.copyWith(runtime: nextRuntime);
    }
  }

  void _updateState({
    Object? session = _playerStateSentinel,
    Object? playlist = _playerStateSentinel,
    Object? playbackTarget = _playerStateSentinel,
    PlayerRuntimeState? runtime,
  }) {
    _state = _state.copyWith(
      session: session,
      playlist: playlist,
      playbackTarget: playbackTarget,
      runtime: runtime,
    );
  }

  @override
  void dispose() {
    final itemId = _currentProgressItemId;
    final runtimeSnapshot = runtime;
    _isDisposed = true;
    unawaited(
      _flushProgress(
        itemId: itemId,
        runtimeState: runtimeSnapshot,
        notify: false,
      ),
    );
    _playbackEngine.removeListener(_handlePlaybackChanged);
    _playbackEngine.dispose();
    super.dispose();
  }
}

const Object _playerStateSentinel = Object();
