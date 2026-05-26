import 'dart:async';

import 'package:aninest_flutter/src/features/player/application/player_controller.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

import 'player_page_widgets/player_control_bar.dart';
import 'player_page_widgets/player_episode_panel.dart';
import 'player_page_widgets/player_top_bar.dart';
import 'player_page_widgets/player_video_stage.dart';

class PlayerPage extends StatefulWidget {
  const PlayerPage({
    super.key,
    required this.controller,
    required this.isActive,
  });

  final PlayerController controller;
  final bool isActive;

  @override
  State<PlayerPage> createState() => _PlayerPageState();
}

class _PlayerPageState extends State<PlayerPage> {
  @override
  void didUpdateWidget(PlayerPage oldWidget) {
    super.didUpdateWidget(oldWidget);

    if (oldWidget.isActive && !widget.isActive) {
      unawaited(widget.controller.pause());
    }
  }

  @override
  void dispose() {
    if (widget.isActive) {
      unawaited(widget.controller.pause());
    }
    super.dispose();
  }

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
                      Expanded(
                        child: PlayerVideoStage(controller: widget.controller),
                      ),
                      SizedBox(
                        height: 70,
                        child: PlayerControlBar(controller: widget.controller),
                      ),
                    ],
                  ),
                ),
                ResizablePane(
                  initialSize: 320,
                  minSize: 240,
                  maxSize: 440,
                  child: PlayerEpisodePanel(controller: widget.controller),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
