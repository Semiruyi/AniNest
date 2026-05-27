import 'package:flutter/services.dart';
import 'package:shadcn_flutter/shadcn_flutter.dart';

class FullscreenPlayerChrome extends StatefulWidget {
  const FullscreenPlayerChrome({
    super.key,
    required this.video,
    required this.controlBar,
  });

  final Widget video;
  final Widget controlBar;

  @override
  State<FullscreenPlayerChrome> createState() => _FullscreenPlayerChromeState();
}

class _FullscreenPlayerChromeState extends State<FullscreenPlayerChrome> {
  static const double _controlBarHeight = 70;
  static const double _revealHotZoneHeight = 96;
  static const Duration _animationDuration = Duration(milliseconds: 160);

  bool _isControlBarVisible = false;

  void _handlePointerEntered(PointerEnterEvent event, double height) {
    _syncControlBarVisibility(event.localPosition.dy, height);
  }

  void _handlePointerHovered(PointerHoverEvent event, double height) {
    _syncControlBarVisibility(event.localPosition.dy, height);
  }

  void _handlePointerExited(PointerExitEvent event) {
    _setControlBarVisible(false);
  }

  void _syncControlBarVisibility(double pointerY, double height) {
    _setControlBarVisible(pointerY >= height - _revealHotZoneHeight);
  }

  void _setControlBarVisible(bool value) {
    if (_isControlBarVisible == value) {
      return;
    }

    setState(() {
      _isControlBarVisible = value;
    });
  }

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;

    return LayoutBuilder(
      builder: (BuildContext context, BoxConstraints constraints) {
        return MouseRegion(
          cursor: SystemMouseCursors.basic,
          onEnter: (PointerEnterEvent event) =>
              _handlePointerEntered(event, constraints.maxHeight),
          onHover: (PointerHoverEvent event) =>
              _handlePointerHovered(event, constraints.maxHeight),
          onExit: _handlePointerExited,
          child: Stack(
            fit: StackFit.expand,
            clipBehavior: Clip.hardEdge,
            children: <Widget>[
              Positioned.fill(child: widget.video),
              Positioned(
                left: 0,
                right: 0,
                bottom: 0,
                height: _controlBarHeight,
                child: IgnorePointer(
                  ignoring: !_isControlBarVisible,
                  child: AnimatedOpacity(
                    opacity: _isControlBarVisible ? 1 : 0,
                    duration: _animationDuration,
                    curve: Curves.easeOut,
                    child: AnimatedSlide(
                      offset: _isControlBarVisible
                          ? Offset.zero
                          : const Offset(0, 1),
                      duration: _animationDuration,
                      curve: Curves.easeOut,
                      child: DecoratedBox(
                        decoration: BoxDecoration(
                          color: colorScheme.card.withValues(alpha: 0.92),
                          border: Border(
                            top: BorderSide(
                              color: colorScheme.border.withValues(alpha: 0.7),
                            ),
                          ),
                        ),
                        child: widget.controlBar,
                      ),
                    ),
                  ),
                ),
              ),
            ],
          ),
        );
      },
    );
  }
}
