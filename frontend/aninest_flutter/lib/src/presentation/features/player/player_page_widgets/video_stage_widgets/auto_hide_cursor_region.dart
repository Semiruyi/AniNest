import 'dart:async';

import 'package:flutter/services.dart';
import 'package:flutter/widgets.dart';

class AutoHideCursorRegion extends StatefulWidget {
  const AutoHideCursorRegion({
    super.key,
    required this.child,
    required this.visibleCursor,
    this.hiddenCursor = SystemMouseCursors.none,
    this.hideDelay = const Duration(seconds: 2),
  });

  final Widget child;
  final MouseCursor visibleCursor;
  final MouseCursor hiddenCursor;
  final Duration hideDelay;

  @override
  State<AutoHideCursorRegion> createState() => _AutoHideCursorRegionState();
}

class _AutoHideCursorRegionState extends State<AutoHideCursorRegion> {
  Timer? _hideTimer;
  bool _isPointerInside = false;
  bool _isCursorVisible = true;

  @override
  void dispose() {
    _hideTimer?.cancel();
    super.dispose();
  }

  void _handlePointerEntered(PointerEnterEvent event) {
    _isPointerInside = true;
    _showCursorAndScheduleHide();
  }

  void _handlePointerHovered(PointerHoverEvent event) {
    if (!_isPointerInside) {
      _isPointerInside = true;
    }
    _showCursorAndScheduleHide();
  }

  void _handlePointerExited(PointerExitEvent event) {
    _isPointerInside = false;
    _hideTimer?.cancel();
    if (!_isCursorVisible) {
      setState(() {
        _isCursorVisible = true;
      });
    }
  }

  void _showCursorAndScheduleHide() {
    _hideTimer?.cancel();
    if (!_isCursorVisible) {
      setState(() {
        _isCursorVisible = true;
      });
    }

    _hideTimer = Timer(widget.hideDelay, () {
      if (!mounted || !_isPointerInside) {
        return;
      }

      setState(() {
        _isCursorVisible = false;
      });
    });
  }

  @override
  Widget build(BuildContext context) {
    return MouseRegion(
      cursor: _isCursorVisible ? widget.visibleCursor : widget.hiddenCursor,
      onEnter: _handlePointerEntered,
      onHover: _handlePointerHovered,
      onExit: _handlePointerExited,
      child: widget.child,
    );
  }
}
