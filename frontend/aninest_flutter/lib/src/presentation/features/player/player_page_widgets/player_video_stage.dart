import 'package:aninest_flutter/src/app/app_controller.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

import 'video_stage_widgets/auto_hide_cursor_region.dart';
import 'video_stage_widgets/player_video_viewport.dart';
import 'player_view_state_controller.dart';

class PlayerVideoStage extends StatelessWidget {
  const PlayerVideoStage({
    super.key,
    required this.controller,
    required this.viewStateController,
    required this.onToggleFullscreen,
    this.isFullscreen = false,
  });

  final AppController controller;
  final PlayerViewStateController viewStateController;
  final bool isFullscreen;
  final VoidCallback onToggleFullscreen;

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: viewStateController,
      builder: (BuildContext context, Widget? child) {
        final colorScheme = Theme.of(context).colorScheme;
        final canTogglePlayback = viewStateController.canTogglePlayback;

        return AutoHideCursorRegion(
          visibleCursor: canTogglePlayback
              ? SystemMouseCursors.click
              : SystemMouseCursors.basic,
          child: GestureDetector(
            behavior: HitTestBehavior.opaque,
            onTap: canTogglePlayback ? controller.togglePlayPause : null,
            onDoubleTap: onToggleFullscreen,
            child: ClipRRect(
              borderRadius: BorderRadius.circular(isFullscreen ? 0 : 8),
              child: ColoredBox(
                color: colorScheme.background,
                child: PlayerVideoViewport(controller: controller),
              ),
            ),
          ),
        );
      },
    );
  }
}
