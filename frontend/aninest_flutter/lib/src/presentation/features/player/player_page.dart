import 'package:aninest_flutter/src/features/player/application/player_controller.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

import 'player_page_widgets/player_control_bar.dart';
import 'player_page_widgets/player_episode_panel.dart';
import 'player_page_widgets/player_top_bar.dart';
import 'player_page_widgets/player_video_stage.dart';

class PlayerPage extends StatelessWidget {
  const PlayerPage({super.key, required this.controller});

  final PlayerController controller;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return Container(
      color: colorScheme.background,
      padding: const EdgeInsets.all(12),
      child: Column(
        children: <Widget>[
          const SizedBox(height: 38, child: PlayerTopBar()),
          Expanded(
            child: ResizablePanel.horizontal(
              draggerBuilder: ResizablePanel.defaultDraggerBuilder,
              draggerThickness: 12,
              children: <ResizablePane>[
                ResizablePane.flex(
                  minSize: 360,
                  child: Column(
                    children: <Widget>[
                      Expanded(child: PlayerVideoStage(controller: controller)),
                      SizedBox(
                        height: 70,
                        child: PlayerControlBar(controller: controller),
                      ),
                    ],
                  ),
                ),
                ResizablePane(
                  initialSize: 320,
                  minSize: 240,
                  maxSize: 440,
                  child: const PlayerEpisodePanel(),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
