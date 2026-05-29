import 'package:aninest_flutter/src/features/player/application/player_controller.dart';
import 'package:flutter/widgets.dart';

import 'focus_context.dart';
import 'player_actions.dart';
import 'player_focus_controller.dart';
import 'player_shortcuts.dart';

class PlayerFocusScope extends StatefulWidget {
  const PlayerFocusScope({
    super.key,
    required this.controller,
    required this.focusController,
    required this.isActive,
    required this.isFullscreen,
    required this.onToggleFullscreen,
    required this.child,
  });

  final PlayerController controller;
  final PlayerFocusController focusController;
  final bool isActive;
  final bool isFullscreen;
  final VoidCallback onToggleFullscreen;
  final Widget child;

  @override
  State<PlayerFocusScope> createState() => _PlayerFocusScopeState();
}

class _PlayerFocusScopeState extends State<PlayerFocusScope> {
  @override
  void initState() {
    super.initState();
    _schedulePrimaryFocusIfNeeded();
  }

  @override
  void didUpdateWidget(PlayerFocusScope oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (!oldWidget.isActive && widget.isActive) {
      _schedulePrimaryFocusIfNeeded();
    } else if (oldWidget.isActive && !widget.isActive) {
      widget.focusController.releaseFocus();
    }
  }

  @override
  Widget build(BuildContext context) {
    return Shortcuts(
      shortcuts: widget.isActive
          ? PlayerShortcuts.bindings
          : const <ShortcutActivator, Intent>{},
      child: Actions(
        actions: buildPlayerActions(
          controller: widget.controller,
          isFullscreen: widget.isFullscreen,
          onToggleFullscreen: widget.onToggleFullscreen,
        ),
        child: Listener(
          behavior: HitTestBehavior.translucent,
          onPointerDown: _handlePointerDown,
          child: Focus(
            focusNode: widget.focusController.focusNode,
            autofocus: widget.isActive,
            canRequestFocus: widget.isActive,
            skipTraversal: !widget.isActive,
            onKeyEvent: _handleKeyEvent,
            child: widget.child,
          ),
        ),
      ),
    );
  }

  KeyEventResult _handleKeyEvent(FocusNode node, KeyEvent event) {
    if (!widget.isActive || FocusContext.hasEditableTextFocus()) {
      return KeyEventResult.ignored;
    }

    return KeyEventResult.ignored;
  }

  void _handlePointerDown(PointerDownEvent event) {
    if (!widget.isActive ||
        FocusContext.hasEditableTextFocus() ||
        widget.focusController.hasFocus) {
      return;
    }

    widget.focusController.requestPrimaryFocus();
  }

  void _schedulePrimaryFocusIfNeeded() {
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted || !widget.isActive || FocusContext.hasEditableTextFocus()) {
        return;
      }

      widget.focusController.requestPrimaryFocus();
    });
  }
}
