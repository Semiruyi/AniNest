import 'package:flutter/services.dart';
import 'package:flutter/widgets.dart';

class FocusablePressableState {
  const FocusablePressableState({
    required this.isHovered,
    required this.isFocused,
    required this.isPressed,
  });

  final bool isHovered;
  final bool isFocused;
  final bool isPressed;
}

typedef FocusablePressableBuilder =
    Widget Function(BuildContext context, FocusablePressableState state);

class FocusablePressable extends StatefulWidget {
  const FocusablePressable({
    super.key,
    required this.builder,
    this.focusNode,
    this.autofocus = false,
    this.enabled = true,
    this.onPressed,
    this.onFocusChange,
    this.mouseCursor,
  });

  final FocusablePressableBuilder builder;
  final FocusNode? focusNode;
  final bool autofocus;
  final bool enabled;
  final VoidCallback? onPressed;
  final ValueChanged<bool>? onFocusChange;
  final MouseCursor? mouseCursor;

  @override
  State<FocusablePressable> createState() => _FocusablePressableState();
}

class _FocusablePressableState extends State<FocusablePressable> {
  static const Map<ShortcutActivator, Intent> _activationShortcuts =
      <ShortcutActivator, Intent>{
        SingleActivator(LogicalKeyboardKey.enter): ActivateIntent(),
      };

  bool _hovered = false;
  bool _focused = false;
  bool _pressed = false;
  FocusNode? _internalFocusNode;

  bool get _isInteractive => widget.enabled && widget.onPressed != null;
  FocusNode get _effectiveFocusNode => widget.focusNode ?? _internalFocusNode!;

  @override
  void initState() {
    super.initState();
    if (widget.focusNode == null) {
      _internalFocusNode = FocusNode();
    }
  }

  @override
  void didUpdateWidget(FocusablePressable oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.focusNode == null && widget.focusNode != null) {
      _internalFocusNode?.dispose();
      _internalFocusNode = null;
    } else if (oldWidget.focusNode != null && widget.focusNode == null) {
      _internalFocusNode = FocusNode();
    }
  }

  @override
  void dispose() {
    _internalFocusNode?.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Semantics(
      button: true,
      enabled: _isInteractive,
      child: FocusableActionDetector(
        enabled: _isInteractive,
        autofocus: widget.autofocus,
        focusNode: _effectiveFocusNode,
        mouseCursor:
            widget.mouseCursor ??
            (_isInteractive
                ? SystemMouseCursors.click
                : SystemMouseCursors.basic),
        shortcuts: _activationShortcuts,
        actions: <Type, Action<Intent>>{
          ActivateIntent: CallbackAction<ActivateIntent>(
            onInvoke: (ActivateIntent intent) {
              if (!_isInteractive) {
                return null;
              }

              _effectiveFocusNode.requestFocus();
              widget.onPressed?.call();
              return null;
            },
          ),
        },
        onShowFocusHighlight: (bool value) {
          if (_focused == value) {
            return;
          }

          setState(() {
            _focused = value;
          });
          widget.onFocusChange?.call(value);
        },
        child: MouseRegion(
          onEnter: _isInteractive
              ? (_) => setState(() {
                  _hovered = true;
                })
              : null,
          onExit: _isInteractive
              ? (_) => setState(() {
                  _hovered = false;
                  _pressed = false;
                })
              : null,
          child: GestureDetector(
            behavior: HitTestBehavior.opaque,
            onTapDown: _isInteractive
                ? (_) {
                    _effectiveFocusNode.requestFocus();
                    setState(() {
                      _pressed = true;
                    });
                  }
                : null,
            onTapCancel: _isInteractive
                ? () => setState(() {
                    _pressed = false;
                  })
                : null,
            onTapUp: _isInteractive
                ? (_) => setState(() {
                    _pressed = false;
                  })
                : null,
            onTap: _isInteractive ? widget.onPressed : null,
            child: widget.builder(
              context,
              FocusablePressableState(
                isHovered: _hovered,
                isFocused: _focused,
                isPressed: _pressed,
              ),
            ),
          ),
        ),
      ),
    );
  }
}
