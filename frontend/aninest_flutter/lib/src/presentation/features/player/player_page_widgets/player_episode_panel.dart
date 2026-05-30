import 'dart:async';

import 'package:aninest_flutter/src/features/player/application/player_controller.dart';
import 'package:aninest_flutter/src/features/player/application/player_runtime_state.dart';
import 'package:aninest_flutter/src/models/playlist_models.dart';
import 'package:aninest_flutter/src/models/session_models.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

import 'episode_panel_widgets/player_episode_panel_frame.dart';
import 'player_selector.dart';

class PlayerEpisodePanel extends StatefulWidget {
  const PlayerEpisodePanel({super.key, required this.controller});

  final PlayerController controller;

  @override
  State<PlayerEpisodePanel> createState() => _PlayerEpisodePanelState();
}

class _PlayerEpisodePanelState extends State<PlayerEpisodePanel> {
  final ScrollController _scrollController = ScrollController();

  @override
  void dispose() {
    _scrollController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return PlayerSelector<
      ({
        PlaylistDto? playlist,
        String? selectedItemId,
        PlaybackTargetDto? playbackTarget,
        PlayerRuntimeState runtime,
      })
    >(
      controller: widget.controller,
      selector: (state) => (
        playlist: state.playlist,
        selectedItemId: state.selectedItemId,
        playbackTarget: state.playbackTarget,
        runtime: state.runtime,
      ),
      builder: (BuildContext context, value) {
        final playlist = value.playlist;
        final selectedItemId = value.selectedItemId;
        final playbackTarget = value.playbackTarget;
        final runtime = value.runtime;
        final currentPlaybackProgressFraction =
            playbackTarget != null &&
                runtime.hasMedia &&
                !runtime.isLoading &&
                runtime.duration > Duration.zero
            ? runtime.progressFraction
            : null;

        return PlayerEpisodePanelFrame(
          playlist: playlist,
          selectedItemId: selectedItemId,
          currentPlaybackItemId: playbackTarget?.itemId,
          currentPlaybackStartPositionMs: playbackTarget?.startPositionMs,
          currentPlaybackProgressFraction: currentPlaybackProgressFraction,
          scrollController: _scrollController,
          onItemPressed: (String itemId) {
            if (itemId != selectedItemId) {
              unawaited(widget.controller.selectItemAndPlay(itemId));
            }
          },
        );
      },
    );
  }
}
