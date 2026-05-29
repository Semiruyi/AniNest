import 'package:aninest_flutter/src/features/player/application/player_controller.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

import 'player_selector.dart';
import 'video_stage_widgets/auto_hide_cursor_region.dart';
import 'video_stage_widgets/player_video_viewport.dart';

class PlayerVideoStage extends StatelessWidget {
  const PlayerVideoStage({
    super.key,
    required this.controller,
    required this.onToggleFullscreen,
    this.isFullscreen = false,
  });

  final PlayerController controller;
  final bool isFullscreen;
  final VoidCallback onToggleFullscreen;

  @override
  Widget build(BuildContext context) {
    return PlayerSelector<bool>(
      controller: controller,
      selector: (state) => state.canTogglePlayback,
      builder: (BuildContext context, canTogglePlayback) {
        final colorScheme = Theme.of(context).colorScheme;

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
