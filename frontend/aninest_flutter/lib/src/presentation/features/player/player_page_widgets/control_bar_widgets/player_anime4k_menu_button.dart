import 'dart:async';

import 'package:aninest_flutter/src/app/app_controller.dart';
import 'package:aninest_flutter/src/features/player/application/player_anime4k_shader_support.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

import 'player_anime4k_menu.dart';

class PlayerAnime4kMenuButton extends StatefulWidget {
  const PlayerAnime4kMenuButton({super.key, required this.controller});

  final AppController controller;

  @override
  State<PlayerAnime4kMenuButton> createState() =>
      _PlayerAnime4kMenuButtonState();
}

class _PlayerAnime4kMenuButtonState extends State<PlayerAnime4kMenuButton> {
  final PopoverController _popoverController = PopoverController();
  bool _hovered = false;
  bool _pressed = false;

  @override
  void dispose() {
    _popoverController.dispose();
    super.dispose();
  }

  void _togglePopover() {
    if (_popoverController.hasOpenPopover) {
      _popoverController.close();
      return;
    }

    unawaited(
      _popoverController.show<void>(
        context: context,
        alignment: Alignment.bottomCenter,
        anchorAlignment: Alignment.topCenter,
        offset: const Offset(0, -8),
        modal: true,
        dismissBackdropFocus: false,
        builder: (BuildContext context) {
          return PlayerAnime4kMenu(
            selectedMode: widget.controller.playerRuntime.anime4kMode,
            onModeSelected: (mode) {
              unawaited(widget.controller.setAnime4kMode(mode));
            },
            onDismissRequested: _popoverController.close,
          );
        },
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: widget.controller,
      builder: (BuildContext context, Widget? child) {
        final colorScheme = Theme.of(context).colorScheme;
        final mode = widget.controller.playerRuntime.anime4kMode;
        final isInteractive = PlayerAnime4kShaderSupport.isSupported;
        final backgroundColor = !isInteractive
            ? Colors.transparent
            : _pressed
            ? colorScheme.foreground.withValues(alpha: 0.18)
            : _hovered
            ? colorScheme.foreground.withValues(alpha: 0.12)
            : mode.isEnabled
            ? colorScheme.primary.withValues(alpha: 0.18)
            : Colors.transparent;
        final foregroundColor = !isInteractive
            ? colorScheme.mutedForeground
            : mode.isEnabled
            ? colorScheme.primary
            : colorScheme.foreground;

        return Tooltip(
          tooltip: (context) => TooltipContainer(
            child: Text(
              isInteractive
                  ? mode.label
                  : 'Anime4K unsupported on this platform',
            ),
          ),
          child: MouseRegion(
            cursor: isInteractive
                ? SystemMouseCursors.click
                : SystemMouseCursors.basic,
            onEnter: isInteractive
                ? (_) => setState(() => _hovered = true)
                : null,
            onExit: isInteractive
                ? (_) => setState(() {
                    _hovered = false;
                    _pressed = false;
                  })
                : null,
            child: GestureDetector(
              behavior: HitTestBehavior.opaque,
              onTapDown: isInteractive
                  ? (_) => setState(() => _pressed = true)
                  : null,
              onTapCancel: isInteractive
                  ? () => setState(() => _pressed = false)
                  : null,
              onTapUp: isInteractive
                  ? (_) => setState(() => _pressed = false)
                  : null,
              onTap: isInteractive ? _togglePopover : null,
              child: AnimatedContainer(
                duration: const Duration(milliseconds: 120),
                curve: Curves.easeOut,
                height: 30,
                padding: const EdgeInsets.symmetric(horizontal: 10),
                decoration: BoxDecoration(
                  color: backgroundColor,
                  borderRadius: BorderRadius.circular(8),
                ),
                child: Center(
                  child: Text(
                    mode.shortLabel,
                    style: TextStyle(color: foregroundColor, fontSize: 12),
                  ).medium(),
                ),
              ),
            ),
          ),
        );
      },
    );
  }
}
