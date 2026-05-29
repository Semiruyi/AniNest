import 'package:shadcn_flutter/shadcn_flutter.dart';

import 'top_bar_widgets/player_top_bar_frame.dart';
import 'top_bar_widgets/player_top_bar_playback_info.dart';
import 'player_view_state_controller.dart';

class PlayerTopBar extends StatelessWidget {
  const PlayerTopBar({super.key, required this.controller});

  final PlayerViewStateController controller;

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: controller,
      builder: (BuildContext context, Widget? child) {
        return PlayerTopBarFrame(
          info: PlayerTopBarPlaybackInfo.fromController(controller),
        );
      },
    );
  }
}
