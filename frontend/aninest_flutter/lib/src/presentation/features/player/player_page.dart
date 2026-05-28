import 'dart:async';

import 'package:aninest_flutter/src/app/app_controller.dart';
import 'package:flutter/services.dart';
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

  final AppController controller;
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

    return Shortcuts(
      shortcuts: const <ShortcutActivator, Intent>{
        SingleActivator(LogicalKeyboardKey.escape): _ExitFullscreenIntent(),
      },
      child: Actions(
        actions: <Type, Action<Intent>>{
          _ExitFullscreenIntent: CallbackAction<_ExitFullscreenIntent>(
            onInvoke: (_ExitFullscreenIntent intent) {
              if (widget.isFullscreen) {
                widget.onToggleFullscreen();
              }
              return null;
            },
          ),
        },
        child: Focus(
          autofocus: widget.isActive,
          child: Container(
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
                          onToggleFullscreen: widget.onToggleFullscreen,
                        )
                      : PlayerStandardPlaybackLayout(
                          controller: widget.controller,
                          onToggleFullscreen: widget.onToggleFullscreen,
                        ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

class _ExitFullscreenIntent extends Intent {
  const _ExitFullscreenIntent();
}
