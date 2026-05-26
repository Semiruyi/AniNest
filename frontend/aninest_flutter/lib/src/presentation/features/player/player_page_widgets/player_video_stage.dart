import 'package:aninest_flutter/src/features/player/application/player_controller.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

import 'video_stage_widgets/player_video_viewport.dart';

class PlayerVideoStage extends StatelessWidget {
  const PlayerVideoStage({super.key, required this.controller});

  final PlayerController controller;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return ClipRRect(
      borderRadius: BorderRadius.circular(8),
      child: ColoredBox(
        color: colorScheme.background,
        child: PlayerVideoViewport(controller: controller),
      ),
    );
  }
}
