import 'dart:async';

import 'package:aninest_flutter/src/core/logging/app_logger.dart';
import 'package:aninest_flutter/src/features/player/application/player_runtime_state.dart';
import 'package:aninest_flutter/src/models/session_models.dart';
import 'package:media_kit/media_kit.dart';
import 'package:media_kit_video/media_kit_video.dart';
import 'package:flutter/foundation.dart';

class PlayerPlaybackEngine extends ChangeNotifier {
  PlayerPlaybackEngine() {
    _videoController = VideoController(_player);
    _bindStreams();
    final state = _player.state;
    _runtimeState = _runtimeState.copyWith(
      isPlaying: state.playing,
      isCompleted: state.completed,
      position: state.position,
      duration: state.duration,
      volume: state.volume,
      rate: state.rate,
      isBuffering: state.buffering,
      buffer: state.buffer,
    );
  }

  final Player _player = Player();
  late final VideoController _videoController;
  final List<StreamSubscription<dynamic>> _subscriptions =
      <StreamSubscription<dynamic>>[];

  PlayerRuntimeState _runtimeState = const PlayerRuntimeState.initial();
  String? _loadedPlaybackKey;
  double _lastNonZeroVolume = 80;
  int _loadGeneration = 0;

  PlayerRuntimeState get runtimeState => _runtimeState;
  VideoController get videoController => _videoController;

  Future<void> load({
    required PlaybackTargetDto? target,
    required SessionStateDto? session,
  }) async {
    final generation = ++_loadGeneration;

    if (target == null || session == null) {
      _loadedPlaybackKey = null;
      _setState(
        hasMedia: false,
        isLoading: false,
        isReady: false,
        isPlaying: false,
        isBuffering: false,
        isCompleted: false,
        position: Duration.zero,
        duration: Duration.zero,
        buffer: Duration.zero,
        errorMessage: null,
      );
      await _player.stop();
      return;
    }

    final playbackKey =
        '${target.itemId}|${target.mediaUrl}|${target.startPositionMs}|${session.preferredRate}|${session.preferredVolume}';
    if (_loadedPlaybackKey == playbackKey) {
      return;
    }

    _setState(
      hasMedia: true,
      isLoading: true,
      isReady: false,
      isCompleted: false,
      errorMessage: null,
    );

    try {
      await _player.open(
        Media(
          target.mediaUrl,
          start: Duration(milliseconds: target.startPositionMs),
        ),
        play: false,
      );
      if (generation != _loadGeneration) {
        return;
      }

      await _player.setRate(session.preferredRate);
      await _player.setVolume(session.preferredVolume.toDouble());
      await _player.setSubtitleTrack(SubtitleTrack.no());
      if (generation != _loadGeneration) {
        return;
      }

      _loadedPlaybackKey = playbackKey;
      _setState(
        hasMedia: true,
        isLoading: false,
        isReady: true,
        errorMessage: null,
      );
    } catch (error, stackTrace) {
      if (generation != _loadGeneration) {
        return;
      }

      _loadedPlaybackKey = null;
      _setState(
        isLoading: false,
        isReady: false,
        isPlaying: false,
        errorMessage: error.toString(),
      );
      AppLogger.error(
        'PlayerPlaybackEngine',
        'Failed to open playback target.',
        error: error,
        stackTrace: stackTrace,
      );
    }
  }

  Future<void> togglePlayPause() async {
    if (!_runtimeState.hasMedia) {
      return;
    }

    await _player.playOrPause();
  }

  Future<void> play() async {
    if (!_runtimeState.hasMedia) {
      return;
    }

    await _player.play();
  }

  Future<void> pause() async {
    if (!_runtimeState.hasMedia) {
      return;
    }

    await _player.pause();
  }

  Future<void> seek(Duration position) async {
    if (!_runtimeState.hasMedia) {
      return;
    }

    await _player.seek(position);
  }

  Future<void> setRate(double rate) async {
    await _player.setRate(rate);
  }

  Future<void> setVolume(double volume) async {
    final clamped = volume.clamp(0, 100).toDouble();
    if (clamped > 0.001) {
      _lastNonZeroVolume = clamped;
    }
    await _player.setVolume(clamped);
  }

  Future<void> toggleMute() async {
    if (_runtimeState.isMuted) {
      await setVolume(_lastNonZeroVolume <= 0.001 ? 80 : _lastNonZeroVolume);
      return;
    }

    _lastNonZeroVolume = _runtimeState.volume;
    await setVolume(0);
  }

  void _bindStreams() {
    _subscriptions.addAll(<StreamSubscription<dynamic>>[
      _player.stream.playing.listen((bool value) {
        _setState(isPlaying: value);
      }),
      _player.stream.completed.listen((bool value) {
        _setState(isCompleted: value);
      }),
      _player.stream.position.listen((Duration value) {
        _setState(position: value);
      }),
      _player.stream.duration.listen((Duration value) {
        _setState(duration: value);
      }),
      _player.stream.volume.listen((double value) {
        if (value > 0.001) {
          _lastNonZeroVolume = value;
        }
        _setState(volume: value);
      }),
      _player.stream.rate.listen((double value) {
        _setState(rate: value);
      }),
      _player.stream.buffering.listen((bool value) {
        _setState(isBuffering: value);
      }),
      _player.stream.buffer.listen((Duration value) {
        _setState(buffer: value);
      }),
      _player.stream.error.listen((String value) {
        _setState(errorMessage: value, isLoading: false, isReady: false);
      }),
    ]);
  }

  void _setState({
    bool? hasMedia,
    bool? isLoading,
    bool? isReady,
    bool? isPlaying,
    bool? isBuffering,
    bool? isCompleted,
    Duration? position,
    Duration? duration,
    Duration? buffer,
    double? volume,
    double? rate,
    Object? errorMessage = _sentinel,
  }) {
    _runtimeState = _runtimeState.copyWith(
      hasMedia: hasMedia,
      isLoading: isLoading,
      isReady: isReady,
      isPlaying: isPlaying,
      isBuffering: isBuffering,
      isCompleted: isCompleted,
      position: position,
      duration: duration,
      buffer: buffer,
      volume: volume,
      rate: rate,
      errorMessage: errorMessage,
    );
    notifyListeners();
  }

  @override
  void dispose() {
    for (final subscription in _subscriptions) {
      subscription.cancel();
    }
    _player.dispose();
    super.dispose();
  }
}

const Object _sentinel = Object();
