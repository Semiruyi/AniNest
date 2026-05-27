import 'package:media_kit/media_kit.dart';

import 'player_subtitle_track_option.dart';

class PlayerSubtitleTrackMapper {
  const PlayerSubtitleTrackMapper._();

  static List<PlayerSubtitleTrackOption> fromMediaKitTracks(
    List<SubtitleTrack> tracks,
  ) {
    final options = <PlayerSubtitleTrackOption>[];
    final seen = <String>{};
    var realTrackIndex = 0;

    for (final track in tracks) {
      if (!seen.add(track.id)) {
        continue;
      }

      final kind = _kindForTrack(track);
      final index =
          kind == PlayerSubtitleTrackKind.automatic ||
              kind == PlayerSubtitleTrackKind.off
          ? 0
          : ++realTrackIndex;

      options.add(
        PlayerSubtitleTrackOption(
          id: track.id,
          title: track.title,
          language: track.language,
          codec: track.codec,
          kind: kind,
          index: index,
        ),
      );
    }

    return options;
  }

  static PlayerSubtitleTrackKind _kindForTrack(SubtitleTrack track) {
    if (track.id == PlayerSubtitleTrackOption.automaticId) {
      return PlayerSubtitleTrackKind.automatic;
    }

    if (track.id == PlayerSubtitleTrackOption.offId) {
      return PlayerSubtitleTrackKind.off;
    }

    if (track.uri || track.data) {
      return PlayerSubtitleTrackKind.external;
    }

    return PlayerSubtitleTrackKind.embedded;
  }
}
