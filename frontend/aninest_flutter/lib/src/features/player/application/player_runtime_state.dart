import 'player_subtitle_track_option.dart';

class PlayerRuntimeState {
  const PlayerRuntimeState({
    required this.hasMedia,
    required this.isLoading,
    required this.isReady,
    required this.isPlaying,
    required this.isBuffering,
    required this.isCompleted,
    required this.position,
    required this.duration,
    required this.buffer,
    required this.volume,
    required this.rate,
    required this.subtitleTracks,
    required this.selectedSubtitleTrackId,
    required this.errorMessage,
  });

  const PlayerRuntimeState.initial()
    : hasMedia = false,
      isLoading = false,
      isReady = false,
      isPlaying = false,
      isBuffering = false,
      isCompleted = false,
      position = Duration.zero,
      duration = Duration.zero,
      buffer = Duration.zero,
      volume = 80,
      rate = 1,
      subtitleTracks = const <PlayerSubtitleTrackOption>[],
      selectedSubtitleTrackId = PlayerSubtitleTrackOption.automaticId,
      errorMessage = null;

  final bool hasMedia;
  final bool isLoading;
  final bool isReady;
  final bool isPlaying;
  final bool isBuffering;
  final bool isCompleted;
  final Duration position;
  final Duration duration;
  final Duration buffer;
  final double volume;
  final double rate;
  final List<PlayerSubtitleTrackOption> subtitleTracks;
  final String selectedSubtitleTrackId;
  final String? errorMessage;

  bool get isMuted => volume <= 0.001;
  bool get hasSelectableSubtitles => subtitleTracks.any(
    (PlayerSubtitleTrackOption option) => !option.isAutomatic && !option.isOff,
  );

  double get progressFraction {
    final total = duration.inMicroseconds;
    if (total <= 0) {
      return 0;
    }

    return (position.inMicroseconds / total).clamp(0.0, 1.0);
  }

  PlayerRuntimeState copyWith({
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
    Object? errorMessage = _sentinel,
  }) {
    return PlayerRuntimeState(
      hasMedia: hasMedia ?? this.hasMedia,
      isLoading: isLoading ?? this.isLoading,
      isReady: isReady ?? this.isReady,
      isPlaying: isPlaying ?? this.isPlaying,
      isBuffering: isBuffering ?? this.isBuffering,
      isCompleted: isCompleted ?? this.isCompleted,
      position: position ?? this.position,
      duration: duration ?? this.duration,
      buffer: buffer ?? this.buffer,
      volume: volume ?? this.volume,
      rate: rate ?? this.rate,
      subtitleTracks: subtitleTracks ?? this.subtitleTracks,
      selectedSubtitleTrackId:
          selectedSubtitleTrackId ?? this.selectedSubtitleTrackId,
      errorMessage: identical(errorMessage, _sentinel)
          ? this.errorMessage
          : errorMessage as String?,
    );
  }
}

const Object _sentinel = Object();
