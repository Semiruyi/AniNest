import 'package:aninest_flutter/src/app/app_controller.dart';
import 'package:aninest_flutter/src/presentation/keyboard/player_shortcut_scope.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

import 'fullscreen_player_chrome.dart';
import 'player_control_bar.dart';
import 'player_episode_panel.dart';
import 'player_video_stage.dart';
import 'player_view_state_controller.dart';

class PlayerStandardPlaybackLayout extends StatelessWidget {
  const PlayerStandardPlaybackLayout({
    super.key,
    required this.controller,
    required this.viewStateController,
    required this.isActive,
    required this.onToggleFullscreen,
  });

  final AppController controller;
  final PlayerViewStateController viewStateController;
  final bool isActive;
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
                  viewStateController: viewStateController,
                  onToggleFullscreen: onToggleFullscreen,
                ),
              ),
              SizedBox(
                height: 70,
                child: PlayerShortcutScope(
                  controller: controller,
                  isActive: isActive,
                  isFullscreen: false,
                  onToggleFullscreen: onToggleFullscreen,
                  child: PlayerControlBar(
                    controller: controller,
                    onToggleFullscreen: onToggleFullscreen,
                  ),
                ),
              ),
            ],
          ),
        ),
        ResizablePane(
          initialSize: 320,
          minSize: 240,
          maxSize: 440,
          child: PlayerEpisodePanel(controller: viewStateController),
        ),
      ],
    );
  }
}

class PlayerFullscreenPlaybackLayout extends StatelessWidget {
  const PlayerFullscreenPlaybackLayout({
    super.key,
    required this.controller,
    required this.viewStateController,
    required this.isActive,
    required this.onToggleFullscreen,
  });

  final AppController controller;
  final PlayerViewStateController viewStateController;
  final bool isActive;
  final VoidCallback onToggleFullscreen;

  @override
  Widget build(BuildContext context) {
    return FullscreenPlayerChrome(
      video: PlayerVideoStage(
        controller: controller,
        viewStateController: viewStateController,
        isFullscreen: true,
        onToggleFullscreen: onToggleFullscreen,
      ),
      controlBar: PlayerShortcutScope(
        controller: controller,
        isActive: isActive,
        isFullscreen: true,
        onToggleFullscreen: onToggleFullscreen,
        child: PlayerControlBar(
          controller: controller,
          isFullscreen: true,
          onToggleFullscreen: onToggleFullscreen,
        ),
      ),
    );
  }
}
