import 'package:aninest_flutter/src/app/app_controller.dart';
import 'package:aninest_flutter/src/features/player/application/player_anime4k_mode.dart';
import 'package:aninest_flutter/src/features/player/application/player_subtitle_track_option.dart';
import 'package:flutter/foundation.dart';

class PlayerControlBarStateController extends ChangeNotifier {
  PlayerControlBarStateController({required AppController appController})
    : _appController = appController,
      _canMovePrevious = appController.canMovePrevious,
      _canMoveNext = appController.canMoveNext,
      _canTogglePlayback = appController.canTogglePlayback,
      _isPlaying = appController.playerRuntime.isPlaying,
      _playbackRate = appController.playbackRate,
      _playbackVolume = appController.playbackVolume,
      _subtitleTracks = appController.playerRuntime.subtitleTracks,
      _selectedSubtitleTrackId =
          appController.playerRuntime.selectedSubtitleTrackId,
      _anime4kMode = appController.playerRuntime.anime4kMode {
    _appController.player.addListener(_handlePlayerChanged);
  }

  final AppController _appController;

  bool _canMovePrevious;
  bool _canMoveNext;
  bool _canTogglePlayback;
  bool _isPlaying;
  double _playbackRate;
  double _playbackVolume;
  List<PlayerSubtitleTrackOption> _subtitleTracks;
  String _selectedSubtitleTrackId;
  PlayerAnime4kMode _anime4kMode;

  AppController get appController => _appController;
  bool get canMovePrevious => _canMovePrevious;
  bool get canMoveNext => _canMoveNext;
  bool get canTogglePlayback => _canTogglePlayback;
  bool get isPlaying => _isPlaying;
  double get playbackRate => _playbackRate;
  double get playbackVolume => _playbackVolume;
  List<PlayerSubtitleTrackOption> get subtitleTracks => _subtitleTracks;
  String get selectedSubtitleTrackId => _selectedSubtitleTrackId;
  PlayerAnime4kMode get anime4kMode => _anime4kMode;

  bool get isMuted => _playbackVolume <= 0.001;
  bool get hasSelectableSubtitles => _subtitleTracks.any(
    (PlayerSubtitleTrackOption option) => !option.isAutomatic && !option.isOff,
  );

  void _handlePlayerChanged() {
    final runtime = _appController.playerRuntime;
    final nextCanMovePrevious = _appController.canMovePrevious;
    final nextCanMoveNext = _appController.canMoveNext;
    final nextCanTogglePlayback = _appController.canTogglePlayback;
    final nextIsPlaying = runtime.isPlaying;
    final nextPlaybackRate = runtime.rate;
    final nextPlaybackVolume = runtime.volume;
    final nextSubtitleTracks = runtime.subtitleTracks;
    final nextSelectedSubtitleTrackId = runtime.selectedSubtitleTrackId;
    final nextAnime4kMode = runtime.anime4kMode;

    final hasChanged =
        nextCanMovePrevious != _canMovePrevious ||
        nextCanMoveNext != _canMoveNext ||
        nextCanTogglePlayback != _canTogglePlayback ||
        nextIsPlaying != _isPlaying ||
        nextPlaybackRate != _playbackRate ||
        nextPlaybackVolume != _playbackVolume ||
        !listEquals(nextSubtitleTracks, _subtitleTracks) ||
        nextSelectedSubtitleTrackId != _selectedSubtitleTrackId ||
        nextAnime4kMode != _anime4kMode;
    if (!hasChanged) {
      return;
    }

    _canMovePrevious = nextCanMovePrevious;
    _canMoveNext = nextCanMoveNext;
    _canTogglePlayback = nextCanTogglePlayback;
    _isPlaying = nextIsPlaying;
    _playbackRate = nextPlaybackRate;
    _playbackVolume = nextPlaybackVolume;
    _subtitleTracks = nextSubtitleTracks;
    _selectedSubtitleTrackId = nextSelectedSubtitleTrackId;
    _anime4kMode = nextAnime4kMode;
    notifyListeners();
  }

  @override
  void dispose() {
    _appController.player.removeListener(_handlePlayerChanged);
    super.dispose();
  }
}
