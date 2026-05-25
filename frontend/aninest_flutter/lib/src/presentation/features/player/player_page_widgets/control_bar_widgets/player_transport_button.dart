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
  });

  final String tooltip;
  final IconData icon;
  final double iconSize;
  final double buttonSize;
  final double borderRadius;
  final VoidCallback? onTap;

  @override
  State<PlayerTransportButton> createState() => _PlayerTransportButtonState();
}

class _PlayerTransportButtonState extends State<PlayerTransportButton> {
  bool _hovered = false;
  bool _pressed = false;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;
    final backgroundColor = _pressed
        ? colorScheme.foreground.withValues(alpha: 0.18)
        : _hovered
            ? colorScheme.foreground.withValues(alpha: 0.12)
            : Colors.transparent;

    return Tooltip(
      tooltip: (context) => TooltipContainer(child: Text(widget.tooltip)),
      child: MouseRegion(
        cursor: SystemMouseCursors.click,
        onEnter: (_) => setState(() => _hovered = true),
        onExit: (_) => setState(() {
          _hovered = false;
          _pressed = false;
        }),
        child: GestureDetector(
          behavior: HitTestBehavior.opaque,
          onTapDown: (_) => setState(() => _pressed = true),
          onTapCancel: () => setState(() => _pressed = false),
          onTapUp: (_) => setState(() => _pressed = false),
          onTap: widget.onTap,
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
                color: colorScheme.foreground,
              ),
            ),
          ),
        ),
      ),
    );
  }
}
