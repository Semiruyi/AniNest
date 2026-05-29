import 'package:aninest_flutter/src/features/player/application/player_runtime_state.dart';
import 'package:aninest_flutter/src/models/playlist_models.dart';
import 'package:aninest_flutter/src/models/session_models.dart';

class PlayerState {
  const PlayerState({
    required this.session,
    required this.playlist,
    required this.playbackTarget,
    required this.runtime,
  });

  const PlayerState.initial()
    : session = null,
      playlist = null,
      playbackTarget = null,
      runtime = const PlayerRuntimeState.initial();

  final SessionStateDto? session;
  final PlaylistDto? playlist;
  final PlaybackTargetDto? playbackTarget;
  final PlayerRuntimeState runtime;

  String? get selectedFolderId => session?.folderId;

  String? get selectedItemId =>
      session?.currentItemId ?? playlist?.currentItemId;

  bool get canMovePrevious => session?.hasPrevious ?? false;
  bool get canMoveNext => session?.hasNext ?? false;
  bool get canTogglePlayback => playbackTarget != null;
  double get playbackRate => runtime.rate;
  double get playbackVolume => runtime.volume;

  PlayerState copyWith({
    Object? session = _sentinel,
    Object? playlist = _sentinel,
    Object? playbackTarget = _sentinel,
    PlayerRuntimeState? runtime,
  }) {
    return PlayerState(
      session: identical(session, _sentinel)
          ? this.session
          : session as SessionStateDto?,
      playlist: identical(playlist, _sentinel)
          ? this.playlist
          : playlist as PlaylistDto?,
      playbackTarget: identical(playbackTarget, _sentinel)
          ? this.playbackTarget
          : playbackTarget as PlaybackTargetDto?,
      runtime: runtime ?? this.runtime,
    );
  }
}

const Object _sentinel = Object();
