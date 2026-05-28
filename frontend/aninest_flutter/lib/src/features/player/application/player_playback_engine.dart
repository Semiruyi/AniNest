import 'dart:async';

import 'package:aninest_flutter/src/core/logging/app_logger.dart';
import 'package:aninest_flutter/src/features/player/application/player_anime4k_mode.dart';
import 'package:aninest_flutter/src/features/player/application/player_anime4k_shader_support.dart';
import 'package:aninest_flutter/src/features/player/application/player_runtime_state.dart';
import 'package:aninest_flutter/src/features/player/application/player_subtitle_track_mapper.dart';
import 'package:aninest_flutter/src/features/player/application/player_subtitle_track_option.dart';
import 'package:aninest_flutter/src/models/session_models.dart';
import 'package:flutter/foundation.dart';
import 'package:media_kit/media_kit.dart';
import 'package:media_kit_video/media_kit_video.dart';

class PlayerPlaybackEngine extends ChangeNotifier {
  static const List<String> _ignoredMpvLogSnippets = <String>[
    'property not found _setProperty(osc, 1)',
    'Failed to create EGL surface',
    'Failed to create file cache.',
  ];

  PlayerPlaybackEngine() {
    _videoController = VideoController(_player);
    _bindStreams();
    final state = _player.state;
    _availableSubtitleTracks = state.tracks.subtitle;
    _runtimeState = _runtimeState.copyWith(
      isPlaying: state.playing,
      isCompleted: state.completed,
      position: state.position,
      duration: state.duration,
      volume: state.volume,
      rate: state.rate,
      isBuffering: state.buffering,
      buffer: state.buffer,
      subtitleTracks: PlayerSubtitleTrackMapper.fromMediaKitTracks(
        state.tracks.subtitle,
      ),
      selectedSubtitleTrackId: state.track.subtitle.id,
      anime4kMode: _anime4kMode,
    );
  }

  final Player _player = Player(
    configuration: const PlayerConfiguration(libass: true),
  );
  late final VideoController _videoController;
  final List<StreamSubscription<dynamic>> _subscriptions =
      <StreamSubscription<dynamic>>[];

  PlayerRuntimeState _runtimeState = const PlayerRuntimeState.initial();
  String? _loadedPlaybackKey;
  List<SubtitleTrack> _availableSubtitleTracks = const <SubtitleTrack>[];
  double _lastNonZeroVolume = 80;
  int _loadGeneration = 0;
  PlayerAnime4kMode _anime4kMode = PlayerAnime4kMode.off;

  PlayerRuntimeState get runtimeState => _runtimeState;
  VideoController get videoController => _videoController;

  Future<void> load({
    required PlaybackTargetDto? target,
    required SessionStateDto? session,
  }) async {
    final generation = ++_loadGeneration;

    if (target == null || session == null) {
      _loadedPlaybackKey = null;
      _availableSubtitleTracks = const <SubtitleTrack>[];
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
        subtitleTracks: const <PlayerSubtitleTrackOption>[],
        selectedSubtitleTrackId: PlayerSubtitleTrackOption.automaticId,
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
      AppLogger.info(
        'PlayerPlaybackEngine',
        'Opening media item=${target.itemId}',
      );
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
      await _player.setSubtitleTrack(SubtitleTrack.auto());
      await _applyAnime4kConfiguration();
      AppLogger.info(
        'PlayerPlaybackEngine',
        'Opened media item=${target.itemId}',
      );
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

  Future<void> setSubtitleTrack(String trackId) async {
    if (!_runtimeState.hasMedia) {
      return;
    }

    final track = _subtitleTrackForId(trackId);
    if (track == null) {
      return;
    }

    await _player.setSubtitleTrack(track);
    _setState(selectedSubtitleTrackId: track.id);
  }

  Future<void> setAnime4kMode(PlayerAnime4kMode mode) async {
    if (_anime4kMode == mode) {
      return;
    }

    _anime4kMode = mode;
    await _applyAnime4kConfiguration();
    _setState(anime4kMode: mode);
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
      _player.stream.tracks.listen((Tracks value) {
        _availableSubtitleTracks = value.subtitle;
        _setState(
          subtitleTracks: PlayerSubtitleTrackMapper.fromMediaKitTracks(
            value.subtitle,
          ),
        );
      }),
      _player.stream.track.listen((Track value) {
        _setState(selectedSubtitleTrackId: value.subtitle.id);
      }),
      _player.stream.buffering.listen((bool value) {
        _setState(isBuffering: value);
      }),
      _player.stream.buffer.listen((Duration value) {
        _setState(buffer: value);
      }),
      _player.stream.log.listen(_handleMpvLog),
      _player.stream.error.listen((String value) {
        AppLogger.error(
          'PlayerPlaybackEngine.MPV',
          'Player stream error: $value',
        );
        _setState(errorMessage: value, isLoading: false, isReady: false);
      }),
    ]);
  }

  SubtitleTrack? _subtitleTrackForId(String trackId) {
    if (trackId == PlayerSubtitleTrackOption.automaticId) {
      return SubtitleTrack.auto();
    }

    if (trackId == PlayerSubtitleTrackOption.offId) {
      return SubtitleTrack.no();
    }

    for (final track in _availableSubtitleTracks) {
      if (track.id == trackId) {
        return track;
      }
    }

    return null;
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
    List<PlayerSubtitleTrackOption>? subtitleTracks,
    String? selectedSubtitleTrackId,
    PlayerAnime4kMode? anime4kMode,
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
      subtitleTracks: subtitleTracks,
      selectedSubtitleTrackId: selectedSubtitleTrackId,
      anime4kMode: anime4kMode,
      errorMessage: errorMessage,
    );
    notifyListeners();
  }

  void _handleMpvLog(PlayerLog log) {
    final message = '${log.prefix}[${log.level}] ${log.text}';
    if (_ignoredMpvLogSnippets.any(message.contains)) {
      return;
    }

    switch (log.level) {
      case 'fatal':
      case 'error':
        AppLogger.error('PlayerPlaybackEngine.MPV', message);
        break;
      case 'warn':
        AppLogger.warning('PlayerPlaybackEngine.MPV', message);
        break;
      default:
        break;
    }
  }

  Future<void> _applyAnime4kConfiguration() async {
    final nativePlayer = _player.platform;
    if (!PlayerAnime4kShaderSupport.isSupported || nativePlayer == null) {
      return;
    }

    try {
      final shaderChain = await PlayerAnime4kShaderSupport.shaderChainFor(
        _anime4kMode,
      );
      final player = nativePlayer as dynamic;
      if (shaderChain == null || shaderChain.isEmpty) {
        await player.command(<String>[
          'change-list',
          'glsl-shaders',
          'clr',
          '',
        ]);
      } else {
        await player.command(<String>[
          'change-list',
          'glsl-shaders',
          'set',
          shaderChain,
        ]);
      }

      final activeShaderChain = await player.getProperty('glsl-shaders');
      final isApplied = _anime4kMode == PlayerAnime4kMode.off
          ? activeShaderChain.trim().isEmpty
          : activeShaderChain.trim().isNotEmpty;
      if (!isApplied) {
        AppLogger.warning(
          'PlayerPlaybackEngine',
          'Anime4K mode verification mismatch for mode=${_anime4kMode.id}',
        );
        return;
      }

      AppLogger.info('PlayerPlaybackEngine', 'Anime4K mode=${_anime4kMode.id}');
    } catch (error, stackTrace) {
      AppLogger.error(
        'PlayerPlaybackEngine',
        'Failed to apply Anime4K configuration.',
        error: error,
        stackTrace: stackTrace,
      );
    }
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
