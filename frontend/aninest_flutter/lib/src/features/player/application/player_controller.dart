import 'dart:async';

import 'package:aninest_flutter/src/api/api_exception.dart';
import 'package:aninest_flutter/src/features/player/application/player_playback_engine.dart';
import 'package:aninest_flutter/src/features/player/application/player_runtime_state.dart';
import 'package:aninest_flutter/src/models/playlist_models.dart';
import 'package:aninest_flutter/src/models/session_models.dart';
import 'package:aninest_flutter/src/services/playlist_api.dart';
import 'package:aninest_flutter/src/services/session_api.dart';
import 'package:flutter/foundation.dart';
import 'package:media_kit_video/media_kit_video.dart';

class PlayerController extends ChangeNotifier {
  PlayerController(this._sessionApi, this._playlistApi) {
    _playbackEngine.addListener(_handlePlaybackChanged);
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
  final PlayerPlaybackEngine _playbackEngine = PlayerPlaybackEngine();

  SessionStateDto? _session;
  PlaylistDto? _playlist;
  PlaybackTargetDto? _playbackTarget;

  SessionStateDto? get session => _session;
  PlaylistDto? get playlist => _playlist;
  PlaybackTargetDto? get playbackTarget => _playbackTarget;
  PlayerRuntimeState get runtime => _playbackEngine.runtimeState;
  VideoController get videoController => _playbackEngine.videoController;

  String? get selectedFolderId => _session?.folderId;
  String? get selectedItemId =>
      _session?.currentItemId ?? _playlist?.currentItemId;

  bool get canMovePrevious => _session?.hasPrevious ?? false;
  bool get canMoveNext => _session?.hasNext ?? false;
  bool get canTogglePlayback => _playbackTarget != null;
  double get playbackRate => runtime.rate;
  double get playbackVolume => runtime.volume;

  void rebind(SessionApi sessionApi, PlaylistApi playlistApi) {
    _sessionApi = sessionApi;
    _playlistApi = playlistApi;
  }

  Future<void> restore() async {
    try {
      _session = await _sessionApi.getCurrent();
    } on ApiException {
      _session = null;
    }

    if (_session == null) {
      _playlist = null;
      _playbackTarget = null;
      await _syncPlayback();
      notifyListeners();
      return;
    }

    await _refreshPlaylist();
    await _syncPlayback();
    notifyListeners();
  }

  Future<void> openFolder(String folderId) async {
    final result = await _sessionApi.openFolder(folderId);
    _session = result.session;
    _playbackTarget = result.playbackTarget;
    await _refreshPlaylist();
    await _syncPlayback();
    notifyListeners();
  }

  Future<void> selectItem(String itemId) async {
    final result = await _sessionApi.selectItem(itemId);
    _session = result.session;
    _playbackTarget = result.playbackTarget;
    await _refreshPlaylist();
    await _syncPlayback();
    notifyListeners();
  }

  Future<void> selectItemAndPlay(String itemId) async {
    await selectItem(itemId);
    await play();
  }

  Future<void> moveNext() async {
    final result = await _sessionApi.moveNext();
    _session = result.session;
    _playbackTarget = result.playbackTarget;
    await _refreshPlaylist();
    await _syncPlayback();
    notifyListeners();
  }

  Future<void> moveNextAndPlay() async {
    await moveNext();
    await play();
  }

  Future<void> movePrevious() async {
    final result = await _sessionApi.movePrevious();
    _session = result.session;
    _playbackTarget = result.playbackTarget;
    await _refreshPlaylist();
    await _syncPlayback();
    notifyListeners();
  }

  Future<void> movePreviousAndPlay() async {
    await movePrevious();
    await play();
  }

  Future<void> togglePlayPause() async {
    await _playbackEngine.togglePlayPause();
  }

  Future<void> play() async {
    if (!canTogglePlayback) {
      return;
    }

    await _playbackEngine.play();
  }

  Future<void> pause() async {
    await _playbackEngine.pause();
  }

  Future<void> seekTo(Duration position) async {
    final maxPosition = runtime.duration;
    if (maxPosition <= Duration.zero) {
      return;
    }

    final clamped = position > maxPosition ? maxPosition : position;
    await _playbackEngine.seek(clamped);
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
    if (_session != null) {
      _session = _session!.copyWith(preferredRate: rate);
      notifyListeners();
    }
  }

  Future<void> toggleMute() async {
    await _playbackEngine.toggleMute();
    if (_session != null) {
      _session = _session!.copyWith(preferredVolume: runtime.volume.round());
      notifyListeners();
    }
  }

  Future<void> setPlaybackVolume(double volume) async {
    await _playbackEngine.setVolume(volume);
    if (_session != null) {
      _session = _session!.copyWith(preferredVolume: volume.round());
      notifyListeners();
    }
  }

  Future<void> closeSession() async {
    await _sessionApi.close();
    clear();
  }

  void clear() {
    final hadState =
        _session != null || _playlist != null || _playbackTarget != null;
    _session = null;
    _playlist = null;
    _playbackTarget = null;
    unawaited(_syncPlayback());
    if (hadState) {
      notifyListeners();
    }
  }

  Future<void> _refreshPlaylist() async {
    if (_session == null) {
      _playlist = null;
      return;
    }

    try {
      _playlist = await _playlistApi.getCurrent();
    } on ApiException {
      _playlist = null;
    }
  }

  Future<void> _syncPlayback() {
    return _playbackEngine.load(target: _playbackTarget, session: _session);
  }

  void _handlePlaybackChanged() {
    notifyListeners();
  }

  @override
  void dispose() {
    _playbackEngine.removeListener(_handlePlaybackChanged);
    _playbackEngine.dispose();
    super.dispose();
  }
}
