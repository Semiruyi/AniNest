import 'dart:async';

import 'package:aninest_flutter/src/features/player/application/player_controller.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

import 'player_page_widgets/player_playback_layout.dart';
import 'player_page_widgets/player_top_bar.dart';

class PlayerPage extends StatefulWidget {
  const PlayerPage({
    super.key,
    required this.controller,
    required this.isActive,
    required this.isFullscreen,
    required this.onToggleFullscreen,
  });

  final PlayerController controller;
  final bool isActive;
  final bool isFullscreen;
  final VoidCallback onToggleFullscreen;

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
    final pagePadding = widget.isFullscreen
        ? EdgeInsets.zero
        : const EdgeInsets.all(12);

    return Container(
      color: colorScheme.background,
      padding: pagePadding,
      child: Column(
        children: <Widget>[
          if (!widget.isFullscreen)
            SizedBox(
              height: 38,
              child: PlayerTopBar(controller: widget.controller),
            ),
          Expanded(
            child: widget.isFullscreen
                ? PlayerFullscreenPlaybackLayout(
                    controller: widget.controller,
                    isActive: widget.isActive,
                    onToggleFullscreen: widget.onToggleFullscreen,
                  )
                : PlayerStandardPlaybackLayout(
                    controller: widget.controller,
                    isActive: widget.isActive,
                    onToggleFullscreen: widget.onToggleFullscreen,
                  ),
          ),
        ],
      ),
    );
  }
}
