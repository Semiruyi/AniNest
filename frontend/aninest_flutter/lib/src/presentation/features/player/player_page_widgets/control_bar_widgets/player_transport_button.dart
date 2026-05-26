import 'package:shadcn_flutter/shadcn_flutter.dart';

class PlayerTransportButton extends StatefulWidget {
  const PlayerTransportButton({
    super.key,
    required this.tooltip,
    required this.icon,
    this.iconSize = 32,
    this.buttonSize = 35,
    this.borderRadius = 8,
    this.onTap,
    this.enabled = true,
  });

  final String tooltip;
  final IconData icon;
  final double iconSize;
  final double buttonSize;
  final double borderRadius;
  final VoidCallback? onTap;
  final bool enabled;

  @override
  State<PlayerTransportButton> createState() => _PlayerTransportButtonState();
}

class _PlayerTransportButtonState extends State<PlayerTransportButton> {
  bool _hovered = false;
  bool _pressed = false;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;
    final isInteractive = widget.enabled && widget.onTap != null;
    final backgroundColor = !isInteractive
        ? Colors.transparent
        : _pressed
            ? colorScheme.foreground.withValues(alpha: 0.18)
            : _hovered
                ? colorScheme.foreground.withValues(alpha: 0.12)
                : Colors.transparent;
    final iconColor = isInteractive
        ? colorScheme.foreground
        : colorScheme.mutedForeground;

    return Tooltip(
      tooltip: (context) => TooltipContainer(child: Text(widget.tooltip)),
      child: MouseRegion(
        cursor: isInteractive
            ? SystemMouseCursors.click
            : SystemMouseCursors.basic,
        onEnter: isInteractive ? (_) => setState(() => _hovered = true) : null,
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
          onTap: isInteractive ? widget.onTap : null,
          child: AnimatedContainer(
            duration: const Duration(milliseconds: 120),
            curve: Curves.easeOut,
            width: widget.buttonSize,
            height: widget.buttonSize,
            decoration: BoxDecoration(
              color: backgroundColor,
              borderRadius: BorderRadius.circular(widget.borderRadius),
            ),
            child: Center(
              child: Icon(
                widget.icon,
                size: widget.iconSize,
                color: iconColor,
              ),
            ),
          ),
        ),
      ),
    );
  }
}
