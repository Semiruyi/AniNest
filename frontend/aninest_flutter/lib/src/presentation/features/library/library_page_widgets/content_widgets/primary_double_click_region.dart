import 'package:flutter/gestures.dart';
import 'package:flutter/widgets.dart';

class PrimaryDoubleClickRegion extends StatefulWidget {
  const PrimaryDoubleClickRegion({
    super.key,
    required this.child,
    required this.onDoubleClick,
    this.maxInterval = const Duration(milliseconds: 350),
    this.maxDistance = 18,
  });

  final Widget child;
  final VoidCallback onDoubleClick;
  final Duration maxInterval;
  final double maxDistance;

  @override
  State<PrimaryDoubleClickRegion> createState() =>
      _PrimaryDoubleClickRegionState();
}

class _PrimaryDoubleClickRegionState extends State<PrimaryDoubleClickRegion> {
  Duration? _lastClickTime;
  Offset? _lastClickPosition;

  void _handlePointerDown(PointerDownEvent event) {
    if ((event.buttons & kPrimaryButton) == 0) {
      _resetClick();
      return;
    }

    final lastClickTime = _lastClickTime;
    final lastClickPosition = _lastClickPosition;
    final isDoubleClick =
        lastClickTime != null &&
        lastClickPosition != null &&
        event.timeStamp - lastClickTime <= widget.maxInterval &&
        (event.localPosition - lastClickPosition).distance <=
            widget.maxDistance;

    if (isDoubleClick) {
      _resetClick();
      widget.onDoubleClick();
      return;
    }

    _lastClickTime = event.timeStamp;
    _lastClickPosition = event.localPosition;
  }

  void _resetClick() {
    _lastClickTime = null;
    _lastClickPosition = null;
  }

  @override
  Widget build(BuildContext context) {
    return Listener(
      behavior: HitTestBehavior.translucent,
      onPointerDown: _handlePointerDown,
      child: widget.child,
    );
  }
}
