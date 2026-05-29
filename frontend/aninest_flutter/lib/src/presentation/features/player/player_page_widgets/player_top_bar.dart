import 'package:aninest_flutter/src/features/player/application/player_controller.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

import 'player_selector.dart';
import 'top_bar_widgets/player_top_bar_frame.dart';
import 'top_bar_widgets/player_top_bar_playback_info.dart';

class PlayerTopBar extends StatelessWidget {
  const PlayerTopBar({super.key, required this.controller});

  final PlayerController controller;

  @override
  Widget build(BuildContext context) {
    return PlayerSelector<PlayerTopBarPlaybackInfo>(
      controller: controller,
      selector: (state) => PlayerTopBarPlaybackInfo.fromSelection(
        playlist: state.playlist,
        selectedItemId: state.selectedItemId,
      ),
      builder: (BuildContext context, value) {
        return PlayerTopBarFrame(info: value);
      },
    );
  }
}
