import 'package:aninest_flutter/src/features/player/application/player_controller.dart';
import 'package:flutter/widgets.dart';

import 'focus_context.dart';
import 'player_actions.dart';
import 'player_shortcuts.dart';

class PlayerShortcutScope extends StatefulWidget {
  const PlayerShortcutScope({
    super.key,
    required this.controller,
    required this.isActive,
    required this.isFullscreen,
    required this.onToggleFullscreen,
    required this.child,
  });

  final PlayerController controller;
  final bool isActive;
  final bool isFullscreen;
  final VoidCallback onToggleFullscreen;
  final Widget child;

  @override
  State<PlayerShortcutScope> createState() => _PlayerShortcutScopeState();
}

class _PlayerShortcutScopeState extends State<PlayerShortcutScope> {
  final FocusNode _focusNode = FocusNode(debugLabel: 'PlayerShortcutScope');

  @override
  void initState() {
    super.initState();
    _scheduleFocusRequestIfNeeded();
  }

  @override
  void didUpdateWidget(PlayerShortcutScope oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (!oldWidget.isActive && widget.isActive) {
      _scheduleFocusRequestIfNeeded();
    }
    if (oldWidget.isActive && !widget.isActive && _focusNode.hasFocus) {
      _focusNode.unfocus();
    }
  }

  @override
  void dispose() {
    _focusNode.dispose();
    super.dispose();
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
        child: Focus(
          focusNode: _focusNode,
          canRequestFocus: widget.isActive,
          skipTraversal: !widget.isActive,
          onKeyEvent: _handleKeyEvent,
          child: widget.child,
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

  void _scheduleFocusRequestIfNeeded() {
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted || !widget.isActive || FocusContext.hasEditableTextFocus()) {
        return;
      }

      _focusNode.requestFocus();
    });
  }
}
