import 'package:aninest_flutter/src/features/player/application/player_controller.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

import 'fullscreen_player_chrome.dart';
import 'player_control_bar.dart';
import 'player_episode_panel.dart';
import 'player_video_stage.dart';

class PlayerStandardPlaybackLayout extends StatelessWidget {
  const PlayerStandardPlaybackLayout({
    super.key,
    required this.controller,
    required this.onToggleFullscreen,
  });

  final PlayerController controller;
  final VoidCallback onToggleFullscreen;

  @override
  Widget build(BuildContext context) {
    return ResizablePanel.horizontal(
      draggerBuilder: ResizablePanel.defaultDraggerBuilder,
      draggerThickness: 12,
      children: <ResizablePane>[
        ResizablePane.flex(
          minSize: 360,
          child: Column(
            children: <Widget>[
              Expanded(
                child: PlayerVideoStage(
                  controller: controller,
                  onToggleFullscreen: onToggleFullscreen,
                ),
              ),
              SizedBox(
                height: 70,
                child: PlayerControlBar(
                  controller: controller,
                  onToggleFullscreen: onToggleFullscreen,
                ),
              ),
            ],
          ),
        ),
        ResizablePane(
          initialSize: 320,
          minSize: 240,
          maxSize: 440,
          child: PlayerEpisodePanel(controller: controller),
        ),
      ],
    );
  }
}

class PlayerFullscreenPlaybackLayout extends StatelessWidget {
  const PlayerFullscreenPlaybackLayout({
    super.key,
    required this.controller,
    required this.onToggleFullscreen,
  });

  final PlayerController controller;
  final VoidCallback onToggleFullscreen;

  @override
  Widget build(BuildContext context) {
    return FullscreenPlayerChrome(
      video: PlayerVideoStage(
        controller: controller,
        isFullscreen: true,
        onToggleFullscreen: onToggleFullscreen,
      ),
      controlBar: PlayerControlBar(
        controller: controller,
        isFullscreen: true,
        onToggleFullscreen: onToggleFullscreen,
      ),
    );
  }
}
